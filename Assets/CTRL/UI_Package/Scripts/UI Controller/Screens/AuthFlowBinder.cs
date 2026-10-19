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
        [Tooltip("[Aemy 2026-10-19] Tai khoan dang ky bang email phai bam link xac minh moi dang nhap duoc.\n" +
                 "Can PlayFab: Add-ons > SMTP + Email Template (Email Verification) + Rule player_updated_contact_email.")]
        [SerializeField] private bool _requireVerifiedEmail = true;
        [Tooltip("Gui lai mail xac minh toi da 1 lan / bao nhieu giay cho moi tai khoan (chong spam).")]
        [SerializeField] private float _resendCooldownSeconds = 120f;
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
                    // [Aemy 2026-10-19] Chua xac minh email -> khong cho vao game.
                    RequireVerifiedEmail(r.PlayerId, () =>
                    {
                        SessionCache.ResetForNewSession(); // [Aemy 2026-10-17] khong lan du lieu tai khoan truoc
                        if (_notice != null) _notice.Hide();
                        SceneLoader.LoadWithLoading(_menuScene);
                    });
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
                        SessionCache.ResetForNewSession(); // [Aemy 2026-10-17]
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

        // [Aemy 2026-10-19] Cong xac minh email sau khi dang nhap bang ten/email + mat khau.
        //  - Confirmed -> vao game.
        //  - NoContactEmail (tai khoan cu / khong co email) -> cho vao (khong co gi de xac minh).
        //  - Pending / Unverified -> gui lai mail (co cooldown), dang xuat, bao kiem tra hop thu.
        //  - Khong doc duoc trang thai (Title chua bat "Contact email addresses" trong Client Profile Options, mat mang...)
        //    -> cho vao + canh bao log, tranh khoa nham moi nguoi.
        private void RequireVerifiedEmail(string playerId, System.Action proceed)
        {
            if (!_requireVerifiedEmail || _useMock || _verifyStatus == null) { proceed(); return; }
            if (_notice != null) _notice.Show("ĐANG KIỂM TRA EMAIL…");
            _verifyStatus.GetEmailVerificationStatus(
                st =>
                {
                    if (st == EmailVerifyStatus.Confirmed || st == EmailVerifyStatus.NoContactEmail) { proceed(); return; }
                    Debug.Log("[AuthFlowBinder] Email chưa xác minh (" + st + ") -> chặn đăng nhập.");
                    BlockUnverified(playerId);
                },
                e =>
                {
                    Debug.LogWarning("[AuthFlowBinder] Không đọc được trạng thái email (" + e.ErrorMessage + ") -> cho vào game.");
                    proceed();
                });
        }

        private void BlockUnverified(string playerId)
        {
            var resend = _verifyStatus as IEmailVerificationResendService;
            string key = "VerifyMailSentAt_" + playerId;
            double last = 0; double.TryParse(PlayerPrefs.GetString(key, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out last);
            double now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool canResend = resend != null && now - last >= _resendCooldownSeconds;

            System.Action<string> done = email =>
            {
                try { _auth.Logout(); } catch (System.Exception ex) { Debug.LogWarning("[AuthFlowBinder] Logout lỗi: " + ex.Message); }
                if (_notice == null) return;
                if (!string.IsNullOrEmpty(email))
                    _notice.Show("EMAIL CHƯA ĐƯỢC XÁC MINH\nĐã gửi link xác minh tới " + MaskEmail(email) + ".\nMở link (kiểm tra cả mục Spam) rồi đăng nhập lại.", 8f);
                else
                    _notice.Show("EMAIL CHƯA ĐƯỢC XÁC MINH\nMở link trong hộp thư (kiểm tra cả mục Spam) rồi đăng nhập lại.", 8f);
            };

            if (!canResend) { done(null); return; }
            resend.ResendVerificationEmail(
                email =>
                {
                    PlayerPrefs.SetString(key, now.ToString(System.Globalization.CultureInfo.InvariantCulture)); PlayerPrefs.Save();
                    done(email);
                },
                e => { Debug.LogWarning("[AuthFlowBinder] Gửi lại mail xác minh lỗi: " + e.ErrorMessage); done(null); });
        }

        // "nyx.vank@gmail.com" -> "ny•••@gmail.com"
        private static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0) return email;
            string head = email.Substring(0, Mathf.Min(2, at));
            return head + "•••" + email.Substring(at);
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
                    Debug.Log($"[AuthFlowBinder] Đăng ký OK: {r.PlayerId}. Quay về trang đăng nhập.");
                    if (_register != null) _register.Clear();
                    // [Aemy 2026-10-17] Dang ky xong KHONG vao thang game nua:
                    // dung session tam cua Register de dat ten hien thi + du lieu mac dinh, roi dang xuat
                    // va quay ve trang Dang nhap (dien san ten) de nguoi choi tu dang nhap.
                    if (_notice != null) _notice.Show("ĐANG TẠO TÀI KHOẢN…");
                    string loginName = !string.IsNullOrWhiteSpace(operatorName) ? operatorName.Trim() : email;
                    System.Action backToLogin = () => ReturnToLoginAfterRegister(loginName, email);
                    if (!_useMock && BackendBootstrap.PlayerProfileService != null)
                    {
                        System.Action initThenBack = () => BackendBootstrap.PlayerProfileService.InitializeNewPlayer(backToLogin, _ie => backToLogin());
                        if (!string.IsNullOrWhiteSpace(operatorName))
                            BackendBootstrap.PlayerProfileService.SetDisplayName(operatorName, initThenBack, _de => initThenBack());
                        else initThenBack();
                    }
                    else backToLogin();
                },
                e =>
                {
                    Debug.LogWarning($"[AuthFlowBinder] Đăng ký lỗi: {e.ErrorCode} - {e.ErrorMessage}");
                    bool nameTaken = e.ErrorCode != null && e.ErrorCode.Contains("Username");
                    if (_register != null)
                        _register.SetAvailability(nameTaken ? "TÊN ĐÃ TỒN TẠI" : e.ErrorMessage, false);
                });
        }

        // [Aemy 2026-10-17] Dang xuat session tam sau Register, ve form Dang nhap + thong bao kiem tra email.
        private void ReturnToLoginAfterRegister(string loginName, string email)
        {
            if (this == null) return;
            try { _auth.Logout(); } catch (System.Exception ex) { Debug.LogWarning("[AuthFlowBinder] Logout sau đăng ký lỗi: " + ex.Message); }
            if (_shell != null) _shell.Show(AuthShellController.AuthScreen.Login);
            if (_login != null) _login.PrefillAfterRegister(loginName);
            if (_notice != null)
            {
                string msg = string.IsNullOrWhiteSpace(email)
                    ? "ĐĂNG KÝ THÀNH CÔNG!\nHãy đăng nhập để vào game."
                    : "ĐĂNG KÝ THÀNH CÔNG!\nĐã gửi email xác minh tới " + email + ".\nMở link trong email rồi đăng nhập."; // [Aemy 2026-10-19] phai xac minh moi vao duoc
                _notice.Show(msg, 6f);
            }
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