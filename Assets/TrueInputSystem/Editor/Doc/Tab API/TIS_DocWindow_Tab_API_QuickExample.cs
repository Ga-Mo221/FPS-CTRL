// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.QuickExample.cs
//  Tab API Reference — module: Code ví dụ nhanh
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_QuickExample()
        {
            GUILayout.Space(8);
            H2("Code Ví Dụ Nhanh — Đọc Input Cơ Bản");
            CodeBlock(
                "var bridge = TISInputBridge.Instance;\n\n" +
                "// Movement\n" +
                "Vector2 move = bridge.MoveDirection;\n" +
                "Vector2 look = bridge.LookDelta;\n" +
                "float   zoom = bridge.ZoomDelta;\n\n" +
                "// Shortcuts\n" +
                "if (bridge.IsJumping)   DoJump();\n" +
                "if (bridge.IsRunning)   SetSpeed(runSpeed);\n" +
                "if (bridge.IsAttacking) DoAttack();\n\n" +
                "// Custom actions\n" +
                "if (bridge.GetSkillAction(\"Skill1\"))          CastSkill1();\n" +
                "if (bridge.IsSkillDown(\"Dodge\"))              DodgeRoll();\n" +
                "if (bridge.IsSkillHeld(\"Charge\"))             ContinueCharge();\n" +
                "if (bridge.IsSkillReleased(\"Charge\"))         ReleaseCharge();\n\n" +
                "// Rumble\n" +
                "bridge.TryRumble(0.6f, 0.3f, 0.2f); // duration\n" +
                "bridge.StopRumble();\n\n" +
                "// Rebinding\n" +
                "TISRebindingSystem.LoadAndApply(bridge);         // Awake()\n" +
                "TISRebindingSystem.StartRebinding(bridge, \"Jump\", RebindTarget.PC, result => {});\n" +
                "string key = TISRebindingSystem.GetDisplayName(TISRebindingSystem.GetActivePath(bridge, \"Jump\", RebindTarget.PC));");

        }

    }  // partial class TISDocWindow
}
#endif
