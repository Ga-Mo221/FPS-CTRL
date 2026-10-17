using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the Loadout screen.
    /// - Loadout config tabs (Alpha/Bravo/Charlie): radio (maxSelection = 1)
    /// - Primary weapons: pick up to 2 (maxSelection = 2)
    /// - Utilities (frag/flash/smoke): pick up to 2 (maxSelection = 2)
    /// Selection rules are configured on the UISelectionGroup components, not here.
    /// </summary>
    public class LoadoutController : MonoBehaviour
    {
        [Serializable]
        public struct WeaponCardInfo
        {
            public string weaponName;
            public string weaponType;
            [Range(0f, 1f)] public float damage;
            [Range(0f, 1f)] public float fireRate;
            [Range(0f, 1f)] public float accuracy;
            public Sprite model;
        }

        [Header("Config slots (radio)")]
        [SerializeField] private UISelectionGroup _loadoutTabGroup;

        [Header("Primary weapons (pick up to 2)")]
        [SerializeField] private UISelectionGroup _weaponGroup;

        [Header("Utilities (pick up to 2)")]
        [SerializeField] private UISelectionGroup _utilityGroup;

        [Header("Save")]
        [SerializeField] private Button _saveButton;

        [Header("Weapon info panel")]
        [SerializeField] private Image _weaponModelImage;
        [SerializeField] private TMP_Text _weaponNameLabel;
        [SerializeField] private TMP_Text _weaponTypeLabel;
        [SerializeField] private UIFillBar _damageBar;
        [SerializeField] private UIFillBar _fireRateBar;
        [SerializeField] private UIFillBar _accuracyBar;

        // ---- Events ----
        public event Action<int> LoadoutSlotChanged;               // Alpha/Bravo/Charlie index
        public event Action<IReadOnlyList<int>> WeaponSelectionChanged; // up to 2 indices
        public event Action<IReadOnlyList<int>> UtilitySelectionChanged; // up to 2 indices
        public event Action<IReadOnlyList<int>, IReadOnlyList<int>> SaveClicked; // weapons, utilities
        // [Aemy 2026-10-11] Sung dang "xem" (khung model + the chi so dung CHUNG 1 nguon -> luon khop nhau).
        public event Action<int> FocusedWeaponChanged;
        public event Action<IReadOnlyList<int>> WeaponsApplied;   // ApplyWeapons (doi slot / tai du lieu) - khong phai nguoi choi bam
        public int FocusedWeapon { get; private set; } = -1;

        /// <summary>Dat sung dang xem (goi tu khung model). Ban FocusedWeaponChanged ke ca khi trung index (lam moi the chi so).</summary>
        public void FocusWeapon(int index)
        {
            FocusedWeapon = index;
            FocusedWeaponChanged?.Invoke(index);
        }

        private void Awake()
        {
            if (_loadoutTabGroup != null) _loadoutTabGroup.SelectionChanged += OnLoadoutTabChanged;
            if (_weaponGroup != null) _weaponGroup.SelectionChanged += OnWeaponChanged;
            if (_utilityGroup != null) _utilityGroup.SelectionChanged += OnUtilityChanged;
            if (_saveButton != null) _saveButton.onClick.AddListener(OnSave);
        }

        // [Aemy 2026-10-12] Tay cam bam A vao sung KHOA -> giong click chuot vao lop khoa (sang Shop).
        private bool _lockClicksHooked;
        private void HookLockedClicks()
        {
            if (_lockClicksHooked || _weaponGroup == null || _weaponGroup.ItemCount == 0) return;
            for (int i = 0; i < _weaponGroup.ItemCount; i++)
            {
                var it = _weaponGroup.GetItem(i); if (it == null) continue;
                int idx = i; it.LockedClicked += _ => { if (LockedWeaponClicked != null) LockedWeaponClicked(idx); };
            }
            _lockClicksHooked = true;
        }

        /// <summary>[Aemy 2026-10-12] O utility (de LoadoutGamepadNav noi cot sung <-> cot utility).</summary>
        public Transform GetUtilityItem(int index) { var it = _utilityGroup != null ? _utilityGroup.GetItem(index) : null; return it != null ? it.transform : null; }

        private void OnEnable()
        {
            HookLockedClicks();
            RefreshWeaponIcons();
        }

        /// <summary>Nap icon sung (iconUrl tu WeaponSpecs) vao child Image ten 'WeaponIcon' cua tung slot, neu slot co.</summary>
        public void RefreshWeaponIcons()
        {
            if (_weaponGroup == null) return;
            for (int i = 0; i < _weaponGroup.ItemCount; i++)
            {
                var item = _weaponGroup.GetItem(i);
                if (item == null) continue;
                var iconTf = item.transform.Find("WeaponIcon");
                if (iconTf == null) continue;
                var img = iconTf.GetComponent<Image>();
                if (img == null) continue;
                Backend.PlayfabImpl.WeaponSpecService.LoadInto(GetWeaponName(i), img, false);
            }
        }

        private void OnDestroy()
        {
            if (_loadoutTabGroup != null) _loadoutTabGroup.SelectionChanged -= OnLoadoutTabChanged;
            if (_weaponGroup != null) _weaponGroup.SelectionChanged -= OnWeaponChanged;
            if (_utilityGroup != null) _utilityGroup.SelectionChanged -= OnUtilityChanged;
            if (_saveButton != null) _saveButton.onClick.RemoveListener(OnSave);
        }

        private void OnLoadoutTabChanged(IReadOnlyList<UISelectable> sel)
        {
            if (sel.Count > 0) LoadoutSlotChanged?.Invoke(sel[0].Index);
        }

        private void OnWeaponChanged(IReadOnlyList<UISelectable> sel)
            => WeaponSelectionChanged?.Invoke(ToIndices(sel));

        private void OnUtilityChanged(IReadOnlyList<UISelectable> sel)
            => UtilitySelectionChanged?.Invoke(ToIndices(sel));

        private void OnSave()
        {
            var weapons = _weaponGroup != null ? _weaponGroup.GetSelectedIndices() : new List<int>();
            var utils = _utilityGroup != null ? _utilityGroup.GetSelectedIndices() : new List<int>();
            SaveClicked?.Invoke(weapons, utils);
        }

        // ---- Public setters ----

        /// <summary>Locks a weapon slot (e.g. still locked by player level).</summary>
        public void SetWeaponLocked(int index, bool locked)
        {
            var item = GetItem(_weaponGroup, index);
            if (item != null) item.SetLocked(locked);
        }

        /// <summary>Doc cap do yeu cau tu nhan "... LV.X" cua item. Tra 0 neu khong co.</summary>
        private static int ReadRequiredLevel(UISelectable item, out TMP_Text lockLabel)
        {
            lockLabel = null;
            var tmps = item.GetComponentsInChildren<TMP_Text>(true);
            if (tmps == null) return 0;
            foreach (var t in tmps)
            {
                var txt = t.text != null ? t.text : "";
                int idx = txt.IndexOf("LV.", System.StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                int j = idx + 3;
                int val = 0; bool any = false;
                while (j < txt.Length && char.IsDigit(txt[j])) { val = val * 10 + (txt[j] - '0'); j++; any = true; }
                if (any) { lockLabel = t; return val; }
            }
            return 0;
        }


        /// <summary>Ap khoa theo cap do nguoi choi cho cac slot sung co nhan "Mo khoa LV.X".
        /// Du cap -> mo khoa + an nhan; thieu cap -> khoa. Slot khong co nhan LV thi bo qua.</summary>
        public void ApplyLevelLocks(int playerLevel)
        {
            if (_weaponGroup == null) return;
            for (int i = 0; i < _weaponGroup.ItemCount; i++)
            {
                var item = _weaponGroup.GetItem(i);
                if (item == null) continue;
                TMP_Text lockLabel;
                int required = ReadRequiredLevel(item, out lockLabel);
                if (required <= 0) continue;
                bool locked = playerLevel < required;
                item.SetLocked(locked);
                if (lockLabel != null) lockLabel.gameObject.SetActive(locked);
            }
        }


        /// <summary>Updates the weapon info panel (right side).</summary>
        public void ShowWeaponInfo(WeaponCardInfo info)
        {
            if (_weaponNameLabel != null) _weaponNameLabel.text = info.weaponName;
            if (_weaponTypeLabel != null) _weaponTypeLabel.text = info.weaponType;
            if (_damageBar != null) _damageBar.SetValue01(info.damage);
            if (_fireRateBar != null) _fireRateBar.SetValue01(info.fireRate);
            if (_accuracyBar != null) _accuracyBar.SetValue01(info.accuracy);
            if (_weaponModelImage != null && info.model != null) _weaponModelImage.sprite = info.model;
            if (_weaponModelImage != null)
                Backend.PlayfabImpl.WeaponSpecService.LoadInto(info.weaponName, _weaponModelImage, true);
        }

        public void SetSaveInteractable(bool value)
        {
            if (_saveButton != null) _saveButton.interactable = value;
        }

        // ---- Loadout slot binding (dung boi LoadoutBinder) ----

        /// <summary>So luong item sung / utility.</summary>
        public int WeaponCount { get { return _weaponGroup != null ? _weaponGroup.ItemCount : 0; } }
        public int UtilityCount { get { return _utilityGroup != null ? _utilityGroup.ItemCount : 0; } }

        /// <summary>Ten sung o vi tri index (doc tu nhan item) - de map index &lt;-&gt; ten.</summary>
        public string GetWeaponName(int index) { return LabelOf(_weaponGroup, index); }
        // [Aemy 2026-10-10] cho LoadoutSkinPresenter: o sung (de bat focus tay cam) + danh sach sung dang chon.
        public Transform GetWeaponItem(int index) { var it = _weaponGroup != null ? _weaponGroup.GetItem(index) : null; return it != null ? it.transform : null; }
        public List<int> SelectedWeaponIndices() { return _weaponGroup != null ? new List<int>(_weaponGroup.GetSelectedIndices()) : new List<int>(); }
        /// <summary>Ten utility o vi tri index.</summary>
        public string GetUtilityName(int index) { return LabelOf(_utilityGroup, index); }

        // [Aemy 2026-10-01] Codename grenade cho utility -> luu len PlayFab de GameManager.GetGrenade(Contains) khop.
        // Thu tu UI utility co dinh: [0] Lựu đạn (Frag), [1] Choáng (Flash), [2] Khói (Smoke).
        public string GetUtilityCodename(int index)
        {
            switch (index)
            {
                case 1: return WeaponID._FLASH_M84;
                case 2: return WeaponID._SMOKE_AN_M8_HC;
                default: return WeaponID._FRAG_M67;
            }
        }

        /// <summary>Loai sung o vi tri index (nhan loai, bo qua nhan khoa "Mo khoa LV."). Rong neu khong co.</summary>
        public string GetWeaponType(int index)
        {
            var item = _weaponGroup != null ? _weaponGroup.GetItem(index) : null;
            if (item == null) return "";
            var tmps = item.GetComponentsInChildren<TMP_Text>(true);
            if (tmps == null || tmps.Length == 0) return "";
            // Ten = nhan font lon nhat.
            TMP_Text nameLbl = null; float maxFs = -1f;
            foreach (var t in tmps) if (t.fontSize > maxFs) { maxFs = t.fontSize; nameLbl = t; }
            // Loai = nhan font lon nhat trong so con lai, khong phai nhan khoa (Mo khoa LV.).
            TMP_Text typeLbl = null; float bestFs = -1f;
            foreach (var t in tmps)
            {
                if (t == nameLbl) continue;
                var txt = t.text != null ? t.text.Trim() : "";
                if (txt.StartsWith("Mở khóa") || txt.Contains("LV.")) continue;
                if (t.fontSize > bestFs) { bestFs = t.fontSize; typeLbl = t; }
            }
            return typeLbl != null ? typeLbl.text.Trim() : "";
        }

        /// <summary>Chon tab slot (KHONG ban su kien) - dung khi ap slot mac dinh luc tai.</summary>
        public void SelectSlotTab(int index)
        {
            if (_loadoutTabGroup != null) _loadoutTabGroup.SelectByIndex(index, false);
        }

        /// <summary>Ap lua chon sung (theo index), xoa lua chon cu. KHONG ban su kien.</summary>
        public void ApplyWeapons(IList<int> indices)
        {
            ApplyTo(_weaponGroup, indices);
            WeaponsApplied?.Invoke(indices != null ? new List<int>(indices) : new List<int>()); // [Aemy 2026-10-11]
        }
        /// <summary>Ap lua chon utility (theo index), xoa lua chon cu. KHONG ban su kien.</summary>
        public void ApplyUtilities(IList<int> indices) { ApplyTo(_utilityGroup, indices); }

        private static void ApplyTo(UISelectionGroup group, IList<int> indices)
        {
            if (group == null) return;
            group.ClearSelection(false);
            if (indices != null)
                foreach (var i in indices) group.SelectByIndex(i, false);
        }

        private static string LabelOf(UISelectionGroup group, int index)
        {
            var item = group != null ? group.GetItem(index) : null;
            if (item == null) return null;
            var t = item.GetComponentInChildren<TMP_Text>(true);
            return t != null ? t.text.Trim() : null;
        }

        // ---- Helpers ----
        private static List<int> ToIndices(IReadOnlyList<UISelectable> sel)
        {
            var list = new List<int>(sel.Count);
            foreach (var s in sel) list.Add(s.Index);
            return list;
        }

        private static UISelectable GetItem(UISelectionGroup group, int index)
            => group != null ? group.GetItem(index) : null;
    

// ===== [Aemy 2026-10-04] Khoa sung theo SO HUU (mua GC o Shop) thay vi theo level =====
        public event System.Action<int> LockedWeaponClicked;
        private readonly Dictionary<int, GameObject> _lockOverlays = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TMP_Text> _lockPriceLabels = new Dictionary<int, TMP_Text>();

        /// <summary>Ap trang thai so huu 1 slot sung. owned=false -> khoa slot + hien lop phu (dim + KHOA + gia).</summary>
        public void SetWeaponOwnership(int index, bool owned, int price, bool hasPrice)
        {
            var item = GetItem(_weaponGroup, index);
            if (item == null) return;
            item.SetLocked(!owned);
            var overlay = EnsureLockOverlay(item, index);
            if (overlay != null) overlay.SetActive(!owned);
            TMP_Text lbl;
            if (!owned && _lockPriceLabels.TryGetValue(index, out lbl) && lbl != null)
                lbl.text = hasPrice ? (price.ToString("#,0") + " GC") : "MUA Ở CỬA HÀNG";
            TMP_Text lockLabel; ReadRequiredLevel(item, out lockLabel);
            if (lockLabel != null) lockLabel.gameObject.SetActive(false);
        }

        private GameObject EnsureLockOverlay(UISelectable item, int index)
        {
            GameObject existing;
            if (_lockOverlays.TryGetValue(index, out existing) && existing != null) return existing;
            var host = item.transform as RectTransform;
            if (host == null) return null;

            var ov = new GameObject("LockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var ort = ov.GetComponent<RectTransform>();
            ort.SetParent(host, false);
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
            var dim = ov.GetComponent<Image>(); dim.color = new Color(0.03f, 0.05f, 0.08f, 0.66f); dim.raycastTarget = true;
            var btn = ov.GetComponent<Button>(); btn.transition = Selectable.Transition.None;
            int idx = index;
            btn.onClick.AddListener(delegate { if (LockedWeaponClicked != null) LockedWeaponClicked(idx); });

            var srcFont = item.GetComponentInChildren<TMP_Text>(true);

            var lockGo = new GameObject("LockLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var lrt = lockGo.GetComponent<RectTransform>();
            lrt.SetParent(ort, false);
            // [Aemy 2026-10-11] Day chu khoa sang PHAI o -> khong de len ten/loai sung ben trai.
            lrt.anchorMin = new Vector2(1f, 0.5f); lrt.anchorMax = new Vector2(1f, 0.5f); lrt.pivot = new Vector2(1f, 0.5f);
            lrt.anchoredPosition = new Vector2(-20f, 11f); lrt.sizeDelta = new Vector2(200f, 30f);
            var ltxt = lockGo.GetComponent<TextMeshProUGUI>();
            ltxt.text = "KHÓA"; ltxt.alignment = TextAlignmentOptions.Right; ltxt.fontSize = 22f; ltxt.fontStyle = FontStyles.Bold;
            ltxt.color = new Color(0.93f, 0.95f, 0.98f, 1f); ltxt.raycastTarget = false; ltxt.enableWordWrapping = false;
            if (srcFont != null && srcFont.font != null) ltxt.font = srcFont.font;

            var priceGo = new GameObject("LockPrice", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var prt = priceGo.GetComponent<RectTransform>();
            prt.SetParent(ort, false);
            prt.anchorMin = new Vector2(1f, 0.5f); prt.anchorMax = new Vector2(1f, 0.5f); prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-20f, -13f); prt.sizeDelta = new Vector2(220f, 24f);
            var ptxt = priceGo.GetComponent<TextMeshProUGUI>();
            ptxt.alignment = TextAlignmentOptions.Right; ptxt.fontSize = 15f; ptxt.fontStyle = FontStyles.Bold;
            ptxt.color = new Color(0.31f, 0.86f, 0.80f, 1f); ptxt.raycastTarget = false; ptxt.enableWordWrapping = false;
            if (srcFont != null && srcFont.font != null) ptxt.font = srcFont.font;

            _lockOverlays[index] = ov;
            _lockPriceLabels[index] = ptxt;
            ov.transform.SetAsLastSibling();
            return ov;
        }
}
}
