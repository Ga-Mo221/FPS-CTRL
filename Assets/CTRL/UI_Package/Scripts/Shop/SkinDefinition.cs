// [UI-INTEGRATION 2026-09-12 - Aemy] FILE MỚI (thêm cho việc ghép UI / hệ skin).
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// Dinh nghia 1 SKIN cho vu khi. La du lieu BEN dung chung 2 giai doan:
    ///   - GIAI DOAN 1 (shop): render preview + nhan dien skin la item shop that (skinId khop ShopItemData.itemId).
    ///   - GIAI DOAN 2 (equip + networked): host/ client goi ApplyTo() de dap bo material len sung khi spawn.
    ///
    /// Cach khop material: 'materials' la danh sach material thay the theo dung THU TU concat
    /// sharedMaterials cua cac SkinnedMeshRenderer tren prefab sung (GetComponentsInChildren, bo qua
    /// ParticleSystemRenderer). Vd Knife co 1 renderer x 1 material -> list 1 phan tu. M4 co 3 renderer
    /// (6+3+3 material) -> list 12 phan tu. Phan tu null = giu material goc o vi tri do.
    /// </summary>
    [CreateAssetMenu(fileName = "Skin", menuName = "Eclipse/Weapon Skin", order = 1)]
    public class SkinDefinition : ScriptableObject
    {
        [Tooltip("Khop itemId cua ShopItemData (vd 'knife_crimson').")]
        public string skinId = "";

        [Tooltip("Ten sung goc, khop WeaponName / WeaponData.WeaponName (vd 'M4', '9mm Demon', 'Knife Short').")]
        public string baseWeapon = "";

        public string displayName = "";
        public ShopRarity rarity = ShopRarity.Common;

        [Tooltip("Bo material thay the, theo dung thu tu concat sharedMaterials cua cac SkinnedMeshRenderer tren prefab sung. Null = giu goc.")]
        public List<Material> materials = new List<Material>();

        /// <summary>
        /// Dap bo material skin len 1 instance vu khi (dung cho preview GD1 va apply in-game GD2).
        /// Duyet cac SkinnedMeshRenderer theo dung thu tu GetComponentsInChildren va gan tuan tu.
        /// </summary>
        public void ApplyTo(GameObject weaponRoot)
        {
            if (weaponRoot == null || materials == null || materials.Count == 0) return;
            var rends = weaponRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int idx = 0;
            foreach (var r in rends)
            {
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (idx >= materials.Count) { r.sharedMaterials = arr; return; }
                    if (materials[idx] != null) arr[i] = materials[idx];
                    idx++;
                }
                r.sharedMaterials = arr;
            }
        }

        /// <summary>Tong so material slot ma prefab sung nay can (de kiem tra 'materials' du chua).</summary>
        public static int CountSlots(GameObject weaponRoot)
        {
            if (weaponRoot == null) return 0;
            int n = 0;
            foreach (var r in weaponRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                n += r.sharedMaterials.Length;
            return n;
        }
    }
}
