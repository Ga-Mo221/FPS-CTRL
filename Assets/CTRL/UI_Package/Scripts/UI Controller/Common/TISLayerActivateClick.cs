using UnityEngine;
using UnityEngine.UI;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-05] Cầu nối giữa TIS layer-navigation và onClick.
    ///
    /// Khi một nút Tab (L2/R2) hoặc SubTab (L1/R1) trở thành "active" trong layer
    /// của nó, TIS chỉ đổi visual + fire <see cref="TISGamepadUIButton.onBecameActiveInLayer"/>
    /// — nó KHÔNG gọi onClick, nên panel phía dưới (ShellController) không được đổi và
    /// chế độ (Team DM / FFA) không được chọn.
    ///
    /// Component này lắng nghe sự kiện đó và gọi lại Button.onClick, để "đáp" vào một
    /// tab = hiện panel tương ứng (giống menu console/LMHT). Đặt chung nút với
    /// <see cref="TISGamepadUIButton"/>.
    /// </summary>
    [RequireComponent(typeof(TISGamepadUIButton))]
    [DisallowMultipleComponent]
    public class TISLayerActivateClick : MonoBehaviour
    {
        private TISGamepadUIButton _btn;

        private void Awake()
        {
            _btn = GetComponent<TISGamepadUIButton>();
        }

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
            // Chỉ phản hồi khi chính nút này được active (phòng trường hợp event dùng chung).
            if (which != _btn) return;
            var b = _btn as Button;
            if (b != null && b.interactable)
                b.onClick.Invoke();
        }
    }
}
