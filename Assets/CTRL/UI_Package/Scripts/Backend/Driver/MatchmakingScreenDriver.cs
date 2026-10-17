using UnityEngine;
using Backend.Bootstrap;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Cầu nối "prefab Matchmaking (MatchmakingController) <-> backend IMatchmakingService".
    /// Bơm trạng thái tìm trận vào UI, xử lý Cancel, và áp cơ chế KHÓA THEO VERIFIED.
    /// KHÔNG phụ thuộc Fusion / FPS-CTRL — test trọn Phase 2 ngay trong sandbox UI.
    /// </summary>
    public class MatchmakingScreenDriver : MonoBehaviour
    {
        [Header("View (prefab Match making)")]
        [SerializeField] private MatchmakingController _view;

        [Header("Gate")]
        [SerializeField] private bool requireVerifiedEmail = true;
        [SerializeField] private bool allowGuestWithoutEmail = true;
        [SerializeField] private bool autoLoginGuestIfNeeded = true;

        [Tooltip("Guest ID để test. Trên CÙNG 1 máy mở 2 cửa sổ, đặt 2 ID KHÁC NHAU " +
                 "để thành 2 tài khoản khác nhau -> ghép được 2 người cùng MatchId. " +
                 "Để trống = dùng Device ID (mặc định).")]
        [SerializeField] private string _guestId = "tester1";

        [Header("Test")]
        [SerializeField] private bool showTestButtons = true;

        private bool _subscribed;
        private bool _searching;

        private void Awake()
        {
            if (_view == null) _view = GetComponentInChildren<MatchmakingController>(true);
        }

        private void OnEnable()
        {
            if (_view != null && !_subscribed) { _view.CancelClicked += OnCancelClicked; _subscribed = true; }
        }

        private void OnDisable()
        {
            if (_view != null && _subscribed) { _view.CancelClicked -= OnCancelClicked; _subscribed = false; }
        }

        public void StartFFA() => Begin(MatchMode.FFA);
        public void StartTDM() => Begin(MatchMode.TDM);

        public void Begin(MatchMode mode)
        {
            if (_searching) return;

            if (!BackendBootstrap.AuthService.IsLoggedIn && autoLoginGuestIfNeeded)
            {
                Title("Đang đăng nhập khách (" + _guestId + ")...");
                // Dùng Guest ID riêng để 2 cửa sổ trên cùng máy ra 2 account khác nhau.
                string id = string.IsNullOrEmpty(_guestId) ? null : _guestId;
                BackendBootstrap.AuthService.LoginAsGuest(id,
                    _ => Gate(mode),
                    e => Title("Lỗi login: " + e.ErrorMessage));
                return;
            }
            Gate(mode);
        }

        private void Gate(MatchMode mode)
        {
            if (!requireVerifiedEmail) { Search(mode); return; }

            Title("Đang kiểm tra xác thực email...");
            BackendBootstrap.EmailVerificationStatusService.GetEmailVerificationStatus(
                st =>
                {
                    bool ok = st == EmailVerifyStatus.Confirmed
                              || (allowGuestWithoutEmail && st == EmailVerifyStatus.NoContactEmail);
                    if (ok) Search(mode);
                    else { ShowView(); Title("[KHOA] Email chua xac thuc (" + st + ")"); }
                },
                e => Title("Lỗi check verify: " + e.ErrorMessage));
        }

        private void Search(MatchMode mode)
        {
            _searching = true;
            ShowView();
            if (_view != null)
            {
                _view.SetTitle("ĐANG TÌM TRẬN");
                _view.SetMode(mode == MatchMode.TDM ? "TEAM DEATHMATCH" : "FREE FOR ALL");
                _view.ResetTimer();
                _view.StartTimer();
            }

            BackendBootstrap.MatchmakingService.StartMatchmaking(
                new MatchmakingRequest { Mode = mode },
                onMatchFound: m =>
                {
                    _searching = false;
                    if (_view != null) { _view.StopTimer(); _view.SetTitle("ĐÃ TÌM THẤY TRẬN!"); }
                    Debug.Log("[MMDriver] MatchFound MatchId=" + m.MatchId);
                    BackendBootstrap.MatchHandoff.HandOff(m);
                },
                onProgress: p => { /* MatchmakingController tự đếm giờ */ },
                onError: e =>
                {
                    _searching = false;
                    if (_view != null) { _view.StopTimer(); _view.SetTitle("LỖI: " + e.ErrorCode); }
                    Debug.LogWarning("[MMDriver] " + e.ErrorMessage);
                },
                onTimedOut: () =>
                {
                    _searching = false;
                    if (_view != null) { _view.StopTimer(); _view.SetTitle("KHÔNG TÌM ĐƯỢC TRẬN (HẾT GIỜ)"); }
                    // [FIX BUG-MATCHMAKING 2026-09-14 - Aemy] tat solo-fallback khi test. Bat lai: bo comment dong duoi.
                    // BackendBootstrap.MatchHandoff.HandOffSolo(mode);
                },
                onCancelled: () =>
                {
                    _searching = false;
                    if (_view != null) _view.StopTimer();
                    Title("Đã hủy tìm trận.");
                });
        }

        private void OnCancelClicked()
        {
            BackendBootstrap.MatchmakingService.CancelMatchmaking(() =>
            {
                _searching = false;
                if (_view != null) _view.StopTimer();
                Debug.Log("[MMDriver] Cancelled");
            });
        }

        private void ShowView() { if (_view != null) _view.gameObject.SetActive(true); }

        private void Title(string t)
        {
            Debug.Log("[MMDriver] " + t);
            if (_view != null) _view.SetTitle(t);
        }

        private void OnGUI()
        {
            if (!showTestButtons) return;
            GUILayout.BeginArea(new Rect(8, 8, 250, 200), GUI.skin.box);
            GUILayout.Label("<b>MATCHMAKING TEST</b>");

            bool loggedIn = BackendBootstrap.AuthService != null && BackendBootstrap.AuthService.IsLoggedIn;
            GUILayout.Label(loggedIn
                ? "Account: " + BackendBootstrap.AuthService.CurrentUserId
                : "Chua login");

            GUILayout.BeginHorizontal();
            GUILayout.Label("GuestId", GUILayout.Width(60));
            _guestId = GUILayout.TextField(_guestId);
            GUILayout.EndHorizontal();
            GUILayout.Label("(2 cua so -> 2 GuestId khac nhau)");

            if (GUILayout.Button("Tim tran FFA")) StartFFA();
            if (GUILayout.Button("Tim tran TDM")) StartTDM();
            if (GUILayout.Button("Huy")) OnCancelClicked();
            GUILayout.EndArea();
        }
    }
}
