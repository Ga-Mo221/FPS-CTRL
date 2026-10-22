// ============================================================
//  TrueInputSystem — TIS_MobileSkillButton.cs  (Runtime)
//
//  Skill button mobile cơ bản:
//    · PointerDown → SetState(true) 1 frame → GetSkillAction() bắt được
//    · Hold        → OnHold / OnHoldTimed mỗi frame
//    · PointerUp   → SetState(false), phát OnRelease
//    · Pointer thoát vùng button → tự release tránh stuck-pressed
//
//  CÁCH DÙNG:
//    // Đọc trạng thái trong game script
//    if (TISMobileButtonRegistry.WasPressedThisFrame("Attack")) { ... }
//    // Hoặc qua event
//    btn.OnPress   += () => { ... };
//    btn.OnRelease += () => { ... };
// ============================================================

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Mobile Button")]
    [RequireComponent(typeof(Image))]
    public class TISMobileSkillButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IDragHandler
    {
        // ══════════════════════════════════════════════════════
        //  ENUM
        // ══════════════════════════════════════════════════════

        public enum VisualMode
        {
            /// <summary>Tint màu Image — giống Unity Button mặc định.</summary>
            ColorTint,
            /// <summary>Đổi Sprite theo trạng thái — màu tint cố định.</summary>
            SpriteSwap,
        }

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — BINDING
        // ══════════════════════════════════════════════════════

        [Tooltip("Tên action khớp với TISInputProfileConfig.\n" +
                 "TISInputBridge.GetSkillAction(skillName) sẽ đọc trạng thái này.")]
        [SerializeField] private string _skillName = "Attack";

        [Tooltip("Giống Selectable.interactable của Unity.\n" +
                 "false → không nhận touch, hiển thị visual riêng.\n" +
                 "Ưu tiên cao hơn Disabled/Cooldown.")]
        [SerializeField] private bool _interactable = true;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — VISUAL FEEDBACK
        // ══════════════════════════════════════════════════════

        [Tooltip("false = không thể touch xuống màn hình. bị chặn lại bởi button.\n" +
            "true = có thể touch xuống màn hình.")]
        [SerializeField] private bool _OnTouchCamera = false;

        [Tooltip("ColorTint  — đổi màu tint theo trạng thái.\n" +
                 "SpriteSwap — đổi Sprite theo trạng thái, màu tint cố định.")]
        [SerializeField] private VisualMode _visualMode = VisualMode.ColorTint;

        [Range(0f, 0.3f)]
        [Tooltip("Thời gian flash màu khi nhấn.")]
        [SerializeField] private float _flashDuration = 0.1f;

        // ── ColorTint ─────────────────────────────────────────
        [Tooltip("Màu khi idle.")]
        [SerializeField] private Color _normalColor             = new Color(1f,    1f,    1f,    0.70f);

        [Tooltip("Màu khi đang nhấn.")]
        [SerializeField] private Color _pressedColor            = new Color(0.6f,  0.85f, 1f,    0.95f);

        [Tooltip("Màu khi disabled (cooldown v.v.).")]
        [SerializeField] private Color _disabledColor           = new Color(0.4f,  0.4f,  0.4f,  0.50f);

        [Tooltip("Màu khi not interactable.\nMặc định mờ hơn disabled.")]
        [SerializeField] private Color _notInteractableColor    = new Color(0.35f, 0.35f, 0.35f, 0.35f);

        // ── SpriteSwap ────────────────────────────────────────
        [Tooltip("Màu tint cố định — thường để White để sprite nguyên màu.")]
        [SerializeField] private Color _spriteTintColor         = Color.white;

        [Tooltip("Sprite khi idle.")]
        [SerializeField] private Sprite _normalSprite           = null;

        [Tooltip("Sprite khi đang nhấn.")]
        [SerializeField] private Sprite _pressedSprite          = null;

        [Tooltip("Sprite khi disabled (cooldown).\nĐể null → dùng Normal + giảm alpha.")]
        [SerializeField] private Sprite _disabledSprite         = null;

        [Tooltip("Sprite khi not interactable.\nĐể null → dùng Normal + giảm alpha riêng.")]
        [SerializeField] private Sprite _notInteractableSprite  = null;

        [Range(0f, 1f)]
        [Tooltip("Alpha fallback khi _disabledSprite = null.")]
        [SerializeField] private float _disabledAlpha           = 0.45f;

        [Range(0f, 1f)]
        [Tooltip("Alpha fallback khi _notInteractableSprite = null.")]
        [SerializeField] private float _notInteractableAlpha    = 0.25f;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — COOLDOWN
        // ══════════════════════════════════════════════════════

        [Tooltip("Image hiển thị cooldown fill (Type = Filled, tỉ lệ 0–1).\n" +
                 "Để null nếu không cần.")]
        [SerializeField] private Image _cooldownFill;

        // ══════════════════════════════════════════════════════
        //  INSPECTOR — EVENTS
        // ══════════════════════════════════════════════════════

        /// <summary>Gọi một lần đúng frame khi nút được nhấn xuống (PointerDown).</summary>
        public UnityEvent OnPress;

        /// <summary>Gọi một lần đúng frame khi nút được nhả ra hoặc pointer thoát khỏi vùng nút (PointerUp / PointerExit).</summary>
        public UnityEvent OnRelease;

        [Tooltip("Gọi mỗi frame khi đang giữ nút.\n" +
                 "Dùng cho: charge skill, hold-to-cast, auto-attack.")]
        public UnityEvent OnHold;

        [Tooltip("Gọi mỗi frame khi đang giữ, truyền thời gian giữ (giây) làm tham số.\n" +
                 "Dùng cho: charge bar, progress indicator, hold threshold detection.")]
        public UnityEvent<float> OnHoldTimed;

        // ══════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════

        private Image _image;
        private bool  _isPressed;

        /// <summary>Disabled do SetDisabled() — block input + đổi màu tối.</summary>
        private bool  _disabledByExternal;

        /// <summary>Disabled do SetCooldownFill() — block input, KHÔNG đổi màu.</summary>
        private bool  _disabledByCooldown;

        /// <summary>Button bị block input khi bất kỳ nguồn nào active.</summary>
        private bool  IsDisabled => _disabledByExternal || _disabledByCooldown;

        private float _flashTimer;
        private float _holdTime;
        private int   _activePointerId = -1;

        // ── Public ──

        /// <summary>
        /// Tên action được bind, khớp với tên khai báo trong <c>TISInputProfileConfig</c>.
        /// </summary>
        public string SkillName => _skillName;

        /// <summary>Thời gian đã giữ nút tính bằng giây. Reset về 0 khi nhả.</summary>
        public float HoldTime => _holdTime;

        /// <summary>
        /// Bật/tắt interactable. Ưu tiên cao hơn SetDisabled (cooldown).
        /// </summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                if (!_interactable && _isPressed)
                    ReleaseButton();
                UpdateVisual(_isPressed);
            }
        }

        // ══════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_cooldownFill != null)
                _cooldownFill.fillAmount = 0f;
        }

        private void Start()
        {
            UpdateVisual(false);
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                if (_flashTimer <= 0f)
                    UpdateVisual(_isPressed);
            }

            if (_isPressed)
            {
                _holdTime += Time.unscaledDeltaTime;
                OnHold?.Invoke();
                OnHoldTimed?.Invoke(_holdTime);
            }
        }

        private void OnDisable()
        {
            if (_isPressed)
                ReleaseButton();
        }

        // ══════════════════════════════════════════════════════
        //  POINTER EVENTS
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Xử lý sự kiện pointer down từ uGUI EventSystem.
        /// Bị bỏ qua nếu button không interactable hoặc đang bị disabled/cooldown.
        /// Ghi nhận touch vào <see cref="TISMobileButtonRegistry"/> và phát <see cref="OnPress"/>.
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable || IsDisabled) return;

            // Mỗi ngón một chủ: EventSystem đã giao ngón này cho nút → camera không bao giờ
            // tự giành được nó. On Touch Camera = ON: nút CHIA ngón cho camera zone để
            // vừa giữ nút (bắn) vừa kéo ngón xoay camera (ngắm) — kiểu CODM / PUBG.
            if (_OnTouchCamera)
                TISTouchCameraZone.Current?.AddPointer(eventData);

            _activePointerId = eventData.pointerId;
            PressButton();
        }

        /// <summary>Bỏ drag threshold để camera nhận chuyển động ngay (On Touch Camera).</summary>
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (_OnTouchCamera) eventData.useDragThreshold = false;
        }

        /// <summary>Chỉ cần cho EventSystem legacy — camera đọc vị trí trực tiếp với Input System UI module.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (_OnTouchCamera && eventData.pointerId == _activePointerId)
                TISTouchCameraZone.Current?.MovePointer(eventData.pointerId, eventData.position);
        }

        /// <summary>
        /// Xử lý sự kiện pointer up từ uGUI EventSystem.
        /// Giải phóng touch khỏi <see cref="TISMobileButtonRegistry"/> và phát <see cref="OnRelease"/>.
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed) return;
            if (_OnTouchCamera)
                TISTouchCameraZone.Current?.RemovePointer(_activePointerId);
            _activePointerId = -1;
            ReleaseButton();
        }

        /// <summary>
        /// Xử lý sự kiện pointer exit từ uGUI EventSystem.
        /// Nếu đang nhấn, tự động release button để tránh stuck-pressed khi ngón tay trượt ra ngoài vùng.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // On Touch Camera: kéo ngón ra khỏi nút để ngắm là bình thường → giữ nút tới khi nhấc tay
            if (_isPressed && !_OnTouchCamera)
            {
                _activePointerId = -1;
                ReleaseButton();
            }
        }

        // ══════════════════════════════════════════════════════
        //  BUTTON LOGIC
        // ══════════════════════════════════════════════════════

        private void PressButton()
        {
            _isPressed = true;
            _holdTime  = 0f;
            TISMobileButtonRegistry.SetState(_skillName, true);

            if (_visualMode == VisualMode.ColorTint)
            {
                _flashTimer = _flashDuration;
                if (_image) _image.color = _pressedColor;
            }
            else
            {
                ApplySpriteSwap(ButtonState.Pressed);
            }

            OnPress?.Invoke();
        }

        private void ReleaseButton()
        {
            _isPressed = false;
            _holdTime  = 0f;
            TISMobileButtonRegistry.SetState(_skillName, false);
            UpdateVisual(false);
            OnRelease?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        //  VISUAL UPDATE
        // ══════════════════════════════════════════════════════

        private enum ButtonState { Normal, Pressed, Disabled, NotInteractable }

        private void UpdateVisual(bool pressed)
        {
            if (_image == null) return;

            ButtonState state;
            if (!_interactable)           state = ButtonState.NotInteractable;
            else if (_disabledByExternal) state = ButtonState.Disabled; // SetDisabled() → màu tối
            else if (_disabledByCooldown) state = ButtonState.Normal;   // cooldown → giữ màu bình thường
            else if (pressed)             state = ButtonState.Pressed;
            else                          state = ButtonState.Normal;

            if (_visualMode == VisualMode.ColorTint)
                ApplyColorTint(state);
            else
                ApplySpriteSwap(state);
        }

        // ── ColorTint ─────────────────────────────────────────

        private void ApplyColorTint(ButtonState state)
        {
            _image.color = state switch
            {
                ButtonState.NotInteractable => _notInteractableColor,
                ButtonState.Disabled        => _disabledColor,
                ButtonState.Pressed         => _pressedColor,
                _                           => _normalColor,
            };
        }

        // ── SpriteSwap ────────────────────────────────────────

        private void ApplySpriteSwap(ButtonState state)
        {
            switch (state)
            {
                case ButtonState.NotInteractable:
                    _image.sprite = _notInteractableSprite != null
                        ? _notInteractableSprite : _normalSprite;
                    _image.color  = FadeIf(_notInteractableSprite == null, _notInteractableAlpha);
                    break;

                case ButtonState.Disabled:
                    _image.sprite = _disabledSprite != null
                        ? _disabledSprite : _normalSprite;
                    _image.color  = FadeIf(_disabledSprite == null, _disabledAlpha);
                    break;

                case ButtonState.Pressed:
                    _image.sprite = _pressedSprite != null ? _pressedSprite : _normalSprite;
                    _image.color  = _spriteTintColor;
                    break;

                default:
                    _image.sprite = _normalSprite;
                    _image.color  = _spriteTintColor;
                    break;
            }
        }

        private Color FadeIf(bool condition, float alpha)
        {
            if (!condition) return _spriteTintColor;
            Color c = _spriteTintColor;
            c.a = alpha;
            return c;
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Bật/tắt disabled state từ bên ngoài (không đủ mana, stance sai, skill bị khóa, v.v.).
        /// Khi <paramref name="disabled"/> là <c>true</c>: block input và hiển thị visual tối.
        /// </summary>
        public void SetDisabled(bool disabled)
        {
            _disabledByExternal = disabled;
            if (disabled && _isPressed)
                ReleaseButton();
            UpdateVisual(_isPressed);
        }

        /// <summary>
        /// Hiển thị tiến độ cooldown qua Image fill và block input trong thời gian đó.
        /// <paramref name="fillAmount"/> = 1 nghĩa là cooldown đầy (không thể dùng), 0 nghĩa là sẵn sàng.
        /// </summary>
        public void SetCooldownFill(float fillAmount)
        {
            if (_cooldownFill == null) return;
            _cooldownFill.fillAmount = Mathf.Clamp01(fillAmount);

            bool isCooling = fillAmount > 0f;
            _disabledByCooldown = isCooling;
            if (isCooling && _isPressed)
                ReleaseButton();
            UpdateVisual(_isPressed);
        }

        /// <summary>
        /// Đổi visual mode lúc runtime giữa <see cref="VisualMode.ColorTint"/> và <see cref="VisualMode.SpriteSwap"/>.
        /// </summary>
        public void SetVisualMode(VisualMode mode)
        {
            _visualMode = mode;
            UpdateVisual(_isPressed);
        }
    }
}