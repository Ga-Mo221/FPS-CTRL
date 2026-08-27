using Game.UI;
using TrueInputSystem;
using UnityEngine;

public class LocalInputController : MonoBehaviour
{
    private TISInputBridge _input;
    private CharComponentManager _componentManager;
    private NPlayer _player;
    private GameHudControllerBase _gameHudController;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _componentManager = GetComponent<CharComponentManager>();
        _player = _componentManager.NPlayer;
        _gameHudController = _componentManager.GameHudController();
    }

    private void Update()
    {
        if (_input == null) _input = TISInputBridge.Instance;
        
        if (_input == null) return;
        
        if (!_player.IsLocalPlayer) return;
        
        OnUI();
        OnTabScoreBoader();
    }
    
    private void OnUI()
    {
        if (_input.GetSkillAction("ESC", TISButtonReadMode.PressUp))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnUI = !GameManager.Instance.OnUI;
            //Debug.Log($"[CharacterInputHandler] OnUI: {GameManager.Instance.OnUI}");
        }
    }

    private void OnTabScoreBoader()
    {
        if (_gameHudController != null)
            _gameHudController.SetScoreboardVisible(_input.GetSkillAction("Tab", TISButtonReadMode.Hold));
    }
}
