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

namespace TrueInputSystem
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

        private TISAimAssist              _aimAssist = new TISAimAssist();
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
                Debug.LogWarning("[TISInputBridge] Duplicate — destroying.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
        }

        private void OnEnable()  => _asset?.Enable();
        private void OnDisable() => _asset?.Disable();

        private void OnDestroy()
        {
            StopRumble();
            if (_asset != null)
            {
                _asset.Disable();
                Destroy(_asset);
                _asset = null;
            }
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            // P3 FIX #18: clear registry khi scene unload tránh button stuck-pressed.
            TISMobileButtonRegistry.ClearAll();
        }

        private void Update()
        {
            UpdateActiveDevice();
            
            if (_enableObjectTracker)
                _objectTracker?.Tick();
        }

        private void LateUpdate()
        {
            TISMobileButtonRegistry.FlushFrame();
            _reader.SnapshotTriggerState();          // P1 FIX #3 — snapshot trigger buffer
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
          // Mobile không thay đổi — luôn ưu tiên cao nhất
          if (_isMobileMode)
          {
              _activeDevice = TISActiveDevice.Mobile;
              return;
          }

          // Gamepad có input frame này → chuyển sang Gamepad
          if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
          {
              _activeDevice = TISActiveDevice.Gamepad;
              return;
          }

          // Keyboard hoặc Mouse có input frame này → chuyển về KeyboardMouse
          bool keyboardInput = Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame;
          bool mouseInput    = Mouse.current    != null && Mouse.current.wasUpdatedThisFrame;
          if (keyboardInput || mouseInput)
          {
              _activeDevice = TISActiveDevice.KeyboardMouse;
              return;
          }

          // Không có input từ bất kỳ thiết bị nào → giữ nguyên thiết bị cũ
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
            Gamepad.current.SetMotorSpeeds(lowFreq, highFreq);
        }

        /// <summary>Dừng rung gamepad ngay lập tức và hủy coroutine rung nếu đang chạy.</summary>
        public void StopRumble()
        {
            if (_rumbleCoroutine != null) { StopCoroutine(_rumbleCoroutine); _rumbleCoroutine = null; }
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        private IEnumerator RumbleCoroutine(float lowFreq, float highFreq, float duration)
        {
            Gamepad.current.SetMotorSpeeds(lowFreq, highFreq);
            yield return new WaitForSecondsRealtime(duration);
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
            _rumbleCoroutine = null;
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
            _activeProfile = profile.CreateRuntimeCopy();
            
            TISRebindingSystem.ApplyOverridesToProfile(_activeProfile);

            // TISProfileBuilder xây dựng asset + map cache
            var buildResult = TISProfileBuilder.Build(_activeProfile, _verboseLog);
            _asset    = buildResult.Asset;
            _mapCache = buildResult.MapCache;
            _asset.Enable();

            // TISActionReader load caches từ asset mới
            _reader.Load(_asset, _activeProfile, _isMobileMode, _debugSkillAction);

            // TISOverrideChannel nhận action references từ reader
            _override.Load(
                _reader.MoveAction,
                _reader.LookAction,
                _reader.LookActionIsMouseDelta,
                _isMobileMode,
                _activeProfile.cameraSettings,
                () => _activeDevice);

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
    }
}

// ============================================================
//  TISInputBuffer — P2 FIX #14
//  Input buffer window để không miss action khi frame drop.
// ============================================================
namespace TrueInputSystem
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