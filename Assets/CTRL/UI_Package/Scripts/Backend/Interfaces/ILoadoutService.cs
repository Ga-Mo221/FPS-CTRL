using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>Lưu/đọc Loadout qua PlayFab User Data (đồng bộ nhiều máy).</summary>
    public interface ILoadoutService
    {
        void SaveLoadout(LoadoutConfig loadout, Action onSuccess, Action<AuthError> onFailure);
        void LoadLoadout(Action<LoadoutConfig> onResult, Action<AuthError> onFailure);

        // --- Loadout nhieu slot (Alpha/Bravo/Charlie) ---
        /// <summary>Luu loadout cho 1 slot cu the (0,1,2...).</summary>
        void SaveLoadoutSlot(int slot, LoadoutConfig loadout, Action onSuccess, Action<AuthError> onFailure);
        /// <summary>Tai ve TAT CA slot 1 lan. Phan tu null = slot do chua co du lieu.</summary>
        void LoadAllLoadouts(Action<LoadoutConfig[]> onResult, Action<AuthError> onFailure);
    }
}
