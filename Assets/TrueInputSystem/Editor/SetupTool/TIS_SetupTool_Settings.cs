// ============================================================
//  TrueInputSystem — InputBridgeSetupTool_Settings.cs  [Partial]
//  Phần 4/5: Platform Settings tabs (Mobile, Camera, Gamepad)
//  và các S_ widget helper.
//
//  LƯU Ý (v2): DrawRightPanel() đã được chuyển sang
//  InputBridgeSetupTool_Panels.cs như một wrapper tab kép
//  (Action Setting + Platform Setting).
//  File này chỉ expose DrawPlatformSettingsContent() và
//  các S_ helper — không còn DrawRightPanel() nữa.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISSetupTool
    {
        // ══════════════════════════════════════════════════════
        //  PLATFORM SETTINGS CONTENT
        //  Được gọi bởi DrawRightPanel() trong Panels.cs
        //  khi tab "Platform Setting" được chọn.
        // ══════════════════════════════════════════════════════

        private void DrawPlatformSettingsContent()
        {
            // Inner tab bar (Mobile / Camera / Gamepad)
            var tabRect = EditorGUILayout.GetControlRect(false, 26f);
            EditorGUI.DrawRect(tabRect, C_BgDeep);

            float tabW = tabRect.width / _settingsTabs.Length;
            for (int i = 0; i < _settingsTabs.Length; i++)
            {
                var tr  = new Rect(tabRect.x + i * tabW, tabRect.y, tabW, tabRect.height);
                bool sel = i == _settingsTab;
                EditorGUI.DrawRect(tr, sel ? new Color(0.16f, 0.17f, 0.21f) : C_BgDeep);
                if (sel)
                    EditorGUI.DrawRect(new Rect(tr.x, tr.yMax - 2f, tr.width, 2f), C_Accent * 0.7f);

                if (GUI.Button(tr, _settingsTabs[i],
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = sel ? C_TextAccent : C_TextDim },
                        fontStyle = sel ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 9,
                    }))
                    _settingsTab = i;
            }
            EditorGUI.DrawRect(
                new Rect(tabRect.x, tabRect.yMax, tabRect.width, 1f), C_Divider);

            GUILayout.Space(6);

            switch (_settingsTab)
            {
                case 0: DrawMobileSettings();  break;
                case 1: DrawCameraSettings();  break;
                case 2: DrawGamepadSettings(); break;
            }
        }

        // ══════════════════════════════════════════════════════
        //  SETTINGS — MOBILE
        // ══════════════════════════════════════════════════════

        private void DrawMobileSettings()
        {
            var s = _workingProfile.mobileSettings;

            S_Group("Virtual Joystick");
            s.joystickRadius   = S_Slider("Radius",       s.joystickRadius,   50f,  250f,
                "Bán kính (pixel) của vùng kéo joystick.\nGiá trị lớn = vùng kéo rộng hơn.");
            s.joystickDeadzone = S_Slider("Deadzone",     s.joystickDeadzone,  0f,   0.5f,
                "Deadzone joystick — loại bỏ rung tay khi cố giữ yên.\n0 = không có deadzone.");
            s.dynamicJoystick  = S_Toggle("Dynamic",      s.dynamicJoystick,
                "ON = joystick xuất hiện tại điểm chạm (PUBG style).\nOFF = cố định ở góc trái màn hình.");
            s.idleOpacity      = S_Slider("Idle Opacity", s.idleOpacity,       0f,   1f,
                "Opacity của joystick khi không được chạm.\n0 = ẩn hoàn toàn, 1 = hiện đầy đủ.");

            S_Group("On-Screen Buttons");
            s.showSkillButtons = S_Toggle("Show Buttons", s.showSkillButtons,
                "ON = hiển thị các button skill trên màn hình mobile.\nOFF = ẩn toàn bộ on-screen button.");
            if (s.showSkillButtons)
                s.skillButtonSize = S_Slider("Button Size", s.skillButtonSize, 60f, 180f,
                    "Kích thước (pixel) của mỗi on-screen skill button.\nKhuyến nghị: 80–120px.");

            S_Group("Camera Touch Zone");
            s.cameraZoneWidthRatio   = S_Slider("Zone Width",  s.cameraZoneWidthRatio,   0.2f, 0.6f,
                "Tỉ lệ chiều rộng màn hình dành cho vùng kéo camera.\n0.4 = 40% bên phải màn hình.");
            s.cameraZoneDebugOpacity = S_Slider("Debug Alpha", s.cameraZoneDebugOpacity,  0f,  0.3f,
                "Opacity vùng camera khi debug — giúp thấy vùng cảm ứng.\nĐặt = 0 trước khi build production.");

            if (s.cameraZoneDebugOpacity > 0f)
                S_Warning("Debug overlay đang bật — nhớ tắt trước khi build.");
        }

        // ══════════════════════════════════════════════════════
        //  SETTINGS — CAMERA
        // ══════════════════════════════════════════════════════

        private void DrawCameraSettings()
        {
            var s = _workingProfile.cameraSettings;

            S_Group("Mouse (PC)");
            s.mouseSensitivity   = S_Vector2("Sensitivity", s.mouseSensitivity,
                "Độ nhạy chuột theo trục X (ngang) và Y (dọc).\nKhuyến nghị: 0.1–0.5.");

            S_Group("Gamepad Right Stick");
            s.gamepadSensitivity = S_Vector2("Sensitivity", s.gamepadSensitivity,
                "Độ nhạy right stick gamepad.\nThường cần lớn hơn mouse nhiều, khuyến nghị: 100–200.");

            S_Group("Mobile Touch");
            s.mobileSensitivity  = S_Vector2("Sensitivity", s.mobileSensitivity,
                "Độ nhạy kéo ngón tay trên mobile.\nKhuyến nghị: 0.08–0.15.");

            S_Group("Behaviour");
            s.invertY   = S_Toggle("Invert Y",  s.invertY,
                "ON = đảo chiều trục Y khi nhìn lên/xuống (flight-sim convention).\nOFF = chiều mặc định.");
            s.smoothing = S_Slider("Smoothing", s.smoothing, 1f, 30f,
                "Tốc độ làm mượt camera.\nCao = camera phản hồi nhanh, thấp = lag cinematic.");

            S_Group("Pitch Limits");
            s.pitchMin = S_Slider("Min (up)",   s.pitchMin, -90f,  0f,
                "Giới hạn góc nhìn lên (giá trị âm = nhìn lên).\nVí dụ: -80 = có thể ngước nhìn gần thẳng đứng.");
            s.pitchMax = S_Slider("Max (down)", s.pitchMax,   0f, 90f,
                "Giới hạn góc nhìn xuống (giá trị dương = nhìn xuống).\nVí dụ: 80 = có thể cúi nhìn gần thẳng đứng.");
        }

        // ══════════════════════════════════════════════════════
        //  SETTINGS — GAMEPAD
        // ══════════════════════════════════════════════════════

        private void DrawGamepadSettings()
        {
            var s = _workingProfile.gamepadSettings;

            S_Group("Deadzone");
            s.leftStickDeadzone  = S_Slider("Left Stick",  s.leftStickDeadzone,  0f, 0.5f,
                "Deadzone left stick (di chuyển).\nTránh nhân vật tự trôi khi tay cầm bị drift.");
            s.rightStickDeadzone = S_Slider("Right Stick", s.rightStickDeadzone, 0f, 0.5f,
                "Deadzone right stick (xoay camera).\nTránh camera tự xoay khi tay cầm bị drift.");

            S_Group("Response Curve");
            s.stickCurve = S_Slider("Curve", s.stickCurve, 1f, 3f,
                "Đường cong phản hồi của stick.\n1 = tuyến tính, >1 = chính xác hơn khi ngắm nhẹ.");

            S_Group("Haptics");
            s.enableRumble = S_Toggle("Enable Rumble", s.enableRumble,
                "ON = bật rung / haptic feedback khi nhận damage hoặc hành động đặc biệt.\nOFF = tắt hoàn toàn.");
        }

        // ══════════════════════════════════════════════════════
        //  S_ WIDGET HELPERS
        // ══════════════════════════════════════════════════════

        /// <summary>Vẽ group header cho một nhóm setting.</summary>
        private void S_Group(string title)
        {
            GUILayout.Space(6);
            var rect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(rect, C_BgDeep);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), C_Accent * 0.6f);
            GUI.Label(rect, "  " + title,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = C_TextAccent },
                    fontSize  = 9,
                    alignment = TextAnchor.MiddleLeft,
                });
            GUILayout.Space(2);
        }

        /// <summary>Warning box dạng bar màu cam.</summary>
        private void S_Warning(string msg)
        {
            GUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.18f, 0.05f));
            DrawBorder(rect, C_Orange * 0.6f, 1f);
            GUI.Label(rect, "  ⚠  " + msg,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    normal   = { textColor = C_Orange },
                });
        }

        /// <summary>Slider row: label (80px) + HorizontalSlider + value label.</summary>
        private float S_Slider(string label, float value, float min, float max, string tooltip = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            float newVal = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            GUILayout.Label(value.ToString("F2"),
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextAccent },
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                },
                GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            return newVal;
        }

        /// <summary>Toggle row: label + ON/OFF button.</summary>
        private bool S_Toggle(string label, bool value, string tooltip = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            bool newVal = GUILayout.Toggle(value, value ? "ON" : "OFF",
                new GUIStyle(EditorStyles.miniButton)
                {
                    normal    = { textColor = value ? C_Green : C_TextDim },
                    fontStyle = FontStyle.Bold,
                },
                GUILayout.Width(36));
            if (EditorGUI.EndChangeCheck()) MarkDirty();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            return newVal;
        }

        /// <summary>Vector2 row: label + X float field + Y float field.</summary>
        private Vector2 S_Vector2(string label, Vector2 value, string tooltip = "")
        {
            GUILayout.Label(new GUIContent(label, tooltip),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } });

            GUILayout.BeginHorizontal();
            GUILayout.Label("X",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_Red }, fontStyle = FontStyle.Bold },
                GUILayout.Width(14));
            EditorGUI.BeginChangeCheck();
            float nx = EditorGUILayout.FloatField(value.x, GUILayout.ExpandWidth(true));

            GUILayout.Label("Y",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_Green }, fontStyle = FontStyle.Bold },
                GUILayout.Width(14));
            float ny = EditorGUILayout.FloatField(value.y, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            return new Vector2(nx, ny);
        }
    }
}
#endif