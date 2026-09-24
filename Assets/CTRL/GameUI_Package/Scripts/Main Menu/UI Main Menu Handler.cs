using System.Collections;
using TMPro;
using UnityEngine;

public class UIMainMenuHandler : MonoBehaviour
{
    [SerializeField] private CanvasGroup _playerDetaisPanel;
    [SerializeField] private CanvasGroup _sessionListPanel;
    [SerializeField] private CanvasGroup _setupPanel;
    [SerializeField] private CanvasGroup _createSessionPanel;
    [SerializeField] private GameObject _statusPanel;
    
    [SerializeField] private TMP_InputField _player_Name_InputField;
    [SerializeField] private TMP_InputField _session_Name_InputField;
    [SerializeField] private TMP_InputField _killTarget_InputField;
    
    [Header("Game Mode")]
    public GamePlayMode GameMode = GamePlayMode.Solo;
    [SerializeField] private TextMeshProUGUI _selected_Mode;
    
    [Header("map")]
    [SerializeField] private TextMeshProUGUI _selected_Map;

    private string _mapName = SceneName._SHORT_MAP_1;
    
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
        
        if (_selected_Mode != null && GameManager.Instance != null)
            _selected_Mode.text = $"Selected Mode: {GameManager.Instance.GetGamePlayMode().ToString()}";
        
        if (_selected_Map != null)
            _selected_Map.text = $"Selected Map: {_mapName}";
    }

    private void HideAllPanels()
    {
        DisplayPanel(_playerDetaisPanel, 0);
        DisplayPanel(_sessionListPanel,0);
        DisplayPanel(_createSessionPanel,0);
        DisplayPanel(_setupPanel,0);
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
        _selected_Mode.text = $"Selected Mode: {GameManager.Instance.GetGamePlayMode().ToString()}";
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
            
            NRunnerHandler.CreateGame(_session_Name_InputField.text, GetLobbyName(GameMode), _mapName);
            
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

        if (index == 2)
        {
            HideAllPanels();
            DisplayPanel(_setupPanel, 1);
        }
    }

    public void OnSelectMap(int mapIndex)
    {
        if (mapIndex == 2)
            _mapName = SceneName._SHORT_MAP_1;
        else if (mapIndex == 1)
            _mapName = SceneName._LONG_MAP_1;
        
        _selected_Map.text = $"Selected Map: {_mapName}";
    }

    public void OnJoiningServer()
    {
        HideAllPanels();
        _statusPanel?.SetActive(true);
    }
}
