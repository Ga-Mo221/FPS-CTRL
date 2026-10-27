// ============================================================
//  TrueInputSystem — TIS_TouchCameraZone.cs
//
//  Xoay camera (1 ngón) + pinch zoom (2 ngón) trên mobile.
//
//  MỖI NGÓN MỘT CHỦ (uGUI pointer ownership):
//    - Zone KHÔNG tự đọc mọi ngón trên màn hình nữa (bỏ EnhancedTouch).
//    - Ngón chỉ thuộc về zone khi EventSystem giao cho nó lúc chạm xuống:
//        · TISFullscreenTouchReceiver: chạm vùng nền bên phải → AddPointer()
//        · TISMobileSkillButton (On Touch Camera = ON): bấm nút vừa kéo ngắm → AddPointer()
//    - Nút skill / joystick nằm trên → EventSystem giao ngón cho chúng trước,
//      zone không bao giờ "giành" ngón của nút. Kéo ngón đi đâu cũng vẫn là của zone
//      cho tới khi nhấc tay (không còn kiểm tra vùng mỗi frame).
//
//  ĐỘ TRỄ: uGUI chỉ dùng để XÁC ĐỊNH CHỦ. Vị trí ngón được đọc TRỰC TIẾP từ
//  Touchscreen / Pointer mỗi frame (ExecutionOrder -90, trước Update của game)
//  → không trễ 1 frame như khi chờ OnDrag của EventSystem.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

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
        [Tooltip("Mép trái vùng camera (tỉ lệ chiều ngang). TISFullscreenTouchReceiver dùng\n" +
                 "giá trị chia vùng của joystick để quyết định ngón nào giao cho camera.")]
        [SerializeField] private float _zoneStartRatio = 0f;

        [SerializeField] private float _zoneEndRatio = 1f;

        [Header("Sensitivity")]
        [SerializeField] private Vector2 _sensitivityOverride = Vector2.zero;
        [SerializeField] private bool    _invertY = false;

        [Tooltip("Chuẩn hoá theo chiều cao màn hình: kéo cùng một quãng (tính theo % màn hình)\n" +
                 "→ xoay cùng một góc trên mọi độ phân giải. Độ nhạy giữ nguyên trên màn 1080p.")]
        [SerializeField] private bool  _normalizeByScreenHeight = true;

        [Tooltip("Chiều cao tham chiếu khi chuẩn hoá (pixel).")]
        [SerializeField] private float _referenceScreenHeight = 1080f;

        [Header("Smoothing")]
        [Range(1f, 60f)]
        [SerializeField] private float _smoothSpeed  = 15f;

        [Range(0f, 20f)]
        [SerializeField] private float _inertiaDecay = 10f;

        [Header("Pinch Zoom")]
        [Tooltip("Bật pinch-to-zoom trên mobile (2 ngón cùng thuộc camera zone).")]
        [SerializeField] private bool  _enablePinchZoom = true;

        [Tooltip("Tốc độ zoom khi pinch. Giá trị khuyến nghị: 0.005 – 0.02.")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float _pinchZoomSpeed = 0.01f;

        [Tooltip("Deadzone pinch (pixel). Dưới mức này không zoom.\n" +
                 "Tránh zoom micro khi cố giữ 2 ngón cố định.")]
        [Range(0f, 30f)]
        [SerializeField] private float _pinchDeadzone = 5f;

        [Header("Debug")]
        // Chi doc trong #if UNITY_EDITOR || DEVELOPMENT_BUILD -> ban release bao CS0414. Giu field (cung layout serialize).
#pragma warning disable CS0414
        [SerializeField] private bool _debugDrawZone = false;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════════════════
        //  OWNED POINTERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Số ngón tối đa zone theo dõi cùng lúc.</summary>
        public const int MaxPointers = 5;

        private struct OwnedPointer
        {
            public int           pointerId;   // uGUI pointerId — khoá để nhận PointerUp
            public int           touchId;     // Touchscreen touchId (touch) — đọc vị trí trực tiếp
            public Touchscreen   touchscreen; // null nếu là chuột / bút
            public Pointer       pointer;     // chuột / bút (Editor simulate mobile)
            public Vector2       position;    // vị trí frame hiện tại
            public Vector2       prevPosition;// vị trí frame trước (lúc vừa chạm = điểm chạm)
            public Vector2       eventPosition;// vị trí mới nhất từ OnDrag (khi không đọc trực tiếp được thiết bị)
        }

        private readonly OwnedPointer[] _pointers = new OwnedPointer[MaxPointers];
        private int _pointerCount;

        /// <summary>Số ngón đang thuộc camera zone.</summary>
        public int PointerCount => _pointerCount;

        /// <summary>Đọc ngón thứ <paramref name="index"/> (0 = ngón chạm sớm nhất, dùng để xoay).</summary>
        public bool TryGetPointer(int index, out int pointerId, out Vector2 position, out Vector2 prevPosition)
        {
            if (index < 0 || index >= _pointerCount)
            {
                pointerId = -1; position = prevPosition = Vector2.zero;
                return false;
            }
            var p = _pointers[index];
            pointerId    = p.pointerId;
            position     = p.position;
            prevPosition = p.prevPosition;
            return true;
        }

        /// <summary>Instance đang bật gần nhất — dùng cho TISMobileSkillButton (On Touch Camera).</summary>
        public static TISTouchCameraZone Current { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME STATE — CAMERA DRAG
        // ══════════════════════════════════════════════════════════════════════

        private int     _rotatingPointerId = -1;
        private Vector2 _smoothedDelta;
        private bool    _hasInertia;

        public Vector2 RawDelta      { get; private set; }
        public Vector2 SmoothedDelta { get; private set; }

        /// <summary>uGUI pointerId của ngón đang xoay camera (-1 = không có).</summary>
        public int     ActiveTouchId => _rotatingPointerId;

        public float ZoneStartRatio => _zoneStartRatio;
        public float ZoneEndRatio   => _zoneEndRatio;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME STATE — PINCH ZOOM
        // ══════════════════════════════════════════════════════════════════════

        private bool  _isPinching;
        private bool  _trackingTwoFingers;
        private int   _pinchIdA = -1, _pinchIdB = -1;
        private float _lastPinchDistance;

        /// <summary>Đang trong trạng thái pinch.</summary>
        public bool  IsPinching     => _isPinching;

        /// <summary>Pinch delta frame hiện tại. Dương = dãn ra = zoom in.</summary>
        public float PinchZoomDelta { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void OnEnable()  => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
            _pointerCount = 0;
            ResetState();
        }

        private void Start()
        {
            if (_inputBridge == null)
                _inputBridge = TISInputBridge.Instance;
            SyncProfileSettings();
        }

        private void Update()
        {
            RefreshPointerPositions();
            ProcessPinch();
            ProcessRotation();
            ProcessEditorScrollZoom();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OWNERSHIP API — gọi từ TISFullscreenTouchReceiver / TISMobileSkillButton
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Giao ngón <paramref name="e"/> cho camera zone (gọi trong OnPointerDown).
        /// Trả về false nếu zone đang tắt hoặc đã đủ <see cref="MaxPointers"/>.
        /// </summary>
        public bool AddPointer(PointerEventData e)
        {
            if (!isActiveAndEnabled || e == null) return false;
            if (IndexOf(e.pointerId) >= 0) return true;
            if (_pointerCount >= MaxPointers) return false;

            var p = new OwnedPointer
            {
                pointerId = e.pointerId,
                touchId   = -1,
                position      = e.position,
                eventPosition = e.position,
            };

            // InputSystemUIInputModule → biết chính xác thiết bị / touchId để đọc trực tiếp.
            // StandaloneInputModule (legacy) → không có, dùng vị trí từ OnDrag (MovePointer).
            if (e is ExtendedPointerEventData ext)
            {
                if (ext.device is Touchscreen ts)
                {
                    p.touchscreen = ts;
                    p.touchId     = ext.touchId;
                }
                else if (ext.device is Pointer ptr)
                {
                    p.pointer = ptr;
                }
            }

            p.prevPosition = p.position;
            _pointers[_pointerCount++] = p;
            return true;
        }

        /// <summary>Cập nhật vị trí từ OnDrag — chỉ dùng khi không đọc trực tiếp được thiết bị.</summary>
        public void MovePointer(int pointerId, Vector2 screenPos)
        {
            int i = IndexOf(pointerId);
            if (i < 0) return;
            // Chỉ lưu lại — RefreshPointerPositions() áp vào đầu frame sau
            // (ghi thẳng vào position sẽ làm prevPosition == position → delta = 0)
            _pointers[i].eventPosition = screenPos;
        }

        /// <summary>Trả ngón về (gọi trong OnPointerUp).</summary>
        public void RemovePointer(int pointerId)
        {
            int i = IndexOf(pointerId);
            if (i < 0) return;

            // Giữ thứ tự chạm (ngón sớm nhất luôn ở index 0)
            for (int k = i; k < _pointerCount - 1; k++)
                _pointers[k] = _pointers[k + 1];
            _pointerCount--;
        }

        /// <summary><c>true</c> nếu ngón này đang thuộc camera zone.</summary>
        public bool OwnsPointer(int pointerId) => IndexOf(pointerId) >= 0;

        private int IndexOf(int pointerId)
        {
            for (int i = 0; i < _pointerCount; i++)
                if (_pointers[i].pointerId == pointerId) return i;
            return -1;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CORE — ĐỌC VỊ TRÍ TRỰC TIẾP
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Đầu mỗi frame: vị trí cũ → prevPosition, đọc vị trí mới trực tiếp từ thiết bị.
        /// Ngón vừa thêm (EventSystem, frame trước) có prevPosition = điểm chạm → delta
        /// frame đầu là quãng đã kéo thật kể từ lúc chạm, không giật.
        /// GestureDetector (-85) đọc cùng cặp position / prevPosition sau camera (-90).
        /// </summary>
        private void RefreshPointerPositions()
        {
            for (int i = _pointerCount - 1; i >= 0; i--)
            {
                ref var p = ref _pointers[i];
                p.prevPosition = p.position;

                bool stillDown = true;
                if (p.touchscreen != null)
                    stillDown = TryReadTouch(p.touchscreen, p.touchId, ref p.position);
                else if (p.pointer != null)
                {
                    if (!p.pointer.added) stillDown = false;
                    else p.position = p.pointer.position.ReadValue();
                }
                else
                {
                    // EventSystem legacy (không có ExtendedPointerEventData) → vị trí từ OnDrag
                    p.position = p.eventPosition;
                }

                // An toàn: ngón đã nhấc mà PointerUp chưa tới (app mất focus, canvas tắt...) → bỏ
                if (!stillDown) { RemovePointer(p.pointerId); continue; }
            }
        }

        private static bool TryReadTouch(Touchscreen ts, int touchId, ref Vector2 position)
        {
            if (!ts.added) return false;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl t = touches[i];
                if (t.touchId.ReadValue() != touchId) continue;
                if (!t.press.isPressed) return false;
                position = t.position.ReadValue();
                return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CORE — PINCH
        // ══════════════════════════════════════════════════════════════════════

        private void ProcessPinch()
        {
            if (!_enablePinchZoom || _pointerCount < 2)
            {
                EndPinch();
                return;
            }

            var a = _pointers[0];
            var b = _pointers[1];
            float currentDist = Vector2.Distance(a.position, b.position);

            // Cặp ngón đổi (một ngón nhấc, ngón khác thay) → snapshot lại, không zoom giật
            if (!_trackingTwoFingers || a.pointerId != _pinchIdA || b.pointerId != _pinchIdB)
            {
                _trackingTwoFingers = true;
                _pinchIdA           = a.pointerId;
                _pinchIdB           = b.pointerId;
                _lastPinchDistance  = currentDist;
                PinchZoomDelta      = 0f;
                return;
            }

            float delta = currentDist - _lastPinchDistance;
            if (Mathf.Abs(delta) > _pinchDeadzone)
            {
                // Khoảng cách đổi đủ lớn → pinch thật (chặn two-finger pan)
                _lastPinchDistance = currentDist;
                float zoomDelta    = delta * _pinchZoomSpeed;
                PinchZoomDelta     = zoomDelta;
                _isPinching        = true;
                _inputBridge?.OverrideZoomInput(zoomDelta);
            }
            else
            {
                // 2 ngón đi cùng hướng → không phải pinch, để GestureDetector nhận pan
                _isPinching    = false;
                PinchZoomDelta = 0f;
                _inputBridge?.ClearZoomOverride();
            }
        }

        private void EndPinch()
        {
            if (!_trackingTwoFingers) return;
            _trackingTwoFingers = false;
            _isPinching         = false;
            _pinchIdA = _pinchIdB = -1;
            PinchZoomDelta      = 0f;
            _inputBridge?.ClearZoomOverride();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CORE — ROTATION (ngón chạm sớm nhất)
        // ══════════════════════════════════════════════════════════════════════

        private void ProcessRotation()
        {
            if (_pointerCount == 0)
            {
                if (_rotatingPointerId != -1)
                {
                    _hasInertia        = _smoothedDelta.sqrMagnitude > 0.001f;
                    _rotatingPointerId = -1;
                }
                HandleInertia();
                return;
            }

            var p = _pointers[0];
            if (p.pointerId != _rotatingPointerId)
            {
                // Ngón xoay mới (chạm mới, hoặc ngón cũ nhấc → ngón kế tiếp thay) → không giật
                _rotatingPointerId = p.pointerId;
                _smoothedDelta     = Vector2.zero;
                _hasInertia        = false;
                ApplyDelta(Vector2.zero);
            }
            else
            {
                ApplyDelta(p.position - p.prevPosition);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  APPLY DELTA & SMOOTH
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyDelta(Vector2 rawPixelDelta)
        {
            if (_normalizeByScreenHeight && Screen.height > 0)
                rawPixelDelta *= _referenceScreenHeight / Screen.height;

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
                if (_smoothedDelta != Vector2.zero || RawDelta != Vector2.zero || _hasInertia)
                    _inputBridge?.ClearLookOverride();
                _smoothedDelta = Vector2.zero;
                RawDelta       = Vector2.zero;
                SmoothedDelta  = Vector2.zero;
                _hasInertia    = false;
                return;
            }

            float decayFactor = 1f - Mathf.Exp(-_inertiaDecay * Time.unscaledDeltaTime);
            _smoothedDelta    = Vector2.Lerp(_smoothedDelta, Vector2.zero, decayFactor);

            SmoothedDelta = _smoothedDelta;
            _inputBridge?.OverrideLookInput(_smoothedDelta);
        }

        private void ResetState()
        {
            _rotatingPointerId  = -1;
            _smoothedDelta      = Vector2.zero;
            RawDelta            = Vector2.zero;
            SmoothedDelta       = Vector2.zero;
            _hasInertia         = false;
            _isPinching         = false;
            _trackingTwoFingers = false;
            _pinchIdA = _pinchIdB = -1;
            PinchZoomDelta      = 0f;
            _inputBridge?.ClearLookOverride();
            _inputBridge?.ClearZoomOverride();
        }

        // ── Editor: lăn chuột = zoom (thay pinch khi Simulate Mobile In Editor) ──

        private void ProcessEditorScrollZoom()
        {
            if (!Application.isEditor || !_enablePinchZoom || _pointerCount >= 2) return;

            var mouse = TISPhysicalMouse.Current;
            if (mouse == null) return;

            float scroll = mouse.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.1f)
            {
                float zoomDelta = scroll * _pinchZoomSpeed * 0.5f;
                PinchZoomDelta  = zoomDelta;
                _inputBridge?.OverrideZoomInput(zoomDelta);
            }
            else if (PinchZoomDelta != 0f && !_trackingTwoFingers)
            {
                PinchZoomDelta = 0f;
                _inputBridge?.ClearZoomOverride();
            }
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

        public void SetZoneStartRatio(float ratio)   => _zoneStartRatio  = ratio;
        public void SetSmoothSpeed(float speed)      => _smoothSpeed     = speed;
        public void SetInertiaDecay(float decay)     => _inertiaDecay    = decay;
        public void SetEnablePinchZoom(bool enable)  => _enablePinchZoom = enable;
        public void SetPinchZoomSpeed(float speed)   => _pinchZoomSpeed  = speed;
        public void SetPinchDeadzone(float deadzone) => _pinchDeadzone   = deadzone;

        public void SyncProfileSettings()
        {
            var cam = _inputBridge?.TISCameraSettings;
            if (cam != null)
                _invertY = cam.invertY;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DEBUG (Editor / Development Build — không có OnGUI trong bản release)
        // ══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _debugLabelStyle;

        private void OnGUI()
        {
            if (!_debugDrawZone) return;

            float opacity = _inputBridge?.TISMobileSettings?.cameraZoneDebugOpacity ?? 0.15f;
            if (opacity <= 0f) opacity = 0.15f;

            float xStart = Screen.width * _zoneStartRatio;
            float xEnd   = Screen.width * _zoneEndRatio;
            var   rect   = new Rect(xStart, 0f, xEnd - xStart, Screen.height);

            var originalColor = GUI.color;
            GUI.color = _isPinching
                ? new Color(1f, 0.3f, 0.3f, opacity)
                : new Color(0.1f, 0.8f, 0.3f, opacity);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;

            _debugLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                normal    = { textColor = Color.white },
                fontSize  = 14,
                alignment = TextAnchor.UpperCenter
            };

            string status = _isPinching
                ? $"PINCH  Δ={PinchZoomDelta:F3}"
                : _rotatingPointerId != -1
                    ? $"Camera Zone  [{_pointerCount} finger]\nΔ={SmoothedDelta:F2}"
                    : "Camera Zone  [idle]";

            GUI.Label(new Rect(xStart, 10f, xEnd - xStart, 50f), status, _debugLabelStyle);
        }
#endif
    }
}
