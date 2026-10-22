// [Aemy 2026-10-17] FILE MOI. Don cache du lieu cua tai khoan truoc khi dang nhap / dang xuat
// (truoc day doi tai khoan tren cung may van thay ten, skin, party, nhan vat... cua nguoi truoc).
using UnityEngine;
using Backend.Bootstrap;

namespace Game.UI
{
    /// <summary>
    /// Mot noi duy nhat biet "nhung gi gan voi tai khoan dang dang nhap":
    ///  - Cache tinh: ProfileCache, WeaponSkinService.
    ///  - GameManager (DontDestroyOnLoad): ten nguoi choi, loadout tam.
    ///  - PlayerPrefs dung chung cu (khong theo tai khoan): xoa; du lieu moi da luu theo PlayFabId
    ///    (LoadoutDraft_, PartyCode_, SelectedCharacter_).
    /// Goi BeforeLogout() truoc khi dang xuat (con session de roi party), ResetForNewSession() ngay khi dang nhap xong.
    /// </summary>
    public static class SessionCache
    {
        private static readonly string[] LegacyGlobalKeys =
        {
            "PartyCode", "SelectedCharacter", "ActiveLoadoutSlot",
            "LoadoutActiveW0", "LoadoutActiveW1", "LoadoutActiveU0", "LoadoutActiveU1",
            "LoadoutSlot0W0", "LoadoutSlot0W1", "LoadoutSlot1W0", "LoadoutSlot1W1", "LoadoutSlot2W0", "LoadoutSlot2W1",
            "EquippedSkins", "PlayerNickName",
        };

        /// <summary>Key PlayerPrefs rieng cho tai khoan dang dang nhap.</summary>
        public static string UserKey(string baseKey)
        {
            var auth = BackendBootstrap.AuthService;
            string id = auth != null ? auth.CurrentUserId : null;
            return baseKey + "_" + (string.IsNullOrEmpty(id) ? "local" : id);
        }

        /// <summary>Dang nhap xong (truoc khi vao menu): bo moi du lieu cua tai khoan truoc.</summary>
        public static void ResetForNewSession()
        {
            ProfileCache.Invalidate();
            Game.Skins.WeaponSkinService.ResetCache();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SetPlayerNickName("");      // FusionMatchHandoff.EnsureNickname se lay lai ten cua tai khoan moi
                gm.SetLoadout(null, null, null, null);
            }
            foreach (var k in LegacyGlobalKeys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
            Debug.Log("[SessionCache] Da don cache phien truoc.");
        }

        /// <summary>Truoc khi dang xuat: roi to doi (con session) roi don cache.</summary>
        public static void BeforeLogout()
        {
            var party = Object.FindFirstObjectByType<PartyController>();
            if (party != null && party.InParty) party.LeaveParty();
            ResetForNewSession();
        }
    }
}
