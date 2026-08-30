using Game.UI;
using TrueInputSystem;
using UnityEngine;

public class LocalInputController : MonoBehaviour
{
    private TISInputBridge _input;
    private CharComponentManager _componentManager;
    private NPlayer _player;
    private GameHudControllerBase _gameHudController;
    private NInGameMessagesHandler _messageHandler;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _componentManager = GetComponent<CharComponentManager>();
        _player = _componentManager.NPlayer;
        _gameHudController = _componentManager.GameHudController();
        _messageHandler = _componentManager.NInGameMessagesHandler;
    }

    private void Update()
    {
        if (_input == null) _input = TISInputBridge.Instance;
        
        if (_input == null) return;
        
        if (!_player.IsLocalPlayer) return;
        
        OnUI();
        OnTabScoreBoader();

        OnUIChatBox();
    }
    
    private void OnUI()
    {
        if (_input.GetSkillAction("ESC", TISButtonReadMode.PressUp))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.SetOnUIOptions(!GameManager.Instance.OnUIOption());
            //Debug.Log($"[CharacterInputHandler] OnUI: {GameManager.Instance.OnUI}");
        }
    }

    private void OnTabScoreBoader()
    {
        if (_gameHudController != null)
            _gameHudController.SetScoreboardVisible(_input.GetSkillAction("TAB", TISButtonReadMode.Hold));
    }

    private void OnUIChatBox()
    {
        if (_input.GetSkillAction("ENTER"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetOnUIChatBox(!GameManager.Instance.OnUIChatBox());
                
                var lastactive = _gameHudController.SendMessageVisible(GameManager.Instance.OnUIChatBox());

                if (!GameManager.Instance.OnUIChatBox() && lastactive)
                {
                    _messageHandler.SendInGameRPCMessage();
                }
            }
        }
    }
}
