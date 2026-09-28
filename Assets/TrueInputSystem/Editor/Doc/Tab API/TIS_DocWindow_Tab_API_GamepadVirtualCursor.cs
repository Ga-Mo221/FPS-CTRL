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
            ApiRow("SetActiveCursor(bool)", "void", "Bật/tắt cursor. Khi bật: reset vị trí về center, sync vị trí với InputSystem. Dùng left stick gamepad để di chuyển, buttonSouth để click.", "red");

        }

    }  // partial class TISDocWindow
}
#endif
