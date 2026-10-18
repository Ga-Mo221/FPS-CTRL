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
    [Networked] public NetworkBool Grenade_Q { get; set; }
    [Networked] public NetworkBool Grenade_E { get; set; }
    public bool QKey = false;
    public bool EKey = false;
    [Networked] public NetworkBool IsTurnInPlaceActive { get; private set; }
    // [NET-OPT 2026-10-18 Aemy] (bao cao 3.4) IsJump -> JumpCount: truoc day Render() GHI IsJump=false vao bien networked
    // (khong hop le, client co the mat event nhay). Nay dung bo dem, Render so sanh voi gia tri local.
    // (JumpCount: xem _eventCounters)
    [Networked] public float NetworkedPitch { get; private set; }
    // ── Fire state — thay IsFiring(bool) bằng FireCount(int) để tiết kiệm băng thông
    // IsFiring chỉ cần 1 tick để NShootHandler đọc trong cùng FixedUpdateNetwork,
    // không cần sync ra ngoài. FireCount sync để remote clients spawn visual.
    // AimForward sync để Host/remote dùng cùng hướng bắn chính xác từ InputData.
    // [NET-OPT 2026-10-18 Aemy] (bao cao 9.4) FireCount / SwitchCount / JumpCount gop thanh 3 byte trong 1 word.
    // Gia tri 0..255 quay vong: noi doc chi so sanh != hoac lay delta (byte)(moi - cu).
    [Networked] private uint _eventCounters { get; set; }
    private int GetCounter(int shift) { return (int)((_eventCounters >> shift) & 0xFF); }
    private void BumpCounter(int shift) { _eventCounters = (_eventCounters & ~(0xFFu << shift)) | ((uint)((GetCounter(shift) + 1) & 0xFF) << shift); }
    public int FireCount   { get { return GetCounter(0); } }   // tăng mỗi phát bắn
    public int SwitchCount { get { return GetCounter(8); } }
    public int JumpCount   { get { return GetCounter(16); } }
    // [NET-OPT 2026-10-18 Aemy] (bao cao muc 1) Bo [Networked]: host va client predict deu gan lai tu GetInput moi tick,
    // proxy khong dung (proxy da co TargetYaw + NetworkedPitch). Tiet kiem 3 word dirty gan nhu moi tick.
    public Vector3 AimForward { get; private set; }  // hướng bắn từ NInputData
    // [NET-OPT 2026-10-18 Aemy] (bao cao 2.1, huong A) Bo ShotInfo (~57 word) khoi state cua MOI player.
    // Chi sync ShotSeed (ghi cung tick voi FireCount, chi shotgun) de host/client predict/proxy ra CUNG huong pellet;
    // proxy tu raycast cuc bo de dung visual (damage van do host tinh bang LagCompensation).
    [Networked] public ushort ShotSeed { get; private set; }

    // SHOOTING STATE
    // IsFiring chỉ dùng nội bộ trong frame FixedUpdateNetwork, không cần Networked
    public bool IsFiring { get; private set; }
    [Networked] public bool RightPuch { get; private set; }
    private bool _rightPuchDelay = false;

    [Networked] public int ShootIndex { get; private set; } = 0;

    [Networked] private TickTimer _meleeCooldownTimer { get; set; } // chặn input sau khi đánh
    [Networked] private TickTimer _meleeComboTimer    { get; set; } // cửa sổ chờ click tiếp theo
    private const float MELEE_COOLDOWN    = 0.2f; // chờ sau khi đánh xong
    private const float MELEE_COMBO_WINDOW = 0.6f; // thời gian để nhấn đòn tiếp theo
    [SerializeField] private bool _canNextAttackCombo = true;

    // Thêm networked timer — Host nhớ "đang trong trạng thái firing"
    // Mỗi khi nhận được Attack=true, gia hạn timer thêm N tick
    // [NET-OPT 2026-10-18 Aemy] (bao cao 3.2) buffer ~100ms chi host dung -> field thuong (mat khi migration khong sao)
    private TickTimer _fireHoldTimer;

    // Số tick buffer — đủ để che lấp jitter 1-2 tick mà không delay cảm giác
    // Với tick rate 30: 3 tick = 100ms buffer.
    // FIX Bug #6: giảm từ 5 → 3 (166.7ms → 100ms) để giảm "ghost firing" sau khi
    // thả chuột nhưng release packet bị mất.
    private const int FIRE_BUFFER_TICKS = 3;

    /// <summary>
    /// AimForward của tick cuối cùng có input packet thật từ client.
    /// Khi Host fire từ buffer timer (không có packet), dùng giá trị này thay vì
    /// AimForward hiện tại (có thể là packet cũ từ tick trước) để raycast đúng hướng.
    /// </summary>
    public Vector3 LastConfirmedAimForward { get; private set; } // [NET-OPT] chi host dung -> field thuong


    [Networked] private TickTimer _slideTimer { get; set; }
    [Networked] private TickTimer _respawnInvincibleTimer { get; set; }
    // [NET-OPT 2026-10-18 Aemy] _currentSpeed/_acceleration: tinh va dung ngay trong tick -> bien local (xem FixedUpdateNetwork)
    [Networked] private bool _isRespawnRequested { get; set; } = false;
    private CharacterState _stateLogic;
    [Networked] private NetworkButtons _previousButtons { get; set; }
    
    [Networked, OnChangedRender(nameof(OnControllerEnabledChanged))]
    private NetworkBool IsControllerEnabled { get; set; } = true;
    
    [Networked] public NetworkBool CanTakeDamage { get; set; } = true;
    
    // (SwitchCount: xem _eventCounters)
    [Networked] private NetworkBool _newRound {get; set;} = false;


    [Networked] public float CameraSpeed { get; private set; } = 1.0f;
    [Networked] public float ADSLookSpeedMultiplier { get; private set; } = 0.5f;
    // [NET-OPT 2026-10-18 Aemy] (bao cao 9.5) He so ADS la SETTING, gui 1 lan luc spawn / khi doi setting
    // (Rpc_SetLookSettings). Host + may predict tu chon he so theo loai sung khi bam ADS -> het RPC lap khi resimulate.
    private float _sniperAds = 0.5f;
    private float _rifleAds  = 0.5f;
    
    private CharComponentManager _componentManager;
    private NetworkCharacterControllerCustom _ncc;
    private ConstraintManager _constraintManager;
    private PlayerMotionController _playerMotionController;
    private NHPHandler  _hpHandler;
    private WeaponManager _weaponManager;
    private NShootHandler _nShootHandler;
    
    // ── helpers cho NShootHandler ──────────────────────────────────────────
    public void IncrementFireCount()
    {
        BumpCounter(0);
    }

    /// <summary>Host ghi seed shotgun cung tick voi IncrementFireCount().</summary>
    public void SetShotSeed(ushort seed) { if (ShotSeed != seed) ShotSeed = seed; }

    /// <summary>
    /// Huong ngam suy tu TargetYaw + NetworkedPitch (proxy khong co AimForward). Lech nhe so voi camera that
    /// (recoil/shake) nhung chi dung cho VISUAL dan tren may nguoi xem.
    /// </summary>
    public Vector3 DerivedAimForward
    {
        get
        {
            float yaw = _ncc != null ? _ncc.GetNetworkRotation().eulerAngles.y : transform.eulerAngles.y;
            return Quaternion.Euler(NetworkedPitch, yaw, 0f) * Vector3.forward;
        }
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
            _nShootHandler = _componentManager.NShootHandler;
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
                // Chết → thoát ADS
                if (StateFl.IsADS)
                    ResetADS();
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

        // FIX Bug #2: IsFiring là local non-networked state, không được Fusion
        // restore/rollback khi resimulate. Reset về false mỗi tick rồi derive lại
        // từ input bên dưới, để tránh giá trị cũ từ tick trước leak sang khi
        // GetInput() == false (packet miss) hoặc khi resimulate.
        IsFiring = false;

        if (GetInput(out NInputData inputdata))
        {
            MoveDirection = inputdata.MoveDirection;
            // Tính pitch trực tiếp trên Host, lưu vào networked property
            float minPitch = _data.PitchMin;
            float maxPitch = _data.PitchMax;
            var pitchCamera = _data.PitchCamera * CameraSpeed;
            
            // if (StateFl.IsADS || StateFl.IsThrow)
            //     pitchCamera *= ADSLookSpeedMultiplier;
            if (StateFl.IsADS)
                pitchCamera *= ADSLookSpeedMultiplier;
            
            NetworkedPitch = Mathf.Clamp(
                NetworkedPitch - inputdata.LookDelta.y * pitchCamera * Runner.DeltaTime,
                minPitch, maxPitch
            );

            
            _playerMotionController.ActiveWeponCTRL()?.SetLookDelta(inputdata.LookDelta);

            // Lưu AimForward từ InputData — chính xác hơn camera local, đồng bộ cho mọi máy
            AimForward = inputdata.AimForward;
            // Ghi nhớ AimForward của tick có input thật — dùng khi Host fire từ buffer timer
            LastConfirmedAimForward = inputdata.AimForward;
            
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
            var (currentSpeed, acceleration) = _data.MaxSpeed(_newState);
            _ncc.Move(moveDirection, currentSpeed, acceleration);
            
            _previousButtons = inputdata.Buttons;
            StateFl = _newState;
        }
        else if (Object.HasStateAuthority)
        {
            // FIX Bug #2: không có input packet trong tick này (jitter/packet miss).
            // Với Auto fire, dùng _fireHoldTimer buffer để quyết định có nên tiếp tục
            // bắn hay không thay vì im lặng để IsFiring = false (đã reset ở trên) làm
            // súng khựng lại giữa chừng.
            if (_playerMotionController != null &&
                _playerMotionController.ActiveWeaponMode() == GunRecoilController.FiringMode.Auto)
            {
                IsFiring = !_fireHoldTimer.ExpiredOrNotRunning(Runner);
            }
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
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Jump))
        {
            if (_hpHandler.IsDead) return;
            
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
            BumpCounter(16);
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
            case GunRecoilController.FiringMode.Burst:
                // Semi/Burst không bị ảnh hưởng bởi vấn đề này
                // vì WasPressed chỉ cần 1 tick đúng là đủ
                IsFiring = false;
                if (input.Buttons.WasPressed(_previousButtons, InputButtons.Attack))
                    IsFiring = true;
                break;

            case GunRecoilController.FiringMode.Melee:
                MeleeComboHandler(input);
                break;

            case GunRecoilController.FiringMode.Auto:
                if (input.Buttons.IsSet(InputButtons.Attack))
                {
                    // FIX Bug #3: luôn refresh (sliding window) thay vì chỉ tạo khi đã hết,
                    // nếu không buffer sẽ hết hạn theo tick tạo ban đầu dù Attack vẫn giữ,
                    // khiến 1 tick miss đúng lúc expire làm súng dừng đột ngột.
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
            canShoot = ctrl.OnFire(_rightPuchDelay, StateFl.IsCrouch);
        StateFl = state;
        _rightPuchDelay = false;
        return canShoot;
    }

    /// <summary>
    /// Xử lý combo melee 3 đòn.
    /// ShootIndex: 0 = idle, 1 = đòn 1, 2 = đòn 2, 3 = đòn 3 → reset về 0.
    /// Nếu trong 0.5s không nhấn tiếp → reset về 0.
    /// </summary>
    private void MeleeComboHandler(NInputData input)
    {
        if (RightPuch)
            return;
        
        IsFiring = false;

        // Đang trong cooldown → chưa nhận click
        if (!_meleeCooldownTimer.ExpiredOrNotRunning(Runner))
            return;

        // Hết cửa sổ combo mà không click → reset
        if (ShootIndex > 0 && _meleeComboTimer.Expired(Runner))
        {
            ShootIndex = 0;
            _meleeComboTimer = default;
            _meleeCooldownTimer = default;
            ResetMeleeCombo();
        }

        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Attack))
        {
            if (ShootIndex > 3)
                return;
            
            if (!_canNextAttackCombo)
                return;
            
            ShootIndex++;
            _canNextAttackCombo = false;
            
            IsFiring = true;
        }
    }

    public void ResetMeleeCombo()
    {
        _canNextAttackCombo = true;
    }

    public void StartCoolDownCombat()
    {
        if (!Object.HasStateAuthority) return;
        
        //Debug.Log("StartCoolDownCombat");
        _canNextAttackCombo = true;
        _meleeComboTimer = default;
        _meleeCooldownTimer = default;
        // Bắt đầu cooldown → chặn input trong 0.5s
        _meleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, MELEE_COOLDOWN);

        // Bắt đầu cửa sổ chờ click tiếp theo 0.5s (tính từ khi hết cooldown)
        _meleeComboTimer = TickTimer.CreateFromSeconds(Runner, MELEE_COOLDOWN + MELEE_COMBO_WINDOW);
    }
    
    private void ADSHandler(NInputData input, ref PlayerStateFlags state)
    {
        var melee = _weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee;

        if (melee)
        {
            var ctrl = _playerMotionController.ActiveWeponCTRL();
            var canShoot = false;
            if (ctrl != null)
                canShoot = ctrl.CanFire();
            
            if (state.IsSwitching || state.IsThrow)
            {
                IsFiring = false;
                return;
            }
            
            if (input.Buttons.WasPressed(_previousButtons, InputButtons.ADS) && canShoot)
            {
                RightPuch = true;
                _rightPuchDelay = true;
                IsFiring = true;
                _stateLogic.EnterADS(ref state, _constraintManager);
            }
        }
        else
        {
            if (state.IsSwitching || state.IsThrow || state.IsSlide)
                return;

            if (input.Buttons.WasPressed(_previousButtons, InputButtons.ADS))
            {
                _stateLogic.EnterADS(ref state, _constraintManager);
                if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Sniper )
                {
                    // Sniper ADS → giảm tốc độ xoay camera
                    ADSLookSpeedMultiplier = _sniperAds;
                }
                else if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Rifle || 
                         _weaponManager.Weapon_Active().WeaponType == WeaponType.Shotgun)
                {
                    // Rifle ADS → giảm tốc độ xoay camera
                    ADSLookSpeedMultiplier = _rifleAds;
                }
            }

            if (input.Buttons.WasReleased(_previousButtons, InputButtons.ADS))
                _stateLogic.ExitADS(ref state);
        }
    }

    public void ResetRightPuch()
    {
        if (!Object.HasStateAuthority) return;
        RightPuch = false;
        ShootIndex = 0;
        _canNextAttackCombo = true;
        _meleeComboTimer = default;
        _meleeCooldownTimer = default;
    }

    public void ResetADS()
    {
        var state = StateFl;
        _stateLogic.ExitADS(ref state);
        StateFl = state;
        //Debug.Log("ResetADS");
    }

    /// <summary>
    /// [NET-OPT 2026-10-18 Aemy] Goi tu may local (InputAuthority) luc spawn / khi doi setting.
    /// Luu ngay he so ADS local (de client predict dung) roi gui 1 RPC duy nhat len host.
    /// </summary>
    public void SendLookSettings(float camSpeed, float sniperAds, float rifleAds)
    {
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority) return;
        ApplyLookSettings(camSpeed, sniperAds, rifleAds);
        if (!Object.HasStateAuthority) Rpc_SetLookSettings(camSpeed, sniperAds, rifleAds);
    }

    /// <summary>Luu he so ADS (moi may) va CameraSpeed (chi host). Goi tu Rpc_SetLookSettings / NPlayer.Rpc_InitPlayer.</summary>
    public void ApplyLookSettings(float camSpeed, float sniperAds, float rifleAds)
    {
        _sniperAds = sniperAds;
        _rifleAds  = rifleAds;
        if (Object != null && Object.IsValid && Object.HasStateAuthority)
            CameraSpeed = camSpeed; // [Networked] field
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetLookSettings(float camSpeed, float sniperAds, float rifleAds)
    {
        Debug.Log($"<color=green>[NCharMovementHandler]</color> Look settings: sens={camSpeed} sniperADS={sniperAds} rifleADS={rifleAds}");
        ApplyLookSettings(camSpeed, sniperAds, rifleAds); // Chỉ chạy trên StateAuthority (Host)
    }

    /// <summary>Tuong thich code cu (SettingManager test button): chi doi do nhay, giu he so ADS hien tai.</summary>
    public void RPC_SetCameraSpeed(float value)
    {
        var sd = SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        SendLookSettings(value, sd != null ? sd.SniperADSSensitivity : _sniperAds, sd != null ? sd.RifleADSSensitivity : _rifleAds);
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

        if (input.Buttons.IsSet(InputButtons.ScrollSwitch) || input.Buttons.WasPressed(_previousButtons, InputButtons.Switch))
        {
            if (_weaponManager.SwitchWeaponActive(0))
            {
                _stateLogic.StartSwitch(ref state);
                BumpCounter(8);
            }
        }

        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Weapon1))
        {
            if (_weaponManager.SwitchWeaponActive(1))
            {
                _stateLogic.StartSwitch(ref state);
                BumpCounter(8);
            }
        }
        
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Weapon2))
        {
            if (_weaponManager.SwitchWeaponActive(2))
            {
                _stateLogic.StartSwitch(ref state);
                BumpCounter(8);
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

        bool QKey_Pressed  = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFrag);
        bool EKey_Pressed = input.Buttons.WasPressed(_previousButtons, InputButtons.ThrowFlash);
        bool QKey_Released  = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFrag);
        bool EKey_Released = input.Buttons.WasReleased(_previousButtons, InputButtons.ThrowFlash);

        if ((QKey_Pressed || EKey_Pressed) && !state.IsThrow)
        {
            if (QKey_Pressed && !Grenade_Q)
            {
                GrenadeID = GetIDGrenade(true);
                QKey = true;
                _nShootHandler.SetGrenadeQ_E(1);
                Debug.Log("<color=green>[NCharMovementHandler][Pressed Q]</color> Grenade ID: " + GrenadeID);
                _stateLogic.StartThrow(ref state, _constraintManager);
            }
            else if (EKey_Pressed && !Grenade_E)
            {
                GrenadeID = GetIDGrenade(false);
                EKey = true;
                _nShootHandler.SetGrenadeQ_E(2);
                Debug.Log("<color=green>[NCharMovementHandler][Pressed E]</color> Grenade ID: " + GrenadeID);
                _stateLogic.StartThrow(ref state, _constraintManager);
            }
            
            return;
        }

        if (state.IsThrow)
        {
            bool shouldStop =
                (QKey && QKey_Released) ||
                (EKey && EKey_Released);

            if (shouldStop)
            {
                if (QKey)
                {
                    QKey = false;
                    Grenade_Q = true;
                }
                if (EKey)
                {
                    EKey = false;
                    Grenade_E = true;
                }
                GrenadeID = 0;
                _stateLogic.StopThrow(ref state);
            }
        }
    }

    private int GetIDGrenade(bool Q)
    {
        string grenadeQ = _nShootHandler.GrenadeQ.ToString();
        string grenadeE = _nShootHandler.GrenadeE.ToString();
        if (Q)
        {
            switch (grenadeQ)
            {
                case WeaponID._SMOKE_AN_M8_HC:
                case WeaponID._FRAG_M67: return 1;
                case WeaponID._FLASH_M84: return 2;
            }
        }
        else
        {
            switch (grenadeE)
            {
                case WeaponID._SMOKE_AN_M8_HC:
                case WeaponID._FRAG_M67: return 1;
                case WeaponID._FLASH_M84: return 2;
            }
        }

        return 0;
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
        ResetADS(); // Chết → thoát ADS
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
        Grenade_E = false;
        Grenade_Q = false;
        QKey = false;
        EKey = false;
    }
#endregion
}