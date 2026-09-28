using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/Fullscreen Touch Receiver")]
    [RequireComponent(typeof(Image))]
    public class TISFullscreenTouchReceiver : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("References")]
        [SerializeField] private TISVirtualJoystick _joystick;

        [Header("Debug")]
        [SerializeField] private bool _debugVisible = false;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME
        // ══════════════════════════════════════════════════════════════════════

        private Image _image;

        // Pointer IDs đang được joystick xử lý
        private readonly HashSet<int> _joystickPointers = new HashSet<int>();

        // ══════════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.color = _debugVisible
                ? new Color(1f, 0.2f, 0.2f, 0.08f)
                : new Color(0f, 0f, 0f, 0f);
            _image.raycastTarget = true;

            var rect      = GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Phải là sibling đầu tiên để không chặn joystick / button ở trên
            transform.SetAsFirstSibling();
        }

        private void OnDisable()
        {
            _joystickPointers.Clear();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  POINTER EVENTS
        // ══════════════════════════════════════════════════════════════════════

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_joystick == null) return;

            // HIGH FIX H8: guard Screen.width == 0 (splash screen / Editor edge case).
            if (Screen.width <= 0) return;

            // Chỉ xử lý nửa trái màn hình
            float normX = eventData.position.x / Screen.width;
            if (normX > _joystick.ActivationZoneWidthRatio) return;

            if (!_joystick.IsDragging)
            {
                _joystick.BeginTouch(eventData.pointerId, eventData.position);
                _joystickPointers.Add(eventData.pointerId);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_joystickPointers.Contains(eventData.pointerId)) return;
            if (_joystick == null) return;

            // FIX v5: Gọi trực tiếp thay vì ExecuteEvents.Execute
            // ProcessDragExternal dùng canvas local space → không bị layout delay
            _joystick.ProcessDragExternal(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_joystickPointers.Contains(eventData.pointerId)) return;
            _joystickPointers.Remove(eventData.pointerId);

            // EventSystem gửi OnPointerUp về receiver (không về joystick)
            // → ForceRelease để joystick luôn reset đúng
            _joystick?.ForceRelease(eventData.pointerId);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC
        // ══════════════════════════════════════════════════════════════════════

        public void InjectJoystick(TISVirtualJoystick joystick)
        {
            _joystick = joystick;
        }
    }
}