// ============================================================
//  TrueInputSystem — TISCode_GestureDetector.cs
//  Code Sub-Tab Module: GestureDetector
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_GestureDetector : ICodeSubTabModule
    {
        public string Label => "GestureDetector";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("TISMultiFingerGestureDetector",
                "Theo dõi centroid, two-finger pan, delta từng ngón trong camera zone.");

            win.H2("Setup");
            win.Body("Gắn TISMultiFingerGestureDetector lên cùng GameObject với TISTouchCameraZone hoặc bất kỳ GameObject nào.");
            win.Step(1, "Add Component", "TISMultiFingerGestureDetector — không cần config Inspector.");
            win.Step(2, "Subscribe events", "OnGestureUpdate nhận GestureData mỗi frame khi có ≥2 ngón tay.");
            win.Step(3, "Đọc data", "gesture.Centroid, gesture.PanDelta, gesture.PinchDelta, gesture.FingerCount.");

            win.Sep();
            win.H2("Đọc Gesture Data Cơ Bản");
            win.CodeBlock(
                "[RequireComponent(typeof(TISMultiFingerGestureDetector))]\n" +
                "public class MyGestureHandler : MonoBehaviour\n" +
                "{\n" +
                "    TISMultiFingerGestureDetector _gesture;\n\n" +
                "    void Awake() => _gesture = GetComponent<TISMultiFingerGestureDetector>();\n\n" +
                "    void OnEnable()  => _gesture.OnGestureUpdate += HandleGesture;\n" +
                "    void OnDisable() => _gesture.OnGestureUpdate -= HandleGesture;\n\n" +
                "    void HandleGesture(TISGestureData data)\n" +
                "    {\n" +
                "        // Two-finger pan\n" +
                "        if (data.FingerCount == 2)\n" +
                "        {\n" +
                "            PanMap(data.PanDelta);\n" +
                "            ZoomMap(data.PinchDelta);\n" +
                "        }\n\n" +
                "        // Three-finger rotate\n" +
                "        if (data.FingerCount == 3)\n" +
                "            RotateMap(data.RotationDelta);\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Centroid — Tâm Của Các Ngón Tay");
            win.CodeBlock(
                "// Vị trí trung bình (screen space) của tất cả ngón đang chạm\n" +
                "Vector2 center = data.Centroid;\n\n" +
                "// Dùng để xác định vùng tương tác\n" +
                "if (center.x < Screen.width * 0.5f)\n" +
                "    HandleLeftSideGesture(data);");

            win.Note("TISMultiFingerGestureDetector chỉ active khi có ≥2 ngón tay trong vùng. OnGestureUpdate không fire khi chỉ có 1 ngón.");
        }
    }
}
#endif
