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

namespace GaMo.TrueInputSystem.Editor
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
                "Đường cong phản hồi CẦN PHẢI (ngắm).\n1 = tuyến tính, >1 = chính xác hơn khi ngắm nhẹ.\n" +
                "Cần trái (di chuyển) luôn tuyến tính.");

            S_Group("Haptics");
            s.enableRumble = S_Toggle("Enable Rumble", s.enableRumble,
                "ON = bật rung / haptic feedback khi nhận damage hoặc hành động đặc biệt.\nOFF = tắt hoàn toàn.");

            DrawMotionSettings();
        }

        // ══════════════════════════════════════════════════════
        //  SETTINGS — GAMEPAD › MOTION (Gyro + Touchpad)
        // ══════════════════════════════════════════════════════

        private void DrawMotionSettings()
        {
            if (_workingProfile.motionSettings == null)
            {
                _workingProfile.motionSettings = new TISMotionSettings();
                MarkDirty();
            }
            var m = _workingProfile.motionSettings;

            GUILayout.Space(8);
            DrawSdl3Status();

            S_Group("Motion › Chung");
            m.onlyWhenGamepadActive = S_Toggle("Chỉ khi dùng tay cầm", m.onlyWhenGamepadActive,
                "Người chơi chuyển sang bàn phím-chuột / cảm ứng → gyro / touchpad ngừng tác động lên action\n" +
                "(tay cầm đặt trên bàn không làm xoay camera). Bấm lại tay cầm là chạy tiếp.");

            S_Group("Motion › Gyro Aim");
            m.enableGyro = S_Toggle("Enable Gyro", m.enableGyro,
                "Công tắc tổng gyro. Đưa gyro vào game bằng binding <TISMotion>/gyro\n" +
                "hoặc ô \"+ Gyro\" của binding Left / Right Stick (Cài Đặt Action).");

            if (m.enableGyro)
            {
                m.gyroActivation = S_Enum("Activation", m.gyroActivation,
                    "Always = luôn bật.\n" +
                    "WhileActionHeld = chỉ bật khi giữ action (ví dụ ngắm — ADS).\n" +
                    "DisabledWhileActionHeld = tắt khi giữ action (nút 'nhấc tay' để chỉnh lại tư thế cầm).");

                if (m.gyroActivation != TISGyroActivation.Always)
                    m.gyroActivationAction = S_ActionPopup("Action", m.gyroActivationAction,
                        "Action dùng để bật / tắt gyro. Lấy từ danh sách action của profile này.");

                m.gyroSpace = S_Enum("Space", m.gyroSpace,
                    "Local = yaw theo trục Y của tay cầm — đúng khi cầm tay cầm nằm ngang.\n" +
                    "Player = yaw theo trục trọng lực — xoay hay nghiêng tay cầm đều quay ngang được (khuyến nghị).");

                m.gyroSensitivity = S_Vector2("Sensitivity", m.gyroSensitivity,
                    "Hệ số độ/giây → LookDelta (cùng đơn vị với right stick sau khi nhân sensitivity).\n" +
                    "Để xoay tay cầm 1° = camera 1°: đặt ≈ 1 / (hệ số xoay camera của game).");

                m.invertGyroX = S_Toggle("Invert X", m.invertGyroX, "Đảo chiều quay ngang của gyro.");
                m.invertGyroY = S_Toggle("Invert Y", m.invertGyroY, "Đảo chiều nhìn lên / xuống của gyro.");

                S_Group("Motion › Gyro Filtering");
                m.gyroDeadzone = S_Slider("Deadzone", m.gyroDeadzone, 0f, 10f,
                    "Tốc độ góc (độ/giây) dưới ngưỡng này bị bỏ hoàn toàn — lọc nhiễu cảm biến.");
                m.tighteningThreshold = S_Slider("Tightening", m.tighteningThreshold, 0f, 20f,
                    "Dưới ngưỡng này (độ/giây) input bị giảm tỉ lệ để chống rung tay.\n0 = tắt.");
                m.smoothingThreshold = S_Slider("Smooth Thr.", m.smoothingThreshold, 0f, 30f,
                    "Soft-tier smoothing: chuyển động chậm hơn ngưỡng này (độ/giây) được làm mượt,\n" +
                    "nhanh hơn thì giữ nguyên để flick không bị trễ. 0 = tắt.");
                if (m.smoothingThreshold > 0f)
                    m.smoothingTime = S_Slider("Smooth Time", m.smoothingTime, 0.01f, 0.5f,
                        "Thời gian làm mượt (giây) cho vùng soft-tier.");

                S_Group("Motion › Gyro Calibration");
                m.autoCalibrate = S_Toggle("Auto Calibrate", m.autoCalibrate,
                    "ON = tự trừ drift khi tay cầm được đặt yên.\n" +
                    "OFF = chỉ calibration thủ công (TISInputBridge.Motion.Begin/EndGyroCalibration).");
                if (m.autoCalibrate)
                {
                    m.stillnessTime = S_Slider("Still Time", m.stillnessTime, 0.2f, 5f,
                        "Tay cầm phải đứng yên bao lâu (giây) trước khi bắt đầu tự hiệu chỉnh.");
                    m.stillnessGyroThreshold = S_Slider("Still Gyro", m.stillnessGyroThreshold, 0.5f, 10f,
                        "Ngưỡng tốc độ góc (độ/giây, đã trừ offset) để coi là đứng yên.\n" +
                        "Quá cao → lần xoay rất chậm của người chơi bị nhầm là drift.");
                    m.stillnessAccelThreshold = S_Slider("Still Accel", m.stillnessAccelThreshold, 0.005f, 0.2f,
                        "Ngưỡng thay đổi gia tốc (g) giữa 2 frame để coi là đứng yên.");
                    if (m.stillnessGyroThreshold > 4f)
                        S_Warning("Still Gyro cao — chuyển động ngắm chậm có thể bị coi là drift.");
                }
            }

            S_Group("Motion › Touchpad Look");
            m.enableTouchpad = S_Toggle("Enable Touch", m.enableTouchpad,
                "Công tắc tổng touchpad (binding <TISMotion>/touchpad*, \"+ Touchpad\", virtual cursor).");
            if (m.enableTouchpad)
                m.touchpadLookSensitivity = S_Vector2("Look Sensitivity", m.touchpadLookSensitivity,
                    "Độ nhạy touchpad cho action \"Look\" (tốc độ vuốt → LookDelta).\n" +
                    "Tốc độ vuốt tính theo bề rộng touchpad / giây. Action khác nhận đơn vị thô.");

            S_Group("Motion › Virtual Cursor");
            m.touchpadMovesCursor = S_Toggle("Touchpad Move", m.touchpadMovesCursor,
                "ON = kéo ngón tay trên touchpad để di chuyển Gamepad Virtual Cursor (giống touchpad laptop).");
            if (m.touchpadMovesCursor)
                m.touchpadCursorSensitivity = S_Slider("Touch Sens.", m.touchpadCursorSensitivity, 0.1f, 5f,
                    "1 = kéo hết chiều ngang touchpad ↔ đi hết chiều ngang màn hình.");
            m.touchpadClickIsLeftClick = S_Toggle("Touch Click", m.touchpadClickIsLeftClick,
                "ON = nhấn (click) touchpad = click chuột trái của virtual cursor.");
            m.gyroMovesCursor = S_Toggle("Gyro Move", m.gyroMovesCursor,
                "ON = xoay tay cầm để di chuyển virtual cursor.\n" +
                "Khi cursor đang bật, gyro tự ngừng xoay camera.");
            if (m.gyroMovesCursor)
                m.gyroCursorPixelsPerDegree = S_Slider("Gyro px/°", m.gyroCursorPixelsPerDegree, 1f, 100f,
                    "Số pixel con trỏ di chuyển khi xoay tay cầm 1 độ.");

            S_Group("Motion › Providers (PC)");
            m.useSdl3OnPC = S_Toggle("Use SDL3", m.useSdl3OnPC,
                "Đọc gyro / touchpad tay cầm thật trên PC qua SDL3.dll.\n" +
                "Chỉ chạy khi profile có binding gyro / touchpad hoặc virtual cursor cần.");
            m.simulateInEditor = S_Toggle("Simulate (Editor)", m.simulateInEditor,
                "Giả lập trong Editor: I/J/K/L/U/O = gyro (Right Shift = nhanh),\n" +
                "giữ T + chuột = touchpad, G = click. Tay cầm thật luôn được ưu tiên.");
            m.showDebugGUI = S_Toggle("Debug GUI", m.showDebugGUI,
                "Bảng gyro / touchpad góc trên-trái (Editor + Development Build).");
        }

        // ── SDL3 status ───────────────────────────────────────

        private const string Sdl3DllName = "SDL3.dll";
        private string _sdl3DllPath;
        private double _sdl3NextCheck;

        private void DrawSdl3Status()
        {
            // PluginImporter.GetAllImporters() không rẻ → chỉ quét lại mỗi 2 giây
            if (EditorApplication.timeSinceStartup >= _sdl3NextCheck)
            {
                _sdl3NextCheck = EditorApplication.timeSinceStartup + 2.0;
                _sdl3DllPath   = null;
                foreach (var imp in PluginImporter.GetAllImporters())
                {
                    if (System.IO.Path.GetFileName(imp.assetPath) == Sdl3DllName)
                    {
                        _sdl3DllPath = imp.assetPath;
                        break;
                    }
                }
            }

            if (_sdl3DllPath != null)
                S_Info("✓  SDL3 sẵn sàng — gyro / touchpad tay PlayStation & Switch trên PC.\n" +
                       "Bind gyro / touchpad ở Cài Đặt Action › Binding Gamepad (<TISMotion>/..., \"+ Gyro\").", C_Green);
            else
                S_Warning("Không thấy SDL3.dll — gyro / touchpad tay cầm trên PC bị tắt.");
        }

        // ── Extra S_ helpers cho Motion ───────────────────────

        /// <summary>Enum popup row: label (80px) + popup.</summary>
        private T S_Enum<T>(string label, T value, string tooltip = "") where T : System.Enum
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            var newVal = (T)EditorGUILayout.EnumPopup(value, EditorStyles.miniPullDown, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) MarkDirty();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            return newVal;
        }

        /// <summary>Popup chọn action theo tên từ profile đang sửa. Giữ nguyên tên lạ (hiện "(không tồn tại)").</summary>
        private string S_ActionPopup(string label, string value, string tooltip = "")
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var map in _workingProfile.actionMaps)
                foreach (var a in map.actions)
                    if (!string.IsNullOrEmpty(a.actionName) && !names.Contains(a.actionName))
                        names.Add(a.actionName);

            int index = names.FindIndex(n => string.Equals(n, value, System.StringComparison.OrdinalIgnoreCase));
            bool missing = index < 0;
            if (missing)
            {
                names.Insert(0, string.IsNullOrEmpty(value) ? "(chưa chọn)" : value + "  (không tồn tại)");
                index = 0;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, tooltip),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(index, names.ToArray(), EditorStyles.miniPullDown,
                GUILayout.ExpandWidth(true));
            bool changed = EditorGUI.EndChangeCheck();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            if (missing)
                S_Warning("Action không có trong profile — gyro sẽ không bật / tắt theo nút.");

            if (!changed || (missing && newIndex == 0)) return value;
            MarkDirty();
            return names[newIndex];
        }

        /// <summary>Info box dạng bar, màu tuỳ chọn.</summary>
        private void S_Info(string msg, Color color)
        {
            GUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 30f);
            EditorGUI.DrawRect(rect, C_BgDeep);
            DrawBorder(rect, color * 0.6f, 1f);
            GUI.Label(rect, "  " + msg,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    normal   = { textColor = color },
                });
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