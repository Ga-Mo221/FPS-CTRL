// ============================================================
//  TrueInputSystem — TISFav_GetSkillAction.cs
//  Favorites Sub-Tab Module: GetSkillAction
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_GetSkillAction : IFavSubTabModule
    {
        public string Label => "GetSkillAction";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "GetSkillAction(name) / IsSkillDown / IsSkillHeld / IsSkillReleased",
                "bool",
                "Đọc button theo tên action — theo profile hoặc mode tường minh.",
                win.C_AccentGreen);

            win.H2("Cú Pháp — 5 Overload");
            win.CodeBlock(
                "// Đọc theo readMode đã config trong Profile:\n" +
                "bool result = _input.GetSkillAction(\"ActionName\");\n\n" +
                "// Đọc với mode tường minh (bỏ qua profile readMode):\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.PressDown);\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.Hold);\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.PressUp);\n\n" +
                "// Shortcut tường minh:\n" +
                "bool down     = _input.IsSkillDown(\"ActionName\");     // PressDown — 1 frame nhấn\n" +
                "bool held      = _input.IsSkillHeld(\"ActionName\");     // Hold      — suốt khi giữ\n" +
                "bool released  = _input.IsSkillReleased(\"ActionName\"); // PressUp   — 1 frame nhả");

            win.Sep();
            win.H2("Ví Dụ — GetSkillAction Cơ Bản");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Attack\"))  DoAttack();\n" +
                "    if (_input.GetSkillAction(\"Skill1\"))  UseDash();\n" +
                "    if (_input.GetSkillAction(\"Skill2\"))  UseShield();\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Charge Mechanic (3 Mode Cùng 1 Action)");
            win.CodeBlock(
                "private float _chargeTime;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillDown(\"Fire\"))     BeginCharge();\n" +
                "    if (_input.IsSkillHeld(\"Fire\"))     ContinueCharge(ref _chargeTime);\n" +
                "    if (_input.IsSkillReleased(\"Fire\")) ReleaseCharge(_chargeTime);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — ADS / Aim Down Sight");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    bool aiming = _input.IsSkillHeld(\"Aim\");\n" +
                "    _cam?.SetZoomEnabled(!aiming);        // tắt scroll zoom khi ADS\n" +
                "    float fov = aiming ? adsFOV : normalFOV;\n" +
                "    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, adsLerpSpeed * Time.deltaTime);\n" +
                "}");

            win.Tip("readMode trong Profile: PressDown = 1 frame khi nhấn (default), Hold = true suốt khi giữ, PressUp = 1 frame khi nhả.");
        }
    }
}
#endif
