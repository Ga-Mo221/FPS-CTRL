using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Khôi phục tài khoản & xác thực email. Tách riêng theo Interface Segregation.
    /// </summary>
    public interface IAccountRecoveryService
    {
        // Quên mật khẩu: PlayFab gửi email chứa link/mã đặt lại mật khẩu tới địa chỉ này.
        void SendPasswordRecoveryEmail(string email,
            Action onSuccess,
            Action<AuthError> onFailure);

        // Đặt/cập nhật contact email cho player đang đăng nhập; nếu Title bật template
        // "Email Verification", PlayFab sẽ gửi email xác thực tới địa chỉ đó.
        void SendEmailVerification(string email,
            Action onSuccess,
            Action<AuthError> onFailure);
    }
}
