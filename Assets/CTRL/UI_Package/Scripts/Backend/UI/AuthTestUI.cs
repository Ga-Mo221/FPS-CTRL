using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.Models;
using Backend.Bootstrap;

namespace Backend.UI
{
    /// <summary>
    /// UI test toàn bộ Auth + hiển thị cơ chế KHÓA THEO VERIFIED.
    /// Sau mỗi lần đăng nhập, tự đọc trạng thái email và cho biết đủ điều kiện vào trận chưa.
    /// </summary>
    public class AuthTestUI : MonoBehaviour
    {
        [Header("Register")]
        [SerializeField] private TMP_InputField regUsername;
        [SerializeField] private TMP_InputField regEmail;
        [SerializeField] private TMP_InputField regPassword;
        [SerializeField] private Button regButton;

        [Header("Login")]
        [SerializeField] private TMP_InputField loginUsername;
        [SerializeField] private TMP_InputField loginPassword;
        [SerializeField] private Button loginButton;

        [Header("Guest / Google (optional)")]
        [SerializeField] private Button guestButton;
        [SerializeField] private Button googleButton;
        [SerializeField] private TMP_InputField googleServerAuthCode;

        [Header("Forgot Password / Verify Email (optional)")]
        [SerializeField] private TMP_InputField recoveryEmail;
        [SerializeField] private Button forgotPasswordButton;
        [SerializeField] private Button verifyEmailButton;

        [Header("Gate")]
        [Tooltip("Bật: khách (chưa có email) vẫn được coi là qua cửa. Tắt: khách cũng bị khóa.")]
        [SerializeField] private bool allowGuestWithoutEmail = true;

        [Header("Output")]
        [SerializeField] private TMP_Text statusText;

        private void Start()
        {
            regButton.onClick.AddListener(OnRegisterClicked);
            loginButton.onClick.AddListener(OnLoginClicked);

            guestButton?.onClick.AddListener(OnGuestClicked);
            googleButton?.onClick.AddListener(OnGoogleClicked);
            forgotPasswordButton?.onClick.AddListener(OnForgotPasswordClicked);
            verifyEmailButton?.onClick.AddListener(OnVerifyEmailClicked);

            SetStatus("San sang. Dien form va bam nut de test.");
        }

        private void OnRegisterClicked()
        {
            var req = new RegisterRequest
            {
                Username = regUsername.text,
                Email = regEmail.text,
                Password = regPassword.text
            };
            SetStatus("Dang dang ky...");
            BackendBootstrap.AuthService.Register(
                req,
                result => { SetStatus($"<color=#4ade80>[OK] Dang ky thanh cong. Da gui email xac thuc.</color>\nPlayerId: {result.PlayerId}"); CheckGate(); },
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        private void OnLoginClicked()
        {
            var input = loginUsername.text.Trim();
            var isEmail = input.Contains("@");
            var req = new LoginRequest { UsernameorEmail = input, Password = loginPassword.text, UseEmail = isEmail };
            SetStatus(isEmail ? "Đang đăng nhập bằng email..." : "Đang đăng nhập bằng username...");
            BackendBootstrap.AuthService.Login(
                req,
                result => { SetStatus($"<color=#4ade80>[OK] Dang nhap thanh cong</color>\nPlayerId: {result.PlayerId}"); CheckGate(); },
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        private void OnGuestClicked()
        {
            SetStatus("Dang dang nhap khach...");
            BackendBootstrap.AuthService.LoginAsGuest(
                null,
                result => { SetStatus($"<color=#4ade80>[OK] Dang nhap khach thanh cong</color>\nPlayerId: {result.PlayerId}"); CheckGate(); },
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        private void OnGoogleClicked()
        {
            var code = googleServerAuthCode != null ? googleServerAuthCode.text.Trim() : string.Empty;
            SetStatus("Dang dang nhap bang Google...");
            BackendBootstrap.ExternalAuthService.LoginWithGoogle(
                new GoogleLoginRequest { ServerAuthCode = code, CreateAccount = true },
                result => { SetStatus($"<color=#4ade80>[OK] Google login thanh cong</color>\nPlayerId: {result.PlayerId}"); CheckGate(); },
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        private void OnForgotPasswordClicked()
        {
            var email = recoveryEmail != null ? recoveryEmail.text.Trim() : string.Empty;
            SetStatus("Dang gui email khoi phuc mat khau...");
            BackendBootstrap.AccountRecoveryService.SendPasswordRecoveryEmail(
                email,
                () => SetStatus($"<color=#4ade80>[OK] Da gui email khoi phuc toi {email}.</color>"),
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        private void OnVerifyEmailClicked()
        {
            var email = recoveryEmail != null ? recoveryEmail.text.Trim() : string.Empty;
            SetStatus("Dang gui email xac thuc...");
            BackendBootstrap.AccountRecoveryService.SendEmailVerification(
                email,
                () => SetStatus($"<color=#4ade80>[OK] Da gui email xac thuc toi {email}.</color>"),
                error => SetStatus($"<color=#f87171>[LOI] {error.ErrorCode}</color>\n{error.ErrorMessage}"));
        }

        // ===== Cơ chế KHÓA THEO VERIFIED =====
        private void CheckGate()
        {
            BackendBootstrap.EmailVerificationStatusService.GetEmailVerificationStatus(
                status =>
                {
                    bool allowed = status == EmailVerifyStatus.Confirmed
                                   || (allowGuestWithoutEmail && status == EmailVerifyStatus.NoContactEmail);

                    string gate = allowed
                        ? "<color=#4ade80>[MO KHOA] Du dieu kien vao tran.</color>"
                        : "<color=#fbbf24>[KHOA] Chua xac thuc email — hay bam link trong mail roi dang nhap lai.</color>";

                    AppendStatus($"Email status: <b>{status}</b>\n{gate}");
                },
                error => AppendStatus($"<color=#f87171>[LOI check verify] {error.ErrorMessage}</color>"));
        }

        private void SetStatus(string msg)
        {
            Debug.Log("[AuthTestUI] " + msg);
            if (statusText != null) statusText.text = msg;
        }

        private void AppendStatus(string msg)
        {
            Debug.Log("[AuthTestUI] " + msg);
            if (statusText != null) statusText.text += "\n" + msg;
        }
    }
}
