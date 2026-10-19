// [Aemy 2026-10-19] FILE MOI. Gui lai mail xac minh cho tai khoan dang dang nhap (ISP: tach khoi IEmailVerificationStatusService).
using System;
using Backend.Models;

namespace Backend.Interfaces
{
    public interface IEmailVerificationResendService
    {
        /// <summary>
        /// Dat lai contact email hien co -> PlayFab ban event player_updated_contact_email -> Rule gui mail xac minh.
        /// onSent nhan dia chi email da gui (de hien cho nguoi choi). Loi / khong co email -> onFailure.
        /// </summary>
        void ResendVerificationEmail(Action<string> onSent, Action<AuthError> onFailure);
    }
}
