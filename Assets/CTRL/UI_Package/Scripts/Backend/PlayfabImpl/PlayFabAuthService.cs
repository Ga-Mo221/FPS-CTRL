using System;
using Backend.Interfaces;
using Backend.Models;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Backend.PlayfabImpl
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  QUY ƯỚC CHIỀU DỮ LIỆU:
    //   ↑ GỬI LÊN  = client GHI/GỬI dữ liệu LÊN PlayFab (Register/Login/Update/AddOrUpdate...)
    //   ↓ LẤY VỀ   = client ĐỌC dữ liệu TỪ PlayFab XUỐNG (Get...)
    //  Đăng nhập = ↑ gửi thông tin xác thực LÊN, rồi ↓ nhận session/hồ sơ VỀ.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cài đặt PlayFab cho toàn bộ luồng xác thực + đọc trạng thái verify.
    /// Implement nhiều interface nhỏ (ISP).
    /// </summary>
    public class PlayFabAuthService :
        IAuthService, IExternalAuthService, IAccountRecoveryService, IEmailVerificationStatusService, IUsernameAvailabilityService
    {
        public bool IsLoggedIn { get; private set; }
        public string CurrentUserId { get; private set; }

        private string _sessionTicket;

        // ============================ REGISTER ============================
        // ↑ GỬI LÊN: tạo tài khoản mới (username + email + password) trên PlayFab.
        // ↓ Kết quả trả VỀ: PlayFabId + SessionTicket (phiên đăng nhập).
        public void Register(RegisterRequest registerRequest,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            var pfRequest = new RegisterPlayFabUserRequest
            {
                Username = registerRequest.Username,
                Email = registerRequest.Email,
                Password = registerRequest.Password,
                RequireBothUsernameAndEmail = true
            };

            PlayFabClientAPI.RegisterPlayFabUser(   // ↑ GỬI LÊN: tạo tài khoản
                pfRequest, result =>
                {
                    SetLoggedIn(result.PlayFabId, result.SessionTicket);  // lưu phiên (trả VỀ)

                    // Sau khi tạo xong -> ↑ GỬI LÊN contact email để PlayFab gửi mail xác thực.
                    if (registerRequest.SendVerificationEmail &&
                        !string.IsNullOrEmpty(registerRequest.Email))
                    {
                        SendEmailVerification(registerRequest.Email, null, null);
                    }

                    onSuccess?.Invoke(new AuthResult
                    {
                        PlayerId = result.PlayFabId,
                        SessionId = result.SessionTicket,
                        IsNewlyCreated = true
                    });
                },
                error => onError?.Invoke(MapError(error))
            );
        }

        // ============================ LOGIN ============================
        // ↑ GỬI LÊN thông tin đăng nhập -> ↓ NHẬN VỀ session. Chọn theo email hay username.
        public void Login(LoginRequest loginRequest,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            if (loginRequest.UseEmail) LoginWithEmail(loginRequest, onSuccess, onError);
            else LoginWithUsername(loginRequest, onSuccess, onError);
        }

        // ↑ GỬI LÊN: email + password -> ↓ NHẬN VỀ session.
        private void LoginWithEmail(LoginRequest loginRequest,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            var pfrequest = new LoginWithEmailAddressRequest
            {
                Email = loginRequest.UsernameorEmail,
                Password = loginRequest.Password
            };

            PlayFabClientAPI.LoginWithEmailAddress(   // ↑ gửi LÊN / ↓ nhận VỀ session
                pfrequest,
                result => HandleLoginResult(result.PlayFabId, result.SessionTicket, result.NewlyCreated, onSuccess),
                error => onError?.Invoke(MapError(error)));
        }


        // ↑ GỬI LÊN: username + password -> ↓ NHẬN VỀ session.
        private void LoginWithUsername(LoginRequest loginRequest,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            var pfrequest = new LoginWithPlayFabRequest
            {
                Username = loginRequest.UsernameorEmail,
                Password = loginRequest.Password
            };

            PlayFabClientAPI.LoginWithPlayFab(   // ↑ gửi LÊN / ↓ nhận VỀ session
                pfrequest,
                result => HandleLoginResult(result.PlayFabId, result.SessionTicket, result.NewlyCreated, onSuccess),
                error => onError?.Invoke(MapError(error)));
        }


        // ============================ GUEST ============================
        // ↑ GỬI LÊN: đăng nhập bằng Device/Custom ID (tạo tài khoản khách nếu chưa có).
        public void LoginAsGuest(string deviceId,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(deviceId))
                deviceId = PlayFabSettings.DeviceUniqueIdentifier;

            var pfrequest = new LoginWithCustomIDRequest
            {
                CustomId = deviceId,
                CreateAccount = true
            };

            PlayFabClientAPI.LoginWithCustomID(   // ↑ gửi LÊN / ↓ nhận VỀ session
                pfrequest,
                result => HandleLoginResult(result.PlayFabId, result.SessionTicket, result.NewlyCreated, onSuccess),
                error => onError?.Invoke(MapError(error)));
        }


        // ============================ GOOGLE / GMAIL ============================
        // ↑ GỬI LÊN: ServerAuthCode của Google -> PlayFab đổi lấy danh tính -> ↓ NHẬN VỀ session.
        public void LoginWithGoogle(GoogleLoginRequest request,
            Action<AuthResult> onSuccess,
            Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(request.ServerAuthCode))
            {
                onError?.Invoke(new AuthError
                {
                    ErrorCode = "MissingServerAuthCode",
                    ErrorMessage = "ServerAuthCode rỗng. Cần lấy từ Google Sign-In SDK trước (xem checklist)."
                });
                return;
            }

            var pfRequest = new LoginWithGoogleAccountRequest
            {
                ServerAuthCode = request.ServerAuthCode,
                CreateAccount = request.CreateAccount
            };

            PlayFabClientAPI.LoginWithGoogleAccount(   // ↑ gửi LÊN / ↓ nhận VỀ session
                pfRequest,
                result => HandleLoginResult(result.PlayFabId, result.SessionTicket, result.NewlyCreated, onSuccess),
                error => onError?.Invoke(MapError(error)));
        }

        

        // GUI LEN: LIEN KET Google vao tai khoan HIEN TAI.
        public void LinkGoogleAccount(string serverAuthCode, Action onSuccess, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(serverAuthCode))
            {
                onError?.Invoke(new AuthError { ErrorCode = "MissingServerAuthCode", ErrorMessage = "ServerAuthCode rong." });
                return;
            }
            PlayFabClientAPI.LinkGoogleAccount(
                new LinkGoogleAccountRequest { ServerAuthCode = serverAuthCode, ForceLink = true },
                _ => onSuccess?.Invoke(),
                error => onError?.Invoke(MapError(error)));
        }

        // [FIX BUG-ACCOUNT 2026-09-14 - Aemy] Lay contact email + trang thai link Google qua GetPlayerProfile.
        // Client GetAccountInfo thuong khong tra PrivateInfo.Email (bi gate) -> dung GetPlayerProfile.
        public void GetLinkedAccountInfo(System.Action<Backend.Models.LinkedAccountInfo> onResult, System.Action<AuthError> onError)
        {
            var req = new GetPlayerProfileRequest
            {
                PlayFabId = CurrentUserId,
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowContactEmailAddresses = true,
                    ShowLinkedAccounts = true
                }
            };
            PlayFabClientAPI.GetPlayerProfile(
                req,
                result =>
                {
                    string email = null;
                    bool googleLinked = false;
                    var prof = result != null ? result.PlayerProfile : null;
                    if (prof != null)
                    {
                        if (prof.ContactEmailAddresses != null && prof.ContactEmailAddresses.Count > 0)
                            email = prof.ContactEmailAddresses[0].EmailAddress;
                        if (prof.LinkedAccounts != null)
                        {
                            foreach (var la in prof.LinkedAccounts)
                            {
                                string plt = "" + la.Platform;
                                if (plt.IndexOf("Google", System.StringComparison.OrdinalIgnoreCase) >= 0) googleLinked = true;
                                if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(la.Email)) email = la.Email;
                            }
                        }
                    }
                    onResult?.Invoke(new Backend.Models.LinkedAccountInfo { Email = email, GoogleLinked = googleLinked });
                },
                error => onError?.Invoke(MapError(error)));
        }

        // ============================ RECOVERY / VERIFY ============================
        // ↑ GỬI LÊN: yêu cầu PlayFab gửi email KHÔI PHỤC MẬT KHẨU tới địa chỉ này.
        public void SendPasswordRecoveryEmail(string email,
            Action onSuccess,
            Action<AuthError> onError)
        {
            var req = new SendAccountRecoveryEmailRequest
            {
                Email = email,
                TitleId = PlayFabSettings.TitleId
            };

            PlayFabClientAPI.SendAccountRecoveryEmail(   // ↑ GỬI LÊN yêu cầu gửi mail
                req,
                result =>
                {
                    Debug.Log($"<color=cyan>[PlayFabAuth]</color> Recovery email sent to {email}");
                    onSuccess?.Invoke();
                },
                error => onError?.Invoke(MapError(error)));
        }

        // ↑ GỬI LÊN: đặt/cập nhật contact email -> PlayFab bắn event -> gửi mail XÁC THỰC.
        public void SendEmailVerification(string email,
            Action onSuccess,
            Action<AuthError> onError)
        {
            var req = new AddOrUpdateContactEmailRequest
            {
                EmailAddress = email
            };

            PlayFabClientAPI.AddOrUpdateContactEmail(   // ↑ GỬI LÊN contact email
                req,
                result =>
                {
                    Debug.Log($"<color=cyan>[PlayFabAuth]</color> Contact email set + verification triggered: {email}");
                    onSuccess?.Invoke();
                },
                error =>
                {
                    Debug.LogWarning($"<color=cyan>[PlayFabAuth]</color> SendEmailVerification failed: {error.ErrorMessage}");
                    onError?.Invoke(MapError(error));
                });
        }

        // ============================ VERIFY STATUS (khóa theo verified) ============================
        // ↓ LẤY VỀ: đọc trạng thái xác thực email (Confirmed/Pending/Unverified) từ profile.
        public void GetEmailVerificationStatus(
            Action<EmailVerifyStatus> onResult,
            Action<AuthError> onError)
        {
            if (!IsLoggedIn || string.IsNullOrEmpty(CurrentUserId))
            {
                onError?.Invoke(new AuthError { ErrorCode = "NotLoggedIn", ErrorMessage = "Chưa đăng nhập." });
                return;
            }

            var req = new GetPlayerProfileRequest
            {
                PlayFabId = CurrentUserId,
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowContactEmailAddresses = true
                }
            };

            PlayFabClientAPI.GetPlayerProfile(   // ↓ LẤY VỀ profile (để đọc trạng thái email)
                req,
                result =>
                {
                    var list = result.PlayerProfile != null ? result.PlayerProfile.ContactEmailAddresses : null;
                    if (list == null || list.Count == 0)
                    {
                        onResult?.Invoke(EmailVerifyStatus.NoContactEmail);
                        return;
                    }
                    onResult?.Invoke(MapVerify(list[0].VerificationStatus));
                },
                error => onError?.Invoke(MapError(error)));
        }

        // (local) map enum PlayFab -> enum trung lập của mình.
        private static EmailVerifyStatus MapVerify(EmailVerificationStatus? s)
        {
            if (s == null) return EmailVerifyStatus.Unverified;
            switch (s.Value)
            {
                case EmailVerificationStatus.Confirmed: return EmailVerifyStatus.Confirmed;
                case EmailVerificationStatus.Pending:   return EmailVerifyStatus.Pending;
                default:                                return EmailVerifyStatus.Unverified;
            }
        }

        // ============================ LOGOUT ============================
        // (local) Xoá phiên đăng nhập trên máy — KHÔNG gọi mạng.
        public void Logout()
        {
            PlayFabClientAPI.ForgetAllCredentials();
            IsLoggedIn = false;
            CurrentUserId = null;
            _sessionTicket = null;
        }

        // ----------------- Helper -----------------
        // Nhận kết quả login (↓ VỀ) -> lưu phiên -> báo cho UI.
        private void HandleLoginResult(string playerId,
            string sessionTicket,
            bool newlyCreated,
            Action<AuthResult> onSuccess)
        {
            SetLoggedIn(playerId, sessionTicket);
            onSuccess?.Invoke(new AuthResult
            {
                PlayerId = playerId,
                SessionId = sessionTicket,
                IsNewlyCreated = newlyCreated
            });
        }

        private void SetLoggedIn(string playerId, string sessionTicket)
        {
            IsLoggedIn = true;
            _sessionTicket = sessionTicket;
            CurrentUserId = playerId;
        }

        private static AuthError MapError(PlayFabError error) => new AuthError
        {
            ErrorCode = error.Error.ToString(),
            ErrorMessage = error.ErrorMessage
        };
    

        // ===================== USERNAME AVAILABILITY =====================
        // GetAccountInfo theo Username: co ket qua = DA ton tai; AccountNotFound = CON trong.
        // Luu y: GetAccountInfo (client) can phien dang nhap; chua dang nhap se bao onError.
        public void CheckUsername(string username,
            Action<bool> onResult,
            Action<AuthError> onError)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                onError?.Invoke(new AuthError { ErrorCode = "EmptyUsername", ErrorMessage = "Tên đăng nhập trống." });
                return;
            }

            var req = new GetAccountInfoRequest { Username = username };
            PlayFabClientAPI.GetAccountInfo(
                req,
                result => onResult?.Invoke(false),
                error =>
                {
                    if (error.Error == PlayFabErrorCode.AccountNotFound)
                        onResult?.Invoke(true);
                    else
                        onError?.Invoke(MapError(error));
                });
        }
}
}
