#if UNITY_EDITOR
using System;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// TRỌNG TÀI DUY NHẤT cho input trong Scene View của bộ Blender Editor Tools.
    ///
    /// Trước đây mỗi hệ thống (pie menu, G/R/S session, gizmo precision, bone picking,
    /// orbit/pan/zoom) tự đoán trạng thái của các hệ thống khác qua vài cờ rời rạc
    /// (VD chỉ có BlenderControls.HasActiveSession), nên thứ tự "ai được xử lý event
    /// trước" không nhất quán -> đè lên nhau, kẹt GUIUtility.hotControl, mất chọn object,
    /// mất Undo... (xem lịch sử fix trong BlenderGizmoPrecisionTools.cs /
    /// SimpleBoneRendererEditor.cs).
    ///
    /// File này định nghĩa 1 thứ tự ưu tiên DUY NHẤT, cố định, từ cao xuống thấp:
    ///
    ///   1) PieMenu           – Create Object Pie Menu / View Axis Pie Menu đang mở
    ///   2) TransformSession  – đang trong phiên G/R/S (BlenderTransformSession)
    ///   3) GizmoDrag         – đang kéo gizmo Move/Rotate/Scale (BlenderGizmoPrecisionTools)
    ///   4) BonePicking       – click/hover chọn bone (SimpleBoneRendererEditor)
    ///   5) SceneNavigation   – orbit / pan / zoom bằng MMB (BlenderSceneViewNavigation)
    ///
    /// QUY TẮC DUY NHẤT: một hệ thống chỉ được bắt đầu xử lý một sự kiện nếu KHÔNG có hệ
    /// thống nào đứng ưu tiên CAO HƠN nó đang active. Mỗi file tự báo cáo trạng thái
    /// "đang active" của chính mình qua các delegate dưới đây (gán 1 lần trong static
    /// constructor của chính nó) — không ai được sửa hộ delegate của người khác.
    ///
    /// Cách dùng ở đầu mỗi handler:
    ///     if (!BlenderInputPriority.IsFreeFor(BlenderInputPriority.Owner.BonePicking)) return;
    /// </summary>
    public static class BlenderInputPriority
    {
        public enum Owner
        {
            PieMenu = 0,
            TransformSession = 1,
            GizmoDrag = 2,
            BonePicking = 3,
            SceneNavigation = 4,
        }

        // Mỗi hệ thống gán đúng 1 lần cho delegate tương ứng với chính nó, trong static
        // constructor của file đó. Mặc định luôn là "không active" để tránh NullReferenceException
        // nếu 1 file nào đó (do lỗi biên dịch/thứ tự load) chưa kịp gán.
        public static Func<bool> IsPieMenuOpen { get; set; } = () => false;
        public static Func<bool> IsTransformSessionActive { get; set; } = () => false;
        public static Func<bool> IsGizmoDragging { get; set; } = () => false;

        /// <summary>
        /// True nếu <paramref name="owner"/> được phép bắt đầu/tiếp tục xử lý sự kiện hiện tại,
        /// tức là không có hệ thống ưu tiên cao hơn nào đang active tại thời điểm gọi.
        /// </summary>
        public static bool IsFreeFor(Owner owner)
        {
            if (owner > Owner.PieMenu && IsPieMenuOpen()) return false;
            if (owner > Owner.TransformSession && IsTransformSessionActive()) return false;
            if (owner > Owner.GizmoDrag && IsGizmoDragging()) return false;
            return true;
        }
    }
}
#endif
