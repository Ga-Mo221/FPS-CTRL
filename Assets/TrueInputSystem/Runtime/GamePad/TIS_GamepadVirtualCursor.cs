using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace GaMo.TrueInputSystem
{
    [DefaultExecutionOrder(-80)]
    [AddComponentMenu("TIS/Demo/TIS Gamepad Virtual Cursor")]
    public class TISGamepadVirtualCursor : MonoBehaviour
    {
        [Header("Cấu hình UI")]
        [Tooltip("Kéo RectTransform của UI Image làm con trỏ chuột vào đây")]
        [SerializeField] private RectTransform cursorTransform;

        [Tooltip("Kéo Canvas chứa con trỏ chuột vào đây (Canvas nên để Render Mode là Screen Space - Overlay hoặc Camera)")]
        [SerializeField] private Canvas parentCanvas;

        [Header("Cấu hình di chuyển")]
        [SerializeField] private float cursorSpeed = 1000f;

        [Header("Thoát cursor → quay lại điều hướng button")]
        [Tooltip("Nhấn D-pad → tắt cursor, focus lại button như cũ.")]
        [SerializeField] private bool exitOnDpad = true;

        [Tooltip("Nhấn LB / RB (SubTab) → tắt cursor.")]
        [SerializeField] private bool exitOnShoulders = true;

        [Tooltip("Nhấn LT / RT (Tab) → tắt cursor.")]
        [SerializeField] private bool exitOnTriggers = true;

        [Tooltip("Người chơi dùng chuột thật (di chuyển / click / lăn) → tắt cursor ảo, trả UI cho chuột.")]
        [SerializeField] private bool exitOnMouse = true;

        [Tooltip("Chuột phải di chuyển quá số pixel này trong 1 frame mới tính là đang dùng chuột (lọc rung tay / bàn rung).")]
        [SerializeField] private float mouseExitThresholdPx = 2f;

        /// <summary>
        /// <c>true</c> khi có virtual cursor đang bật. Khi đó left stick dành riêng cho cursor:
        /// TISGamepadUIButton / TISUINavigationContext bỏ qua left stick.
        /// </summary>
        public static bool IsCursorActive { get; private set; }

        // Hỗ trợ Enter Play Mode khi tắt Domain Reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsCursorActive = false;

        /// <summary>Phát ra khi cursor tự tắt vì người chơi nhấn D-pad / LB / RB / LT / RT.</summary>
        public event System.Action OnExitByNavigationInput;

        /// <summary>Phát ra khi cursor tự tắt vì người chơi bắt đầu dùng chuột thật.</summary>
        public event System.Action OnExitByMouse;

        private Vector2 currentPosition;
        private Mouse virtualMouse;
        private InputSystemUIInputModule uiInputModule;

        private bool _enabled = false;
        private bool _isLeftButtonPressed = false;
        private bool _southHeld;
        private bool _touchpadHeld;
        private TISInputBridge _input;
        private InputActionAsset _originalActionsAsset;

        // Trạng thái điều hướng UI trước khi bật cursor — khôi phục khi tắt
        private GameObject _savedSelection;
        private bool       _savedSendNavigationEvents = true;
        private bool       _navigationLocked;

        // Thời gian (giây) chặn button di chuyển sau khi thoát cursor —
        // để lần nhấn D-pad / LB / RB dùng để thoát không làm button nhảy thêm 1 bước.
        private const float ExitConsumeTime = 0.3f;

        private void Awake()
        {
            _input = TISInputBridge.Instance;

            if (_input != null)
                _input.RegisterGamepadCursor(this);
            
            if (cursorTransform == null)
            {
                // Không có hình con trỏ: cursor vẫn hoạt động (điều khiển chuột ảo cho UI),
                // chỉ là không hiện hình. Trước đây dòng dưới ném NullReferenceException.
                Debug.LogWarning(
                    "[TISGamepadVirtualCursor] Chưa gán Cursor Transform — con trỏ sẽ không hiển thị.", this);
            }
            else if (!_enabled)
                cursorTransform.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (virtualMouse == null)
            {
                virtualMouse = (Mouse)InputSystem.AddDevice("Mouse", "VirtualGamepadMouse");
            }
            else if (!virtualMouse.added)
            {
                InputSystem.AddDevice(virtualMouse);
            }
            TISPhysicalMouse.RegisterVirtual(virtualMouse);

            // Chỉ tìm UI Input Module ở đây. Bộ action UI của project CHỈ bị thay bằng
            // DefaultInputActions trong lúc cursor đang bật (LockNavigation) — trước đây bị
            // thay suốt thời gian component bật, ghi đè cấu hình UI của cả project.
            uiInputModule = FindFirstObjectByType<InputSystemUIInputModule>();

            _isLeftButtonPressed = _southHeld = _touchpadHeld = false;
            currentPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
            UpdateCursorPosition(force: true);
        }

        private void OnDestroy()
        {
            // DefaultInputActions do cursor tạo — huỷ nếu không còn được module dùng
            if (_defaultUiActions != null
                && (uiInputModule == null || uiInputModule.actionsAsset != _defaultUiActions))
                Destroy(_defaultUiActions);
            _defaultUiActions = null;
        }

        void OnDisable()
        {
            // Component bị tắt khi cursor đang bật → trả lại điều hướng UI
            if (_enabled)
            {
                UnlockNavigation(restoreFocus: false);   // trả lại cả bộ action UI gốc
                IsCursorActive = false;
            }
            
            // Gỡ bỏ chuột ảo khi tắt script để tránh rác bộ nhớ
            if (virtualMouse != null && virtualMouse.added)
            {
                InputSystem.RemoveDevice(virtualMouse);
            }
            TISPhysicalMouse.UnregisterVirtual(virtualMouse);
        }

        void Update()
        {
            if (!_enabled) return;

            if (virtualMouse == null) return;

            // Chuột thật được dùng → tắt cursor ảo. Không restore focus button:
            // người dùng chuột không cần button sáng sẵn như khi dùng tay cầm.
            if (exitOnMouse && WasPhysicalMouseUsed())
            {
                SetActiveCursorInternal(false, restoreFocus: false);
                OnExitByMouse?.Invoke();
                return;
            }

            Gamepad gamepad = Gamepad.current;

            // D-pad / LB / RB / LT / RT → tắt cursor, quay lại điều hướng button như cũ.
            // Cursor chạy trước button (DefaultExecutionOrder -80 < -50) nên button
            // thấy cursor đã tắt ngay trong frame này; lần nhấn này bị "nuốt" (ExitConsumeTime).
            if (gamepad != null && WasNavigationExitPressed(gamepad))
            {
                SetActiveCursor(false);
                OnExitByNavigationInput?.Invoke();
                return;
            }

            var motion   = _input != null ? _input.Motion : null;
            var settings = _input != null ? _input.ActiveProfile?.motionSettings : null;
            if (gamepad == null && (motion == null || (!motion.HasTouchpad && !motion.HasGyro))) return;

            // Di chuyển con trỏ bằng cần gạt trái
            if (gamepad != null)
            {
                Vector2 stickInput = gamepad.leftStick.ReadValue();
                if (stickInput.magnitude > 0.1f)
                    currentPosition += stickInput * cursorSpeed * Time.unscaledDeltaTime;
            }

            // Touchpad: kéo như touchpad laptop (delta chuẩn hoá → pixel theo chiều ngang màn hình)
            if (motion != null && settings != null && settings.touchpadMovesCursor && motion.HasTouchpad)
                currentPosition += motion.TouchpadDelta * Screen.width * settings.touchpadCursorSensitivity;

            // Gyro: xoay tay cầm để trỏ (gyro aim đã tự tắt khi cursor bật)
            if (motion != null && settings != null && settings.gyroMovesCursor && motion.HasGyro)
                currentPosition += motion.GyroRate * settings.gyroCursorPixelsPerDegree * Time.unscaledDeltaTime;

            // Giới hạn trong phạm vi màn hình
            currentPosition.x = Mathf.Clamp(currentPosition.x, 0, Screen.width);
            currentPosition.y = Mathf.Clamp(currentPosition.y, 0, Screen.height);

            // Nút A (Xbox) / ✕ (PS) → Left Click. Chỉ tính cạnh nhấn để nút đang giữ sẵn
            // lúc bật cursor không thành click.
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                _southHeld = true;
            if (gamepad == null || gamepad.buttonSouth.wasReleasedThisFrame)
                _southHeld = false;

            // Nhấn touchpad → Left Click
            bool padClickEnabled = motion != null && settings != null && settings.touchpadClickIsLeftClick;
            if (padClickEnabled && motion.TouchpadPressedThisFrame)
                _touchpadHeld = true;
            if (!padClickEnabled || !motion.TouchpadPressed)
                _touchpadHeld = false;

            _isLeftButtonPressed = _southHeld || _touchpadHeld;

            // Chỉ gửi state khi vị trí / nút thay đổi — Input System giữ nguyên state cuối
            // (kể cả nút đang giữ) nên không cần queue event mỗi frame.
            UpdateCursorPosition();
        }

        private bool    _hasSentState;
        private Vector2 _lastSentPosition;
        private bool    _lastSentButton;

        private void UpdateCursorPosition(bool force = false)
        {
            if (!force && _hasSentState
                && _lastSentButton == _isLeftButtonPressed
                && (_lastSentPosition - currentPosition).sqrMagnitude < 0.01f)
                return;

            _hasSentState     = true;
            _lastSentPosition = currentPosition;
            _lastSentButton   = _isLeftButtonPressed;

            // Cập nhật vị trí cho UI Image
            if (cursorTransform != null && parentCanvas != null)
            {
                Vector2 localPos;
                // Chuyển đổi tọa độ màn hình (Screen Space) sang tọa độ của Canvas
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentCanvas.transform as RectTransform,
                    currentPosition,
                    parentCanvas.worldCamera,
                    out localPos
                );
                cursorTransform.localPosition = localPos;
            }

            // Gộp position + button state vào cùng 1 event
            // Nếu dùng QueueStateEvent riêng lẻ, MouseState mới sẽ reset button về false
            InputSystem.QueueStateEvent(virtualMouse, new MouseState
            {
                position = currentPosition,
                buttons = (ushort)(_isLeftButtonPressed ? 1 : 0) // bit 0 = left button
            });
        }

        public void SetActiveCursor(bool state) => SetActiveCursorInternal(state, restoreFocus: true);

        private void SetActiveCursorInternal(bool state, bool restoreFocus)
        {
            if (state != _enabled)
            {
                if (state) LockNavigation();
                else       UnlockNavigation(restoreFocus);
            }

            _enabled       = state;
            IsCursorActive = state;
            // Không có hình con trỏ vẫn phải reset vị trí / nút bên dưới
            if (cursorTransform != null)
                cursorTransform.gameObject.SetActive(state);

            // Reset vị trí và trạng thái mỗi khi bật cursor
            if (state)
            {
                _isLeftButtonPressed = _southHeld = _touchpadHeld = false;
                currentPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);

                // Đồng bộ ngay lập tức
                UpdateCursorPosition(force: true);

                // Force InputSystem update để UI nhận đúng vị trí mới
                InputSystem.Update();
            }
        }
        
        public bool GetEnabled()
        {
            return _enabled;
        }

        // ── Khoá / mở điều hướng UI khi bật / tắt cursor ─────────────────────

        /// <summary>
        /// Bật cursor: tắt navigation event của EventSystem (left stick / D-pad không đổi
        /// selection, nút A không "submit" button đang chọn) và nhớ button đang chọn.
        /// </summary>
        private void LockNavigation()
        {
            SwapToDefaultUiActions();

            var es = EventSystem.current;
            if (es == null || _navigationLocked) return;

            _savedSelection            = es.currentSelectedGameObject;
            _savedSendNavigationEvents = es.sendNavigationEvents;
            es.sendNavigationEvents    = false;
            _navigationLocked          = true;
        }

        /// <summary>
        /// Tắt cursor: trả lại navigation event, focus lại button.
        /// Ưu tiên: button vừa click bằng cursor → button đã chọn trước khi bật cursor
        /// → để TISUINavigationContext tự khôi phục (last / default content).
        /// </summary>
        private void UnlockNavigation(bool restoreFocus)
        {
            RestoreUiActions();

            if (!_navigationLocked) return;
            _navigationLocked = false;

            var es = EventSystem.current;
            if (es != null)
            {
                es.sendNavigationEvents = _savedSendNavigationEvents;

                if (restoreFocus && !IsUsableNavButton(es.currentSelectedGameObject))
                {
                    if (IsUsableNavButton(_savedSelection))
                        es.SetSelectedGameObject(_savedSelection);
                    else
                        foreach (var ctx in FindObjectsByType<TISUINavigationContext>(FindObjectsSortMode.None))
                            if (ctx.isActiveAndEnabled) ctx.RestoreFocusIfLost();
                }
            }
            _savedSelection = null;

            // Nuốt lần nhấn dùng để thoát: button chưa di chuyển ngay trong ExitConsumeTime
            float until = Time.unscaledTime + ExitConsumeTime;
            foreach (var layer in new[] { TISGamepadUIButton.NavigationLayer.Tab,
                                          TISGamepadUIButton.NavigationLayer.SubTab,
                                          TISGamepadUIButton.NavigationLayer.Content })
                if (TISGamepadUIButton.s_NextMoveTime[layer] < until)
                    TISGamepadUIButton.s_NextMoveTime[layer] = until;
        }

        // ── Bộ action UI khi cursor bật ──────────────────────

        private InputActionAsset _defaultUiActions;   // tạo 1 lần, dùng lại cho mọi lần bật
        private bool             _uiActionsSwapped;

        private void SwapToDefaultUiActions()
        {
            if (uiInputModule == null || _uiActionsSwapped) return;

            _originalActionsAsset = uiInputModule.actionsAsset;
            if (_defaultUiActions == null)
            {
                uiInputModule.actionsAsset = null;
                uiInputModule.AssignDefaultActions();
                _defaultUiActions = uiInputModule.actionsAsset;
            }
            else
            {
                uiInputModule.actionsAsset = _defaultUiActions;
            }
            _uiActionsSwapped = true;
        }

        private void RestoreUiActions()
        {
            if (!_uiActionsSwapped) return;
            _uiActionsSwapped = false;
            // Setter tự resolve lại point / leftClick / navigate... theo asset gốc
            if (uiInputModule != null && _originalActionsAsset != null)
                uiInputModule.actionsAsset = _originalActionsAsset;
        }

        private static bool IsUsableNavButton(GameObject go)
            => go != null && go.activeInHierarchy
               && go.TryGetComponent<TISGamepadUIButton>(out var btn) && btn.IsInteractable();

        /// <summary>
        /// Chuột THẬT có được dùng trong frame này không (di chuyển quá ngưỡng / click / lăn).
        /// Dùng TISPhysicalMouse → bỏ qua chuột ảo của chính cursor này.
        /// </summary>
        private bool WasPhysicalMouseUsed()
        {
            var m = TISPhysicalMouse.Current;
            if (m == null) return false;

            // Touchpad giả lập (giữ T + rê chuột) dùng chính chuột thật → không tính là "dùng chuột"
            if (_input != null && _input.Motion.IsTouching) return false;

            if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame
                || m.middleButton.wasPressedThisFrame)
                return true;
            if (Mathf.Abs(m.scroll.ReadValue().y) > 0.01f) return true;

            float t = mouseExitThresholdPx;
            return m.delta.ReadValue().sqrMagnitude > t * t;
        }

        private bool WasNavigationExitPressed(Gamepad gp)
        {
            if (exitOnDpad && (gp.dpad.up.wasPressedThisFrame   || gp.dpad.down.wasPressedThisFrame ||
                               gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame))
                return true;
            if (exitOnShoulders && (gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame))
                return true;
            if (exitOnTriggers && (gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame))
                return true;
            return false;
        }
    }
}
