namespace TrueInputSystem
{
    /// <summary>
    /// Chế độ đọc trạng thái của một Button action.
    /// </summary>
    public enum TISButtonReadMode
    {
        /// <summary>
        /// true đúng 1 frame khi nhấn xuống (GetKeyDown).
        /// Dùng cho: nhảy, tấn công, tương tác, kỹ năng instant.
        /// </summary>
        PressDown,

        /// <summary>
        /// true suốt thời gian đang giữ phím (GetKey).
        /// Dùng cho: chạy, ngồi, ngắm bắn, charge liên tục.
        /// </summary>
        Hold,

        /// <summary>
        /// true đúng 1 frame khi nhả ra (GetKeyUp).
        /// Dùng cho: release charge, kết thúc aim, confirm hold-to-cast.
        /// </summary>
        PressUp,
    }
}