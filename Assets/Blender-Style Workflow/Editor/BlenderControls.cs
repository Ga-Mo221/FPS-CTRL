using UnityEngine;
using UnityEditor;
namespace GaMo.BlenderEditorTools
{
    [InitializeOnLoad]
    public static class BlenderControls
    {
        static BlenderTransformSession session;

        /// Dùng bởi BlenderSceneViewNavigation để nhường MMB khi đang trong G/R/S session.
        public static bool HasActiveSession => session != null;

        static BlenderControls()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// Trả về true nếu người dùng đang dùng Terrain tool (Paint, Sculpt, v.v.).
        /// Khi đó các phím G/R/S sẽ bị tắt để tránh xung đột.
        /// </summary>
        static bool IsTerrainToolActive()
        {
            // Kiểm tra object được chọn có phải Terrain không
            var selectedObj = Selection.activeGameObject;
            if (selectedObj != null && selectedObj.GetComponent<Terrain>() != null)
                return true;

            // Kiểm tra EditorTool hiện tại có phải terrain tool không
            // (Unity 2020.2+ dùng ToolManager)
#if UNITY_2020_2_OR_NEWER
            var activeTool = UnityEditor.EditorTools.ToolManager.activeToolType;
            if (activeTool != null)
            {
                string toolName = activeTool.FullName ?? "";
                if (toolName.Contains("Terrain") || toolName.Contains("terrain"))
                    return true;
            }
#endif
            return false;
        }

        static void OnSceneGUI(SceneView view)
        {
            // ===== CHECK IF FEATURE IS ENABLED =====
            if (!BlenderEditorPreferences.TransformHotkeysEnabled)
                return;
            
            Event e = Event.current;
            Transform target = Selection.activeTransform;
            
            if (target == null) return;

            // ===== 1. DRAW BUTTONS WHEN IDLE =====
            if (session == null)
            {
                BlenderTransformSession.DrawContextButtons(target);
            }

            // Lock Unity default controls when in session
            if (session != null)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // ===== 2. START SESSION (G, R, S) =====
            // Bỏ qua nếu đang dùng Terrain tool để tránh xung đột phím tắt
            if (session == null && IsTerrainToolActive()) return;

            if (session == null && e.type == EventType.KeyDown && !e.alt && !e.control && !e.command) 
            {
                if (e.keyCode == KeyCode.G)
                    session = new BlenderTransformSession(BlenderTransformSession.TransformMode.Move, target, e.mousePosition);

                else if (e.keyCode == KeyCode.R)
                    session = new BlenderTransformSession(BlenderTransformSession.TransformMode.Rotate, target, e.mousePosition);

                else if (e.keyCode == KeyCode.S)
                    session = new BlenderTransformSession(BlenderTransformSession.TransformMode.Scale, target, e.mousePosition);

                if (session != null) e.Use();
            }

            if (session == null) return;

            // ===== 3. HANDLE INPUT (KEYBOARD & MOUSE) =====
            
            // A. Xử lý Bàn phím
            if (e.type == EventType.KeyDown)
            {
                // Axis Constraints
                if (e.keyCode == KeyCode.X) session.SetAxis(Vector3.right, e.shift);
                if (e.keyCode == KeyCode.Y) session.SetAxis(Vector3.up, e.shift);
                if (e.keyCode == KeyCode.Z) session.SetAxis(Vector3.forward, e.shift);

                // Numeric Input
                if (e.character != '\0') session.AppendNumeric(e.character);
                if (e.keyCode == KeyCode.Backspace) session.Backspace();

                // Confirm (Enter)
                if (e.keyCode == KeyCode.Return) 
                {
                    ConfirmSession();
                    e.Use();
                    return; 
                }

                // Cancel (Escape)
                if (e.keyCode == KeyCode.Escape) 
                {
                    CancelSession();
                    e.Use();
                    return;
                }

                // Consume event if valid key
                if (session != null) e.Use();
            }

            // B. Xử lý Chuột
            if (e.type == EventType.MouseDown)
            {
                // ===== MMB (button 2) trong session → bắt đầu detect axis =====
                if (e.button == 2)
                {
                    session.OnMMBDown(e.mousePosition);
                    e.Use();
                    return;
                }

                // Chuột trái (0) = Xác nhận
                if (e.button == 0)
                {
                    ConfirmSession();
                    e.Use();
                    return;
                }
                // Chuột phải (1) = Hủy
                else if (e.button == 1)
                {
                    CancelSession();
                    e.Use();
                    return;
                }
            }

            // ===== MMB Drag → update tracking position =====
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                session.OnMMBDrag(e.mousePosition);
                e.Use();
                return;
            }

            // ===== MMB Up → commit axis detection =====
            if (e.type == EventType.MouseUp && e.button == 2)
            {
                session.OnMMBUp(view.camera);
                e.Use();
                return;
            }

            // ===== 4. UPDATE VISUALS =====
            bool shouldUpdate = e.type == EventType.MouseMove
                             || e.type == EventType.MouseDrag
                             || e.type == EventType.KeyDown
                             || e.type == EventType.KeyUp
                             || e.type == EventType.Repaint
                             || e.type == EventType.Layout;

            if (session != null && shouldUpdate)
            {
                session.Update(e.mousePosition, view.camera, e.shift);
                session.DrawHUD();
                view.Repaint();
            }
        }

        static void ConfirmSession()
        {
            session?.OnConfirm();
            session = null;
        }

        static void CancelSession()
        {
            if (session != null)
            {
                session.Cancel();
                session = null;
            }
        }
    }
}