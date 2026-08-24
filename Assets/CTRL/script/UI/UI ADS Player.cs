using Game.UI;
using UnityEngine;

public class UIADSPlayer : MonoBehaviour
{
    private NCharMovementHandler _nCharMovementHandler;
    private GameHudControllerBase _gameHudController;

    private void Awake()
    {
        _nCharMovementHandler = GetComponentInParent<NCharMovementHandler>();
        _gameHudController = GetComponent<GameHudControllerBase>();
    }

    private void Update()
    {
        if (_nCharMovementHandler == null || _nCharMovementHandler.Object == null || !_nCharMovementHandler.Object.IsValid)
            return;
        
        var _state = _nCharMovementHandler.StateFl;
        
        _gameHudController.SetCrosshairVisible(!_state.IsADS);
    }
}
