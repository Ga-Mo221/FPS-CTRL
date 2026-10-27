#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Fix: Sau khi chọn object ở Hierarchy (hoặc bất kỳ panel nào khác), keyboard focus
    /// của Unity vẫn nằm ở panel đó, không phải Scene View. Do đó các phím tắt xử lý qua
    /// SceneView.duringSceneGui (G/R/S, pie menu hotkeys...) không nhận được KeyDown cho
    /// tới khi người dùng click chuột vào Scene View, vì Unity chỉ gửi sự kiện bàn phím
    /// tới EditorWindow.focusedWindow (khác với sự kiện chuột, vốn gửi theo cửa sổ đang
    /// nằm dưới con trỏ).
    ///
    /// Script này ép Scene View nhận focus ngay khi chuột di vào / di chuyển trong đó,
    /// giống hành vi "focus follows mouse" của Blender.
    /// </summary>
    [InitializeOnLoad]
    static class BlenderSceneFocusFix
    {
        static BlenderSceneFocusFix()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sv)
        {
            // Bật nhận MouseEnterWindow/MouseLeaveWindow (idempotent, vô hại nếu đã bật).
            sv.wantsMouseEnterLeaveWindow = true;

            Event e = Event.current;
            if (e == null) return;

            bool mouseActivity = e.type == EventType.MouseEnterWindow
                               || e.type == EventType.MouseMove
                               || e.type == EventType.MouseDown;

            if (!mouseActivity) return;

            // Nếu Scene View chưa phải cửa sổ đang có keyboard focus -> ép focus.
            if (EditorWindow.focusedWindow != sv)
            {
                sv.Focus();
            }
        }
    }
}
#endif
