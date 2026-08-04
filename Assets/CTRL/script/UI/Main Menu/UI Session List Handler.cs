using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISessionListHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _status_text;
    [SerializeField] private GameObject _sessionItemListPrefab;
    [SerializeField] private VerticalLayoutGroup _verticalLayoutGroup;

    private void Awake()
    {
        ClearList();
    }

    public void ClearList()
    {
        foreach (Transform child in _verticalLayoutGroup.transform)
        {
            Destroy(child.gameObject);
        }
        
        _status_text.gameObject.SetActive(false);
    }

    public void AddToList(SessionInfo sessionInfo, GamePlayMode gamePlayMode)
    {
        if (gamePlayMode != GameManager.instance.GetGamePlayMode()) return;
        
        UISessionInfoItem item = Instantiate(_sessionItemListPrefab, _verticalLayoutGroup.transform).GetComponent<UISessionInfoItem>();
        
        item.SetInformation(sessionInfo);

        item.OnJoinSession += AddedSessionInfoListUIItem_OnJoinSession;
    }

    private void AddedSessionInfoListUIItem_OnJoinSession(SessionInfo sessionInfo)
    {
        NetworkRunnerHandler networkRunnerHandler = FindAnyObjectByType<NetworkRunnerHandler>();
        UIMainMenuHandler uiMainMenuHandler = FindAnyObjectByType<UIMainMenuHandler>();
        if (networkRunnerHandler != null && uiMainMenuHandler != null)
        {
            networkRunnerHandler.JoinGame(sessionInfo,
                uiMainMenuHandler.GetLobbyName(GameManager.instance.GetGamePlayMode()));
            uiMainMenuHandler.OnJoiningServer();
        }
    }

    public void OnNoSessionsFound()
    {
        _status_text.text = "No game sessions found";
        ClearList();
        _status_text.gameObject.SetActive(true);
    }

    public void OnLookingForGameSessions()
    {
        _status_text.text = "Looking for game sessions...";
        ClearList();
        _status_text.gameObject.SetActive(true);
    }
}
