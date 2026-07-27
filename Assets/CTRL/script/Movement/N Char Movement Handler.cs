using Fusion;
using UnityEngine;

[DefaultExecutionOrder(20)]
public class NCharMovementHandler : NetworkBehaviour
{
    [SerializeField] private PlayerData _data;
    public PlayerData Data => _data;
    
    [Networked] public Vector2 MoveDirection {get; set;}
    
    [Networked] public PlayerStateFlags _state {get; set;}
    private PlayerStateFlags _newState;
    [Networked] public int _grenadeID {get; set;}
    [Networked] public NetworkBool IsTurnInPlaceActive { get; set; }
    [Networked] public NetworkBool IsJump { get; set; }
    [Networked] public float LookDeltaY {get; set;}
    
    [Networked] private TickTimer _switchTimer { get; set; }
    [Networked] private TickTimer _slideTimer { get; set; }
    
    [Networked] public float _currentSpeed { get; set; }
    [Networked] public float _acceleration { get; set; }

    [Networked] public NetworkBool IsFiring { get; set; }

    private CharacterState _stateLogic;
    [Networked] private NetworkButtons _previousButtons { get; set; }
    
    private CharComponentManager _componentManager;
    private NetworkCharacterControllerCustom _ncc;
    private ConstraintManager _constraintManager;
    private FreeCameraController _freeCameraController;
    private PlayerMotionController _playerMotionController;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        if (_componentManager != null)
        {
            _ncc = _componentManager.NetworkCharacterController;
            _constraintManager = _componentManager.ConstraintManager;
            _freeCameraController = _componentManager.FreeCameraController;
            _playerMotionController = _componentManager.PlayerMotionController;
        }
        _stateLogic = new CharacterState();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NInputData inputdata))
        {
            MoveDirection = inputdata.MoveDirection;
            LookDeltaY = inputdata.LookDelta.y;
            _freeCameraController.Pitch(_data, LookDeltaY, _data.PitchCamera, Runner.DeltaTime);
            _playerMotionController.ActiveWeponCTRL()?.SetLookDelta(inputdata.LookDelta);
            
            _newState = _state;

            ShootHandler(inputdata, ref _newState);
            ReloadHandler(inputdata, ref _newState);
            ADSHandler(inputdata, ref _newState);
            SwitchWeaponHandler(inputdata, ref _newState);
            ModeSwitchHandler(inputdata);
            GrenadeHandler(inputdata, ref _newState);
            JumpHandler(inputdata, ref _newState);
            RunHandler(inputdata, ref _newState);
            CrouchOrSlideHandler(inputdata, ref _newState);
            MoveHandler(inputdata, ref _newState);
            
            _ncc.Rotate(inputdata.LookDelta.x);
            
            Vector3 moveDirection = transform.forward * inputdata.MoveDirection.y + transform.right * inputdata.MoveDirection.x;
            moveDirection.Normalize();
            (_currentSpeed, _acceleration) = _data.MaxSpeed(_newState);
            _ncc.Move(moveDirection, _currentSpeed, _acceleration);
            
            _previousButtons = inputdata.Buttons;
            _state = _newState;
        }
    }
    
#region Movement
    private void MoveHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.Move(input.MoveDirection) == Vector2.zero)
        {
            if (!state.IsSlide)
                _stateLogic.StopMovement(ref state, _constraintManager);
            IsTurnInPlaceActive = _ncc.Grounded;
        }else IsTurnInPlaceActive = false;
        
        if (Mathf.Abs(input.MoveDirection.y) < 0.2f) _stateLogic.StopRun(ref state);
        
        if (state.IsSprint && Mathf.Abs(input.MoveDirection.x) > 0.45f)
        {
            _stateLogic.StartRun(ref state, _constraintManager);
        }
        
        _playerMotionController?.ActiveWeponCTRL()?.SetMoveDirection(MoveDirection);
    }

    private void RunHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Run))
        {
            if (state.IsSlide) return;
            if (input.MoveDirection.y < 0.7f) return;
            
            _stateLogic.StopReload(ref state);
            _stateLogic.StopThrow(ref state);
            _grenadeID = 0;

            if (state.IsSprint) return;

            if (state.IsRun)
            {
                _stateLogic.StartSprint(ref state, _constraintManager);
                return;
            }
            
            if (state.IsCrouch) _stateLogic.StopCrouch(ref state);
            
            _stateLogic.StartRun(ref state, _constraintManager);
        }
    }
#endregion

#region Crouching Or Slide
    private void CrouchOrSlideHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Crouch))
        {
            _stateLogic.ToggleCrouch(ref state, _constraintManager);
            _slideTimer = TickTimer.CreateFromSeconds(Runner, _data.SlideHoldDuration);
        }

        if (state.IsSlide && _slideTimer.Expired(Runner))
        {
            _stateLogic.StopSlide(ref state, _constraintManager);
        }
    }
#endregion

#region Jump
    private void JumpHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Jump))
        {
            // Nếu đang slide thì hủy slide ngay lập tức
            if (state.IsSlide)
            {
                _stateLogic.StopSlide(ref state, _constraintManager);
            }
            else if (state.IsCrouch)
            {
                _stateLogic.StopCrouch(ref state);
                return;
            }
            
            _stateLogic.Jump(ref state);
            IsJump = true;
            _ncc.Jump(); // NCC đã tự check Grounded bên trong
        }
    }
#endregion

#region Weapon Control
    private void ShootHandler(NInputData input, ref PlayerStateFlags state)
    {
        IsFiring = false;

        if (state.IsSwitching || state.IsThrow)
            return;

        switch (_playerMotionController.ActiveWeaponMode())
         {
             case GunRecoilController.FiringMode.Semi:
             case GunRecoilController.FiringMode.Burst:
                 if (input.Buttons.WasPressed(_previousButtons, InputButtons.Attack))
                 {
                     IsFiring = true;
                 }
                 break;
             case GunRecoilController.FiringMode.Auto:
                 if (input.Buttons.IsSet(InputButtons.Attack))
                 {
                     IsFiring = true;
                 }
                 break;
         }
    }
    
    public void FireOnce()
    {
        var state = _state;
        _stateLogic.ToggleShoot(ref state, _constraintManager); 
        _playerMotionController.ActiveWeponCTRL()?.OnFire();
        _state = state;
    }
    
    private void ADSHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsSwitching || state.IsThrow || state.IsSlide)
            return;
        
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.ADS))
            _stateLogic.EnterADS(ref state, _constraintManager);
        
        if  (input.Buttons.WasReleased(_previousButtons, InputButtons.ADS))
            _stateLogic.ExitADS(ref state);
    }

    private void ReloadHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Reload))
            _stateLogic.StartReload(ref state, _constraintManager);
    }

    public void StopReload()
    {
        if (!Object.HasStateAuthority) return; // chỉ Host (hoặc chủ sở hữu state) mới được phép sửa

        var s = _state;
        _stateLogic.StopReload(ref s);
        _state = s;
    }

    private void SwitchWeaponHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsThrow) return;

        if (input.ZoomDelta != 0 || input.Buttons.WasPressed(_previousButtons, InputButtons.Switch))
        {
            _stateLogic.StartSwitch(ref state);
            _switchTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
        }

        if (state.IsSwitching && _switchTimer.Expired(Runner))
        {
            _stateLogic.StopSwitch(ref state);
        }
    }

    private void GrenadeHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsSwitching || state.IsSlide) return;

        bool fragPressed  = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFrag);
        bool flashPressed = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFlash);

        bool fragReleased  = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFrag);
        bool flashReleased = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFlash);

        // Bắt đầu throw: vừa nhấn, và CHƯA đang throw
        if ((fragPressed || flashPressed) && !state.IsThrow)
        {
            _grenadeID = fragPressed ? 1 : 2;
            _stateLogic.StartThrow(ref state, _constraintManager);
            return;
        }

        // Dừng throw: đúng loại lựu đạn đang cầm vừa được NHẢ ra
        if (state.IsThrow)
        {
            bool shouldStop =
                (_grenadeID == 1 && fragReleased) ||
                (_grenadeID == 2 && flashReleased);

            if (shouldStop)
            {
                _grenadeID = 0;
                _stateLogic.StopThrow(ref state);
            }
        }
    }

    private void ModeSwitchHandler(NInputData input)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Mode))
            _playerMotionController.ModeSwitch();
    }
    #endregion
}