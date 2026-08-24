using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    // Controller cho màn hình chết / killcam.
    // Hiện ai đã hạ gục mình và đếm ngược thời gian hồi sinh; nút hồi sinh
    // chỉ bấm được khi đếm ngược về 0.
    // Cách dùng: gọi SetKiller(tên) rồi StartRespawnCountdown(giây);
    // đăng ký sự kiện RespawnClicked để xử lý khi người chơi bấm hồi sinh.
    public class KillcamController : MonoBehaviour
    {
        [Header("Thông tin")]
        [SerializeField] private TMP_Text _killerLabel;            // tên người hạ gục mình
        [SerializeField] private TMP_Text _respawnCountdownLabel;  // số giây đếm ngược

        [Header("Nút hồi sinh")]
        [SerializeField] private Button _respawnButton;
        [SerializeField] private TMP_Text _respawnButtonLabel;
        [Tooltip("Chữ trên nút khi đang đếm ngược (chưa cho hồi sinh).")]
        [SerializeField] private string _waitingText = "ĐANG CHỜ...";
        [Tooltip("Chữ trên nút khi đã cho phép hồi sinh.")]
        [SerializeField] private string _readyText = "HỒI SINH";

        // Phát khi người chơi bấm nút hồi sinh (lúc đã cho phép).
        public event Action RespawnClicked;
        // Phát một lần khi đếm ngược về 0 (hồi sinh sẵn sàng).
        public event Action RespawnAvailable;

        private float _remaining;   // số giây còn lại
        private bool _counting;     // đang đếm ngược hay không
        private bool _available;    // đã cho phép hồi sinh chưa

        private void Awake()
        {
            if (_respawnButton != null) _respawnButton.onClick.AddListener(OnRespawn);
        }

        private void OnDestroy()
        {
            if (_respawnButton != null) _respawnButton.onClick.RemoveListener(OnRespawn);
        }

        private void Update()
        {
            if (!_counting) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _counting = false;
                SetAvailable(true);
            }
            UpdateCountdownLabel();
        }

        // Đặt tên người đã hạ gục mình.
        public void SetKiller(string killerName)
        {
            if (_killerLabel != null) _killerLabel.text = killerName;
        }

        // Bắt đầu đếm ngược hồi sinh với độ dài cho trước (giây).
        public void StartRespawnCountdown(float seconds)
        {
            _remaining = Mathf.Max(0f, seconds);
            _counting = _remaining > 0f;
            SetAvailable(!_counting);
            UpdateCountdownLabel();
        }

        // Đặt trực tiếp số giây còn lại (nếu được điều khiển từ bên ngoài).
        public void SetRemaining(float seconds)
        {
            _remaining = Mathf.Max(0f, seconds);
            if (_remaining <= 0f) SetAvailable(true);
            UpdateCountdownLabel();
        }

        private void OnRespawn()
        {
            if (!_available) return;
            RespawnClicked?.Invoke();
        }

        // Bật/tắt trạng thái cho phép hồi sinh (đổi màu nút + chữ + phát sự kiện).
        private void SetAvailable(bool available)
        {
            bool wasAvailable = _available;
            _available = available;
            if (_respawnButton != null) _respawnButton.interactable = available;
            if (_respawnButtonLabel != null) _respawnButtonLabel.text = available ? _readyText : _waitingText;
            if (available && !wasAvailable) RespawnAvailable?.Invoke();
        }

        private void UpdateCountdownLabel()
        {
            if (_respawnCountdownLabel != null)
                _respawnCountdownLabel.text = Mathf.CeilToInt(_remaining).ToString();
        }
    }
}
