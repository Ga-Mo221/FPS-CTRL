using System;
using System.Globalization;
using UnityEngine;

namespace Game.Shop
{
    public enum ShopRarity { Common, Rare, Epic, Legendary }
    public enum ShopCategory { Featured, Weapon, Character, BattlePass }
    public enum ShopCurrency { Gold, ArenaCoin }

    /// <summary>
    /// ASSET SOAN THAO (authoring) cho 1 mat hang. Day la NGUON LOCAL de:
    ///   - Soan item trong Editor (Create > Eclipse > Shop Item) + giu anh art/modelArt local.
    ///   - Tool ShopCatalogSync quet cac asset nay de day len PlayFab (Catalog + Title Data JSON).
    ///
    /// RUNTIME shop dung STRUCT 'ShopItemInfo' (lay tu Title Data/doc). Asset nay chuyen thanh
    /// struct qua ToInfo(). Nho vay: van con workflow soan tay + tool sync (khong vo), dong thoi
    /// data chay game la struct nhe, lay tu server -> shop "dong".
    /// </summary>
    [CreateAssetMenu(fileName = "ShopItem", menuName = "Eclipse/Shop Item", order = 0)]
    public class ShopItemData : ScriptableObject
    {
        [Header("Dinh danh")]
        [Tooltip("Ma san pham (duy nhat). Dung cho backend/economy.")]
        public string itemId = "";
        public string displayName = "Ten vat pham";

        [Header("Phan loai")]
        public ShopCategory category = ShopCategory.Weapon;
        public ShopRarity rarity = ShopRarity.Common;

        [Header("Gia")]
        [Tooltip("Gia thuong (khi khong giam).")]
        public int price = 1000;
        public ShopCurrency currency = ShopCurrency.Gold;

        [Header("Giam gia (tuy chon)")]
        public bool onSale = false;
        [Tooltip("Gia sau giam (chi dung khi onSale).")]
        public int salePrice = 0;
        [Tooltip("Thoi diem HET giam gia, ISO UTC 'yyyy-MM-ddTHH:mm:ssZ'. Bo trong = giam khong gioi han thoi gian.")]
        public string saleEndUtc = "";

        [Header("Hien thi (anh local)")]
        [Tooltip("Anh/nen card local. Bo trong -> dung nen theo do hiem trong ShopItemCard.")]
        public Sprite art;
        [Tooltip("Anh skin/nhan vat local hien trong o ItemModel cua card.")]
        public Sprite modelArt;
        [Tooltip("Danh dau 'SAP RA MAT' (khong mua duoc).")]
        public bool comingSoon = false;

        [Header("Anh tai dong (tuy chon) - key PlayFab Content hoac URL")]
        [Tooltip("Trong -> dung anh local/registry/default. Co -> game tai runtime va thay the.")]
        public string artUrl = "";
        [Tooltip("Trong -> dung anh model local/registry/default. Co -> game tai runtime va thay the.")]
        public string modelUrl = "";

        [Header("Featured (chi dung khi category = Featured)")]
        [Tooltip("Vd: 'BO SUU TAP . APEX PREDATOR'")]
        public string collectionName = "";

        /// <summary>Con dang giam gia? out remaining = thoi gian con lai (Zero neu khong gioi han/het).</summary>
        public bool IsOnSale(out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (!onSale) return false;
            if (string.IsNullOrEmpty(saleEndUtc)) return true; // giam khong gioi han thoi gian
            DateTime end;
            if (DateTime.TryParse(saleEndUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out end))
            {
                remaining = end - DateTime.UtcNow;
                return remaining > TimeSpan.Zero;
            }
            return true; // parse loi -> coi nhu dang giam (an toan cho hien thi)
        }

        /// <summary>Gia hien thi hien tai (giam neu dang sale con hieu luc).</summary>
        public int DisplayPrice
        {
            get { TimeSpan r; return IsOnSale(out r) ? salePrice : price; }
        }

        /// <summary>Co hien gia goc gach ngang khong.</summary>
        public bool ShowStrikethrough
        {
            get { TimeSpan r; return IsOnSale(out r); }
        }

        /// <summary>Dong goi asset nay thanh struct runtime ShopItemInfo (dung de sinh card / xuat JSON).</summary>
        public ShopItemInfo ToInfo()
        {
            return new ShopItemInfo
            {
                itemId = itemId,
                displayName = displayName,
                category = category,
                rarity = rarity,
                price = price,
                currency = currency,
                onSale = onSale,
                salePrice = salePrice,
                saleEndUtc = saleEndUtc,
                comingSoon = comingSoon,
                collectionName = collectionName,
                artUrl = artUrl,
                modelUrl = modelUrl
            };
        }
    }
}
