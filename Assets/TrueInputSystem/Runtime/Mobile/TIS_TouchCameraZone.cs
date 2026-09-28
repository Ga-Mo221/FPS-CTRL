using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch      = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/Touch Camera Zone")]
    [DefaultExecutionOrder(-90)]
    public class TISTouchCameraZone : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("References")]
        [SerializeField] private TISInputBridge    _inputBridge;
        [SerializeField] private TISVirtualJoystick _joystick;

        [Header("Zone Settings")]
        [SerializeField] private float _zoneStartRatio = 0f;
        
        [SerializeField] private float _zoneEndRatio = 1f;

        [Header("Sensitivity")]
        [SerializeField] private Vector2 _sensitivityOverride = Vector2.zero;
        [SerializeField] private bool    _invertY = false;

        [Header("Smoothing")]
        [Range(1f, 60f)]
        [SerializeField] private float _smoothSpeed  = 15f;

        [Range(0f, 20f)]
        [SerializeField] private float _inertiaDecay = 10f;

        [Header("Pinch Zoom")]
        [Tooltip("Bật pinch-to-zoom trên mobile.\n" +
                 "Chỉ nhận khi CẢ 2 ngón ở trong camera zone (nửa phải).")]
        [SerializeField] private bool  _enablePinchZoom = true;

        [Tooltip("Tốc độ zoom khi pinch. Giá trị khuyến nghị: 0.005 – 0.02.")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float _pinchZoomSpeed = 0.01f;

        [Tooltip("Deadzone pinch (pixel). Dưới mức này không zoom.\n" +
                 "Tránh zoom micro khi cố giữ 2 ngón cố định.")]
        [Range(0f, 30f)]
        [SerializeField] private float _pinchDeadzone = 5f;

        [Header("Debug")]
        [SerializeField] private bool _debugDrawZone = false;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME STATE — CAMERA DRAG
        // ══════════════════════════════════════════════════════════════════════

        private int     _activeTouchId = -1;
        private Vector2 _lastTouchPos;
        private Vector2 _smoothedDelta;
        private bool    _hasInertia;

        // Fields dùng bởi TIS_TouchCameraZoneEditorBridge (Editor DLL) qua Reflection
        //private bool    _mouseActive  = false;
        private Vector2 _lastMousePos = Vector2.zero;

        public Vector2 RawDelta      { get; private set; }
        public Vector2 SmoothedDelta { get; private set; }
        public int     ActiveTouchId => _activeTouchId;
        
        public float ZoneStartRatio => _zoneStartRatio;
        public float ZoneEndRatio   => _zoneEndRatio;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME STATE — PINCH ZOOM
        // ══════════════════════════════════════════════════════════════════════

        private bool  _isPinching;
        private bool  _trackingTwoFingers;   // đang có 2 ngón trong zone, chưa xác định pinch hay pan
        private float _lastPinchDistance;

        /// <summary>Đang trong trạng thái pinch.</summary>
        public bool  IsPinching     => _isPinching;

        /// <summary>
        /// Pinch delta frame hiện tại (pixel).
        /// Dương = 2 ngón dãn ra = zoom in.
        /// Âm   = 2 ngón thu vào = zoom out.
        /// </summary>
        public float PinchZoomDelta { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            // Đảm bảo EnhancedTouch được enable ngay khi component bật
            if (Application.isMobilePlatform || Application.isEditor)
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            ResetState();
        }

        private void Start()
        {
            if (_inputBridge == null)
                _inputBridge = TISInputBridge.Instance;

            if (!Application.isMobilePlatform && !Application.isEditor)
            {
                enabled = false;
                return;
            }
            // FIX M3: EnhancedTouchSupport.Enable() đã được gọi trong OnEnable().
            // Không cần gọi lại ở đây — gọi trùng chỉ increment reference count
            // và cần Disable() thêm 1 lần tương ứng.
            SyncProfileSettings();
        }


        // Delegate để Editor inject HandleMouseFallback mà không cần #if UNITY_EDITOR trong DLL
        public static Action<TISTouchCameraZone> EditorUpdateOverride = null;

        private void Update()
        {
            if (Application.isEditor && EditorUpdateOverride != null)
            {
                EditorUpdateOverride(this);
                if (!_inputBridge.SimulateMobileInEditor())
                    return;
            }
            ProcessEnhancedTouches();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CORE TOUCH PROCESSING
        // ══════════════════════════════════════════════════════════════════════

        private void ProcessEnhancedTouches()
        {
            var activeTouches  = Touch.activeTouches;
            int joystickFinger = _joystick != null ? _joystick.ActiveTouchId : -1;

            // ── Thu thập ngón trong camera zone ──────────────
            // Loại trừ ngón joystick ngay từ đầu.
            //
            // HIGH FIX H11: Pinch detection wrong finger pair.
            // BUG CŨ: camFinger0 được assign theo ORDER trong activeTouches — không nhất thiết
            // là ngón đang rotate (_activeTouchId). Nếu ngón mới thêm vào (camFinger1) có
            // touchId nhỏ hơn _activeTouchId, nó sẽ thành camFinger0 → distance calc dùng sai
            // pair → pinch jump ngay lần đầu → zoom giật mạnh.
            //
            // FIX: _activeTouchId (ngón đang rotate) LUÔN là camFinger0.
            // Ngón mới thêm vào sẽ là camFinger1 bất kể thứ tự trong activeTouches.
            int   camFinger0 = _activeTouchId; // ưu tiên ngón đang rotate
            int   camFinger1 = -1;
            float camPos0X = 0f, camPos0Y = 0f;
            float camPos1X = 0f, camPos1Y = 0f;
            int   camCount = 0;
            bool  foundActive = false; // dùng lại bên dưới cho rotation check

            // Pass 1: nếu _activeTouchId hợp lệ, tìm vị trí của nó trước
            if (_activeTouchId != -1)
            {
                for (int i = 0; i < activeTouches.Count; i++)
                {
                    var t = activeTouches[i];
                    if (t.touchId != _activeTouchId) continue;
                    float normX = t.screenPosition.x / Screen.width;
                    if (normX >= _zoneStartRatio && normX <= _zoneEndRatio
                        && t.touchId != joystickFinger)
                    {
                        camPos0X = t.screenPosition.x;
                        camPos0Y = t.screenPosition.y;
                        camCount = 1;
                    }
                    break;
                }
            }

            // Pass 2: thu thập tất cả ngón khác trong zone làm camFinger1 (và fallback camFinger0)
            for (int i = 0; i < activeTouches.Count && camCount < 2; i++)
            {
                var touch = activeTouches[i];
                if (touch.touchId == joystickFinger) continue;
                if (TISMobileButtonRegistry.IsEnhancedTouchConsumed(touch.touchId))
                {
                    continue;
                }
                
                if (touch.touchId == _activeTouchId) continue; // đã xử lý ở Pass 1

                float normX = touch.screenPosition.x / Screen.width;
                if (normX < _zoneStartRatio || normX > _zoneEndRatio) continue;

                if (camCount == 0)
                {
                    // Không có _activeTouchId trong zone → dùng ngón đầu tiên tìm thấy
                    camFinger0 = touch.touchId;
                    camPos0X   = touch.screenPosition.x;
                    camPos0Y   = touch.screenPosition.y;
                    camCount   = 1;
                }
                else
                {
                    // camFinger0 đã có → đây là ngón thứ 2 cho pinch
                    camFinger1 = touch.touchId;
                    camPos1X   = touch.screenPosition.x;
                    camPos1Y   = touch.screenPosition.y;
                    camCount   = 2;
                }
            }

            // ── PINCH: cả 2 ngón trong camera zone ───────────
            // Xoay và zoom hoạt động độc lập — có thể dùng đồng thời.
            if (_enablePinchZoom && camCount >= 2)
            {
                // FIX #8: Tính khoảng cách bằng dx/dy thủ công — không alloc Vector2.
                float _dx         = camPos1X - camPos0X;
                float _dy         = camPos1Y - camPos0Y;
                float currentDist = Mathf.Sqrt(_dx * _dx + _dy * _dy);

                if (!_trackingTwoFingers)
                {
                    // Lần đầu có 2 ngón trong zone — chỉ snapshot distance.
                    // KHÔNG set _isPinching = true tại đây để GestureDetector vẫn
                    // có thể nhận pan trong cùng frame.
                    _trackingTwoFingers = true;
                    _lastPinchDistance  = currentDist;
                    PinchZoomDelta      = 0f;
                }
                else
                {
                    float delta = currentDist - _lastPinchDistance;

                    if (Mathf.Abs(delta) > _pinchDeadzone)
                    {
                        // Khoảng cách thay đổi đủ lớn → đây là pinch zoom thật sự.
                        // Chỉ lúc này mới set _isPinching = true để chặn pan.
                        _lastPinchDistance = currentDist;
                        float zoomDelta    = delta * _pinchZoomSpeed;
                        PinchZoomDelta     = zoomDelta;
                        _isPinching        = true;
                        _inputBridge?.OverrideZoomInput(zoomDelta);
                    }
                    else
                    {
                        // Khoảng cách gần như không đổi → 2 ngón pan cùng hướng,
                        // KHÔNG coi là pinch → GestureDetector được phép xử lý pan.
                        _isPinching    = false;
                        PinchZoomDelta = 0f;
                        _inputBridge?.ClearZoomOverride();
                    }
                }
                // KHÔNG return — tiếp tục xử lý rotation bên dưới.
            }
            else
            {
                // Kết thúc pinch / không còn 2 ngón
                if (_trackingTwoFingers)
                {
                    _trackingTwoFingers = false;
                    _isPinching         = false;
                    PinchZoomDelta      = 0f;
                    _inputBridge?.ClearZoomOverride();
                }
            }

            // ── CAMERA ROTATION: 1 ngón trong zone ───────────
            if (activeTouches.Count == 0)
            {
                if (_activeTouchId != -1)
                {
                    _hasInertia    = _smoothedDelta.sqrMagnitude > 0.001f;
                    _activeTouchId = -1;
                }
                HandleInertia();
                return;
            }

            // foundActive đã khai báo ở phần thu thập ngón (HIGH FIX H11)
            foundActive = false;

            for (int i = 0; i < activeTouches.Count; i++)
            {
                var touch = activeTouches[i];
                if (touch.touchId == joystickFinger) continue;
                if (TISMobileButtonRegistry.IsEnhancedTouchConsumed(touch.touchId))
                {
                    continue;
                }

                float normX = touch.screenPosition.x / Screen.width;
                if (normX < _zoneStartRatio || normX > _zoneEndRatio) continue;

                if (touch.phase == TouchPhase.Began && _activeTouchId == -1)
                {
                    _activeTouchId = touch.touchId;
                    _lastTouchPos  = touch.screenPosition;
                    _smoothedDelta = Vector2.zero;
                    _hasInertia    = false;
                }

                if (touch.touchId == _activeTouchId)
                {
                    if (touch.phase == TouchPhase.Moved)
                    {
                        Vector2 delta = touch.screenPosition - _lastTouchPos;
                        _lastTouchPos = touch.screenPosition;
                        ApplyDelta(delta);
                        foundActive = true;
                    }
                    else if (touch.phase == TouchPhase.Stationary)
                    {
                        ApplyDelta(Vector2.zero);
                        foundActive = true;
                    }
                    else if (touch.phase == TouchPhase.Ended ||
                             touch.phase == TouchPhase.Canceled)
                    {
                        _hasInertia    = _smoothedDelta.sqrMagnitude > 0.001f;
                        _activeTouchId = -1;
                    }
                }
            }

            if (!foundActive && _activeTouchId == -1)
                HandleInertia();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  APPLY DELTA & SMOOTH
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyDelta(Vector2 rawPixelDelta)
        {
            Vector2 sensitivity = GetSensitivity();
            Vector2 scaledDelta = new Vector2(
                rawPixelDelta.x * sensitivity.x,
                rawPixelDelta.y * sensitivity.y);

            if (GetInvertY()) scaledDelta.y = -scaledDelta.y;

            RawDelta = scaledDelta;

            float lerpFactor = 1f - Mathf.Exp(-_smoothSpeed * Time.unscaledDeltaTime);
            _smoothedDelta   = Vector2.Lerp(_smoothedDelta, scaledDelta, lerpFactor);

            SmoothedDelta = _smoothedDelta;
            _inputBridge?.OverrideLookInput(_smoothedDelta);
        }

        private void HandleInertia()
        {
            if (!_hasInertia || _smoothedDelta.sqrMagnitude < 0.00001f)
            {
                _smoothedDelta = Vector2.zero;
                RawDelta       = Vector2.zero;
                SmoothedDelta  = Vector2.zero;
                _hasInertia    = false;
                _inputBridge?.ClearLookOverride();
                return;
            }

            float decayFactor = 1f - Mathf.Exp(-_inertiaDecay * Time.unscaledDeltaTime);
            _smoothedDelta    = Vector2.Lerp(_smoothedDelta, Vector2.zero, decayFactor);

            SmoothedDelta = _smoothedDelta;
            _inputBridge?.OverrideLookInput(_smoothedDelta);
        }

        private void ResetState()
        {
            _activeTouchId = -1;
            _lastTouchPos  = Vector2.zero;
            _smoothedDelta = Vector2.zero;
            RawDelta       = Vector2.zero;
            SmoothedDelta  = Vector2.zero;
            _hasInertia    = false;
            _isPinching         = false;
            _trackingTwoFingers = false;
            PinchZoomDelta      = 0f;
            _inputBridge?.ClearLookOverride();
            _inputBridge?.ClearZoomOverride();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private Vector2 GetSensitivity()
        {
            if (_sensitivityOverride.sqrMagnitude > 0f)
                return _sensitivityOverride;

            var cam = _inputBridge?.TISCameraSettings;
            return cam != null ? cam.mobileSensitivity : new Vector2(0.12f, 0.12f);
        }

        private bool GetInvertY()
        {
            if (_invertY) return true;
            return _inputBridge?.TISCameraSettings?.invertY ?? false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public void InjectDependencies(TISInputBridge bridge, TISVirtualJoystick joystick)
        {
            _inputBridge = bridge;
            _joystick    = joystick;
        }

        // Được gọi bởi TIS_TouchCameraZoneEditorBridge qua Reflection
        private void OverrideZoomViaAccessor(float zoomDelta)
            => _inputBridge?.OverrideZoomInput(zoomDelta);

        // P2 FIX #12: Public setters thay thế reflection từ TISMobileInputSetup.DatField().
        // Rename field → không còn silent fail.
        public void SetZoneStartRatio(float ratio)   => _zoneStartRatio  = ratio;
        public void SetSmoothSpeed(float speed)      => _smoothSpeed     = speed;
        public void SetInertiaDecay(float decay)     => _inertiaDecay    = decay;
        public void SetEnablePinchZoom(bool enable)  => _enablePinchZoom = enable;
        public void SetPinchZoomSpeed(float speed)   => _pinchZoomSpeed  = speed;
        public void SetPinchDeadzone(float deadzone) => _pinchDeadzone   = deadzone;

        public void SyncProfileSettings()
        {
            // var mobile = _inputBridge?.TISMobileSettings;
            // if (mobile != null)
            //     _zoneStartRatio = 1f - mobile.cameraZoneWidthRatio;

            var cam = _inputBridge?.TISCameraSettings;
            if (cam != null)
                _invertY = cam.invertY;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DEBUG GIZMOS (Editor only)
        // ══════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_debugDrawZone) return;
            if (!Application.isEditor) return;

            float opacity = _inputBridge?.TISMobileSettings?.cameraZoneDebugOpacity ?? 0.15f;
            if (opacity <= 0f) return;

            float xStart = Screen.width  * _zoneStartRatio;
            float xEnd   = Screen.width  * _zoneEndRatio;
            float height = Screen.height;

            var rect = new Rect(xStart, 0f, xEnd - xStart, height);
            var originalColor = GUI.color;

            // Màu xanh = idle, đỏ nhạt = đang pinch
            GUI.color = _isPinching
                ? new Color(1f, 0.3f, 0.3f, opacity)
                : new Color(0.1f, 0.8f, 0.3f, opacity);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal    = { textColor = Color.white },
                fontSize  = 14,
                alignment = TextAnchor.UpperCenter
            };

            string status = _isPinching
                ? $"PINCH  Δ={PinchZoomDelta:F3}"
                : _activeTouchId >= 0
                    ? $"Camera Zone  [finger {_activeTouchId}]\nΔ={SmoothedDelta:F2}"
                    : "Camera Zone  [idle]";

            GUI.Label(new Rect(xStart, 10f, xEnd - xStart, 50f), status, labelStyle);
        }
    }
}