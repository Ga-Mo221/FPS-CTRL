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
    private int _lastJumpCount;

    // Giữ trigger IsJumping trong 1 khoảng ngắn thay vì reset ngay frame sau:
    // nếu Animator đang blend (vd. SLIDE IN → SLIDING) thì trigger 1 frame sẽ bị bỏ lỡ.
    private const float JumpTriggerWindow = 0.2f;
    private float _jumpTriggerTime;
    private bool _jumpTriggerPending;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
        _hpHandler = _componentManager.NHPHandler;
        _lastJumpCount   = _movementHandler.JumpCount; // nguoi vao sau khong phat lai cu nhay cu
        _lastFireCount   = _movementHandler.FireCount;
        _lastSwitchCount = _movementHandler.SwitchCount;
    }

    public override void Render()
    {
        var state = _movementHandler.StateFl;
        _smoothTime = _movementHandler.Data.MoveSmooth;

        _playerMotionController.SetShootIndex(_movementHandler.ShootIndex);
        
        if (_movementHandler.FireCount != _lastFireCount)
        {
            _lastFireCount = _movementHandler.FireCount;
            
            // Client (InputAuthority, không phải host) cầm dao đã tự predict trigger nhân vật
            // trong NShootHandler.FixedUpdateNetwork → bỏ qua ở đây để không chém 2 lần.
            bool predictedMelee = Object.HasInputAuthority && !Object.HasStateAuthority
                && _componentManager.WeaponManager != null
                && _componentManager.WeaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee;

            if (!_movementHandler.RightPuch && !predictedMelee)
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

        if (_movementHandler.JumpCount != _lastJumpCount)
        {
            _lastJumpCount = _movementHandler.JumpCount;
            _playerMotionController.IsJumping();
            _jumpTriggerTime = Time.time;
            _jumpTriggerPending = true;
        }
        else if (_jumpTriggerPending && Time.time - _jumpTriggerTime > JumpTriggerWindow)
        {
            // Hết cửa sổ mà Animator chưa dùng trigger → xoá để không bị nhảy "muộn".
            _playerMotionController.ResetJumping();
            _jumpTriggerPending = false;
        }
    }
    
    public void LocalStopSwitch()
    {
        _localIsSwitching = false;
        //Debug.Log("[NCharacterAnimationHandler] LocalStopSwitch");
    }
}
