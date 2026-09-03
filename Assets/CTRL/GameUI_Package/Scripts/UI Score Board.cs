using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIScoreBoard : MonoBehaviour
{
    [SerializeField] private UIPlayerScore[] _playerScores;

    [SerializeField] private TextMeshProUGUI _blue_Score_Text;
    [SerializeField] private TextMeshProUGUI _red_Score_Text;
    
    private NetworkGameState _networkGameState;

    private void Start()
    {
        _networkGameState = NetworkGameState.Instance;
    }

    private void LateUpdate()
    {
        if (_networkGameState == null)
            _networkGameState = NetworkGameState.Instance;
        
        if (_networkGameState == null || _playerScores.Length == 0) return;

        if (_blue_Score_Text != null)
            _blue_Score_Text.text = _networkGameState.BlueKill.ToString();
        
        if (_red_Score_Text != null)
            _red_Score_Text.text = _networkGameState.RedKill.ToString();

        int slotCount = _playerScores.Length;
        for (int i = 0; i < slotCount; i++)
        {
            if (GameManager.Instance != null && GameManager.Instance.GetGamePlayMode() == GamePlayMode.Solo)
                SoloMode(i);
            else TeameMode(i);
        }
    }

    private void SoloMode(int i)
    {
        if (i < _networkGameState.KillLeaderboard.Count)
        {
            KillInfo killInfo = _networkGameState.KillLeaderboard[i];
            bool isLocalPlayer = GameManager.Instance != null && GameManager.Instance.GetPlayerID() == killInfo.PlayerId;
            _playerScores[i].SetPlayer(i + 1, killInfo.PlayerName, killInfo.Kills, killInfo.Deaths, killInfo.Assist, isLocalPlayer);
        }
        else _playerScores[i].SetPlayer(0, "", 0, 0, 0, false);
    }

    private void TeameMode(int i)
    {
        List<KillInfo> RedList = new List<KillInfo>();
        List<KillInfo> BlueList = new List<KillInfo>();
        foreach (var team in _networkGameState.KillLeaderboard)
        {
            if (team.Team == PlayerTeam.Blue)
                BlueList.Add(team);
            else if (team.Team == PlayerTeam.Red)
                RedList.Add(team);
        }

        if (i < 5)
        {
            if (i < BlueList.Count)
            {
                KillInfo killInfo = BlueList[i];
                bool isLocalPlayer = GameManager.Instance != null && GameManager.Instance.GetPlayerID() == killInfo.PlayerId;
                _playerScores[i].SetPlayer(i + 1, killInfo.PlayerName, killInfo.Kills, killInfo.Deaths, killInfo.Assist, isLocalPlayer);
            }
            else _playerScores[i].SetPlayer(0, "", 0, 0, 0, false);
        }
        else
        {
            int index = i - 5;
            if (index < RedList.Count)
            {
                KillInfo killInfo = RedList[index];
                bool isLocalPlayer = GameManager.Instance != null && GameManager.Instance.GetPlayerID() == killInfo.PlayerId;
                _playerScores[i].SetPlayer(index+1, killInfo.PlayerName, killInfo.Kills, killInfo.Deaths, killInfo.Assist, isLocalPlayer);
            }
            else _playerScores[i].SetPlayer(0, "", 0, 0, 0, false);
        }
    }
}
