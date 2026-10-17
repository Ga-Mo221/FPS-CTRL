using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Backend.Bootstrap;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Cầu nối MenuShell &lt;-&gt; PlayFab (giống AuthFlowBinder cho luồng auth).
    /// Khi vào menu: lấy hồ sơ (tên/level/avatar) + coins từ PlayFab
    /// rồi đổ vào TopBar/PlayPanel. Nối chọn-mode và nút Start/Ready ở PlayPanel với
    /// luồng tìm trận. Các presentation controller không đụng backend; binder là lớp
    /// DUY NHẤT biết cả hai phía.
    /// </summary>
    public class MenuShellBinder : MonoBehaviour
    {
        // Thông tin 1 mode chơi, hiển thị ở PlayPanel khi được chọn.
        [Serializable]
        public struct ModeInfo
        {
            public string title;
            [TextArea] public string description;
            public MatchMode mode;   // FFA / TDM -> dùng khi bắt đầu tìm trận
        }

        [Header("Views (kéo thả trong MenuShell)")]
        [SerializeField] private TopBarController _topBar;
        [SerializeField] private PlayPanelController _play;
        [SerializeField] private WeaponStatsController _weaponStats;
        [SerializeField] private PartyPanelController _party;
        [SerializeField] private PartyController _partyController; // party session (ready-gate + queue chung)
        [Tooltip("Tùy chọn: Shop trong menu — mua xong tự refresh lại số dư coins.")]
        [SerializeField] private Game.Shop.ShopController _shop;

        [Header("Tìm trận")]
        [Tooltip("Tùy chọn: nếu gán driver màn hình tìm trận, nút Start sẽ giao cho nó " +
                 "(tái dùng logic gate/verified). Bỏ trống -> binder tự gọi MatchmakingService.")]
        [SerializeField] private MatchmakingScreenDriver _matchmakingDriver;
        // [UI-INTEGRATION 2026-09-13] Prefab man matchmaking - hien khi DA tim thay tran (bo trong = bo qua).
        [SerializeField] private MatchmakingController _matchmakingScreen;
        [Tooltip("[UI-INTEGRATION] Giay o lobby (hien roster) truoc khi vao tran.")]
        [SerializeField] private float _lobbyHoldSeconds = 5f;
        // [Aemy] Bat/tat solo-fallback: het gio tim tran khong du nguoi -> co vao game solo/warm-up hay khong.
        // TAT (mac dinh) de ep test ghep tran 2 client that. BAT de choi/test mot minh duoc.
        [Tooltip("Bật: hết giờ tìm trận mà không đủ người -> vào game solo/warm-up. Tắt: chỉ báo 'không tìm được trận'.")]
        [SerializeField] private bool _enableSoloFallback = false;
        private Coroutine _lobbyCo;
        private Coroutine _presenceCo; // [Aemy] heartbeat/poll presence
        private System.Collections.Generic.List<Backend.Models.FriendInfo> _lastFriends; // [Aemy] cache SocialPanel

        [Header("Cấu hình")]
        [Tooltip("Mã virtual currency dùng cho coins (khớp mã tạo trên PlayFab, vd GC / CR).")]
        [SerializeField] private string _currencyCode = "GC";

        [Tooltip("Tải ảnh avatar từ AvatarUrl của PlayFab về hiển thị.")]
        [SerializeField] private bool _loadAvatar = true;

        [Tooltip("Danh sách mode theo đúng thứ tự các nút chọn mode trong PlayPanel.")]
        [SerializeField] private ModeInfo[] _modes = new ModeInfo[]
        {
            new ModeInfo { title = "FREE FOR ALL", description = "Đấu đơn · Ai cũng là đối thủ · Đạt mốc hạ gục trước để thắng", mode = MatchMode.FFA },
            new ModeInfo { title = "TEAM DEATHMATCH", description = "2 đội · Đội đạt mốc điểm trước sẽ thắng", mode = MatchMode.TDM },
        };

        // [MAP 2026-09-28 Aemy]
        [System.Serializable]
        public struct MapInfo
        {
            public string displayName; public string sceneName; public bool available;
            // [Aemy 2026-10-10] anh + mo ta map (o chon map, vung VAO TRAN, man Matchmaking)
            public Sprite icon; public Sprite background;
            [TextArea(2, 4)] public string description;
        }
        [Tooltip("Map theo thu tu o BAN DO. available=false -> khoa 'coming soon'.")]
        [SerializeField] private MapInfo[] _maps = new MapInfo[]
        {
            new MapInfo { displayName = "MERIDIAN", sceneName = "ShortMap1", available = true },
            new MapInfo { displayName = "LongMap1", sceneName = "LongMap1", available = true },
            new MapInfo { displayName = "TERMINAL", sceneName = "", available = false },
            new MapInfo { displayName = "COMING SOON", sceneName = "", available = false },
        };

        private int _selectedModeIndex = 0;
        private bool _searching;

        private void Awake()
        {
            if (_play != null)
            {
                _play.ModeSelected += OnModeSelected;
                _play.ReadyClicked += OnStartClicked;
                _play.StartClicked += OnStartClicked;
                _play.MapSelected += OnMapSelected; // [MAP 2026-09-28 Aemy]
            }
            if (_party != null)
                _party.ReadyClicked += OnPartyReady;
            if (_partyController != null) _partyController.OnPartyMatchReady += OnPartyMatchReady; // [PARTY-FIX] qua lobby
            if (_shop != null)
                _shop.PurchaseConfirmed += OnPurchaseConfirmed;
            Backend.Bootstrap.ProfileCache.Changed += OnProfileChanged; // doi avatar/ten -> refresh top bar
            // [Aemy 2026-10-17] Doi trang bi / skin la cap nhat 3 balo vao GameManager NGAY (khong doi bam LUU / Vao tran).
            Game.UI.LoadoutDraftStore.Changed += OnLoadoutDraftChanged;
            Game.Skins.WeaponSkinService.Changed += OnSkinsChanged;
        }

        private void OnDestroy()
        {
            if (_play != null)
            {
                _play.ModeSelected -= OnModeSelected;
                _play.ReadyClicked -= OnStartClicked;
                _play.StartClicked -= OnStartClicked;
                _play.MapSelected -= OnMapSelected;
            }
            if (_party != null)
                _party.ReadyClicked -= OnPartyReady;
            if (_partyController != null) _partyController.OnPartyMatchReady -= OnPartyMatchReady;
            if (_shop != null)
                _shop.PurchaseConfirmed -= OnPurchaseConfirmed;
            Backend.Bootstrap.ProfileCache.Changed -= OnProfileChanged;
            Game.UI.LoadoutDraftStore.Changed -= OnLoadoutDraftChanged;
            Game.Skins.WeaponSkinService.Changed -= OnSkinsChanged;
        }

        // [Aemy 2026-10-17] Bo 3 slot dung lan cuoi -> dung lai khi skin doi (truoc day skin tai tu server XONG SAU khi
        // da dung balo => vao tran mat skin; doi skin sau khi vao menu cung khong ap dung).
        private LoadoutConfig[] _lastLoadoutArr;
        private void OnLoadoutDraftChanged()
        {
            LoadoutConfig[] d;
            if (!Game.UI.LoadoutDraftStore.TryGet(out d)) return;
            _lastLoadoutArr = d;
            ApplyLoadoutToGame(d);
        }
        private void OnSkinsChanged()
        {
            if (_lastLoadoutArr != null) ApplyLoadoutToGame(_lastLoadoutArr);
            else RefreshLoadout();
        }

        // [Aemy 2026-09-28] Ho so doi (avatar/ten) -> nap lai top bar ngay, khong can vao lai menu.
        private void OnProfileChanged() { RefreshProfile(); }

        private void Start()
        {
            // [CURSOR 2026-09-28 Aemy] Ve menu -> luon hien chuot (tranh mat con tro sau tran).
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            RefreshProfile();
            RefreshCoins();
            RefreshWeapons();
            RefreshStats(); // [Aemy 2026-09-29 #2]
            Game.Skins.WeaponSkinService.LoadFromServer(); // [Aemy 2026-10-10] skin dang trang bi (dong bo giua cac may)
            RefreshFriends();
            StartPresence(); // [Aemy]
            // [Aemy 2026-09-24] Auto dang ky ten vao chi muc ngay khi vao menu -> player LUON tim duoc nhau
            // theo ten ma KHONG can mo panel ban be truoc.
            var _fsvc = BackendBootstrap.FriendsService;
            if (_fsvc != null) _fsvc.IndexMyName();
            // [Aemy 2026-09-24] Load chi so sung tu Title Data + overlay len WeaponData (SO dung chung)
            // -> vao tran la da co so moi, KHONG can build lai.
            Backend.PlayfabImpl.WeaponSpecService.LoadAndApply();
            RefreshLoadout(); // [UI-INTEGRATION] cache loadout slot active -> GameManager (cum B)
            ApplyModeInfo(_selectedModeIndex);
            ConfigureMaps(); // [MAP 2026-09-28 Aemy]
            WirePartyLeaveButton(); // [PARTY-FIX #69] nut Roi to doi
        }

        // ---------------- TopBar: hồ sơ + coins ----------------

        // ↓ Lấy tên hiển thị + level (+ avatar) từ PlayFab đổ vào TopBar.
        public void RefreshProfile()
        {
            ProfileCache.Get(profile =>
            {
                var displayName = string.IsNullOrEmpty(profile.DisplayName) ? "PLAYER" : profile.DisplayName;
                if (_topBar != null)
                {
                    _topBar.SetPlayerName(displayName);
                    _topBar.SetLevel(profile.Level);
                }
                if (_loadAvatar && !string.IsNullOrEmpty(profile.AvatarUrl))
                    StartCoroutine(DownloadAvatar(profile.AvatarUrl));

                if (_party != null)
                {
                    for (int i = 1; i < _party.SlotCount; i++) _party.SetEmpty(i);
                    _party.SetMember(0, displayName, true, "Sẵn sàng");
                    if (_loadAvatar && !string.IsNullOrEmpty(profile.AvatarUrl))
                        StartCoroutine(DownloadSprite(profile.AvatarUrl, s => _party.SetMemberAvatar(0, s)));
                }
            },
            err => Debug.LogWarning("[MenuShellBinder] GetMyProfile lỗi: " + err.ErrorMessage));
        }

        // ↓ Lấy số dư virtual currency (theo _currencyCode) đổ vào TopBar.
        public void RefreshCoins()
        {
            var svc = BackendBootstrap.MenuDataService;
            if (svc == null) return;

            svc.GetVirtualCurrency(balances =>
            {
                int coins = 0;
                if (balances != null && !string.IsNullOrEmpty(_currencyCode))
                    balances.TryGetValue(_currencyCode, out coins);
                if (_topBar != null) _topBar.SetCoins(coins);
            },
            err => Debug.LogWarning("[MenuShellBinder] Coins lỗi: " + err.ErrorMessage));
        }

        // ↓ Lấy top súng nhiều kill nhất từ PlayFab đổ vào panel "SÚNG CHỦ LỰC".
        public void RefreshWeapons()
        {
            if (_weaponStats == null) return;
            var svc = BackendBootstrap.PlayerStatsService;
            if (svc == null) return;
            int n = _weaponStats.SlotCount;
            svc.GetTopWeapons(n, list =>
            {
                int count = list != null ? list.Count : 0;
                for (int i = 0; i < count && i < n; i++) _weaponStats.SetWeapon(i, list[i].weapon, list[i].kills);
                _weaponStats.HideFrom(count);
            },
            err => { if (_weaponStats != null) _weaponStats.HideFrom(0); Debug.LogWarning("[MenuShellBinder] Weapons lỗi: " + err.ErrorMessage); });
        }

        // [Aemy 2026-09-29 #2] Do THONG KE (K/D, Kills/tran, Headshot%, Chuoi cao nhat) vao panel o menu.
        public void RefreshStats()
        {
            var svc = BackendBootstrap.PlayerStatsService;
            if (svc == null) return;
            svc.GetMyStats(cs =>
            {
                if (cs == null) return;
                float kpm = cs.Matches > 0 ? (float)cs.Kills / cs.Matches : 0f;
                // [Aemy 2026-10-11] Hien TI LE KDA = (Kill + Assist) / Death (chua chet lan nao -> chia 1), khong hien tong tung so nua.
                float kda = (cs.Kills + cs.Assist) / (float)Mathf.Max(1, cs.Deaths);
                SetStatValue("K / D", kda.ToString("0.00"));
                SetStatValue("Kills/trận TB", kpm.ToString("0.0"));
                SetStatValue("Headshot %", cs.HeadshotPercent + "%");
                SetStatValue("Chuỗi cao nhất", "—"); // chua co nguon best-streak tren PlayFab
            },
            err => Debug.LogWarning("[MenuShellBinder] Stats lỗi: " + err.ErrorMessage));
        }

        // Tim label trong panel THONG KE theo ten -> set gia tri vao child \"Thông số\".
        private static void SetStatValue(string labelName, string value)
        {
            var all = GameObject.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t == null || t.gameObject.name != labelName) continue;
                var par = t.transform.parent;
                if (par == null || par.name != "THỐNG KÊ") continue;
                for (int i = 0; i < t.transform.childCount; i++)
                {
                    var c = t.transform.GetChild(i).GetComponent<TMPro.TextMeshProUGUI>();
                    if (c != null) { c.text = value; c.enableWordWrapping = false; c.overflowMode = TMPro.TextOverflowModes.Overflow; break; } // [KDA 2026-10-04] NoWrap
                }
                break;
            }
        }

        // ↓ Lấy danh sách bạn bè từ PlayFab -> đổ số online + avatar vào panel "PHÒNG CỦA BẠN".
        public void RefreshFriends()
        {
            if (_party == null) return;
            var svc = BackendBootstrap.FriendsService;
            if (svc == null) return;

            svc.GetFriendsWithPresence(list =>
            {
                _lastFriends = list; // [Aemy]
                int count = list != null ? list.Count : 0;
                int online = 0; if (list != null) foreach (var fr in list) if (fr.Presence == FriendPresence.Online) online++;
                _party.SetFriendsCount(online); // [Aemy] hien SO ONLINE

                int slots = _party.FriendAvatarCount;
                int shown = Mathf.Min(count, slots);
                for (int i = 0; i < shown; i++)
                {
                    int idx = i;
                    var url = list[i].AvatarUrl;
                    if (_loadAvatar && !string.IsNullOrEmpty(url))
                        StartCoroutine(DownloadSprite(url, s => _party.SetFriendAvatar(idx, s)));
                    else
                        _party.SetFriendAvatar(idx, null);
                }
                _party.HideFriendAvatarsFrom(shown);
            },
            err => Debug.LogWarning("[MenuShellBinder] Friends lỗi: " + err.ErrorMessage));
        }

        // [Aemy 2026-09-21] Heartbeat online + poll presence dinh ky.
        private void StartPresence()
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc != null) svc.Heartbeat();
            if (_presenceCo == null) _presenceCo = StartCoroutine(PresenceLoop());
        }

        private IEnumerator PresenceLoop()
        {
            var wait = new WaitForSeconds(15f);
            while (isActiveAndEnabled)
            {
                yield return wait;
                var svc = BackendBootstrap.FriendsService;
                if (svc != null) svc.Heartbeat();
                RefreshFriends();
            }
            _presenceCo = null;
        }

        /// <summary>[Aemy] Danh sach ban moi nhat (co presence) cho SocialPanel.</summary>
        public System.Collections.Generic.List<Backend.Models.FriendInfo> LatestFriends { get { return _lastFriends; } }

        // [UI-INTEGRATION 2026-09-13] Nap loadout slot dang chon tu PlayFab -> cache vao GameManager (NPlayer khai len host khi vao tran).
        public void RefreshLoadout()
        {
            var svc = BackendBootstrap.LoadoutService;
            if (svc == null || GameManager.Instance == null) return;
            svc.LoadAllLoadouts(arr0 =>
            {
                // [Aemy 2026-10-17] Con ban nhap chua dong bo -> ban nhap moi la lua chon moi nhat cua nguoi choi.
                LoadoutConfig[] _draft;
                var arr = Game.UI.LoadoutDraftStore.TryGet(out _draft) ? _draft : arr0;
                if (arr != null) _lastLoadoutArr = arr;
                ApplyLoadoutToGame(arr);
            },
            e => Debug.LogWarning("[MenuShellBinder] LoadLoadout loi: " + e.ErrorMessage));
        }

        /// <summary>[Aemy 2026-10-17] Dung 3 balo (ALPHA/BRAVO/CHARLIE) + skin dang trang bi -> GameManager.</summary>
        private void ApplyLoadoutToGame(LoadoutConfig[] arr)
        {
                if (arr == null || GameManager.Instance == null) return;
                var _gmi = GameManager.Instance;
                var _sd  = _gmi.GetSetupData();

                // [LOADOUT-3BALO 2026-10-11 Aemy] Map CO DINH: ALPHA -> Balo 1, BRAVO -> Balo 2, CHARLIE -> Balo 3
                // (truoc day slot "dang chon" bi day len Balo 1 -> thu tu trong tran bi xoay). Vao tran luon cam Balo 1 (Nyx chot).
                // Utility chuan hoa ve codename (GetGrenade khop Contains). Slot/utility trong -> fallback mac dinh.
                BaloData _def = new BaloData(WeaponID._9MM_DEMON, WeaponID._9MM_DEMON, WeaponID._FRAG_M67, WeaponID._FLASH_M84);

                System.Func<string, string> _normUtil = raw =>
                {
                    if (string.IsNullOrEmpty(raw)) return null;
                    string s = raw.ToLowerInvariant();
                    if (s.Contains("m84") || s.Contains("flash")) return WeaponID._FLASH_M84;
                    if (s.Contains("m67") || s.Contains("frag"))  return WeaponID._FRAG_M67;
                    if (s.Contains("smoke") || s.Contains("an-m8") || s.Contains("m8")) return WeaponID._SMOKE_AN_M8_HC;
                    return null; // ten hien thi cu -> dung fallback
                };

                System.Func<LoadoutConfig, BaloData, BaloData> _buildBalo = (cfg, fb) =>
                {
                    string w0 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 0 && !string.IsNullOrEmpty(cfg.Weapons[0])) ? cfg.Weapons[0] : fb.Weapon_One;
                    string w1 = (cfg != null && cfg.Weapons != null && cfg.Weapons.Length > 1 && !string.IsNullOrEmpty(cfg.Weapons[1])) ? cfg.Weapons[1] : fb.Weapon_Two;
                    // [Aemy 2026-10-11] Ten menu -> ID trong tran ("Dao" -> "Knife"); truoc day dao khong spawn + o balo trong.
                    w0 = Game.UI.WeaponLookup.ToGameId(w0); w1 = Game.UI.WeaponLookup.ToGameId(w1);
                    string u0raw = (cfg != null && cfg.Utilities != null && cfg.Utilities.Length > 0) ? cfg.Utilities[0] : null;
                    string u1raw = (cfg != null && cfg.Utilities != null && cfg.Utilities.Length > 1) ? cfg.Utilities[1] : null;
                    string u0 = _normUtil(u0raw); if (string.IsNullOrEmpty(u0)) u0 = fb.Grenade_Q;
                    string u1 = _normUtil(u1raw); if (string.IsNullOrEmpty(u1)) u1 = fb.Grenade_E;
                    // [Aemy 2026-10-10] Skin dang trang bi -> spawn theo ID skin (vd Umbra -> M4_DR). Spawn qua mang nen ai cung thay.
                    w0 = Game.Skins.WeaponSkinService.Resolve(w0); w1 = Game.Skins.WeaponSkinService.Resolve(w1);
                    return new BaloData(w0, w1, u0, u1);
                };

                BaloData _b1 = _buildBalo(arr.Length > 0 ? arr[0] : null, _def);   // ALPHA
                BaloData[] _others = new BaloData[2];
                _others[0] = _buildBalo(arr.Length > 1 ? arr[1] : null, _def);    // BRAVO
                _others[1] = _buildBalo(arr.Length > 2 ? arr[2] : null, _def);    // CHARLIE

                string _selChar = UnityEngine.PlayerPrefs.GetString("SelectedCharacter", _sd.CharacterName); if (string.IsNullOrEmpty(_selChar)) _selChar = _sd.CharacterName; // [Aemy 2026-10-05] nhan vat da chon
                _gmi.SetSetupData(new GamePlaySetupData(_selChar, _b1, _others[0], _others[1]));
                _gmi.setCurrentBalo(_b1);
                _gmi.SetLoadout(_b1.Weapon_One, _b1.Weapon_Two, _b1.Grenade_Q, _b1.Grenade_E);
                Debug.Log("[MenuShellBinder] Loadout 3 balo (Alpha/Bravo/Charlie): B1=" + _b1.Weapon_One + "/" + _b1.Weapon_Two + "|" + _b1.Grenade_Q + "/" + _b1.Grenade_E + " B2=" + _others[0].Weapon_One + "/" + _others[0].Weapon_Two + " B3=" + _others[1].Weapon_One + "/" + _others[1].Weapon_Two);
        }

        // Mua xong o Shop -> cap nhat lai so du coins tren TopBar.
        private void OnPurchaseConfirmed(Game.Shop.ShopItemInfo d) { RefreshCoins(); }
        /// <summary>[Aemy 2026-10-10] Cho man khac (vd mo khoa skin o Trang bi) cap nhat so GC tren TopBar.</summary>
        public void RequestRefreshCoins() { RefreshCoins(); }

        // Bấm "SẴN SÀNG" trong party -> đánh dấu sẵn sàng (Fusion sẽ đồng bộ về sau).
        private void OnPartyReady()
        {
            // [PARTY-FIX] Trong party: chi MEMBER toggle ready (leader luon san sang). Ngoai party: tim tran solo.
            if (_partyController != null && _partyController.InParty)
            {
                _partyController.PendingMode = CurrentMode();
                if (_partyController.IsLeader) return; // leader luon ready, khong toggle
                _partyController.ToggleReady();
            }
            else
            {
                OnStartClicked();
            }
        }

        // Tải 1 ảnh từ URL -> Sprite -> trả về qua callback (dùng cho avatar thành viên/bạn bè).
        private IEnumerator DownloadSprite(string url, Action<Sprite> onDone)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[MenuShellBinder] Tải ảnh lỗi: " + req.error);
                    yield break;
                }
                var tex = DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 64); // [#5] thu nho 64px
                if (tex != null && onDone != null)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    onDone(sprite);
                }
            }
        }



        // ---------------- PlayPanel: mode + tìm trận ----------------



        private void OnModeSelected(int index)
        {
            _selectedModeIndex = index;
            ApplyModeInfo(index);
            if (_partyController != null) _partyController.PendingMode = CurrentMode(); // [Aemy]
        }

        // Cập nhật tiêu đề + mô tả mode đang chọn lên PlayPanel.
        private void ApplyModeInfo(int index)
        {
            if (_play == null || _modes == null || _modes.Length == 0) return;
            int i = Mathf.Clamp(index, 0, _modes.Length - 1);
            _play.SetModeDescription(_modes[i].title, _modes[i].description);
        }

        // [MAP 2026-09-28 Aemy] Khoa o map chua co (coming soon) + chon map dau tien co san.
        private void ConfigureMaps()
        {
            if (_play == null || _maps == null) return;
            int n = _play.MapCount;
            int firstAvail = -1;
            for (int i = 0; i < n; i++)
            {
                bool avail = (i < _maps.Length) && _maps[i].available;
                if (i < _maps.Length) _play.SetMapName(i, _maps[i].displayName); // [MAP-SYNC 2026-10-01]
                if (i < _maps.Length) _play.SetMapIcon(i, _maps[i].icon); // [Aemy 2026-10-10]
                _play.SetMapLocked(i, !avail);
                if (avail && firstAvail < 0) firstAvail = i;
            }
            if (firstAvail >= 0)
            {
                _play.SelectMap(firstAvail, false);
                if (GameManager.Instance != null && firstAvail < _maps.Length) GameManager.Instance.SetSelectedMapScene(_maps[firstAvail].sceneName);
                ApplySelectedMap(firstAvail); // [Aemy 2026-10-10]
            }
        }

        private void OnMapSelected(int index)
        {
            if (_maps == null || index < 0 || index >= _maps.Length || !_maps[index].available) return;
            if (GameManager.Instance != null) GameManager.Instance.SetSelectedMapScene(_maps[index].sceneName);
            ApplySelectedMap(index); // [Aemy 2026-10-10]
        }

        // [Aemy 2026-10-10] Map dang chon (cho vung VAO TRAN + man Matchmaking).
        private int _selectedMapIndex = 0;
        public bool TryGetSelectedMap(out MapInfo info)
        {
            info = default(MapInfo);
            if (_maps == null || _selectedMapIndex < 0 || _selectedMapIndex >= _maps.Length) return false;
            info = _maps[_selectedMapIndex]; return true;
        }
        private void ApplySelectedMap(int index)
        {
            if (_maps == null || index < 0 || index >= _maps.Length) return;
            _selectedMapIndex = index;
            if (_play != null) _play.SetSelectedMapArt(_maps[index].background, _maps[index].displayName);
            if (_matchmakingScreen != null) _matchmakingScreen.SetMap(_maps[index].displayName, _maps[index].description, _maps[index].background);
        }

        private MatchMode CurrentMode()
        {
            if (_modes != null && _modes.Length > 0)
                return _modes[Mathf.Clamp(_selectedModeIndex, 0, _modes.Length - 1)].mode;
            return MatchMode.FFA;
        }

        // Nhấn Ready/Start -> bắt đầu tìm trận theo mode đang chọn.
        // [PARTY-FIX #69] Nut RỜI to doi (object trong scene: Overlay 2/bt_roi_party).
        private void WirePartyLeaveButton()
        {
            if (_party == null) return;
            var t = _party.transform.Find("bt_roi_party");
            if (t == null) return;
            var b = t.GetComponent<UnityEngine.UI.Button>();
            if (b == null) return;
            b.onClick.RemoveListener(OnLeavePartyClicked);
            b.onClick.AddListener(OnLeavePartyClicked);
        }
        private void OnLeavePartyClicked()
        {
            if (_partyController != null) _partyController.LeaveParty();
            if (_party != null) { for (int i = 1; i < _party.SlotCount; i++) _party.SetEmpty(i); }
            RefreshProfile();
        }

        // [PARTY-FIX] Party bat dau -> show lobby matchmaking (roster tu party) roi handoff.
        private void OnPartyMatchReady(string session, MatchMode mode) { EnterPartyLobby(session, mode); }
        private void EnterPartyLobby(string session, MatchMode mode)
        {
            var match = new MatchFoundResult { MatchId = session, Mode = mode, QueueName = "" };
            if (_matchmakingScreen == null) { if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOff(match); return; }
            bool team = mode == MatchMode.TDM;
            OpenLobbyScreen(team, "TỔ ĐỘI ĐÃ SẴN SÀNG");
            var list = new List<LobbyMember>();
            var st = _partyController != null ? _partyController.State : null;
            string selfId = BackendBootstrap.AuthService != null ? BackendBootstrap.AuthService.CurrentUserId : null;
            if (st != null && st.Members != null)
                foreach (var m in st.Members)
                    list.Add(new LobbyMember { IsLocal = (m.PlayFabId == selfId), TeamId = "", Name = m.Name, AvatarUrl = m.AvatarUrl });
            FillLobbyRoster(list, team);
            bool isLeader = _partyController != null && _partyController.IsLeader;
            Debug.Log("[Party-DIAG] HANDOFF session=" + session + " isLeader=" + isLeader);
            StartLobbyHold(() =>
            {
                if (BackendBootstrap.MatchHandoff == null) return;
                if (isLeader) BackendBootstrap.MatchHandoff.HandOff(match); // leader vao truoc -> lam host
                else StartCoroutine(DelayedHandoff(match, 3f)); // member vao SAU ~3s -> join phong host (tranh 2 host)
            });
        }

        private IEnumerator DelayedHandoff(MatchFoundResult match, float delay)
        {
            float t = 0f; while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOff(match);
        }

        private void OnStartClicked()
        {
            var mode = CurrentMode();
            // [PARTY-FIX] Trong party: chi leader bat dau, va phai TAT CA san sang.
            if (_partyController != null && _partyController.InParty)
            {
                if (!_partyController.IsLeader)
                { _partyController.ToggleReady(); if (_play != null) _play.SetQueueStatus(_partyController.SelfReady ? "Đã sẵn sàng" : "Chưa sẵn sàng"); return; } // [PARTY-FIX] member bam = toggle ready, khong popup
                var pst = _partyController.State;
                if (pst == null || !pst.AllReady())
                { if (ModalDialog.Instance != null) ModalDialog.Instance.Info("Chưa sẵn sàng", "Vẫn còn thành viên chưa sẵn sàng."); return; }
                _partyController.PendingMode = mode;
                _partyController.StartPartyMatch(mode);
                return;
            }
            RefreshLoadout(); // [UI-INTEGRATION] nap loadout MOI NHAT tu PlayFab ngay khi bam Vao tran

            // Có driver màn hình tìm trận -> giao cho nó (tái dùng gate/verified + view).
            if (_matchmakingDriver != null) { _matchmakingDriver.Begin(mode); return; }

            // Không có -> tự gọi matchmaking, cập nhật trạng thái ngay trên PlayPanel.
            if (_searching) return;
            var mm = BackendBootstrap.MatchmakingService;
            if (mm == null) { Debug.LogWarning("[MenuShellBinder] Chưa có MatchmakingService."); return; }

            _searching = true;
            StartSearchTimer(); // [UI-INTEGRATION] nut Start bien thanh dong ho mm:ss
            if (_play != null) { _play.SetQueueStatus("Đang tìm trận..."); _play.SetStartInteractable(false); }

            mm.StartMatchmaking(
                new MatchmakingRequest { Mode = mode },
                onMatchFound: m =>
                {
                    _searching = false;
                    if (_play != null) _play.SetQueueStatus("Đã tìm thấy trận!");
                    StopSearchTimer();
                    EnterLobby(m, mode); // [UI-INTEGRATION] lobby roster THAT (PlayFab GetMatch) roi vao tran
                },
                onProgress: _ => { if (_play != null) _play.SetQueueStatus("Đang tìm trận..."); },
                onError: e =>
                {
                    _searching = false;
                    StopSearchTimer();
                    if (_play != null) { _play.SetQueueStatus("Lỗi: " + e.ErrorCode); _play.SetStartInteractable(true); }
                },
                onTimedOut: () =>
                {
                    _searching = false;
                    if (_play != null) _play.SetQueueStatus("Không tìm được trận (hết giờ). Thử lại?");
                    StopSearchTimer();
                    // [Aemy 2026-09-24] Solo-fallback gio dieu khien bang _enableSoloFallback (Inspector).
                    // Bat -> het gio tim tran ma khong du nguoi thi vao game solo/warm-up.
                    if (_enableSoloFallback) EnterLobbySolo(mode);
                    else if (_play != null) _play.SetStartInteractable(true);
                },
                onCancelled: () =>
                {
                    _searching = false;
                    StopSearchTimer();
                    if (_play != null) { _play.SetQueueStatus("Đã hủy tìm trận."); _play.SetStartInteractable(true); }
                });
        }

        // Tải ảnh avatar từ URL -> Sprite -> đưa vào TopBar.
        // [UI-INTEGRATION 2026-09-13] Dong ho mm:ss hien ngay tren nut Start trong luc tim tran.
        private Coroutine _searchTimerCo;
        private void StartSearchTimer()
        {
            StopSearchTimer();
            if (_play != null && isActiveAndEnabled) _searchTimerCo = StartCoroutine(SearchTimerCo());
        }
        private void StopSearchTimer()
        {
            if (_searchTimerCo != null) { StopCoroutine(_searchTimerCo); _searchTimerCo = null; }
            if (_play != null) _play.RestoreStartLabel();
        }
        private IEnumerator SearchTimerCo()
        {
            float startT = Time.unscaledTime;
            while (true)
            {
                int total = Mathf.FloorToInt(Time.unscaledTime - startT);
                if (_play != null) _play.SetStartLabel(string.Format("{0:00}:{1:00}", total / 60, total % 60));
                yield return new WaitForSeconds(1f);
            }
        }
        // [UI-INTEGRATION 2026-09-13] LOBBY: hien roster nguoi choi THAT (lay tu PlayFab GetMatch) roi moi vao tran.
        private void EnterLobby(MatchFoundResult match, MatchMode mode)
        {
            if (_matchmakingScreen == null) { HandOffNow(match, mode); return; }
            bool team = mode == MatchMode.TDM;
            OpenLobbyScreen(team, "ĐÃ TÌM THẤY TRẬN");
            var mm = BackendBootstrap.MatchmakingService;
            if (mm != null && !string.IsNullOrEmpty(match.MatchId))
                mm.GetMatchMembers(match.MatchId, match.QueueName,
                    members => FillLobbyRoster(members, team),
                    e => Debug.LogWarning("[MenuShellBinder] GetMatch loi: " + e.ErrorMessage));
            StartLobbyHold(() => { if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOff(match); });
        }

        private void EnterLobbySolo(MatchMode mode)
        {
            if (_matchmakingScreen == null) { if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOffSolo(mode); return; }
            bool team = mode == MatchMode.TDM;
            OpenLobbyScreen(team, "VÀO TRẬN");
            var me = new LobbyMember { IsLocal = true, TeamId = "", Name = "Player", AvatarUrl = "" };
            var prof = ProfileCache.Cached;
            if (prof != null) { if (!string.IsNullOrEmpty(prof.DisplayName)) me.Name = prof.DisplayName; if (!string.IsNullOrEmpty(prof.AvatarUrl)) me.AvatarUrl = prof.AvatarUrl; }
            FillLobbyRoster(new List<LobbyMember> { me }, team);
            StartLobbyHold(() => { if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOffSolo(mode); });
        }

        private void HandOffNow(MatchFoundResult match, MatchMode mode)
        {
            if (BackendBootstrap.MatchHandoff != null) BackendBootstrap.MatchHandoff.HandOff(match);
        }

        private void OpenLobbyScreen(bool team, string title)
        {
            _matchmakingScreen.gameObject.SetActive(true);
            _matchmakingScreen.StopTimer();
            _matchmakingScreen.SetTitle(title);
            _matchmakingScreen.SetMode(team ? "TEAM DEATHMATCH" : "FREE FOR ALL");
        }

        private void StartLobbyHold(Action onDone)
        {
            if (_lobbyCo != null) StopCoroutine(_lobbyCo);
            _lobbyCo = StartCoroutine(LobbyHoldCo(onDone));
        }

        private IEnumerator LobbyHoldCo(Action onDone)
        {
            float t = _lobbyHoldSeconds;
            while (t > 0f)
            {
                int total = Mathf.CeilToInt(t);
                if (_matchmakingScreen != null) _matchmakingScreen.SetTimeText(string.Format("{0:00}:{1:00}", total / 60, total % 60));
                yield return new WaitForSeconds(0.25f);
                t -= 0.25f;
            }
            if (_matchmakingScreen != null) _matchmakingScreen.SetTimeText("00:00");
            if (onDone != null) onDone();
        }

        // Do roster vao man hinh matchmaking. Team: chia 2 doi theo TeamId; FFA: 1 danh sach lien, bo ten doi.
        // [Aemy 2026-09-17] Roster hop nhat (da bo chia 2 doi). Local len dau (slot on dinh),
        // giu nguyen thu tu nguoi khac (khong 'day' player), o trong de TRANG.
        private void FillLobbyRoster(List<LobbyMember> members, bool team)
        {
            if (_matchmakingScreen == null || members == null) return;
            const int SZ = 5;

            // Local truoc, con lai giu thu tu goc.
            var ordered = new List<LobbyMember>();
            foreach (var m in members) if (m != null && m.IsLocal) ordered.Add(m);
            foreach (var m in members) if (m != null && !m.IsLocal) ordered.Add(m);

            int fill = 0;
            foreach (var mem in ordered)
            {
                if (fill >= SZ * 2) break;
                string st = mem.IsLocal ? "BẠN" : "";
                if (fill < SZ) { _matchmakingScreen.SetTeam1Member(fill, mem.Name, null, st); LoadAvatarInto(mem, 1, fill); }
                else { _matchmakingScreen.SetTeam2Member(fill - SZ, mem.Name, null, st); LoadAvatarInto(mem, 2, fill - SZ); }
                fill++;
            }
            // O trong: de TRANG (giong o roster trong), khong 'Dang cho...'.
            for (int i = (fill < SZ ? fill : SZ); i < SZ; i++) _matchmakingScreen.SetTeam1Member(i, "", null, "");
            int rs = fill - SZ; if (rs < 0) rs = 0;
            for (int i = rs; i < SZ; i++) _matchmakingScreen.SetTeam2Member(i, "", null, "");
            _matchmakingScreen.SetTeamHeaders("NGƯỜI CHƠI (" + fill + ")", "");
        }

        private void LoadAvatarInto(LobbyMember mem, int teamCol, int index)
        {
            if (mem == null || string.IsNullOrEmpty(mem.AvatarUrl)) return;
            StartCoroutine(DownloadAvatarInto(mem, teamCol, index));
        }

        private IEnumerator DownloadAvatarInto(LobbyMember mem, int teamCol, int index)
        {
            using (var req = UnityWebRequestTexture.GetTexture(mem.AvatarUrl))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var tex = DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 64); // [#5] thu nho 64px
                if (tex == null || _matchmakingScreen == null) yield break;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                string status = mem.IsLocal ? "BẠN" : "";
                if (teamCol == 1) _matchmakingScreen.SetTeam1Member(index, mem.Name, sp, status);
                else _matchmakingScreen.SetTeam2Member(index, mem.Name, sp, status);
            }
        }

        private IEnumerator DownloadAvatar(string url)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok)
                {
                    Debug.LogWarning("[MenuShellBinder] Tải avatar lỗi: " + req.error);
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 64); // [#5] thu nho 64px
                if (tex != null && _topBar != null)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    _topBar.SetAvatar(sprite);
                }
            }
        }
    }
}