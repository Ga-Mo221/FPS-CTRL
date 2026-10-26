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
    // QKey/EKey truoc la bien thuong nhung ghi trong FixedUpdateNetwork
    // -> lech khi client resimulate. Nay gop ca 4 co (Grenade_Q/E da dung + dang giu Q/E) vao 1 byte networked:
    // duoc rollback dung khi resimulate, va 2 NetworkBool (2 word) con 1 word.
    [Networked] private byte _grenadeFlags { get; set; }
    private const byte GF_USED_Q = 1, GF_USED_E = 2, GF_HOLD_Q = 4, GF_HOLD_E = 8;
    private bool GetGF(byte f) { return (_grenadeFlags & f) != 0; }
    private void SetGF(byte f, bool v) { _grenadeFlags = v ? (byte)(_grenadeFlags | f) : (byte)(_grenadeFlags & ~f); }
    public bool Grenade_Q { get { return GetGF(GF_USED_Q); } set { SetGF(GF_USED_Q, value); } }
    public bool Grenade_E { get { return GetGF(GF_USED_E); } set { SetGF(GF_USED_E, value); } }
    public bool QKey      { get { return GetGF(GF_HOLD_Q); } set { SetGF(GF_HOLD_Q, value); } }
    public bool EKey      { get { return GetGF(GF_HOLD_E); } set { SetGF(GF_HOLD_E, value); } }
    [Networked] public NetworkBool IsTurnInPlaceActive { get; private set; }
    // IsJump -> JumpCount: truoc day Render() GHI IsJump=false vao bien networked
    // (khong hop le, client co the mat event nhay). Nay dung bo dem, Render so sanh voi gia tri local.
    // (JumpCount: xem _eventCounters)
    [Networked] public float NetworkedPitch { get; private set; }
    // ── Fire state — thay IsFiring(bool) bằng FireCount(int) để tiết kiệm băng thông
    // IsFiring chỉ cần 1 tick để NShootHandler đọc trong cùng FixedUpdateNetwork,
    // không cần sync ra ngoài. FireCount sync để remote clients spawn visual.
    // AimForward sync để Host/remote dùng cùng hướng bắn chính xác từ InputData.
    // FireCount / SwitchCount / JumpCount gop thanh 3 byte trong 1 word.
    // Gia tri 0..255 quay vong: noi doc chi so sanh != hoac lay delta (byte)(moi - cu).
    [Networked] private uint _eventCounters { get; set; }
    private int GetCounter(int shift) { return (int)((_eventCounters >> shift) & 0xFF); }
    private void BumpCounter(int shift) { _eventCounters = (_eventCounters & ~(0xFFu << shift)) | ((uint)((GetCounter(shift) + 1) & 0xFF) << shift); }
    public int FireCount   { get { return GetCounter(0); } }   // tăng mỗi phát bắn
    public int SwitchCount { get { return GetCounter(8); } }
    public int JumpCount   { get { return GetCounter(16); } }
    // Bo [Networked]: host va client predict deu gan lai tu GetInput moi tick,
    // proxy khong dung (proxy da co TargetYaw + NetworkedPitch). Tiet kiem 3 word dirty gan nhu moi tick.
    public Vector3 AimForward { get; private set; }  // hướng bắn từ NInputData
    // Bo ShotInfo (~57 word) khoi state cua MOI player.
    // Chi sync ShotSeed (ghi cung tick voi FireCount, chi shotgun) de host/client predict/proxy ra CUNG huong pellet;
    // proxy tu raycast cuc bo de dung visual (damage van do host tinh bang LagCompensation).
    [Networked] public ushort ShotSeed { get; private set; }

    // SHOOTING STATE
    // IsFiring chỉ dùng nội bộ trong frame FixedUpdateNetwork, không cần Networked
    public bool IsFiring { get; private set; }
    [Networked] public bool RightPuch { get; private set; }
    private bool _rightPuchDelay = false;

    [Networked] public int ShootIndex { get; private set; } = 0;

    // Toàn bộ gating combo dao CHỈ dựa vào state networked (ShootIndex, _meleeQueued, các TickTimer),
    // không dùng cờ thường / animation event (chạy riêng từng máy, khác thời điểm → host/client lệch).
    [Networked] private TickTimer _meleeCooldownTimer { get; set; } // sớm nhất được lên bậc combo tiếp
    [Networked] private TickTimer _meleeComboTimer    { get; set; } // hết combo → reset ShootIndex
    [Networked] private TickTimer _meleeSwingEndTimer { get; set; } // lúc clip đòn hiện tại kết thúc
    [Networked] private byte _meleeQueued { get; set; }              // số click đang chờ
    private const int MELEE_MAX_COMBO = 3;
    [Tooltip("Độ dài clip từng đòn (SHOOT, SHOOT_2, SHOOT_3) trong Animator, dùng để biết khi nào đòn sau bắt đầu.")]
    [SerializeField] private float[] _meleeSwingLengths = { 0.43f, 0.63f, 0.63f };
    [Tooltip("Chờ (giây) sau khi 1 đòn bắt đầu rồi mới được tăng ShootIndex cho đòn kế tiếp.")]
    [SerializeField] private float _meleeAdvanceDelay = 0.1f;
    [Tooltip("Sau khi đòn cuối hết clip, chờ thêm (giây) rồi mới reset combo về 0.")]
    [SerializeField] private float _meleeComboResetGrace = 0.3f;

    // Thêm networked timer — Host nhớ "đang trong trạng thái firing"
    // Mỗi khi nhận được Attack=true, gia hạn timer thêm N tick
    // buffer ~100ms chi host dung -> field thuong (mat khi migration khong sao)
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
    // Khóa nhận lệnh ném mới sau khi thả Q/E, chờ animation ném chạy xong (ThrowGrenade từ animation event).
    [Networked] private TickTimer _grenadeLockTimer { get; set; }
    // Giữ nút ngắm trong lúc trượt / đổi súng / ném → bật ngắm khi hết trạng thái chặn (xem ADSHandler).
    [Networked] private NetworkBool _adsQueued { get; set; }
    [Tooltip("Thời gian (giây) không nhận lệnh ném mới sau khi thả Q/E, chờ animation ném của lần trước.")]
    [SerializeField] private float _grenadeThrowLock = 1.2f;
    // _currentSpeed/_acceleration: tinh va dung ngay trong tick -> bien local (xem FixedUpdateNetwork)
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
    // He so ADS la SETTING, gui 1 lan luc spawn / khi doi setting
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

        var weaponMode = _playerMotionController.ActiveWeaponMode();

        // Đổi sang súng giữa combo dao → MeleeComboHandler không chạy nữa, phải tự reset.
        if (weaponMode != GunRecoilController.FiringMode.Melee && (ShootIndex != 0 || _meleeQueued != 0))
            ResetMeleeComboState();

        switch (weaponMode)
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
            canShoot = ctrl.WeaponType == WeaponType.Melee && !_rightPuchDelay
                ? ctrl.MeleeSwing()   // chém trái: đã gate bằng state networked trong MeleeComboHandler
                : ctrl.OnFire(_rightPuchDelay, StateFl.IsCrouch);
        StateFl = state;
        _rightPuchDelay = false;
        return canShoot;
    }

    /// <summary>
    /// Xử lý combo melee 3 đòn. ShootIndex: 0 = idle, 1..3 = đòn đang/sắp chạy.
    ///
    /// Animator chuyển đòn KHÔNG dùng trigger mà đọc ShootIndex ở cuối mỗi đòn
    /// (SHOOT → "___" → SHOOT_2 nếu ShootIndex == 2, ngược lại về NULL). Vì vậy:
    ///  • ShootIndex phải lên N+1 TRONG LÚC đòn N đang chạy (không thì combo bị cắt),
    ///  • nhưng KHÔNG được vượt quá N+1 trước khi đòn N+1 bắt đầu ("___" chỉ nhận == hoặc &lt;, sẽ kẹt).
    /// → Click được xếp hàng (_meleeQueued), mỗi lần chỉ tăng 1 bậc khi đòn hiện tại đã bắt đầu.
    /// Thời điểm bắt đầu/kết thúc mỗi đòn tính theo độ dài clip (_meleeSwingLengths) bằng TickTimer
    /// networked → host và client predict ra cùng ShootIndex.
    /// </summary>
    private void MeleeComboHandler(NInputData input)
    {
        if (RightPuch)
            return;

        IsFiring = false;

        // Combo đã chạy xong (đòn cuối hết + grace) và không còn click chờ → về idle.
        if (ShootIndex > 0 && _meleeQueued == 0 && _meleeComboTimer.Expired(Runner))
        {
            ShootIndex = 0;
            _meleeComboTimer = default;
            _meleeCooldownTimer = default;
            _meleeSwingEndTimer = default;
        }

        // Xếp hàng click (tối đa đủ 3 đòn tính cả đòn đang chạy).
        if (input.Buttons.WasPressed(_previousButtons, InputButtons.Attack)
            && ShootIndex + _meleeQueued < MELEE_MAX_COMBO)
        {
            _meleeQueued++;
        }

        // Tiến lên đòn tiếp theo khi đòn hiện tại đã thực sự bắt đầu.
        if (_meleeQueued > 0 && _meleeCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            _meleeQueued--;

            // Đòn 1 bắt đầu ngay (trigger IsShooting qua FireCount); đòn sau bắt đầu khi đòn trước hết clip.
            float startDelay = 0f;
            if (ShootIndex > 0)
                startDelay = _meleeSwingEndTimer.RemainingTime(Runner) ?? 0f;
            else
                IsFiring = true;

            ShootIndex++;

            float swingLength = MeleeSwingLength(ShootIndex);
            _meleeSwingEndTimer = TickTimer.CreateFromSeconds(Runner, startDelay + swingLength);
            _meleeComboTimer    = TickTimer.CreateFromSeconds(Runner, startDelay + swingLength + _meleeComboResetGrace);
            // Chỉ cho lên bậc tiếp sau khi đòn này đã vào state trong Animator.
            _meleeCooldownTimer = TickTimer.CreateFromSeconds(Runner, startDelay + _meleeAdvanceDelay);
        }
    }

    private void ResetMeleeComboState()
    {
        ShootIndex = 0;
        _meleeQueued = 0;
        _meleeCooldownTimer = default;
        _meleeComboTimer = default;
        _meleeSwingEndTimer = default;
    }

    private float MeleeSwingLength(int index)
    {
        int i = index - 1;
        if (_meleeSwingLengths == null || i < 0 || i >= _meleeSwingLengths.Length) return 0.5f;
        return _meleeSwingLengths[i];
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
                ADSLookSpeedMultiplier = 1f; // đấm phải bằng dao: không giảm tốc xoay
            }
        }
        else
        {
            bool held = input.Buttons.IsSet(InputButtons.ADS);

            // Đang trượt / đổi súng / ném: chưa ngắm được. Trước đây bỏ qua luôn lần bấm → giữ chuột phải
            // suốt lúc trượt thì trượt xong vẫn không ngắm. Giờ ghi nhớ (networked) "đang giữ ngắm" và
            // bật ngắm ngay khi hết trạng thái chặn. Không áp cho nạp đạn: EnterADS huỷ reload.
            if (state.IsSwitching || state.IsThrow || state.IsSlide)
            {
                _adsQueued = held;
                if (input.Buttons.WasReleased(_previousButtons, InputButtons.ADS))
                    _stateLogic.ExitADS(ref state);
                return;
            }

            bool queuedHeld = _adsQueued && held;
            _adsQueued = false;

            if (input.Buttons.WasPressed(_previousButtons, InputButtons.ADS) || (queuedHeld && !state.IsADS))
            {
                _stateLogic.EnterADS(ref state, _constraintManager);
                // Luôn gán theo vũ khí HIỆN TẠI (trước đây loại khác giữ nguyên hệ số cũ của lần ngắm trước).
                ADSLookSpeedMultiplier = AdsMultiplierFor(_weaponManager.Weapon_Active()?.WeaponType);
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
        _meleeQueued = 0;
        _meleeSwingEndTimer = default;
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
    /// Goi tu may local (InputAuthority) luc spawn / khi doi setting.
    /// Luu ngay he so ADS local (de client predict dung) roi gui 1 RPC duy nhat len host.
    /// </summary>
    public void SendLookSettings(float camSpeed, float sniperAds, float rifleAds)
    {
        if (Object == null || !Object.IsValid || !Object.HasInputAuthority) return;
        ApplyLookSettings(camSpeed, sniperAds, rifleAds);
        if (!Object.HasStateAuthority) Rpc_SetLookSettings(camSpeed, sniperAds, rifleAds);
    }

    /// <summary>Luu he so ADS (moi may) va CameraSpeed (chi host). Goi tu Rpc_SetLookSettings / NPlayer.Rpc_InitPlayer.</summary>
    // Hệ số tối thiểu: slider setting kéo về 0 thì ngắm sẽ không xoay được → kẹp lại.
    private const float MIN_ADS_MULTIPLIER = 0.05f;

    private float AdsMultiplierFor(WeaponType? type)
    {
        if (type == WeaponType.Sniper) return _sniperAds;
        if (type == WeaponType.Rifle || type == WeaponType.Shotgun) return _rifleAds;
        return 1f;
    }

    public void ApplyLookSettings(float camSpeed, float sniperAds, float rifleAds)
    {
        _sniperAds = Mathf.Max(MIN_ADS_MULTIPLIER, sniperAds);
        _rifleAds  = Mathf.Max(MIN_ADS_MULTIPLIER, rifleAds);
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
            // Lần ném trước chưa xong (animation chưa tới event ném) → bỏ qua lệnh mới.
            if (!_grenadeLockTimer.ExpiredOrNotRunning(Runner)) return;

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
                // KHÔNG đánh dấu Grenade_Q/E đã dùng ở đây: nếu animation ném không chạy được
                // (đang di chuyển, Spine layer chưa về NULL...) thì lựu đạn bị mất oan.
                // Việc đánh dấu chuyển sang OnGrenadeThrown(), gọi khi ThrowGrenade() thực sự spawn lựu đạn.
                QKey = false;
                EKey = false;
                GrenadeID = 0;
                _stateLogic.StopThrow(ref state);
                _grenadeLockTimer = TickTimer.CreateFromSeconds(Runner, _grenadeThrowLock);
            }
        }
    }

    /// <summary>
    /// Host: gọi từ NShootHandler.ThrowGrenade() sau khi lựu đạn đã được spawn.
    /// slot: 1 = Q, 2 = E (giống SetGrenadeQ_E).
    /// </summary>
    public void OnGrenadeThrown(byte slot)
    {
        if (!Object.HasStateAuthority) return;
        if (slot == 1) Grenade_Q = true;
        else if (slot == 2) Grenade_E = true;
        _grenadeLockTimer = TickTimer.None; // ném xong → nhận lệnh ném tiếp ngay
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

    
    // [Aemy 2026-10-26] Tay trai IK = ham cua StateFl (tat khi chay nhanh / truot), khong phai su kien.
    // Moi truong hop doi state (ke ca RequestRespawn reset StateFl, rollback/resim cua Fusion) deu tu dung.
    public override void Render()
    {
        if (_constraintManager == null) return;
        var s = StateFl;
        _constraintManager.SetLeftHandIK(!(s.IsSprint || s.IsSlide));
    }

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
        GrenadeID = 0;
        _grenadeLockTimer = TickTimer.None;
        ResetMeleeComboState();
    }
#endregion
}