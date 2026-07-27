// ============================================================
//  TrueInputSystem — TISCode_Camera.cs
//  Code Sub-Tab Module: Camera
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_Camera : ICodeSubTabModule
    {
        public string Label => "Camera";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Camera Input — LookDelta & ZoomDelta",
                "Xoay camera và zoom thống nhất: chuột, gamepad right stick và kéo ngón tay.");

            win.H2("LookDelta — Xoay Camera");
            win.Body("PC: delta chuột | Gamepad: right stick | Mobile: kéo ngón trên CameraZone. Tự chuẩn hoá mouse delta (chia 100):");
            win.CodeBlock(
                "Vector2 look = _input.LookDelta;\n\n" +
                "// Áp dụng vào yaw / pitch\n" +
                "yaw   += look.x * sensitivity;\n" +
                "pitch -= look.y * sensitivity;\n" +
                "pitch  = Mathf.Clamp(pitch, -80f, 80f);\n\n" +
                "transform.rotation = Quaternion.Euler(pitch, yaw, 0f);");

            win.Sep();
            win.H2("ZoomDelta — Pinch Zoom / Scroll Wheel");
            win.Body("Consume pattern — tự reset về 0 sau mỗi lần đọc. Dương = zoom in, âm = zoom out:");
            win.CodeBlock(
                "float zoom = _input.ZoomDelta;\n\n" +
                "if (Mathf.Abs(zoom) > 0.001f)\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * zoomSpeed,\n" +
                "                            minDistance, maxDistance);");

            win.Note("ZoomDelta được consume mỗi frame — chỉ đọc 1 lần trong Update(). Đọc nhiều lần sẽ luôn nhận 0 ở lần thứ 2.");

            win.Sep();
            win.H2("Override LookDelta (Camera Swipe Zone / Replay)");
            win.Body("Inject delta xoay từ vùng kéo ngón tay, AI director, hoặc replay system:");
            win.CodeBlock(
                "// Trong TISTouchCameraZone — inject delta từ ngón tay\n" +
                "public void OnCameraZoneDrag(Vector2 delta)\n" +
                "{\n" +
                "    TISInputBridge.Instance.OverrideLookInput(delta * sensitivity);\n" +
                "}\n\n" +
                "// Gọi khi nhả ngón — hoàn trả lại input thật\n" +
                "public void OnCameraZoneRelease()\n" +
                "{\n" +
                "    TISInputBridge.Instance.ClearLookOverride();\n" +
                "}");

            win.Sep();
            win.H2("Override ZoomDelta (Pinch Gesture)");
            win.Body("TISTouchCameraZone gọi method này nội bộ. Bạn cũng có thể gọi từ gesture recognizer tự viết:");
            win.CodeBlock(
                "// Inject pinch zoom từ gesture recognizer\n" +
                "float pinchDelta = GetPinchDelta();\n" +
                "TISInputBridge.Instance.OverrideZoomInput(pinchDelta);\n\n" +
                "// Khi kết thúc gesture\n" +
                "TISInputBridge.Instance.ClearZoomOverride();");
        }
    }
}
#endif
