// ============================================================
//  TrueInputSystem — TISCode_AimAssistDeadzone.cs
//  Code Sub-Tab Module: AimAssist/DZ
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_AimAssistDeadzone : ICodeSubTabModule
    {
        public string Label => "AimAssist/DZ";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("AimAssist & Deadzone",
                "AimAssistProcess + ApplyDeadzone — hỗ trợ ngắm và xử lý vùng chết analog.");

            win.H2("AimAssistSettings — Cấu Hình");
            win.CodeBlock(
                "readonly AimAssistSettings _aimSettings = new AimAssistSettings(\n" +
                "    snapRadius:         8f,    // góc độ từ tâm màn hình để kích hoạt\n" +
                "    assistSpeed:        6f,    // tốc độ hút camera về target\n" +
                "    assistWithoutInput: false, // false = chỉ hỗ trợ khi có input (chống aimbot)\n" +
                "    enabledPlatforms:   AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile\n" +
                ");");

            win.Sep();
            win.H2("AimAssistProcess — Tích Hợp Với Camera");
            win.CodeBlock(
                "void LateUpdate()\n" +
                "{\n" +
                "    var bridge = TISInputBridge.Instance;\n" +
                "    Vector2 look = bridge.LookDelta;\n\n" +
                "    if (bridge.AimAssistTarget != null)\n" +
                "    {\n" +
                "        look = bridge.AimAssistProcess(\n" +
                "            _aimSettings,\n" +
                "            Camera.main.transform,\n" +
                "            bridge.AimAssistTarget.position,\n" +
                "            look,\n" +
                "            intensity: 0.5f  // 0=tắt, 1=mạnh nhất\n" +
                "        );\n" +
                "    }\n\n" +
                "    _yaw   += look.x;\n" +
                "    _pitch -= look.y;\n" +
                "    transform.eulerAngles = new Vector3(_pitch, _yaw, 0);\n" +
                "}\n\n" +
                "void OnLockOn(Transform enemy)  => TISInputBridge.Instance.AimAssistTarget = enemy;\n" +
                "void OnLockOff()                => TISInputBridge.Instance.AimAssistTarget = null;");

            win.Sep();
            win.H2("ApplyDeadzone — Scaled Radial Deadzone");
            win.Body("Dùng khi muốn tự xử lý deadzone cho raw input từ thiết bị:");
            win.CodeBlock(
                "Vector2 rawStick = new Vector2(\n" +
                "    Input.GetAxis(\"Horizontal\"),\n" +
                "    Input.GetAxis(\"Vertical\")\n" +
                ");\n\n" +
                "// Scaled Radial: input < 0.2f → zero, input = 1f → 1f\n" +
                "Vector2 processed = TISInputBridge.Instance.ApplyDeadzone(rawStick, 0.2f);\n\n" +
                "transform.Translate(processed.x * speed * Time.deltaTime, 0, processed.y * speed * Time.deltaTime);");

            win.Note("ApplyDeadzone dùng Scaled Radial formula: (magnitude - deadzone) / (1 - deadzone). Input nhỏ hơn deadzone → Vector2.zero. Input > 1 bị clamp về 1.");
        }
    }
}
#endif
