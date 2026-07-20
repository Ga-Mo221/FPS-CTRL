namespace TrueInputSystem
{
    /// <summary>
    /// Tập hợp hằng số string dùng làm ID cho mobile binding.
    /// <para>
    /// Thay vì hardcode string, hãy dùng các hằng số này
    /// để tránh lỗi typo và dễ refactor.
    /// </para>
    /// </summary>
    public static class TISVirtualControlID
    {
        // ── Movement ──────────────────────────────────────────
        /// <summary>Virtual joystick bên trái màn hình.</summary>
        public const string TISVirtualJoystick = "TISVirtualJoystick";

        // ── Camera ────────────────────────────────────────────
        /// <summary>Vùng kéo ngón tay thứ 2 để pan camera.</summary>
        public const string CameraZone = "CameraZone";

        // ── Skill / Action Buttons ─────────────────────────────
        // Các const Button_0..Button_7 đã được thay bằng ButtonByIndex()
        // để không giới hạn số lượng button.
        // Dùng ButtonByIndex(n) thay cho Button_n ở mọi nơi.
        //
        // Ví dụ tương đương:
        //   Button_0  →  ButtonByIndex(0)   →  "Button_0"
        //   Button_7  →  ButtonByIndex(7)   →  "Button_7"
        //   Button_15 →  ButtonByIndex(15)  →  "Button_15"  ← mới, không giới hạn

        /// <summary>
        /// Trả về Button ID theo index (không giới hạn số lượng).
        /// Dùng ở mọi nơi thay cho các const Button_0..Button_N.
        /// <para>Ví dụ: ButtonByIndex(0) → "Button_0"</para>
        /// </summary>
        public static string ButtonByIndex(int index) => $"Button_{index}";

        /// <summary>
        /// Trả về true nếu ID là một button (Button_0 .. Button_7).
        /// </summary>
        public static bool IsButton(string id)
        {
            return id != null && id.StartsWith("Button_");
        }

        /// <summary>
        /// Lấy index từ Button ID ("Button_3" → 3).
        /// Trả về -1 nếu không phải button ID.
        /// </summary>
        public static int GetButtonIndex(string id)
        {
            if (!IsButton(id)) return -1;
            var parts = id.Split('_');
            return parts.Length == 2 && int.TryParse(parts[1], out int idx) ? idx : -1;
        }
    }
}