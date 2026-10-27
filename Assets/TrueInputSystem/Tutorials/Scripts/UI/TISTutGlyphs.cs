// ============================================================
//  TrueInputSystem Tutorials — TISTutGlyphs.cs
//  Tên / ký hiệu nút theo loại tay cầm đang cắm (Xbox · PlayStation · Switch).
// ============================================================

using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace GaMo.TrueInputSystem.Tutorials
{
    public static class TISTutGlyphs
    {
        public enum Family { Xbox, PlayStation, Switch }

        public static Family Current
        {
            get
            {
                var gp = Gamepad.current;
                if (gp == null) return Family.Xbox;
                if (gp is DualShockGamepad) return Family.PlayStation;
                // So tên layout thay vì kiểu Switch* (lớp đó chỉ biên dịch trên vài nền tảng)
                string layout = gp.layout ?? "";
                if (layout.IndexOf("Switch", System.StringComparison.OrdinalIgnoreCase) >= 0) return Family.Switch;
                return Family.Xbox;
            }
        }

        public static bool IsPlayStation => Current == Family.PlayStation;

        public static string South => Current switch { Family.PlayStation => "✕", Family.Switch => "B", _ => "A" };
        public static string East  => Current switch { Family.PlayStation => "○", Family.Switch => "A", _ => "B" };
        public static string West  => Current switch { Family.PlayStation => "□", Family.Switch => "Y", _ => "X" };
        public static string North => Current switch { Family.PlayStation => "△", Family.Switch => "X", _ => "Y" };
        public static string LB    => Current switch { Family.PlayStation => "L1", Family.Switch => "L",  _ => "LB" };
        public static string RB    => Current switch { Family.PlayStation => "R1", Family.Switch => "R",  _ => "RB" };
        public static string LT    => Current switch { Family.PlayStation => "L2", Family.Switch => "ZL", _ => "LT" };
        public static string RT    => Current switch { Family.PlayStation => "R2", Family.Switch => "ZR", _ => "RT" };
        public static string L3    => Current == Family.PlayStation ? "L3" : "LS";
        public static string View  => Current switch { Family.PlayStation => "SHARE", Family.Switch => "−", _ => "VIEW" };
        public static string Menu  => Current switch { Family.PlayStation => "OPTIONS", Family.Switch => "+", _ => "MENU" };

        /// <summary>Tên hiển thị của tay cầm đang dùng (hoặc gợi ý cắm tay cầm).</summary>
        public static string DeviceName => Gamepad.current != null ? Gamepad.current.displayName : "Chưa cắm tay cầm";
    }
}
