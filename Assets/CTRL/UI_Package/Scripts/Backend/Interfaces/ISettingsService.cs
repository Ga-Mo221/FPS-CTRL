using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>Luu/doc TOAN BO Settings nguoi choi qua PlayFab User Data (dong bo nhieu may).
    /// Settings duoc serialize thanh 1 chuoi JSON; manager phia UI/game tu map SettingData <-> JSON.</summary>
    public interface ISettingsService
    {
        /// <summary>Gui JSON settings LEN PlayFab (key "Settings").</summary>
        void SaveSettings(string settingsJson, Action onSuccess, Action<AuthError> onFailure);
        /// <summary>Lay JSON settings VE. Tra null neu chua tung luu.</summary>
        void LoadSettings(Action<string> onResult, Action<AuthError> onFailure);
    }
}
