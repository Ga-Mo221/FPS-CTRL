using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/Virtual Joystick")]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TISVirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("References")]
        [SerializeField] private RectTransform _knob;
        [SerializeField] private TISInputBridge   _inputBridge;

        [Header("Dynamic Joystick")]
        [Tooltip("true  = hiện tại điểm chạm (PUBG/Liên Quân style).\n" +
                 "false = cố định vị trí.")]
        [SerializeField] private bool _dynamicMode = true;

        [Tooltip("Tỉ lệ chiều ngang màn hình dành cho joystick. 0.5 = nửa trái.")]
        [Range(0.3f, 0.6f)]
        [SerializeField] private float _activationZoneWidthRatio = 0.5f;

        [Tooltip("Margin pixel từ cạnh màn hình — tránh joystick bị cắt.")]
        [Range(0f, 80f)]
        [SerializeField] private float _edgeMarginPx = 20f;

        [Header("Appearance")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleOpacity   = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float _activeOpacity = 0.85f;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════════════════

        private RectTransform _bgRect;
        private RectTransform _canvasRect;
        [SerializeField] private CanvasGroup   _canvasGroup;
        [SerializeField] private Canvas        _canvas;
        private Image         _bgImage;
        private Image         _knobImage;
        [SerializeField] private Vector2       _anchoredOrigin;
        private float         _radius;

        private int   _touchId    = -1;
        private bool  _isDragging = false;
        private bool  _radiusSetExternally = false; // true khi SetRadius() được gọi từ TISMobileInputSetup
        private bool  _dynamicModeSetExternally = false; // true khi SetDynamicMode() được gọi từ TISMobileInputSetup
        private bool  _idleOpacitySetExternally = false; // true khi SetIdleOpacity() được gọi từ TISMobileInputSetup
        // FIX M4: Camera cache — GetEventCamera() kết quả không đổi trong runtime
        // (renderMode và worldCamera ổn định sau Awake). Cache 1 lần tránh gọi mỗi drag frame.
        private Camera _cachedEventCamera;
        //private bool   _eventCameraCached = false;
        private float _cachedDeadzone = 0.08f;       // FIX #9: cache để không gọi nullable chain mỗi frame

        // ── Public ──
        public Vector2 RawInput       { get; private set; }
        public float   InputMagnitude => RawInput.magnitude;
        public int     ActiveTouchId  => _touchId;
        public bool    IsDragging     => _isDragging;
        public float   ActivationZoneWidthRatio => _activationZoneWidthRatio;

        // ══════════════════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════════════════

        public event Action<Vector2> OnInputChanged;
        public event Action          OnReleased;

        // ══════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _bgRect      = GetComponent<RectTransform>();
            _bgImage     = GetComponent<Image>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _canvas      = GetComponentInParent<Canvas>();

            if (_canvas != null)
                _canvasRect = _canvas.GetComponent<RectTransform>();
            if (_knob != null)
                _knobImage = _knob.GetComponent<Image>();

            // ── FIX v6: ANCHOR SPACE FIX ──────────────────────────────────
            // ScreenPointToLocalPointInRectangle(_canvasRect) trả về tọa độ
            // với origin = canvas PIVOT = CENTER.
            // Để anchoredPosition khớp với không gian đó, anchor + pivot của
            // _bgRect phải là CENTER (0.5, 0.5), KHÔNG phải bottom-left (0, 0).
            //
            // Đặt ở Awake() để đảm bảo đúng trước khi bất kỳ code nào khác
            // đọc anchoredPosition (kể cả code từ CreateDefaultJoystick).
            _bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            _bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            _bgRect.pivot     = new Vector2(0.5f, 0.5f);
            // ──────────────────────────────────────────────────────────────

            // Snapshot origin SAU KHI đã fix anchor
            _anchoredOrigin = new Vector2(-800f, -300f);
            _bgRect.anchoredPosition = _anchoredOrigin;

            // FIX M4: cache event camera ngay trong Awake — renderMode và worldCamera
            // không thay đổi sau khi canvas được setup.
            _cachedEventCamera  = GetEventCamera();
            //_eventCameraCached  = true;
            
            if (!Application.isEditor)
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }
        
        private void OnDestroy()
        {
            if (!Application.isEditor)
                UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            if (_inputBridge == null)
                _inputBridge = TISInputBridge.Instance;

            ApplyProfileSettings();
            SetIdle();
        }

        private void OnDisable() => ResetJoystick();

        // ══════════════════════════════════════════════════════════════════════
        //  UGUI POINTER EVENTS  (fixed mode)
        // ══════════════════════════════════════════════════════════════════════
        
        public void SetAnchoredOrigin(Vector2 origin)
        {
            _anchoredOrigin = origin;
            if (_bgRect != null)
                _bgRect.anchoredPosition = _anchoredOrigin;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_isDragging || _dynamicMode) return;
            StartDrag(e.pointerId, e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_isDragging || e.pointerId != _touchId) return;
            ProcessDragFromScreen(e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _touchId) return;
            ResetJoystick();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API — gọi bởi TISFullscreenTouchReceiver
        // ══════════════════════════════════════════════════════════════════════

        public void BeginTouch(int pointerId, Vector2 screenPos)
        {
            if (_isDragging) return;

            // HIGH FIX H8: Screen.width có thể = 0 trong edge case (splash screen, Editor
            // window resize, first frame trước khi layout xong) → divide by zero → NaN.
            // NaN > bất kỳ float nào = false → touch bị bỏ qua (silent) nhưng vẫn safe.
            // Thêm explicit guard để rõ ràng và tránh NaN propagate vào _bgRect.
            if (Screen.width <= 0) return;
            if (screenPos.x / Screen.width > _activationZoneWidthRatio) return;

            if (_dynamicMode)
            {
                PlaceJoystickAt(screenPos);
                SetVisible(true);
            }

            StartDrag(pointerId, screenPos);
        }

        /// <summary>Forward drag từ TISFullscreenTouchReceiver — direct call (v5+).</summary>
        public void ProcessDragExternal(Vector2 screenPos)
        {
            if (!_isDragging) return;
            ProcessDragFromScreen(screenPos);
        }

        public void ForceRelease(int pointerId)
        {
            if (_touchId != pointerId) return;
            ResetJoystick();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CORE LOGIC
        // ══════════════════════════════════════════════════════════════════════

        private void StartDrag(int pointerId, Vector2 screenPos)
        {
            _touchId    = pointerId;
            _isDragging = true;

            // ← THÊM: fixed mode cần set activeOpacity khi bắt đầu kéo
            if (!_dynamicMode && _canvasGroup)
                _canvasGroup.alpha = _activeOpacity;

            ProcessDragFromScreen(screenPos);
        }

        /// <summary>
        /// Tính knob offset bằng cách:
        ///   1. Convert screenPos → _canvasRect local space (origin = canvas center).
        ///   2. Delta = localInCanvas - bgRect.anchoredPosition.
        ///      Hoạt động vì anchor bg = center = cùng space với localInCanvas.
        ///   3. Clamp delta trong _radius → đặt vào knob.anchoredPosition.
        /// </summary>
        private void ProcessDragFromScreen(Vector2 screenPos)
        {
            if (_canvasRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, _cachedEventCamera, out Vector2 localInCanvas);

            Vector2 delta   = localInCanvas - _bgRect.anchoredPosition;
            Vector2 clamped = Vector2.ClampMagnitude(delta, _radius);

            if (_knob != null)
                _knob.anchoredPosition = clamped;

            // ── Analog deadzone remapping ─────────────────────────────────────
            // raw = giá trị [-1, 1] giữ nguyên hướng thực (30° → 30°, không snap)
            float rawMag = _radius > 0f ? clamped.magnitude / _radius : 0f;
            // FIX #9: đọc _cachedDeadzone thay vì gọi nullable chain mỗi frame
            float dead   = _cachedDeadzone;

            Vector2 result;
            if (rawMag < dead)
            {
                // Trong vùng deadzone → zero (loại bỏ rung tay)
                result = Vector2.zero;
            }
            else
            {
                // Remap [dead..1] → [0..1] để không bị jump khi ra khỏi deadzone
                // Giữ nguyên HƯỚNG (direction) từ clamped → góc 30° vẫn là 30°
                // BUG 4 FIX: guard chia zero — nếu clamped.magnitude quá nhỏ do floating
                // point thì trả về zero thay vì NaN → tránh transform.Translate(NaN).
                float remappedMag = Mathf.InverseLerp(dead, 1f, rawMag);
                float mag         = clamped.magnitude;
                result = mag > 1e-5f
                    ? (clamped / mag) * remappedMag
                    : Vector2.zero;
            }

            RawInput = result;
            _inputBridge?.OverrideMoveInput(result);
            OnInputChanged?.Invoke(result);
        }

        private void ResetJoystick()
        {
            _isDragging = false;
            _touchId    = -1;
            RawInput    = Vector2.zero;

            if (_knob != null)
                _knob.anchoredPosition = Vector2.zero;

            if (_bgRect != null)
                _bgRect.anchoredPosition = _anchoredOrigin;
            
            SetIdle();

            _inputBridge?.ClearMoveOverride();
            OnReleased?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PLACEMENT
        // ══════════════════════════════════════════════════════════════════════

        private void PlaceJoystickAt(Vector2 screenPos)
        {
            if (_canvasRect == null) return;

            // screen → canvas local (origin = canvas center)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, _cachedEventCamera, out Vector2 localInCanvas);

            Rect  cr    = _canvasRect.rect;
            float halfW = cr.width  * 0.5f;
            float halfH = cr.height * 0.5f;

            float scaleFactor = (_canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
            float pad         = _radius + (_edgeMarginPx / scaleFactor);

            // Giới hạn ngang theo _activationZoneWidthRatio:
            //   Vùng joystick = [0, _activationZoneWidthRatio * screenWidth]
            //   Trong canvas space (origin = center):
            //     cạnh trái  = -halfW
            //     cạnh phải của vùng = -halfW + _activationZoneWidthRatio * cr.width
            float zoneRightEdge = -halfW + (_activationZoneWidthRatio * cr.width);

            float cx = Mathf.Clamp(localInCanvas.x,
                                   -halfW + pad,
                                   zoneRightEdge - pad);
            float cy = Mathf.Clamp(localInCanvas.y, -halfH + pad, halfH - pad);

            _bgRect.anchoredPosition = new Vector2(cx, cy);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VISIBILITY
        // ══════════════════════════════════════════════════════════════════════

        // SetVisible chỉ dùng cho Dynamic mode khi đang drag
        private void SetVisible(bool visible)
        {
            if (_bgImage)   _bgImage.enabled   = visible;
            if (_knobImage) _knobImage.enabled  = visible;
            if (_canvasGroup)
            {
                _canvasGroup.alpha          = visible ? _activeOpacity : _idleOpacity;
                _canvasGroup.blocksRaycasts = visible;
            }
        }
        
        // trạng thái idle đúng cho cả 2 mode
        private void SetIdle()
        {
            if (_dynamicMode)
            {
                // Dynamic: ẩn hoàn toàn, chờ touch mới hiện
                if (_bgImage)   _bgImage.enabled   = true;
                if (_knobImage) _knobImage.enabled  = true;
                if (_canvasGroup)
                {
                    _canvasGroup.alpha          = _idleOpacity;
                    _canvasGroup.blocksRaycasts = false;
                }
            }
            else
            {
                // Fixed: luôn hiện image, chỉ dùng idleOpacity
                if (_bgImage)   _bgImage.enabled   = true;
                if (_knobImage) _knobImage.enabled  = true;
                _bgRect.anchoredPosition = _anchoredOrigin;
                if (_canvasGroup)
                {
                    _canvasGroup.alpha          = _idleOpacity;
                    _canvasGroup.blocksRaycasts = true;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private Camera GetEventCamera()
        {
            if (_canvas == null) return null;
            return _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC SETUP
        // ══════════════════════════════════════════════════════════════════════

        public void InjectDependencies(TISInputBridge bridge)
        {
            _inputBridge = bridge;
            // FIX #9: update cache ngay khi bridge được inject
            var s = bridge?.TISMobileSettings;
            if (s != null) _cachedDeadzone = s.joystickDeadzone;
        }

        public void InjectKnob(RectTransform knob)
        {
            _knob      = knob;
            _knobImage = knob?.GetComponent<Image>();
        }

        public void ApplyProfileSettings()
        {
            var s = _inputBridge?.TISMobileSettings;
            if (s == null) { if (_radius <= 0f) _radius = 100f; return; }

            // Chỉ đọc radius từ profile khi chưa được set bởi TISMobileInputSetup.
            // Nếu _radiusSetExternally = true → TISMobileInputSetup đã truyền đúng
            // giá trị _joystickBGDiameter → không ghi đè.
            if (!_radiusSetExternally)
            {
                _radius = s.joystickRadius;
                if (_bgRect != null)
                    _bgRect.sizeDelta = new Vector2(_radius * 2f, _radius * 2f);
            }

            // Chỉ đọc idleOpacity / dynamicJoystick từ profile khi chưa bị TISMobileInputSetup
            // ghi đè bằng SetIdleOpacity() / SetDynamicMode(). Nếu đã set externally →
            // giữ nguyên giá trị custom, không để profile-swap ghi đè mất.
            if (!_idleOpacitySetExternally)
                _idleOpacity = s.idleOpacity;

            if (!_dynamicModeSetExternally)
                _dynamicMode = s.dynamicJoystick;

            // FIX #9: cache deadzone một lần tại đây — ProcessDragFromScreen() đọc _cachedDeadzone
            // thay vì gọi nullable chain _inputBridge?.TISMobileSettings?.joystickDeadzone mỗi frame.
            _cachedDeadzone = s.joystickDeadzone;

            if (_radius <= 0f) _radius = 100f;
        }

        public void SetRadius(float radius)
        {
            _radius = Mathf.Max(10f, radius);
            _radiusSetExternally = true;
            if (_bgRect != null)
                _bgRect.sizeDelta = new Vector2(_radius * 2f, _radius * 2f);
        }

        // P2 FIX #12: Public setter thay thế reflection DatField("_activationZoneWidthRatio").
        public void SetActivationZoneWidthRatio(float ratio) => _activationZoneWidthRatio = ratio;

        /// <summary>
        /// Ghi đè opacity khi joystick đang active (đang được chạm/kéo).
        /// Field này không nằm trong TISMobileSettings (profile) nên không có
        /// vấn đề bị profile-swap ghi đè — có thể set trực tiếp bất kỳ lúc nào.
        /// </summary>
        public void SetActiveOpacity(float opacity) => _activeOpacity = Mathf.Clamp01(opacity);

        /// <summary>
        /// Ghi đè dynamicJoystick (true = hiện tại điểm chạm, false = cố định vị trí).
        /// Đánh dấu externally-set để ApplyProfileSettings() (gọi khi Start hoặc khi
        /// profile đổi) không ghi đè lại giá trị custom này bằng giá trị từ profile.
        /// </summary>
        public void SetDynamicMode(bool dynamic)
        {
            _dynamicMode = dynamic;
            _dynamicModeSetExternally = true;
        }

        /// <summary>
        /// Ghi đè idleOpacity (opacity khi joystick không được chạm).
        /// Đánh dấu externally-set — xem SetDynamicMode().
        /// </summary>
        public void SetIdleOpacity(float opacity)
        {
            _idleOpacity = Mathf.Clamp01(opacity);
            _idleOpacitySetExternally = true;
        }

        // Setter dùng bởi Editor DLL qua delegate — không cần #if UNITY_EDITOR
        public void SetKnobSizeRatioDebug(float ratio) => _knobSizeRatioDebug = ratio;

        // ══════════════════════════════════════════════════════════════════════
        //  DEBUG PREVIEW — OnGUI (Editor only)
        // ══════════════════════════════════════════════════════════════════════

        [Header("Debug Preview")]
        [Tooltip("Hiện debug preview màn hình điện thoại với joystick BG/Knob và phân vùng.")]
        [SerializeField] private bool _debugPreview = false;

        [Tooltip("Knob size ratio để dùng trong debug preview (khớp với _knobSizeRatio ở TISMobileInputSetup).")]
        [Range(0.2f, 0.85f)]
        [SerializeField] private float _knobSizeRatioDebug = 0.5f;

        // Delegate để Editor DLL inject OnGUI logic mà không cần #if UNITY_EDITOR trong Runtime
        public static Action<TISVirtualJoystick> EditorOnGUIOverride = null;

        private void OnGUI()
        {
            if (!_debugPreview) return;
            if (!Application.isEditor) return;
            EditorOnGUIOverride?.Invoke(this);
        }

        // Expose các field/method cần thiết cho Editor DLL qua Reflection
        private const float _referenceWidth  = 1920f;
        private const float _referenceHeight = 1080f;
    }
}