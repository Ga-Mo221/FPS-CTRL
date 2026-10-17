// [Aemy 2026-10-11] FILE MOI. Tim WeaponData tu ten o Trang bi (vd "Dao" -> Knife). Dung chung cho khung model + the chi so.
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public static class WeaponLookup
    {
        // Ten CU da tung luu len PlayFab -> ID that. [2026-10-11] o Trang bi da doi "Dao" -> "Knife" cho thong nhat
        // voi Vesper/Umbra... (nhan = ID); giu dong nay de loadout cu da luu "Dao" van doc dung.
        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dao", "Knife" },
        };

        /// <summary>Cac cap (ten cu -> ID) de map du lieu da luu kieu cu.</summary>
        public static IEnumerable<KeyValuePair<string, string>> LegacyNames { get { return Alias; } }

        /// <summary>ID -> ten sung (WeaponName) -> bang alias. Null neu khong co.</summary>
        public static WeaponData Resolve(string loadoutName)
        {
            var gm = GameManager.Instance;
            if (gm == null || string.IsNullOrEmpty(loadoutName)) return null;
            var d = gm.GetWeaponData(loadoutName);
            if (d != null) return d;
            var list = gm.GetWeaponDataList();
            if (list != null)
                foreach (var w in list)
                {
                    var wd = w.Data();
                    if (wd != null && string.Equals(wd.WeaponName, loadoutName, StringComparison.OrdinalIgnoreCase)) return wd;
                }
            string id;
            return Alias.TryGetValue(loadoutName, out id) ? gm.GetWeaponData(id) : null;
        }

        /// <summary>[Aemy 2026-10-11] Ten luu o Trang bi -> ID vu khi trong tran (vd "Dao" -> "Knife").
        /// Khong tim thay (hoac chua co GameManager) -> giu nguyen.</summary>
        public static string ToGameId(string loadoutName)
        {
            if (string.IsNullOrEmpty(loadoutName)) return loadoutName;
            var d = Resolve(loadoutName);
            return (d != null && !string.IsNullOrEmpty(d.ID)) ? d.ID : loadoutName;
        }
    }
}
