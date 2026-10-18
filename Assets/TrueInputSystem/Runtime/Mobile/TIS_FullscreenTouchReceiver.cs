// ============================================================
//  TrueInputSystem — TIS_FullscreenTouchReceiver.cs
//
//  Lớp nền toàn màn hình — BỘ PHÂN PHỐI NGÓN TAY (mỗi ngón một chủ).
//
//  · Nằm DƯỚI mọi control (sibling đầu tiên, canvas sortingOrder thấp):
//    EventSystem raycast giao ngón cho nút skill / joystick ở trên TRƯỚC.
//    Ngón chỉ rơi xuống đây khi chạm vào vùng trống.
//  · Lúc chạm xuống, chia theo vị trí:
//        trái  (≤ tỉ lệ joystick) → TISVirtualJoystick
//        phải                     → TISTouchCameraZone
//  · Sau đó mọi OnDrag / OnPointerUp của ngón đó đi về ĐÚNG chủ đã chọn,
//    dù ngón kéo sang nửa bên kia — không còn kiểm tra vùng mỗi frame.
//  · Không vẽ gì (cullTransparentMesh) → không tốn fill-rate GPU,
//    vẫn nhận raycast.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/Fullscreen Touch Receiver")]
    [RequireComponent(typeof(Image))]
    public class TISFullscreenTouchReceiver : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("References")]
        [SerializeField] private TISVirtualJoystick _joystick;
        [SerializeField] private TISTouchCameraZone _cameraZone;

        [Header("Debug")]
        [SerializeField] private bool _debugVisible = false;

        // ══════════════════════════════════════════════════════════════════════
        //  RUNTIME
        // ══════════════════════════════════════════════════════════════════════

        private enum Owner { Joystick, Camera }

        private Image _image;

        // pointerId → chủ được giao lúc chạm xuống
        private readonly Dictionary<int, Owner> _owners = new Dictionary<int, Owner>();

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

            // Alpha 0 → CanvasRenderer bỏ qua khi vẽ (không overdraw toàn màn hình),
            // raycast của Graphic vẫn hoạt động bình thường.
            _image.canvasRenderer.cullTransparentMesh = !_debugVisible;

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
            // Trả lại mọi ngón đang giữ
            foreach (var kv in _owners)
                Release(kv.Key, kv.Value);
            _owners.Clear();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  POINTER EVENTS
        // ══════════════════════════════════════════════════════════════════════

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Screen.width <= 0) return;
            if (_owners.ContainsKey(eventData.pointerId)) return;

            float normX = eventData.position.x / Screen.width;
            bool  joystickReady = _joystick != null && _joystick.isActiveAndEnabled && !_joystick.IsDragging;

            // Trái → joystick (nếu joystick đang rảnh). Joystick bận → ngón thứ 2 bên trái
            // cũng được giao cho camera thay vì bị bỏ phí.
            if (joystickReady && normX <= _joystick.ActivationZoneWidthRatio)
            {
                _joystick.BeginTouch(eventData.pointerId, eventData.position);
                if (_joystick.IsDragging)
                {
                    _owners[eventData.pointerId] = Owner.Joystick;
                    return;
                }
            }

            if (_cameraZone != null && _cameraZone.AddPointer(eventData))
                _owners[eventData.pointerId] = Owner.Camera;
        }

        /// <summary>Bỏ drag threshold → OnDrag chạy ngay từ pixel đầu tiên (joystick không bị "kẹt" ~10px).</summary>
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_owners.TryGetValue(eventData.pointerId, out var owner)) return;

            if (owner == Owner.Joystick)
                _joystick?.ProcessDragExternal(eventData.position);
            else
                _cameraZone?.MovePointer(eventData.pointerId, eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_owners.TryGetValue(eventData.pointerId, out var owner)) return;
            _owners.Remove(eventData.pointerId);
            Release(eventData.pointerId, owner);
        }

        private void Release(int pointerId, Owner owner)
        {
            if (owner == Owner.Joystick) _joystick?.ForceRelease(pointerId);
            else                         _cameraZone?.RemovePointer(pointerId);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC
        // ══════════════════════════════════════════════════════════════════════

        public void InjectJoystick(TISVirtualJoystick joystick)   => _joystick   = joystick;
        public void InjectCameraZone(TISTouchCameraZone zone)     => _cameraZone = zone;
    }
}
