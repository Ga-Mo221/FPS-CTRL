using System;
using System.Collections;
using UnityEngine;
using Backend.Bootstrap;
using Backend.Interfaces;
using Backend.Models;

namespace Game.UI
{
    // [Aemy 2026-09-21] Dieu phoi party: tao/join/roi, moi ban, ready-gate, queue chung (cung 1 Fusion session).
    public class PartyController : MonoBehaviour
    {
        [SerializeField] private PartyPanelController _party;   // hien member o top bar (tuy chon)
        [SerializeField] private float _pollInterval = 3f;
        [SerializeField] private float _invitePollInterval = 5f;

        public string Code { get; private set; }
        public bool InParty { get { return !string.IsNullOrEmpty(Code); } }
        public bool IsLeader { get; private set; }
        public PartyState State { get; private set; }
        public MatchMode PendingMode { get; set; } = MatchMode.FFA; // [Aemy] leader auto-start theo mode nay
        public bool SelfReady { get; private set; }

        public event Action<PartyInvite> OnInviteReceived;
        public event Action<PartyState> OnPartyChanged;
        public event Action<string, MatchMode> OnPartyMatchReady; // [PARTY-FIX] session, mode -> MenuShellBinder show lobby roi handoff

        private Coroutine _pollCo, _inviteCo;
        private bool _handedOff;
        private string _lastHandledMatch; private bool _restoreArmed; // [PARTY-FIX #5]
        private int _hbTick; // [PARTY-HB]
        private float _partyStartRealtime; // [PARTY-HB] moc khoi dong -> grace tranh da nham khi ve menu
        // [PARTY-HB-FIX 2026-10-01] Theo doi Seen thay doi theo DONG HO LOCAL cua leader (mien nhiem lech gio cross-device).
        private readonly System.Collections.Generic.Dictionary<string, long> _seenVal = new System.Collections.Generic.Dictionary<string, long>();
        private readonly System.Collections.Generic.Dictionary<string, float> _seenAt = new System.Collections.Generic.Dictionary<string, float>();
        private const float KICK_STALE_SECONDS = 30f; // kick neu Seen KHONG tien trong 30s (do bang dong ho leader)

        private void OnEnable()  { if (_inviteCo == null) _inviteCo = StartCoroutine(InvitePoll()); }
        private void OnDisable()
        {
            if (_pollCo != null) { StopCoroutine(_pollCo); _pollCo = null; }
            if (_inviteCo != null) { StopCoroutine(_inviteCo); _inviteCo = null; }
        }

        private void Start()
        {
            // [PARTY-FIX #5] Ve menu -> GIU party (khoi phuc), KHONG tu roi. Xoa MatchId cu de khong bi keo lai vao tran vua xong.
            var leftover = PlayerPrefs.GetString("PartyCode", "");
            if (!string.IsNullOrEmpty(leftover) && !InParty)
            {
                Code = leftover; _restoreArmed = true; _handedOff = false;
                StartPolling();
                Debug.Log("[Party-DIAG] Khoi phuc party khi vao menu: " + leftover);
            }
        }

        private static string SelfId() { return BackendBootstrap.AuthService != null ? BackendBootstrap.AuthService.CurrentUserId : null; }

        public void InviteFriend(string friendPlayFabId)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || string.IsNullOrEmpty(friendPlayFabId)) { Debug.LogWarning("[Party-DIAG] SEND huy: thieu svc/id"); return; }
            Debug.Log("[Party-DIAG] SEND: moi " + friendPlayFabId + " (InParty=" + InParty + ")");
            if (InParty) { svc.SendInvite(friendPlayFabId, Code, SendOk, SendErr); return; }
            svc.CreateParty(code =>
            {
                Code = code; IsLeader = true; SelfReady = true; _handedOff = false; PlayerPrefs.SetString("PartyCode", code); StartPolling();
                Debug.Log("[Party-DIAG] SEND: da tao party code=" + code + ", dang gui invite...");
                svc.SendInvite(friendPlayFabId, code, SendOk, SendErr);
            }, LogErr);
        }

        public void AcceptInvite(string code)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || string.IsNullOrEmpty(code)) return;
            Debug.Log("[Party-DIAG] ACCEPT: xoa invite + join code=" + code);
            svc.ClearInvite(null, null); // [PARTY-FIX] xoa NGAY -> khong spam popup du join loi
            svc.JoinParty(code,
                () => { Code = code; IsLeader = false; SelfReady = false; _handedOff = false; PlayerPrefs.SetString("PartyCode", code); StartPolling(); Debug.Log("[Party-DIAG] ACCEPT: JOIN ok (da vao party)"); },
                e => { Debug.LogWarning("[Party-DIAG] ACCEPT: JOIN LOI: " + (e != null ? e.ErrorMessage : "?")); });
        }

        public void DeclineInvite() { var svc = BackendBootstrap.PartyService; if (svc != null) svc.ClearInvite(null, null); }
        private static void SendOk() { Debug.Log("[Party-DIAG] SEND ok (CloudScript SendPartyInvite da chay xong)"); }
        private static void SendErr(AuthError e) { Debug.LogWarning("[Party-DIAG] SEND LOI: " + (e != null ? e.ErrorMessage : "?")); }

        public void SetReady(bool ready)
        {
            SelfReady = ready;
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty) return;
            svc.SetReady(Code, ready, null, LogErr);
        }

        public void ToggleReady() { SetReady(!SelfReady); }

        public void LeaveParty()
        {
            var svc = BackendBootstrap.PartyService;
            if (svc != null && InParty) svc.LeaveParty(Code, null, LogErr);
            Code = null; IsLeader = false; State = null; SelfReady = false;
            _seenVal.Clear(); _seenAt.Clear();
            PlayerPrefs.DeleteKey("PartyCode");
            if (_pollCo != null) { StopCoroutine(_pollCo); _pollCo = null; }
        }

        // Leader bat dau: chi khi TAT CA ready. Ghi match -> ca party queue chung.
        public bool StartPartyMatch(MatchMode mode)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty || !IsLeader) return false;
            if (State == null || !State.AllReady()) { Debug.LogWarning("[Party] Chua du thanh vien san sang."); return false; }
            string val = mode.ToString() + "|party_" + Code;
            svc.SetMatchId(Code, val, null, LogErr);
            return true;
        }

        private void StartPolling() { if (_pollCo == null) { _partyStartRealtime = Time.unscaledTime; _pollCo = StartCoroutine(PartyPoll()); } }

        private IEnumerator PartyPoll()
        {
            var wait = new WaitForSeconds(_pollInterval);
            while (InParty)
            {
                var svc = BackendBootstrap.PartyService;
                if (svc != null)
                {
                    if (_hbTick % 3 == 0) svc.Touch(Code, SelfReady, null, null); // [PARTY-HB] ~9s/lan
                    _hbTick++;
                    svc.GetParty(Code, st =>
                    {
                        State = st;
                        // [PARTY-FIX #5] Khoi phuc IsLeader/SelfReady + xoa MatchId cu (khong vao lai tran).
                        if (_restoreArmed && st != null)
                        {
                            string me = SelfId();
                            IsLeader = (!string.IsNullOrEmpty(me) && me == st.LeaderId);
                            if (st.Members != null) foreach (var mm in st.Members) if (mm.PlayFabId == me) SelfReady = IsLeader ? true : mm.Ready;
                            _lastHandledMatch = st.MatchId; _restoreArmed = false;
                            if (IsLeader && !string.IsNullOrEmpty(st.MatchId)) { var s2 = BackendBootstrap.PartyService; if (s2 != null) s2.SetMatchId(Code, "", null, LogErr); }
                        }
                        if (OnPartyChanged != null) OnPartyChanged(st);
                        ApplyToPanel(st);
                        // [PARTY-HB] Leader: da member khong heartbeat > 30s ra khoi party.
                        // Grace: 12s dau sau khi vao menu KHONG da ai (cho member ve tu tran + heartbeat lai).
                        if (IsLeader && st != null && st.Members != null && Time.unscaledTime - _partyStartRealtime > 12f)
                        {
                            string meId = SelfId(); var svc2 = BackendBootstrap.PartyService;
                            float tnow = Time.unscaledTime;
                            var present = new System.Collections.Generic.HashSet<string>();
                            foreach (var mm in st.Members)
                            {
                                if (mm == null || string.IsNullOrEmpty(mm.PlayFabId)) continue;
                                present.Add(mm.PlayFabId);
                                if (mm.PlayFabId == meId || mm.Seen <= 0) continue;
                                long prev; bool had = _seenVal.TryGetValue(mm.PlayFabId, out prev);
                                if (!had || mm.Seen != prev) { _seenVal[mm.PlayFabId] = mm.Seen; _seenAt[mm.PlayFabId] = tnow; }
                                else
                                {
                                    float since; _seenAt.TryGetValue(mm.PlayFabId, out since);
                                    if (tnow - since > KICK_STALE_SECONDS && svc2 != null)
                                    {
                                        Debug.Log("[Party-HB] Kick stale: " + mm.Name + " (" + (tnow - since).ToString("0") + "s khong heartbeat - dong ho leader)");
                                        svc2.KickMember(Code, mm.PlayFabId, null, LogErr);
                                        _seenVal.Remove(mm.PlayFabId); _seenAt.Remove(mm.PlayFabId);
                                    }
                                }
                            }
                            var _gone = new System.Collections.Generic.List<string>();
                            foreach (var k in _seenVal.Keys) if (!present.Contains(k)) _gone.Add(k);
                            foreach (var k in _gone) { _seenVal.Remove(k); _seenAt.Remove(k); }
                        }
                        string mid = st != null ? st.MatchId : null;
                        if (string.IsNullOrEmpty(mid)) { _handedOff = false; _lastHandledMatch = null; }
                        else if (!_handedOff && mid != _lastHandledMatch) { _handedOff = true; _lastHandledMatch = mid; HandOffToPartyMatch(mid); }
                    }, e =>
                    {
                        LogErr(e);
                        if (_restoreArmed) { Code = null; IsLeader = false; State = null; _restoreArmed = false; PlayerPrefs.DeleteKey("PartyCode"); if (_pollCo != null) { StopCoroutine(_pollCo); _pollCo = null; } }
                    });
                }
                yield return wait;
            }
            _pollCo = null;
        }

        private IEnumerator InvitePoll()
        {
            Debug.Log("[Party-DIAG] InvitePoll BAT DAU (interval=" + _invitePollInterval + "s) — dang cho loi moi...");
            var wait = new WaitForSeconds(_invitePollInterval);
            while (isActiveAndEnabled)
            {
                var svc = BackendBootstrap.PartyService;
                if (svc != null) // [PARTY-FIX] bo chan !InParty -> da o party van nhan duoc loi moi
                    svc.CheckInvite(inv => { if (inv != null && !string.IsNullOrEmpty(inv.Code) && OnInviteReceived != null) OnInviteReceived(inv); }, e => Debug.LogWarning("[Party-DIAG] CHECK loi: " + (e != null ? e.ErrorMessage : "?")));
                yield return wait;
            }
            _inviteCo = null;
        }

        private void HandOffToPartyMatch(string matchVal)
        {
            var ho = BackendBootstrap.MatchHandoff;
            if (ho == null || string.IsNullOrEmpty(matchVal)) return;
            MatchMode mode = MatchMode.FFA; string session = matchVal;
            int bar = matchVal.IndexOf('|');
            if (bar >= 0) { var m = matchVal.Substring(0, bar); session = matchVal.Substring(bar + 1); if (m == "TDM") mode = MatchMode.TDM; }
            if (OnPartyMatchReady != null) OnPartyMatchReady(session, mode); // qua lobby matchmaking truoc
            else ho.HandOff(new MatchFoundResult { MatchId = session, Mode = mode, QueueName = "" });
        }

        private readonly System.Collections.Generic.Dictionary<int, string> _avatarShown = new System.Collections.Generic.Dictionary<int, string>();

        private void ApplyToPanel(PartyState st)
        {
            if (_party == null || st == null) return;
            int n = _party.SlotCount;
            for (int i = 0; i < n; i++)
            {
                if (st.Members != null && i < st.Members.Count)
                {
                    var m = st.Members[i];
                    _party.SetMember(i, m.Name, m.PlayFabId == st.LeaderId, m.Ready ? "Sẵn sàng" : "Chưa sẵn sàng");
                    if (!string.IsNullOrEmpty(m.AvatarUrl))
                    {
                        string shown; _avatarShown.TryGetValue(i, out shown);
                        if (shown != m.AvatarUrl) { _avatarShown[i] = m.AvatarUrl; StartCoroutine(DownloadPartyAvatar(i, m.AvatarUrl)); }
                    }
                }
                else { _party.SetEmpty(i); _avatarShown.Remove(i); }
            }
        }

        private IEnumerator DownloadPartyAvatar(int index, string url)
        {
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 64); // [#5] thu nho 64px
                if (tex == null || _party == null) yield break;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _party.SetMemberAvatar(index, sp);
            }
        }

        private static void LogErr(AuthError e) { if (e != null) Debug.LogWarning("[Party] " + e.ErrorMessage); }
    }
}
