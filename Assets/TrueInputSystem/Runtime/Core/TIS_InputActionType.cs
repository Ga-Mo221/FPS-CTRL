// ============================================================
//  TrueInputSystem — TISInputActionType.cs
//  Định nghĩa loại dữ liệu mà một InputAction trả về.
// ============================================================

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Loại giá trị mà một action trả về khi được đọc.
    /// </summary>
    public enum TISInputActionType
    {
        /// <summary>
        /// Trả về bool — true khi phím/nút được nhấn.
        /// Dùng cho: Jump, Attack, Skill...
        /// </summary>
        Button,

        /// <summary>
        /// Trả về Vector2 — giá trị 2 chiều liên tục.
        /// Dùng cho: MoveDirection, LookDelta...
        /// </summary>
        Vector2,

        /// <summary>
        /// Trả về float — giá trị 1 chiều liên tục.
        /// Dùng cho: Throttle, Brake trong racing game...
        /// </summary>
        Axis
    }
}
