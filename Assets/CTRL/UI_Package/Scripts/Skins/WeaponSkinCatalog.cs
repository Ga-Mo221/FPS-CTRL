// [Aemy 2026-10-10] FILE MOI. Danh sach skin sung (them skin moi = them 1 dong trong asset, khong sua code).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Skins
{
    [CreateAssetMenu(menuName = "Eclipse Strike/Weapon Skin Catalog", fileName = "WeaponSkinCatalog")]
    public class WeaponSkinCatalog : ScriptableObject
    {
        [Serializable]
        public class Skin
        {
            [Tooltip("ID sung goc (= ten sung trong Trang bi), vd Umbra, Vesper.")]
            public string baseWeaponId;
            [Tooltip("ID WeaponData cua skin (GameManager._weapon_Prefabs), vd Umbra_DR (= ID sung goc + _DR/_G).")]
            public string skinWeaponId;
            [Tooltip("Ten hien thi, vd DARK RED.")]
            public string displayName;
            [Tooltip("Mau chu ten skin (Trang bi + HUD trong tran).")]
            public Color tagColor = new Color(0.91f, 0.71f, 0.29f, 1f);
            // [Aemy 2026-10-11] Bo fallbackPrice: gia/mua skin chi nam o CUA HANG (ShopCatalog).
            /// <summary>itemId trong ShopCatalog / OwnedShopItems = skinWeaponId viet thuong.</summary>
            public string ItemId { get { return string.IsNullOrEmpty(skinWeaponId) ? "" : skinWeaponId.Trim().ToLowerInvariant(); } }
        }

        [SerializeField] private List<Skin> _skins = new List<Skin>();
        public IReadOnlyList<Skin> Skins { get { return _skins; } }

        /// <summary>Cac skin cua 1 sung goc (khong gom ban goc).</summary>
        public List<Skin> SkinsFor(string baseWeaponId)
        {
            var r = new List<Skin>();
            if (string.IsNullOrEmpty(baseWeaponId)) return r;
            foreach (var s in _skins) if (s != null && string.Equals(s.baseWeaponId, baseWeaponId, StringComparison.OrdinalIgnoreCase)) r.Add(s);
            return r;
        }

        public Skin FindBySkinId(string skinWeaponId)
        {
            foreach (var s in _skins) if (s != null && string.Equals(s.skinWeaponId, skinWeaponId, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        private static WeaponSkinCatalog _cached;
        /// <summary>Asset dat trong Resources/WeaponSkinCatalog.</summary>
        public static WeaponSkinCatalog Load()
        {
            if (_cached == null) _cached = Resources.Load<WeaponSkinCatalog>("WeaponSkinCatalog");
            return _cached;
        }
    }
}
