using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Điều khiển một Image kiểu Filled (thanh máu/giáp, thanh điểm đội, thanh XP...).
    // Chỉ lo hiển thị: đưa vào giá trị 0..1 hoặc cặp current/max, nó cập nhật độ đầy của thanh.
    // Cách dùng: gán Image (Image Type = Filled) vào _fillImage, rồi gọi SetValue01() hoặc SetValue().
    public class UIFillBar : MonoBehaviour
    {
        // Image cần có Image Type = Filled. Để trống thì tự lấy Image trên cùng GameObject.
        [Tooltip("Image có Image Type = Filled. Để trống thì dùng Image trên cùng GameObject.")]
        [SerializeField] private Image _fillImage;

        [Range(0f, 1f)][SerializeField] private float _value = 1f;

        [Header("Tùy chọn: chạy mượt")]
        // Nếu bật, thanh sẽ trượt từ từ về giá trị mới thay vì nhảy ngay.
        [SerializeField] private bool _animate = false;
        [SerializeField] private float _lerpSpeed = 8f;

        private float _target = 1f; // giá trị đích khi đang trượt mượt

        private void Reset() => _fillImage = GetComponent<Image>();

        private void Awake()
        {
            if (_fillImage == null) _fillImage = GetComponent<Image>();
            _target = _value;
            Apply(_value);
        }

        private void Update()
        {
            if (!_animate || _fillImage == null) return;
            if (Mathf.Approximately(_fillImage.fillAmount, _target)) return;
            Apply(Mathf.MoveTowards(_fillImage.fillAmount, _target, _lerpSpeed * Time.deltaTime));
        }

        // Đặt độ đầy theo giá trị chuẩn hóa 0..1.
        public void SetValue01(float value01)
        {
            _target = Mathf.Clamp01(value01);
            if (_animate) return;
            Apply(_target);
        }

        // Đặt độ đầy theo cặp current/max (nếu max <= 0 thì thanh rỗng).
        public void SetValue(float current, float max)
            => SetValue01(max <= 0f ? 0f : current / max);

        private void Apply(float v)
        {
            _value = v;
            if (_fillImage != null) _fillImage.fillAmount = v;
        }
    }
}
