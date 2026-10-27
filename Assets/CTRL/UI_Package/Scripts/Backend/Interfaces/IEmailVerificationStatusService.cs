using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Đọc trạng thái xác thực email của player đang đăng nhập.
    /// Dùng cho cơ chế "khóa theo verified" (chặn vào trận/tính năng khi chưa Confirmed).
    /// </summary>
    public interface IEmailVerificationStatusService
    {
        void GetEmailVerificationStatus(
            Action<EmailVerifyStatus> onResult,
            Action<AuthError> onFailure);
    }
}
