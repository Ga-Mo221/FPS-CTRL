// [Aemy 2026-10-24] FILE MOI. Phong cho trong tran: mang man ghep tran (MatchmakingController) cua menu vao map.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Vao map ngay khi ghep duoc tran (phong Fusion mo -> nguoi dang tim tran vao them duoc). Trong luc
    /// NetworkGameState.InLobby (mac dinh 15s, het cho hoac phong day thi thoi), phu len man ghep tran giong het menu:
    /// danh sach nguoi trong phong cap nhat lien tuc (khong hien bo dem). Het phong cho -> an, de dem nguoc 5..1 + voice
    /// "three / two / one / fight" cua game chay nhu cu (khong dung toi bo dem / am thanh do).
    ///
    /// Cach lam: MenuShellBinder dang ky man ghep tran cua menu (SetSource). Ngay truoc khi vao tran,
    /// FusionMatchHandoff goi Capture() -> nhan ban man do (dang hien dung map / che do / roster) sang 1 canvas
    /// DontDestroyOnLoad. Khong can prefab moi hay sua scene.
    /// Tu huy khi: het phong cho / tran da bat dau (vao giua tran), vao tran that bai (Release), quay lai menu.
    /// </summary>
    public static class MatchLobbyOverlay
    {
        private static MatchmakingController _source;
        private static string _menuScene;
        private static Runner _runner;

        /// <summary>[2026-10-27] Man phong cho dang phu man hinh -> LocalInputController khong mo Pause (ESC / Start).</summary>
        public static bool IsShowing => _runner != null;

        // [Aemy 2026-10-27] Thong tin map theo ten scene (menu dang ky) -> vao map roi hien DUNG map dang choi.
        private struct MapView { public string Name, Description; public Sprite Background; }
        private static readonly Dictionary<string, MapView> _maps = new Dictionary<string, MapView>(System.StringComparer.OrdinalIgnoreCase);

        // Editor tat domain reload -> reset moi lan Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _source = null; _menuScene = null; _runner = null; _maps.Clear(); }

        /// <summary>Menu dang ky ten / mo ta / anh cua map ung voi scene.</summary>
        public static void RegisterMap(string sceneName, string displayName, string description, Sprite background)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            _maps[sceneName] = new MapView { Name = displayName, Description = description, Background = background };
        }

        /// <summary>MenuShellBinder goi khi vao menu: man ghep tran dung lam mau.</summary>
        public static void SetSource(MatchmakingController source)
        {
            _source = source;
            if (source != null) _menuScene = source.gameObject.scene.name;
        }

        /// <summary>Ngay truoc khi vao tran: chup man ghep tran dang hien sang canvas song qua doi scene.</summary>
        public static void Capture()
        {
            Release();
            if (_source == null) return;

            var host = new GameObject("[MatchLobbyOverlay]");
            Object.DontDestroyOnLoad(host);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrderAboveFader();
            var scaler = host.AddComponent<CanvasScaler>();
            var srcCanvas = _source.GetComponentInParent<Canvas>();
            var srcScaler = srcCanvas != null ? srcCanvas.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (srcScaler != null)
            {
                scaler.uiScaleMode = srcScaler.uiScaleMode;
                scaler.referenceResolution = srcScaler.referenceResolution;
                scaler.screenMatchMode = srcScaler.screenMatchMode;
                scaler.matchWidthOrHeight = srcScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = srcScaler.referencePixelsPerUnit;
            }

            // [Aemy 2026-10-27] Lop CHAN click: phu ca man, nam duoi man ghep tran. Truoc day moi Graphic tat raycast
            // -> click xuyen xuong panel chon balo (tu mo khi vao tran) phia sau -> bam duoc balo trong luc cho.
            host.AddComponent<GraphicRaycaster>();
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blocker.transform.SetParent(host.transform, false);
            Stretch(blocker.transform as RectTransform);
            var blockImg = blocker.GetComponent<Image>();
            blockImg.color = new Color(0f, 0f, 0f, 0f);
            blockImg.raycastTarget = true;

            var clone = Object.Instantiate(_source.gameObject, host.transform, false);
            clone.name = "Matchmaking (phong cho)";
            Stretch(clone.transform as RectTransform);
            // [Aemy 2026-10-27] Nen (BG) trong scene dat CO DINH 1920x1080 giua man -> man rong hon 16:9 (21:9, 32:9...)
            // bi ho 2 ben, nhin nhu "co lai" khi sang map (o menu con nen MenuShell phia sau che). Ep keo gian full man;
            // Pic ben trong dung AspectRatioFitter EnvelopeParent nen van phu kin + giu ti le anh.
            var bg = clone.transform.Find("BG") as RectTransform;
            if (bg != null) Stretch(bg);
            clone.SetActive(true);
            var view = clone.GetComponent<MatchmakingController>();
            view.StopTimer();
            view.SetCancelVisible(false);
            // Chi la man hinh xem: khong cho dieu huong tay cam / click vao chinh man nay (Blocker o duoi lo viec chan).
            foreach (var g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            foreach (var sel in clone.GetComponentsInChildren<Selectable>(true)) sel.interactable = false;

            _runner = host.AddComponent<Runner>();
            _runner.Init(view, _menuScene, blocker);
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Vao tran that bai / huy: bo man phong cho.</summary>
        public static void Release()
        {
            if (_runner != null) Object.Destroy(_runner.gameObject);
            _runner = null;
        }

        // [2026-10-24] TREN man den chuyen scene (SceneFader): tu menu -> tai map -> phong cho chi thay DUNG 1 man ghep tran
        // (truoc nam duoi -> man menu, den, roi hien lai = trong nhu 2 lan ghep tran).
        private static int SortingOrderAboveFader()
        {
            int order = 900;
            var fader = SceneFader.Instance;
            var fc = fader != null ? fader.GetComponentInChildren<Canvas>(true) : null;
            if (fc != null) order = Mathf.Max(order, Mathf.Min(fc.rootCanvas.sortingOrder + 1, 32767));
            return order;
        }

        private class Runner : MonoBehaviour
        {
            private const float MaxWaitForMatchSeconds = 30f; // khong vao duoc tran trong khoang nay -> bo
            private MatchmakingController _view;
            private string _menuScene;
            private float _startedAt, _nextRoster;
            private bool _leftMenu;
            private bool _mapApplied;
            private GameObject _blocker;
            private float _nextLock;
            // [Aemy 2026-10-27] Panel chon balo bi khoa trong luc cho (CanvasGroup.interactable = false) -> tra lai khi het phong cho.
            private readonly List<CanvasGroup> _locked = new List<CanvasGroup>();
            private readonly List<bool> _lockedWas = new List<bool>();

            public void Init(MatchmakingController view, string menuScene, GameObject blocker)
            {
                _view = view; _menuScene = menuScene; _blocker = blocker; _startedAt = Time.unscaledTime;
                NetworkRunnerHandler.StartGameFailed += OnStartGameFailed;
            }

            private void OnDestroy()
            {
                NetworkRunnerHandler.StartGameFailed -= OnStartGameFailed;
                UnlockLoadout();
            }

            // Khoa moi panel chon balo dang co (ca panel dang an: HUD tu bat lai khi OnUILoadout).
            // Chuot: Blocker da chan. Tay cam / Enter: Button khong interactable -> Submit khong goi onClick.
            private void LockLoadout()
            {
                if (Time.unscaledTime < _nextLock) return;
                _nextLock = Time.unscaledTime + 0.5f;
                foreach (var lo in Object.FindObjectsByType<SelectLoadoutController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (lo == null) continue;
                    var cg = lo.GetComponent<CanvasGroup>();
                    if (cg == null) cg = lo.gameObject.AddComponent<CanvasGroup>();
                    if (_locked.Contains(cg)) continue;
                    _locked.Add(cg); _lockedWas.Add(cg.interactable);
                    cg.interactable = false;
                    // dang focus 1 nut balo (tay cam) -> bo focus de khong con vien chon
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(lo.transform))
                        es.SetSelectedGameObject(null);
                }
            }

            private void UnlockLoadout()
            {
                for (int i = 0; i < _locked.Count; i++) if (_locked[i] != null) _locked[i].interactable = _lockedWas[i];
                _locked.Clear(); _lockedWas.Clear();
            }
            private void OnStartGameFailed(Fusion.ShutdownReason reason) { Release(); }

            private void Update()
            {
                if (_view == null) { Release(); return; }
                string active = SceneManager.GetActiveScene().name;
                bool inMenu = !string.IsNullOrEmpty(_menuScene) && active == _menuScene;
                if (!inMenu) _leftMenu = true;
                else if (_leftMenu || Time.unscaledTime - _startedAt > MaxWaitForMatchSeconds) { Release(); return; }
                if (!inMenu) LockLoadout(); // da sang map: panel balo tu mo -> khoa trong luc phong cho

                var gs = NetworkGameState.Instance;
                if (gs == null || gs.Object == null || !gs.Object.IsValid)
                {
                    // Dang tai map / cho NetworkGameState: giu nguyen man (roster cua menu, che do + ten map).
                    if (!inMenu && Time.unscaledTime - _startedAt > MaxWaitForMatchSeconds) { Release(); return; }
                    _view.StopTimer();
                    return;
                }
                if (!gs.InLobby || gs.GameStarted || gs.IsMatchOver()) { Release(); return; }

                // [2026-10-27] Khong cho mo Pause trong luc cho (truoc day an man cho -> lo man tran phia sau).
                // ESC da bi chan o LocalInputController; Pause lo dang mo (vd. mo truoc khi man cho hien) -> dong lai.
                var gm = GameManager.Instance;
                if (gm != null && gm.OnUIOption())
                {
                    gm.SetOnUIOptions(false);
                    var hud = Object.FindFirstObjectByType<GameHudControllerBase>();
                    if (hud != null) hud.SetPauseVisible(false);
                }
                _view.StopTimer(); // OnEnable cua man ghep tran tu bat dong ho dem len -> tat (dong ho dang an)

                // [2026-10-24] Nyx: khong hien bo dem / tieu de, chi che do + ten map + nguoi trong phong.
                bool team = gs.Mode == GamePlayMode.Team;
                _view.SetMode(team ? "TEAM DEATHMATCH" : "FREE FOR ALL");

                // [Aemy 2026-10-27] Map that dang choi (vao bang ma: map cua chu phong, khong phai map minh chon o menu).
                if (!_mapApplied)
                {
                    _mapApplied = true;
                    MapView mv;
                    if (_maps.TryGetValue(active, out mv)) _view.SetMap(mv.Name, mv.Description, mv.Background);
                }

                if (Time.unscaledTime >= _nextRoster)
                {
                    _nextRoster = Time.unscaledTime + 0.5f;
                    LobbyRosterView.Fill(_view, BuildRoster(team), team, this);
                }
            }

            // Nguoi trong phong (NPlayer da spawn tren may nay). Chua co ten (vua vao) -> "..." cho den khi dong bo.
            private static List<LobbyMember> BuildRoster(bool team)
            {
                var list = new List<LobbyMember>();
                foreach (var np in Object.FindObjectsByType<NPlayer>(FindObjectsSortMode.None))
                {
                    if (np == null || np.Object == null || !np.Object.IsValid) continue;
                    string name = np.Nickname.ToString();
                    string teamId = "";
                    if (team && np.NetworkedTeam == PlayerTeam.Blue) teamId = "Blue";
                    else if (team && np.NetworkedTeam == PlayerTeam.Red) teamId = "Red";
                    list.Add(new LobbyMember
                    {
                        IsLocal = np.Object.HasInputAuthority,
                        Name = string.IsNullOrEmpty(name) ? "..." : name,
                        TeamId = teamId,
                        AvatarUrl = np.AvatarUrl.ToString(),
                    });
                }
                return list;
            }
        }
    }
}
