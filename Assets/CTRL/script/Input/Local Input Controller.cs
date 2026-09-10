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
    private WeaponManager _weaponManager;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _componentManager = GetComponent<CharComponentManager>();
        _player = _componentManager.NPlayer;
        _gameHudController = _componentManager.GameHudController();
        _messageHandler = _componentManager.NInGameMessagesHandler;
        _weaponManager = _componentManager.WeaponManager;
    }

    private void Update()
    {
        if (_input == null) _input = TISInputBridge.Instance;
        
        if (_input == null) return;
        
        if (!_player.IsLocalPlayer) return;
        
        OnUI();
        OnTabScoreBoader();

        OnUIChatBox();

        OnDragItem();

        OpenLoadOut();
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

    private void OnDragItem()
    {
        if (_input.GetSkillAction("Drag", TISButtonReadMode.PressDown))
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.GetCanDragItem())
            {
                var item = gm.GetDropItemWeapon;
                if (item != null)
                {
                    _weaponManager.Rpc_RequestChangeWeapon(item.WeaponName.ToString(), item.CurrentBullet, item.MaxBullet);
                    item.Despawn();
                }
            }
        }
    }

    private void OpenLoadOut()
    {
        if (_input.GetSkillAction("B", TISButtonReadMode.PressUp))
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.SetOnuiLoadout(true))
            {
                _gameHudController.SetLoadoutVisible(true);
            }
        }
    }
}
