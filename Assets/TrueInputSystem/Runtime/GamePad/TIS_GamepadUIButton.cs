using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Gamepad UI Button")]
    [DefaultExecutionOrder(-50)]
    public class TISGamepadUIButton : Button
    {
        // ── Enums ─────────────────────────────────────────────────────────────

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
        /// Dùng để "đóng băng" một nhóm button khi panel khác đang active.
        /// </summary>
        [SerializeField] private bool isNavigationEnabled = true;

        // ── Multi-layer Active Registry ───────────────────────────────────────
        // Mỗi layer giữ đúng 1 "virtual selected" button.
        // Content layer KHÔNG dùng registry này — nó dùng EventSystem thật.
        private static readonly Dictionary<NavigationLayer, TISGamepadUIButton>
            s_ActiveButtons = new Dictionary<NavigationLayer, TISGamepadUIButton>();

        // Hold delay dùng chung, tránh race condition giữa các layer
        private static readonly Dictionary<NavigationLayer, float>
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
        /// Fired khi button này trở thành "active" trong layer của nó thông qua
        /// global layer input (tức là Tab/SubTab đổi trong khi focus đang ở nơi khác).
        /// UINavigationContext lắng nghe event này để redirect focus về content đầu tiên.
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
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (isFirstSelected && layer == NavigationLayer.Content)
                StartCoroutine(SelectNextFrame());
        }

        private System.Collections.IEnumerator SelectNextFrame()
        {
            yield return null;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            else
            {
                Debug.LogWarning("[GamepadUIButton] Không tìm thấy EventSystem trong Scene! " +
                                 "Vui lòng tạo một cái (GameObject → UI → Event System).");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Dọn registry khi button bị destroy
            if (s_ActiveButtons.TryGetValue(layer, out var current) && current == this)
                s_ActiveButtons.Remove(layer);
        }

        // ── Select / Deselect ─────────────────────────────────────────────────

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            onSelect?.Invoke();
            SnapToScrollView();

            if (alwaysActive)
                s_ActiveButtons[layer] = this;
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (alwaysActive)
            {
                // Giữ visual Highlighted thay vì trả về Normal
                DoStateTransition(SelectionState.Highlighted, false);
                onDeselect?.Invoke();
                // KHÔNG gọi base để tránh reset visual
                return;
            }

            base.OnDeselect(eventData);
            onDeselect?.Invoke();
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            // Nếu navigation bị tắt → bỏ qua toàn bộ input xử lý
            if (!isNavigationEnabled) return;

            bool isFocused = EventSystem.current != null
                             && EventSystem.current.currentSelectedGameObject == gameObject;

            if (isFocused)
            {
                // Button này đang có real focus → xử lý navigation của chính nó
                HandleGamepadNavigation();
            }
            else if (alwaysActive)
            {
                // Chỉ button nào là "active" trong layer của nó mới được xử lý input
                if (s_ActiveButtons.TryGetValue(layer, out var active) && active == this)
                    HandleGlobalLayerInput();
            }
        }

        // ── Navigation khi đang có real focus ─────────────────────────────────

        private void HandleGamepadNavigation()
        {
            if (Time.unscaledTime < s_NextMoveTime[NavigationLayer.Content]) return;

            Vector2 inputDir = ReadInputDirection();

            if (Mathf.Abs(inputDir.x) > 0.5f) inputDir.x = Mathf.Sign(inputDir.x); else inputDir.x = 0;
            if (Mathf.Abs(inputDir.y) > 0.5f) inputDir.y = Mathf.Sign(inputDir.y); else inputDir.y = 0;

            if (inputDir == Vector2.zero) return;

            TISGamepadUIButton target = ResolveTarget(inputDir);
            if (target != null && target.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                s_NextMoveTime[NavigationLayer.Content] = Time.unscaledTime + holdDelay;
            }
        }

        // ── Global layer input: Tab / SubTab xử lý input khi không có real focus

        private void HandleGlobalLayerInput()
        {
            if (Time.unscaledTime < s_NextMoveTime[layer]) return;

            float axis = ReadLayerAxis();
            if (axis == 0f) return;

            TISGamepadUIButton target = null;
            if (axis > 0f) target = (navigationMode == NavMode.Vertical) ? navDown  : navRight;
            else           target = (navigationMode == NavMode.Vertical) ? navUp    : navLeft;

            if (target != null && target.gameObject.activeInHierarchy)
            {
                // Lấy button đang active trong layer này (có thể là this, hoặc button khác)
                s_ActiveButtons.TryGetValue(layer, out var current);

                // Deselect button cũ
                if (current != null && current != target)
                {
                    current.DoStateTransition(SelectionState.Normal, false);
                    current.onDeselect?.Invoke();
                }

                // Đăng ký button mới
                s_ActiveButtons[layer] = target;

                // Select button mới
                target.DoStateTransition(SelectionState.Highlighted, false);
                target.onSelect?.Invoke();

                // Notify UINavigationContext để redirect Content focus
                target.onBecameActiveInLayer?.Invoke(target);

                s_NextMoveTime[layer] = Time.unscaledTime + holdDelay;
            }
        }

        // ── Đọc input direction (dùng cho Content layer) ──────────────────────

        private Vector2 ReadInputDirection()
        {
            Vector2 inputDir = Vector2.zero;
            Gamepad gamepad  = Gamepad.current;

            if (gamepad != null)
            {
                if (inputSource == InputSource.Default)
                {
                    Vector2 dpad  = gamepad.dpad.ReadValue();
                    Vector2 stick = gamepad.leftStick.ReadValue();
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

            // Keyboard fallback
            if (inputDir == Vector2.zero && Keyboard.current != null)
            {
                if (inputSource == InputSource.Default)
                {
                    if (Keyboard.current.leftArrowKey.isPressed  || Keyboard.current.aKey.isPressed) inputDir.x = -1;
                    if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) inputDir.x =  1;
                    if (Keyboard.current.upArrowKey.isPressed    || Keyboard.current.wKey.isPressed) inputDir.y =  1;
                    if (Keyboard.current.downArrowKey.isPressed  || Keyboard.current.sKey.isPressed) inputDir.y = -1;
                }
            }

            return inputDir;
        }

        /// <summary>
        /// Đọc trục "tab" tương ứng với layer này — dùng cho global layer input.
        /// Trả về +1, -1 hoặc 0.
        /// </summary>
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

            // Keyboard fallback (Q/E cho Tab, Z/C cho SubTab — tuỳ chỉnh nếu cần)
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

        // ── Public API: đăng ký button như là "active" trong layer của nó ─────
        // Gọi từ UINavigationContext khi khởi tạo hoặc reset scene.

        /// <summary>
        /// Đăng ký button này là "active" trong layer — dùng khi khởi tạo scene.
        /// Tự động deselect button cũ và fire đủ onDeselect / onSelect.
        /// </summary>
        public void SetAsActiveInLayer()
        {
            // Deselect button cũ trong cùng layer (nếu có và khác this)
            if (s_ActiveButtons.TryGetValue(layer, out var prev) && prev != this && prev != null)
            {
                prev.DoStateTransition(SelectionState.Normal, false);
                prev.onDeselect?.Invoke();
            }

            s_ActiveButtons[layer] = this;
            DoStateTransition(SelectionState.Highlighted, false);
            onSelect?.Invoke();
        }

        /// <summary>
        /// Tắt navigation — button sẽ không nhận input hay di chuyển focus sang button khác.
        /// Gọi khi mở panel mới đè lên nhóm button này.
        /// </summary>
        public void DisableNavigation()
        {
            isNavigationEnabled = false;

            // Reset visual về Normal để tránh button "đứng im" nhưng vẫn sáng
            DoStateTransition(SelectionState.Normal, false);
        }

        /// <summary>
        /// Bật lại navigation — button có thể nhận input trở lại.
        /// Gọi khi panel mới đóng lại.
        /// </summary>
        public void EnableNavigation()
        {
            isNavigationEnabled = true;
        }

        public static TISGamepadUIButton GetActiveInLayer(NavigationLayer navLayer)
        {
            s_ActiveButtons.TryGetValue(navLayer, out var btn);
            return btn;
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