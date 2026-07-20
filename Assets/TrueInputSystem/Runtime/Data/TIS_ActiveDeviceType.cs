// ============================================================
//  TrueInputSystem — TIS_ActiveDeviceType.cs
//  Enum xác định loại thiết bị input đang được sử dụng.
// ============================================================

namespace TrueInputSystem
{
    /// <summary>
    /// Loại thiết bị input đang được người chơi sử dụng tích cực.
    /// Đọc qua <see cref="ITISInputBridge.ActiveDevice"/>.
    /// </summary>
    public enum TISActiveDevice
    {
        /// <summary>
        /// Keyboard + Mouse (PC mặc định).
        /// Trả về khi không phải mobile và không có gamepad active.
        /// </summary>
        KeyboardMouse,

        /// <summary>
        /// Gamepad / Controller (Xbox, PlayStation, Switch Pro...).
        /// Trả về khi <c>Gamepad.current</c> nhận input trong frame này.
        /// </summary>
        Gamepad,

        /// <summary>
        /// Touchscreen (điện thoại / tablet).
        /// Trả về khi đang chạy trên mobile platform hoặc SimulateMobileInEditor = true.
        /// </summary>
        Mobile,
    }
}
