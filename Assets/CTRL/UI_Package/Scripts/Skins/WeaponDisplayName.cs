
using TMPro;
using UnityEngine;

namespace Game.Skins
{
    /// <summary>
    /// Skin hien la: "VESPER" + ten skin to mau rieng (vd "VESPER <color>NOBLE GOLD</color>") tren 1 DONG,
    /// tu thu nho chu neu dai -> khong xuong dong, khong de len so dan ben duoi.
    /// Sung thuong: giu nguyen WeaponData.WeaponName.
    /// </summary>
    public static class WeaponDisplayName
    {
        /// <summary>Chuoi rich-text ten vu khi (sung skin -> ten sung goc + ten skin to mau).</summary>
        public static string Format(WeaponData data)
        {
            if (data == null) return "";
            var catalog = WeaponSkinCatalog.Load();
            var skin = catalog != null ? catalog.FindBySkinId(data.ID) : null;
            if (skin == null) return data.WeaponName;

            string baseName = skin.baseWeaponId;
            var gm = GameManager.Instance;
            var baseData = gm != null ? gm.GetWeaponData(skin.baseWeaponId) : null;
            if (baseData != null && !string.IsNullOrEmpty(baseData.WeaponName)) baseName = baseData.WeaponName;

            string skinName = string.IsNullOrEmpty(skin.displayName) ? data.WeaponName : skin.displayName;
            return baseName.ToUpperInvariant() + " <color=#" + ColorUtility.ToHtmlStringRGB(skin.tagColor) + ">" + skinName.ToUpperInvariant() + "</color>";
        }

        /// <summary>Gan ten vao label + ep 1 dong, tu co chu (giu co chu goc lam co toi da).</summary>
        public static void Apply(TMP_Text label, WeaponData data)
        {
            if (label == null) return;
            MakeSingleLine(label);
            label.text = Format(data);
        }

        public static void MakeSingleLine(TMP_Text label)
        {
            if (label == null) return;
            if (!label.enableAutoSizing)
            {
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
                label.enableAutoSizing = true;
            }
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
