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

        // Editor tat domain reload -> reset moi lan Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _source = null; _menuScene = null; _runner = null; }

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

            var clone = Object.Instantiate(_source.gameObject, host.transform, false);
            clone.name = "Matchmaking (phong cho)";
            var rt = clone.transform as RectTransform;
            if (rt != null) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
            clone.SetActive(true);
            var view = clone.GetComponent<MatchmakingController>();
            view.StopTimer();
            view.SetCancelVisible(false);
            // Chi la man hinh xem: khong chan click / khong cho dieu huong tay cam vao.
            foreach (var g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            _runner = host.AddComponent<Runner>();
            _runner.Init(view, _menuScene);
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

            public void Init(MatchmakingController view, string menuScene)
            {
                _view = view; _menuScene = menuScene; _startedAt = Time.unscaledTime;
                NetworkRunnerHandler.StartGameFailed += OnStartGameFailed;
            }

            private void OnDestroy() { NetworkRunnerHandler.StartGameFailed -= OnStartGameFailed; }
            private void OnStartGameFailed(Fusion.ShutdownReason reason) { Release(); }

            private void Update()
            {
                if (_view == null) { Release(); return; }
                string active = SceneManager.GetActiveScene().name;
                bool inMenu = !string.IsNullOrEmpty(_menuScene) && active == _menuScene;
                if (!inMenu) _leftMenu = true;
                else if (_leftMenu || Time.unscaledTime - _startedAt > MaxWaitForMatchSeconds) { Release(); return; }

                var gs = NetworkGameState.Instance;
                if (gs == null || gs.Object == null || !gs.Object.IsValid)
                {
                    // Dang tai map / cho NetworkGameState: giu nguyen man (roster cua menu, che do + ten map).
                    if (!inMenu && Time.unscaledTime - _startedAt > MaxWaitForMatchSeconds) { Release(); return; }
                    _view.StopTimer();
                    return;
                }
                if (!gs.InLobby || gs.GameStarted || gs.IsMatchOver()) { Release(); return; }

                // Mo Pause trong luc cho -> tam an de thay Pause.
                bool pause = GameManager.Instance != null && GameManager.Instance.OnUIOption();
                if (_view.gameObject.activeSelf == pause) _view.gameObject.SetActive(!pause);
                if (pause) return;
                _view.StopTimer(); // OnEnable cua man ghep tran tu bat dong ho dem len -> tat (dong ho dang an)

                // [2026-10-24] Nyx: khong hien bo dem / tieu de, chi che do + ten map + nguoi trong phong.
                bool team = gs.Mode == GamePlayMode.Team;
                _view.SetMode(team ? "TEAM DEATHMATCH" : "FREE FOR ALL");

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
