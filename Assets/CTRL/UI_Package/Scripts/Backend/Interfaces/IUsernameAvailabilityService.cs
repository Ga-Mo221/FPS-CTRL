using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Kiểm tra một username còn trống hay đã có người dùng — cho indicator
    /// "CÒN TRỐNG / ĐÃ TỒN TẠI" ở màn Đăng ký. Tách riêng theo Interface Segregation:
    /// màn login/quên mật khẩu không phải phụ thuộc vào việc tra username.
    /// </summary>
    public interface IUsernameAvailabilityService
    {
        /// <param name="onResult">true = còn trống; false = đã có người dùng.</param>
        void CheckUsername(string username,
            Action<bool> onResult,
            Action<AuthError> onError);
    }
}
