using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Shop
{
    /// <summary>
    /// 1 the (card) san pham trong luoi. ShopController TU sinh card nay tu du lieu struct
    /// (ShopItemInfo) lay ve tu PlayFab Title Data / doc, roi goi Bind(info, bg, model, owned).
    ///
    /// - Anh (bg/model) do ShopController truyen vao (local/registry/default), sau do neu item co
    ///   artUrl/modelUrl thi tai dong va thay the qua SetBg()/SetModel().
    /// - 'data' (ShopItemData) chi de XEM TRUOC trong Editor (Edit-mode preview), khong bat buoc.
    /// </summary>
    [ExecuteAlways]
    public class ShopItemCard : MonoBehaviour
    {
        [Header("Preview trong Editor (tuy chon)")]
        [Tooltip("Gan 1 asset ShopItemData de xem thu card trong Edit-mode. Runtime khong dung.")]
        public ShopItemData data;

        [Header("Refs")]
        [SerializeField] private Image _bg;
        [SerializeField] private TMP_Text _rarityLabel;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _priceLabel;
        [SerializeField] private TMP_Text _originalPriceLabel; // gach ngang khi giam
        [SerializeField] private GameObject _saleBadge;        // badge goc card khi giam
        [SerializeField] private TMP_Text _saleBadgeLabel;     // "CON X NGAY"
        [SerializeField] private GameObject _comingSoon;
        [SerializeField] private Image _itemModel;
        [SerializeField] private GameObject _purchasedOverlay;
        [Tooltip("Cac dong thong tin - an het khi Sap ra mat.")]
        [SerializeField] private GameObject[] _infoObjects;
        [SerializeField] private Button _button;

        [Header("Nen theo do hiem (fallback khi item khong co art)")]
        [SerializeField] private Sprite _commonBg;
        [SerializeField] private Sprite _rareBg;
        [SerializeField] private Sprite _epicBg;
        [SerializeField] private Sprite _legendaryBg;

        public bool Owned { get; private set; }
        public ShopItemInfo Info { get; private set; }
        public bool HasInfo { get; private set; }
        public string Key { get { return HasInfo ? Info.Key : string.Empty; } }

        /// <summary>Nguoi choi bam vao card -> gui struct info cho ShopController xu ly mua.</summary>
        public event Action<ShopItemInfo> Clicked;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && data != null) Bind(data.ToInfo(), data.art, data.modelArt, false);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || data == null) return;
            var self = this;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (self != null && !Application.isPlaying && self.data != null && self.isActiveAndEnabled)
                    self.Bind(self.data.ToInfo(), self.data.art, self.data.modelArt, false);
            };
#endif
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (HasInfo && Clicked != null) Clicked(Info);
        }

        /// <summary>Do du lieu struct + anh (bg/model tuc thoi) vao card.</summary>
        public void Bind(ShopItemInfo info, Sprite bgArt, Sprite model, bool owned)
        {
            Info = info; HasInfo = true; Owned = owned;
            gameObject.SetActive(true);

            bool coming = info.comingSoon;

            if (_bg != null)
            {
                var spr = bgArt != null ? bgArt : RaritySprite(info.rarity);
                if (spr != null) _bg.sprite = spr;
                SetAlpha(_bg, 1f);
            }

            // Sap ra mat: an het info, chi con Bg + overlay
            SetInfoActive(!coming);

            if (!coming)
            {
                if (_rarityLabel != null) { _rarityLabel.text = info.rarity.ToString().ToUpperInvariant(); SetAlpha(_rarityLabel, 1f); }
                if (_nameLabel != null) { _nameLabel.text = info.displayName; SetAlpha(_nameLabel, 1f); }
                SetModel(model);
                RefreshSale();
            }

            if (_comingSoon != null) _comingSoon.SetActive(coming);
            if (_purchasedOverlay != null) _purchasedOverlay.SetActive(owned && !coming);
            if (_button != null) _button.interactable = !coming && !owned;
        }

        /// <summary>Thay anh NEN (goi khi tai dong xong). Null -> giu nguyen.</summary>
        public void SetBg(Sprite s)
        {
            if (s == null || _bg == null) return;
            _bg.sprite = s; SetAlpha(_bg, 1f);
        }

        /// <summary>Thay anh MODEL. Null -> an o model.</summary>
        public void SetModel(Sprite s)
        {
            if (_itemModel == null) return;
            if (s != null)
            {
                _itemModel.sprite = s;
                _itemModel.preserveAspect = true; // giu ti le anh sung (truoc bi keo gian theo khung 557x152)
                _itemModel.enabled = true;
                _itemModel.gameObject.SetActive(true);
                SetAlpha(_itemModel, 1f);
            }
            else
            {
                _itemModel.gameObject.SetActive(false);
            }
        }

        /// <summary>Cap nhat gia + trang thai giam gia (goi dinh ky de dem nguoc).</summary>
        public void RefreshSale()
        {
            if (!HasInfo || Info.comingSoon) return;
            var info = Info;
            TimeSpan rem;
            bool sale = info.IsOnSale(out rem);
            // Gia goc gach ngang ghep NGAY SAU gia giam trong cung 1 label (truoc la label rieng vi tri co dinh -> de chu len nhau "2,5003,500").
            if (_priceLabel != null)
            {
                string txt = FormatPrice(info.DisplayPrice);
                if (sale) txt += "  <size=70%><color=#7E8A90><s>" + FormatPrice(info.price) + "</s></color></size>";
                _priceLabel.text = txt; SetAlpha(_priceLabel, 1f);
            }
            if (_originalPriceLabel != null) _originalPriceLabel.gameObject.SetActive(false);
            if (_saleBadge != null) _saleBadge.SetActive(sale);
            if (_saleBadgeLabel != null && sale) _saleBadgeLabel.text = SaleText(info, rem);
        }

        private Sprite RaritySprite(ShopRarity r)
        {
            switch (r)
            {
                case ShopRarity.Legendary: return _legendaryBg;
                case ShopRarity.Epic: return _epicBg;
                case ShopRarity.Rare: return _rareBg;
                default: return _commonBg != null ? _commonBg : _rareBg;
            }
        }

        private static void SetAlpha(UnityEngine.UI.Graphic g, float a)
        {
            if (g == null) return;
            var c = g.color; c.a = a; g.color = c;
        }

        private void SetInfoActive(bool on)
        {
            if (_infoObjects == null) return;
            foreach (var g in _infoObjects) if (g != null) g.SetActive(on);
        }

        private static string FormatPrice(int p) { return p.ToString("#,0"); }

        private static string SaleText(ShopItemInfo d, TimeSpan rem)
        {
            if (string.IsNullOrEmpty(d.saleEndUtc)) return "GIẢM GIÁ";
            if (rem.TotalDays >= 1) return "CÒN " + Mathf.CeilToInt((float)rem.TotalDays) + " NGÀY";
            if (rem.TotalHours >= 1) return "CÒN " + Mathf.CeilToInt((float)rem.TotalHours) + " GIỜ";
            return "CÒN " + Mathf.Max(1, Mathf.CeilToInt((float)rem.TotalMinutes)) + " PHÚT";
        }
    }
}
