using System;
using Backend.Interfaces;
using Backend.Models;
using UnityEngine;

namespace Backend.Mock
{
    /// <summary>
    /// In-memory fake implementing the three auth ports (login, external/Google,
    /// account recovery). It lets the whole auth UI flow run and be tested without
    /// PlayFab or a network. Rules are intentionally trivial; to go live, inject the
    /// real <see cref="PlayfabImpl.PlayFabAuthService"/> (via BackendBootstrap) in its
    /// place — the UI doesn't change because it only depends on the interfaces.
    /// Callbacks fire synchronously (good enough for a mock).
    /// </summary>
    public class MockAuthService : IAuthService, IExternalAuthService, IAccountRecoveryService, IUsernameAvailabilityService, IEmailVerificationStatusService, IEmailVerificationResendService
    {
        public bool IsLoggedIn { get; private set; }
        public string CurrentUserId { get; private set; }
        private int _verifyPolls;

        public void Register(RegisterRequest request, Action<AuthResult> onSuccess, Action<AuthError> onFailure)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            { Fail(onFailure, "INVALID_INPUT", "Email/mật khẩu không hợp lệ."); return; }

            IsLoggedIn = true;
            CurrentUserId = "mock-" + request.Username;
            Debug.Log($"[MockAuth] Register OK — user={request.Username}, email={request.Email}");
            onSuccess?.Invoke(new AuthResult { PlayerId = CurrentUserId, SessionId = "mock-session", IsNewlyCreated = true });
        }

        public void Login(LoginRequest request, Action<AuthResult> onSuccess, Action<AuthError> onFailure)
        {
            if (request == null || string.IsNullOrEmpty(request.Password))
            { Fail(onFailure, "BAD_CREDENTIALS", "Sai mật khẩu."); return; }

            IsLoggedIn = true;
            CurrentUserId = "mock-" + request.UsernameorEmail;
            Debug.Log($"[MockAuth] Login OK — {request.UsernameorEmail} (useEmail={request.UseEmail})");
            onSuccess?.Invoke(new AuthResult { PlayerId = CurrentUserId, SessionId = "mock-session", IsNewlyCreated = false });
        }


        public void LoginAsGuest(string deviceId, Action<AuthResult> onSuccess, Action<AuthError> onFailure)
        {
            IsLoggedIn = true;
            CurrentUserId = "mock-guest";
            Debug.Log("[MockAuth] Guest login OK");
            onSuccess?.Invoke(new AuthResult { PlayerId = CurrentUserId, SessionId = "mock-session", IsNewlyCreated = true });
        }

        public void Logout()
        {
            IsLoggedIn = false;
            CurrentUserId = null;
            Debug.Log("[MockAuth] Logout");
        }

        public void LoginWithGoogle(GoogleLoginRequest request, Action<AuthResult> onSuccess, Action<AuthError> onFailure)
        {
            IsLoggedIn = true;
            CurrentUserId = "mock-google";
            Debug.Log("[MockAuth] Google login OK");
            onSuccess?.Invoke(new AuthResult { PlayerId = CurrentUserId, SessionId = "mock-session", IsNewlyCreated = false });
        }


        public void LinkGoogleAccount(string serverAuthCode, Action onSuccess, Action<AuthError> onFailure)
        {
            Debug.Log("[MockAuth] Link Google OK");
            _mockGoogleLinked = true; 
            onSuccess?.Invoke();
        }

        public void SendPasswordRecoveryEmail(string email, Action onSuccess, Action<AuthError> onFailure)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            { Fail(onFailure, "INVALID_EMAIL", "Email không hợp lệ."); return; }
            Debug.Log($"[MockAuth] Recovery email sent -> {email}");
            onSuccess?.Invoke();
        }

        public void SendEmailVerification(string email, Action onSuccess, Action<AuthError> onFailure)
        {
            Debug.Log($"[MockAuth] Verification email sent -> {email}");
            onSuccess?.Invoke();
        }
        
        private bool _mockGoogleLinked;
        public void GetLinkedAccountInfo(Action<LinkedAccountInfo> onResult, Action<AuthError> onFailure)
        {
            onResult?.Invoke(new LinkedAccountInfo { Email = "mock@example.com", GoogleLinked = _mockGoogleLinked });
        }

        private static void Fail(Action<AuthError> onFailure, string code, string message)
        {
            Debug.LogWarning($"[MockAuth] {code}: {message}");
            onFailure?.Invoke(new AuthError { ErrorCode = code, ErrorMessage = message });
        }
    

        public void CheckUsername(string username, Action<bool> onResult, Action<AuthError> onError)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                onError?.Invoke(new AuthError { ErrorCode = "EmptyUsername", ErrorMessage = "Tên đăng nhập trống." });
                return;
            }
            var taken = new[] { "admin", "operator_01", "nyx", "aemy" };
            bool available = System.Array.IndexOf(taken, username.Trim().ToLowerInvariant()) < 0;
            Debug.Log($"[MockAuth] CheckUsername '{username}' -> {(available ? "CON TRONG" : "DA TON TAI")}");
            onResult?.Invoke(available);
        }


        public void GetEmailVerificationStatus(Action<EmailVerifyStatus> onResult, Action<AuthError> onError)
        {
            _verifyPolls++;
            var status = _verifyPolls >= 3 ? EmailVerifyStatus.Confirmed : EmailVerifyStatus.Pending;
            Debug.Log($"[MockAuth] EmailVerificationStatus poll #{_verifyPolls} -> {status}");
            onResult?.Invoke(status);
        }

        // Gia lap gui lai mail xac minh.
        public void ResendVerificationEmail(Action<string> onSent, Action<AuthError> onFailure)
        {
            Debug.Log("[MockAuth] ResendVerificationEmail");
            onSent?.Invoke("mock@example.com");
        }
}
}
