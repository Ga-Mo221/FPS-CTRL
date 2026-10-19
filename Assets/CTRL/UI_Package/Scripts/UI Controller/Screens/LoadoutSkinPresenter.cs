// [Aemy 2026-10-10] FILE MOI. Vung giua Trang bi: model sung xoay duoc + chon SKIN bang 2 nut trai/phai.
// [Aemy 2026-10-11] Bo gia/mua o day: skin chua so huu -> nut DEN CUA HANG (mua trong Shop).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using Backend.Bootstrap;
using Game.Skins;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// - Sung dang "xem" = sung vua chon/bo chon trong danh sach, hoac sung tay cam dang di toi.
    /// - Nut ◄ ► (chuot/tay cam; tay cam them phim X/Y) doi qua: [Ban goc] + cac skin cua sung (WeaponSkinCatalog).
    /// - Skin DA SO HUU -> trang bi ngay (WeaponSkinService.Equip) -> vao tran spawn sung theo ID skin (dong bo mang).
    /// - Skin CHUA SO HUU -> hien khoa + loi nhan "mua trong Cua hang" + nut DEN CUA HANG (TopBar -> tab Shop).
    ///   Mua ban do ShopController lo (SRP); quay lai Trang bi -> OnEnable tai lai danh sach so huu.
    /// </summary>
    public class LoadoutSkinPresenter : MonoBehaviour
    {
        [SerializeField] private LoadoutController _loadout;
        [SerializeField] private ModelPreviewStage _preview;
        [SerializeField] private TMP_Text _weaponNameLabel;
        [SerializeField] private TMP_Text _skinNameLabel;
        [SerializeField] private TMP_Text _skinCounterLabel;
        [SerializeField] private TISGamepadUIButton _prevButton;
        [SerializeField] private TISGamepadUIButton _nextButton;
        [SerializeField] private GameObject _lockGroup;
        [FormerlySerializedAs("_priceLabel")]
        [SerializeField] private TMP_Text _lockHintLabel;        // "Mua skin này trong CỬA HÀNG"
        [FormerlySerializedAs("_unlockButton")]
        [SerializeField] private TISGamepadUIButton _shopButton; // "ĐẾN CỬA HÀNG"
        [SerializeField] private TMP_Text _statusLabel;          // "ĐANG DÙNG" / "CHƯA SỞ HỮU"
        [SerializeField] private TMP_Text _emptyLabel;           // "Chưa có model" khi sung khong co prefab

        private WeaponSkinCatalog _catalog;
        private readonly HashSet<string> _owned = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _options = new List<string>();   // [base, skin1, skin2...] (ID)
        private List<int> _lastSelection = new List<int>();
        private string _baseId = "";
        private int _option;
        private bool _hooked;
        private Color _skinLabelDefault;
        private bool _skinLabelColorSaved;

        private void Awake()
        {
            _catalog = WeaponSkinCatalog.Load();
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Step(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Step(1));
            if (_shopButton != null) _shopButton.onClick.AddListener(GoToShop);
            if (_skinNameLabel != null) { _skinLabelDefault = _skinNameLabel.color; _skinLabelColorSaved = true; }
        }

        private void OnEnable()
        {
            if (_loadout != null)
            {
                _loadout.WeaponSelectionChanged += OnWeaponSelection;
                _loadout.WeaponsApplied += OnWeaponsApplied;          // [Aemy 2026-10-11] doi slot ALPHA/BRAVO/CHARLIE, tai du lieu
                _loadout.FocusedWeaponChanged += ShowWeapon;           // model + chi so cung 1 nguon
                _loadout.WeaponRightClicked += FocusWeapon;            // [Aemy 2026-10-17] chuot phai = chi xem
            }
            HookItemFocus();
            RefreshOwnership();
            // Danh sach sung co the duoc LoadoutBinder nap tre 1 frame -> chon sung "dang xem" sau 1 frame.
            StartCoroutine(FocusInitialNextFrame());
        }

        private System.Collections.IEnumerator FocusInitialNextFrame()
        {
            yield return null;
            var sel = _loadout != null ? _loadout.SelectedWeaponIndices() : null;
            _lastSelection = sel != null ? new List<int>(sel) : new List<int>();
            FocusWeapon(PickPreferred(_lastSelection));
        }

        private void OnWeaponsApplied(IReadOnlyList<int> sel)
        {
            _lastSelection = sel != null ? new List<int>(sel) : new List<int>();
            if (isActiveAndEnabled) FocusWeapon(PickPreferred(_lastSelection));
        }

        // Uu tien: sung dang chon CO skin -> sung dang chon CO model -> sung dang chon bat ky -> sung dau tien co model.
        private int PickPreferred(IList<int> candidates)
        {
            if (_loadout == null) return -1;
            var gm = GameManager.Instance;
            System.Func<int, bool> hasSkins = i => _catalog != null && _catalog.SkinsFor(_loadout.GetWeaponName(i) ?? "").Count > 0;
            System.Func<int, bool> hasModel = i => ModelSourceOf(WeaponLookup.Resolve(_loadout.GetWeaponName(i) ?? "")) != null;
            if (candidates != null)
            {
                foreach (var i in candidates) if (hasSkins(i)) return i;
                foreach (var i in candidates) if (hasModel(i)) return i;
                if (candidates.Count > 0) return candidates[0];
            }
            for (int i = 0; i < _loadout.WeaponCount; i++) if (hasModel(i)) return i;
            return 0;
        }

        private void OnDisable()
        {
            if (_loadout != null)
            {
                _loadout.WeaponSelectionChanged -= OnWeaponSelection;
                _loadout.WeaponsApplied -= OnWeaponsApplied;
                _loadout.FocusedWeaponChanged -= ShowWeapon;
                _loadout.WeaponRightClicked -= FocusWeapon;
            }
        }

        // tay cam di toi o sung -> xem sung do
        private void HookItemFocus()
        {
            if (_hooked || _loadout == null) return;
            for (int i = 0; i < _loadout.WeaponCount; i++)
            {
                var t = _loadout.GetWeaponItem(i); if (t == null) continue;
                var b = t.GetComponent<TISGamepadUIButton>(); if (b == null) continue;
                int idx = i; b.onSelect.AddListener(() => FocusWeapon(idx));
                // [Aemy 2026-10-11] Bam chuot vao o nao (ke ca Dao / sung vua bo chon) -> xem o do. Doi 1 frame de chay SAU su kien chon.
                b.onClick.AddListener(() => { if (isActiveAndEnabled) StartCoroutine(FocusNextFrame(idx)); });
            }
            _hooked = true;
        }

        private System.Collections.IEnumerator FocusNextFrame(int idx)
        {
            yield return null;
            FocusWeapon(idx);
        }

        private void OnWeaponSelection(IReadOnlyList<int> sel)
        {
            var added = new List<int>();
            foreach (var i in sel) if (!_lastSelection.Contains(i)) added.Add(i); // sung vua them
            int focus;
            if (added.Count == 1) focus = added[0];                 // nguoi choi vua bam chon 1 sung
            else if (added.Count > 1) focus = PickPreferred(added); // nap ca bo (doi preset / load)
            else focus = sel.Count > 0 ? sel[sel.Count - 1] : -1;
            _lastSelection = new List<int>(sel);
            if (focus >= 0) FocusWeapon(focus);
        }

        private void RefreshOwnership()
        {
            var svc = BackendBootstrap.MenuDataService;
            if (svc == null) { Render(); return; }
            svc.GetOwnedItems(ids => { _owned.Clear(); if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _owned.Add(id.Trim().ToLowerInvariant()); Render(); },
                              e => Render());
        }

        // Chon sung dang xem -> LoadoutController ban FocusedWeaponChanged -> ShowWeapon (model) + LoadoutBinder (the chi so).
        private void FocusWeapon(int index)
        {
            if (_loadout == null || index < 0 || index >= _loadout.WeaponCount) return;
            _loadout.FocusWeapon(index);
        }

        private void ShowWeapon(int index)
        {
            if (_loadout == null || index < 0 || index >= _loadout.WeaponCount) return;
            _baseId = _loadout.GetWeaponName(index) ?? "";
            _options.Clear(); _options.Add(_baseId);
            if (_catalog != null) foreach (var s in _catalog.SkinsFor(_baseId)) _options.Add(s.skinWeaponId);
            string eq = WeaponSkinService.GetEquipped(_baseId);
            _option = Mathf.Max(0, _options.IndexOf(eq));
            Render();
        }

        private void Step(int dir)
        {
            if (_options.Count <= 1) return;
            _option = (_option + dir + _options.Count) % _options.Count;
            // da so huu -> trang bi luon
            if (IsOwned(_option)) WeaponSkinService.Equip(_baseId, _option == 0 ? "" : _options[_option]);
            Render();
        }

        private bool IsOwned(int option)
        {
            if (option <= 0) return true; // ban goc: khoa/mo do LoadoutBinder quan ly
            var skin = _catalog != null ? _catalog.FindBySkinId(_options[option]) : null;
            return skin != null && _owned.Contains(skin.ItemId);
        }

        private void Render()
        {
            if (_options.Count == 0) return;
            string id = _options[Mathf.Clamp(_option, 0, _options.Count - 1)];
            var skin = _option > 0 && _catalog != null ? _catalog.FindBySkinId(id) : null;
            bool owned = IsOwned(_option);
            bool equipped = string.Equals(WeaponSkinService.GetEquipped(_baseId), _option == 0 ? "" : id, System.StringComparison.OrdinalIgnoreCase);

            if (_weaponNameLabel) _weaponNameLabel.text = _baseId.ToUpperInvariant();
            if (_skinNameLabel)
            {
                _skinNameLabel.text = _option == 0 ? "MẶC ĐỊNH" : (skin != null && !string.IsNullOrEmpty(skin.displayName) ? skin.displayName : id);
                if (_skinLabelColorSaved) _skinNameLabel.color = skin != null ? skin.tagColor : _skinLabelDefault; // mau rieng tung skin
            }
            if (_skinCounterLabel) _skinCounterLabel.text = _options.Count > 1 ? (_option + 1) + " / " + _options.Count : "Chưa có skin";
            bool many = _options.Count > 1;
            if (_prevButton) _prevButton.gameObject.SetActive(many);
            if (_nextButton) _nextButton.gameObject.SetActive(many);
            if (_lockGroup) _lockGroup.SetActive(!owned);
            if (_lockHintLabel) _lockHintLabel.text = "Mua skin này trong CỬA HÀNG";
            if (_statusLabel) _statusLabel.text = !owned ? "CHƯA SỞ HỮU" : (equipped ? "ĐANG DÙNG" : "");

            // model: prefab roi cua WeaponData (co hinh sung), nuong tinh -> khong chay script Fusion
            GameObject src = null;
            var data = !string.IsNullOrEmpty(id) ? WeaponLookup.Resolve(id) : null; // [Aemy 2026-10-11] "Dao" -> Knife
            src = ModelSourceOf(data);
            bool shown = _preview != null && _preview.Show(src, true, data != null ? data.PreviewEuler : Vector3.zero); // [Aemy 2026-10-19] goc rieng tung sung (Dusk)
            if (_emptyLabel) _emptyLabel.gameObject.SetActive(!shown);
        }

        // Hinh de xem: prefab roi (DropItemPrefab) neu co, khong thi prefab sung (vd Dao chi co "Knife PF").
        private static GameObject ModelSourceOf(WeaponData d)
        {
            if (d == null) return null;
            if (d.DropItemPrefab != null) return d.DropItemPrefab.gameObject;
            var gm = GameManager.Instance;
            return gm != null ? gm.GetWeapon(d.ID) : null;
        }

        // Skin chua so huu -> sang tab CUA HANG de mua (ShopController lo mua ban).
        private void GoToShop()
        {
            MenuNavigation.OpenShopWeapons(this);
        }

        // tay cam: X = skin truoc, Y = skin sau (chi khi panel dang mo + dang dung tay cam + khong co overlay)
        private void Update()
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp == null || TISGamepadUIButton.ExternalSuppress > 0) return;
            var br = TISInputBridge.Instance; if (br != null && !br.IsGamepad) return;
            if (gp.buttonWest.wasPressedThisFrame) Step(-1);
            else if (gp.buttonNorth.wasPressedThisFrame) Step(1);
        }
    }
}
