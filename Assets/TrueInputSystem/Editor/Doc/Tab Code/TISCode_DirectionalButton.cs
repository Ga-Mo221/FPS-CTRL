// ============================================================
//  TrueInputSystem — TISCode_DirectionalButton.cs
//  Code Sub-Tab Module: Directional Btn
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_DirectionalButton : ICodeSubTabModule
    {
        public string Label => "Directional Btn";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Directional Skill Button",
                "TISDirectionalSkillButton — kéo để chọn hướng, nhả để cast (Liên Quân style).");

            win.H2("Option A — Đọc Trực Tiếp Từ Component");
            win.CodeBlock(
                "[SerializeField] TISDirectionalSkillButton _skill1Btn;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_skill1Btn.WasCastThisFrame)\n" +
                "    {\n" +
                "        Vector2 dir   = _skill1Btn.CastDirection;  // normalized, zero = auto-target\n" +
                "        float   angle = _skill1Btn.DirectionAngle; // 0°=phải, 90°=trên\n" +
                "        SpawnSkill(dir, angle);\n" +
                "    }\n\n" +
                "    if (_skill1Btn.IsDragging)\n" +
                "        ShowDirectionArrow(_skill1Btn.Direction);\n" +
                "}");

            win.Sep();
            win.H2("Option B — Dùng Event (Không Cần Reference)");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _skill1Btn.OnDirectionCast += (skillName, dir) =>\n" +
                "    {\n" +
                "        if (dir == Vector2.zero) AutoTargetCast();\n" +
                "        else                    DirectionalCast(dir);\n" +
                "    };\n\n" +
                "    _skill1Btn.OnCancelled.AddListener(() => CancelSkillPreview());\n" +
                "    _skill1Btn.OnDragStarted.AddListener(() => ShowRangeCircle());\n" +
                "}");

            win.Sep();
            win.H2("Option C — TISDirectionalRegistry Pattern");
            win.Body("Dùng registry giống GetSkillAction — không cần reference component:");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (TISMobileButtonRegistry.WasPressedThisFrame(\"Skill1\"))\n" +
                "    {\n" +
                "        Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill1_Dir\");\n" +
                "        SpawnSkill1(dir);\n" +
                "    }\n\n" +
                "    Vector2 aimDir = TISInputBridge.Instance.GetVector2Action(\"AimDirection\");\n" +
                "    UpdateAimIndicator(aimDir);\n" +
                "}");

            win.Sep();
            win.H2("ForceCast — Cast Bằng Code (Tutorial / Automation)");
            win.CodeBlock(
                "_skill1Btn.ForceCast(new Vector2(1f, 1f).normalized);\n\n" +
                "// Cast auto-target (zero direction)\n" +
                "_skill1Btn.ForceCast(Vector2.zero);");

            win.Sep();
            win.H2("Cooldown Control");
            win.CodeBlock(
                "IEnumerator CooldownRoutine(TISDirectionalSkillButton btn, float duration)\n" +
                "{\n" +
                "    float elapsed = 0f;\n" +
                "    while (elapsed < duration)\n" +
                "    {\n" +
                "        elapsed += Time.deltaTime;\n" +
                "        btn.SetCooldownFill(1f - elapsed / duration);\n" +
                "        yield return null;\n" +
                "    }\n" +
                "    btn.SetCooldownFill(0f);\n" +
                "}");
        }
    }
}
#endif
