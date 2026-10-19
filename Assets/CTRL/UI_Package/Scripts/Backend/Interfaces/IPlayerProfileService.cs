using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>Hồ sơ hiển thị: displayName, avatar, level. GetProfile(id) dùng cho roster.</summary>
    public interface IPlayerProfileService
    {
        void GetMyProfile(Action<PlayerProfileInfo> onResult, Action<AuthError> onFailure);
        void GetProfile(string playFabId, Action<PlayerProfileInfo> onResult, Action<AuthError> onFailure);

        void SetDisplayName(string displayName, Action onSuccess, Action<AuthError> onFailure);
        void SetAvatarUrl(string imageUrl, Action onSuccess, Action<AuthError> onFailure);
        void SetLevel(int level, Action onSuccess, Action<AuthError> onFailure);

        // Xoá vĩnh viễn tài khoản người chơi hiện tại (gọi CloudScript 'DeleteMyAccount' -> server.DeletePlayer).
        void DeleteMyAccount(Action onSuccess, Action<AuthError> onFailure);

        // set mac dinh khi tao player moi.
        void InitializeNewPlayer(Action onSuccess, Action<AuthError> onFailure);
    }
}
