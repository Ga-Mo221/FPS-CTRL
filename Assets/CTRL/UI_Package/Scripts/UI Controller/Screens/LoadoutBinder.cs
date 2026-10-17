using System.Collections.Generic;
using UnityEngine;
using Backend.Bootstrap;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Cau noi Loadout UI (presentation) &lt;-&gt; PlayFab (ILoadoutService).
    /// Luong:
    ///  - Mo man: LoadAllLoadouts -> tai ca 3 slot ve. Slot nao co du lieu thi do vao UI,
    ///    slot rong thi de trong.
    ///  - Doi tab (Alpha/Bravo/Charlie): hien lua chon da luu cua slot do.
    ///  - Doi sung/utility: cap nhat vao bo nho phien cua slot dang chon.
    ///  - Bam Save: luu slot dang chon len PlayFab (SaveLoadoutSlot).
    /// Luu theo TEN sung/utility (khop LoadoutConfig). LoadoutController lo phan hien thi.
    /// </summary>
    public class LoadoutBinder : MonoBehaviour
    {
        [SerializeField] private LoadoutController _loadout;
        [Tooltip("So slot loadout (Alpha/Bravo/Charlie = 3).")]
        [SerializeField] private int _slotCount = 3;

        private LoadoutConfig[] _slots;                 // cache phien theo slot
        private int _currentSlot = 0;                   // slot (tab) dang chon
        private bool _loaded;                            // da tai xong tu PlayFab chua

        // Bang tra ten <-> index (dung nhan item cua UI).
        private readonly Dictionary<string, int> _weaponIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _utilIndex = new Dictionary<string, int>();

private void Awake()
        {
            _slots = new LoadoutConfig[Mathf.Max(1, _slotCount)];
            if (_loadout != null)
            {
                _loadout.LoadoutSlotChanged += OnSlotChanged;
                _loadout.WeaponSelectionChanged += OnWeaponsChanged;
                _loadout.UtilitySelectionChanged += OnUtilsChanged;
                _loadout.SaveClicked += OnSave;
                _loadout.LockedWeaponClicked += OnLockedWeaponClicked;
                _loadout.FocusedWeaponChanged += ShowWeaponStats; // [Aemy 2026-10-11] the chi so = sung dang xem
                BuildCatalogs();
            }
        }

private void OnDestroy()
        {
            if (_loadout != null)
            {
                _loadout.LoadoutSlotChanged -= OnSlotChanged;
                _loadout.WeaponSelectionChanged -= OnWeaponsChanged;
                _loadout.UtilitySelectionChanged -= OnUtilsChanged;
                _loadout.SaveClicked -= OnSave;
                _loadout.LockedWeaponClicked -= OnLockedWeaponClicked;
                _loadout.FocusedWeaponChanged -= ShowWeaponStats;
            }
        }

private void Start()
        {
            LoadAll();
        }

        // Moi lan mo panel Trang bi -> ap lai khoa theo so huu (vua mua o Shop ve la cap nhat).
        private void OnEnable()
        {
            ApplyOwnershipLocks();
        }

        /// <summary>Doc cap do nguoi choi tu PlayFab roi ap khoa/mo cac slot sung khoa theo level.</summary>
        /// <summary>Doc cap do nguoi choi (qua ProfileCache dung chung) roi ap khoa/mo cac slot sung khoa theo level.</summary>
        public void ApplyLevelLocks()
        {
            if (_loadout == null) return;
            ProfileCache.Get(
                profile => _loadout.ApplyLevelLocks(profile.Level),
                err => Debug.LogWarning("[LoadoutBinder] Doc level loi: " + err.ErrorMessage));
        }


        // Xay bang ten->index tu nhan cac item trong UI.
        private void BuildCatalogs()
        {
            _weaponIndex.Clear();
            for (int i = 0; i < _loadout.WeaponCount; i++)
            {
                var n = _loadout.GetWeaponName(i);
                if (!string.IsNullOrEmpty(n)) _weaponIndex[n] = i;
            }
            // [Aemy 2026-10-11] Loadout cu luu ten cu ("Dao") -> van tro dung o sung moi ("Knife").
            foreach (var kv in WeaponLookup.LegacyNames)
            {
                int k;
                if (!_weaponIndex.ContainsKey(kv.Key) && _weaponIndex.TryGetValue(kv.Value, out k)) _weaponIndex[kv.Key] = k;
            }
            _utilIndex.Clear();
            for (int i = 0; i < _loadout.UtilityCount; i++)
            {
                var n = _loadout.GetUtilityName(i);
                if (!string.IsNullOrEmpty(n)) _utilIndex[n] = i;
                // [Aemy 2026-10-10] Luu len PlayFab la CODENAME (Frag-M67/Flash-M84/Smoke-AN-M8 HC) -> phai map ca codename,
                // neu khong luu xong vao lai se khong hien utility da chon.
                var code = _loadout.GetUtilityCodename(i);
                if (!string.IsNullOrEmpty(code)) _utilIndex[code] = i;
            }
        }

        /// <summary>Tai ca 3 slot tu PlayFab roi do slot dang chon vao UI.</summary>
        public void LoadAll()
        {
            var svc = BackendBootstrap.LoadoutService;
            if (svc == null) { Debug.LogWarning("[LoadoutBinder] Chua co LoadoutService."); return; }

            svc.LoadAllLoadouts(arr =>
            {
                for (int i = 0; i < _slots.Length; i++)
                    _slots[i] = (arr != null && i < arr.Length) ? arr[i] : null;
                _loaded = true;
                ApplySlot(_currentSlot);   // do slot dang chon (co data thi hien, khong thi trong)
            },
            err => Debug.LogWarning("[LoadoutBinder] Tai loadout loi: " + err.ErrorMessage));
        }

        // Doi tab -> hien lua chon da luu cua slot do.
        private void OnSlotChanged(int index)
        {
            _currentSlot = index;
            UnityEngine.PlayerPrefs.SetInt("ActiveLoadoutSlot", index); // [UI-INTEGRATION] nho slot dang chon de vao tran dung loadout
            if (_loaded) { ApplySlot(index); WriteActiveLoadoutPrefs(index >= 0 && index < _slots.Length ? _slots[index] : null); } // [LOADOUT-FIX 2026-09-28 Aemy]
        }

        // Do cau hinh slot index len UI (theo ten -> index). Slot rong -> xoa lua chon.
        private void ApplySlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return;
            var cfg = _slots[index];
            var wIdx = new List<int>();
            var uIdx = new List<int>();
            if (cfg != null)
            {
                if (cfg.Weapons != null)
                    foreach (var n in cfg.Weapons)
                    {
                        int k;
                        if (!string.IsNullOrEmpty(n) && _weaponIndex.TryGetValue(n, out k)) wIdx.Add(k);
                    }
                if (cfg.Utilities != null)
                    foreach (var n in cfg.Utilities)
                    {
                        int k;
                        if (!string.IsNullOrEmpty(n) && (_utilIndex.TryGetValue(n, out k) || (k = ResolveUtilAlias(n)) >= 0) && !uIdx.Contains(k)) uIdx.Add(k); // [Aemy 2026-10-10]
                    }
            }
            _loadout.ApplyWeapons(wIdx);
            _loadout.ApplyUtilities(uIdx);
            // [Aemy 2026-10-11] Chi so theo sung dang xem: LoadoutSkinPresenter nghe WeaponsApplied -> FocusWeapon -> ShowWeaponStats.
        }

        // Nguoi choi doi sung/utility -> cap nhat bo nho phien (chua luu len cloud).
        private void OnWeaponsChanged(IReadOnlyList<int> indices)
        {
            if (!_loaded) return;
            var slot = EnsureSlot(_currentSlot);
            slot.Weapons = NamesFromIndices(indices, true);
            slot.WeaponTypes = TypesFromIndices(indices);   // luu them loai sung
            // [Aemy 2026-10-11] chi so theo sung dang xem (LoadoutSkinPresenter -> FocusWeapon)
        }

        private void OnUtilsChanged(IReadOnlyList<int> indices)
        {
            if (!_loaded) return;
            EnsureSlot(_currentSlot).Utilities = NamesFromIndices(indices, false);
        }

        // Bam Save -> luu slot dang chon len PlayFab.
        private void OnSave(IReadOnlyList<int> weapons, IReadOnlyList<int> utils)
        {
            var cfg = EnsureSlot(_currentSlot);
            cfg.Weapons = NamesFromIndices(weapons, true);
            cfg.WeaponTypes = TypesFromIndices(weapons);   // luu them loai sung
            cfg.Utilities = NamesFromIndices(utils, false);
            UnityEngine.PlayerPrefs.SetInt("ActiveLoadoutSlot", _currentSlot); // [LOADOUT-FIX 2026-09-28 Aemy]
            WriteActiveLoadoutPrefs(cfg); // [LOADOUT-FIX 2026-09-28 Aemy]
            WriteAllSlotsPrefs(); // [LOADOUT 2026-09-28 Aemy] ghi ca 3 slot -> panel doi trang bi trong tran doc dung

            var svc = BackendBootstrap.LoadoutService;
            if (svc == null) { Debug.LogWarning("[LoadoutBinder] Chua co LoadoutService."); return; }

            _loadout.SetSaveInteractable(false);
            // [SAVE-ALL 2026-09-28 Aemy] 1 lan bam Luu -> luu TAT CA slot co du lieu.
            int pending = 0;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) pending++;
            if (pending == 0) { _loadout.SetSaveInteractable(true); return; }
            int done = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                int idx = i;
                svc.SaveLoadoutSlot(idx, _slots[idx],
                    () => { done++; if (done >= pending) { Debug.Log("[LoadoutBinder] Da luu " + pending + " slot."); _loadout.SetSaveInteractable(true); } },
                    err => { done++; Debug.LogWarning("[LoadoutBinder] Luu slot " + idx + " loi: " + err.ErrorMessage); if (done >= pending) _loadout.SetSaveInteractable(true); });
            }
        }

        // [LOADOUT-FIX 2026-09-28 Aemy] Ghi loadout dang chon vao PlayerPrefs (nguon dong bo, doc lai khi vao tran).
        private static void WriteActiveLoadoutPrefs(LoadoutConfig cfg)
        {
            string w0 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 0) ? (cfg.Weapons[0] ?? "") : "";
            string w1 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 1) ? (cfg.Weapons[1] ?? "") : "";
            string u0 = (cfg != null && cfg.Utilities != null && cfg.Utilities.Length > 0) ? (cfg.Utilities[0] ?? "") : "";
            string u1 = (cfg != null && cfg.Utilities != null && cfg.Utilities.Length > 1) ? (cfg.Utilities[1] ?? "") : "";
            UnityEngine.PlayerPrefs.SetString("LoadoutActiveW0", w0);
            UnityEngine.PlayerPrefs.SetString("LoadoutActiveW1", w1);
            UnityEngine.PlayerPrefs.SetString("LoadoutActiveU0", u0);
            UnityEngine.PlayerPrefs.SetString("LoadoutActiveU1", u1);
            UnityEngine.PlayerPrefs.Save();
            UnityEngine.Debug.Log("[LoadoutBinder] Prefs loadout: " + w0 + "/" + w1 + " | " + u0 + "/" + u1);
        }

        // [STATS 2026-09-28 Aemy] Hien chi so vu khi that (tu WeaponData qua GameManager) len panel info.
        private void ShowWeaponStats(int weaponIndex)
        {
            if (_loadout == null || weaponIndex < 0) return;
            string wname = _loadout.GetWeaponName(weaponIndex);
            string wtype = _loadout.GetWeaponType(weaponIndex);
            var info = new LoadoutController.WeaponCardInfo { weaponName = wname, weaponType = wtype, model = null };
            var gm = GameManager.Instance;
            var data = WeaponLookup.Resolve(wname); // [Aemy 2026-10-11] "Dao" -> Knife
            if (data != null)
            {
                // Chuan hoa 0..1 (hang so co the chinh cho khop cam giac game).
                info.damage = Mathf.Clamp01(data.Damage / 100f);
                float fd = Mathf.Max(0.01f, data.FireDelay);
                info.fireRate = Mathf.Clamp01((1f / fd) / 15f);
                int spread = data.SpreadAngle;
                info.accuracy = spread > 0 ? Mathf.Clamp01(1f - spread / 12f) : Mathf.Clamp01(data.Range / 200f);
            }
            _loadout.ShowWeaponInfo(info);
        }

        // [LOADOUT 2026-09-28 Aemy] Ghi vu khi cua CA 3 slot vao PlayerPrefs -> SelectLoadoutController (trong tran) doc.
        private void WriteAllSlotsPrefs()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var cfg = _slots[i];
                string w0 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 0) ? (cfg.Weapons[0] ?? "") : "";
                string w1 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 1) ? (cfg.Weapons[1] ?? "") : "";
                UnityEngine.PlayerPrefs.SetString("LoadoutSlot" + i + "W0", w0);
                UnityEngine.PlayerPrefs.SetString("LoadoutSlot" + i + "W1", w1);
            }
            UnityEngine.PlayerPrefs.Save();
        }

        // [Aemy 2026-10-10] Du lieu cu (luu theo ten tieng Viet / ten khac) -> doan theo tu khoa. -1 neu khong nhan ra.
        // Thu tu UI utility: [0] Frag (M67), [1] Flash (M84), [2] Smoke (AN-M8 HC). Kiem tra choang/khoi truoc vi deu chua "lựu đạn".
        private static int ResolveUtilAlias(string n)
        {
            var s = n.ToLowerInvariant();
            if (s.Contains("m84") || s.Contains("flash") || s.Contains("choáng")) return 1;
            if (s.Contains("smoke") || s.Contains("m8") || s.Contains("khói")) return 2;
            if (s.Contains("m67") || s.Contains("frag") || s.Contains("lựu đạn") || s == "ge") return 0;
            return -1;
        }

        private LoadoutConfig EnsureSlot(int i)
        {
            if (_slots[i] == null) _slots[i] = new LoadoutConfig();
            return _slots[i];
        }

        // Doi danh sach index -> mang ten[2] (LoadoutConfig dung mang co dinh 2 phan tu).
        private string[] NamesFromIndices(IReadOnlyList<int> indices, bool weapon)
        {
            var arr = new string[2];
            if (indices != null)
                for (int i = 0; i < indices.Count && i < 2; i++)
                    arr[i] = weapon ? _loadout.GetWeaponName(indices[i]) : _loadout.GetUtilityCodename(indices[i]); // [Aemy 2026-10-01] luu codename grenade
            return arr;
        }

        // Doi danh sach index sung -> mang loai[2] (song song voi ten).
        private string[] TypesFromIndices(IReadOnlyList<int> indices)
        {
            var arr = new string[2];
            if (indices != null)
                for (int i = 0; i < indices.Count && i < 2; i++)
                    arr[i] = _loadout.GetWeaponType(indices[i]);
            return arr;
        }

    

// ===== [Aemy 2026-10-04] Khoa sung theo SO HUU (mua GC o Shop) thay cho theo level =====
        private static readonly System.Collections.Generic.HashSet<string> _freeWeapons =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Knife", "Dao", "Vesper" }; // [Aemy 2026-10-11] o dao doi ten "Knife"
        private readonly System.Collections.Generic.Dictionary<string, int> _weaponPrice =
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _ownedWeapons =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // Nhan sung (Dao/Vesper/Umbra...) -> itemId shop (khop dong Nyx them trong Sheet tab Shop).
        private static string WeaponItemId(string label)
        {
            return string.IsNullOrEmpty(label) ? "" : label.Trim().ToLowerInvariant();
        }

        /// <summary>Khoa sung chua so huu (tru Dao/Vesper mien phi). Gia doc tu ShopCatalog, so huu tu GetOwnedItems.</summary>
        public void ApplyOwnershipLocks()
        {
            if (_loadout == null) return;
            Game.Shop.ShopCatalogService.LoadWithFallback("ShopCatalog", null, (items, fromServer) =>
            {
                _weaponPrice.Clear();
                if (items != null)
                    foreach (var it in items)
                        if (!string.IsNullOrEmpty(it.itemId)) _weaponPrice[it.itemId] = it.DisplayPrice;
                LoadOwnedThenApply();
            });
        }

        private void LoadOwnedThenApply()
        {
            var svc = BackendBootstrap.MenuDataService;
            if (svc == null) { ApplyLocksNow(); return; }
            svc.GetOwnedItems(
                ids =>
                {
                    _ownedWeapons.Clear();
                    if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _ownedWeapons.Add(id);
                    ApplyLocksNow();
                },
                err => { Debug.LogWarning("[LoadoutBinder] GetOwnedItems loi: " + (err != null ? err.ErrorMessage : "?")); ApplyLocksNow(); });
        }

        private void ApplyLocksNow()
        {
            if (_loadout == null) return;
            for (int i = 0; i < _loadout.WeaponCount; i++)
            {
                string label = _loadout.GetWeaponName(i);
                string id = WeaponItemId(label);
                bool free = !string.IsNullOrEmpty(label) && _freeWeapons.Contains(label);
                bool owned = free || _ownedWeapons.Contains(id);
                // [Aemy 2026-10-11] Khong hien gia o Trang bi nua (Nyx: muon mua thi vao Shop) -> o khoa ghi "MUA Ở CỬA HÀNG".
                _loadout.SetWeaponOwnership(i, owned, 0, false);
            }
        }

        // Bam vao sung dang khoa -> chuyen sang Shop, tab Sung (1).
        private void OnLockedWeaponClicked(int index)
        {
            MenuNavigation.OpenShopWeapons(this); // [Aemy 2026-10-11] qua TopBar -> highlight tab CUA HANG dung
        }
}
}