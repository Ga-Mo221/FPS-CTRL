
using Backend.Interfaces;
using Backend.Models;
using UnityEngine;

namespace Backend.Handoff
{
    /// <summary>
    /// Cầu nối THẬT: PlayFab match -> phòng chơi Fusion. Thay LoggingMatchHandoff trong FPS-CTRL.
    ///
    /// Dùng NetworkRunnerHandler (qua GameManager.Instance) với SessionName = match.MatchId.
    /// KHÔNG cần OnJoinLobby trước khi CreateGame — theo đúng luồng host của UIMainMenuHandler
    /// (OnStartNewSessionClicked chỉ gọi NewGame + CreateGame). Mọi client cùng match nhận CÙNG
    /// MatchId -> cùng CreateGame(AutoHostOrClient) -> Fusion tự chọn 1 host, các peer còn lại join
    /// -> hội tụ vào cùng 1 phòng.
    ///
    /// Yêu cầu: GameManager + NetworkRunnerHandler phải tồn tại (DontDestroyOnLoad) từ MenuShell
    /// trước khi bấm "Bắt đầu" (xem bootstrap ở MenuShell).
    /// </summary>
    public class FusionMatchHandoff : IMatchHandoff
    {
        private const string GameplaySceneName = "ShortMap1"; // fallback mac dinh (map thuc lay tu GameManager.GetSelectedMapScene)

        public void HandOff(MatchFoundResult match)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[FusionHandoff] GameManager.Instance null — chưa bootstrap GameManager ở MenuShell?"); return; }
            NetworkRunnerHandler runner = gm.NetworkRunnerHandler;
            if (runner == null) { Debug.LogError("[FusionHandoff] NetworkRunnerHandler null — chưa bootstrap Runner ở MenuShell?"); return; }

            GamePlayMode mode = MapMode(match.Mode);
            gm.SetGamePlayMode(mode);
            EnsureNickname(gm);
            gm.NewGame();

            string lobby = LobbyName(mode);
            Debug.Log($"<color=#22d3ee>[FusionHandoff]</color> Vào phòng Fusion: session='{match.MatchId}', lobby='{lobby}', mode={match.Mode}.");
            // [UI-INTEGRATION] fade den truoc khi Fusion load scene cho muot, roi CreateGame.
            if (Game.UI.SceneFader.Instance != null)
                Game.UI.SceneFader.Instance.FadeOut(() => runner.CreateGame(match.MatchId, lobby, gm.GetSelectedMapScene()));
            else
                runner.CreateGame(match.MatchId, lobby, gm.GetSelectedMapScene());
        }

        public void HandOffSolo(MatchMode modeIn)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[FusionHandoff] GameManager.Instance null."); return; }
            NetworkRunnerHandler runner = gm.NetworkRunnerHandler;
            if (runner == null) { Debug.LogError("[FusionHandoff] NetworkRunnerHandler null."); return; }

            GamePlayMode mode = MapMode(modeIn);
            gm.SetGamePlayMode(mode);
            EnsureNickname(gm);
            gm.NewGame();

            string lobby = LobbyName(mode);
            string sessionName = "solo_" + modeIn + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Debug.Log($"<color=#22d3ee>[FusionHandoff]</color> Ép bắt đầu solo/warm-up: session='{sessionName}', lobby='{lobby}', mode={modeIn}.");
            // [UI-INTEGRATION] fade den truoc khi Fusion load scene cho muot, roi CreateGame.
            if (Game.UI.SceneFader.Instance != null)
                Game.UI.SceneFader.Instance.FadeOut(() => runner.CreateGame(sessionName, lobby, gm.GetSelectedMapScene()));
            else
                runner.CreateGame(sessionName, lobby, gm.GetSelectedMapScene());
        }

        // Phong tuy chinh: chu phong tao (Host), ban be vao bang ma (Client). onResult(ok, lyDo).
        public void HandOffCustom(string code, MatchMode rule, bool asHost, System.Action<bool, string> onResult)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.NetworkRunnerHandler == null) { if (onResult != null) onResult(false, "NoRunner"); return; }
            NetworkRunnerHandler runner = gm.NetworkRunnerHandler;
            GamePlayMode mode = MapMode(rule);
            gm.SetGamePlayMode(mode);
            EnsureNickname(gm);
            gm.NewGame();
            string session = Game.UI.CustomRoomSession.SessionOf(code);
            System.Action<bool, string> done = (ok, why) =>
            {
                if (!ok && Game.UI.SceneFader.Instance != null) Game.UI.SceneFader.Instance.FadeIn();
                if (onResult != null) onResult(ok, why);
            };
            System.Action go = () => runner.StartCustomGame(session, "Custom", gm.GetSelectedMapScene(), asHost, done);
            if (Game.UI.SceneFader.Instance != null) Game.UI.SceneFader.Instance.FadeOut(go); else go();
        }

        // Vao THANG 1 phong dang dien ra (tim duoc qua lobby Fusion), GameMode.Client.
        // onResult(false) -> phong vua day/dong: caller quay lai tim tran PlayFab.
        public void HandOffJoinExisting(string sessionName, MatchMode modeIn, System.Action<bool, string> onResult)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.NetworkRunnerHandler == null) { if (onResult != null) onResult(false, "NoRunner"); return; }
            NetworkRunnerHandler runner = gm.NetworkRunnerHandler;
            GamePlayMode mode = MapMode(modeIn);
            gm.SetGamePlayMode(mode);
            EnsureNickname(gm);
            gm.NewGame();
            System.Action<bool, string> done = (ok, why) =>
            {
                if (!ok && Game.UI.SceneFader.Instance != null) Game.UI.SceneFader.Instance.FadeIn();
                if (onResult != null) onResult(ok, why);
            };
            System.Action go = () => runner.JoinExistingSession(sessionName, LobbyName(mode), done);
            if (Game.UI.SceneFader.Instance != null) Game.UI.SceneFader.Instance.FadeOut(go); else go();
        }

        public static string LobbyNameFor(MatchMode mode) { return LobbyName(MapMode(mode)); }

        // FFA -> Solo (không chia team) ; TDM -> Team.
        private static GamePlayMode MapMode(MatchMode mode)
        {
            return mode == MatchMode.TDM ? GamePlayMode.Team : GamePlayMode.Solo;
        }

        private static string LobbyName(GamePlayMode mode)
        {
            return mode == GamePlayMode.Team ? "Team" : "Solo";
        }

        // An toàn: nếu GameManager chưa có nickname (menu chưa set) thì lấy từ PlayerPrefs như UIMainMenuHandler.
        // Dam bao LUON co ten: PlayFab display name -> PlayerPrefs -> "PLAYER".
        private static void EnsureNickname(GameManager gm)
        {
            if (!string.IsNullOrEmpty(gm.GetPlayerNickName())) return;
            var prof = Backend.Bootstrap.ProfileCache.Cached;
            if (prof != null && !string.IsNullOrEmpty(prof.DisplayName)) { gm.SetPlayerNickName(prof.DisplayName); return; }
            if (PlayerPrefs.HasKey("PlayerNickName") && !string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerNickName")))
                { gm.SetPlayerNickName(PlayerPrefs.GetString("PlayerNickName")); return; }
            gm.SetPlayerNickName("PLAYER");
        }
    }
}
