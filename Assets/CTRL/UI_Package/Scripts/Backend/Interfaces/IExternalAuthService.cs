using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Đăng nhập qua nhà cung cấp bên thứ ba (Google/Gmail...).
    /// Tách khỏi IAuthService theo Interface Segregation Principle:
    /// code chỉ cần đăng nhập thường không phải phụ thuộc vào Google.
    /// </summary>
    public interface IExternalAuthService
    {
        void LoginWithGoogle(GoogleLoginRequest request,
            Action<AuthResult> onSuccess,
            Action<AuthError> onFailure);

        /// <summary>Lien ket tai khoan Google vao tai khoan HIEN TAI (dang dang nhap).</summary>
        void GetLinkedAccountInfo(Action<LinkedAccountInfo> onResult, Action<AuthError> onFailure);

        void LinkGoogleAccount(string serverAuthCode,
            Action onSuccess,
            Action<AuthError> onFailure);
    }
}
