using Game.UI;
using GaMo.TrueInputSystem;
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
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Vao tran bang chon Balo tu mo -> SetOnUIOptions(true) bi tu choi khi loadout dang mo
                // => ESC/Start khong mo duoc Pause. Dang mo bang Balo thi dong no (giu balo dang chon) roi mo Pause.
                if (gm.OnUILoadout() && !gm.OnUIOption())
                {
                    if (_gameHudController != null) _gameHudController.SetLoadoutVisible(false);
                    gm.SetOnuiLoadout(false);
                }
                gm.SetOnUIOptions(!gm.OnUIOption());
                // hien/an panel Pause theo co
                if (_gameHudController != null) _gameHudController.SetPauseVisible(gm.OnUIOption());
            }
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
                    _weaponManager.Rpc_RequestChangeWeapon(item.WeaponID.ToString(), item.CurrentBullet, item.MaxBullet);
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
                var local = NPlayer.Local;
                if (local != null)
                {
                    var Components = local.ComponentManager;
                    if (Components == null) return;

                    var Weapons = Components.WeaponManager;
                    if (Weapons == null) return;

                    var Grenade = Components.NCharMovementHandler;
                    if (Grenade == null) return;

                    bool W1 = true, W2 = true;

                    if (Weapons.Weapon_One_CTRL != null)
                        W1 = Weapons.Weapon_One_CTRL.CanSwitchLayout();
                    
                    if (Weapons.Weapon_Two_CTRL != null)
                        W2 = Weapons.Weapon_Two_CTRL.CanSwitchLayout();
                    
                    if (W1 && W2 && !Grenade.Grenade_Q && !Grenade.Grenade_E)
                        _gameHudController.SetLoadoutVisible(true);
                    else gm.SetOnuiLoadout(false);
                }
            }
        }
    }
}
