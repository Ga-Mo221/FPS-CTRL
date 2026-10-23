using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Shop
{
    /// <summary>
    /// DU LIEU 1 mat hang duoi dang STRUCT thuan (khong phai asset). Day la model
    /// RUNTIME + model JSON dung de (de)serialize khi lay tu PlayFab Title Data / file doc.
    ///
    /// MUC DICH: shop "dong" - thong tin item lay tu server (Title Data) hoac file JSON
    /// roi game TU sinh card gan vao catalog bang code. Vi la struct nen copy/sua/truyen
    /// nhe nhang, khong dinh liu asset.
    ///
    /// ANH (art/model) KHONG nam trong struct nay vi anh la asset/nhi phan. Thay vao do
    /// struct chi giu 'artUrl'/'modelUrl' (key tren PlayFab Content hoac URL day du) —
    /// ShopSpriteLoader se tai runtime; neu trong thi ShopArtRegistry lo anh local/default.
    /// </summary>
    [Serializable]
    public struct ShopItemInfo
    {
        [Tooltip("Ma san pham duy nhat (dung cho backend/economy).")]
        public string itemId;
        public string displayName;

        public ShopCategory category;
        public ShopRarity rarity;

        public int price;
        public ShopCurrency currency;

        public bool onSale;
        public int salePrice;
        [Tooltip("Thoi diem HET giam, ISO UTC 'yyyy-MM-ddTHH:mm:ssZ'. Trong = khong gioi han.")]
        public string saleEndUtc;

        public bool comingSoon;
        [Tooltip("Vd: 'BO SUU TAP . APEX PREDATOR' (chi dung cho Featured).")]
        public string collectionName;

        [Tooltip("Key PlayFab Content hoac URL anh NEN card. Trong -> dung registry/default.")]
        public string artUrl;
        [Tooltip("Key PlayFab Content hoac URL anh MODEL/skin. Trong -> dung registry/default.")]
        public string modelUrl;

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

        /// <summary>Co gach ngang gia goc khong.</summary>
        public bool ShowStrikethrough
        {
            get { TimeSpan r; return IsOnSale(out r); }
        }

        /// <summary>Khoa dinh danh dung cho _owned / inventory (itemId, fallback displayName).</summary>
        public string Key
        {
            get { return string.IsNullOrEmpty(itemId) ? displayName : itemId; }
        }
    }

    /// <summary>Boc 1 list item de JsonUtility (de)serialize duoc (JSON tren Title Data / file doc).</summary>
    [Serializable]
    public class ShopCatalogDoc
    {
        public int version = 1;
        public List<ShopItemInfo> items = new List<ShopItemInfo>();
    }

    /// <summary>Doc/ghi JSON catalog. Enum luu duoi dang so (JsonUtility) - xem chu thich duoi.</summary>
    /// <remarks>
    /// category: 0=Featured 1=Weapon 2=Character 3=BattlePass
    /// rarity:   0=Common 1=Rare 2=Epic 3=Legendary
    /// currency: 0=Gold 1=ArenaCoin
    /// </remarks>
    public static class ShopCatalogJson
    {
        public static List<ShopItemInfo> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<ShopItemInfo>();
            try
            {
                var doc = JsonUtility.FromJson<ShopCatalogDoc>(json);
                if (doc != null && doc.items != null) return doc.items;
            }
            catch (Exception e) { Debug.LogWarning("[ShopCatalogJson] Parse loi: " + e.Message); }
            return new List<ShopItemInfo>();
        }

        public static string Build(List<ShopItemInfo> items)
        {
            var doc = new ShopCatalogDoc();
            if (items != null) doc.items = items;
            return JsonUtility.ToJson(doc, true);
        }
    }
}
