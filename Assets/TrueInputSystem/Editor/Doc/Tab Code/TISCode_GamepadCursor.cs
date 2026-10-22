// ============================================================
//  TrueInputSystem — TISCode_GamepadCursor.cs
//  Code Sub-Tab Module: Gamepad Cursor
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_GamepadCursor : ICodeSubTabModule
    {
        public string Label => "Gamepad Cursor";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Gamepad Virtual Cursor",
                "TISGamepadVirtualCursor — chuột ảo điều khiển bằng left stick gamepad.");

            win.H2("Setup");
            win.Step(1, "Add Component", "Gắn TISGamepadVirtualCursor vào bất kỳ GameObject nào trong scene.");
            win.Step(2, "Assign Inspector", "Kéo RectTransform (UI Image con trỏ) → cursorTransform. Kéo Canvas chứa cursor → parentCanvas.");
            win.Step(3, "Tự đăng ký", "Component tự gọi bridge.RegisterGamepadCursor(this) trong Awake() — không cần làm gì thêm.");

            win.Sep();
            win.H2("Bật / Tắt Cursor");
            win.CodeBlock(
                "void OnOpenMenu()\n" +
                "{\n" +
                "    TISInputBridge.Instance.SetActiveGamepadCursor(true);\n" +
                "    // Cursor tự reset về center màn hình khi bật\n" +
                "}\n\n" +
                "void OnCloseMenu()\n" +
                "    => TISInputBridge.Instance.SetActiveGamepadCursor(false);");

            win.Sep();
            win.H2("Điều Kiện Hoạt Động");
            win.CodeBlock(
                "// 1. TISGamepadVirtualCursor component tồn tại trong scene\n" +
                "// 2. Gamepad.current != null\n" +
                "// 3. SetActiveGamepadCursor(true) đã được gọi\n\n" +
                "// Cursor dùng InputSystem.QueueDeltaStateEvent để inject chuột ảo\n" +
                "// → UI EventSystem nhận event click khi nhấn buttonSouth (A/Cross)\n" +
                "// → Tương thích với mọi Unity UI Button, Toggle, Slider");

            win.Sep();
            win.H2("Left Stick Dành Cho Cursor & Tự Thoát");
            win.Body("Khi cursor bật, left stick chỉ điều khiển cursor (button TIS không bị điều hướng, nút A không submit button đang chọn). Cursor tự tắt và focus lại button cũ khi:");
            win.CodeBlock(
                "// • Nhấn D-pad / LB / RB / LT / RT  (Exit On Dpad / Shoulders / Triggers)\n" +
                "// • Dùng chuột thật                  (Exit On Mouse — không focus lại button)\n\n" +
                "var cursor = FindAnyObjectByType<TISGamepadVirtualCursor>();\n" +
                "cursor.OnExitByNavigationInput += () => Debug.Log(\"Thoát cursor bằng D-pad / LB / RB\");\n" +
                "cursor.OnExitByMouse           += () => Debug.Log(\"Người chơi chuyển sang chuột\");\n\n" +
                "// Script khác kiểm tra cursor đang bật (left stick đang dành cho cursor):\n" +
                "if (TISGamepadVirtualCursor.IsCursorActive) return;");

            win.Sep();
            win.H2("Touchpad / Gyro Điều Khiển Cursor");
            win.Body("Profile › Motion Settings › Virtual Cursor: Touchpad Move (kéo như touchpad laptop), Touch Click (nhấn touchpad = click), Gyro Move (xoay tay cầm để trỏ).");

            win.Note("TISGamepadVirtualCursor tự tìm TISInputBridge.Instance trong Awake. Assign cursorTransform và parentCanvas trong Inspector là đủ.");
        }
    }
}
#endif
