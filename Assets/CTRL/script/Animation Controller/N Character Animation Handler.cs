using Fusion;
using UnityEngine;

public class NCharacterAnimationHandler : NetworkBehaviour
{
    private CharComponentManager _componentManager;
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private NHPHandler _hpHandler;
    private Vector2 _move;
    private Vector2 _velocity;
    private float _smoothTime;
    private int _lastFireCount;
    private bool _localIsSwitching;
    private int _lastSwitchCount;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
        _hpHandler = _componentManager.NHPHandler;
    }

    public override void Render()
    {
        var state = _movementHandler.StateFl;
        _smoothTime = _movementHandler.Data.MoveSmooth;

        _playerMotionController.SetShootIndex(_movementHandler.ShootIndex);
        
        if (_movementHandler.FireCount != _lastFireCount)
        {
            _lastFireCount = _movementHandler.FireCount;
            
            if (!_movementHandler.RightPuch)
                _playerMotionController.IsShooting();
        }
        
        _playerMotionController.IsDead(_hpHandler.IsDead);

        Vector2 targetMove = state.Move(_movementHandler.MoveDirection);
        _move = Vector2.SmoothDamp(_move, targetMove, ref _velocity, _smoothTime);
        _playerMotionController.Move(_move);
        
        if (!_playerMotionController.IsReloading(state.IsReloading))
            _movementHandler.StopReload();
        
        _playerMotionController.IsGrouded(_componentManager.NetworkCharacterController.Grounded);
        _playerMotionController.ADS(state.IsADS);
        _playerMotionController.GrenadeID(_movementHandler.GrenadeID);
        _playerMotionController.IsThrow(state.IsThrow);
        _playerMotionController.IsCrouching(state.IsCrouch);
        _playerMotionController.IsSliding(state.IsSlide);
        
        if (_movementHandler.SwitchCount != _lastSwitchCount)
        {
            _lastSwitchCount = _movementHandler.SwitchCount;
            _localIsSwitching = true;
            //Debug.Log("[NCharacterAnimationHandler] switch");
        }
        
        _playerMotionController.IsSwitching(_localIsSwitching);

        if (_movementHandler.IsJump)
        {
            _playerMotionController.IsJumping();
            _movementHandler.IsJump = false;
        }
        else
        {
            _playerMotionController.ResetJumping();
        }
    }
    
    public void LocalStopSwitch()
    {
        _localIsSwitching = false;
        Debug.Log("[NCharacterAnimationHandler] LocalStopSwitch");
    }
}
