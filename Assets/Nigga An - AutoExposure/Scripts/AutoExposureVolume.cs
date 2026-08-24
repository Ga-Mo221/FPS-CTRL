using System;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable once CheckNamespace
namespace Lepsima.Shaders.AutoExposure {

    [Serializable]
    [VolumeComponentMenu("Post-processing/AutoExposure")]
    public class AutoExposure : VolumeComponent, IPostProcessComponent {
    
        [Tooltip("Số frame cần để tính lại độ sáng trung bình của màn hình. Giá trị càng nhỏ, exposure cập nhật càng thường xuyên nhưng càng tốn hiệu năng (GPU phải chạy compute shader thường xuyên hơn).")]
        public ClampedIntParameter framesPerCompute = new(5, 1, 20);

        [Space]
        [Tooltip("Giới hạn độ sáng tối đa của một pixel trước khi đưa vào tính exposure, để các vật thể quá sáng (như đèn, mặt trời) không làm hỏng kết quả tính trung bình và không xung đột với Bloom.")]
        public FloatParameter brightnessLimit = new(15f);
    
        [Tooltip("Điểm trắng (white point) dùng cho thuật toán tone mapping Reinhard. Giá trị càng lớn, vùng sáng càng giữ được nhiều chi tiết hơn (ít bị cháy sáng/clip trắng).")]
        public FloatParameter whitePoint = new(3f);

        [Tooltip("Biên độ tối đa mà độ sáng của một pixel được phép thay đổi so với giá trị gốc (đơn vị EV/log). Giá trị X là giới hạn dưới (tối đi), giá trị Y là giới hạn trên (sáng lên). Dùng để tránh exposure thay đổi quá gắt.")]
        public FloatRangeParameter exposureRange = new(new Vector2(-2.5f, 0.6f), -6.0f, 6.0f);
    
        [Space]
        [Tooltip("Tốc độ tăng exposure khi màn hình chuyển từ sáng sang tối (mắt thích nghi với bóng tối). Giá trị càng lớn, exposure tăng càng nhanh.")]
        public FloatParameter increaseSpeed = new(5f);
    
        [Tooltip("Tốc độ giảm exposure khi màn hình chuyển từ tối sang sáng (mắt thích nghi với ánh sáng). Giá trị càng lớn, exposure giảm càng nhanh.")]
        public FloatParameter decreaseSpeed = new(5f);
    
        public bool IsActive() => framesPerCompute.overrideState;
    }
}