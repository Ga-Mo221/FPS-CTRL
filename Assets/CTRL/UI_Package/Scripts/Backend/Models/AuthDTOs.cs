namespace Backend.Models
{
    public class RegisterRequest
    {
        public string Username;
        public string Email;
        public string Password;

        // Sau khi đăng ký thành công, tự đặt contact email + gửi email xác thực
        // (nếu Title đã bật template "Email Verification").
        public bool SendVerificationEmail = true;
    }

    public class LoginRequest
    {
        public string UsernameorEmail;
        public string Password;
        public bool UseEmail;
    }

    // Đăng nhập bằng liên kết Google (Gmail).
    public class GoogleLoginRequest
    {
        public string ServerAuthCode;
        public bool CreateAccount = true;
    }

    public class AuthResult
    {
        public string PlayerId;
        public string SessionId;
        public bool IsNewlyCreated;
    }

    public class AuthError
    {
        public string ErrorCode;
        public string ErrorMessage;
    }

    //  email + trang thai link Google de hien o tab Tai khoan.
    public class LinkedAccountInfo
    {
        public string Email;
        public bool GoogleLinked;
    }

    /// <summary>Trạng thái xác thực email của người chơi (map từ PlayFab EmailVerificationStatus).</summary>
    public enum EmailVerifyStatus
    {
        NoContactEmail, // chưa có contact email (thường là khách/guest)
        Unverified,     // có email nhưng chưa gửi/chưa xác thực
        Pending,        // đã gửi mail xác thực, đang chờ bấm link
        Confirmed       // đã xác thực xong
    }
}
