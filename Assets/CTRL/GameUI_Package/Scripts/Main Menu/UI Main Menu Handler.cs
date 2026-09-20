using System.Collections;
using TMPro;
using UnityEngine;

public class UIMainMenuHandler : MonoBehaviour
{
    [SerializeField] private CanvasGroup _playerDetaisPanel;
    [SerializeField] private CanvasGroup _sessionListPanel;
    [SerializeField] private CanvasGroup _createSessionPanel;
    [SerializeField] private GameObject _statusPanel;
    
    [SerializeField] private TMP_InputField _player_Name_InputField;
    [SerializeField] private TMP_InputField _session_Name_InputField;
    [SerializeField] private TMP_InputField _killTarget_InputField;
    public GamePlayMode GameMode = GamePlayMode.Solo;
    
    private void Start()
    {
        if (PlayerPrefs.HasKey("PlayerNickName"))
        {
            _player_Name_InputField.text = PlayerPrefs.GetString("PlayerNickName");
        }
        
        if (PlayerPrefs.GetInt("ReturnToSessionList", 0) == 1)
        {
            PlayerPrefs.DeleteKey("ReturnToSessionList");
            OnFindGameClicked(1f);
        }
    }

    private void HideAllPanels()
    {
        DisplayPanel(_playerDetaisPanel, 0);
        DisplayPanel(_sessionListPanel,0);
        DisplayPanel(_createSessionPanel,0);
        _statusPanel?.SetActive(false);
    }

    private void DisplayPanel(CanvasGroup canvasGroup, int alpha)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha != 0;
    }

    public void OnFindGameClicked(float time = 0.5f)
    {
        PlayerPrefs.SetString("PlayerNickName", _player_Name_InputField.text);
        PlayerPrefs.Save();

        GameManager.Instance.SetPlayerNickName(_player_Name_InputField.text);
        
        NetworkRunnerHandler NRunnerHandler = FindAnyObjectByType<NetworkRunnerHandler>();
        
        if (NRunnerHandler != null)
        {
            HideAllPanels();
            DisplayPanel(_sessionListPanel, 1);

            if (gameObject.activeInHierarchy)
                StartCoroutine(OnjoinLobbyRoutine(NRunnerHandler, time));
        }
    }
    private IEnumerator OnjoinLobbyRoutine(NetworkRunnerHandler runner, float time)
    {
        yield return new WaitForSeconds(time);
        runner.OnJoinLobby(GetLobbyName(GameMode));
        FindAnyObjectByType<UISessionListHandler>(FindObjectsInactive.Include).OnLookingForGameSessions();
    }

    public void OnCreateNewGameClicked()
    {
        HideAllPanels();
        DisplayPanel(_createSessionPanel, 1);
        if (GameManager.Instance != null && _killTarget_InputField != null)
            _killTarget_InputField.text = GameManager.Instance.KillTarget.ToString();
    }

    public void SetGameMode(int modeindex)
    {
        if (modeindex == 0)
            GameMode = GamePlayMode.Solo;
        else if (modeindex == 1)
            GameMode = GamePlayMode.Team;
        GameManager.Instance.SetGamePlayMode(GameMode);
    }

    public string GetLobbyName(GamePlayMode mode)
    {
        switch (mode)
        {
            case GamePlayMode.Solo:
                return "Solo";
            case GamePlayMode.Team:
                return "Team";
        }
        return "Solo";
    }
    
    public void OnStartNewSessionClicked()
    {
        if (GameManager.Instance != null && _killTarget_InputField != null)
        {
            if (int.TryParse(_killTarget_InputField.text, out int killTarget))
            {
                GameManager.Instance.SetKillTarget(killTarget);
            }
        }
        
        NetworkRunnerHandler NRunnerHandler = FindAnyObjectByType<NetworkRunnerHandler>();
        GameManager.Instance.NewGame();
        if (NRunnerHandler != null)
        {
            HideAllPanels();
            
            NRunnerHandler.CreateGame(_session_Name_InputField.text, GetLobbyName(GameMode), SceneName._LONG_MAP_1);
            
            _statusPanel?.SetActive(true);
        }
    }

    public void OnBackClicked(int index)
    {
        if (index == 0)
        {
            NetworkRunnerHandler NRunnerHandler = FindAnyObjectByType<NetworkRunnerHandler>();
            if (NRunnerHandler != null)
            {
                NRunnerHandler.OnLeaveLobby();
                HideAllPanels();
                DisplayPanel(_playerDetaisPanel, 1);
            }
        }
        if (index == 1)
        {
            HideAllPanels();
            DisplayPanel(_sessionListPanel, 1);
        }
    }

    public void OnJoiningServer()
    {
        HideAllPanels();
        _statusPanel?.SetActive(true);
    }
}
