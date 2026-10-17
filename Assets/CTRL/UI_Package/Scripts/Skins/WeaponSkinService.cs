// [Aemy 2026-10-10] FILE MOI. Skin dang trang bi cho tung sung + doi ID sung goc -> ID skin khi vao tran.
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Game.Skins
{
    /// <summary>
    /// Luu "sung goc -> skin dang trang bi" (vd Umbra -> M4_DR).
    /// - Cache: PlayerPrefs "EquippedSkins" (doc ngay ca khi offline).
    /// - Dong bo: PlayFab User Data "EquippedSkins" (JSON) -> doi may van giu skin.
    /// - Vao tran: MenuShellBinder goi Resolve(id) khi dung BaloData -> sung spawn qua mang theo ID skin
    ///   => moi nguoi trong tran deu thay dung skin (WeaponManager spawn bang ID).
    /// - So huu kiem tra o UI (chi trang bi duoc skin da mua); mua qua CloudScript PurchaseShopItem nhu sung.
    /// </summary>
    public static class WeaponSkinService
    {
        private const string Key = "EquippedSkins";                         // key PlayFab User Data
        private static string PrefKey { get { return Game.UI.SessionCache.UserKey(Key); } } // [Aemy 2026-10-17] cache may theo tai khoan
        private static Dictionary<string, string> _equipped;
        public static event Action Changed;

        private static Dictionary<string, string> Map
        {
            get
            {
                if (_equipped == null) _equipped = Parse(PlayerPrefs.GetString(PrefKey, ""));
                return _equipped;
            }
        }

        /// <summary>Skin dang trang bi cho sung goc ("" = ban goc).</summary>
        public static string GetEquipped(string baseWeaponId)
        {
            string v; return !string.IsNullOrEmpty(baseWeaponId) && Map.TryGetValue(baseWeaponId, out v) ? v : "";
        }

        /// <summary>ID dung de spawn: skin dang trang bi neu co, nguoc lai ID goc.</summary>
        public static string Resolve(string baseWeaponId)
        {
            var s = GetEquipped(baseWeaponId);
            return string.IsNullOrEmpty(s) ? baseWeaponId : s;
        }

        /// <summary>Trang bi skin (skinWeaponId rong = ve ban goc). Luu cache + PlayFab.</summary>
        public static void Equip(string baseWeaponId, string skinWeaponId)
        {
            if (string.IsNullOrEmpty(baseWeaponId)) return;
            if (string.IsNullOrEmpty(skinWeaponId)) Map.Remove(baseWeaponId); else Map[baseWeaponId] = skinWeaponId;
            string json = Serialize(Map);
            PlayerPrefs.SetString(PrefKey, json); PlayerPrefs.Save();
            if (PlayFabClientAPI.IsClientLoggedIn())
                PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest { Data = new Dictionary<string, string> { { Key, json } } },
                    r => { }, e => Debug.LogWarning("[WeaponSkinService] Luu skin loi: " + e.ErrorMessage));
            if (Changed != null) Changed();
        }

        /// <summary>Tai tu PlayFab (goi khi vao menu). Ghi de cache neu server co du lieu.</summary>
        public static void LoadFromServer(Action onDone = null)
        {
            if (!PlayFabClientAPI.IsClientLoggedIn()) { if (onDone != null) onDone(); return; }
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { Key } }, r =>
            {
                UserDataRecord rec;
                if (r.Data != null && r.Data.TryGetValue(Key, out rec) && rec != null && !string.IsNullOrEmpty(rec.Value))
                {
                    _equipped = Parse(rec.Value);
                    PlayerPrefs.SetString(PrefKey, rec.Value); PlayerPrefs.Save();
                    if (Changed != null) Changed();
                }
                else if (Map.Count > 0)
                {
                    // Tai khoan nay chua trang bi skin nao -> xoa cache cua tai khoan truoc tren cung may (tranh spawn skin chua so huu).
                    _equipped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    PlayerPrefs.DeleteKey(PrefKey); PlayerPrefs.Save();
                    if (Changed != null) Changed();
                }
                if (onDone != null) onDone();
            }, e => { Debug.LogWarning("[WeaponSkinService] Tai skin loi: " + e.ErrorMessage); if (onDone != null) onDone(); });
        }

        /// <summary>[Aemy 2026-10-17] Doi tai khoan: bo skin dang nho trong bo nho (doc lai theo tai khoan moi).</summary>
        public static void ResetCache()
        {
            _equipped = null;
            if (Changed != null) Changed();
        }

        // JSON phang {"Umbra":"M4_DR",...} — tu viet de khong phu thuoc thu vien.
        private static Dictionary<string, string> Parse(string json)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json)) return d;
            try
            {
                var obj = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, string>>(json);
                if (obj != null) foreach (var kv in obj) if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value)) d[kv.Key] = kv.Value;
            }
            catch (Exception ex) { Debug.LogWarning("[WeaponSkinService] JSON skin hong: " + ex.Message); }
            return d;
        }

        private static string Serialize(Dictionary<string, string> d)
        {
            return PlayFab.Json.PlayFabSimpleJson.SerializeObject(d);
        }
    }
}
