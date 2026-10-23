// [Aemy 2026-10-11] FILE MOI. Dieu huong dung chung trong MenuShell (tranh lap code o nhieu man).
using UnityEngine;

namespace Game.UI
{
    public static class MenuNavigation
    {
        public const int ShopWeaponTab = 1; // ShopCategory.Weapon (sung + skin sung)

        /// <summary>
        /// Mo CUA HANG, tab Sung — nhu nguoi choi bam tab tren TopBar (highlight TopBar dong bo).
        /// Dung cho: o sung khoa trong Trang bi, nut "DEN CUA HANG" cua skin chua so huu.
        /// </summary>
        public static void OpenShopWeapons(Component context = null)
        {
            var top = Object.FindFirstObjectByType<TopBarController>();
            if (top != null) top.Open(TopBarController.MenuTab.Shop);
            else
            {
                var shell = context != null ? context.GetComponentInParent<ShellController>(true) : Object.FindFirstObjectByType<ShellController>();
                if (shell != null) shell.Show(TopBarController.MenuTab.Shop);
            }
            var shop = Object.FindFirstObjectByType<Game.Shop.ShopController>(FindObjectsInactive.Include);
            if (shop != null) shop.SelectTab(ShopWeaponTab);
        }
    }
}
