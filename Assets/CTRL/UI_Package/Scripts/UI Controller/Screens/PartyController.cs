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
        // [Aemy 2026-10-17] Thanh vien: nhan cai dat (che do/map) cua truong nhom. Bi da khoi doi -> OnKicked.
        public event Action<PartySettings> OnLeaderSettings;
        public event Action OnKicked;
        /// <summary>Cai dat truong nhom dang dung (thanh vien dung de khoa + hien thi). null = chua co.</summary>
        public PartySettings LeaderSettings { get; private set; }
        /// <summary>[Aemy 2026-10-17] Tran party cuoi: scene map (dinh tu truong nhom) + chuoi phu (custom).</summary>
        public string LastMatchScene { get; private set; }
        public string LastMatchExtra { get; private set; }

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

        // [Aemy 2026-10-17] Ma party luu THEO TAI KHOAN (truoc day key chung "PartyCode" -> doi tai khoan tren cung may
        // van khoi phuc party cua nguoi truoc).
        private const string LegacyPartyKey = "PartyCode";
        private static string PartyKey() { var id = SelfId(); return LegacyPartyKey + "_" + (string.IsNullOrEmpty(id) ? "local" : id); }

        private void Start()
        {
            if (PlayerPrefs.HasKey(LegacyPartyKey)) { PlayerPrefs.DeleteKey(LegacyPartyKey); PlayerPrefs.Save(); }
            // [PARTY-FIX #5] Ve menu -> GIU party (khoi phuc), KHONG tu roi. Xoa MatchId cu de khong bi keo lai vao tran vua xong.
            var leftover = PlayerPrefs.GetString(PartyKey(), "");
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
                Code = code; IsLeader = true; SelfReady = true; _handedOff = false; PlayerPrefs.SetString(PartyKey(), code); StartPolling();
                if (_pendingSettings != null) PublishSettings(_pendingSettings);
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
                () => { Code = code; IsLeader = false; SelfReady = false; _handedOff = false; LeaderSettings = null; PlayerPrefs.SetString(PartyKey(), code); StartPolling(); Debug.Log("[Party-DIAG] ACCEPT: JOIN ok (da vao party)"); },
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
            if (svc != null && InParty)
            {
                // [Aemy 2026-10-17] Truong nhom roi -> giao truong nhom cho nguoi con lai truoc (khong de nhom "mo coi").
                if (IsLeader && State != null && State.Members != null)
                {
                    string me = SelfId();
                    foreach (var m in State.Members)
                        if (m != null && !string.IsNullOrEmpty(m.PlayFabId) && m.PlayFabId != me) { svc.SetLeader(Code, m.PlayFabId, null, LogErr); break; }
                }
                svc.LeaveParty(Code, null, LogErr);
            }
            ClearLocalState();
        }

        // [Aemy 2026-10-17] Xoa SACH du lieu party tren may (roi / bi da / khoi phuc that bai) + bao UI ve trang thai solo.
        private void ClearLocalState()
        {
            Code = null; IsLeader = false; State = null; SelfReady = false; LeaderSettings = null;
            _seenVal.Clear(); _seenAt.Clear(); _avatarShown.Clear();
            _handedOff = false; _lastHandledMatch = null; _restoreArmed = false; _lastPublished = null;
            PlayerPrefs.DeleteKey(PartyKey()); PlayerPrefs.Save();
            if (_pollCo != null) { StopCoroutine(_pollCo); _pollCo = null; }
            if (_party != null) for (int i = 1; i < _party.SlotCount; i++) _party.SetEmpty(i);
            if (OnPartyChanged != null) OnPartyChanged(null);
        }

        // ===== [Aemy 2026-10-17] Truong nhom: da / chuyen truong nhom / dong bo cai dat =====
        public void KickMember(string playFabId)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty || !IsLeader || string.IsNullOrEmpty(playFabId) || playFabId == SelfId()) return;
            svc.KickMember(Code, playFabId, () => Debug.Log("[Party] Da da " + playFabId), LogErr);
            _seenVal.Remove(playFabId); _seenAt.Remove(playFabId);
        }

        public void TransferLeader(string playFabId)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty || !IsLeader || string.IsNullOrEmpty(playFabId) || playFabId == SelfId()) return;
            svc.SetLeader(Code, playFabId, null, LogErr);
            IsLeader = false; SelfReady = false; // nguoi moi lam truong nhom; minh thanh thanh vien (phai bam san sang lai)
            svc.SetReady(Code, false, null, LogErr);
        }

        private PartySettings _pendingSettings, _lastPublished;
        /// <summary>Truong nhom doi che do / map -> ghi len party. Chua co party thi nho lai, tao party xong se ghi.</summary>
        public void PublishSettings(PartySettings s)
        {
            if (s == null) return;
            _pendingSettings = s;
            if (!InParty || !IsLeader) return;
            if (s.SameAs(_lastPublished)) return;
            var svc = BackendBootstrap.PartyService; if (svc == null) return;
            _lastPublished = new PartySettings { modeIndex = s.modeIndex, mapIndex = s.mapIndex, killTarget = s.killTarget, customRule = s.customRule };
            svc.SetSettings(Code, JsonUtility.ToJson(s), null, LogErr);
        }

        public bool IsMember(string playFabId) { return State != null && State.HasMember(playFabId); }
        public string MyId { get { return SelfId(); } }

        // Leader bat dau: chi khi TAT CA ready. Ghi match -> ca party queue chung.
        // [Aemy 2026-10-17] Kem scene map cua truong nhom ("MODE|scene|session|extra") -> ai lam host Fusion cung load DUNG map.
        public bool StartPartyMatch(MatchMode mode, string sceneName = null, string session = null, string extra = null)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty || !IsLeader) return false;
            if (State == null || !State.AllReady()) { Debug.LogWarning("[Party] Chua du thanh vien san sang."); return false; }
            if (string.IsNullOrEmpty(session)) session = "party_" + Code + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 4);
            string val = mode.ToString() + "|" + (sceneName ?? "") + "|" + session + "|" + (extra ?? "");
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
                        if (!InParty) return; // da roi party trong luc cho ket qua
                        // [Aemy 2026-10-17] Khong con trong danh sach thanh vien -> bi truong nhom da (hoac nhom da giai tan).
                        string meNow = SelfId();
                        if (!_restoreArmed && st != null && st.Members != null && st.Members.Count > 0 && !st.HasMember(meNow))
                        {
                            Debug.Log("[Party] Minh khong con trong nhom -> bi da / nhom giai tan.");
                            ClearLocalState();
                            if (OnKicked != null) OnKicked();
                            return;
                        }
                        // [Aemy 2026-10-17] Truong nhom da roi ma chua giao -> thanh vien dau tien nhan truong nhom.
                        if (st != null && st.Members != null && st.Members.Count > 0 && !st.HasMember(st.LeaderId) && st.Members[0].PlayFabId == meNow)
                        {
                            var s3 = BackendBootstrap.PartyService; if (s3 != null) s3.SetLeader(Code, meNow, null, LogErr);
                            st.LeaderId = meNow;
                        }
                        bool wasLeader = IsLeader;
                        if (!_restoreArmed && st != null && !string.IsNullOrEmpty(st.LeaderId)) IsLeader = st.LeaderId == meNow; // nhan chuyen truong nhom
                        if (IsLeader && !wasLeader) { SelfReady = true; if (_pendingSettings != null) { _lastPublished = null; PublishSettings(_pendingSettings); } }
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
                        // Thanh vien: ap cai dat cua truong nhom.
                        if (!IsLeader && st != null && !string.IsNullOrEmpty(st.SettingsJson))
                        {
                            PartySettings ps = null;
                            try { ps = JsonUtility.FromJson<PartySettings>(st.SettingsJson); } catch { ps = null; }
                            if (ps != null && !ps.SameAs(LeaderSettings)) { LeaderSettings = ps; if (OnLeaderSettings != null) OnLeaderSettings(ps); }
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
                        if (_restoreArmed) ClearLocalState(); // [Aemy 2026-10-17] khoi phuc that bai -> xoa sach
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
            // [Aemy 2026-10-17] "MODE|scene|session|extra" (moi) hoac "MODE|session" (cu).
            var parts = matchVal.Split('|');
            LastMatchScene = null; LastMatchExtra = null;
            if (parts.Length >= 3) { session = parts[2]; LastMatchScene = parts[1]; LastMatchExtra = parts.Length > 3 ? parts[3] : null; }
            else if (parts.Length == 2) session = parts[1];
            if (parts.Length >= 2) { if (parts[0] == "TDM") mode = MatchMode.TDM; }
            if (!string.IsNullOrEmpty(LastMatchScene) && GameManager.Instance != null) GameManager.Instance.SetSelectedMapScene(LastMatchScene);
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
