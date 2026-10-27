using UnityEngine;
using Backend.Bootstrap;
using Backend.Models;


namespace Game.UI
{
    /// <summary>
    /// Composition root for the authentication flow. Owns the three auth panels
    /// (Login / Register / Forgot password) and shows exactly one at a time.
    /// It listens to navigation events raised by each panel's controller and swaps
    /// the visible panel — so the panels stay decoupled from each other (they only
    /// announce intent; the shell decides what to show). Same one-of-many pattern
    /// as <see cref="ShellController"/>.
    /// </summary>
    public class AuthShellController : MonoBehaviour
    {
        public enum AuthScreen { Login, Register, ForgotPassword }

        [Header("Panels")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private GameObject _forgotPanel;

        [Header("Panel controllers (for navigation events)")]
        [SerializeField] private LoginController _loginController;
        [SerializeField] private RegisterController _registerController;
        [SerializeField] private ForgotPasswordController _forgotController;

        [SerializeField] private AuthScreen _default = AuthScreen.Login;

        [Tooltip("Scene menu chinh se vao sau khi dang nhap thanh cong.")]
        [SerializeField] private string _menuScene = "MenuShell";

        private void Awake()
        {
            if (_loginController != null)
            {
                _loginController.RegisterRequested += ShowRegister;
                _loginController.ForgotRequested += ShowForgot;
                
                // Đăng ký nhận sự kiện Login & Google Click
                // [FIX BUG-LOGIN 2026-09-14 - Aemy] tat 2 subscribe nay: AuthFlowBinder moi la noi goi Login+notice.
                // _loginController.LoginSubmitted += OnLoginSubmitted;
                // _loginController.GoogleClicked += OnGoogleClicked;
            }
            if (_registerController != null)
                _registerController.LoginRequested += ShowLogin;
            if (_forgotController != null)
                _forgotController.BackRequested += ShowLogin;
        }

        private void OnDestroy()
        {
            if (_loginController != null)
            {
                _loginController.RegisterRequested -= ShowRegister;
                _loginController.ForgotRequested -= ShowForgot;
                
                // Hủy đăng ký sự kiện
                // _loginController.LoginSubmitted -= OnLoginSubmitted;
                // _loginController.GoogleClicked -= OnGoogleClicked;
            }
            if (_registerController != null)
                _registerController.LoginRequested -= ShowLogin;
            if (_forgotController != null)
                _forgotController.BackRequested -= ShowLogin;
        }

        private void Start() => Show(_default);

        private void ShowLogin() => Show(AuthScreen.Login);
        private void ShowRegister() => Show(AuthScreen.Register);
        private void ShowForgot() => Show(AuthScreen.ForgotPassword);

        /// <summary>Activate the panel for <paramref name="screen"/>, hide the others.</summary>
        public void Show(AuthScreen screen)
        {
            if (_loginPanel != null) _loginPanel.SetActive(screen == AuthScreen.Login);
            if (_registerPanel != null) _registerPanel.SetActive(screen == AuthScreen.Register);
            if (_forgotPanel != null) _forgotPanel.SetActive(screen == AuthScreen.ForgotPassword);
        }
        
        private async void OnGoogleClicked()
        {
            Debug.Log("[AuthShell] Bắt đầu luồng đăng nhập Google trên PC...");

            // 1. Gọi service mở trình duyệt & lắng nghe callback trả về code
            string authCode = await BackendBootstrap.PCGoogleAuthService.GetServerAuthCodeAsync(
                BackendBootstrap.GoogleWebClientId,
                BackendBootstrap.GoogleLocalPort
            );

            if (string.IsNullOrEmpty(authCode))
            {
                Debug.LogError("[AuthShell] Không lấy được AuthCode từ Google (người dùng hủy hoặc gặp lỗi).");
                return;
            }

            Debug.Log("[AuthShell] Lấy AuthCode thành công, đang xác thực với PlayFab...");

            // GỬI ServerAuthCode sang PlayFab
            var googleReq = new GoogleLoginRequest
            {
                ServerAuthCode = authCode,
                CreateAccount = true
            };

            // Truyền tham số trực tiếp theo thứ tự: (request, onSuccess, onError)
            BackendBootstrap.ExternalAuthService.LoginWithGoogle(
                googleReq,
                authResult =>
                {
                    Debug.Log($"<color=green>[Success]</color> Đăng nhập Google PlayFab thành công! PlayerId: {authResult.PlayerId}");
                    // Dang nhap Google thanh cong -> vao menu chinh (qua man Loading).
                    SceneLoader.LoadWithLoading(_menuScene);
                },
                authError =>
                {
                    Debug.LogError($"<color=red>[Error]</color> Đăng nhập Google PlayFab thất bại: {authError.ErrorCode} - {authError.ErrorMessage}");
                }
            );
        }

        private void OnLoginSubmitted(string emailOrUsername, string password, bool remember)
        {
            // Luồng đăng nhập bằng Email / Username thông thường
            var loginReq = new LoginRequest
            {
                UsernameorEmail = emailOrUsername,
                Password = password,
                UseEmail = emailOrUsername.Contains("@")
            };

            BackendBootstrap.AuthService.Login(
                loginReq,
                onSuccess: result =>
                {
                    Debug.Log($"<color=green>[Success]</color> Đăng nhập PlayFab thành công! PlayerId: {result.PlayerId}");
                    // Dang nhap email/username thanh cong -> vao menu chinh (qua man Loading).
                    SceneLoader.LoadWithLoading(_menuScene);
                },
                onFailure: error =>
                {
                    Debug.LogError($"<color=red>[Error]</color> Đăng nhập thất bại: {error.ErrorMessage}");
                }
            );
        }
    }
}
