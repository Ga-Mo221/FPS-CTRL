using UnityEngine;

namespace TrueInputSystem
{
    public class TISInputDeadzone
    {
        /// <summary>
        /// Hàm áp dụng Deadzone cho Vector2 và chuẩn hóa lại tỉ lệ (Scaled Radial Deadzone)
        /// </summary>
        /// <param name="rawInput">Vector2 input thô từ thiết bị điều khiển</param>
        /// <returns>Vector2 đã được xử lý deadzone</returns>
        /// <param name="deadzoneRadius">Ngưỡng deadzone (từ 0 đến 1). Các input có độ dài nhỏ hơn ngưỡng này sẽ bị bỏ qua.</param>
        public Vector2 ApplyDeadzone(Vector2 rawInput, float deadzoneRadius)
        {
            // Lấy độ dài (độ mạnh) của vector input
            float magnitude = rawInput.magnitude;

            // Nếu độ dài nhỏ hơn ngưỡng deadzone, coi như không có input
            if (magnitude < deadzoneRadius)
            {
                return Vector2.zero;
            }

            // Nếu input đạt tối đa (ví dụ đẩy sát cạnh), giới hạn magnitude lại bằng 1
            if (magnitude > 1f)
            {
                rawInput.Normalize();
                magnitude = 1f;
            }

            // Công thức Scaled Radial: Nội suy mượt mà từ điểm deadzone đến 1
            // Giúp input không bị nhảy đột ngột từ 0 lên giá trị deadzone khi vừa vượt ngưỡng
            float scaledMagnitude = (magnitude - deadzoneRadius) / (1f - deadzoneRadius);

            // Trả về vector đã được tối ưu hướng và độ dài mới
            return rawInput.normalized * scaledMagnitude;
        }
    }
}