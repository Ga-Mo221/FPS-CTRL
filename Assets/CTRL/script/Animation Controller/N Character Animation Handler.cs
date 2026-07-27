using Fusion;
using UnityEngine;

public class NCharacterAnimationHandler : NetworkBehaviour
{
    private CharComponentManager _componentManager;
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private Vector2 _move;
    private Vector2 _velocity;
    private float _smoothTime;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
    }

    public override void Render()
    {
        var state = _movementHandler._state;
        _smoothTime = _movementHandler.Data.MoveSmooth;

        Vector2 targetMove = state.Move(_movementHandler.MoveDirection);
        _move = Vector2.SmoothDamp(_move, targetMove, ref _velocity, _smoothTime);
        _playerMotionController.Move(_move);
        _playerMotionController.IsGrouded(_componentManager.NetworkCharacterController.Grounded);
        _playerMotionController.ADS(state.IsADS);
        _playerMotionController.IsReloading(state.IsReloading);
        _playerMotionController.GrenadeID(_movementHandler._grenadeID);
        _playerMotionController.IsThrow(state.IsThrow);
        _playerMotionController.IsCrouching(state.IsCrouch);
        _playerMotionController.IsSliding(state.IsSlide);
        _playerMotionController.IsSwitching(state.IsSwitching);

        if (_movementHandler.IsJump)
        {
            _playerMotionController.IsJumping();
            _movementHandler.IsJump = false;
        }
    }
}
