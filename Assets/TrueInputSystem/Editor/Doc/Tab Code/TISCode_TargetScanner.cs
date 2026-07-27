// ============================================================
//  TrueInputSystem — TISCode_TargetScanner.cs
//  Code Sub-Tab Module: TargetScanner
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_TargetScanner : ICodeSubTabModule
    {
        public string Label => "TargetScanner";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("TISTargetScanner",
                "Tự động lock target gần tâm màn hình nhất — kết hợp với AimAssistProcess().");

            win.H2("Setup");
            win.Body("Gắn TISTargetScanner lên Camera GameObject. Cấu hình trong Inspector: targetTags, scanAngle, maxDistance, scanInterval.");
            win.CodeBlock(
                "// Inspector setup:\n" +
                "// ▸ Target Filter: Tags = [\"Enemy\"], hoặc bật useLayerInsteadOfTag\n" +
                "// ▸ Scan Settings: scanAngle = 10°, maxDistance = 50m, scanInterval = 0.1s\n" +
                "// ▸ AimPointMode: ColliderCenter (khuyến nghị)");

            win.Sep();
            win.H2("Cơ Bản — HasTarget + AimAssistProcess");
            win.CodeBlock(
                "[SerializeField] TISTargetScanner _scanner;\n\n" +
                "readonly AimAssistSettings _aimSettings = new AimAssistSettings(\n" +
                "    snapRadius: 8f, assistSpeed: 6f, assistWithoutInput: false,\n" +
                "    enabledPlatforms: AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile\n" +
                ");\n\n" +
                "void LateUpdate()\n" +
                "{\n" +
                "    var bridge = TISInputBridge.Instance;\n" +
                "    Vector2 look = bridge.LookDelta;\n\n" +
                "    if (_scanner.HasTarget)\n" +
                "        look = bridge.AimAssistProcess(\n" +
                "            _aimSettings, Camera.main.transform,\n" +
                "            _scanner.CurrentAimPoint, look, intensity: 0.45f\n" +
                "        );\n\n" +
                "    _yaw   += look.x * sensitivity;\n" +
                "    _pitch  = Mathf.Clamp(_pitch - look.y * sensitivity, -80f, 80f);\n" +
                "    Camera.main.transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);\n" +
                "}");

            win.Sep();
            win.H2("ForceScan & ClearTarget");
            win.CodeBlock(
                "// Quét lại ngay sau khi spawn wave mới\n" +
                "void OnWaveSpawned() => _scanner.ForceScan();\n\n" +
                "// Khi enemy bị destroy\n" +
                "void OnEnemyDied(GameObject enemy)\n" +
                "{\n" +
                "    if (_scanner.CurrentTarget == enemy.transform)\n" +
                "        _scanner.ClearTarget();\n" +
                "}\n\n" +
                "// Debug thông tin target\n" +
                "void LogTarget()\n" +
                "{\n" +
                "    if (_scanner.HasTarget)\n" +
                "        Debug.Log($\"Target: {_scanner.CurrentTarget.name}, Angle: {_scanner.CurrentAngle:F1}°\");\n" +
                "}");
        }
    }
}
#endif
