using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Shop
{
    /// <summary>
    /// "Noi bat" (Featured) = 1 item that su, o to dau shop. Runtime: ShopController goi
    /// SetInfo(info, art, owned) voi struct lay tu catalog dong. 'data' (ShopItemData) chi de
    /// XEM TRUOC trong Editor. Anh _art hien anh MODEL cua item; co the tai dong qua SetArt().
    /// Nut MUA ban su kien BuyClicked (struct info) cho ShopController.
    /// </summary>
    [ExecuteAlways]
    public class ShopFeaturedCard : MonoBehaviour
    {
        [Header("Preview trong Editor (tuy chon)")]
        [Tooltip("Gan 1 asset ShopItemData de xem thu trong Edit-mode. Runtime khong dung.")]
        public ShopItemData data;

        [Header("Refs")]
        [SerializeField] private Image _art;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _collectionLabel;
        [SerializeField] private TMP_Text _buyLabel;
        [SerializeField] private TMP_Text _originalPriceLabel;
        [SerializeField] private GameObject _saleTagBg;
        [SerializeField] private TMP_Text _saleTagLabel;
        [SerializeField] private GameObject _comingSoon;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Image _buyBg;

        private static readonly Color Teal = new Color(0.31f, 0.84f, 0.79f, 1f);
        private static readonly Color Dim = new Color(0.22f, 0.25f, 0.30f, 1f);

        private ShopItemInfo _info;
        private bool _hasInfo;

        public bool Owned { get; private set; }
        public bool HasInfo { get { return _hasInfo; } }
        public string Key { get { bool ok; var f = CurrentInfo(out ok); return ok ? f.Key : string.Empty; } }

        /// <summary>Nguoi choi bam MUA -> gui struct info cho ShopController.</summary>
        public event Action<ShopItemInfo> BuyClicked;

        private void Awake() { if (_buyButton != null) _buyButton.onClick.AddListener(OnBuy); }
        private void OnDestroy() { if (_buyButton != null) _buyButton.onClick.RemoveListener(OnBuy); }
        private void OnBuy() { bool ok; var f = CurrentInfo(out ok); if (ok && BuyClicked != null) BuyClicked(f); }

        private void OnEnable() { if (!Application.isPlaying) Refresh(false); }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            var self = this;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (self != null && !Application.isPlaying && self.isActiveAndEnabled) self.Refresh(false);
            };
#endif
        }

        /// <summary>Runtime: gan struct info + anh model (tuc thoi) cho featured card.</summary>
        public void SetInfo(ShopItemInfo info, Sprite art, bool owned)
        {
            _info = info; _hasInfo = true;
            if (art != null && _art != null) { _art.sprite = art; _art.preserveAspect = true; } // giu ti le anh
            Refresh(owned);
        }

        /// <summary>Thay anh (goi khi tai dong xong). Null -> giu nguyen.</summary>
        public void SetArt(Sprite s) { if (s != null && _art != null) { _art.sprite = s; _art.preserveAspect = true; } }

        public void Refresh(bool owned)
        {
            Owned = owned;
            bool ok;
            var f = CurrentInfo(out ok);
            if (!ok) return;

            // Edit-mode preview: neu chua co struct info, lay anh model tu asset 'data'.
            if (!_hasInfo && data != null && _art != null && data.modelArt != null) _art.sprite = data.modelArt;

            if (_nameLabel != null) _nameLabel.text = f.displayName;
            if (_collectionLabel != null) _collectionLabel.text = f.collectionName;

            TimeSpan rem;
            bool sale = f.IsOnSale(out rem);
            bool cs = f.comingSoon;

            if (_buyLabel != null)
                _buyLabel.text = cs ? "SẮP RA MẮT" : (owned ? "ĐÃ SỞ HỮU" : ("MUA · " + f.DisplayPrice.ToString("#,0")));
            if (_originalPriceLabel != null)
            {
                _originalPriceLabel.gameObject.SetActive(sale && !cs && !owned);
                if (sale) _originalPriceLabel.text = f.price.ToString("#,0");
            }
            if (_saleTagBg != null) _saleTagBg.SetActive(sale && !cs && !owned);
            if (_saleTagLabel != null)
            {
                _saleTagLabel.gameObject.SetActive(sale && !cs && !owned);
                if (sale) _saleTagLabel.text = "CÓ HẠN · " + RemainingText(rem);
            }
            if (_comingSoon != null) _comingSoon.SetActive(cs);
            if (_buyButton != null) _buyButton.interactable = !cs && !owned;
            if (_buyBg != null) _buyBg.color = (cs || owned) ? Dim : Teal;
        }

        private ShopItemInfo CurrentInfo(out bool ok)
        {
            if (_hasInfo) { ok = true; return _info; }
            if (data != null) { ok = true; return data.ToInfo(); }
            ok = false; return default(ShopItemInfo);
        }

        private static string RemainingText(TimeSpan rem)
        {
            if (rem.TotalDays >= 1) return "CÒN " + Mathf.CeilToInt((float)rem.TotalDays) + " NGÀY";
            if (rem.TotalHours >= 1) return "CÒN " + Mathf.CeilToInt((float)rem.TotalHours) + " GIỜ";
            return "CÒN " + Mathf.Max(1, Mathf.CeilToInt((float)rem.TotalMinutes)) + " PHÚT";
        }
    }
}
