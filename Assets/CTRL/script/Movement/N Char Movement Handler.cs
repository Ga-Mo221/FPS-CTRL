using Fusion;
using UnityEngine;

[DefaultExecutionOrder(20)]
public class NCharMovementHandler : NetworkBehaviour
{
    [SerializeField] private PlayerData _data;
    public PlayerData Data => _data;
    [Networked] public Vector2 MoveDirection {get; private set;}
    [Networked] public PlayerStateFlags StateFl {get; set;}
    private PlayerStateFlags _newState;
    [Networked] public int GrenadeID {get; private set;}
    [Networked] public NetworkBool Frag { get; set; }
    [Networked] public NetworkBool Flash { get; set; }
    [Networked] public NetworkBool IsTurnInPlaceActive { get; private set; }
    [Networked] public NetworkBool IsJump { get; set; }
    [Networked] public float NetworkedPitch { get; private set; }
    // ── Fire state — thay IsFiring(bool) bằng FireCount(int) để tiết kiệm băng thông
    // IsFiring chỉ cần 1 tick để NShootHandler đọc trong cùng FixedUpdateNetwork,
    // không cần sync ra ngoài. FireCount sync để remote clients spawn visual.
    // AimForward sync để Host/remote dùng cùng hướng bắn chính xác từ InputData.
    [Networked] public int   FireCount   { get; private set; }  // tăng mỗi phát bắn
    [Networked] public Vector3 AimForward { get; private set; }  // hướng bắn từ NInputData
    [Networked] public ShotInfo LastShotInfo { get; private set; } // kết quả raycast từ Host

    // SHOOTING STATE
    // IsFiring chỉ dùng nội bộ trong frame FixedUpdateNetwork, không cần Networked
    public bool IsFiring { get; private set; }

    // Thêm networked timer — Host nhớ "đang trong trạng thái firing"
    // Mỗi khi nhận được Attack=true, gia hạn timer thêm N tick
    [Networked] private TickTimer _fireHoldTimer { get; set; }

    // Số tick buffer — đủ để che lấp jitter 1-2 tick mà không delay cảm giác
    // Với tick rate 30: 3 tick = 100ms buffer
    private const int FIRE_BUFFER_TICKS = 5;


    [Networked] private TickTimer _slideTimer { get; set; }
    [Networked] private TickTimer _respawnInvincibleTimer { get; set; }
    [Networked] private float _currentSpeed { get; set; }
    [Networked] private float _acceleration { get; set; }
    [Networked] private bool _isRespawnRequested { get; set; } = false;
    private CharacterState _stateLogic;
    [Networked] private NetworkButtons _previousButtons { get; set; }
    
    [Networked, OnChangedRender(nameof(OnControllerEnabledChanged))]
    private NetworkBool IsControllerEnabled { get; set; } = true;
    
    [Networked] public NetworkBool CanTakeDamage { get; set; } = true;
    
    [Networked] public int SwitchCount { get; private set; }
    [Networked] private NetworkBool _newRound {get; set;} = false;


    [Networked] public float CameraSpeed { get; private set; } = 1.0f;
    [Networked] public float ADSLookSpeedMultiplier { get; private set; } = 0.5f;
    
    private CharComponentManager _componentManager;
    private NetworkCharacterControllerCustom _ncc;
    private ConstraintManager _constraintManager;
    private PlayerMotionController _playerMotionController;
    private NHPHandler  _hpHandler;
    private WeaponManager _weaponManager;
    
    // ── helpers cho NShootHandler ──────────────────────────────────────────
    public void IncrementFireCount() => FireCount++;
    public void SetLastShotInfo(Vector3 pos, Vector3 normal, bool showEffect)
    {
        LastShotInfo = new ShotInfo
        {
            HitPosition = pos,
            HitNormal   = normal,
            ShowEffect  = showEffect
        };
    }

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        if (_componentManager != null)
        {
            _ncc = _componentManager.NetworkCharacterController;
            _constraintManager = _componentManager.ConstraintManager;
            _playerMotionController = _componentManager.PlayerMotionController;
            _hpHandler = _componentManager.NHPHandler;
            _weaponManager = _componentManager.WeaponManager;
        }
        _stateLogic = new CharacterState();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (_isRespawnRequested)
            {
                Respawn();
                return;
            }

            if (!CanTakeDamage && _respawnInvincibleTimer.Expired(Runner))
                CanTakeDamage = true;
            
            if (_hpHandler.IsDead)
            {
                IsFiring = false;
                if (_ncc.Controller.enabled)
                    _ncc.Move(Vector3.zero, 0, 0);

                // ── Nhấn Jump để hồi sinh sớm ──
                if (GetInput(out NInputData inputdata1))
                {
                    if (inputdata1.Buttons.WasPressed(_previousButtons, InputButtons.Jump))
                        _hpHandler.RequestEarlyRevive();
                    _previousButtons = inputdata1.Buttons; // giữ state buttons
                }

                return;
            }
        }
        
        //if (GameManager.instance.OnUI) return;
        
        if (GetInput(out NInputData inputdata))
        {
            MoveDirection = inputdata.MoveDirection;
            // Tính pitch trực tiếp trên Host, lưu vào networked property
            float minPitch = _data.PitchMin;
            float maxPitch = _data.PitchMax;
            var pitchCamera = _data.PitchCamera * CameraSpeed;
            
            if (StateFl.IsADS || StateFl.IsThrow)
                pitchCamera *= ADSLookSpeedMultiplier;
            
            NetworkedPitch = Mathf.Clamp(
                NetworkedPitch - inputdata.LookDelta.y * pitchCamera * Runner.DeltaTime,
                minPitch, maxPitch
            );

            
            _playerMotionController.ActiveWeponCTRL()?.SetLookDelta(inputdata.LookDelta);

            // Lưu AimForward từ InputData — chính xác hơn camera local, đồng bộ cho mọi máy
            AimForward = inputdata.AimForward;
            
            _newState = StateFl;

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

            CCSetHeight();
            
            // cần có setting riêng.
            _ncc.AccumulateYaw(inputdata.LookDelta.x);
            
            Vector3 moveDirection = transform.forward * inputdata.MoveDirection.y + transform.right * inputdata.MoveDirection.x;
            moveDirection.Normalize();
            (_currentSpeed, _acceleration) = _data.MaxSpeed(_newState);
            _ncc.Move(moveDirection, _currentSpeed, _acceleration);
            
            _previousButtons = inputdata.Buttons;
            StateFl = _newState;
        }
    }

    private void CCSetHeight()
    {
        if (StateFl.IsSlide)
            _ncc.SetControllerHeight(PlayerState.Slide);
        else if (StateFl.IsCrouch)
            _ncc.SetControllerHeight(PlayerState.Crouching);
        else _ncc.SetControllerHeight(PlayerState.Standing);
            
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
            GrenadeID = 0;

            if (state.IsSprint) return;

            if (state.IsRun && _weaponManager.Weapon_Active().WeaponType != WeaponType.Sniper)
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
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Crouch) && !state.IsSlide)
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
        if (IsJump) IsJump = false;
        
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Jump))
        {
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
            _ncc.Jump();
        }
    }
#endregion

#region Weapon Control
    private void ShootHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsSwitching || state.IsThrow)
        {
            IsFiring = false;
            return;
        }

        switch (_playerMotionController.ActiveWeaponMode())
        {
            case GunRecoilController.FiringMode.Semi:
            case GunRecoilController.FiringMode.Melee:
            case GunRecoilController.FiringMode.Burst:
                // Semi/Burst không bị ảnh hưởng bởi vấn đề này
                // vì WasPressed chỉ cần 1 tick đúng là đủ
                IsFiring = false;
                if (input.Buttons.WasPressed(_previousButtons, InputButtons.Attack))
                    IsFiring = true;
                break;

            case GunRecoilController.FiringMode.Auto:
                if (input.Buttons.IsSet(InputButtons.Attack))
                {
                    // Nhận được Attack=true → gia hạn buffer timer
                    if (_fireHoldTimer.ExpiredOrNotRunning(Runner))
                        _fireHoldTimer = TickTimer.CreateFromTicks(Runner, FIRE_BUFFER_TICKS);
                    IsFiring = true;
                }
                else if (input.Buttons.WasReleased(_previousButtons, InputButtons.Attack))
                {
                    // Xác nhận rõ ràng nút được thả → dừng ngay, không chờ timer
                    _fireHoldTimer = default;
                    IsFiring = false;
                }
                else
                {
                    // Không có input packet trong tick này (jitter/packet chưa đến)
                    // → dùng timer để quyết định còn firing không
                    IsFiring = !_fireHoldTimer.ExpiredOrNotRunning(Runner);
                }
                break;
        }
    }
    
    public bool FireOnce()
    {
        var state = StateFl;
        _stateLogic.ToggleShoot(ref state, _constraintManager);
        var ctrl = _playerMotionController.ActiveWeponCTRL();
        var canShoot = false;
        if (ctrl != null)
            canShoot = ctrl.OnFire();
        StateFl = state;
        return canShoot;
    }
    
    private void ADSHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsSwitching || state.IsThrow || state.IsSlide)
            return;
        
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.ADS))
        {
            _stateLogic.EnterADS(ref state, _constraintManager);
            if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Sniper)
            {
                // Sniper ADS → giảm tốc độ xoay camera
                if (Object.HasInputAuthority)
                    RPC_SetAdsMultiplier(SettingManager.Instance.SettingData.SniperADSSensitivity);
            }
            else if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Rifle)
            {
                // Rifle ADS → giảm tốc độ xoay camera
                if (Object.HasInputAuthority)
                    RPC_SetAdsMultiplier(SettingManager.Instance.SettingData.RifleADSSensitivity);
            }
        }

        if (input.Buttons.WasReleased(_previousButtons, InputButtons.ADS))
            _stateLogic.ExitADS(ref state);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetAdsMultiplier(float value)
    {
        // Chỉ chạy trên StateAuthority (Host)
        ADSLookSpeedMultiplier = value; // [Networked] field
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetCameraSpeed(float value)
    {
        // Chỉ chạy trên StateAuthority (Host)
        CameraSpeed = value; // [Networked] field
    }

    private void ReloadHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Reload))
            _stateLogic.StartReload(ref state, _constraintManager);
    }

    public void StopReload()
    {
        if (!Object.HasStateAuthority) return;

        var s = StateFl;
        _stateLogic.StopReload(ref s);
        StateFl = s;
    }

    private void SwitchWeaponHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsThrow || state.IsSwitching) return;

        if (input.ZoomDelta != 0 || input.Buttons.WasPressed(_previousButtons, InputButtons.Switch))
        {
            if (_weaponManager.SwitchWeaponActive(0))
            {
                _stateLogic.StartSwitch(ref state);
                SwitchCount++;
            }
        }

        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Weapon1))
        {
            if (_weaponManager.SwitchWeaponActive(1))
            {
                _stateLogic.StartSwitch(ref state);
                SwitchCount++;
            }
        }
        
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Weapon2))
        {
            if (_weaponManager.SwitchWeaponActive(2))
            {
                _stateLogic.StartSwitch(ref state);
                SwitchCount++;
            }
        }
    }

    public void StopSwitch()
    {
        if (!Object.HasStateAuthority) return;
        
        var s = StateFl;
        _stateLogic.StopSwitch(ref s);
        StateFl = s;
    }

    private void GrenadeHandler(NInputData input, ref PlayerStateFlags state)
    {
        if (state.IsSwitching || state.IsSlide) return;

        bool fragPressed  = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFrag);
        bool flashPressed = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFlash);
        bool fragReleased  = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFrag);
        bool flashReleased = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFlash);

        if ((fragPressed || flashPressed) && !state.IsThrow)
        {
            if (fragPressed && !Frag)
            {
                GrenadeID = 1;
                _stateLogic.StartThrow(ref state, _constraintManager);
            }
            else if (flashPressed && !Flash)
            {
                GrenadeID = 2;
                _stateLogic.StartThrow(ref state, _constraintManager);
            }
            
            return;
        }

        if (state.IsThrow)
        {
            bool shouldStop =
                (GrenadeID == 1 && fragReleased) ||
                (GrenadeID == 2 && flashReleased);

            if (shouldStop)
            {
                //if (GrenadeID == 1) Frag = true;
                //if (GrenadeID == 2) Flash = true;
                GrenadeID = 0;
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

    
#region Dead Status
    public void RequestRespawn(bool newRound = false)
    {
        if (!Object.HasStateAuthority) return;
        _newRound = newRound;
        StateFl = new PlayerStateFlags();
        _isRespawnRequested = true;
    }

    public void DisableCCC()  // tên giữ nguyên
    {
        if (!HasStateAuthority) return;
        IsControllerEnabled = false;
        CanTakeDamage = false;
        Debug.Log("<color=green>[NCharMovementHandler]</color> Dead");
    }
    
    void OnControllerEnabledChanged()
    {
        // Chạy trên MỌI máy khi IsControllerEnabled thay đổi
        _ncc.Controller.enabled = IsControllerEnabled;
        Debug.Log($"<color=green>[NCharMovementHandler]</color> {transform.name}: {IsControllerEnabled}");
    }

    private void Respawn()
    {
        IsControllerEnabled = true;
        _ncc.Controller.enabled = true;
        
        // ── Lấy vị trí spawn thông minh thay cho random ──
        var nPlayer = GetComponent<NPlayer>();
        if (SpawnManager.Instance != null && nPlayer != null)
        {
            SpawnResult result = _newRound ? SpawnManager.Instance.GetInitialSpawnPoint(Runner, nPlayer) : 
                SpawnManager.Instance.GetRespawnPoint(Runner, nPlayer);
            _ncc.Teleport(result.Position);
            _ncc.SetYaw(result.Rotation.eulerAngles.y);  // ← ĐÚNG: ghi thẳng vào TargetYaw
        }
        else
        {
            // Fallback nếu SpawnManager chưa có (không nên xảy ra mid-game)
            _ncc.Teleport(Utils.GetRandomSpawnPoint());
        }
        
        _hpHandler.OnRespawn();
        _isRespawnRequested = false;
        if (_weaponManager != null) _weaponManager.Respawn();
        CanTakeDamage = false;
        
        var cantakedamageTime = 3f;
        if (GameManager.Instance != null)
            cantakedamageTime = GameManager.Instance.CanTakeDamageTime;
        
        _respawnInvincibleTimer = TickTimer.CreateFromSeconds(Runner, cantakedamageTime);
        _weaponManager.Rpc_SwitchWeapon();
        Flash = false;
        Frag = false;
    }
#endregion
}