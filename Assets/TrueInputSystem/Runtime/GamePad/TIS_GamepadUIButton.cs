using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Gamepad UI Button")]
    [DefaultExecutionOrder(-50)]
    public class TISGamepadUIButton : Button
    {
        // ── Enums ─────────────────────────────────────────────────────────────

        // [Aemy 2026-10-05] >0 = dang co overlay (chon nhan vat/avatar) -> chan input menu phia sau.
        public static int ExternalSuppress = 0;

        public enum NavMode     { FourDirectional, Horizontal, Vertical }
        public enum InputSource { Default, LB_RB, LT_RT }

        /// <summary>
        /// Layer trong hệ thống navigation phân cấp.
        /// Tab    → LT/RT  (always active, virtual selected)
        /// SubTab → LB/RB  (always active, virtual selected)
        /// Content→ DPad/Stick (real EventSystem focus)
        /// </summary>
        public enum NavigationLayer { Tab, SubTab, Content }

        // ── Inspector fields ──────────────────────────────────────────────────

        [HideInInspector] public NavMode          navigationMode = NavMode.FourDirectional;
        [HideInInspector] public InputSource       inputSource    = InputSource.Default;
        [HideInInspector] public NavigationLayer   layer          = NavigationLayer.Content;
        [HideInInspector] public float             holdDelay      = 0.2f;

        [HideInInspector] public TISGamepadUIButton navUp;
        [HideInInspector] public TISGamepadUIButton navDown;
        [HideInInspector] public TISGamepadUIButton navLeft;
        [HideInInspector] public TISGamepadUIButton navRight;
        
        // Assigned by TISUINavigationContext.ScanChildButtons() or manually.
        [HideInInspector] public TISUINavigationContext context;

        /// <summary>
        /// Khi bật, button này duy trì visual "Selected" dù EventSystem focus ở nơi khác.
        /// Dùng cho Tab và SubTab layer.
        /// </summary>
        [HideInInspector] public bool alwaysActive = false;

        /// <summary>
        /// Khi bật, button này sẽ tự nhận EventSystem focus sau 1 frame khi GameObject được kích hoạt.
        /// Chỉ hoạt động với Content layer.
        /// </summary>
        [HideInInspector] public bool isFirstSelected = false;

        /// <summary>
        /// Bật/tắt button này khỏi hệ thống navigation gamepad.
        /// Khi false: button không xử lý input, không nhận focus, không di chuyển sang button khác.
        /// </summary>
        [SerializeField] private bool isNavigationEnabled = true;

        // ── Hold-delay per layer (static, shared) ─────────────────────────────
        // Button chỉ đọc/ghi cooldown, KHÔNG giữ active state — Context giữ.
        internal static readonly Dictionary<NavigationLayer, float>
            s_NextMoveTime = new Dictionary<NavigationLayer, float>
            {
                { NavigationLayer.Tab,     0f },
                { NavigationLayer.SubTab,  0f },
                { NavigationLayer.Content, 0f },
            };

        // ── Events ────────────────────────────────────────────────────────────

        [System.Serializable] public class SelectEvent : UnityEngine.Events.UnityEvent { }
        [HideInInspector] public SelectEvent onSelect;
        [HideInInspector] public SelectEvent onDeselect;

        /// <summary>
        /// Fired khi button này trở thành "active" trong layer của nó.
        /// UINavigationContext lắng nghe event này để redirect focus.
        /// </summary>
        [System.Serializable] public class LayerChangedEvent : UnityEngine.Events.UnityEvent<TISGamepadUIButton> { }
        [HideInInspector] public LayerChangedEvent onBecameActiveInLayer;

        // ── Cache ─────────────────────────────────────────────────────────────

        private ScrollRect parentScrollRect;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            parentScrollRect = GetComponentInParent<ScrollRect>();
            navigation = new Navigation { mode = Navigation.Mode.None };

            // Log mỗi lần nhấn (Submit/Click) — chỉ đăng ký ở runtime
            if (Application.isPlaying)
                onClick.AddListener(LogPress);
        }

        private void LogPress()
        {
            if (context != null)
                context.Log($"<color=#A5D6A7>[Press · {layer}]</color> \"{name}\"");
            else
                Debug.Log($"[TIS Nav] [Press · {layer}] \"{name}\"", this);
        }

        protected override void Start()
        {
            base.Start();
            // Đăng ký với Context để Context biết button này tồn tại
            if (context)
                context.RegisterButton(this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Selectable là [ExecuteAlways] → OnEnable cũng chạy ở Edit Mode (sau mỗi lần
            // reload script). Lúc đó EventSystem không chạy → cảnh báo nhầm. Chỉ chọn khi Play.
            if (Application.isPlaying && isFirstSelected && layer == NavigationLayer.Content)
                StartCoroutine(SelectNextFrame());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Báo Context biết button này đang disable để Context cập nhật state
            if (context != null)
                context.OnButtonDisabled(this);
        }

        protected override void OnDestroy()
        {
            if (Application.isPlaying)
                onClick.RemoveListener(LogPress);
            base.OnDestroy();
            if (context != null)
                context.UnregisterButton(this);
        }

        // Số frame chờ EventSystem (vd. EventSystem nằm trong scene load additive sau)
        private const int EventSystemWaitFrames = 10;

        private System.Collections.IEnumerator SelectNextFrame()
        {
            for (int i = 0; i < EventSystemWaitFrames; i++)
            {
                yield return null;
                if (EventSystem.current == null) continue;

                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(gameObject);
                yield break;
            }

            Debug.LogWarning(
                $"[GamepadUIButton] \"{name}\": không có EventSystem đang bật sau {EventSystemWaitFrames} frame " +
                "→ không thể chọn sẵn button (Is First Selected).", this);
        }

        // ── Select / Deselect ─────────────────────────────────────────────────

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            onSelect?.Invoke();
            SnapToScrollView();

            // Báo Context: button này vừa được select (qua EventSystem thật)
            if (context != null)
                context.NotifyButtonSelected(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (alwaysActive)
            {
                DoStateTransition(SelectionState.Highlighted, false);
                onDeselect?.Invoke();
                return;
            }

            base.OnDeselect(eventData);
            onDeselect?.Invoke();
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!isNavigationEnabled) return;
            if (ExternalSuppress > 0) return;                                  // overlay dang mo
            // Controller-first [Aemy]: tay cầm điều hướng đầy đủ. Bàn phím / chuột: chỉ phím mũi tên
            // điều hướng Content (không WASD — trùng phím di chuyển; không Q/E/Z/C — trùng phím game).
            var _brg = TISInputBridge.Instance;
            bool kbNav = _brg != null && _brg.IsKeyboardMouse;
            if (_brg != null && !_brg.IsGamepad && !kbNav) return;

            bool isFocused = EventSystem.current != null
                             && EventSystem.current.currentSelectedGameObject == gameObject;

            if (isFocused)
            {
                // Button đang có real focus → đọc input, gửi lên Context xử lý
                HandleContentInput();
            }
            else if (alwaysActive && !kbNav)
            {
                // Tab/SubTab: chỉ button đang active trong layer mới đọc input
                if (context != null
                    && context.IsActiveInLayer(this))
                    HandleLayerInput();
            }
        }

        // ── Content layer input ───────────────────────────────────────────────

        private void HandleContentInput()
        {
            if (Time.unscaledTime < s_NextMoveTime[NavigationLayer.Content]) return;

            Vector2 inputDir = ReadInputDirection();

            if (Mathf.Abs(inputDir.x) > 0.5f) inputDir.x = Mathf.Sign(inputDir.x); else inputDir.x = 0;
            if (Mathf.Abs(inputDir.y) > 0.5f) inputDir.y = Mathf.Sign(inputDir.y); else inputDir.y = 0;

            if (inputDir == Vector2.zero) return;

            TISGamepadUIButton target = ResolveTarget(inputDir);

            // Delegate lên Context: Context kiểm tra state rồi quyết định có move không
            if (context != null)
                context.RequestContentMove(this, target);
            else if (target != null && target.gameObject.activeInHierarchy)
            {
                // Không có Context → tự navigate như cũ
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                s_NextMoveTime[NavigationLayer.Content] = Time.unscaledTime + holdDelay;
            }
        }

        // ── Tab / SubTab layer input ──────────────────────────────────────────

        private void HandleLayerInput()
        {
            if (Time.unscaledTime < s_NextMoveTime[layer]) return;

            float axis = ReadLayerAxis();
            if (axis == 0f) return;

            TISGamepadUIButton target = null;
            if (axis > 0f) target = (navigationMode == NavMode.Vertical) ? navDown  : navRight;
            else           target = (navigationMode == NavMode.Vertical) ? navUp    : navLeft;

            // Delegate lên Context: Context kiểm tra state rồi quyết định có move không
            if (context != null)
                context.RequestLayerMove(this, target);
        }

        // ── Public API (gọi từ Context) ───────────────────────────────────────
        
        /// <summary>
        /// Called by TISUINavigationContext.ScanChildButtons() to wire this button
        /// to its owning context. Can also be set manually in the inspector.
        /// </summary>
        public void SetContext(TISUINavigationContext ctx) => context = ctx;

        /// <summary>
        /// Context gọi hàm này để set button thành active trong layer (Tab/SubTab).
        /// Button chỉ lo visual và fire event — KHÔNG tự ghi state vào dictionary.
        /// </summary>
        public void ApplyActiveVisual()
        {
            DoStateTransition(SelectionState.Highlighted, false);
            onSelect?.Invoke();
            onBecameActiveInLayer?.Invoke(this);
        }

        /// <summary>
        /// Context gọi hàm này để clear active visual của button (Tab/SubTab).
        /// </summary>
        public void ClearActiveVisual()
        {
            DoStateTransition(SelectionState.Normal, false);
            onDeselect?.Invoke();
        }

        /// <summary>
        /// Tắt navigation — button sẽ không nhận input hay di chuyển focus sang button khác.
        /// </summary>
        public void DisableNavigation()
        {
            isNavigationEnabled = false;
            DoStateTransition(SelectionState.Normal, false);
        }

        /// <summary>
        /// Bật lại navigation — button có thể nhận input trở lại.
        /// </summary>
        public void EnableNavigation()
        {
            isNavigationEnabled = true;
        }

        // ── Input readers ─────────────────────────────────────────────────────

        private Vector2 ReadInputDirection()
        {
            Vector2 inputDir = Vector2.zero;
            Gamepad gamepad  = Gamepad.current;

            if (gamepad != null)
            {
                if (inputSource == InputSource.Default)
                {
                    Vector2 dpad  = gamepad.dpad.ReadValue();
                    // Virtual cursor đang bật → left stick dành cho cursor, không điều hướng button
                    Vector2 stick = TISGamepadVirtualCursor.IsCursorActive
                        ? Vector2.zero
                        : gamepad.leftStick.ReadValue();
                    inputDir = (dpad.sqrMagnitude > 0.1f) ? dpad : stick;
                }
                else if (inputSource == InputSource.LB_RB)
                {
                    if (gamepad.leftShoulder.isPressed)  inputDir = (navigationMode == NavMode.Horizontal) ? Vector2.left  : Vector2.up;
                    if (gamepad.rightShoulder.isPressed) inputDir = (navigationMode == NavMode.Horizontal) ? Vector2.right : Vector2.down;
                }
                else if (inputSource == InputSource.LT_RT)
                {
                    if (gamepad.leftTrigger.isPressed)  inputDir = (navigationMode == NavMode.Horizontal) ? Vector2.left  : Vector2.up;
                    if (gamepad.rightTrigger.isPressed) inputDir = (navigationMode == NavMode.Horizontal) ? Vector2.right : Vector2.down;
                }
            }

            if (inputDir == Vector2.zero && Keyboard.current != null)
            {
                if (inputSource == InputSource.Default)
                {
                    // Chỉ phím mũi tên — WASD là phím di chuyển, đọc ở đây sẽ làm UI nhảy khi đi bộ
                    if (Keyboard.current.leftArrowKey.isPressed)  inputDir.x = -1;
                    if (Keyboard.current.rightArrowKey.isPressed) inputDir.x =  1;
                    if (Keyboard.current.upArrowKey.isPressed)    inputDir.y =  1;
                    if (Keyboard.current.downArrowKey.isPressed)  inputDir.y = -1;
                }
            }

            return inputDir;
        }

        private float ReadLayerAxis()
        {
            Gamepad gamepad = Gamepad.current;

            if (gamepad != null)
            {
                if (inputSource == InputSource.LT_RT)
                {
                    if (gamepad.rightTrigger.isPressed) return  1f;
                    if (gamepad.leftTrigger.isPressed)  return -1f;
                }
                else if (inputSource == InputSource.LB_RB)
                {
                    if (gamepad.rightShoulder.isPressed) return  1f;
                    if (gamepad.leftShoulder.isPressed)  return -1f;
                }
            }

            if (Keyboard.current != null)
            {
                if (layer == NavigationLayer.Tab)
                {
                    if (Keyboard.current.eKey.isPressed) return  1f;
                    if (Keyboard.current.qKey.isPressed) return -1f;
                }
                else if (layer == NavigationLayer.SubTab)
                {
                    if (Keyboard.current.cKey.isPressed) return  1f;
                    if (Keyboard.current.zKey.isPressed) return -1f;
                }
            }

            return 0f;
        }

        // ── Resolve navigation target ─────────────────────────────────────────

        private TISGamepadUIButton ResolveTarget(Vector2 inputDir)
        {
            TISGamepadUIButton target = null;

            if (navigationMode == NavMode.FourDirectional || navigationMode == NavMode.Horizontal)
            {
                if      (inputDir.x > 0) target = navRight;
                else if (inputDir.x < 0) target = navLeft;
            }

            if ((navigationMode == NavMode.FourDirectional || navigationMode == NavMode.Vertical) && target == null)
            {
                if      (inputDir.y > 0) target = navUp;
                else if (inputDir.y < 0) target = navDown;
            }

            return target;
        }

        // ── Snap to ScrollView ────────────────────────────────────────────────

        private void SnapToScrollView()
        {
            if (parentScrollRect == null) return;

            RectTransform viewport = parentScrollRect.viewport;
            RectTransform content  = parentScrollRect.content;
            RectTransform target   = GetComponent<RectTransform>();

            if (viewport == null || content == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            Vector3 buttonLocalPos = viewport.InverseTransformPoint(target.position);
            Vector3 newContentPos  = content.anchoredPosition;
            float   padding        = 25f;

            if (parentScrollRect.vertical)
            {
                float buttonMinY = buttonLocalPos.y - (target.rect.height * target.pivot.y);
                float buttonMaxY = buttonLocalPos.y + (target.rect.height * (1f - target.pivot.y));
                float viewMinY   = viewport.rect.yMin;
                float viewMaxY   = viewport.rect.yMax;

                if (buttonMinY < viewMinY + padding)
                    newContentPos.y += (viewMinY + padding) - buttonMinY;
                else if (buttonMaxY > viewMaxY - padding)
                    newContentPos.y -= buttonMaxY - (viewMaxY - padding);
            }

            if (parentScrollRect.horizontal)
            {
                float buttonMinX = buttonLocalPos.x - (target.rect.width * target.pivot.x);
                float buttonMaxX = buttonLocalPos.x + (target.rect.width * (1f - target.pivot.x));
                float viewMinX   = viewport.rect.xMin;
                float viewMaxX   = viewport.rect.xMax;

                if (buttonMinX < viewMinX + padding)
                    newContentPos.x += (viewMinX + padding) - buttonMinX;
                else if (buttonMaxX > viewMaxX - padding)
                    newContentPos.x -= buttonMaxX - (viewMaxX - padding);
            }

            content.anchoredPosition = newContentPos;
        }
    }
}