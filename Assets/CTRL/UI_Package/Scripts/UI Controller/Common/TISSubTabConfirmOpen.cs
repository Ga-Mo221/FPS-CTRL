using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-05] Cho category Cài đặt (SubTab) kiểu "lướt L1/R1 để chọn,
    /// bấm nút xác nhận (A / ✕ / Enter) mới MỞ" — thay cho hành vi mặc định
    /// "đáp vào là mở luôn" của <see cref="TISLayerActivateClick"/>.
    ///
    /// Cơ chế:
    /// • Khi nút trở thành active trong layer (lướt L1/R1 tới) → KHÔNG gọi onClick,
    ///   mà XOÁ focus content (EventSystem = null) → trạng thái "chờ" (pending).
    /// • Trong lúc chờ (focus content == null) + nút này đang active-in-layer,
    ///   nếu bấm nút xác nhận (South / Enter / Space) → gọi onClick (mở section).
    /// Nhờ focus = null lúc chờ nên nút xác nhận không đụng submit của content.
    /// </summary>
    [RequireComponent(typeof(TISGamepadUIButton))]
    [DisallowMultipleComponent]
    public class TISSubTabConfirmOpen : MonoBehaviour
    {
        private TISGamepadUIButton _btn;
        private static float _nextTime;

        private void Awake() { _btn = GetComponent<TISGamepadUIButton>(); }

        private void OnEnable()
        {
            if (_btn == null) _btn = GetComponent<TISGamepadUIButton>();
            if (_btn != null) _btn.onBecameActiveInLayer.AddListener(OnActivated);
        }

        private void OnDisable()
        {
            if (_btn != null) _btn.onBecameActiveInLayer.RemoveListener(OnActivated);
        }

        private void OnActivated(TISGamepadUIButton which)
        {
            if (which != _btn) return;
            // Lướt tới = chỉ highlight; xoá focus content => "chờ xác nhận", chưa mở.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void Update()
        {
            if (_btn == null || _btn.context == null) return;
            if (TISGamepadUIButton.ExternalSuppress > 0) return;
            var br = TISInputBridge.Instance;
            if (br != null && !br.IsGamepad) return;
            if (!_btn.context.IsActiveInLayer(_btn)) return;
            if (EventSystem.current == null) return;
            if (EventSystem.current.currentSelectedGameObject != null) return; // chỉ khi đang "chờ"
            if (Time.unscaledTime < _nextTime) return;

            bool submit = false;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) submit = true;
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) submit = true;

            if (submit)
            {
                _nextTime = Time.unscaledTime + 0.25f;
                var b = _btn as Button;
                if (b != null && b.interactable) b.onClick.Invoke();
            }
        }
    }
}
