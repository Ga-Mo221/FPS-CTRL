// ============================================================
//  TrueInputSystem — TISCode_Rumble.cs
//  Code Sub-Tab Module: Rumble
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Rumble : ICodeSubTabModule
    {
        public string Label => "Rumble";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Gamepad Rumble",
                "TryRumble và StopRumble — haptic feedback trên gamepad.");

            win.H2("TryRumble — Rung Có Hẹn Giờ");
            win.Body("Rung trong duration giây rồi tự dừng. Tham số: lowFrequency (motor to), highFrequency (motor nhỏ):");
            win.CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n\n" +
                "// Trúng đạn — rung ngắn\n" +
                "void OnHit()     => input.TryRumble(0.6f, 0.3f, 0.15f);\n\n" +
                "// Nổ lớn gần\n" +
                "void OnExplode() => input.TryRumble(1.0f, 0.8f, 0.4f);\n\n" +
                "// Bắn súng ngắn\n" +
                "void OnShoot()   => input.TryRumble(0.0f, 0.35f, 0.05f);\n\n" +
                "// Rung scale theo damage\n" +
                "void OnHit(float damage)\n" +
                "{\n" +
                "    float intensity = Mathf.Clamp(damage / 100f, 0.3f, 2f);\n" +
                "    input.TryRumble(intensity * 0.6f, intensity * 0.3f, 0.2f);\n" +
                "}");

            win.Sep();
            win.H2("TryRumble — Rung Liên Tục");
            win.Body("Không hẹn giờ — gọi StopRumble() để dừng. Dùng cho: đứng gần vực, drift xe, debuff rung:");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillHeld(\"Drift\"))\n" +
                "        _input.TryRumble(0.7f, 0.2f);\n" +
                "    else\n" +
                "        _input.StopRumble();\n" +
                "}\n\n" +
                "// Rung khi trong vùng nguy hiểm\n" +
                "void Update2()\n" +
                "{\n" +
                "    if (IsInDangerZone())\n" +
                "        _input.TryRumble(0.4f, 0.1f);\n" +
                "    else\n" +
                "        _input.StopRumble();\n" +
                "}");

            win.Sep();
            win.H2("StopRumble — Dừng Ngay Lập Tức");
            win.Body("Luôn gọi trong OnDisable() hoặc OnDestroy() để tránh controller rung vô hạn khi scene unload:");
            win.CodeBlock(
                "void OnDisable()\n" +
                "{\n" +
                "    TISInputBridge.Instance?.StopRumble();\n" +
                "}");

            win.Sep();
            win.H2("Điều Kiện Hoạt Động");
            win.Body("Rumble chỉ hoạt động khi tất cả điều kiện sau đều đúng:");
            win.CodeBlock(
                "// 1. enableRumble = true trong TISInputProfileConfig → tab Gamepad\n" +
                "// 2. Gamepad.current != null (tay cầm đang kết nối)\n" +
                "// 3. Gọi qua TISInputBridge (không gọi Gamepad.current.SetMotorSpeeds trực tiếp)\n\n" +
                "// Kiểm tra nhanh:\n" +
                "if (TISInputBridge.Instance.TISGamepadSettings.enableRumble)\n" +
                "    Debug.Log(\"Rumble enabled\");");

            win.Warn("Rumble không hoạt động trên mobile. enableRumble = false trong profile mobile để tránh lỗi.");
        }
    }
}
#endif
