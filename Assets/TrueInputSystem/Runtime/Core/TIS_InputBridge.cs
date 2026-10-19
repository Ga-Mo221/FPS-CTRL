// ============================================================
//  TrueInputSystem — TISInputBridge.cs
//
//  P3 FIX #15: Refactored thành thin facade.
//  Trách nhiệm của TISInputBridge sau refactor:
//    1. Singleton lifecycle & MonoBehaviour hooks  (Awake / OnDestroy / Update / LateUpdate)
//    2. Profile hot-swap                           (SwapProfile)
//    3. Action map enable / disable                (SetActionMapActive)
//    4. Gesture proxy                              (GestureActiveFingers, ...)
//    5. Object tracker proxy                       (ToucherIsSingleTouch, ...)
//    6. Gamepad rumble                             (TryRumble, StopRumble)
//    7. Expose ITISInputBridge API                 (delegate sang sub-classes)
//
//  Logic bị tách ra:
//    TISProfileBuilder  — xây dựng InputActionAsset từ profile
//    TISActionReader    — cache + đọc action (Button / Vector2 / Axis)
//    TISOverrideChannel — Move / Look / Zoom override values
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Input Bridge")]
    [DefaultExecutionOrder(-100)]
    public class TISInputBridge : MonoBehaviour, ITISInputBridge
    {
        // ══════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════

        [Tooltip("TISInputProfileConfig asset chứa cấu hình input.")]
        [SerializeField] private TISInputProfileConfig _profile;

        [Tooltip("Bật để log chi tiết trong Console khi khởi tạo.")]
        [SerializeField] private bool _verboseLog = false;

        [Tooltip("Bật để giữ GameObject này tồn tại khi chuyển scene (DontDestroyOnLoad).\n" +
                 "Khi ON: TISInputBridge sẽ không bị hủy khi load scene mới.\n" +
                 "Khi OFF (mặc định): hành vi bình thường theo scene lifecycle.")]
        [SerializeField] private bool _dontDestroyOnLoad = false;

        [Tooltip("Bật để test TISMobileSkillButton trong Editor.\n" +
                 "Khi ON: GetSkillAction() đọc TISMobileButtonRegistry thay vì keyboard.\n" +
                 "Khi OFF (mặc định): keyboard/mouse hoạt động bình thường.")]
        [SerializeField] private bool _simulateMobileInEditor = false;

        [Tooltip("Bật để log ra Console mỗi khi GetSkillAction() trả về true.")]
        [SerializeField] private bool _debugSkillAction = false;

        [Tooltip("Bật theo dõi touch vào GameObject, double-tap object và double-tap màn hình.\n" +
                 "Khi OFF: các API Toucher*() không hoạt động.")]
        [SerializeField] private bool _enableObjectTracker = false;

        [Tooltip("Camera dùng để Raycast. Để trống → tự dùng Camera.main.")]
        [SerializeField] private Camera _trackerCamera;

        [Range(0.1f, 1.0f)]
        [Tooltip("Khoảng thời gian tối đa (giây) giữa 2 lần tap để tính là double-tap.")]
        [SerializeField] private float _trackerDoubleTapInterval = 0.35f;

        [Range(10f, 150f)]
        [Tooltip("Khoảng cách pixel tối đa giữa 2 tap để tính là double-tap vào cùng vùng.")]
        [SerializeField] private float _trackerDoubleTapDistancePx = 60f;

        [Tooltip("Layer mask cho Raycast 3D. Mặc định: Everything.")]
        [SerializeField] private LayerMask _trackerLayerMask = ~0;

        [Tooltip("Khoảng cách Raycast tối đa (3D).")]
        [SerializeField] private float _trackerRaycastMaxDistance = 1000f;

        [Tooltip("Bật để dùng Physics2D.Raycast thay cho Physics.Raycast (game 2D).")]
        [SerializeField] private bool _trackerUse2DPhysics = false;

        [Tooltip("Bật để log chi tiết từ Object Tracker ra Console.")]
        [SerializeField] private bool _trackerDebugLog = false;

        // ══════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Singleton instance duy nhất của <see cref="TISInputBridge"/>.
        /// Tự động được gán trong <c>Awake</c> và xóa khi bị hủy.
        /// </summary>
        public static TISInputBridge Instance { get; private set; }

        // ══════════════════════════════════════════════════════
        //  SUB-SYSTEMS (P3 FIX #15)
        // ══════════════════════════════════════════════════════

        private readonly TISActionReader    _reader   = new TISActionReader();
        private readonly TISOverrideChannel _override = new TISOverrideChannel();
        private readonly TISMotionSystem    _motion   = new TISMotionSystem();

        // ══════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════

        private InputActionAsset               _asset;
        private TISInputProfileConfig          _activeProfile;
        private Dictionary<string, InputActionMap> _mapCache =
            new Dictionary<string, InputActionMap>(StringComparer.OrdinalIgnoreCase);

        private TISMultiFingerGestureDetector _gestureDetector;
        private TISObjectTracker              _objectTracker;

        private TISGamepadVirtualCursor       _gamepadVirtualCursor;
        
        private bool _isMobileMode;
        private TISActiveDevice _activeDevice = TISActiveDevice.KeyboardMouse;
        
        // ══════════════════════════════════════════════════════
        //  AIM ASSIST && DEADZONE
        // ══════════════════════════════════════════════════════

        private readonly TISAimAssist     _aimAssist = new TISAimAssist();
        public Transform AimAssistTarget { get; set; }
        private TISInputDeadzone      _inputDeadzone = new TISInputDeadzone();

        // ══════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Được gọi sau khi <see cref="SwapProfile"/> hoàn tất thành công.
        /// Truyền vào <see cref="TISInputProfileConfig"/> đang active mới.
        /// </summary>
        public event Action<TISInputProfileConfig> OnProfileSwapped;
        
        // Delegate — mặc định trả false, Editor có thể inject trước Awake
        public static Func<bool> SimulatorCheckerOverride = null;

        // ══════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Bản trùng (thường do scene mới có bridge trong khi bridge cũ DontDestroyOnLoad).
                // Vẫn huỷ cả GameObject (tránh 2 bộ mobile UI / setup cùng chạy), nhưng đánh dấu
                // để Update / OnDestroy của bản trùng KHÔNG đụng vào bridge thật (rumble, sự kiện...).
                _isDuplicate = true;
                enabled      = false;
                Debug.LogWarning(
                    $"[TISInputBridge] Duplicate trên \"{name}\" — huỷ GameObject này (kể cả component khác trên nó). " +
                    $"Bridge đang dùng: \"{Instance.name}\".", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _aimAssist.ActiveDeviceProvider = () => _activeDevice;
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
            
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            if (_profile == null)
            {
                Debug.LogWarning(
                    "[TISInputBridge] Profile chưa assign! Đang dùng DefaultProfile.\n" +
                    "→ Assign TISInputProfileConfig.asset vào field Profile của TISInputBridge.");
                _profile = TISInputProfileConfig.CreateDefault();
            }
            if (Application.isEditor && SimulatorCheckerOverride != null)
                _simulateMobileInEditor = SimulatorCheckerOverride.Invoke();

            _isMobileMode = Application.isMobilePlatform
                            || (Application.isEditor && _simulateMobileInEditor);

            InitialiseWithProfile(_profile);
            InitialiseObjectTracker();

            //  Mo game khi dang cam tay cam -> bat dau o che do tay cam
            // (menu/TopBar tu chon san, khong mat lan bam dau de "danh thuc" tay cam).
            if (!_isMobileMode && Gamepad.current != null) _activeDevice = TISActiveDevice.Gamepad;
            InputSystem.onDeviceChange += OnDeviceChangeAemy;
        }

        // Cam tay cam vao luc dang chay -> chuyen sang che do tay cam ngay.
        private void OnDeviceChangeAemy(InputDevice device, InputDeviceChange change)
        {
            if (_isMobileMode) return;
            if (change == InputDeviceChange.Added && device is Gamepad) _activeDevice = TISActiveDevice.Gamepad;
        }

        private void OnEnable()  => _asset?.Enable();
        private void OnDisable() => _asset?.Disable();

        private bool _isDuplicate;

        private void OnDestroy()
        {
            // Bản trùng chưa khởi tạo gì → không tắt rung / gỡ sự kiện của bridge thật
            if (_isDuplicate) return;

            StopBuiltInMotionSources();
            _objectTracker?.Dispose();
            StopRumble();
            if (_asset != null)
            {
                _asset.Disable();
                Destroy(_asset);
                _asset = null;
            }
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            InputSystem.onDeviceChange -= OnDeviceChangeAemy;
            if (_activeProfile != null) Destroy(_activeProfile);   // runtime copy do bridge tạo
            if (Instance == this) Instance = null;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // P3 FIX #18: clear registry khi scene unload tránh button stuck-pressed.
            TISMobileButtonRegistry.ClearAll();
        }

        private void Update()
        {
            _reader.SampleTriggers();                // trạng thái LT/RT lấy mẫu 1 lần, đầu frame
            UpdateActiveDevice();
            SyncBuiltInMotionSources();
            _motion.Tick(Time.unscaledDeltaTime);
            PushMotionDevice();                      // gyro / touchpad → <TISMotion> (đọc được ngay frame này)

            if (_enableObjectTracker)
                _objectTracker?.Tick();
        }

        private void LateUpdate()
        {
            TISMobileButtonRegistry.FlushFrame();
            _override.ResetFrameFlags();           // P2 FIX #10 — reset zoom consumed flag
        }

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — MOVEMENT / CAMERA
        // ══════════════════════════════════════════════════════

        /// <summary>Hướng di chuyển hiện tại (WASD / left-stick / joystick ảo). Override nếu <see cref="OverrideMoveInput"/> đang active.</summary>
        public Vector2 MoveDirection => _override.MoveDirection;

        /// <summary>Delta camera look (mouse / right-stick / swipe). Override nếu <see cref="OverrideLookInput"/> đang active.</summary>
        public Vector2 LookDelta     => _override.LookDelta;

        /// <summary>Delta zoom (scroll wheel / pinch). Override nếu <see cref="OverrideZoomInput"/> đang active.</summary>
        public float   ZoomDelta     => _override.ZoomDelta;

        /// <summary>Trả về <c>true</c> khi action "Jump" đang được nhấn (Hold mode).</summary>
        public bool IsJumping   => GetSkillAction("Jump");

        /// <summary>Trả về <c>true</c> khi action "Run" đang được nhấn (Hold mode).</summary>
        public bool IsRunning   => GetSkillAction("Run");

        /// <summary>Trả về <c>true</c> khi action "Attack" đang được nhấn (Hold mode).</summary>
        public bool IsAttacking => GetSkillAction("Attack");

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — SKILL ACTIONS
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra action theo tên với mode mặc định được cấu hình trong profile.
        /// Tự động đọc từ <see cref="TISMobileButtonRegistry"/> khi ở mobile mode.
        /// </summary>
        /// <param name="actionName">Tên action (phân biệt hoa/thường không quan trọng).</param>
        /// <returns><c>true</c> nếu action đang active trong frame này.</returns>
        public bool GetSkillAction(string actionName)
            => _reader.GetSkillAction(actionName);

        /// <summary>
        /// Kiểm tra action theo tên với <paramref name="mode"/> chỉ định rõ ràng.
        /// </summary>
        /// <param name="actionName">Tên action.</param>
        /// <param name="mode">Chế độ đọc: <see cref="TISButtonReadMode.PressDown"/>, <see cref="TISButtonReadMode.Hold"/>, hoặc <see cref="TISButtonReadMode.PressUp"/>.</param>
        /// <returns><c>true</c> nếu điều kiện <paramref name="mode"/> thoả mãn trong frame này.</returns>
        public bool GetSkillAction(string actionName, TISButtonReadMode mode)
            => _reader.GetSkillAction(actionName, mode);

        /// <summary>Trả về <c>true</c> đúng frame khi action <paramref name="actionName"/> bắt đầu được nhấn (PressDown).</summary>
        public bool IsSkillDown(string actionName)     => GetSkillAction(actionName, TISButtonReadMode.PressDown);

        /// <summary>Trả về <c>true</c> trong toàn bộ thời gian action <paramref name="actionName"/> đang được giữ (Hold).</summary>
        public bool IsSkillHeld(string actionName)     => GetSkillAction(actionName, TISButtonReadMode.Hold);

        /// <summary>Trả về <c>true</c> đúng frame khi action <paramref name="actionName"/> được thả ra (PressUp).</summary>
        public bool IsSkillReleased(string actionName) => GetSkillAction(actionName, TISButtonReadMode.PressUp);

        /// <summary>Đọc giá trị Vector2 của action (ví dụ: axis 2D, joystick ảo).</summary>
        /// <param name="actionName">Tên action kiểu Vector2.</param>
        public Vector2 GetVector2Action(string actionName) => _reader.GetVector2Action(actionName);

        /// <summary>Đọc giá trị float 1D của action (ví dụ: scroll, trigger).</summary>
        /// <param name="actionName">Tên action kiểu Axis.</param>
        public float   GetAxisAction(string actionName)    => _reader.GetAxisAction(actionName);

        // ── Handle API: lấy 1 lần, đọc mỗi frame không tra string ──
        //   TISActionHandle _jump;
        //   void Start()  => _jump = TISInputBridge.Instance.GetActionHandle("Jump");
        //   void Update() { if (TISInputBridge.Instance.IsSkillDown(_jump)) ... }

        /// <summary>
        /// Lấy handle cho action <paramref name="actionName"/> để đọc nhanh mỗi frame qua các overload nhận
        /// <see cref="TISActionHandle"/>. Kết quả giống hệt overload string; handle vẫn đúng sau khi swap profile.
        /// </summary>
        public TISActionHandle GetActionHandle(string actionName) => _reader.GetHandle(actionName);

        /// <inheritdoc cref="GetSkillAction(string)"/>
        public bool GetSkillAction(in TISActionHandle action) => _reader.GetSkillAction(in action);

        /// <inheritdoc cref="GetSkillAction(string, TISButtonReadMode)"/>
        public bool GetSkillAction(in TISActionHandle action, TISButtonReadMode mode) => _reader.GetSkillAction(in action, mode);

        /// <inheritdoc cref="IsSkillDown(string)"/>
        public bool IsSkillDown(in TISActionHandle action)     => _reader.GetSkillAction(in action, TISButtonReadMode.PressDown);

        /// <inheritdoc cref="IsSkillHeld(string)"/>
        public bool IsSkillHeld(in TISActionHandle action)     => _reader.GetSkillAction(in action, TISButtonReadMode.Hold);

        /// <inheritdoc cref="IsSkillReleased(string)"/>
        public bool IsSkillReleased(in TISActionHandle action) => _reader.GetSkillAction(in action, TISButtonReadMode.PressUp);

        /// <inheritdoc cref="GetVector2Action(string)"/>
        public Vector2 GetVector2Action(in TISActionHandle action) => _reader.GetVector2Action(in action);

        /// <inheritdoc cref="GetAxisAction(string)"/>
        public float   GetAxisAction(in TISActionHandle action)    => _reader.GetAxisAction(in action);

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — MOBILE OVERRIDE
        // ══════════════════════════════════════════════════════

        /// <summary>Ghi đè hướng di chuyển bằng <paramref name="value"/> (dùng cho joystick ảo mobile).</summary>
        public void OverrideMoveInput(Vector2 value) => _override.OverrideMoveInput(value);

        /// <summary>Xóa override di chuyển, trả về đọc từ InputSystem bình thường.</summary>
        public void ClearMoveOverride()              => _override.ClearMoveOverride();

        /// <summary>Ghi đè delta camera look bằng <paramref name="delta"/> (dùng cho vùng swipe camera mobile).</summary>
        public void OverrideLookInput(Vector2 delta) => _override.OverrideLookInput(delta);

        /// <summary>Xóa override look, trả về đọc từ InputSystem bình thường.</summary>
        public void ClearLookOverride()              => _override.ClearLookOverride();

        /// <summary>Ghi đè giá trị của một action Vector2 theo tên.</summary>
        public void OverrideVector2Action(string actionName, Vector2 value)
            => TISDirectionalRegistry.SetVector2Override(actionName, value);

        /// <summary>Xóa override của action Vector2, trả về đọc InputSystem bình thường.</summary>
        public void ClearVector2Override(string actionName)
            => TISDirectionalRegistry.ClearVector2Override(actionName);

        /// <summary>Ghi đè delta zoom bằng <paramref name="delta"/> (dùng cho pinch-to-zoom mobile).</summary>
        public void OverrideZoomInput(float delta)   => _override.OverrideZoomInput(delta);

        /// <summary>Xóa override zoom, trả về đọc từ InputSystem bình thường.</summary>
        public void ClearZoomOverride()              => _override.ClearZoomOverride();

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — ACTION MAP CONTROL
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Bật hoặc tắt một action map theo tên.
        /// </summary>
        /// <param name="mapName">Tên của action map (không phân biệt hoa/thường).</param>
        /// <param name="active"><c>true</c> để enable, <c>false</c> để disable.</param>
        public void SetActionMapActive(string mapName, bool active)
        {
            if (_mapCache.TryGetValue(mapName, out var map))
            {
                if (active) map.Enable();
                else        map.Disable();
            }
            else
            {
                Debug.LogWarning(
                    $"[TISInputBridge] SetActionMapActive: không tìm thấy map \"{mapName}\".");
            }
        }

        /// <summary>Disable toàn bộ action map hiện có trong cache (dùng để tạm tắt input, ví dụ khi mở UI/menu).</summary>
        public void DisableAllMaps()
        {
            foreach (var map in _mapCache.Values)
                map.Disable();
        }

        /// <summary>Enable/disable các action map theo trạng thái <c>enabledByDefault</c> được cấu hình trong profile active.</summary>
        public void EnableDefaultMaps()
        {
            if (_activeProfile == null) return;
            foreach (var mapData in _activeProfile.actionMaps)
            {
                if (_mapCache.TryGetValue(mapData.mapName, out var map))
                {
                    if (mapData.enabledByDefault) map.Enable();
                    else                          map.Disable();
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — PROFILE SWAP
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Thay thế toàn bộ input profile đang chạy bằng <paramref name="newProfile"/>.
        /// Asset cũ bị hủy, cache được rebuild, mobile registry được clear.
        /// Kích hoạt <see cref="OnProfileSwapped"/> sau khi hoàn tất.
        /// </summary>
        /// <param name="newProfile">Profile mới cần chuyển sang. Không được là <c>null</c>.</param>
        public void SwapProfile(TISInputProfileConfig newProfile)
        {
            if (newProfile == null)
            {
                Debug.LogError("[TISInputBridge] SwapProfile: newProfile là null!");
                return;
            }

            if (_asset != null)
            {
                _asset.Disable();
                Destroy(_asset);
                _asset = null;
            }

            _mapCache.Clear();
            TISMobileButtonRegistry.ClearAll();
            _override.ClearAll();

            _isMobileMode = Application.isMobilePlatform
                            || (Application.isEditor && _simulateMobileInEditor);

            InitialiseWithProfile(newProfile);
            OnProfileSwapped?.Invoke(_activeProfile);

            Debug.Log($"[TISInputBridge] ✓ SwapProfile → \"{_activeProfile.profileName}\"");
        }
        
        // ══════════════════════════════════════════════════════
        //  GAMEPAD VIRTUAL CURSOR
        // ══════════════════════════════════════════════════════
        
        public void RegisterGamepadCursor(TISGamepadVirtualCursor cursor)
            => _gamepadVirtualCursor = cursor;

        public void SetActiveGamepadCursor(bool active)
            => _gamepadVirtualCursor?.SetActiveCursor(active);

        public bool GetGamepadCursorEnable()
        {
            if (_gamepadVirtualCursor == null) return false;
            return _gamepadVirtualCursor.GetEnabled();
        }

        // ══════════════════════════════════════════════════════
        //  MOTION (GYRO + TOUCHPAD)
        // ══════════════════════════════════════════════════════

        /// <summary>Gyro + touchpad của tay cầm. Gyro đã được cộng sẵn vào <see cref="LookDelta"/>.</summary>
        public TISMotionSystem Motion => _motion;

        /// <summary>
        /// Đăng ký provider gyro/touchpad (simulated, SDL3, console...).
        /// Provider CONNECTED có <see cref="ITISMotionSource.Priority"/> cao nhất được dùng.
        /// </summary>
        public void RegisterMotionSource(ITISMotionSource source)   => _motion.AddSource(source);

        /// <summary>Gỡ provider đã đăng ký.</summary>
        public void UnregisterMotionSource(ITISMotionSource source) => _motion.RemoveSource(source);

        /// <summary>
        /// Công tắc gyro lúc chạy (vd. menu Settings của game). Gyro hoạt động khi CẢ công tắc này
        /// lẫn Profile › Motion Settings › Enable Gyro bật. Áp dụng ngay frame sau.
        /// </summary>
        public bool GyroEnabled { get; set; } = true;

        /// <summary>Công tắc touchpad lúc chạy (AND với Profile › Motion Settings › Enable Touchpad).</summary>
        public bool TouchpadEnabled { get; set; } = true;

        /// <summary>Bật bảng debug gyro / touchpad lúc chạy (OR với Profile › Motion Settings › Show Debug GUI).</summary>
        public bool ShowMotionDebugGUI { get; set; }

        /// <summary>Thiết bị ảo &lt;TISMotion&gt; đang dùng (null khi profile không bind gyro / touchpad).</summary>
        public TISMotionDevice MotionDevice => _motionDevice;

        // ── Built-in providers (SDL3 + Simulated) + thiết bị ảo ─────────────

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        private TISSdl3MotionSource _sdl3Source;
#endif
        private TISSimulatedMotionSource _simulatedRegistered;
        private TISMotionDevice          _motionDevice;
        private bool                     _profileUsesMotion;   // tính lại mỗi lần build profile

        /// <summary>
        /// Tạo / huỷ provider + thiết bị ảo theo Profile › Motion Settings. Gọi mỗi frame →
        /// đổi cấu hình / swap profile lúc đang Play có hiệu lực ngay.
        /// Provider (SDL3...) CHỈ chạy khi cần: profile có binding gyro / touchpad,
        /// hoặc virtual cursor dùng touchpad / gyro.
        /// </summary>
        private void SyncBuiltInMotionSources()
        {
            var ms = _activeProfile != null ? _activeProfile.motionSettings : null;

            bool gyroOn  = ms != null && ms.enableGyro     && GyroEnabled;
            bool touchOn = ms != null && ms.enableTouchpad && TouchpadEnabled;
            _motion.GyroEnabled     = gyroOn;
            _motion.TouchpadEnabled = touchOn;

            bool cursorWants = _gamepadVirtualCursor != null && ms != null
                               && (ms.touchpadMovesCursor || ms.gyroMovesCursor);
            bool needed      = (gyroOn || touchOn) && (_profileUsesMotion || cursorWants);

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            // DualShock 4 / DualSense qua Bluetooth chỉ rung được qua SDL3 → mở SDL3 cả khi profile không dùng gyro
            bool rumbleWantsSdl = ms != null && _activeProfile.gamepadSettings.enableRumble
                                  && Gamepad.current is UnityEngine.InputSystem.DualShock.DualShockGamepad;
            bool wantSdl = (needed || rumbleWantsSdl) && ms.useSdl3OnPC;
            if (wantSdl && _sdl3Source == null)
            {
                _sdl3Source = new TISSdl3MotionSource(ms.sdl3);
                _sdl3Source.Start();
                _motion.AddSource(_sdl3Source);
            }
            else if (!wantSdl && _sdl3Source != null)
            {
                _motion.RemoveSource(_sdl3Source);
                _sdl3Source.Stop();
                _sdl3Source = null;
            }
#endif

            // Instance giả lập nằm trong profile (runtime copy) → swap profile thì đổi instance
            var wantSim = needed && ms.simulateInEditor && Application.isEditor ? ms.simulated : null;
            if (!ReferenceEquals(_simulatedRegistered, wantSim))
            {
                if (_simulatedRegistered != null) _motion.RemoveSource(_simulatedRegistered);
                if (wantSim != null) { wantSim.ResetState(); _motion.AddSource(wantSim); }
                _simulatedRegistered = wantSim;
            }

            // Thiết bị ảo chỉ cần khi có binding <TISMotion> / "+ Gyro" / "+ Touchpad"
            bool wantDevice = _profileUsesMotion && (gyroOn || touchOn);
            if (wantDevice && _motionDevice == null)
            {
                TISMotionDevice.EnsureRegistered();
                _motionDevice = InputSystem.AddDevice<TISMotionDevice>();
            }
            else if (!wantDevice && _motionDevice != null)
            {
                RemoveMotionDevice();
            }
        }

        /// <summary>Ghi gyro / touchpad đã xử lý vào thiết bị ảo — action đọc được ngay trong frame.</summary>
        private void PushMotionDevice()
        {
            if (_motionDevice == null || !_motionDevice.added) return;

            // Virtual cursor đang bật: touchpad / gyro điều khiển con trỏ, không xoay camera
            bool cursorActive = GetGamepadCursorEnable();

            // Người chơi đang dùng bàn phím-chuột / cảm ứng → bỏ gyro / touchpad của tay cầm nằm trên bàn.
            // Nguồn giả lập (phím I J K L, T + chuột) vốn điều khiển bằng bàn phím → không chặn.
            var ms = _activeProfile?.motionSettings;
            bool padIdle = ms != null && ms.onlyWhenGamepadActive
                           && _activeDevice != TISActiveDevice.Gamepad
                           && !(_motion.Source is TISSimulatedMotionSource);

            bool touching     = _motion.HasTouchpad && _motion.IsTouching && !cursorActive && !padIdle;
            bool pressed      = _motion.HasTouchpad && _motion.TouchpadPressed && !cursorActive && !padIdle;

            var state = new TISMotionState
            {
                gyro             = padIdle ? Vector2.zero : _motion.GyroAim,   // đã gate cursor + activation trong TISMotionSystem
                touchpad         = touching ? _motion.TouchpadVelocity : Vector2.zero,
                touchpadPosition = touching ? _motion.TouchpadPosition : Vector2.zero,
                buttons          = (touching ? 1u : 0u) | (pressed ? 2u : 0u),
            };
            _motionDevice.Push(state);
        }

        private void RemoveMotionDevice()
        {
            if (_motionDevice != null && _motionDevice.added)
                InputSystem.RemoveDevice(_motionDevice);
            _motionDevice = null;
        }

        private void StopBuiltInMotionSources()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            if (_sdl3Source != null)
            {
                _motion.RemoveSource(_sdl3Source);
                _sdl3Source.Stop();
                _sdl3Source = null;
            }
#endif
            if (_simulatedRegistered != null)
            {
                _motion.RemoveSource(_simulatedRegistered);
                _simulatedRegistered = null;
            }
            RemoveMotionDevice();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Chỉ khai báo OnGUI trong Editor / Development Build — Unity gọi OnGUI mỗi frame
        // nếu method tồn tại (kể cả khi không vẽ gì), nên bỏ hẳn khỏi bản release.
        private void OnGUI()
        {
            if (ShowMotionDebugGUI || (_activeProfile != null && _activeProfile.motionSettings != null
                                       && _activeProfile.motionSettings.showDebugGUI))
                TISMotionDebugGUI.Draw(this);
        }
#endif

        private readonly HashSet<string> _missingMotionActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // GetSkillAction log warning mỗi lần gọi với tên không tồn tại → kiểm tra trước, chỉ warn 1 lần.
        private bool IsMotionActivationHeld(string actionName)
        {
            bool exists = _asset?.FindAction(actionName, throwIfNotFound: false) != null
                          || _activeProfile?.FindAction(actionName) != null;
            if (!exists)
            {
                if (_missingMotionActions.Add(actionName))
                    Debug.LogWarning(
                        $"[TISInputBridge] Gyro activation action \"{actionName}\" không tồn tại trong profile.\n" +
                        "→ Sửa Motion Settings > Gyro Activation Action, hoặc đặt Gyro Activation = Always.");
                return false;
            }
            return _reader.GetSkillAction(actionName, TISButtonReadMode.Hold);
        }

        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — GESTURE PROXY
        // ══════════════════════════════════════════════════════

        /// <summary>Đăng ký gesture detector để TISInputBridge proxy các thuộc tính gesture.</summary>
        public void RegisterGestureDetector(TISMultiFingerGestureDetector detector)
            => _gestureDetector = detector;

        /// <summary>Hủy đăng ký gesture detector. Chỉ hủy nếu <paramref name="detector"/> đúng instance đang giữ.</summary>
        public void UnregisterGestureDetector(TISMultiFingerGestureDetector detector)
        {
            if (_gestureDetector == detector) _gestureDetector = null;
        }

        /// <summary>Số ngón tay đang chạm màn hình hiện tại.</summary>
        public int     GestureActiveFingers      => _gestureDetector?.ActiveFingerCount ?? 0;

        /// <summary>Vị trí centroid (tâm) của tất cả ngón tay đang chạm, tính theo pixel màn hình.</summary>
        public Vector2 GestureCentroidPosition   => _gestureDetector?.CentroidPosition ?? Vector2.zero;

        /// <summary>Delta dịch chuyển centroid so với frame trước, tính theo pixel.</summary>
        public Vector2 GestureCentroidDelta      => _gestureDetector?.CentroidDelta ?? Vector2.zero;

        /// <summary><c>true</c> khi đang nhận gesture kéo hai ngón tay song song (two-finger pan).</summary>
        public bool    GestureIsTwoFingerPanning => _gestureDetector != null && _gestureDetector.IsTwoFingerPanning;

        /// <summary>Delta pan hai ngón tay đã được làm mượt (smoothed), tính theo pixel.</summary>
        public Vector2 GestureTwoFingerPanDelta  => _gestureDetector?.TwoFingerPanDelta ?? Vector2.zero;

        /// <summary>Delta pan hai ngón tay thô (raw, không smoothed), tính theo pixel.</summary>
        public Vector2 GestureTwoFingerPanRaw    => _gestureDetector?.TwoFingerPanRaw ?? Vector2.zero;

        /// <summary>Delta dịch chuyển của ngón tay thứ nhất (index 0) so với frame trước.</summary>
        public Vector2 GestureFinger0Delta       => _gestureDetector?.Finger0Delta ?? Vector2.zero;

        /// <summary>Delta dịch chuyển của ngón tay thứ hai (index 1) so với frame trước.</summary>
        public Vector2 GestureFinger1Delta       => _gestureDetector?.Finger1Delta ?? Vector2.zero;

        /// <summary>Lấy vị trí màn hình (pixel) của ngón tay tại <paramref name="index"/>.</summary>
        /// <param name="index">Chỉ số ngón tay (0-based).</param>
        public Vector2 GestureGetFingerPosition(int index)
            => _gestureDetector != null ? _gestureDetector.GetFingerPosition(index) : Vector2.zero;

        /// <summary>
        /// Đồng bộ zone nhận gesture theo tỉ lệ màn hình dọc.
        /// </summary>
        /// <param name="zoneStartRatio">Tỉ lệ bắt đầu zone (0 = bottom, 1 = top).</param>
        /// <param name="zoneEndRatio">Tỉ lệ kết thúc zone (mặc định 1f).</param>
        public void GestureSyncZone(float zoneStartRatio, float zoneEndRatio = 1f)
            => _gestureDetector?.SetZoneRatio(zoneStartRatio, this, zoneEndRatio);

        /// <summary>Đồng bộ zone gesture theo cấu hình hiện tại của detector (không thay đổi tỉ lệ).</summary>
        public void GestureSyncZone() => _gestureDetector?.SyncZone();

        // ══════════════════════════════════════════════════════
        //  PROFILE INFO
        // ══════════════════════════════════════════════════════

        /// <summary>Profile config đang được dùng tại runtime (sau khi đã tạo runtime copy).</summary>
        public TISInputProfileConfig ActiveProfile     => _activeProfile;

        /// <summary>
        /// Profile ScriptableObject gốc được assign trong Inspector — KHÔNG bị mutate bởi rebinding.
        /// TISRebindingSystem dùng property này để luôn clone từ nguồn sạch khi RebuildBridge.
        /// </summary>
        public TISInputProfileConfig OriginalProfile   => _profile;

        /// <summary>Camera settings lấy từ <see cref="ActiveProfile"/>. <c>null</c> nếu chưa có profile.</summary>
        public TISCameraSettings     TISCameraSettings  => _activeProfile?.cameraSettings;

        /// <summary>Mobile settings lấy từ <see cref="ActiveProfile"/>. <c>null</c> nếu chưa có profile.</summary>
        public TISMobileSettings     TISMobileSettings  => _activeProfile?.mobileSettings;

        /// <summary>Gamepad settings lấy từ <see cref="ActiveProfile"/>. <c>null</c> nếu chưa có profile.</summary>
        public TISGamepadSettings    TISGamepadSettings => _activeProfile?.gamepadSettings;
        
        // ══════════════════════════════════════════════════════
        //  ITISInputBridge — ACTIVE DEVICE DETECTION
        // ══════════════════════════════════════════════════════

        public TISActiveDevice ActiveDevice => _activeDevice;

        public bool IsKeyboardMouse => _activeDevice == TISActiveDevice.KeyboardMouse;
        public bool IsGamepad       => _activeDevice == TISActiveDevice.Gamepad;
        public bool IsMobile        => _isMobileMode;

private void UpdateActiveDevice()
        {
            // Mobile luon uu tien cao nhat
            if (_isMobileMode)
            {
                // Mobile + tay cầm Bluetooth: bấm tay cầm → Gamepad (UI button điều hướng được),
                // chạm màn hình → quay về Mobile. Không có input → giữ nguyên (hysteresis).
                var mgp = Gamepad.current;
                if (mgp != null && GamepadHasInputCached(mgp)) { _activeDevice = TISActiveDevice.Gamepad; return; }

                var ts = Touchscreen.current;
                bool touched = (ts != null && ts.primaryTouch.press.isPressed)
                               // Editor giả lập mobile: chuột thật đóng vai ngón tay
                               || (Application.isEditor && TISPhysicalMouse.Current != null
                                   && TISPhysicalMouse.Current.leftButton.isPressed);
                if (_activeDevice != TISActiveDevice.Gamepad || touched)
                    _activeDevice = TISActiveDevice.Mobile;
                return;
            }

            // FIX: goi IsActuated() tren CA device khong dang tin
            // (magnitude cap-device co the tra -1 => luon false), khien menu khong bao gio
            // nhan ra tay cam => chan sach di chuyen (dpad/L1R1/L2R2) nhung nut A van chay.
            // => Poll tung control cu the voi nguong (bo stick drift).
            var gp = Gamepad.current;
            if (gp != null && GamepadHasInputCached(gp)) { _activeDevice = TISActiveDevice.Gamepad; return; }

            var kb = Keyboard.current;
            bool kbActive = kb != null
                            && (NeedsPoll(ref _kbPoll, kb) ? StorePoll(ref _kbPoll, kb, kb.anyKey.isPressed) : false);
            bool mouseActive = false;
            // Chuột ảo của virtual cursor (click bằng nút A) không được tính là KeyboardMouse
            var m = TISPhysicalMouse.Current;
            if (m != null)
                mouseActive = NeedsPoll(ref _mousePoll, m) ? StorePoll(ref _mousePoll, m, MouseHasInput(m)) : false;
            if (kbActive || mouseActive) { _activeDevice = TISActiveDevice.KeyboardMouse; return; }

            // Khong co input chu y -> giu nguyen thiet bi cu (hysteresis)
        }

        private static bool MouseHasInput(Mouse m)
        {
            if (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed) return true;
            if (m.delta.ReadValue().sqrMagnitude > 4f) return true;        // di chuyen > ~2px
            if (Mathf.Abs(m.scroll.ReadValue().y) > 0.01f) return true;
            return false;
        }

        // ── Bỏ poll khi device đứng yên ─────────────────────────
        // Kết quả poll là hàm thuần của state device, mà state chỉ đổi khi device nhận event
        // (lastUpdateTime đổi theo). → Device không có event mới VÀ lần poll trước "không có input"
        // thì kết quả chắc chắn vẫn là false: bỏ qua ~20 lần đọc control. Lần trước "có input"
        // thì luôn poll lại, vì control delta (mouse.delta / scroll) tự về 0 đầu frame mà
        // không cần event. Kết quả từng frame giống hệt poll mỗi frame.
        private struct DevicePoll
        {
            public InputDevice device;
            public double      lastUpdateTime;
            public bool        active;
        }

        private DevicePoll _gamepadPoll, _kbPoll, _mousePoll;

        private static bool NeedsPoll(ref DevicePoll p, InputDevice d)
            => p.active || !ReferenceEquals(p.device, d) || p.lastUpdateTime != d.lastUpdateTime;

        private static bool StorePoll(ref DevicePoll p, InputDevice d, bool active)
        {
            p.device         = d;
            p.lastUpdateTime = d.lastUpdateTime;
            p.active         = active;
            return active;
        }

        private bool GamepadHasInputCached(Gamepad gp)
            => NeedsPoll(ref _gamepadPoll, gp) ? StorePoll(ref _gamepadPoll, gp, GamepadHasInput(gp)) : false;

        // Poll input tay cam theo tung control (dang tin hon IsActuated cap-device).
        private static bool GamepadHasInput(Gamepad gp)
        {
            if (gp.dpad.ReadValue().sqrMagnitude > 0.1f) return true;
            if (gp.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
            if (gp.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
            if (gp.leftTrigger.ReadValue() > 0.3f || gp.rightTrigger.ReadValue() > 0.3f) return true;
            if (gp.leftShoulder.isPressed || gp.rightShoulder.isPressed) return true;
            if (gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.buttonWest.isPressed || gp.buttonNorth.isPressed) return true;
            if (gp.startButton.isPressed || gp.selectButton.isPressed) return true;
            if (gp.leftStickButton.isPressed || gp.rightStickButton.isPressed) return true;
            return false;
        }

        // ══════════════════════════════════════════════════════
        //  OBJECT TRACKER — SETUP
        // ══════════════════════════════════════════════════════

        private void InitialiseObjectTracker()
        {
            _objectTracker = new TISObjectTracker
            {
                RaycastCamera       = _trackerCamera,
                DoubleTapInterval   = _trackerDoubleTapInterval,
                DoubleTapDistancePx = _trackerDoubleTapDistancePx,
                RaycastLayerMask    = _trackerLayerMask,
                RaycastMaxDistance  = _trackerRaycastMaxDistance,
                Use2DPhysics        = _trackerUse2DPhysics,
                DebugLog            = _trackerDebugLog,
            };
        }

        /// <summary>Bật hoặc tắt Object Tracker tại runtime (tương đương thay đổi Inspector field <c>Enable Object Tracker</c>).</summary>
        public void SetObjectTrackerEnabled(bool enabled) => _enableObjectTracker = enabled;

        /// <summary>
        /// Thay camera dùng để Raycast của Object Tracker tại runtime.
        /// </summary>
        /// <param name="cam">Camera mới. <c>null</c> để tracker tự dùng <c>Camera.main</c>.</param>
        public void SetTrackerCamera(Camera cam)
        {
            _trackerCamera = cam;
            if (_objectTracker != null) _objectTracker.RaycastCamera = cam;
        }

        // ══════════════════════════════════════════════════════
        //  OBJECT TRACKER — PUBLIC API (proxy)
        // ══════════════════════════════════════════════════════

        /// <summary>Danh sách InstanceID của tất cả GameObject đang được chạm trong frame này.</summary>
        public IReadOnlyList<int> ToucherObjectIDs         => _objectTracker?.TouchedObjectIDs;

        /// <summary>Số lượng GameObject đang được chạm trong frame này.</summary>
        public int                ToucherCount             => _objectTracker?.TouchedCount ?? 0;

        /// <summary>Trả về InstanceID của GameObject được chạm gần nhất.</summary>
        public int     ToucherGetLastID()                             => _objectTracker?.GetLastTouchedID() ?? 0;

        /// <summary>Trả về tên của GameObject được chạm gần nhất. Rỗng nếu chưa có.</summary>
        public string  ToucherGetLastName()                           => _objectTracker?.GetLastTouchedName() ?? "";

        /// <summary><c>true</c> nếu không có GameObject nào đang được chạm.</summary>
        public bool    ToucherHasNoObjects()                          => _objectTracker?.HasNoTouchedObjects() ?? true;

        /// <summary><c>true</c> nếu xảy ra double-tap trên GameObject có <paramref name="instanceID"/> chỉ định.</summary>
        public bool    ToucherIsDoubleTapOnObject(int instanceID)     => _objectTracker?.IsDoubleTapOnObject(instanceID) ?? false;

        /// <summary><c>true</c> nếu xảy ra double-tap ở bất kỳ vị trí nào trên màn hình.</summary>
        public bool    ToucherIsDoubleTapOnScreen()                   => _objectTracker?.IsDoubleTapOnScreen() ?? false;

        /// <summary>Trả về GameObject vừa bị double-tap gần nhất. <c>null</c> nếu không có.</summary>
        public GameObject ToucherGetDoubleTappedObject()              => _objectTracker?.GetDoubleTappedObject();

        /// <summary>Trả về GameObject bị double-tap có <paramref name="instanceID"/> chỉ định. <c>null</c> nếu không khớp.</summary>
        public GameObject ToucherGetDoubleTappedObject(int instanceID) => _objectTracker?.GetDoubleTappedObject(instanceID);

        /// <summary><c>true</c> nếu GameObject có <paramref name="instanceID"/> đã được chạm trong frame này.</summary>
        public bool    ToucherWasTouched(int instanceID)              => _objectTracker?.WasTouched(instanceID) ?? false;

        /// <summary>Xóa danh sách các GameObject đang được theo dõi (giữ lại trạng thái double-tap).</summary>
        public void    ToucherClearList()                             => _objectTracker?.ClearTouchedList();
        /// <summary>Xóa toàn bộ trạng thái Object Tracker (list chạm, double-tap, touch count).</summary>
        public void    ToucherClearAll()                              => _objectTracker?.ClearAll();

        /// <summary>Reset trạng thái double-tap cho GameObject có <paramref name="instanceID"/> chỉ định.</summary>
        public void    ToucherResetDoubleTapForObject(int instanceID) => _objectTracker?.ResetDoubleTapForObject(instanceID);

        /// <summary>Reset trạng thái double-tap toàn màn hình.</summary>
        public void    ToucherResetDoubleTapScreen()                  => _objectTracker?.ResetDoubleTapScreen();

        /// <summary><c>true</c> khi chỉ có đúng một ngón tay đang chạm màn hình.</summary>
        public bool    ToucherIsSingleTouch()                         => _objectTracker?.IsSingleTouchOnScreen() ?? false;

        /// <summary>Trả về GameObject duy nhất đang được chạm (khi single-touch). <c>null</c> nếu không có.</summary>
        public GameObject ToucherGetSingleObject()                    => _objectTracker?.GetSingleTouchedObject();

        /// <summary>Reset bộ đếm touch, dùng để phân biệt các phiên chạm mới.</summary>
        public void    ToucherClearTouchCount()                       => _objectTracker?.ClearTouchCount();

        /// <summary>In log danh sách các GameObject đang được chạm ra Console (Debug).</summary>
        public void    ToucherLogObjects()                            => _objectTracker?.LogTouchedObjects();

        public Vector3 ToucherWorldPosition() => _objectTracker?.GetTouchWorldPosition() ??  Vector3.zero;

        /// <summary>[Editor only] Phản chiếu giá trị Inspector field <c>_simulateMobileInEditor</c>. Dùng bởi custom editor.</summary>
        public bool SimulateMobileInEditor() => _simulateMobileInEditor;
        
        // ── Drag Position API ──────────────────────────────────────────────

        /// <summary>
        /// [API 1 — UI Drag] Vị trí screen space (pixel) của pointer/touch đang giữ.
        /// Cập nhật liên tục mỗi frame.
        /// Dùng để di chuyển RectTransform khi kéo item trong Canvas UI.
        /// Vector2.zero nếu không có pointer active.
        /// </summary>
        public Vector2 GetPointerScreenPosition()
            => _objectTracker?.GetPointerScreenPosition() ?? Vector2.zero;

        /// <summary>
        /// Delta screen (pixel) của pointer so với frame trước.
        /// Dùng để tính offset kéo trong UI mà không cần lưu vị trí cũ ngoài.
        /// Vector2.zero nếu không có pointer active.
        /// </summary>
        public Vector2 GetPointerScreenDelta()
            => _objectTracker?.GetPointerScreenDelta() ?? Vector2.zero;

        /// <summary>
        /// [API 2 — World Drag] Vị trí World Space tương ứng với pointer/touch đang giữ.
        /// Dùng để kéo thả hoặc vứt GameObject trong không gian 3D hoặc 2D.
        /// <para>3D: depth = khoảng cách từ camera đến object đang kéo.</para>
        /// <para>2D (Ortho): depth = Mathf.Abs(Camera.main.transform.position.z).</para>
        /// Vector3.zero nếu không có pointer active hoặc không tìm được Camera.
        /// </summary>
        /// <param name="depth">Khoảng cách từ camera đến mặt phẳng đặt object.</param>
        public Vector3 GetPointerWorldPosition(float depth = 10f)
            => _objectTracker?.GetPointerWorldPosition(depth) ?? Vector3.zero;

        /// <summary>true khi pointer/touch đang được giữ (drag đang diễn ra).</summary>
        public bool IsPointerHeld
            => _objectTracker?.IsPointerHeld ?? false;

        // ══════════════════════════════════════════════════════
        //  RUMBLE
        // ══════════════════════════════════════════════════════

        private Coroutine _rumbleCoroutine;

        /// <summary>
        /// Phát rung gamepad trong <paramref name="duration"/> giây.
        /// Không làm gì nếu profile tắt rumble hoặc không có gamepad kết nối.
        /// </summary>
        /// <param name="lowFreq">Cường độ motor tần số thấp (0–1), thường là rung nặng.</param>
        /// <param name="highFreq">Cường độ motor tần số cao (0–1), thường là rung nhẹ.</param>
        /// <param name="duration">Thời gian rung tính bằng giây.</param>
        public void TryRumble(float lowFreq, float highFreq, float duration)
        {
            if (!(_activeProfile?.gamepadSettings.enableRumble ?? false)) return;
            if (Gamepad.current == null || duration <= 0f) return;

            lowFreq  = Mathf.Clamp01(lowFreq);
            highFreq = Mathf.Clamp01(highFreq);

            if (_rumbleCoroutine != null) StopCoroutine(_rumbleCoroutine);
            _rumbleCoroutine = StartCoroutine(RumbleCoroutine(lowFreq, highFreq, duration));
        }

        /// <summary>
        /// Phát rung gamepad liên tục (không tự dừng) cho đến khi gọi <see cref="StopRumble"/>.
        /// Không làm gì nếu profile tắt rumble hoặc không có gamepad kết nối.
        /// </summary>
        /// <param name="lowFreq">Cường độ motor tần số thấp (0–1).</param>
        /// <param name="highFreq">Cường độ motor tần số cao (0–1).</param>
        public void TryRumble(float lowFreq, float highFreq)
        {
            if (!(_activeProfile?.gamepadSettings.enableRumble ?? false)) return;
            if (Gamepad.current == null) return;

            lowFreq  = Mathf.Clamp01(lowFreq);
            highFreq = Mathf.Clamp01(highFreq);

            if (_rumbleCoroutine != null) { StopCoroutine(_rumbleCoroutine); _rumbleCoroutine = null; }
            SetMotors(lowFreq, highFreq, 0f);
        }

        /// <summary>Dừng rung gamepad ngay lập tức và hủy coroutine rung nếu đang chạy.</summary>
        public void StopRumble()
        {
            if (_rumbleCoroutine != null) { StopCoroutine(_rumbleCoroutine); _rumbleCoroutine = null; }
            SetMotors(0f, 0f, 0f);
        }

        private IEnumerator RumbleCoroutine(float lowFreq, float highFreq, float duration)
        {
            SetMotors(lowFreq, highFreq, duration);
            yield return new WaitForSecondsRealtime(duration);
            SetMotors(0f, 0f, 0f);
            _rumbleCoroutine = null;
        }

        /// <summary>
        /// Gửi lệnh rung tới Gamepad.current (Unity) và tay cầm SDL3 đang mở (nếu có).
        /// DualShock 4 / DualSense qua Bluetooth chỉ rung được qua SDL3; tay XInput rung qua Unity.
        /// </summary>
        private void SetMotors(float low, float high, float seconds)
        {
            Gamepad.current?.SetMotorSpeeds(low, high);
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            _sdl3Source?.Rumble(low, high, seconds);
#endif
        }
        
        // ============================================================
        //  API AIMASSIST
        // ============================================================

        /// <summary>
        /// Áp dụng aim assist, trả về lượng góc xoay hoàn chỉnh (đơn vị: Độ) sau khi đã cộng lực hút.
        /// </summary>
        /// <param name="cameraTransform">Transform của camera</param>
        /// <param name="target">Mục tiêu trong World Space</param>
        /// <param name="playerLookDelta">Góc xoay dự kiến của người chơi trong frame này (đơn vị: Độ)</param>
        /// <param name="intensity">Mức độ hỗ trợ (0 = tắt, 1 = mạnh nhất)</param>
        /// <returns>Tổng góc xoay Delta (Độ) cuối cùng để áp dụng vào Camera</returns>
        public Vector2 AimAssistProcess(AimAssistSettings settings, Transform cameraTransform, Vector3 target, Vector2 playerLookDelta, float intensity = 0.3f)
            => _aimAssist.AimAssistProcess(settings, cameraTransform, target, playerLookDelta, intensity);

        /// <summary>
        /// Overload nhận Transform: <c>null</c> = không có mục tiêu. Dùng khi mục tiêu có thể
        /// nằm đúng gốc toạ độ (overload Vector3 coi Vector3.zero là "không có mục tiêu").
        /// </summary>
        public Vector2 AimAssistProcess(AimAssistSettings settings, Transform cameraTransform, Transform target, Vector2 playerLookDelta, float intensity = 0.3f)
        {
            if (target == null) return playerLookDelta;
            // Lệch cực nhỏ để không trùng sentinel Vector3.zero của overload cũ
            Vector3 p = target.position;
            if (p == Vector3.zero) p = new Vector3(0f, 0f, 1e-5f);
            return _aimAssist.AimAssistProcess(settings, cameraTransform, p, playerLookDelta, intensity);
        }
        
        public void UpdateActivePlatform() => _aimAssist.UpdateActivePlatform();

        // ============================================================
        //  API DEADZONE
        // ============================================================
        
        /// <summary>
        /// Hàm áp dụng Deadzone cho Vector2 và chuẩn hóa lại tỉ lệ (Scaled Radial Deadzone)
        /// </summary>
        /// <param name="rawInput">Vector2 input thô từ thiết bị điều khiển</param>
        /// <returns>Vector2 đã được xử lý deadzone</returns>
        /// <param name="deadzoneRadius">Ngưỡng deadzone (từ 0 đến 1). Các input có độ dài nhỏ hơn ngưỡng này sẽ bị bỏ qua.</param>
        public Vector2 ApplyDeadzone(Vector2 rawInput, float deadzoneRadius = 0.2f)
            => _inputDeadzone.ApplyDeadzone(rawInput, deadzoneRadius);

        // ══════════════════════════════════════════════════════
        //  PRIVATE — INITIALISE
        // ══════════════════════════════════════════════════════

        private void InitialiseWithProfile(TISInputProfileConfig profile)
        {
            // _activeProfile luôn là runtime copy do bridge tạo → huỷ bản cũ khi thay
            // (trước đây mỗi lần SwapProfile / rebind rò 1 ScriptableObject).
            var oldProfile = _activeProfile;
            _activeProfile = profile.CreateRuntimeCopy();
            if (oldProfile != null && oldProfile != _profile && oldProfile != profile)
                Destroy(oldProfile);
            
            TISRebindingSystem.ApplyOverridesToProfile(_activeProfile);

            // TISProfileBuilder xây dựng asset + map cache
            var buildResult = TISProfileBuilder.Build(_activeProfile, _verboseLog);
            _profileUsesMotion = TISProfileBuilder.UsesMotion(_activeProfile);
            _asset    = buildResult.Asset;
            _mapCache = buildResult.MapCache;
            _asset.Enable();

            // TISActionReader load caches từ asset mới
            _reader.Load(_asset, _activeProfile, _isMobileMode, _debugSkillAction);

            // Motion: gyro bị tắt khi virtual cursor đang bật (gyro lúc đó điều khiển cursor)
            _missingMotionActions.Clear();
            _motion.Load(
                _activeProfile.motionSettings,
                IsMotionActivationHeld,
                GetGamepadCursorEnable);

            // Lựa chọn bật / tắt gyro / touchpad theo profile + action (API / đã lưu) → áp lên asset mới
            RestoreMotionPrefs();

            // TISOverrideChannel nhận action references từ reader
            _override.Load(
                _reader.MoveAction,
                _reader.LookAction,
                _reader.LookActionIsMouseDelta,
                _isMobileMode,
                _activeProfile.cameraSettings,
                () => _activeDevice,
                _motion);

            EnableDefaultMaps();

            if (_verboseLog) LogActiveProfile();
        }

        // ══════════════════════════════════════════════════════
        //  CONTEXT MENU
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// In ra Console thông tin chi tiết profile đang active: tên map, action, binding PC/Gamepad/Mobile.
        /// Cũng được gọi tự động khi <c>_verboseLog</c> bật.
        /// </summary>
        [ContextMenu("Log Active Profile")]
        public void LogActiveProfile()
        {
            if (_activeProfile == null)
            {
                Debug.Log("[TISInputBridge] Chưa có profile nào active.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[TISInputBridge] ═══ Profile: \"{_activeProfile.profileName}\" ═══");

            foreach (var map in _activeProfile.actionMaps)
            {
                sb.AppendLine($"  ▶ Map: {map.mapName} [enabled={map.enabledByDefault}]");
                foreach (var a in map.actions)
                    sb.AppendLine(
                        $"      [{a.actionType,-8}] {a.actionName,-20}" +
                        $" PC={a.bindingPC,-30}" +
                        $" GP={a.bindingGamepad,-25}" +
                        $" MB={a.bindingMobile,-20}" +
                        $" mode={a.readMode}");
            }

            sb.AppendLine(
                $"  Camera  : mouse={_activeProfile.cameraSettings.mouseSensitivity}" +
                $" smoothing={_activeProfile.cameraSettings.smoothing}");
            sb.AppendLine(
                $"  Gamepad : L={_activeProfile.gamepadSettings.leftStickDeadzone}" +
                $" R={_activeProfile.gamepadSettings.rightStickDeadzone}");

            Debug.Log(sb.ToString());
        }

        // ══════════════════════════════════════════════════════
        //  MOTION API — bật / tắt gyro + touchpad theo profile / action
        // ══════════════════════════════════════════════════════
        //
        //  TrueInputSystem — TIS_InputBridge.Motion.cs  [partial]
        //
        //  API bật / tắt gyro + touchpad lúc chạy:
        //
        //    · TOÀN GAME   : GyroEnabled / TouchpadEnabled            (TIS_InputBridge.cs)
        //    · THEO PROFILE: SetProfileMotion(gyro, touchpad, save)
        //    · THEO ACTION : SetActionMotion(action, gyro, touchpad, save)
        //                    SetActionGyroEnabled / SetActionTouchpadEnabled
        //
        //  Gyro / touchpad của một action chạy khi CẢ 3 mức đều bật.
        //  Tắt theo action = tắt đúng binding gyro / touchpad của action đó (binding override
        //  path rỗng) — áp dụng cho cả "+ Gyro / + Touchpad" lẫn binding <TISMotion>/... riêng.
        //  Không cần rebuild profile. save: true → lưu lựa chọn người chơi (TISMotionPrefs),
        //  tự áp lại khi load / swap profile.

        // ══════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════

        private struct ActionMotionState
        {
            public bool gyro;
            public bool touchpad;
        }

        // Lựa chọn theo action cho profile hiện tại (khoá = TISMotionPrefs.ProfileKey)
        private readonly Dictionary<string, ActionMotionState> _actionMotion =
            new Dictionary<string, ActionMotionState>(StringComparer.OrdinalIgnoreCase);

        private string _motionPrefsKey;
        private bool?  _profileGyroOverride;
        private bool?  _profileTouchpadOverride;
        private bool   _profileGyroDefault     = true;   // giá trị trong profile asset
        private bool   _profileTouchpadDefault = true;

        /// <summary>Phát ra khi bất kỳ lựa chọn bật / tắt gyro / touchpad nào thay đổi (cập nhật UI Settings).</summary>
        public event Action OnMotionSettingsChanged;

        // ══════════════════════════════════════════════════════
        //  THEO PROFILE
        // ══════════════════════════════════════════════════════

        /// <summary>Gyro của profile đang chạy (Profile › Motion Settings › Enable Gyro, sau override).</summary>
        public bool ProfileGyroEnabled
            => _activeProfile != null && _activeProfile.motionSettings != null && _activeProfile.motionSettings.enableGyro;

        /// <summary>Touchpad của profile đang chạy (Profile › Motion Settings › Enable Touchpad, sau override).</summary>
        public bool ProfileTouchpadEnabled
            => _activeProfile != null && _activeProfile.motionSettings != null && _activeProfile.motionSettings.enableTouchpad;

        /// <summary>
        /// Bật / tắt gyro và touchpad cho TOÀN BỘ action của profile đang chạy. Hiệu lực ngay.
        /// </summary>
        /// <param name="save">true = lưu lựa chọn cho lần mở game sau (theo tên profile).</param>
        public void SetProfileMotion(bool gyro, bool touchpad, bool save = false)
        {
            _profileGyroOverride     = gyro;
            _profileTouchpadOverride = touchpad;
            ApplyProfileMotionOverride();
            if (save) SaveMotionPrefs();
            OnMotionSettingsChanged?.Invoke();
        }

        /// <summary>Bật / tắt riêng gyro của profile đang chạy.</summary>
        public void SetProfileGyroEnabled(bool enabled, bool save = false)
            => SetProfileMotion(enabled, ProfileTouchpadEnabled, save);

        /// <summary>Bật / tắt riêng touchpad của profile đang chạy.</summary>
        public void SetProfileTouchpadEnabled(bool enabled, bool save = false)
            => SetProfileMotion(ProfileGyroEnabled, enabled, save);

        // ══════════════════════════════════════════════════════
        //  THEO ACTION
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Bật / tắt gyro và touchpad của MỘT action (vd. chỉ tắt gyro của "Look"). Hiệu lực ngay,
        /// không rebuild profile. Không ảnh hưởng action khác.
        /// </summary>
        /// <param name="actionName">Tên action (không phân biệt hoa / thường).</param>
        /// <param name="save">true = lưu lựa chọn cho lần mở game sau (theo tên profile).</param>
        public void SetActionMotion(string actionName, bool gyro, bool touchpad, bool save = false)
        {
            if (string.IsNullOrEmpty(actionName)) return;

            if (_asset != null && _asset.FindAction(actionName, throwIfNotFound: false) == null)
                Debug.LogWarning($"[TISInputBridge] SetActionMotion: không tìm thấy action \"{actionName}\".");

            _actionMotion[actionName] = new ActionMotionState { gyro = gyro, touchpad = touchpad };
            ApplyActionMotionOverride(actionName);
            if (save) SaveMotionPrefs();
            OnMotionSettingsChanged?.Invoke();
        }

        /// <summary>Bật / tắt riêng gyro của một action.</summary>
        public void SetActionGyroEnabled(string actionName, bool enabled, bool save = false)
            => SetActionMotion(actionName, enabled, GetActionState(actionName).touchpad, save);

        /// <summary>Bật / tắt riêng touchpad của một action.</summary>
        public void SetActionTouchpadEnabled(string actionName, bool enabled, bool save = false)
            => SetActionMotion(actionName, GetActionState(actionName).gyro, enabled, save);

        /// <summary>
        /// Gyro của action có đang THỰC SỰ chạy không: action có binding gyro, và cả 3 mức
        /// (toàn game, profile, action) đều bật.
        /// </summary>
        public bool IsActionGyroEnabled(string actionName)
            => GyroEnabled && ProfileGyroEnabled && ActionHasGyro(actionName) && GetActionState(actionName).gyro;

        /// <summary>Touchpad của action có đang thực sự chạy không (giống <see cref="IsActionGyroEnabled"/>).</summary>
        public bool IsActionTouchpadEnabled(string actionName)
            => TouchpadEnabled && ProfileTouchpadEnabled && ActionHasTouchpad(actionName) && GetActionState(actionName).touchpad;

        /// <summary>Action có binding gyro không (<c>&lt;TISMotion&gt;/gyro</c> hoặc "+ Gyro").</summary>
        public bool ActionHasGyro(string actionName)     => ActionHasMotionBinding(actionName, gyro: true);

        /// <summary>Action có binding touchpad không (<c>&lt;TISMotion&gt;/touchpad*</c> hoặc "+ Touchpad").</summary>
        public bool ActionHasTouchpad(string actionName) => ActionHasMotionBinding(actionName, gyro: false);

        // ══════════════════════════════════════════════════════
        //  RESET
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Trả mọi lựa chọn theo profile / action về cấu hình gốc của profile asset.
        /// </summary>
        /// <param name="deleteSaved">true = xoá luôn file lựa chọn đã lưu của profile này.</param>
        public void ResetMotionSettings(bool deleteSaved = true)
        {
            var names = new List<string>(_actionMotion.Keys);
            _actionMotion.Clear();
            foreach (var n in names) ApplyActionMotionOverride(n);

            _profileGyroOverride = _profileTouchpadOverride = null;
            ApplyProfileMotionOverride();

            if (deleteSaved && !string.IsNullOrEmpty(_motionPrefsKey))
                TISMotionPrefs.Delete(_motionPrefsKey);
            OnMotionSettingsChanged?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        //  INTERNAL — gọi từ InitialiseWithProfile()
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Sau mỗi lần build / swap profile: nạp lựa chọn đã lưu (nếu đổi sang profile khác)
        /// và áp lại lên asset mới.
        /// </summary>
        private void RestoreMotionPrefs()
        {
            if (_activeProfile == null) return;

            var ms = _activeProfile.motionSettings;
            _profileGyroDefault     = ms == null || ms.enableGyro;
            _profileTouchpadDefault = ms == null || ms.enableTouchpad;

            string key = TISMotionPrefs.ProfileKey(_activeProfile.profileName);
            if (key != _motionPrefsKey)
            {
                // Đổi profile → bỏ lựa chọn của profile cũ, nạp lựa chọn đã lưu của profile mới
                _motionPrefsKey = key;
                _actionMotion.Clear();
                _profileGyroOverride = _profileTouchpadOverride = null;

                var file = TISMotionPrefs.Load(key);
                if (file != null)
                {
                    if (file.hasProfileGyro)     _profileGyroOverride     = file.profileGyro;
                    if (file.hasProfileTouchpad) _profileTouchpadOverride = file.profileTouchpad;
                    foreach (var a in file.actions)
                        if (!string.IsNullOrEmpty(a.actionName))
                            _actionMotion[a.actionName] = new ActionMotionState { gyro = a.gyro, touchpad = a.touchpad };
                }
            }

            // Cùng profile (rebind / swap lại) → giữ lựa chọn trong phiên, áp lên asset mới
            ApplyProfileMotionOverride();
            foreach (var name in _actionMotion.Keys)
                ApplyActionMotionOverride(name);
        }

        private void ApplyProfileMotionOverride()
        {
            var ms = _activeProfile != null ? _activeProfile.motionSettings : null;
            if (ms == null) return;
            ms.enableGyro     = _profileGyroOverride     ?? _profileGyroDefault;
            ms.enableTouchpad = _profileTouchpadOverride ?? _profileTouchpadDefault;
        }

        private ActionMotionState GetActionState(string actionName)
            => !string.IsNullOrEmpty(actionName) && _actionMotion.TryGetValue(actionName, out var s)
                ? s
                : new ActionMotionState { gyro = true, touchpad = true };

        /// <summary>Tắt / bật đúng các binding gyro / touchpad của action bằng binding override.</summary>
        private void ApplyActionMotionOverride(string actionName)
        {
            var action = _asset != null ? _asset.FindAction(actionName, throwIfNotFound: false) : null;
            if (action == null) return;

            var state    = GetActionState(actionName);
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                bool isGyro  = IsGyroBinding(b);
                bool isTouch = !isGyro && IsTouchpadBinding(b);
                if (!isGyro && !isTouch) continue;

                bool on = isGyro ? state.gyro : state.touchpad;
                if (on)
                {
                    if (b.overridePath != null) action.RemoveBindingOverride(i);
                }
                else if (b.overridePath != "")
                {
                    // Override path rỗng = binding bị vô hiệu (part composite đọc ra 0)
                    action.ApplyBindingOverride(i, "");
                }
            }
        }

        private bool ActionHasMotionBinding(string actionName, bool gyro)
        {
            var action = _asset != null ? _asset.FindAction(actionName, throwIfNotFound: false) : null;
            if (action == null) return false;
            foreach (var b in action.bindings)
                if (gyro ? IsGyroBinding(b) : IsTouchpadBinding(b)) return true;
            return false;
        }

        private static bool IsGyroBinding(InputBinding b)
            => b.isPartOfComposite
                ? string.Equals(b.name, "Gyro", StringComparison.OrdinalIgnoreCase)
                : TISMotionPaths.IsGyroPath(b.path);

        private static bool IsTouchpadBinding(InputBinding b)
            => b.isPartOfComposite
                ? string.Equals(b.name, "Touchpad", StringComparison.OrdinalIgnoreCase)
                : TISMotionPaths.IsMotionPath(b.path) && !TISMotionPaths.IsGyroPath(b.path);

        private void SaveMotionPrefs()
        {
            if (string.IsNullOrEmpty(_motionPrefsKey)) return;

            var data = new TISMotionPrefsFile
            {
                hasProfileGyro      = _profileGyroOverride.HasValue,
                profileGyro         = _profileGyroOverride ?? true,
                hasProfileTouchpad  = _profileTouchpadOverride.HasValue,
                profileTouchpad     = _profileTouchpadOverride ?? true,
            };
            foreach (var kv in _actionMotion)
                data.actions.Add(new TISActionMotionPref
                {
                    actionName = kv.Key,
                    gyro       = kv.Value.gyro,
                    touchpad   = kv.Value.touchpad,
                });
            TISMotionPrefs.Save(_motionPrefsKey, data);
        }
    }
}

// ============================================================
//  TISInputBuffer — P2 FIX #14
//  Input buffer window để không miss action khi frame drop.
// ============================================================
namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Input buffer với window time cho phép action không bị miss khi frame drop.
    /// Buffer sẽ giữ nguyên "pressed" state trong <see cref="WindowSeconds"/> giây
    /// sau khi nhận input — caller gọi <see cref="Consume"/> để kiểm tra và reset.
    /// </summary>
    public class TISInputBuffer
    {
        /// <summary>Tên action mà buffer này theo dõi.</summary>
        public string ActionName    { get; }

        /// <summary>Khoảng thời gian (giây) buffer giữ trạng thái pressed sau khi nhận input.</summary>
        public float  WindowSeconds { get; set; }

        private float _bufferedUntil = -1f;

        /// <summary><c>true</c> nếu buffer đang trong cửa sổ thời gian hợp lệ (chưa hết hạn và chưa bị consume).</summary>
        public bool IsBuffered => UnityEngine.Time.unscaledTime <= _bufferedUntil;

        public TISInputBuffer(string actionName, float windowSeconds = TISConstants.DefaultInputBufferWindowSeconds)
        {
            ActionName    = actionName;
            WindowSeconds = windowSeconds;
        }

        /// <summary>
        /// Cập nhật buffer mỗi frame. Nếu <paramref name="pressedThisFrame"/> là <c>true</c>,
        /// gia hạn cửa sổ buffer thêm <see cref="WindowSeconds"/> giây.
        /// </summary>
        public void Tick(bool pressedThisFrame)
        {
            if (pressedThisFrame)
                _bufferedUntil = UnityEngine.Time.unscaledTime + WindowSeconds;
        }

        /// <summary>
        /// Overload tiện lợi: tự đọc PressDown của <see cref="ActionName"/> từ <paramref name="bridge"/> rồi gọi <see cref="Tick(bool)"/>.
        /// </summary>
        public void Tick(TISInputBridge bridge)
        {
            bool pressed = bridge != null
                && bridge.GetSkillAction(ActionName, TISButtonReadMode.PressDown);
            Tick(pressed);
        }

        /// <summary>
        /// Kiểm tra buffer và tiêu thụ (reset) nếu hợp lệ.
        /// </summary>
        /// <returns><c>true</c> nếu buffer đang active và đã được reset; <c>false</c> nếu không có gì để tiêu thụ.</returns>
        public bool Consume()
        {
            if (!IsBuffered) return false;
            _bufferedUntil = -1f;
            return true;
        }

        /// <summary>Xóa buffer ngay lập tức, bất kể còn trong cửa sổ hay không.</summary>
        public void Clear() => _bufferedUntil = -1f;

        /// <summary>
        /// Gia hạn thêm <paramref name="extraSeconds"/> cho buffer nếu đang active.
        /// Không làm gì nếu buffer đã hết hạn.
        /// </summary>
        public void Extend(float extraSeconds)
        {
            if (IsBuffered) _bufferedUntil += extraSeconds;
        }
    }
}