// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.GamepadVirtualCursor.cs
//  Tab API Reference — module: TISGamepadVirtualCursor
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_GamepadVirtualCursor()
        {
            GUILayout.Space(8);
            // ── TISGamepadVirtualCursor ────────────────────────────────────
            H2("TISGamepadVirtualCursor — Methods");
            Note("Gắn lên bất kỳ GameObject nào. Assign cursorTransform (RectTransform UI cursor) và parentCanvas trong Inspector. Tự đăng ký với bridge qua RegisterGamepadCursor(this) trong Awake().");
            ApiHeader();
            ApiRow("SetActiveCursor(bool)", "void", "Bật/tắt cursor. Khi bật: reset vị trí về center, khoá điều hướng UI (left stick chỉ điều khiển cursor). Khi tắt: trả điều hướng, focus lại button cũ.", "red");
            ApiRow("GetEnabled()",          "bool", "Cursor có đang bật không.", "green");

            GUILayout.Space(8);
            H2("TISGamepadVirtualCursor — Properties & Events");
            ApiHeader();
            ApiRow("IsCursorActive",          "static bool", "true khi có virtual cursor đang bật — left stick dành cho cursor (TISGamepadUIButton / TISUINavigationContext bỏ qua left stick).", "green");
            ApiRow("OnExitByNavigationInput", "event Action", "Cursor tự tắt vì người chơi nhấn D-pad / LB / RB / LT / RT.", "purple");
            ApiRow("OnExitByMouse",           "event Action", "Cursor tự tắt vì người chơi bắt đầu dùng chuột thật.", "purple");

            GUILayout.Space(8);
            H2("TISGamepadVirtualCursor — Inspector");
            ApiHeader("Field", "Mặc định", "Mô tả");
            ApiRow("Exit On Dpad",            "true",  "Nhấn D-pad → tắt cursor, quay lại điều hướng button.", "amber");
            ApiRow("Exit On Shoulders",       "true",  "Nhấn LB / RB (SubTab) → tắt cursor.", "amber");
            ApiRow("Exit On Triggers",        "true",  "Nhấn LT / RT (Tab) → tắt cursor.", "amber");
            ApiRow("Exit On Mouse",           "true",  "Dùng chuột thật (di / click / lăn) → tắt cursor.", "amber");
            ApiRow("Mouse Exit Threshold Px", "2",     "Số pixel chuột phải di trong 1 frame mới tính là đang dùng chuột.", "amber");
            Note("Touchpad / gyro điều khiển cursor: Profile › Motion Settings › Virtual Cursor (Touchpad Move, Touch Click, Gyro Move).");

        }

    }  // partial class TISDocWindow
}
#endif
