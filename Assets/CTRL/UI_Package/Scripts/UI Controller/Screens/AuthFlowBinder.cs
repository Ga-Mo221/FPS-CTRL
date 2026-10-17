using Backend.Bootstrap;
using Backend.Interfaces;
using Backend.Models;
using Backend.Mock;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The glue between the auth panel controllers (presentation) and the auth
    /// services (backend). Subscribes to each controller's intent events, translates
    /// them into service DTOs, calls the port, and pushes results back into the UI
    /// (status text, availability indicator, navigation). The one place that knows
    /// both worlds — controllers stay ignorant of PlayFab, services ignorant of UI.
    ///
    /// Default runs on <see cref="MockAuthService"/> so the flow is testable without
    /// PlayFab. Turn <c>_useMock</c> off to consume the real services from
    /// <see cref="BackendBootstrap"/>; nothing else changes because everything here
    /// depends only on interfaces.
    /// </summary>
    public class AuthFlowBinder : MonoBehaviour
    {
        [Header("Shell + panel controllers")]
        [SerializeField] private AuthShellController _shell;
        [SerializeField] private LoginController _login;
        [SerializeField] private RegisterController _register;
        [SerializeField] private ForgotPasswordController _forgot;

        [Header("Service source")]
        [Tooltip("On: in-memory MockAuthService (no PlayFab). Off: real services from BackendBootstrap.")]
        [SerializeField] private bool _useMock = true;

        private IAuthService _auth;
        private IExternalAuthService _external;
        private IAccountRecoveryService _recovery;
        private IUsernameAvailabilityService _usernames;
        private IEmailVerificationStatusService _verifyStatus;
        [SerializeField] private CenterNotice _notice;
        [SerializeField] private string _menuScene = "MenuShell";
        [Tooltip("Số giây tự tắt popup báo lỗi login.")]
        [SerializeField] private float _errorNoticeSeconds = 5f;
        private bool _emailVerified;

        private void Awake()
        {
            if (_useMock || BackendBootstrap.AuthService == null)
            {
                var mock = new MockAuthService();
                _auth = mock; _external = mock; _recovery = mock; _usernames = mock; _verifyStatus = mock;
                Debug.Log("[AuthFlowBinder] Using MockAuthService (no PlayFab).");
            }
            else
            {
                _auth = BackendBootstrap.AuthService;
                _external = BackendBootstrap.ExternalAuthService;
                _recovery = BackendBootstrap.AccountRecoveryService;
                _usernames = BackendBootstrap.UsernameAvailabilityService;
                _verifyStatus = BackendBootstrap.EmailVerificationStatusService;
                Debug.Log("[AuthFlowBinder] Using BackendBootstrap services.");
            }
            if (_register != null) { _register.RegisterSubmitted += OnRegister; _register.UsernameChanged += OnUsernameChanged; }
            if (_forgot != null) _forgot.ResetRequested += OnReset;
            if (_login != null) { _login.LoginSubmitted += OnLogin; _login.GoogleClicked += OnGoogleLogin; }
            // [REMEMBER 2026-09-29 Aemy] Prefill neu da tick ghi nho dang nhap.
            if (_login != null && PlayerPrefs.GetInt("RememberLogin", 0) == 1)
                _login.Prefill(PlayerPrefs.GetString("SavedUser", ""), RememberDec(PlayerPrefs.GetString("SavedPass", "")), true);
        }

        // [REMEMBER 2026-09-29 Aemy] Luu/xoa + ma hoa nhe thong tin dang nhap (base64, khong phai bao mat that).
        private static void RememberSave(bool remember, string user, string pass)
        {
            if (remember) { PlayerPrefs.SetInt("RememberLogin", 1); PlayerPrefs.SetString("SavedUser", user); PlayerPrefs.SetString("SavedPass", RememberEnc(pass)); }
            else { PlayerPrefs.DeleteKey("RememberLogin"); PlayerPrefs.DeleteKey("SavedUser"); PlayerPrefs.DeleteKey("SavedPass"); }
            PlayerPrefs.Save();
        }
        private static string RememberEnc(string s) { return string.IsNullOrEmpty(s) ? "" : System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)); }
        private static string RememberDec(string s) { if (string.IsNullOrEmpty(s)) return ""; try { return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(s)); } catch { return ""; } }

        private void OnDestroy()
        {
            if (_register != null) { _register.RegisterSubmitted -= OnRegister; _register.UsernameChanged -= OnUsernameChanged; }
            if (_forgot != null) _forgot.ResetRequested -= OnReset;
            if (_login != null) { _login.LoginSubmitted -= OnLogin; _login.GoogleClicked -= OnGoogleLogin; }
        }

        // ---- Login ----
        // Nguoi dung bam Dang nhap: dung/email + mat khau -> AuthService.Login.
        // Thanh cong -> vao MenuShell QUA MAN HINH LOADING (SceneLoader.LoadWithLoading).
        private void OnLogin(string usernameOrEmail, string password, bool remember)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                if (_notice != null) _notice.Show("NHẬP TÊN/EMAIL VÀ MẬT KHẨU", _errorNoticeSeconds);
                return;
            }
            bool isEmail = usernameOrEmail.Contains("@");
            var req = new LoginRequest { UsernameorEmail = usernameOrEmail.Trim(), Password = password, UseEmail = isEmail };
            if (_notice != null) _notice.Show("ĐANG ĐĂNG NHẬP…");
            _auth.Login(req,
                r =>
                {
                    Debug.Log("[AuthFlowBinder] Đăng nhập OK: " + r.PlayerId);
                    RememberSave(remember, usernameOrEmail.Trim(), password); // [REMEMBER 2026-09-29 Aemy]
                    if (_notice != null) _notice.Hide();
                    SceneLoader.LoadWithLoading(_menuScene);
                },
                e =>
                {
                    Debug.LogWarning("[AuthFlowBinder] Đăng nhập lỗi: " + e.ErrorCode + " - " + e.ErrorMessage);
                    if (_notice != null) _notice.Show("SAI TÀI KHOẢN HOẶC MẬT KHẨU", _errorNoticeSeconds);
                });
        }

        // Dang nhap bang Google (PC: mo trinh duyet lay server auth code roi LoginWithGoogle).
        private async void OnGoogleLogin()
        {
            if (_useMock)
            {
                if (_notice != null) _notice.Show("GOOGLE CHỈ CHẠY VỚI PLAYFAB THẬT", _errorNoticeSeconds);
                return;
            }
            try
            {
                if (_notice != null) _notice.Show("ĐANG MỞ GOOGLE…");
                string code = await BackendBootstrap.PCGoogleAuthService.GetServerAuthCodeAsync(
                    BackendBootstrap.GoogleWebClientId, BackendBootstrap.GoogleLocalPort);
                if (string.IsNullOrEmpty(code))
                {
                    if (_notice != null) _notice.Show("ĐÃ HỦY ĐĂNG NHẬP GOOGLE", _errorNoticeSeconds);
                    return;
                }
                var g = new GoogleLoginRequest { ServerAuthCode = code, CreateAccount = true };
                _external.LoginWithGoogle(g,
                    r =>
                    {
                        if (_notice != null) _notice.Hide();
                        SceneLoader.LoadWithLoading(_menuScene);
                    },
                    e =>
                    {
                        if (_notice != null) _notice.Show("GOOGLE LỖI: " + e.ErrorMessage, _errorNoticeSeconds);
                    });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[AuthFlowBinder] Google login lỗi: " + ex.Message);
                if (_notice != null) _notice.Show("GOOGLE LỖI", _errorNoticeSeconds);
            }
        }

        // ---- Register ----
        private void OnRegister(string operatorName, string email, string password, string confirm, bool agree)
        {
            if (!agree)
            {
                if (_register != null) _register.SetAvailability("CẦN ĐỒNG Ý ĐIỀU KHOẢN", false);
                return;
            }
            if (password != confirm)
            {
                if (_register != null) _register.SetAvailability("MẬT KHẨU KHÔNG KHỚP", false);
                return;
            }

            var req = new RegisterRequest { Username = operatorName, Email = email, Password = password };
            _auth.Register(req,
                r =>
                {
                    Debug.Log($"[AuthFlowBinder] Đăng ký OK: {r.PlayerId}. Vào thẳng game.");
                    if (_register != null) _register.Clear();
                    // [REG-FIX 2026-09-29 Aemy] Dang ky xong -> set default + vao thang MenuShell (PlayFab da co session sau Register).
                    System.Action goMenu = () => { if (_notice != null) _notice.Hide(); SceneLoader.LoadWithLoading(_menuScene); };
                    if (!_useMock && BackendBootstrap.PlayerProfileService != null)
                    {
                        System.Action initThenGo = () => BackendBootstrap.PlayerProfileService.InitializeNewPlayer(goMenu, _ie => goMenu());
                        if (!string.IsNullOrWhiteSpace(operatorName))
                            BackendBootstrap.PlayerProfileService.SetDisplayName(operatorName, initThenGo, _de => initThenGo());
                        else initThenGo();
                    }
                    else goMenu();
                },
                e =>
                {
                    Debug.LogWarning($"[AuthFlowBinder] Đăng ký lỗi: {e.ErrorCode} - {e.ErrorMessage}");
                    bool nameTaken = e.ErrorCode != null && e.ErrorCode.Contains("Username");
                    if (_register != null)
                        _register.SetAvailability(nameTaken ? "TÊN ĐÃ TỒN TẠI" : e.ErrorMessage, false);
                });
        }

        // Live username availability while typing (debounced by the controller).
        private void OnUsernameChanged(string username)
        {
            if (_register == null) return;
            if (_usernames == null || string.IsNullOrWhiteSpace(username))
            {
                _register.SetAvailability(string.Empty);
                return;
            }
            _register.SetAvailability("ĐANG KIỂM TRA…", null);
            _usernames.CheckUsername(username,
                available => _register.SetAvailability(available ? "CÒN TRỐNG" : "ĐÃ TỒN TẠI", available),
                e =>
                {
                    // Can't check right now (e.g. GetAccountInfo needs a session) — stay neutral;
                    // RegisterPlayFabUser will still reject a duplicate name on submit.
                    Debug.Log($"[AuthFlowBinder] Không kiểm tra được username lúc này: {e.ErrorMessage}");
                    _register.SetAvailability(string.Empty, null);
                });
        }

        // ---- Forgot password ----
        private void OnReset(string email)
        {
            _recovery.SendPasswordRecoveryEmail(email,
                () =>
                {
                    Debug.Log("[AuthFlowBinder] Đã gửi email khôi phục.");
                    if (_forgot != null) _forgot.SetStatus("Đã gửi! Kiểm tra hộp thư");
                },
                e =>
                {
                    Debug.LogWarning($"[AuthFlowBinder] Khôi phục lỗi: {e.ErrorMessage}");
                    if (_forgot != null) _forgot.SetStatus("Lỗi: " + e.ErrorMessage);
                });
        }


        // Sau khi đăng ký: hiện banner giữa màn, poll trạng thái xác minh email.
        private IEnumerator WaitForEmailVerification(string email)
        {
            _emailVerified = false;
            _notice.Show("ĐANG CHỜ XÁC MINH EMAIL…\nHãy mở link trong hộp thư: " + email);

            float elapsed = 0f, timeout = 120f, interval = 4f;
            while (!_emailVerified && elapsed < timeout)
            {
                _verifyStatus.GetEmailVerificationStatus(
                    s => { if (s == EmailVerifyStatus.Confirmed) _emailVerified = true; },
                    e => { });
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            if (_emailVerified)
            {
                _notice.Show("ĐÃ XÁC MINH EMAIL — HOÀN TẤT!");
                yield return new WaitForSeconds(2f);
            }
            else
            {
                _notice.Show("CHƯA XÁC MINH — BẠN CÓ THỂ ĐĂNG NHẬP VÀ XÁC MINH SAU.");
                yield return new WaitForSeconds(3f);
            }
            _notice.Hide();
            if (_shell != null) _shell.Show(AuthShellController.AuthScreen.Login);
        }
}
}