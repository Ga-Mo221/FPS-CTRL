// ============================================================
//  TrueInputSystem — TISCode_ObjectTracker.cs
//  Code Sub-Tab Module: ObjectTracker
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_ObjectTracker : ICodeSubTabModule
    {
        public string Label => "ObjectTracker";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("TISTouchObjectTracker",
                "Raycast touch → tích lũy InstanceID. Double-tap detect trên 3D/2D object.");

            win.H2("Setup");
            win.Step(1, "Add Component", "TISTouchObjectTracker lên GameObject chứa Camera (hoặc scene manager).");
            win.Step(2, "Bật Object Tracker", "Tick Enable Object Tracker trong Inspector của TISInputBridge.");
            win.Step(3, "Subscribe events", "OnObjectTapped, OnObjectDoubleTapped, OnObjectHeld.");

            win.Sep();
            win.H2("Phát Hiện Tap & Double-Tap");
            win.CodeBlock(
                "[RequireComponent(typeof(TISTouchObjectTracker))]\n" +
                "public class SelectionHandler : MonoBehaviour\n" +
                "{\n" +
                "    TISTouchObjectTracker _tracker;\n\n" +
                "    void Awake() => _tracker = GetComponent<TISTouchObjectTracker>();\n\n" +
                "    void OnEnable()\n" +
                "    {\n" +
                "        _tracker.OnObjectTapped       += OnTap;\n" +
                "        _tracker.OnObjectDoubleTapped  += OnDoubleTap;\n" +
                "    }\n\n" +
                "    void OnDisable()\n" +
                "    {\n" +
                "        _tracker.OnObjectTapped       -= OnTap;\n" +
                "        _tracker.OnObjectDoubleTapped  -= OnDoubleTap;\n" +
                "    }\n\n" +
                "    void OnTap(GameObject hit)\n" +
                "    {\n" +
                "        SelectObject(hit);\n" +
                "    }\n\n" +
                "    void OnDoubleTap(GameObject hit)\n" +
                "    {\n" +
                "        OpenContextMenu(hit);\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Query — GetTrackedObjects");
            win.CodeBlock(
                "// Lấy danh sách tất cả object đang được chạm (tích lũy trong frame)\n" +
                "var tracked = _tracker.GetTrackedObjects();\n" +
                "foreach (var obj in tracked)\n" +
                "    HighlightObject(obj);\n\n" +
                "// Kiểm tra object cụ thể đang bị chạm không\n" +
                "if (_tracker.IsBeingTouched(myObject))\n" +
                "    ShowSelectionRing(myObject);");

            win.Note("TISTouchObjectTracker dùng Physics.Raycast và Physics2D.Raycast. Bật đúng layer mask trong Inspector để tránh detect các object không mong muốn.");
        }
    }
}
#endif
