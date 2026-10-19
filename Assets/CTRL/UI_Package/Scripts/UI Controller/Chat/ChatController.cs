using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Backend.Bootstrap;
using Backend.Interfaces;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-19] Logic chat ngoai menu (khong ve UI - ChatBoxView lo phan hien thi).
    ///  - Kenh "party": tin cua to doi hien tai (chi bat khi to doi co tu 2 nguoi).
    ///  - Kenh "dm:&lt;PlayFabId&gt;": nhan rieng voi tung ban be.
    /// Polling PlayFab (IChatService): to doi moi ~2.5s; cuoc tro chuyen dang mo moi ~3s;
    /// cac ban online/away luan phien moi ~4s de bat tin moi (cham chua doc). Ban offline chi quet 1 lan khi mo.
    /// Chi chay khi object dang bat (tab CHOI) -> khong ton API khi o tab khac; tin van con trong hop thu di nen
    /// quay lai la keo ve du.
    /// </summary>
    public class ChatController : MonoBehaviour
    {
        public const string PartyKey = "party";
        public const string DmPrefix = "dm:";
        public const int MaxTextLength = 120;

        [SerializeField] private PartyController _party;
        [SerializeField] private float _partyPoll = 2.5f;
        [SerializeField] private float _activeDmPoll = 3f;
        [SerializeField] private float _backgroundDmPoll = 4f;
        [SerializeField] private float _friendsRefresh = 30f;
        [SerializeField] private float _minSendInterval = 0.6f;

        private const int PartyOutboxCap = 20;     // tin cua minh giu trong hop thu party
        private const int DirectOutboxCap = 30;    // tong tin nhan rieng giu trong hop thu (moi ban toi da 10)
        private const int DirectPerFriendCap = 10;
        private const int ConversationCap = 100;   // tin hien thi toi da moi kenh

        public class Conversation
        {
            public string Key;
            public readonly List<ChatMessage> Messages = new List<ChatMessage>();
            public readonly HashSet<string> Ids = new HashSet<string>();
            public long LastRead;
            public int Unread;
        }

        public event Action<string> ConversationChanged;   // key kenh vua co tin moi / doi
        public event Action UnreadChanged;
        public event Action FriendsChanged;
        public event Action PartyChanged;

        private readonly Dictionary<string, Conversation> _convos = new Dictionary<string, Conversation>();
        private readonly List<ChatMessage> _myPartyOut = new List<ChatMessage>();
        private readonly List<ChatMessage> _myDirectOut = new List<ChatMessage>();
        private readonly List<FriendInfo> _friends = new List<FriendInfo>();
        private readonly HashSet<string> _scannedOnce = new HashSet<string>();
        private string _partyCode;
        private string _visibleKey;          // kenh dang hien tren man hinh (null = thu gon)
        private bool _directLoaded;
        private float _lastSend;
        private int _bgCursor;
        private Coroutine _loop;

        private static IChatService Svc { get { return BackendBootstrap.ChatService; } }
        public string SelfId { get { return BackendBootstrap.AuthService != null ? BackendBootstrap.AuthService.CurrentUserId : null; } }
        private static string SelfName()
        {
            var p = ProfileCache.Cached;
            return (p != null && !string.IsNullOrEmpty(p.DisplayName)) ? p.DisplayName : "PLAYER";
        }

        public IList<FriendInfo> Friends { get { return _friends; } }
        public bool PartyAvailable { get { return _party != null && _party.InParty && _party.State != null && _party.State.Members != null && _party.State.Members.Count > 1; } }

        public int TotalUnread
        {
            get { int n = 0; foreach (var c in _convos.Values) n += c.Unread; return n; }
        }
        public int UnreadOf(string key) { Conversation c; return _convos.TryGetValue(key, out c) ? c.Unread : 0; }
        public int UnreadDirect
        {
            get { int n = 0; foreach (var kv in _convos) if (kv.Key.StartsWith(DmPrefix)) n += kv.Value.Unread; return n; }
        }

        public static string DmKey(string friendId) { return DmPrefix + friendId; }

        public string FriendName(string friendId)
        {
            foreach (var f in _friends) if (f.PlayFabId == friendId) return string.IsNullOrEmpty(f.DisplayName) ? friendId : f.DisplayName;
            return friendId;
        }

        public IList<ChatMessage> Messages(string key) { return Get(key).Messages; }

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            if (_party == null) _party = FindFirstObjectByType<PartyController>(FindObjectsInactive.Include);
        }

        private void OnEnable() { if (_loop == null) _loop = StartCoroutine(Loop()); }
        private void OnDisable() { if (_loop != null) { StopCoroutine(_loop); _loop = null; } }

        /// <summary>View bao kenh dang duoc xem (null khi thu gon) -> danh dau da doc + uu tien polling kenh do.</summary>
        public void SetVisible(string key)
        {
            _visibleKey = key;
            if (!string.IsNullOrEmpty(key)) MarkRead(key);
        }

        // ------------------------------------------------------------------ gui tin
        /// <summary>Gui tin vao kenh. Tra ve loi (VN) neu khong gui duoc, null neu OK.</summary>
        public string Send(string key, string text)
        {
            text = Clean(text);
            if (string.IsNullOrEmpty(text)) return null;
            string self = SelfId;
            if (string.IsNullOrEmpty(self) || Svc == null) return "Chưa đăng nhập.";
            if (Time.unscaledTime - _lastSend < _minSendInterval) return "Gửi chậm lại một chút nhé.";
            _lastSend = Time.unscaledTime;

            var msg = new ChatMessage { f = self, n = SelfName(), x = text, t = NowMs() };
            if (key == PartyKey)
            {
                if (!PartyAvailable) return "Mời bạn vào tổ đội để chat cùng nhau.";
                msg.o = "";
                _myPartyOut.Add(msg);
                while (_myPartyOut.Count > PartyOutboxCap) _myPartyOut.RemoveAt(0);
                Svc.PublishPartyOutbox(_partyCode, new List<ChatMessage>(_myPartyOut), null,
                    e => Debug.LogWarning("[Chat] Gui tin to doi loi: " + (e != null ? e.ErrorMessage : "?")));
            }
            else if (key.StartsWith(DmPrefix))
            {
                msg.o = key.Substring(DmPrefix.Length);
                _myDirectOut.Add(msg);
                TrimDirectOutbox();
                Svc.PublishDirectOutbox(new List<ChatMessage>(_myDirectOut), null,
                    e => Debug.LogWarning("[Chat] Gui tin rieng loi: " + (e != null ? e.ErrorMessage : "?")));
            }
            else return null;

            Merge(key, new List<ChatMessage> { msg });
            return null;
        }

        private void TrimDirectOutbox()
        {
            _myDirectOut.Sort((a, b) => a.t.CompareTo(b.t));
            // moi ban toi da DirectPerFriendCap tin moi nhat
            var perFriend = new Dictionary<string, int>();
            for (int i = _myDirectOut.Count - 1; i >= 0; i--)
            {
                string to = _myDirectOut[i].o ?? "";
                int n; perFriend.TryGetValue(to, out n); n++; perFriend[to] = n;
                if (n > DirectPerFriendCap) _myDirectOut.RemoveAt(i);
            }
            while (_myDirectOut.Count > DirectOutboxCap) _myDirectOut.RemoveAt(0);
        }

        private static string Clean(string s)
        {
            if (s == null) return null;
            s = s.Replace("\n", " ").Replace("\r", " ").Trim();
            if (s.Length > MaxTextLength) s = s.Substring(0, MaxTextLength);
            return s;
        }

        private static long NowMs() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

        // ------------------------------------------------------------------ polling
        private IEnumerator Loop()
        {
            // cho dang nhap xong
            while (string.IsNullOrEmpty(SelfId) || Svc == null) yield return new WaitForSecondsRealtime(1f);
            if (!_directLoaded) LoadMyDirectOutbox();

            float nextParty = 0f, nextActive = 0f, nextBg = 1f, nextFriends = 0f;
            var tick = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                float now = Time.unscaledTime;
                if (now >= nextFriends) { nextFriends = now + _friendsRefresh; RefreshFriends(); }
                if (now >= nextParty) { nextParty = now + _partyPoll; PollParty(); }
                if (now >= nextActive)
                {
                    nextActive = now + _activeDmPoll;
                    if (!string.IsNullOrEmpty(_visibleKey) && _visibleKey.StartsWith(DmPrefix)) PollFriend(_visibleKey.Substring(DmPrefix.Length));
                }
                if (now >= nextBg) { nextBg = now + _backgroundDmPoll; PollNextFriend(); }
                yield return tick;
            }
        }

        private void LoadMyDirectOutbox()
        {
            string self = SelfId;
            Svc.FetchDirectOutbox(self, list =>
            {
                _directLoaded = true;
                _myDirectOut.Clear();
                foreach (var m in list) if (m.f == self && !string.IsNullOrEmpty(m.o)) _myDirectOut.Add(m);
                TrimDirectOutbox();
                var byFriend = new Dictionary<string, List<ChatMessage>>();
                foreach (var m in _myDirectOut)
                {
                    List<ChatMessage> l; if (!byFriend.TryGetValue(m.o, out l)) { l = new List<ChatMessage>(); byFriend[m.o] = l; }
                    l.Add(m);
                }
                foreach (var kv in byFriend) Merge(DmKey(kv.Key), kv.Value);
            }, e => { _directLoaded = true; Debug.LogWarning("[Chat] Khong doc duoc hop thu rieng: " + (e != null ? e.ErrorMessage : "?")); });
        }

        private void RefreshFriends()
        {
            var fs = BackendBootstrap.FriendsService;
            if (fs == null) return;
            fs.GetFriendsWithPresence(list =>
            {
                _friends.Clear();
                if (list != null) _friends.AddRange(list);
                // online truoc, roi away, roi offline; cung nhom thi theo ten
                _friends.Sort((a, b) =>
                {
                    int c = ((int)b.Presence).CompareTo((int)a.Presence);
                    return c != 0 ? c : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
                });
                if (FriendsChanged != null) FriendsChanged();
            }, e => Debug.LogWarning("[Chat] Khong lay duoc ban be: " + (e != null ? e.ErrorMessage : "?")));
        }

        private void PollParty()
        {
            bool avail = PartyAvailable;
            string code = avail ? _party.Code : null;
            if (code != _partyCode)
            {
                // doi to doi (hoac roi to doi) -> kenh to doi bat dau lai
                _partyCode = code;
                _myPartyOut.Clear();
                var c = Get(PartyKey); c.Messages.Clear(); c.Ids.Clear(); c.Unread = 0;
                if (ConversationChanged != null) ConversationChanged(PartyKey);
                if (UnreadChanged != null) UnreadChanged();
                if (PartyChanged != null) PartyChanged();
            }
            if (!avail) return;
            var ids = new List<string>();
            foreach (var m in _party.State.Members) ids.Add(m.PlayFabId);
            string asked = code;
            Svc.FetchParty(code, ids, list =>
            {
                if (asked != _partyCode) return; // da doi to doi trong luc cho
                Merge(PartyKey, list);
            }, e => { });
        }

        private void PollNextFriend()
        {
            if (_friends.Count == 0) return;
            // uu tien: ban chua quet lan nao (ke ca offline) -> roi luan phien ban online/away
            foreach (var f in _friends)
                if (!_scannedOnce.Contains(f.PlayFabId)) { PollFriend(f.PlayFabId); return; }
            for (int i = 0; i < _friends.Count; i++)
            {
                var f = _friends[(_bgCursor + i) % _friends.Count];
                if (f.Presence == FriendPresence.Offline) continue;
                _bgCursor = (_bgCursor + i + 1) % _friends.Count;
                if (_visibleKey == DmKey(f.PlayFabId)) continue; // da poll rieng
                PollFriend(f.PlayFabId);
                return;
            }
        }

        private void PollFriend(string friendId)
        {
            if (string.IsNullOrEmpty(friendId)) return;
            _scannedOnce.Add(friendId);
            string self = SelfId;
            Svc.FetchDirectOutbox(friendId, list =>
            {
                var toMe = new List<ChatMessage>();
                foreach (var m in list) if (m.f == friendId && m.o == self) toMe.Add(m);
                if (toMe.Count > 0) Merge(DmKey(friendId), toMe);
            }, e => { });
        }

        // ------------------------------------------------------------------ kenh / chua doc
        private Conversation Get(string key)
        {
            Conversation c;
            if (!_convos.TryGetValue(key, out c))
            {
                c = new Conversation { Key = key, LastRead = LoadLastRead(key) };
                _convos[key] = c;
            }
            return c;
        }

        private void Merge(string key, List<ChatMessage> incoming)
        {
            if (incoming == null || incoming.Count == 0) return;
            var c = Get(key);
            bool added = false;
            foreach (var m in incoming)
            {
                if (m == null || string.IsNullOrEmpty(m.x)) continue;
                if (c.Ids.Add(m.Id)) { c.Messages.Add(m); added = true; }
            }
            if (!added) return;
            c.Messages.Sort((a, b) => a.t.CompareTo(b.t));
            while (c.Messages.Count > ConversationCap) { c.Ids.Remove(c.Messages[0].Id); c.Messages.RemoveAt(0); }
            if (_visibleKey == key) MarkRead(key); else RecountUnread(c);
            if (ConversationChanged != null) ConversationChanged(key);
        }

        private void RecountUnread(Conversation c)
        {
            string self = SelfId; int n = 0;
            foreach (var m in c.Messages) if (m.f != self && m.t > c.LastRead) n++;
            if (n != c.Unread) { c.Unread = n; if (UnreadChanged != null) UnreadChanged(); }
        }

        private void MarkRead(string key)
        {
            var c = Get(key);
            long last = c.LastRead;
            foreach (var m in c.Messages) if (m.t > last) last = m.t;
            if (last != c.LastRead) { c.LastRead = last; SaveLastRead(key, last); }
            if (c.Unread != 0) { c.Unread = 0; if (UnreadChanged != null) UnreadChanged(); }
        }

        // Moc "da doc" luu theo tai khoan (PlayerPrefs).
        private string ReadPrefKey(string key) { return "chat_read_" + SelfId + "_" + key; }
        private long LoadLastRead(string key)
        {
            if (key == PartyKey) return NowMs(); // kenh to doi: chi tin moi tu luc vao
            // nhan rieng: chua tung doc -> 0 (tin ban gui luc minh offline van bao chua doc)
            string s = PlayerPrefs.GetString(ReadPrefKey(key), "");
            long v; return long.TryParse(s, out v) ? v : 0L;
        }
        private void SaveLastRead(string key, long v)
        {
            if (key == PartyKey) return;
            PlayerPrefs.SetString(ReadPrefKey(key), v.ToString());
        }
    }
}
