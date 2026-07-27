using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace TrueInputSystem
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

        private Vector2 currentPosition;
        private Mouse virtualMouse;
        private InputSystemUIInputModule uiInputModule;

        private bool _enabled = false;
        private bool _isLeftButtonPressed = false;
        private TISInputBridge _input;

        private void Awake()
        {
            _input = TISInputBridge.Instance;

            if (_input != null)
                _input.RegisterGamepadCursor(this);
            
            if (!_enabled)
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

            // Tìm và cấu hình UI Input Module
            uiInputModule = FindFirstObjectByType<InputSystemUIInputModule>();
            if (uiInputModule != null)
            {
                uiInputModule.actionsAsset = null;
            }

            _isLeftButtonPressed = false;
            currentPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
            UpdateCursorPosition();
        }

        void OnDisable()
        {
            // Gỡ bỏ chuột ảo khi tắt script để tránh rác bộ nhớ
            if (virtualMouse != null && virtualMouse.added)
            {
                InputSystem.RemoveDevice(virtualMouse);
            }
        }

        void Update()
        {
            if (!_enabled) return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null || virtualMouse == null) return;

            // Di chuyển con trỏ bằng cần gạt trái
            Vector2 stickInput = gamepad.leftStick.ReadValue();

            if (stickInput.magnitude > 0.1f)
            {
                currentPosition += stickInput * cursorSpeed * Time.deltaTime;

                // Giới hạn trong phạm vi màn hình
                currentPosition.x = Mathf.Clamp(currentPosition.x, 0, Screen.width);
                currentPosition.y = Mathf.Clamp(currentPosition.y, 0, Screen.height);
            }

            // Cập nhật trạng thái nút A (Xbox) / X (PS) → Left Click
            if (gamepad.buttonSouth.wasPressedThisFrame)
                _isLeftButtonPressed = true;

            if (gamepad.buttonSouth.wasReleasedThisFrame)
                _isLeftButtonPressed = false;

            // Luôn đồng bộ position + button state mỗi frame
            // để giữ nguyên trạng thái hold khi di chuyển cursor
            UpdateCursorPosition();
        }

        private void UpdateCursorPosition()
        {
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

        public void SetActiveCursor(bool state)
        {
            _enabled = state;
            if (cursorTransform == null) return;
            cursorTransform.gameObject.SetActive(state);

            // Reset vị trí và trạng thái mỗi khi bật cursor
            if (state)
            {
                _isLeftButtonPressed = false;
                currentPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);

                // Đồng bộ ngay lập tức
                UpdateCursorPosition();

                // Force InputSystem update để UI nhận đúng vị trí mới
                InputSystem.Update();
            }
        }
        
        public bool GetEnabled()
        {
            return _enabled;
        }
    }
}
