using System.Collections;
using Game.UI;
using UnityEngine;

public class UIGameplay : MonoBehaviour
{
    [SerializeField] private UIResultViewController _winlose;
    private Coroutine _endGame;

    private bool OnLoad = false;
    private bool _reported = false;   // guard bao cao 1 lan

    private void Awake()
    {
        // reset so lieu dau tran (hygiene cho pipeline stats).
        Game.Match.MatchStatsReporter.Instance.ResetMatch();
        Game.UI.CustomRoomSession.AttachHudLabel(transform); // hien ma phong tuy chinh tren HUD
    }

    private void Update()
    {
        var gs = NetworkGameState.Instance;
        if (gs == null || gs.Object == null || !gs.Object.IsValid) return;

        if (GameManager.Instance == null) return;

        if (gs.IsMatchOver() && !OnLoad)
        {
            // Set Mvp
            KillInfo mvpPlayer = new KillInfo();
            if (gs.KillLeaderboard.Count > 0)
                mvpPlayer = gs.KillLeaderboard[0];
            _winlose?.SetMvp(mvpPlayer);

            // Set State
            KillInfo killInfo = new KillInfo();
            gs.TryGetKillInfo(GameManager.Instance.GetPlayerID(), out killInfo);

            float ratio = killInfo.Deaths == 0
                ? killInfo.Kills + killInfo.Assist
                : (float)(killInfo.Kills + killInfo.Assist) / killInfo.Deaths;

            PlayerMatchStats playerMatchStats = new PlayerMatchStats(killInfo.Kills, killInfo.Deaths, killInfo.Assist, ratio, GameManager.Instance.GetBestStreak());

            _winlose?.SetStats(playerMatchStats);

            // Set Result + xac dinh thang/thua
            bool won;
            if (GameManager.Instance.GetGamePlayMode() == GamePlayMode.Team)
                won = Team(gs);
            else
                won = Solo(gs);

            //  Day stats + EXP len PlayFab, roi hien phan thuong (EXP/Level).
            ReportAndShowRewards(killInfo, won);

            if (_endGame == null && gameObject.activeInHierarchy)
                _endGame = StartCoroutine(DisplayResult(3f));

            OnLoad = true;
        }
    }

    private bool Team(NetworkGameState gs)
    {
        // Forfeit -> doi con lai thang; nguoc lai theo ti so kill.
        bool blueWon;
        bool draw = false;
        if (gs.EndedByForfeit && gs.ForfeitWinnerTeam >= 0)
            blueWon = (gs.ForfeitWinnerTeam == (int)PlayerTeam.Blue);
        else
        {
            draw = gs.BlueKill == gs.RedKill; // hoa -> khong ai thang (truoc day doi Do tu "thang")
            blueWon = gs.BlueKill > gs.RedKill;
        }

        var local = NPlayer.Local;
        bool localPlayerWon = !draw && local != null && local.NetworkedTeam == (blueWon ? PlayerTeam.Blue : PlayerTeam.Red);
        // Ten = DOI THANG (Alpha = Blue, Beta = Red - khop HUD), khong phai doi cua minh.
        string name = draw ? "Alpha · Beta" : (blueWon ? "Alpha" : "Beta");
        string winScore = (blueWon ? gs.BlueKill : gs.RedKill).ToString();
        int loseScore = blueWon ? gs.RedKill : gs.BlueKill;
        _winlose?.SetResult(new MatchResult(name, winScore, loseScore, draw ? 2 : (localPlayerWon ? 1 : 0)));
        // Ti so theo goc nhin nguoi choi: TRAI = doi minh, PHAI = doi dich, to mau theo doi (Xanh/Do).
        // Thang 10-2 (minh Xanh) -> "10 - 2"; ben Do thua thay "2 - 10".
        bool myBlue = local == null || local.NetworkedTeam == PlayerTeam.Blue;
        int myScore = myBlue ? gs.BlueKill : gs.RedKill;
        int enemyScore = myBlue ? gs.RedKill : gs.BlueKill;
        _winlose?.SetTeamScores(myScore, myBlue, enemyScore);
        return localPlayerWon;
    }

    private bool Solo(NetworkGameState gs)
    {
        KillInfo killInfo = new KillInfo();
        gs.TryGetKillInfo(GameManager.Instance.GetPlayerID(), out killInfo);
        int rank = gs.GetLeaderboardRank(GameManager.Instance.GetPlayerID());

        bool localPlayerWon;
        if (gs.EndedByForfeit) localPlayerWon = gs.ForfeitWinnerId.Equals(GameManager.Instance.GetPlayerID()); // [LAST-MAN] nguoi con lai thang
        else localPlayerWon = rank == 1; // #1 = Thang
        MatchResult matchResult = new MatchResult(killInfo.PlayerName, $"#{rank}", killInfo.Kills, localPlayerWon ? 1 : 0);
        _winlose?.SetResult(matchResult);
        return localPlayerWon;
    }

    // EXP tran = TotalExp + 100 (hoan thanh) + 200 (neu thang).
    // Day len PlayFab qua MatchStatsReporter; dung Experience that tra ve de tinh Level + phan thuong.
    private void ReportAndShowRewards(KillInfo killInfo, bool won)
    {
        if (_reported) return;
        _reported = true;

        int expGained = killInfo.TotalExp + 100 + (won ? 200 : 0);
        int credits = won ? 200 : 100;   // GC du kien (CloudScript AwardMatchGC cong that khi da deploy)

        Game.Match.MatchStatsReporter.Instance.ReportMatchEnd(
            killInfo.Kills, killInfo.Deaths, killInfo.Assist, 0, expGained, won,
            total =>
            {
                int xpInto, xpNext;
                int lvl = Backend.Models.LevelCurve.LevelForXp(total.Experience, out xpInto, out xpNext);
                _winlose?.SetRewards(new RewardInfo(expGained, credits, lvl, xpInto, xpNext));
            },
            err =>
            {
                // Chua dang nhap / chua bootstrap: van hien phan thuong tam (tinh theo EXP tran).
                int xpInto, xpNext;
                int lvl = Backend.Models.LevelCurve.LevelForXp(expGained, out xpInto, out xpNext);
                _winlose?.SetRewards(new RewardInfo(expGained, credits, lvl, xpInto, xpNext));
                Debug.LogWarning("[UIGameplay] ReportMatchEnd loi: " + err);
            });
    }

    private IEnumerator DisplayResult(float time)
    {
        yield return new WaitForSeconds(time);
        if (_winlose != null)
            _winlose.ResultVisible(2f);
    }
}
