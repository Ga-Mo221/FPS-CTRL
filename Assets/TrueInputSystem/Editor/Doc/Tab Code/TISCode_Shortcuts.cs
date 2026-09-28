// ============================================================
//  TrueInputSystem — TISCode_Shortcuts.cs
//  Code Sub-Tab Module: Shortcuts
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Shortcuts : ICodeSubTabModule
    {
        public string Label => "Shortcuts";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Button Shortcut",
                "Properties tiện lợi cho Jump, Run, Attack và IsSkillDown/Held/Released.");

            win.H2("3 Shortcut Properties Thường Dùng");
            win.Body("Đọc trực tiếp, không cần biết tên action trong profile:");
            win.CodeBlock(
                "bool jump   = _input.IsJumping;    // Space / buttonSouth / on-screen Jump\n" +
                "bool run    = _input.IsRunning;     // LeftShift / leftStickPress\n" +
                "bool attack = _input.IsAttacking;   // Mouse0 / buttonSouth / Attack btn");

            win.Sep();
            win.H2("Dùng Trong Update — IsJumping / IsRunning / IsAttacking");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsJumping)   DoJump();\n" +
                "    if (_input.IsRunning)   SetSprint(true);\n" +
                "    if (_input.IsAttacking) Attack();\n" +
                "}");

            win.Sep();
            win.H2("ReadMode — Khi Nào Trigger?");
            win.Body("Mỗi action có thể config <b>readMode</b> trong TISInputActionDefinition:");
            win.CodeBlock(
                "PressDown  // true đúng 1 frame khi nhấn — mặc định cho button\n" +
                "Hold       // true suốt khi đang giữ phím\n" +
                "PressUp    // true đúng 1 frame khi nhả phím");

            win.Sep();
            win.H2("IsSkillDown / IsSkillHeld / IsSkillReleased");
            win.Body("Shortcut cho GetSkillAction với mode tường minh — bất kể readMode trong profile:");
            win.CodeBlock(
                "// PressDown — true đúng 1 frame khi nhấn\n" +
                "if (_input.IsSkillDown(\"Dodge\"))     DoDodge();\n\n" +
                "// Hold — true suốt khi đang giữ\n" +
                "if (_input.IsSkillHeld(\"Shield\"))    BlockDamage();\n\n" +
                "// PressUp — true đúng 1 frame khi nhả\n" +
                "if (_input.IsSkillReleased(\"Aim\"))   ExitAimMode();");

            win.Sep();
            win.H2("Charge Mechanic — Dùng Cả 3 Mode Trên 1 Action");
            win.Body("Khi cần đọc nhấn + giữ + nhả trên cùng 1 action, dùng IsSkillDown/Held/Released:");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillDown(\"Charge\"))     BeginCharge();\n" +
                "    if (_input.IsSkillHeld(\"Charge\"))     ContinueCharge();\n" +
                "    if (_input.IsSkillReleased(\"Charge\")) ReleaseCharge();\n" +
                "}");

            win.Tip("IsJumping và IsRunning không check TISMobileButtonRegistry mặc định. Xem tab Nâng Cao để patch thêm mobile support.");
        }
    }
}
#endif