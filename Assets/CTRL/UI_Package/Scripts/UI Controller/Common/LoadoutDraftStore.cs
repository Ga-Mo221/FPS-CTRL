// [Aemy 2026-10-17] FILE MOI. Ban nhap Loadout luu NGAY tren may (PlayerPrefs theo tung tai khoan).
using System;
using UnityEngine;
using Backend.Models;
using Backend.Bootstrap;

namespace Game.UI
{
    /// <summary>
    /// Moi lan nguoi choi doi sung / utility / tab ALPHA-BRAVO-CHARLIE, LoadoutBinder ghi ca 3 slot vao day.
    /// - Chua bam LUU ma truong nhom tim thay tran -> MenuShellBinder van dung ban nhap moi nhat (event Changed).
    /// - Key theo PlayFabId -> khong lan du lieu giua cac tai khoan tren cung may.
    /// - Luu len PlayFab thanh cong -> Clear() (ban nhap chi ton tai khi con thay doi CHUA dong bo).
    /// </summary>
    public static class LoadoutDraftStore
    {
        [Serializable] private class Wrap { public LoadoutConfig[] slots; }

        public static event Action Changed;

        private static string Key
        {
            get
            {
                var auth = BackendBootstrap.AuthService;
                string id = auth != null ? auth.CurrentUserId : null;
                return "LoadoutDraft_" + (string.IsNullOrEmpty(id) ? "local" : id);
            }
        }

        public static bool TryGet(out LoadoutConfig[] slots)
        {
            slots = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var w = JsonUtility.FromJson<Wrap>(json);
                if (w == null || w.slots == null || w.slots.Length == 0) return false;
                slots = w.slots;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LoadoutDraftStore] Ban nhap hong, bo qua: " + ex.Message);
                return false;
            }
        }

        public static void Put(LoadoutConfig[] slots)
        {
            if (slots == null) return;
            var copy = new LoadoutConfig[slots.Length];
            for (int i = 0; i < slots.Length; i++) copy[i] = slots[i] != null ? slots[i] : new LoadoutConfig();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Wrap { slots = copy }));
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
