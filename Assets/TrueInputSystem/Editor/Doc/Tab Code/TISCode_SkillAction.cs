// ============================================================
//  TrueInputSystem — TISCode_SkillAction.cs
//  Code Sub-Tab Module: GetSkillAction
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_SkillAction : ICodeSubTabModule
    {
        public string Label => "GetSkillAction";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("GetSkillAction",
                "API tổng quát cho mọi button action — tìm xuyên suốt tất cả Action Map.");

            win.H2("Cú Pháp — 2 Overload");
            win.CodeBlock(
                "// Overload 1: dùng readMode được cấu hình trong profile\n" +
                "bool result = _input.GetSkillAction(\"actionName\");\n\n" +
                "// Overload 2: truyền mode tường minh — bỏ qua readMode trong profile\n" +
                "bool result = _input.GetSkillAction(\"actionName\", TISButtonReadMode.Hold);");

            win.Sep();
            win.H2("Ví Dụ Thực Tế — Nhiều Skill");
            win.CodeBlock(
                "void HandleSkills()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Attack\"))  DoAttack();\n" +
                "    if (_input.GetSkillAction(\"Skill_1\")) CastFireball();\n" +
                "    if (_input.GetSkillAction(\"Skill_2\")) Dash();\n" +
                "    if (_input.GetSkillAction(\"Skill_3\")) Shield();\n" +
                "    if (_input.GetSkillAction(\"Brake\"))   ApplyBrake();\n" +
                "    if (_input.GetSkillAction(\"Horn\"))    PlayHorn();\n" +
                "}");

            win.Sep();
            win.H2("Charge Mechanic — Overload 2 Với 3 Mode");
            win.Body("Dùng overload 2 khi cần đọc cả 3 event (nhấn, giữ, nhả) trên cùng 1 action:");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.PressDown))\n" +
                "        BeginCharge();\n\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.Hold))\n" +
                "        ContinueCharge();\n\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.PressUp))\n" +
                "        ReleaseCharge();\n" +
                "}");

            win.Sep();
            win.H2("Một Action — Nhiều Platform Binding");
            win.Body("GetSkillAction trả về true nếu <b>bất kỳ</b> binding nào active (PC, Gamepad, hoặc Mobile):");
            win.CodeBlock(
                "// Profile: Action \"Jump\"\n" +
                "//   bindingPC     = <Keyboard>/space\n" +
                "//   bindingGP     = <Gamepad>/buttonSouth\n" +
                "//   bindingMobile = Button_0\n\n" +
                "// Code — không đổi dù platform nào:\n" +
                "if (_input.GetSkillAction(\"Jump\")) DoJump();");

            win.Sep();
            win.H2("2 Phím Cho Cùng 1 Action");
            win.Body("Thêm action phụ với binding thứ hai, rồi OR cả hai trong code:");
            win.CodeBlock(
                "// Profile: thêm action \"Jump2\" với bindingPC = <Keyboard>/numpad0\n" +
                "if (_input.GetSkillAction(\"Jump\") || _input.GetSkillAction(\"Jump2\"))\n" +
                "    DoJump();");

            win.Note("Tên trong GetSkillAction(\"tên\") phải khớp actionName trong profile. Case-insensitive nhưng nên viết nhất quán.");
        }
    }
}
#endif
