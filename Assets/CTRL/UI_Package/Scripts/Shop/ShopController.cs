using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.UI; // ModalDialog
using Backend.Bootstrap;
using Backend.Models;

namespace Game.Shop
{
    /// <summary>
    /// Dieu khien Cua Hang DONG. Item KHONG con keo tay tung asset nua ma duoc NAP tu:
    ///   - PlayFab Title Data (key _titleDataKey) khi chay game — sua tren dashboard = cap nhat, KHONG build lai,
    ///   - fallback file JSON local (_fallbackCatalog) khi offline/first-run,
    ///   - hoac _catalog (cac ShopItemData) lam nguon xem-truoc trong Editor / seed khi server+local deu trong.
    /// Game TU sinh card (struct ShopItemInfo) va gan vao luoi. Anh: local/default qua ShopArtRegistry,
    /// tai dong (URL/PlayFab Content) qua ShopSpriteLoader roi thay the.
    ///
    /// LUONG: OnEnable(playing) -> LoadCatalog() -> Rebuild() -> LoadOwned() -> Rebuild().
    /// </summary>
    [ExecuteAlways]
    public class ShopController : MonoBehaviour
    {
        [Header("Nguon DONG (runtime)")]
        [Tooltip("Key tren PlayFab Title Data chua JSON danh sach item.")]
        [SerializeField] private string _titleDataKey = "ShopCatalog";
        [Tooltip("File JSON bundle san lam fallback khi offline/first-run (tuy chon).")]
        [SerializeField] private TextAsset _fallbackCatalog;
        [Tooltip("Kho anh local + anh model MAC DINH (bake san lam placeholder/fallback).")]
        [SerializeField] private ShopArtRegistry _artRegistry;

        [Header("Nguon soan-thao / xem-truoc (Editor)")]
        [Tooltip("Cac ShopItemData de xem truoc trong Editor + seed khi server/local trong. Tool sync quet t:ShopItemData toan project.")]
        [SerializeField] private List<ShopItemData> _catalog = new List<ShopItemData>();
        [Tooltip("Prefab ItemCard de nhan ban.")]
        [SerializeField] private ShopItemCard _cardTemplate;

        [Header("Luoi")]
        [SerializeField] private RectTransform _gridRoot;
        [SerializeField] private Vector2 _gridOrigin = new Vector2(74f, 206f);
        [Tooltip("x = khoang cach 2 cot, y = khoang cach 2 hang.")]
        [SerializeField] private Vector2 _gridStep = new Vector2(573f, 272f);
        [SerializeField] private int _columns = 2;
        [Tooltip("Layout khi o tab Sung/Nhan vat (trai deu full width).")]
        [SerializeField] private Vector2 _gridOriginWide = new Vector2(-670f, 206f);
        [SerializeField] private int _columnsWide = 3;
        [SerializeField] private GameObject _dividerV;
        [SerializeField] private RectTransform _gridHeader;
        [Tooltip("Neu _gridRoot la Content cua ScrollRect: tu keo dai chieu cao theo so hang de cuon.")]
        [SerializeField] private bool _autoGridHeight = true;
        [SerializeField] private float _gridBottomPadding = 60f;

        [Header("Featured")]
        [SerializeField] private ShopFeaturedCard _featured;
        [SerializeField] private GameObject _featuredRoot;

        [Header("Tabs (0=Noi bat, 1=Sung, 2=Nhan vat)")]
        [SerializeField] private Button[] _tabButtons;
        [SerializeField] private Image[] _tabBgs;
        [SerializeField] private TMP_Text[] _tabLabels;
        [SerializeField] private Sprite _tabBorderSprite;
        [SerializeField] private Sprite _tabFillSprite;

        [Header("Tuy chon")]
        [Tooltip("BAT = demo (khong tru tien that). TAT = mua that qua PlayFab.")]
        [SerializeField] private bool _mockSuccess = false;
        [Tooltip("Ma virtual currency PlayFab cho tung loai tien.")]
        [SerializeField] private string _goldCurrencyCode = "GC";
        [SerializeField] private string _arenaCurrencyCode = "AC";

        private static readonly Color TealColor = new Color(0.31f, 0.84f, 0.79f, 1f);
        private static readonly Color TabDim = new Color(0.35f, 0.40f, 0.46f, 1f);

        /// <summary>Ban da xac nhan mua - noi backend/economy that o day (binder cap nhat coins).</summary>
        public event Action<ShopItemInfo> PurchaseConfirmed;

        private readonly List<ShopItemCard> _spawned = new List<ShopItemCard>();
        private readonly List<ShopItemInfo> _items = new List<ShopItemInfo>();
        private readonly HashSet<string> _owned = new HashSet<string>();
        private int _currentTab = 0;
        private float _saleTick;
        private bool _loaded;
        private string _featuredKey = "";
        private bool _hasFeatured;
        // [#13 2026-09-29] Carousel featured: nhieu item + nut trai/phai + slide.
        private readonly List<ShopItemInfo> _featuredList = new List<ShopItemInfo>();
        private int _featuredIndex;
        private UnityEngine.UI.Button _featPrev, _featNext;
        private CanvasGroup _featCg; private RectTransform _featRt; private Coroutine _featAnim;

        private void Awake()
        {
            if (_featured != null) _featured.BuyClicked += OnBuyClicked;
            if (_tabButtons != null)
                for (int i = 0; i < _tabButtons.Length; i++)
                {
                    int idx = i;
                    if (_tabButtons[i] != null) _tabButtons[i].onClick.AddListener(() => SelectTab(idx));
                }
        }

        private void OnDestroy()
        {
            if (_featured != null) _featured.BuyClicked -= OnBuyClicked;
        }

        private void OnEnable()
        {
            ApplyTabVisual();
            if (Application.isPlaying) { LoadCatalog(); return; }
#if UNITY_EDITOR
            var self = this;
            UnityEditor.EditorApplication.delayCall += () => { if (self != null && self.isActiveAndEnabled) self.Rebuild(); };
#endif
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            var self = this;
            UnityEditor.EditorApplication.delayCall += () => { if (self != null && self.isActiveAndEnabled) self.Rebuild(); };
#endif
        }

        // ---- Nap catalog dong (runtime): Title Data -> fallback local -> seed tu _catalog ----
        private void LoadCatalog()
        {
            ShopCatalogService.LoadWithFallback(_titleDataKey, _fallbackCatalog, (items, fromServer) =>
            {
                _items.Clear();
                if (items != null) _items.AddRange(items);
                if (_items.Count == 0) _items.AddRange(CollectAuthoringInfos()); // server + local trong -> seed
                _loaded = true;
                Debug.Log("[Shop] Nap " + _items.Count + " item (" + (fromServer ? "server/TitleData" : "local/seed") + ").");
                Rebuild();
                LoadOwned();
            });
        }

        // ---- Danh sach item dung de dung card ----
        private List<ShopItemInfo> WorkingItems()
        {
            if (Application.isPlaying && _loaded) return _items;
            return CollectAuthoringInfos(); // Editor / chua nap xong -> xem truoc tu asset
        }

        // Gop cac ShopItemData (catalog + featured.data), dedup theo Key.
        private List<ShopItemInfo> CollectAuthoringInfos()
        {
            var list = new List<ShopItemInfo>();
            var seen = new HashSet<string>();
            if (_catalog != null)
                foreach (var so in _catalog)
                    if (so != null) { var info = so.ToInfo(); if (seen.Add(info.Key)) list.Add(info); }
            if (_featured != null && _featured.data != null)
            {
                var info = _featured.data.ToInfo();
                if (seen.Add(info.Key)) list.Add(info);
            }
            return list;
        }

        // ---- Anh tuc thoi (local/registry/SO) truoc khi tai dong ----
        private ShopItemData FindSO(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_catalog != null) foreach (var so in _catalog) if (so != null && so.itemId == itemId) return so;
            if (_featured != null && _featured.data != null && _featured.data.itemId == itemId) return _featured.data;
            return null;
        }

        private void ResolveImmediate(ShopItemInfo info, out Sprite bg, out Sprite model)
        {
            bg = null; model = null;
            if (_artRegistry != null)
            {
                bg = _artRegistry.GetArt(info.itemId);
                model = _artRegistry.GetModel(info.itemId); // co the tra defaultModel
            }
            var so = FindSO(info.itemId);
            if (so != null)
            {
                if (bg == null) bg = so.art;
                if (so.modelArt != null) model = so.modelArt;
            }
            if (model == null && _artRegistry != null) model = _artRegistry.defaultModel;
        }

        /// <summary>Xoa card cu va sinh lai toan bo tu danh sach item hien tai (theo tab).</summary>
        [ContextMenu("Refresh Preview")]
        public void Rebuild()
        {
            if (_gridRoot == null) return;

            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                var ch = _gridRoot.GetChild(i);
                if (ch.GetComponent<ShopItemCard>() != null)
                {
                    if (Application.isPlaying) Destroy(ch.gameObject);
                    else DestroyImmediate(ch.gameObject);
                }
            }
            _spawned.Clear();

            bool featuredTab = (_currentTab == 0);
            Vector2 origin = featuredTab ? _gridOrigin : _gridOriginWide;
            int cols = Mathf.Max(1, featuredTab ? _columns : _columnsWide);

            var items = WorkingItems();
            int count = 0;

            // Neu tab hien co it nhat 1 mon MUA DUOC -> an cac mon 'sap ra mat'.
            // Neu tab khong co mon nao mua duoc -> hien 'sap ra mat' lam placeholder.
            bool includeComing = true;
            foreach (var it2 in items) { if (it2.category != ShopCategory.Featured && PassTab(it2) && !it2.comingSoon) { includeComing = false; break; } }

            if (_cardTemplate != null)
            {
                int idx = 0;
                foreach (var info in items)
                {
                    if (info.category == ShopCategory.Featured) continue;
                    if (!PassTab(info)) continue;
                    if (info.comingSoon && !includeComing) continue;
                    var card = Instantiate(_cardTemplate, _gridRoot);
                    card.gameObject.name = "ItemCard_" + (string.IsNullOrEmpty(info.itemId) ? info.displayName : info.itemId);
                    card.gameObject.SetActive(true);
                    card.data = null; // runtime dung struct, khong dinh asset
                    var rt = card.transform as RectTransform;
                    rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); // neo mep-tren de doc lap chieu cao content
                    int col = idx % cols;
                    int row = idx / cols;
                    rt.anchoredPosition = new Vector2(origin.x + col * _gridStep.x, origin.y - row * _gridStep.y);

                    Sprite bg, model;
                    ResolveImmediate(info, out bg, out model);
                    card.Bind(info, bg, model, IsOwned(info.Key));

                    if (Application.isPlaying)
                    {
                        var c = card; var ai = info;
                        if (!string.IsNullOrEmpty(ai.artUrl))
                            ShopSpriteLoader.Load(ai.artUrl, s => { if (c != null) c.SetBg(s); });
                        if (!string.IsNullOrEmpty(ai.modelUrl))
                            ShopSpriteLoader.Load(ai.modelUrl, s => { if (c != null) c.SetModel(s != null ? s : (_artRegistry != null ? _artRegistry.defaultModel : null)); });
                    }

                    card.Clicked -= OnItemClicked;
                    card.Clicked += OnItemClicked;
                    _spawned.Add(card);
                    idx++;
                }
                count = idx;
            }

            SetupFeatured(items);

            if (_dividerV != null) _dividerV.SetActive(featuredTab);
            if (_featuredRoot != null) _featuredRoot.SetActive(featuredTab);
            if (_gridHeader != null) { var hp = _gridHeader.anchoredPosition; hp.x = featuredTab ? -89f : origin.x; _gridHeader.anchoredPosition = hp; }

            if (_autoGridHeight) ApplyGridHeight(count, cols, origin.y);

#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private void SetupFeatured(List<ShopItemInfo> items)
        {
            _hasFeatured = false; _featuredKey = "";
            if (_featured == null) return;
            _featuredList.Clear();
            foreach (var info in items) if (info.category == ShopCategory.Featured) _featuredList.Add(info);
            if (_featuredList.Count == 0) { if (!Application.isPlaying) _featured.Refresh(false); EnsureFeaturedArrows(); return; }
            _hasFeatured = true;
            _featuredIndex = Mathf.Clamp(_featuredIndex, 0, _featuredList.Count - 1);
            EnsureFeaturedArrows();
            ShowFeatured(_featuredIndex, 0);
        }

        // [#13] Hien 1 featured theo index (dir: -1 trai, +1 phai, 0 khong slide).
        private void ShowFeatured(int index, int dir)
        {
            if (_featured == null || _featuredList.Count == 0) return;
            _featuredIndex = (index % _featuredList.Count + _featuredList.Count) % _featuredList.Count;
            var info = _featuredList[_featuredIndex];
            _featuredKey = info.Key; _hasFeatured = true;
            Sprite bg, model; ResolveImmediate(info, out bg, out model);
            _featured.SetInfo(info, model, IsOwned(info.Key));
            if (Application.isPlaying && !string.IsNullOrEmpty(info.modelUrl)) { var f = _featured; ShopSpriteLoader.Load(info.modelUrl, s => { if (f != null) f.SetArt(s); }); }
            bool many = _featuredList.Count > 1;
            if (_featPrev != null) _featPrev.gameObject.SetActive(many);
            if (_featNext != null) _featNext.gameObject.SetActive(many);
            if (dir != 0 && Application.isPlaying && isActiveAndEnabled) { if (_featAnim != null) StopCoroutine(_featAnim); _featAnim = StartCoroutine(FeaturedSlideCo(dir)); }
        }

        private System.Collections.IEnumerator FeaturedSlideCo(int dir)
        {
            if (_featRt == null) _featRt = _featured.GetComponent<RectTransform>();
            if (_featCg == null) { _featCg = _featured.GetComponent<CanvasGroup>(); if (_featCg == null) _featCg = _featured.gameObject.AddComponent<CanvasGroup>(); }
            if (_featRt == null) yield break;
            Vector2 baseP = _featRt.anchoredPosition; float off = 70f * dir;
            float d = 0.18f, e = 0f;
            _featRt.anchoredPosition = baseP + new Vector2(off, 0f); if (_featCg != null) _featCg.alpha = 0.2f;
            while (e < d) { e += Time.unscaledDeltaTime; float k = Mathf.Clamp01(e / d); _featRt.anchoredPosition = Vector2.Lerp(baseP + new Vector2(off, 0f), baseP, k); if (_featCg != null) _featCg.alpha = Mathf.Lerp(0.2f, 1f, k); yield return null; }
            _featRt.anchoredPosition = baseP; if (_featCg != null) _featCg.alpha = 1f; _featAnim = null;
        }

        private void FeaturedPrev() { ShowFeatured(_featuredIndex - 1, -1); }
        private void FeaturedNext() { ShowFeatured(_featuredIndex + 1, +1); }

        // [#13] Tao 2 nut trai/phai o 2 mep khu featured (runtime, khong can prefab).
        private void EnsureFeaturedArrows()
        {
            var host = _featuredRoot != null ? _featuredRoot.transform : (_featured != null ? _featured.transform.parent : null);
            if (host == null) return;
            if (_featPrev == null) _featPrev = MakeArrow(host, "bt_feat_prev", false, FeaturedPrev);
            if (_featNext == null) _featNext = MakeArrow(host, "bt_feat_next", true, FeaturedNext);
        }

        private UnityEngine.UI.Button MakeArrow(Transform host, string name, bool right, UnityEngine.Events.UnityAction cb)
        {
            foreach (Transform c in host) if (c.name == name) { var b0 = c.GetComponent<UnityEngine.UI.Button>(); if (b0 != null) { b0.onClick.RemoveAllListeners(); b0.onClick.AddListener(cb); } return b0; }
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(host, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(52f, 72f);
            rt.anchoredPosition = new Vector2(right ? -34f : 34f, 0f);
            var img = go.GetComponent<UnityEngine.UI.Image>(); img.color = new Color(0f, 0f, 0f, 0.45f);
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>(); lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var txt = lblGo.GetComponent<TMPro.TextMeshProUGUI>(); txt.text = right ? "\u203A" : "\u2039"; txt.fontSize = 40; txt.alignment = TMPro.TextAlignmentOptions.Center; txt.color = Color.white;
            var btn = go.GetComponent<UnityEngine.UI.Button>(); btn.targetGraphic = img; btn.onClick.AddListener(cb);
            go.transform.SetAsLastSibling();
            return btn;
        }

        // Keo dai Content khi grid nam trong ScrollRect (co ScrollRect to tien). Khong thi bo qua.
        private void ApplyGridHeight(int count, int cols, float originY)
        {
            if (_gridRoot == null) return;
            var scroll = _gridRoot.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content != _gridRoot) return;
            int rows = Mathf.CeilToInt(count / Mathf.Max(1f, cols));
            float cardH = _gridStep.y;
            if (_cardTemplate != null) { var crt = _cardTemplate.transform as RectTransform; if (crt != null && crt.rect.height > 1f) cardH = crt.rect.height; }
            // Content neo mep-tren: cao = le tren (|originY|) + (rows-1)*buoc + 1 the + padding day.
            float used = Mathf.Abs(originY) + Mathf.Max(0, rows - 1) * _gridStep.y + cardH * 0.5f + _gridBottomPadding;
            var sd = _gridRoot.sizeDelta;
            sd.y = Mathf.Max(used, 0f);
            _gridRoot.sizeDelta = sd;
        }

        private bool PassTab(ShopItemInfo info)
        {
            switch (_currentTab)
            {
                case 1: return info.category == ShopCategory.Weapon;
                case 2: return info.category == ShopCategory.Character;
                default: return true;
            }
        }

        /// <summary>[Aemy 2026-10-12] Tab dang mo (0 Noi bat, 1 Sung, 2 Nhan vat) - cho ShopGamepadNav.</summary>
        public int CurrentTab { get { return _currentTab; } }

        public void SelectTab(int index)
        {
            _currentTab = Mathf.Clamp(index, 0, 2);
            ApplyTabVisual();
            Rebuild();
        }

        private void ApplyTabVisual()
        {
            if (_tabBgs == null) return;
            for (int i = 0; i < _tabBgs.Length; i++)
            {
                bool active = (i == _currentTab);
                if (_tabBgs[i] != null)
                {
                    if (active && _tabFillSprite != null) _tabBgs[i].sprite = _tabFillSprite;
                    else if (!active && _tabBorderSprite != null) _tabBgs[i].sprite = _tabBorderSprite;
                    _tabBgs[i].color = active ? TealColor : TabDim;
                }
                if (_tabLabels != null && i < _tabLabels.Length && _tabLabels[i] != null)
                    _tabLabels[i].color = active ? new Color(0.06f, 0.09f, 0.12f, 1f) : new Color(0.60f, 0.65f, 0.72f, 1f);
            }
        }

        private void OnItemClicked(ShopItemInfo d) { TryBuy(d); }
        private void OnBuyClicked(ShopItemInfo d) { TryBuy(d); }

        private void TryBuy(ShopItemInfo d)
        {
            if (ModalDialog.Instance == null) return;
            if (d.comingSoon) { ModalDialog.Instance.Info("Sắp ra mắt", d.displayName + " chưa mở bán. Hãy quay lại sau nhé!"); return; }
            if (IsOwned(d.Key)) { ModalDialog.Instance.Info("Đã sở hữu", "Bạn đã có \"" + d.displayName + "\" rồi."); return; }
            string cur = d.currency == ShopCurrency.Gold ? "Vàng" : "Arena Coin";
            string price = d.DisplayPrice.ToString("#,0") + " " + cur;
            ModalDialog.Instance.Confirm("XÁC NHẬN MUA", "Mua \"" + d.displayName + "\" với giá " + price + "?", () => Purchase(d), "MUA", "HỦY");
        }

        private void Purchase(ShopItemInfo d)
        {
            // Che do demo/editor: khong tru tien that.
            if (_mockSuccess || !Application.isPlaying || BackendBootstrap.MenuDataService == null)
            {
                _owned.Add(d.Key);
                if (PurchaseConfirmed != null) PurchaseConfirmed(d);
                if (_mockSuccess && ModalDialog.Instance != null)
                    ModalDialog.Instance.Info("MUA THÀNH CÔNG", "Bạn đã sở hữu \"" + d.displayName + "\"!");
                Rebuild();
                return;
            }
            // Mua that qua PlayFab: server tru VC + cap item (chong gian lan).
            string code = d.currency == ShopCurrency.Gold ? _goldCurrencyCode : _arenaCurrencyCode;
            var item = d;
            BackendBootstrap.MenuDataService.PurchaseItem(item.Key, code, item.DisplayPrice,
                () =>
                {
                    _owned.Add(item.Key);
                    if (PurchaseConfirmed != null) PurchaseConfirmed(item);
                    if (ModalDialog.Instance != null)
                        ModalDialog.Instance.Info("MUA THÀNH CÔNG", "Bạn đã sở hữu \"" + item.displayName + "\"!");
                    Rebuild();
                },
                err =>
                {
                    if (ModalDialog.Instance != null)
                        ModalDialog.Instance.Info("MUA THẤT BẠI", err != null ? err.ErrorMessage : "Không đủ tiền hoặc lỗi hệ thống.");
                });
        }

        // Nap danh sach da mua tu PlayFab inventory (khi vao Shop luc runtime).
        private void LoadOwned()
        {
            if (!Application.isPlaying || BackendBootstrap.MenuDataService == null) return;
            BackendBootstrap.MenuDataService.GetOwnedItems(
                ids =>
                {
                    _owned.Clear();
                    if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _owned.Add(id);
                    Rebuild();
                },
                err => Debug.LogWarning("[Shop] Nap danh sach da mua loi: " + (err != null ? err.ErrorMessage : "?")));
        }

        private bool IsOwned(string key) { return !string.IsNullOrEmpty(key) && _owned.Contains(key); }

        private void Update()
        {
            if (!Application.isPlaying) return;
            _saleTick += Time.unscaledDeltaTime;
            if (_saleTick < 30f) return;
            _saleTick = 0f;
            foreach (var c in _spawned) if (c != null && c.gameObject.activeSelf) c.RefreshSale();
            if (_featured != null && _hasFeatured) _featured.Refresh(IsOwned(_featuredKey));
        }
    }
}
