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
        public event Action OnPartyMatchCancelled; // [Aemy 2026-10-18] truong nhom huy tran vua bat dau (MatchId bi xoa)
        private string _lastStartedMatch;   // gia tri MatchId truong nhom vua ghi
        private string _ignoreMatch;        // MatchId da huy -> poll tre khong duoc vao tran nua
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
        private float _partyStartRealtime; // [PARTY-HB] moc khoi dong -> grace tranh da nham khi ve menu
        // [PARTY-HB-FIX 2026-10-01] Theo doi Seen thay doi theo DONG HO LOCAL cua leader (mien nhiem lech gio cross-device).
        private readonly System.Collections.Generic.Dictionary<string, long> _seenVal = new System.Collections.Generic.Dictionary<string, long>();
        private readonly System.Collections.Generic.Dictionary<string, float> _seenAt = new System.Collections.Generic.Dictionary<string, float>();
        // [Aemy 2026-10-22] 30s -> 15s (heartbeat 5s, chay ca trong tran): thoat game dot ngot (crash / tat tien trinh)
        // duoc don sau ~15-18s. Thoat binh thuong thi PartyHeartbeat roi nhom truoc khi tat -> ~3s.
        private const float KICK_STALE_SECONDS = 15f; // kick neu Seen KHONG tien trong 15s (do bang dong ho local)

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
                // [Aemy 2026-10-22] Ve tu tran trong CUNG phien choi: lay lai san sang / truong nhom da nho
                // (truoc day SelfReady=false toi khi poll xong -> nut va o party lech nhau).
                if (PartyHeartbeat.Code == leftover) { IsLeader = PartyHeartbeat.IsLeader; SelfReady = PartyHeartbeat.Ready; }
                StartPolling();
                Debug.Log("[Party-DIAG] Khoi phuc party khi vao menu: " + leftover);
            }
        }

        private static string SelfId() { return BackendBootstrap.AuthService != null ? BackendBootstrap.AuthService.CurrentUserId : null; }

        /// <summary>
        /// [Aemy 2026-10-22] Goi ngay sau khi DANG NHAP (SessionCache.ResetForNewSession).
        /// Con ma party luu tu lan choi truoc = lan truoc thoat game dot ngot (tat game, crash, mat dien) ma chua roi nhom.
        /// -> KHONG khoi phuc nua: xoa cache tren may + roi nhom tren server NGAY (truong nhom thi giao cho nguoi con lai
        ///    truoc), de nhom hien tai thay minh roi o lan poll ke tiep (~3s) thay vi cho 30s mat heartbeat.
        /// Ve menu sau tran KHONG di qua day (khong dang nhap lai) nen van giu party nhu cu.
        /// </summary>
        public static void LeaveStalePartyOnLogin()
        {
            string key = PartyKey();
            string code = PlayerPrefs.GetString(key, "");
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            if (string.IsNullOrEmpty(code)) return;
            var svc = BackendBootstrap.PartyService;
            string me = SelfId();
            if (svc == null || string.IsNullOrEmpty(me)) return;
            Debug.Log("[Party] Dang nhap lai: roi party cu " + code + " (lan truoc thoat game ma chua roi nhom).");
            LeaveOnServer(code, () => Debug.Log("[Party] Da roi party cu " + code));
        }

        /// <summary>
        /// [Aemy 2026-10-22] Roi nhom tren server (khong can PartyController trong scene): truong nhom thi giao truong nhom
        /// cho nguoi con lai TRUOC roi moi roi. onDone goi ca khi loi (nguoi goi co the dang cho de tat game).
        /// </summary>
        public static void LeaveOnServer(string code, Action onDone)
        {
            var svc = BackendBootstrap.PartyService;
            string me = SelfId();
            if (svc == null || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(me)) { if (onDone != null) onDone(); return; }
            Action leave = () => svc.LeaveParty(code, () => { if (onDone != null) onDone(); }, e => { LogErr(e); if (onDone != null) onDone(); });
            svc.GetParty(code, st =>
            {
                string heir = null;
                if (st != null && st.LeaderId == me && st.Members != null)
                    foreach (var m in st.Members)
                        if (m != null && !string.IsNullOrEmpty(m.PlayFabId) && m.PlayFabId != me) { heir = m.PlayFabId; break; }
                if (heir != null) svc.SetLeader(code, heir, () => leave(), e => { LogErr(e); leave(); });
                else leave();
            },
            e => leave()); // nhom da giai tan / khong doc duoc -> van thu roi
        }

        /// <summary>[Aemy 2026-10-22] Xoa ma party da luu cua tai khoan nay (vd vua roi nhom luc tat game).</summary>
        public static void ForgetSavedParty()
        {
            string key = PartyKey();
            if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        // [2026-10-21] Moi cung 1 nguoi: cho 5s moi duoc moi lai (static -> giu qua viec dung lai dong ban / mo lai panel).
        public const float InviteCooldown = 5f;
        private static readonly System.Collections.Generic.Dictionary<string, float> _lastInviteAt = new System.Collections.Generic.Dictionary<string, float>();
        /// <summary>So giay con lai truoc khi duoc moi lai nguoi nay (0 = moi duoc).</summary>
        public static float InviteCooldownLeft(string playFabId)
        {
            float t;
            if (string.IsNullOrEmpty(playFabId) || !_lastInviteAt.TryGetValue(playFabId, out t)) return 0f;
            float elapsed = Time.unscaledTime - t;
            if (elapsed < 0f) return 0f; // moc tu lan Play truoc (Editor tat domain reload) -> bo qua
            return Mathf.Max(0f, InviteCooldown - elapsed);
        }

        /// <summary>Moi ban vao party. Tra false neu dang trong thoi gian cho (5s) hoac thieu du lieu.</summary>
        public bool InviteFriend(string friendPlayFabId)
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || string.IsNullOrEmpty(friendPlayFabId)) { Debug.LogWarning("[Party-DIAG] SEND huy: thieu svc/id"); return false; }
            if (InviteCooldownLeft(friendPlayFabId) > 0f) return false;
            _lastInviteAt[friendPlayFabId] = Time.unscaledTime;
            InviteFriendNow(svc, friendPlayFabId);
            return true;
        }

        private void InviteFriendNow(IPartyService svc, string friendPlayFabId)
        {
            Debug.Log("[Party-DIAG] SEND: moi " + friendPlayFabId + " (InParty=" + InParty + ")");
            if (InParty) { svc.SendInvite(friendPlayFabId, Code, SendOk, SendErr); return; }
            svc.CreateParty(code =>
            {
                Code = code; IsLeader = true; SelfReady = true; _handedOff = false; PlayerPrefs.SetString(PartyKey(), code); PartyHeartbeat.Begin(code, true, true); StartPolling();
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
                () => { Code = code; IsLeader = false; SelfReady = false; _handedOff = false; LeaderSettings = null; PlayerPrefs.SetString(PartyKey(), code); PartyHeartbeat.Begin(code, false, false); StartPolling(); Debug.Log("[Party-DIAG] ACCEPT: JOIN ok (da vao party)"); },
                e => { Debug.LogWarning("[Party-DIAG] ACCEPT: JOIN LOI: " + (e != null ? e.ErrorMessage : "?")); });
        }

        public void DeclineInvite() { var svc = BackendBootstrap.PartyService; if (svc != null) svc.ClearInvite(null, null); }
        private static void SendOk() { Debug.Log("[Party-DIAG] SEND ok (CloudScript SendPartyInvite da chay xong)"); }
        private static void SendErr(AuthError e) { Debug.LogWarning("[Party-DIAG] SEND LOI: " + (e != null ? e.ErrorMessage : "?")); }

        public void SetReady(bool ready)
        {
            SelfReady = ready;
            PartyHeartbeat.Set(SelfReady, IsLeader); // heartbeat ghi dung gia tri moi (khong ghi de lai gia tri cu)
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
            _seenVal.Clear(); _seenAt.Clear(); _avatarShown.Clear(); _slotMember.Clear();
            PartyHeartbeat.End(); // [Aemy 2026-10-22] ngung heartbeat xuyen scene
            _handedOff = false; _lastHandledMatch = null; _restoreArmed = false; _lastPublished = null;
            PlayerPrefs.DeleteKey(PartyKey()); PlayerPrefs.Save();
            if (_pollCo != null) { StopCoroutine(_pollCo); _pollCo = null; }
            if (_party != null) for (int i = 1; i < _party.SlotCount; i++) { _party.SetEmpty(i); _party.ResetMemberAvatar(i); }
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
            PartyHeartbeat.Set(false, false);
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
            _lastStartedMatch = val; _ignoreMatch = null;
            svc.SetMatchId(Code, val, null, LogErr);
            return true;
        }

        /// <summary>[Aemy 2026-10-18] Truong nhom huy tran vua bat dau (trong luc dem nguoc) -> xoa MatchId cho ca to doi.</summary>
        public void CancelPartyMatch()
        {
            var svc = BackendBootstrap.PartyService;
            if (svc == null || !InParty || !IsLeader) return;
            _ignoreMatch = _lastStartedMatch;
            _handedOff = false;
            svc.SetMatchId(Code, "", null, LogErr);
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
                    // [Aemy 2026-10-22] Heartbeat chuyen sang PartyHeartbeat (chay ca trong tran). Khi dang khoi phuc
                    // CHUA Touch: truoc day tick 0 ghi ready=false (SelfReady mac dinh) de len gia tri that tren server.
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
                            if (st.Members != null && st.Members.Count > 0 && !st.HasMember(me))
                            {
                                // [Aemy 2026-10-22] Vang lau (thoat game) -> da bi don khoi nhom: xoa sach, khong khoi phuc.
                                Debug.Log("[Party] Khoi phuc: minh khong con trong nhom -> xoa party cu.");
                                ClearLocalState();
                                if (OnKicked != null) OnKicked();
                                return;
                            }
                            IsLeader = (!string.IsNullOrEmpty(me) && me == st.LeaderId);
                            bool carried = PartyHeartbeat.Code == Code; // ve tu tran cung phien: gia tri local la moi nhat
                            if (IsLeader) SelfReady = true;
                            else if (!carried && st.Members != null) foreach (var mm in st.Members) if (mm.PlayFabId == me) SelfReady = mm.Ready;
                            _lastHandledMatch = st.MatchId; _restoreArmed = false;
                            PartyHeartbeat.Begin(Code, SelfReady, IsLeader);
                            if (carried) SetReady(SelfReady); // ghi lai ngay -> o party cua moi nguoi khop voi nut
                            if (IsLeader && !string.IsNullOrEmpty(st.MatchId)) { var s2 = BackendBootstrap.PartyService; if (s2 != null) s2.SetMatchId(Code, "", null, LogErr); }
                        }
                        // Thanh vien: ap cai dat cua truong nhom.
                        if (!IsLeader && st != null && !string.IsNullOrEmpty(st.SettingsJson))
                        {
                            PartySettings ps = null;
                            try { ps = JsonUtility.FromJson<PartySettings>(st.SettingsJson); } catch { ps = null; }
                            if (ps != null && !ps.SameAs(LeaderSettings)) { LeaderSettings = ps; if (OnLeaderSettings != null) OnLeaderSettings(ps); }
                        }
                        // [PARTY-HB] Grace 12s dau sau khi vao menu (cho moi nguoi heartbeat lai) roi moi xet mat ket noi.
                        if (st != null && st.Members != null && Time.unscaledTime - _partyStartRealtime > 12f) HandleStaleMembers(st);
                        if (InParty && !_restoreArmed) PartyHeartbeat.Set(SelfReady, IsLeader);
                        if (OnPartyChanged != null) OnPartyChanged(st);
                        ApplyToPanel(st);
                        string mid = st != null ? st.MatchId : null;
                        if (string.IsNullOrEmpty(mid))
                        {
                            // [Aemy 2026-10-18] MatchId bi xoa khi dang cho vao tran -> truong nhom da huy
                            if (_handedOff && OnPartyMatchCancelled != null) OnPartyMatchCancelled();
                            _handedOff = false; _lastHandledMatch = null; // _ignoreMatch giu lai: phan hoi poll tre mang MatchId cu van bi bo qua
                        }
                        else if (!_handedOff && mid != _lastHandledMatch && mid != _ignoreMatch) { _handedOff = true; _lastHandledMatch = mid; HandOffToPartyMatch(mid); }
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

        // [PARTY-HB] Theo doi Seen cua MOI nguoi bang DONG HO LOCAL (mien nhiem lech gio giua cac may).
        // [Aemy 2026-10-22] Ca thanh vien cung theo doi: truong nhom thoat game (khong con heartbeat > 30s)
        //   -> nguoi con song dau tien trong danh sach nhan truong nhom va da truong nhom ma ra. Truoc day khong ai don
        //   -> thanh vien ket voi truong nhom ma, khong bat dau tran duoc.
        private void HandleStaleMembers(PartyState st)
        {
            string me = SelfId(); float now = Time.unscaledTime;
            var present = new System.Collections.Generic.HashSet<string>();
            var stale = new System.Collections.Generic.HashSet<string>();
            foreach (var mm in st.Members)
            {
                if (mm == null || string.IsNullOrEmpty(mm.PlayFabId)) continue;
                present.Add(mm.PlayFabId);
                if (mm.PlayFabId == me || mm.Seen <= 0) continue;
                long prev; bool had = _seenVal.TryGetValue(mm.PlayFabId, out prev);
                if (!had || mm.Seen != prev) { _seenVal[mm.PlayFabId] = mm.Seen; _seenAt[mm.PlayFabId] = now; continue; }
                float since; _seenAt.TryGetValue(mm.PlayFabId, out since);
                if (now - since > KICK_STALE_SECONDS) stale.Add(mm.PlayFabId);
            }
            var gone = new System.Collections.Generic.List<string>();
            foreach (var k in _seenVal.Keys) if (!present.Contains(k)) gone.Add(k);
            foreach (var k in gone) { _seenVal.Remove(k); _seenAt.Remove(k); }
            if (stale.Count == 0) return;

            var svc = BackendBootstrap.PartyService; if (svc == null) return;
            if (IsLeader)
            {
                foreach (var id in stale)
                {
                    Debug.Log("[Party-HB] Kick stale: " + id + " (khong heartbeat > " + KICK_STALE_SECONDS + "s - dong ho local)");
                    svc.KickMember(Code, id, null, LogErr);
                    _seenVal.Remove(id); _seenAt.Remove(id);
                }
                return;
            }

            string leader = st.LeaderId;
            if (string.IsNullOrEmpty(leader) || !stale.Contains(leader)) return;
            string heir = null;
            foreach (var mm in st.Members)
            {
                if (mm == null || string.IsNullOrEmpty(mm.PlayFabId) || mm.PlayFabId == leader || stale.Contains(mm.PlayFabId)) continue;
                heir = mm.PlayFabId; break;
            }
            if (heir != me) return; // nguoi khac nhan (moi may tinh cung 1 ket qua)
            Debug.Log("[Party-HB] Truong nhom " + leader + " mat ket noi -> minh nhan truong nhom + da truong nhom cu.");
            svc.SetLeader(Code, me, null, LogErr);
            svc.KickMember(Code, leader, null, LogErr);
            _seenVal.Remove(leader); _seenAt.Remove(leader);
            st.LeaderId = me; st.Members.RemoveAll(m => m != null && m.PlayFabId == leader);
            IsLeader = true; SelfReady = true;
            if (_pendingSettings != null) { _lastPublished = null; PublishSettings(_pendingSettings); }
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
        private readonly System.Collections.Generic.Dictionary<int, string> _slotMember = new System.Collections.Generic.Dictionary<int, string>(); // o -> PlayFabId

        private void ApplyToPanel(PartyState st)
        {
            if (_party == null || st == null) return;
            int n = _party.SlotCount;
            for (int i = 0; i < n; i++)
            {
                if (st.Members != null && i < st.Members.Count)
                {
                    var m = st.Members[i];
                    // [Aemy 2026-10-22] O cua minh: theo trang thai local (giong nut) -> khong lech trong luc cho server.
                    bool ready = m.PlayFabId == SelfId() ? SelfReady : m.Ready;
                    _party.SetMember(i, m.Name, m.PlayFabId == st.LeaderId, ready ? "Sẵn sàng" : "Chưa sẵn sàng");
                    // [Aemy 2026-10-22] Doi nguoi trong o (co nguoi roi di, danh sach don len) -> bo avatar cua nguoi cu.
                    string prevId; _slotMember.TryGetValue(i, out prevId);
                    if (prevId != m.PlayFabId)
                    {
                        if (prevId != null) { _avatarShown.Remove(i); _party.ResetMemberAvatar(i); }
                        _slotMember[i] = m.PlayFabId;
                    }
                    if (!string.IsNullOrEmpty(m.AvatarUrl))
                    {
                        string shown; _avatarShown.TryGetValue(i, out shown);
                        if (shown != m.AvatarUrl) { _avatarShown[i] = m.AvatarUrl; StartCoroutine(DownloadPartyAvatar(i, m.AvatarUrl)); }
                    }
                }
                else
                {
                    _party.SetEmpty(i);
                    if (_slotMember.Remove(i)) { _avatarShown.Remove(i); _party.ResetMemberAvatar(i); }
                }
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
                string still; if (!_avatarShown.TryGetValue(index, out still) || still != url) yield break; // o da doi nguoi trong luc tai
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _party.SetMemberAvatar(index, sp);
            }
        }

        private static void LogErr(AuthError e) { if (e != null) Debug.LogWarning("[Party] " + e.ErrorMessage); }
    }
}
