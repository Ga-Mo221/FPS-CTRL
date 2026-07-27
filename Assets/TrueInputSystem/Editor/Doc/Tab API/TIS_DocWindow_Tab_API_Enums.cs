// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.Enums.cs
//  Tab API Reference — module: Enums tổng hợp
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_Enums()
        {
            GUILayout.Space(8);
            // ── Enums ──────────────────────────────────────────────────────
            H2("Enums");
            CodeBlock(
                "// TISButtonReadMode:\n" +
                "PressDown   // true đúng 1 frame khi nhấn (GetKeyDown) — mặc định cho button\n" +
                "Hold        // true suốt khi đang giữ (GetKey)\n" +
                "PressUp     // true đúng 1 frame khi nhả (GetKeyUp)\n\n" +
                "// RebindTarget:\n" +
                "PC          // Keyboard + Mouse\n" +
                "Gamepad     // Controller\n\n" +
                "// AimAssistPlatform:\n" +
                "None        // Tắt hoàn toàn\n" +
                "PC          // Keyboard + Mouse\n" +
                "Gamepad     // Controller\n" +
                "Mobile      // Touchscreen\n" +
                "All         // PC | Gamepad | Mobile\n\n" +
                "// TISTargetScanner.AimPointMode:\n" +
                "Center          // Gốc Transform của target\n" +
                "ColliderCenter  // Tâm Bounds của Collider (default)\n" +
                "Custom          // Offset tùy chỉnh (_aimPointOffset)\n\n" +
                "// TISMobileSkillButton.VisualMode:\n" +
                "ColorTint   // Tint màu Image theo trạng thái\n" +
                "SpriteSwap  // Đổi Sprite theo trạng thái");

        }

    }  // partial class TISDocWindow
}
#endif
