using System.Collections;
using Game.UI;
using UnityEngine;

public class UIGameplay : MonoBehaviour
{
    [SerializeField] private UIResultViewController _winlose;
    private Coroutine _endGame;
    
    private void Update()
    {
        var gs = NetworkGameState.Instance;
        if (gs == null || gs.Object == null || !gs.Object.IsValid) return;
            
        if (GameManager.Instance == null) return;

        if (gs.IsMatchOver())
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
                
            // Set Result
            if (GameManager.Instance.GetGamePlayMode() == GamePlayMode.Team)
                Team(gs);
            else Solo(gs);
                
            if (_endGame == null && gameObject.activeInHierarchy)
                _endGame = StartCoroutine(DisplayResult(3f));
        }
    }
    
    private void Team(NetworkGameState gs)
    {
        MatchResult matchResult = new MatchResult();
        if (gs.BlueKill > gs.RedKill)
        {
            bool LocalPlayerWon = NPlayer.Local.NetworkedTeam == PlayerTeam.Blue;

            string name = LocalPlayerWon ? "Alpha" : "Beta";

            matchResult = new MatchResult(name, gs.BlueKill.ToString(), gs.RedKill,
                LocalPlayerWon ? 1 : 0);
        }
        else
        {
            bool LocalPlayerWon = NPlayer.Local.NetworkedTeam == PlayerTeam.Red;

            string name = LocalPlayerWon ? "Beta" : "Alpha";

            matchResult = new MatchResult(name, gs.RedKill.ToString(), gs.BlueKill,
                LocalPlayerWon ? 1 : 0);
        }
                
        _winlose?.SetResult(matchResult);
    }

    private void Solo(NetworkGameState gs)
    {
        KillInfo killInfo = new KillInfo();
        gs.TryGetKillInfo(GameManager.Instance.GetPlayerID(), out killInfo);
        int rank = gs.GetLeaderboardRank(GameManager.Instance.GetPlayerID());
            
        MatchResult matchResult = new MatchResult(killInfo.PlayerName, $"#{rank}", killInfo.Kills, 2);
            
        _winlose?.SetResult(matchResult);
            
    }

    private IEnumerator DisplayResult(float time)
    {
        yield return new WaitForSeconds(time);
        if (_winlose != null)
            _winlose.ResultVisible(2f);
    }
}
