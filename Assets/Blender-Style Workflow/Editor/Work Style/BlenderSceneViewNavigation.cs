#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [InitializeOnLoad]
    public static class BlenderSceneViewNavigation
    {
        static Vector2 lastMousePos; 
        static bool isOrbiting = false;
        static bool isZooming = false;  // Ctrl + MMB drag
        // Unity "Scene View/Zoom 1" (Clutch) nuốt e.button khi drag,
        // nên ta track riêng bằng GUIUtility.hotControl
        //static int zoomControlID = -1;

        static BlenderSceneViewNavigation()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            // ===== CHECK IF FEATURE IS ENABLED =====
            if (!BlenderEditorPreferences.SceneNavigationEnabled)
                return;
            
            Event e = Event.current;
            if (e == null) return;

            // ===== ZOOM (Scroll) =====
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = e.delta.y * (sceneView.size * 0.05f); 
                sceneView.size += zoomDelta;
                sceneView.Repaint();
                e.Use();
            }

            // ===== MIDDLE MOUSE HANDLING =====
            // Nhường MMB nếu có hệ thống ưu tiên cao hơn đang active (Pie Menu, phiên G/R/S,
            // hoặc đang kéo gizmo Move/Rotate/Scale). Trước đây chỉ kiểm tra riêng
            // BlenderControls.HasActiveSession nên orbit vẫn có thể "cướp" MMB ngay giữa lúc
            // pie menu đang mở hoặc đang kéo gizmo — nay dùng đúng 1 nguồn sự thật duy nhất.
            if (!BlenderInputPriority.IsFreeFor(BlenderInputPriority.Owner.SceneNavigation)) return;

            if (e.button == 2)
            {
                if (e.type == EventType.MouseDown)
                {
                    lastMousePos = e.mousePosition;

                    // ===== ZOOM MODE (Ctrl + MMB) =====
                    if (e.control)
                    {
                        isZooming = true;
                        isOrbiting = false;
                    }
                    else
                    {
                        isOrbiting = true;
                        isZooming = false;
                    }

                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    isOrbiting = false;
                    isZooming = false;
                }
                else if (e.type == EventType.MouseDrag)
                {
                    Vector2 currentMousePos = e.mousePosition;
                    Vector2 delta = currentMousePos - lastMousePos;
                    lastMousePos = currentMousePos;

                    // ===== ZOOM (Ctrl + MMB drag) =====
                    if (isZooming)
                    {
                        // Di chuyển lên (delta.y âm) → zoom in, xuống → zoom out
                        // Dùng delta.y; nhân với size để zoom có cảm giác đều ở mọi khoảng cách
                        float zoomDelta = delta.y * (sceneView.size * 0.005f);
                        sceneView.size = Mathf.Max(0.01f, sceneView.size + zoomDelta);
                        sceneView.Repaint();
                        e.Use();
                    }
                    else if (isOrbiting)
                    {
                        // ===== PAN (Shift + MMB) =====
                        if (e.shift)
                        {
                            Pan(sceneView, delta);
                        }
                        // ===== ORBIT (MMB Only) =====
                        else
                        {
                            // Thoát Orthographic → chuyển về Perspective trước khi orbit
                            if (sceneView.orthographic && BlenderEditorPreferences.PieMenuExitOrthoOnOrbit)
                                sceneView.orthographic = false;

                            Orbit(sceneView, delta);
                        }

                        sceneView.Repaint();
                        e.Use();
                    }
                }
            }
        }

        static void Pan(SceneView view, Vector2 delta)
        {
            float panSpeed = view.size * 0.0025f;
            Transform cam = view.camera.transform;
            
            Vector3 right = cam.right * -delta.x * panSpeed;
            Vector3 up = cam.up * delta.y * panSpeed;
            
            Vector3 move = right + up;
            view.pivot += move;
        }

        static void Orbit(SceneView view, Vector2 delta)
        {
            const float orbitSpeed = 0.4f;
            const float maxPitch = 89.9f;

            // Không đọc/ghi lại eulerAngles: khi pitch vượt 90°, Unity tách euler thành
            // (x, y+180, z=180) → ép z=0 làm view lật 180° qua lại liên tục.
            // Thay vào đó tính pitch từ hướng nhìn, clamp, rồi xoay bằng quaternion.
            Quaternion rot = view.rotation;
            Vector3 forward = rot * Vector3.forward;
            float currentPitch = Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float targetPitch = Mathf.Clamp(currentPitch + delta.y * orbitSpeed, -maxPitch, maxPitch);
            float pitchDelta = targetPitch - currentPitch;
            float yawDelta = delta.x * orbitSpeed;

            // Yaw quanh trục Y thế giới, pitch quanh trục X cục bộ của camera
            rot = Quaternion.AngleAxis(yawDelta, Vector3.up) * rot * Quaternion.AngleAxis(pitchDelta, Vector3.right);

            view.rotation = rot;
            view.LookAtDirect(view.pivot, view.rotation);
        }
    }
}
#endif