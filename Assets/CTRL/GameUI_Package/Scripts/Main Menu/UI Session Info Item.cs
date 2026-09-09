using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISessionInfoItem : MonoBehaviour
{
    [SerializeField] private Button _join_Button;
    [SerializeField] private TextMeshProUGUI _session_Name;
    [SerializeField] private TextMeshProUGUI _player_Count;
    
    private SessionInfo _sessionInfo;

    public event Action<SessionInfo> OnJoinSession;

    public void SetInformation(SessionInfo sessionInfo)
    {
        _sessionInfo = sessionInfo;
        
        _session_Name.text = sessionInfo.Name;
        _player_Count.text = $"{sessionInfo.PlayerCount.ToString()} / {sessionInfo.MaxPlayers.ToString()}";

        bool IsJoinButtonActive = true;
        
        if (sessionInfo.PlayerCount >= sessionInfo.MaxPlayers)
            IsJoinButtonActive = false;
        
        _join_Button.interactable = IsJoinButtonActive;
    }

    public void OnClick()
    {
        OnJoinSession?.Invoke(_sessionInfo);
        GameManager.Instance.NewGame();
    }
}
