using Game.UI;
using UnityEngine;

public class UIADSPlayer : MonoBehaviour
{
    private NCharMovementHandler _nCharMovementHandler;
    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;

    private void Awake()
    {
        _nCharMovementHandler = GetComponentInParent<NCharMovementHandler>();
        _componentManager = GetComponentInParent<CharComponentManager>();
        _gameHudController = GetComponent<GameHudControllerBase>();
    }

    private void Update()
    {
        if (_nCharMovementHandler == null || _nCharMovementHandler.Object == null || !_nCharMovementHandler.Object.IsValid)
            return;

        var _state = _nCharMovementHandler.StateFl;

        // Sniper không có tâm ảo (cả bắn hông lẫn ADS) — chỉ ngắm qua scope.
        var weaponManager = _componentManager != null ? _componentManager.WeaponManager : null;
        var activeWeapon = weaponManager != null ? weaponManager.Weapon_Active() : null;
        bool isSniper = activeWeapon != null && activeWeapon.WeaponType == WeaponType.Sniper;

        _gameHudController.SetCrosshairVisible(!_state.IsADS && !isSniper);
    }
}
