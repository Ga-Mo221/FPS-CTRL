// ============================================================
//  TrueInputSystem — TIS_DirectionalSkillButton.cs
//
//  Skill button điều hướng kiểu Liên Quân / Tốc Chiến:
//    · Nhấn giữ + kéo → chọn hướng kỹ năng (Vector2 direction)
//    · Nhả tay         → cast skill theo hướng đã chọn
//    · Tap nhanh       → cast ngay (quick tap, direction = zero)
//    · Kéo vào Cancel Zone + nhả → HỦY kỹ năng (không cast)
//
//  ══════════════════════════════════════════════════════════
//  CANCEL ZONE (LIÊN QUÂN STYLE)
//  ══════════════════════════════════════════════════════════
//  · Tạo một UI GameObject (ví dụ: hình X hoặc icon hủy) ở giữa màn hình
//  · Assign RectTransform vào _cancelZone trong Inspector
//  · GameObject đó nên SetActive(false) ban đầu — script tự bật/tắt
//  · Assign Image vào _cancelZoneImage để được đổi màu khi hover
//  · Lắng nghe event OnCancelled để xử lý logic hủy ở game script:
//      btn.OnCancelled.AddListener(() => Debug.Log("Skill cancelled!"));
//  · Hoặc check property: btn.IsInCancelZone (true khi hover trong vùng hủy)
//
//  ══════════════════════════════════════════════════════════
//  TÍCH HỢP VỚI TIS
//  ══════════════════════════════════════════════════════════
//  · Ghi skillName vào TISMobileButtonRegistry (PressDown 1 frame)
//    → GetSkillAction("Skill1") hoạt động bình thường
//  · Ghi direction vào TISDirectionalRegistry (static store)
//    → TISDirectionalRegistry.GetDirection("Skill1_Dir")
//
//  ══════════════════════════════════════════════════════════
//  CÁCH DÙNG TRONG GAME SCRIPT
//  ══════════════════════════════════════════════════════════
//  // Option A: đọc từ component trực tiếp
//  if (btn.WasCastThisFrame)
//  {
//      Vector2 dir   = btn.CastDirection;   // normalized, zero = auto-target
//      float   angle = btn.DirectionAngle;  // 0° = phải, 90° = trên
//      SpawnProjectile(dir);
//  }
//
//  // Option B: registry (giống GetSkillAction pattern)
//  if (TISMobileButtonRegistry.WasPressedThisFrame("Skill1"))
//  {
//      Vector2 dir = TISDirectionalRegistry.GetDirection("Skill1_Dir");
//      SpawnProjectile(dir);
//  }
//
//  // Option C: event
//  btn.OnDirectionCast += (skillName, dir) => SpawnProjectile(dir);
// ============================================================

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Mobile Button Direction")]
    [RequireComponent(typeof(Image))]
    public class TISDirectionalSkillButton : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
    {
        // ══════════════════════════════════════════════════════
        //  ENUMS
        // ══════════════════════════════════════════════════════

        public enum CastMode
        {
            /// <summary>
            /// Nhả tay → cast theo hướng đang kéo (chuẩn Liên Quân).
            /// Tap nhanh (dưới quickTapMaxSeconds) → cast với direction = zero (auto-target).
            /// Kéo chưa đủ threshold + giữ lâu → không cast.
            /// </summary>
            DragRelease,

            /// <summary>
            /// Nhả tay → luôn cast, dù kéo bao xa.
            /// Tap = cast với direction = zero.
            /// </summary>
            Always,
        }

        public enum IndicatorStyle
        {
            /// <summary>Mũi tên xoay theo hướng kéo (hình chữ nhật nếu không có sprite).</summary>
            Arrow,
            /// <summary>Đường thẳng kéo dài từ tâm button.</summary>
            Line,
            /// <summary>Dot tròn di chuyển theo vị trí ngón tay (kiểu joystick knob).</summary>
            Knob,
            /// <summary>Không hiện indicator.</summary>
            None,
        }

        public enum VisualMode
        {
            /// <summary>Chỉ đổi màu tint — Sprite không thay đổi.</summary>
            ColorTint,
            /// <summary>Chỉ đổi Sprite — Color luôn giữ nguyên Color.white.</summary>
            SpriteSwap,
        }

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — BINDING
        // ══════════════════════════════════════════════════════

        [Header("Binding")]
        [Tooltip("Tên action Button trong TISInputProfileConfig.\n" +
                 "GetSkillAction(skillName) trả về true đúng 1 frame khi cast.")]
        [SerializeField] private string _skillName = "Skill1";

        [Tooltip("Key dùng để lưu hướng vào TISDirectionalRegistry.\n" +
                 "Để trống → tự dùng skillName + '_Dir' (vd: 'Skill1_Dir').")]
        [SerializeField] private string _directionActionName = "";

        [Tooltip("false = button block touch, camera zone không nhận ngón này.\n" +
                 "true  = touch xuyên qua xuống camera zone.")]
        [SerializeField] private bool _OnTouchCamera = false;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — BEHAVIOUR
        // ══════════════════════════════════════════════════════

        [Header("Behaviour")]
        [Tooltip("DragRelease: nhả tay mới cast (chuẩn Liên Quân / Tốc Chiến).\n" +
                 "Always: luôn cast khi nhả, kể cả tap.")]
        [SerializeField] private CastMode _castMode = CastMode.DragRelease;

        [Range(5f, 120f)]
        [Tooltip("Khoảng cách pixel tối thiểu phải kéo mới tính là có hướng.\n" +
                 "Dưới ngưỡng này = tap nhanh (auto-target).")]
        [SerializeField] private float _dragThresholdPx = 20f;

        [Range(0.05f, 0.5f)]
        [Tooltip("Thời gian tối đa (giây) để nhận là 'tap nhanh'.\n" +
                 "Nếu giữ lâu hơn và chưa kéo → không cast khi nhả (chờ kéo).")]
        [SerializeField] private float _quickTapMaxSeconds = 0.2f;

        [Tooltip("Bật → tap nhanh cast ngay với direction = zero (auto-target).\n" +
                 "Tắt → phải kéo mới cast.")]
        [SerializeField] private bool _allowQuickTap = true;

        [Range(0f, 300f)]
        [Tooltip("Giới hạn khoảng cách kéo từ tâm button (pixel). 0 = không giới hạn.")]
        [SerializeField] private float _maxDragRadiusPx = 120f;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — VISUAL: BUTTON
        // ══════════════════════════════════════════════════════

        [Header("Visual — Button Colors & Sprites")]
        [Tooltip("ColorTint: chỉ đổi màu, giữ sprite.\nSpriteSwap: chỉ đổi sprite, color = white.")]
        [SerializeField] private VisualMode _buttonVisualMode = VisualMode.ColorTint;

        [SerializeField] private Color  _normalColor    = new Color(1f,    1f,    1f,    0.75f);
        [Tooltip("Sprite thay thế khi Normal. Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _normalSprite   = null;

        [SerializeField] private Color  _draggingColor  = new Color(0.45f, 0.85f, 1f,    0.95f);
        [Tooltip("Sprite thay thế khi đang kéo. Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _draggingSprite = null;

        [SerializeField] private Color  _castColor      = new Color(0.3f,  1f,    0.55f, 0.95f);
        [Tooltip("Sprite flash khi cast. Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _castSprite     = null;

        [SerializeField] private Color  _disabledColor  = new Color(0.4f,  0.4f,  0.4f,  0.50f);
        [Tooltip("Sprite khi disabled. Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _disabledSprite = null;

        [Range(0f, 0.3f)]
        [Tooltip("Thời gian flash màu/sprite sau khi cast.")]
        [SerializeField] private float _castFlashDuration = 0.12f;

        // Sprite gốc — lưu để restore
        private Sprite _originalButtonSprite;

        [Header("Cooldown")]
        [Tooltip("Image hiển thị fill cooldown (Type = Filled). Để null nếu không cần.")]
        [SerializeField] private Image _cooldownFill = null;

        [Header("Range Circle (optional)")]
        [Tooltip("Image vòng tròn tầm kỹ năng — hiện khi đang kéo, ẩn khi nhả.")]
        [SerializeField] private Image _rangeCircleImage = null;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — CANCEL ZONE (kiểu Liên Quân)
        // ══════════════════════════════════════════════════════

        [Header("Cancel Zone (Liên Quân style)")]
        [Tooltip("RectTransform của UI Cancel (vùng hủy kỹ năng).\n" +
                 "Kéo ngón tay vào đây khi đang chọn hướng → hủy, không cast.\n" +
                 "Để null → không có tính năng hủy.")]
        [SerializeField] private RectTransform _cancelZone = null;

        [Tooltip("Image của Cancel Zone để đổi màu khi hover vào.\n" +
                 "Để null → chỉ dùng RectTransform để detect vùng, không đổi màu.")]
        [SerializeField] private Image _cancelZoneImage = null;

        [Tooltip("ColorTint: chỉ đổi màu cancel zone.\nSpriteSwap: chỉ đổi sprite, color = white.")]
        [SerializeField] private VisualMode _cancelZoneVisualMode = VisualMode.ColorTint;

        [Tooltip("Màu Cancel Zone khi idle (đang kéo nhưng chưa vào vùng hủy).")]
        [SerializeField] private Color  _cancelZoneNormalColor  = new Color(1f,   1f,   1f,   0.5f);
        [Tooltip("Sprite Cancel Zone khi idle. Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _cancelZoneNormalSprite = null;

        [Tooltip("Màu Cancel Zone khi ngón tay đang ở trong vùng hủy — sẵn sàng cancel.")]
        [SerializeField] private Color  _cancelZoneActiveColor  = new Color(1f,   0.25f,0.25f,0.95f);
        [Tooltip("Sprite Cancel Zone khi active (ngón tay đang trong vùng hủy). Null = giữ sprite gốc.")]
        [SerializeField] private Sprite _cancelZoneActiveSprite = null;

        [Tooltip("Màu indicator khi ngón tay đang ở vùng hủy.")]
        [SerializeField] private Color  _indicatorCancelColor   = new Color(1f,   0.2f, 0.2f, 0.9f);
        [Tooltip("Sprite indicator khi ngón tay ở vùng hủy. Null = dùng sprite mặc định.")]
        [SerializeField] private Sprite _indicatorCancelSprite  = null;

        // Sprite gốc của cancel zone — lưu để restore
        private Sprite _originalCancelZoneSprite;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — VISUAL: DIRECTION INDICATOR
        // ══════════════════════════════════════════════════════

        [Header("Visual — Direction Indicator")]
        [SerializeField] private IndicatorStyle _indicatorStyle = IndicatorStyle.Arrow;

        [Tooltip("ColorTint: chỉ đổi màu indicator, giữ sprite.\nSpriteSwap: chỉ đổi sprite, color = white.")]
        [SerializeField] private VisualMode _indicatorVisualMode = VisualMode.ColorTint;

        [Tooltip("RectTransform gốc của indicator.\n" +
                 "Để null → tự tạo một hình chữ nhật đơn giản.\n" +
                 "Assign GameObject có Image component để dùng sprite mũi tên custom.")]
        [SerializeField] private RectTransform _indicatorRoot = null;

        [Tooltip("Sprite dùng cho indicator (mũi tên, line, v.v.).\n" +
                 "Để null → solid color hình chữ nhật.")]
        [SerializeField] private Sprite _indicatorSprite = null;

        [Range(20f, 250f)]
        [Tooltip("Độ dài indicator (pixel canvas). Đo từ tâm button.")]
        [SerializeField] private float _indicatorLength = 80f;

        [Range(4f, 50f)]
        [Tooltip("Độ rộng indicator (pixel canvas).")]
        [SerializeField] private float _indicatorWidth = 16f;

        [Tooltip("Màu indicator khi đang kéo nhưng chưa đủ threshold.")]
        [SerializeField] private Color  _indicatorColor        = new Color(1f,   0.85f, 0.3f, 0.75f);
        [Tooltip("Sprite indicator khi đang kéo chưa đủ threshold. Null = dùng sprite mặc định.")]
        [SerializeField] private Sprite _indicatorDraggingSprite = null;

        [Tooltip("Màu indicator khi đã đủ threshold — sẵn sàng cast.")]
        [SerializeField] private Color  _indicatorReadyColor   = new Color(0.3f, 1f,    0.5f, 0.95f);
        [Tooltip("Sprite indicator khi sẵn sàng cast. Null = dùng sprite mặc định.")]
        [SerializeField] private Sprite _indicatorReadySprite  = null;

        [Range(10f, 80f)]
        [Tooltip("Kích thước (diameter) của knob dot khi dùng style Knob (pixel canvas).")]
        [SerializeField] private float _knobSize = 36f;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — EVENTS
        // ══════════════════════════════════════════════════════

        [Header("Events")]
        [Tooltip("Gọi một lần khi bắt đầu kéo (vượt dragThreshold).")]
        public UnityEvent                  OnDragStarted;

        [Tooltip("Gọi mỗi frame khi đang kéo. Param = hướng hiện tại (normalized).")]
        public UnityEvent<Vector2>         OnDirectionChanged;

        [Tooltip("Gọi đúng frame khi cast. Param = hướng cast (zero = quick tap).")]
        public UnityEvent<Vector2>         OnCast;

        [Tooltip("Gọi đúng frame khi cast — kèm tên skill.\n" +
                 "Dùng khi 1 handler xử lý nhiều directional button.")]
        public UnityEvent<string, Vector2> OnDirectionCast;

        [Tooltip("Gọi khi người dùng kéo vào Cancel Zone và nhả tay → hủy kỹ năng.")]
        public UnityEvent                  OnCancelled;

        // ══════════════════════════════════════════════════════
        //  PUBLIC READ-ONLY PROPERTIES
        // ══════════════════════════════════════════════════════

        /// <summary>Hướng hiện tại trong khi kéo (normalized). Vector2.zero nếu chưa kéo đủ.</summary>
        public Vector2 Direction        { get; private set; }

        /// <summary>Góc của hướng theo độ. 0° = phải, 90° = lên, ngược chiều kim đồng hồ.</summary>
        public float   DirectionAngle   => Direction.sqrMagnitude > 0.001f
                                            ? Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg
                                            : 0f;

        /// <summary>true khi đang kéo (đã vượt dragThreshold).</summary>
        public bool    IsDragging       { get; private set; }

        /// <summary>true đúng 1 frame ngay khi cast xảy ra.</summary>
        public bool    WasCastThisFrame { get; private set; }

        /// <summary>Hướng đã cast trong frame này. Vector2.zero = quick tap (auto-target).</summary>
        public Vector2 CastDirection    { get; private set; }

        /// <summary>true khi ngón tay đang hover trong Cancel Zone — indicator sẽ đỏ.</summary>
        public bool    IsInCancelZone   => _isInCancelZone;

        public string  SkillName        => _skillName;
        public bool    Interactable     => !IsDisabled;

        // ══════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════

        private Image         _image;
        private RectTransform _rectTransform;
        private Canvas        _canvas;
        private Camera        _uiCamera;

        // Touch tracking
        private bool    _isPointerDown;
        private int     _activePointerId = -1;
        private float   _pointerDownTime;
        private Vector2 _pointerDownScreenPos;
        private Vector2 _currentScreenPos;
        private bool    _dragEventFired;

        // Visual
        private float _flashTimer;

        // Disable state
        private bool _disabledByExternal;
        private bool _disabledByCooldown;
        private bool IsDisabled => _disabledByExternal || _disabledByCooldown;

        // Indicator (auto-created)
        private RectTransform _autoIndicatorRoot;
        private Image         _indicatorImage;

        // Registry release — SetState(true) trong ExecuteCast, SetState(false) trong LateUpdate
        private bool _pendingRegistryRelease;

        // Direction clear — delay 1 frame sau cast để game code kịp đọc direction
        //private bool _pendingDirectionClear;
        
        // Thay _pendingDirectionClear bool bằng int counter
        private int _directionClearCountdown; // 0 = không pending

        // Cancel zone tracking
        private bool _isInCancelZone;

        // Direction key (computed once)
        private string DirKey => string.IsNullOrEmpty(_directionActionName)
                                    ? _skillName + "_Dir"
                                    : _directionActionName;

        // ══════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _image         = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _canvas        = GetComponentInParent<Canvas>();
            _uiCamera      = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                                ? _canvas.worldCamera : null;

            BuildIndicator();

            // Lưu sprite gốc để restore
            _originalButtonSprite    = _image != null ? _image.sprite : null;
            _originalCancelZoneSprite = _cancelZoneImage != null ? _cancelZoneImage.sprite : null;
        }

        private void Start()
        {
            SetIndicatorActive(false);
            if (_rangeCircleImage != null) _rangeCircleImage.gameObject.SetActive(false);
            ApplyButtonColor(false);
        }

        private void Update()
        {
            // Reset per-frame cast flags ở đầu frame
            WasCastThisFrame = false;
            CastDirection    = Vector2.zero;

            // Flash countdown
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                if (_flashTimer <= 0f)
                    ApplyButtonColor(IsDragging);
            }
        }

        private void LateUpdate()
        {
            // Release registry state sau 1 frame (đảm bảo WasPressedThisFrame() đọc được)
            if (_pendingRegistryRelease)
            {
                TISMobileButtonRegistry.SetState(_skillName, false);
                _pendingRegistryRelease = false;
            }

            // Clear direction sau 1 frame — đảm bảo game code đọc được direction đúng frame cast
            // ── FIX: delay direction clear thêm 1 frame ──────────
            // Frame cast  : _directionClearCountdown = 2 (set bởi CleanupAfterRelease)
            // Frame cast+1: countdown → 1, direction vẫn sống (game code đọc ở đây)
            // Frame cast+2: countdown → 0, clear direction
            if (_directionClearCountdown > 0)
            {
                _directionClearCountdown--;
                if (_directionClearCountdown == 0)
                {
                    TISDirectionalRegistry.SetDirection(DirKey, Vector2.zero);
                    if (!string.IsNullOrEmpty(_directionActionName))
                        TISDirectionalRegistry.ClearVector2Override(_directionActionName);
                    //_pendingDirectionClear = false;
                }
            }
        }

        private void OnDisable()
        {
            ForceReset();
        }

        // ══════════════════════════════════════════════════════
        //  POINTER EVENTS
        // ══════════════════════════════════════════════════════

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isPointerDown) return;  // đang có touch khác
            if (IsDisabled)     return;

            _isPointerDown        = true;
            _activePointerId      = eventData.pointerId;
            _pointerDownTime      = Time.unscaledTime;
            _pointerDownScreenPos = eventData.position;
            _currentScreenPos     = eventData.position;
            _dragEventFired       = false;
            IsDragging            = false;
            Direction             = Vector2.zero;

            if (!_OnTouchCamera)
                TISMobileButtonRegistry.ConsumePointerByPositionMapped(
                    eventData.pointerId, eventData.position);

            if (_rangeCircleImage != null)
                _rangeCircleImage.gameObject.SetActive(true);

            // Hiện Cancel Zone khi bắt đầu hold
            if (_cancelZone != null)
            {
                _cancelZone.gameObject.SetActive(true);
                _isInCancelZone = false;
                if (_cancelZoneImage != null)
                    _cancelZoneImage.color = _cancelZoneNormalColor;
            }

            ApplyButtonColor(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isPointerDown || eventData.pointerId != _activePointerId) return;

            _currentScreenPos = eventData.position;
            Vector2 delta     = eventData.position - _pointerDownScreenPos;
            float   dist      = delta.magnitude;

            // Kích hoạt drag khi vượt threshold
            if (!IsDragging && dist >= _dragThresholdPx)
            {
                IsDragging = true;
                if (!_dragEventFired)
                {
                    _dragEventFired = true;
                    OnDragStarted?.Invoke();
                }
            }

            if (!IsDragging) return;

            // Clamp radius nếu cần
            Vector2 clamped = _maxDragRadiusPx > 0f
                ? Vector2.ClampMagnitude(delta, _maxDragRadiusPx)
                : delta;

            Direction = clamped.sqrMagnitude > 0.001f ? clamped.normalized : Vector2.zero;

            // Ghi vào registry liên tục khi kéo
            TISDirectionalRegistry.SetDirection(DirKey, Direction);

            // Override action Vector2 real-time (nếu _directionActionName được set)
            if (!string.IsNullOrEmpty(_directionActionName))
                TISDirectionalRegistry.SetVector2Override(_directionActionName, Direction);

            ApplyButtonColor(true);
            RefreshIndicator(dist);
            OnDirectionChanged?.Invoke(Direction);

            // ── Cancel Zone detection ──────────────────────────
            if (_cancelZone != null)
            {
                bool wasInCancel = _isInCancelZone;
                _isInCancelZone  = RectTransformUtility.RectangleContainsScreenPoint(
                    _cancelZone, eventData.position, _uiCamera);

                if (_isInCancelZone != wasInCancel)
                {
                    // Đổi màu + sprite Cancel Zone
                    if (_cancelZoneImage != null)
                    {
                        var targetCancelSprite = _isInCancelZone ? _cancelZoneActiveSprite : _cancelZoneNormalSprite;
                        if (_cancelZoneVisualMode == VisualMode.SpriteSwap)
                        {
                            _cancelZoneImage.color  = Color.white;
                            _cancelZoneImage.sprite = targetCancelSprite != null ? targetCancelSprite : _originalCancelZoneSprite;
                        }
                        else
                        {
                            _cancelZoneImage.color  = _isInCancelZone ? _cancelZoneActiveColor : _cancelZoneNormalColor;
                            _cancelZoneImage.sprite = _originalCancelZoneSprite;
                        }
                    }

                    // Đổi màu + sprite indicator
                    if (_indicatorImage != null && _isInCancelZone)
                    {
                        if (_indicatorVisualMode == VisualMode.SpriteSwap)
                        {
                            _indicatorImage.color  = Color.white;
                            if (_indicatorCancelSprite != null)
                                _indicatorImage.sprite = _indicatorCancelSprite;
                        }
                        else
                        {
                            _indicatorImage.color  = _indicatorCancelColor;
                        }
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPointerDown || eventData.pointerId != _activePointerId) return;
            HandleRelease();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // KHÔNG cancel khi ngón trượt ra ngoài button — vẫn tiếp tục nhận OnDrag
            // (EventSystem tự gửi IDragHandler kể cả ngoài rect).
            // OnPointerExit chỉ fire khi ngón ra khỏi rect, không khi nhấc lên.
        }

        // ══════════════════════════════════════════════════════
        //  CORE LOGIC
        // ══════════════════════════════════════════════════════

        private void HandleRelease()
        {
            if (!_OnTouchCamera)
                TISMobileButtonRegistry.ReleasePointerMapped(_activePointerId);

            // ── Hủy kỹ năng nếu nhả trong Cancel Zone ─────────
            if (_isInCancelZone && _cancelZone != null)
            {
                OnCancelled?.Invoke();
                CleanupAfterRelease();
                return;
            }

            float   holdTime     = Time.unscaledTime - _pointerDownTime;
            float   dragDist     = (_currentScreenPos - _pointerDownScreenPos).magnitude;
            bool    isQuickTap   = holdTime <= _quickTapMaxSeconds && !IsDragging;
            bool    hasDirection = IsDragging && dragDist >= _dragThresholdPx;

            bool    shouldCast = false;
            Vector2 castDir    = Vector2.zero;

            switch (_castMode)
            {
                case CastMode.DragRelease:
                    if (hasDirection)
                    {
                        shouldCast = true;
                        castDir    = Direction;
                    }
                    else if (isQuickTap && _allowQuickTap)
                    {
                        shouldCast = true;
                        castDir    = Vector2.zero; // auto-target
                    }
                    // Giữ lâu mà không kéo → không cast (chờ user kéo đúng hướng)
                    break;

                case CastMode.Always:
                    shouldCast = true;
                    castDir    = hasDirection ? Direction : Vector2.zero;
                    break;
            }

            if (shouldCast)
                ExecuteCast(castDir);

            CleanupAfterRelease();
        }

        private void ExecuteCast(Vector2 castDir)
        {
            // 1. Button pressed state (1 frame) → GetSkillAction() bắt được
            TISMobileButtonRegistry.SetState(_skillName, true);
            _pendingRegistryRelease = true; // LateUpdate sẽ SetState(false)

            // 2. Lưu direction cuối cùng
            TISDirectionalRegistry.SetDirection(DirKey, castDir);

            // 3. Frame-level state
            WasCastThisFrame = true;
            CastDirection    = castDir;

            // 4. Visual flash
            _flashTimer = _castFlashDuration;
            if (_image != null)
            {
                if (_buttonVisualMode == VisualMode.SpriteSwap)
                {
                    _image.color  = Color.white;
                    _image.sprite = _castSprite != null ? _castSprite : _originalButtonSprite;
                }
                else
                {
                    _image.color  = _castColor;
                    _image.sprite = _originalButtonSprite;
                }
            }

            // 5. Events
            OnCast?.Invoke(castDir);
            OnDirectionCast?.Invoke(_skillName, castDir);
        }

        private void CleanupAfterRelease()
        {
            _isPointerDown   = false;
            _activePointerId = -1;
            IsDragging       = false;
            Direction        = Vector2.zero;

            // Direction registry: nếu vừa cast thì delay clear sang LateUpdate (1 frame sau)
            // để game code kịp đọc direction trong cùng frame cast.
            // Nếu không cast (cancel / ForceReset) thì clear ngay.
            if (_pendingRegistryRelease)
            {
                // Đang pending cast → delay direction clear cùng với registry release
                //_pendingDirectionClear = true;
                _directionClearCountdown  = 2;
            }
            else
            {
                // Không có cast (cancelled, reset) → clear ngay
                TISDirectionalRegistry.SetDirection(DirKey, Vector2.zero);
                if (!string.IsNullOrEmpty(_directionActionName))
                    TISDirectionalRegistry.ClearVector2Override(_directionActionName);
            }

            SetIndicatorActive(false);
            if (_rangeCircleImage != null)
                _rangeCircleImage.gameObject.SetActive(false);

            // Ẩn Cancel Zone và reset state
            if (_cancelZone != null)
            {
                _cancelZone.gameObject.SetActive(false);
                _isInCancelZone = false;
                if (_cancelZoneImage != null)
                {
                    _cancelZoneImage.color  = _cancelZoneNormalColor;
                    _cancelZoneImage.sprite = _originalCancelZoneSprite;
                }
            }

            ApplyButtonColor(false);
        }

        private void ForceReset()
        {
            if (_isPointerDown)
            {
                TISMobileButtonRegistry.ReleasePointerMapped(_activePointerId);
                TISMobileButtonRegistry.SetState(_skillName, false);
                TISDirectionalRegistry.SetDirection(DirKey, Vector2.zero);
                if (!string.IsNullOrEmpty(_directionActionName))
                    TISDirectionalRegistry.ClearVector2Override(_directionActionName);
            }
            _pendingRegistryRelease = false;
            //_pendingDirectionClear  = false;
            _isPointerDown          = false;
            _activePointerId        = -1;
            IsDragging              = false;
            Direction               = Vector2.zero;
            SetIndicatorActive(false);
            if (_rangeCircleImage != null)
                _rangeCircleImage.gameObject.SetActive(false);
            if (_cancelZone != null)
            {
                _cancelZone.gameObject.SetActive(false);
                _isInCancelZone = false;
                if (_cancelZoneImage != null)
                {
                    _cancelZoneImage.color  = _cancelZoneNormalColor;
                    _cancelZoneImage.sprite = _originalCancelZoneSprite;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  VISUAL — BUTTON COLOR
        // ══════════════════════════════════════════════════════

        private void ApplyButtonColor(bool dragging)
        {
            if (_image == null || _flashTimer > 0f) return;

            Color  targetColor;
            Sprite targetSprite;

            if (IsDisabled)
            {
                targetColor  = _disabledColor;
                targetSprite = _disabledSprite;
            }
            else if (dragging)
            {
                targetColor  = _draggingColor;
                targetSprite = _draggingSprite;
            }
            else
            {
                targetColor  = _normalColor;
                targetSprite = _normalSprite;
            }

            if (_buttonVisualMode == VisualMode.SpriteSwap)
            {
                _image.color  = Color.white;
                _image.sprite = targetSprite != null ? targetSprite : _originalButtonSprite;
            }
            else // ColorTint
            {
                _image.color  = targetColor;
                _image.sprite = _originalButtonSprite;
            }
        }

        // ══════════════════════════════════════════════════════
        //  VISUAL — INDICATOR
        // ══════════════════════════════════════════════════════

        private void BuildIndicator()
        {
            if (_indicatorStyle == IndicatorStyle.None) return;
            if (_indicatorRoot  != null)
            {
                // Dùng indicator user đã assign
                _indicatorImage = _indicatorRoot.GetComponent<Image>();
                return;
            }

            // Auto-create
            var go = new GameObject("DirectionIndicator", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            _autoIndicatorRoot = go.GetComponent<RectTransform>();
            _indicatorRoot     = _autoIndicatorRoot;
            _indicatorImage    = go.GetComponent<Image>();

            if (_indicatorStyle == IndicatorStyle.Knob)
            {
                // Knob: pivot ở giữa, di chuyển bằng anchoredPosition
                _autoIndicatorRoot.anchorMin        = new Vector2(0.5f, 0.5f);
                _autoIndicatorRoot.anchorMax        = new Vector2(0.5f, 0.5f);
                _autoIndicatorRoot.pivot            = new Vector2(0.5f, 0.5f);
                _autoIndicatorRoot.anchoredPosition = Vector2.zero;
                _autoIndicatorRoot.sizeDelta        = new Vector2(_knobSize, _knobSize);

                // Dùng sprite nếu có, nếu không → circle bằng Sprite.Create hoặc solid
                if (_indicatorSprite != null)
                    _indicatorImage.sprite = _indicatorSprite;
                else
                {
                    // Tạo sprite hình tròn đơn giản từ texture
                    _indicatorImage.sprite = BuildCircleSprite(32);
                }
            }
            else
            {
                // Arrow / Line: pivot ở bottom (0.5, 0) → indicator mọc ra từ tâm button
                _autoIndicatorRoot.anchorMin        = new Vector2(0.5f, 0.5f);
                _autoIndicatorRoot.anchorMax        = new Vector2(0.5f, 0.5f);
                _autoIndicatorRoot.pivot            = new Vector2(0.5f, 0f);
                _autoIndicatorRoot.anchoredPosition = Vector2.zero;
                _autoIndicatorRoot.sizeDelta        = new Vector2(_indicatorWidth, _indicatorLength);

                if (_indicatorSprite != null)
                    _indicatorImage.sprite = _indicatorSprite;
            }

            _indicatorImage.color = _indicatorColor;
        }

        /// <summary>Tạo Sprite hình tròn 32×32 bằng code (dùng khi Knob không assign sprite).</summary>
        private static Sprite BuildCircleSprite(int resolution)
        {
            var tex    = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            float half = resolution * 0.5f;
            float r2   = half * half;
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - half + 0.5f, dy = y - half + 0.5f;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= r2 ? Color.white : Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }

        private void SetIndicatorActive(bool active)
        {
            if (_indicatorRoot != null)
                _indicatorRoot.gameObject.SetActive(active);
        }

        private void RefreshIndicator(float currentDistPx)
        {
            if (_indicatorStyle == IndicatorStyle.None || _indicatorRoot == null) return;
            if (!IsDragging || Direction.sqrMagnitude < 0.001f)
            {
                SetIndicatorActive(false);
                return;
            }

            SetIndicatorActive(true);

            bool ready = currentDistPx >= _dragThresholdPx;

            if (_indicatorStyle == IndicatorStyle.Knob)
            {
                // Di chuyển knob theo hướng drag, clamp trong maxDragRadius
                float clampedDist  = _maxDragRadiusPx > 0f
                    ? Mathf.Min(currentDistPx, _maxDragRadiusPx)
                    : currentDistPx;
                // Scale từ screen px → canvas local px
                float scaleFactor  = _canvas != null ? 1f / _canvas.scaleFactor : 1f;
                _indicatorRoot.anchoredPosition = Direction * clampedDist * scaleFactor;
                _indicatorRoot.localRotation    = Quaternion.identity; // knob không xoay
            }
            else
            {
                // Arrow / Line: xoay theo hướng, không di chuyển
                float angle = Mathf.Atan2(Direction.x, Direction.y) * Mathf.Rad2Deg;
                _indicatorRoot.localRotation    = Quaternion.Euler(0f, 0f, -angle);
                _indicatorRoot.anchoredPosition = Vector2.zero;
            }

            // Đổi màu + sprite khi sẵn sàng cast (chỉ khi không ở Cancel Zone)
            if (_indicatorImage != null)
            {
                Color  targetIndicatorColor;
                Sprite targetIndicatorSprite;

                if (_isInCancelZone)
                {
                    targetIndicatorColor  = _indicatorCancelColor;
                    targetIndicatorSprite = _indicatorCancelSprite;
                }
                else if (ready)
                {
                    targetIndicatorColor  = _indicatorReadyColor;
                    targetIndicatorSprite = _indicatorReadySprite;
                }
                else
                {
                    targetIndicatorColor  = _indicatorColor;
                    targetIndicatorSprite = _indicatorDraggingSprite;
                }

                if (_indicatorVisualMode == VisualMode.SpriteSwap)
                {
                    _indicatorImage.color  = Color.white;
                    if (targetIndicatorSprite != null)
                        _indicatorImage.sprite = targetIndicatorSprite;
                }
                else // ColorTint
                {
                    _indicatorImage.color = targetIndicatorColor;
                    if (targetIndicatorSprite != null)
                        _indicatorImage.sprite = targetIndicatorSprite;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Bật/tắt button từ game code (thiếu mana, skill locked, v.v.).
        /// </summary>
        public void SetDisabled(bool disabled)
        {
            _disabledByExternal = disabled;
            if (disabled && _isPointerDown)
            {
                TISMobileButtonRegistry.ReleasePointerMapped(_activePointerId);
                CleanupAfterRelease();
            }
            ApplyButtonColor(IsDragging);
        }

        /// <summary>
        /// Set fill cooldown và block input khi đang cooldown.
        /// fillAmount = 1 → đầy cooldown (không thể dùng). 0 → sẵn sàng.
        /// </summary>
        public void SetCooldownFill(float fillAmount)
        {
            if (_cooldownFill != null)
                _cooldownFill.fillAmount = Mathf.Clamp01(fillAmount);

            bool wasCooling   = _disabledByCooldown;
            _disabledByCooldown = fillAmount > 0f;

            if (!wasCooling && _disabledByCooldown && _isPointerDown)
            {
                TISMobileButtonRegistry.ReleasePointerMapped(_activePointerId);
                CleanupAfterRelease();
            }
            ApplyButtonColor(IsDragging);
        }

        /// <summary>
        /// Force cast bằng code (không cần touch — dùng cho tutorial / automation).
        /// </summary>
        public void ForceCast(Vector2 direction)
        {
            if (IsDisabled) return;
            ExecuteCast(direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  TISDirectionalRegistry — Static store cho direction data
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Registry tĩnh lưu direction vector của directional skill buttons.
    /// Tách riêng khỏi TISMobileButtonRegistry (bool) vì direction là Vector2.
    ///
    /// CÁCH ĐỌC (direction sau cast):
    ///   Vector2 dir = TISDirectionalRegistry.GetDirection("Skill1_Dir");
    ///   // dir = Vector2.zero nếu quick tap (auto-target) hoặc chưa cast
    ///
    /// CÁCH ĐỌC (override action Vector2 — đọc qua bridge):
    ///   Vector2 aim = TISInputBridge.Instance.GetVector2Action("AimDirection");
    ///   // Trả về Direction đang kéo real-time, hoặc Vector2.zero khi nhả.
    /// </summary>
    public static class TISDirectionalRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<string, Vector2> _directions
            = new System.Collections.Generic.Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);

        // ── Vector2 Action Override ───────────────────────────
        // Cho phép TISDirectionalSkillButton ghi đè một action Vector2 trong profile
        // để game code đọc qua bridge.GetVector2Action(actionName) mà không cần component.

        private static readonly System.Collections.Generic.Dictionary<string, Vector2> _v2Overrides
            = new System.Collections.Generic.Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Ghi đè giá trị của một action Vector2 theo tên.
        /// Gọi bởi TISDirectionalSkillButton khi <c>_directionActionName</c> được set.
        /// TISActionReader sẽ ưu tiên đọc giá trị này thay vì InputAction.
        /// </summary>
        public static void SetVector2Override(string actionName, Vector2 value)
        {
            if (string.IsNullOrEmpty(actionName)) return;
            _v2Overrides[actionName] = value;
        }

        /// <summary>
        /// Xóa override của một action Vector2 — trả về đọc InputAction bình thường.
        /// </summary>
        public static void ClearVector2Override(string actionName)
        {
            if (!string.IsNullOrEmpty(actionName))
                _v2Overrides.Remove(actionName);
        }

        /// <summary>
        /// Kiểm tra xem action có đang bị override không và lấy giá trị.
        /// Dùng bởi TISActionReader.GetVector2Action().
        /// </summary>
        public static bool TryGetVector2Override(string actionName, out Vector2 value)
        {
            if (!string.IsNullOrEmpty(actionName) && _v2Overrides.TryGetValue(actionName, out value))
                return true;
            value = Vector2.zero;
            return false;
        }

        // ── Direction (sau cast) ──────────────────────────────

        /// <summary>Ghi hướng. Gọi bởi TISDirectionalSkillButton (internal).</summary>
        public static void SetDirection(string key, Vector2 direction)
        {
            if (string.IsNullOrEmpty(key)) return;
            _directions[key] = direction;
        }

        /// <summary>
        /// Đọc hướng kỹ năng (normalized).
        /// Trả về Vector2.zero nếu key không tồn tại, chưa cast, hoặc quick tap.
        /// </summary>
        public static Vector2 GetDirection(string key)
        {
            if (string.IsNullOrEmpty(key)) return Vector2.zero;
            return _directions.TryGetValue(key, out var v) ? v : Vector2.zero;
        }

        /// <summary>Xóa toàn bộ — gọi tự động khi scene unload.</summary>
        public static void ClearAll()
        {
            _directions.Clear();
            _v2Overrides.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterHook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene _)
            => ClearAll();
    }
}