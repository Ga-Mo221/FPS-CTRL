// Đặt file này vào thư mục Editor (ví dụ: Assets/TrueInputSystem/Editor/)
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem
{
    [CustomEditor(typeof(TISMobileInputSetup))]
    public class TISMobileInputSetupEditor : UnityEditor.Editor
    {
        // ── Tab state ─────────────────────────────────────────────────────────
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Tổng Quan", "Joystick", "Phân Vùng", "Camera", "Runtime" };

        // ── Serialized properties ─────────────────────────────────────────────
        // Preview
        SerializedProperty _previewDoc;
        // Tổng Quan
        SerializedProperty _inputBridge;
        SerializedProperty _canvasSortOrder;
        SerializedProperty _referenceResolution;
        SerializedProperty _matchWidthOrHeight;
        SerializedProperty _joystickPrefab;
        SerializedProperty _cameraZonePrefab;
        // Joystick
        SerializedProperty _joystickBGDiameter;
        SerializedProperty _knobSizeRatio;
        SerializedProperty _spriteNen;
        SerializedProperty _spriteKnob;
        SerializedProperty _mauNen;
        SerializedProperty _mauKnobHienThi;
        // Phân Vùng
        SerializedProperty _batJoystick;
        SerializedProperty _batCamera;
        SerializedProperty _tyLeJoystick;
        // Camera
        // Joystick — Nâng cao
        SerializedProperty _tuyChinhJoystickNangCao;
        SerializedProperty _joystickAnchoredOrigin;
        SerializedProperty _joystickActiveOpacity;
        SerializedProperty _dynamicJoystickOverride;
        SerializedProperty _idleOpacityOverride;

        SerializedProperty _batXoayCamera;
        SerializedProperty _tocDoMuotXoay;
        SerializedProperty _quanTinhXoay;
        SerializedProperty _batZoom;
        SerializedProperty _tocDoZoom;
        SerializedProperty _nguongPinchPx;

        private void OnEnable()
        {
            _previewDoc          = serializedObject.FindProperty("_previewDoc");
            
            if (_previewDoc == null) 
            {
                Debug.LogWarning("[TIS Editor DLL] Không tìm thấy trường _previewDoc ở file Runtime! Kiểm tra lại file DLL Runtime.");
            }
            
            _inputBridge         = serializedObject.FindProperty("_inputBridge");
            _canvasSortOrder     = serializedObject.FindProperty("_canvasSortOrder");
            _referenceResolution = serializedObject.FindProperty("_referenceResolution");
            _matchWidthOrHeight  = serializedObject.FindProperty("_matchWidthOrHeight");
            _joystickPrefab      = serializedObject.FindProperty("_joystickPrefab");
            _cameraZonePrefab    = serializedObject.FindProperty("_cameraZonePrefab");

            _joystickBGDiameter  = serializedObject.FindProperty("_joystickBGDiameter");
            _knobSizeRatio       = serializedObject.FindProperty("_knobSizeRatio");
            _spriteNen           = serializedObject.FindProperty("_spriteNen");
            _spriteKnob          = serializedObject.FindProperty("_spriteKnob");
            _mauNen              = serializedObject.FindProperty("_mauNen");
            _mauKnobHienThi      = serializedObject.FindProperty("_mauKnobHienThi");

            _batJoystick         = serializedObject.FindProperty("_batJoystick");
            _batCamera           = serializedObject.FindProperty("_batCamera");
            _tyLeJoystick        = serializedObject.FindProperty("_tyLeJoystick");

            _tuyChinhJoystickNangCao  = serializedObject.FindProperty("_tuyChinhJoystickNangCao");
            _joystickAnchoredOrigin   = serializedObject.FindProperty("_joystickAnchoredOrigin");
            _joystickActiveOpacity    = serializedObject.FindProperty("_joystickActiveOpacity");
            _dynamicJoystickOverride  = serializedObject.FindProperty("_dynamicJoystickOverride");
            _idleOpacityOverride      = serializedObject.FindProperty("_idleOpacityOverride");

            _batXoayCamera       = serializedObject.FindProperty("_batXoayCamera");
            _tocDoMuotXoay       = serializedObject.FindProperty("_tocDoMuotXoay");
            _quanTinhXoay        = serializedObject.FindProperty("_quanTinhXoay");
            _batZoom             = serializedObject.FindProperty("_batZoom");
            _tocDoZoom           = serializedObject.FindProperty("_tocDoZoom");
            _nguongPinchPx       = serializedObject.FindProperty("_nguongPinchPx");
            
            SetComponentIcon();
        }
        
        private void SetComponentIcon()
        {
            // Tìm file icon theo tên, không cần biết nằm ở folder nào
            string[] guids = AssetDatabase.FindAssets("mobileinput t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Lọc đúng icon của package TrueInputSystem
                if (path.Contains("TrueInputSystem") && path.Contains("Editor/Icon"))
                {
                    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (icon != null)
                    {
                        EditorGUIUtility.SetIconForObject(target, icon);
                        break;
                    }
                }
            }
        }

        // ── Wrapped Toolbar ───────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ toolbar giống GUILayout.Toolbar nhưng tự xuống hàng khi
        /// inspector quá hẹp để chứa tất cả tab trên một hàng.
        /// </summary>
        private int DrawWrappedToolbar(int selected, string[] tabs)
        {
            float viewWidth   = EditorGUIUtility.currentViewWidth - 20f;
            float tabMinWidth = 60f;
            float tabPadding  = 4f;

            int tabsPerRow = Mathf.Max(1, Mathf.FloorToInt(
                (viewWidth + tabPadding) / (tabMinWidth + tabPadding)));

            int tabCount = tabs.Length;
            int rows     = Mathf.CeilToInt((float)tabCount / tabsPerRow);

            for (int row = 0; row < rows; row++)
            {
                int startIdx = row * tabsPerRow;
                int endIdx   = Mathf.Min(startIdx + tabsPerRow, tabCount);
                int count    = endIdx - startIdx;

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = startIdx; i < endIdx; i++)
                    {
                        float tabWidth = (viewWidth - (count - 1) * tabPadding) / count;

                        bool isSelected = (selected == i);
                        bool clicked    = GUILayout.Toggle(
                            isSelected, tabs[i],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(tabWidth), GUILayout.Height(22f));

                        if (clicked && !isSelected)
                            selected = i;

                        if (i < endIdx - 1)
                            GUILayout.Space(tabPadding);
                    }
                }
            }

            return selected;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var setup = (TISMobileInputSetup)target;

            // ── Preview ───────────────────────────────────────────────────────
            SectionTitle("Preview Màn Hình Điện Thoại");

            EditorGUI.BeginChangeCheck();
            bool doc = EditorGUILayout.ToggleLeft("Dọc (9:16) — tắt để xem Ngang (16:9)", _previewDoc.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                _previewDoc.boolValue = doc;
                serializedObject.ApplyModifiedProperties();
            }

            // SỬA TẠI ĐÂY: Gọi hàm DrawPreviewDebug nội bộ của file Editor, truyền setup vào
            DrawPreviewDebug(setup);
    
            EditorGUILayout.Space(4);

            // ── Tabs ──────────────────────────────────────────────────────────
            _selectedTab = DrawWrappedToolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(4);

            // ... (Giữ nguyên phần switch case bên dưới)
            switch (_selectedTab)
            {
                case 0: DrawTabTongQuan(setup); break;
                case 1: DrawTabJoystick(setup); break;
                case 2: DrawTabPhanVung(setup); break;
                case 3: DrawTabCamera(setup);   break;
                case 4: DrawTabRuntime(setup);  break;
            }

            serializedObject.ApplyModifiedProperties();
        }
        
        // Đặt đoạn code này vào bên trong class TISMobileInputSetupEditor
        private void DrawPreviewDebug(TISMobileInputSetup setup)
        {
            // Lấy giá trị từ SerializedProperty _previewDoc đã có trong Editor
            bool doc = _previewDoc.boolValue;

            float phoneW, phoneH;
            if (doc)  { phoneW = 130f; phoneH = 230f; }
            else      { phoneW = 260f; phoneH = 146f; }

            float totalH = phoneH + 46f;
            var   area   = GUILayoutUtility.GetRect(0f, totalH, GUILayout.ExpandWidth(true));
            if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint) return;
            if (area.width < 10f) return;

            float ox = area.x + (area.width - phoneW) * 0.5f;
            float oy = area.y + 8f;

            float refW = doc ? 1080f : 1920f;
            float refH = doc ? 1920f : 1080f;
            float scaleX = phoneW / refW;
            float scaleY = phoneH / refH;
            float scale  = Mathf.Min(scaleX, scaleY);

            EditorGUI.DrawRect(
                new Rect(ox - 4, oy - 4, phoneW + 8, phoneH + 8),
                new Color(0.10f, 0.11f, 0.14f));
            EditorGUI.DrawRect(
                new Rect(ox, oy, phoneW, phoneH),
                new Color(0.05f, 0.07f, 0.10f));

            // Truy cập các thuộc tính/biến của setup thông qua serializedProperty hoặc biến public
            // Giả định các biến này đã được khai báo public/internal trong TISMobileInputSetup
            float tyLeHT = setup._tyLeJoystick; 
            float divX = ox + tyLeHT * phoneW;

            if (setup._batJoystick)
                EditorGUI.DrawRect(
                    new Rect(ox, oy, divX - ox, phoneH),
                    new Color(0.25f, 0.55f, 1f, 0.09f));

            if (setup._batCamera)
                EditorGUI.DrawRect(
                    new Rect(divX, oy, ox + phoneW - divX, phoneH),
                    new Color(0.2f, 0.85f, 0.5f, 0.06f));

            EditorGUI.DrawRect(
                new Rect(divX - 0.75f, oy, 1.5f, phoneH),
                new Color(0.45f, 0.68f, 1f, 0.55f));

            var styleL = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 8,
                alignment = TextAnchor.UpperCenter,
                normal    = { textColor = new Color(0.55f, 0.78f, 1f, 0.85f) }
            };
            var styleR = new GUIStyle(styleL)
            {
                normal = { textColor = new Color(0.3f, 0.9f, 0.62f, 0.85f) }
            };
            GUI.Label(new Rect(ox, oy + 3f, divX - ox, 14f),
                setup._batJoystick ? $"◀  {tyLeHT * 100f:F0}%" : "— OFF —", styleL);
            GUI.Label(new Rect(divX, oy + 3f, ox + phoneW - divX, 14f),
                setup._batCamera ? $"{(1f - tyLeHT) * 100f:F0}%  ▶" : "— OFF —", styleR);

            float halfW  = refW * 0.5f;
            float halfH  = refH * 0.5f;
            float r      = setup._joystickBGDiameter * 0.5f;
            float pad    = r + 20f;

            float zoneRight = -halfW + tyLeHT * refW;
            float joyCanX   = Mathf.Clamp(-halfW + pad, -halfW + pad, zoneRight - pad);
            float joyCanY   = -halfH + pad;

            float joyGUIx = ox + (joyCanX + halfW) * scaleX;
            float joyGUIy = oy + (halfH - joyCanY) * scaleY;

            float bgRadGUI   = r * scale;
            float knobRadGUI = bgRadGUI * setup._knobSizeRatio;

            if (setup._batJoystick)
            {
                VeTronEditor(new Vector2(joyGUIx, joyGUIy), bgRadGUI,
                    new Color(0.4f, 0.65f, 1f, 0.75f), new Color(1f, 1f, 1f, 0.06f));

                VeTronEditor(new Vector2(joyGUIx, joyGUIy), knobRadGUI,
                    new Color(1f, 0.82f, 0.25f, 0.9f), new Color(1f, 1f, 1f, 0.18f));

                float lineY = joyGUIy + bgRadGUI + 5f;
                EditorGUI.DrawRect(
                    new Rect(joyGUIx - bgRadGUI, lineY, bgRadGUI * 2f, 1f),
                    new Color(0.4f, 0.65f, 1f, 0.4f));
                var dimBG = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 8,
                    alignment = TextAnchor.UpperCenter,
                    normal    = { textColor = new Color(0.55f, 0.75f, 1f, 0.85f) }
                };
                GUI.Label(new Rect(joyGUIx - 42f, lineY + 1f, 84f, 14f),
                    $"BG ⌀ {setup._joystickBGDiameter:F0}px", dimBG);

                float lineY2 = lineY + 14f;
                EditorGUI.DrawRect(
                    new Rect(joyGUIx - knobRadGUI, lineY2, knobRadGUI * 2f, 1f),
                    new Color(1f, 0.82f, 0.25f, 0.35f));
                var dimKnob = new GUIStyle(dimBG)
                {
                    normal = { textColor = new Color(1f, 0.82f, 0.25f, 0.85f) }
                };
                GUI.Label(new Rect(joyGUIx - 42f, lineY2 + 1f, 84f, 14f),
                    $"Knob ⌀ {setup._joystickBGDiameter * setup._knobSizeRatio:F0}px", dimKnob);
            }
        }

        private void VeTronEditor(Vector2 center, float r, Color stroke, Color fill, int seg = 52)
        {
            for (int row = -(int)r; row <= (int)r; row++)
            {
                float y  = row;
                float hw = Mathf.Sqrt(Mathf.Max(0f, r * r - y * y));
                EditorGUI.DrawRect(
                    new Rect(center.x - hw, center.y - y - 0.5f, hw * 2f, 1f), fill);
            }
            for (int i = 0; i < seg; i++)
            {
                float a0 = (i       / (float)seg) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)seg) * Mathf.PI * 2f;
                float x0 = center.x + Mathf.Cos(a0) * r,  y0 = center.y - Mathf.Sin(a0) * r;
                float x1 = center.x + Mathf.Cos(a1) * r,  y1 = center.y - Mathf.Sin(a1) * r;
                EditorGUI.DrawRect(
                    new Rect(Mathf.Min(x0,x1) - 0.5f, Mathf.Min(y0,y1) - 0.5f,
                             Mathf.Abs(x1-x0) + 1f,   Mathf.Abs(y1-y0) + 1f),
                    stroke);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: TỔNG QUAN
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabTongQuan(TISMobileInputSetup setup)
        {
            SectionTitle("Tham Chiếu Cốt Lõi");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_inputBridge, new GUIContent("Input Bridge", _inputBridge.tooltip));
            if (GUILayout.Button("Tự Tìm", GUILayout.Width(70)))
            {
                Undo.RecordObject(setup, "Tim Bridge Tu Dong");
                setup.TimBridgeTuDong();
                EditorUtility.SetDirty(setup);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(_canvasSortOrder,     new GUIContent("Canvas Sort Order"));
            EditorGUILayout.PropertyField(_referenceResolution, new GUIContent("Reference Resolution"));
            EditorGUILayout.PropertyField(_matchWidthOrHeight,  new GUIContent("Match Width Or Height"));

            EditorGUILayout.Space(8);
            SectionTitle("Prefab (Tuỳ Chọn)");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_joystickPrefab,   new GUIContent("Joystick Prefab"));
            EditorGUILayout.PropertyField(_cameraZonePrefab, new GUIContent("Camera Zone Prefab"));
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (setup._joystickPrefabAssigned)
                HelpBoxWarning("⚠ Đang dùng Joystick Prefab — cài đặt kích thước và sprite ở tab Joystick sẽ bị bỏ qua.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: JOYSTICK
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabJoystick(TISMobileInputSetup setup)
        {
            bool prefabOn = setup._joystickPrefabAssigned;

            SectionTitle("Kích Thước");
            using (new EditorGUI.DisabledScope(prefabOn))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_joystickBGDiameter, new GUIContent("BG Diameter (px)"));
                EditorGUILayout.PropertyField(_knobSizeRatio,       new GUIContent("Knob Size Ratio"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    setup._onSizeChanged();
                }

                // Progress bar preview — BG
                float bg   = _joystickBGDiameter.floatValue;
                float knob = bg * _knobSizeRatio.floatValue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Nền BG  {bg:F0} px", GUILayout.Width(110));
                Rect rBG = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                DrawProgressBar(rBG, bg / 320f, new Color(0.3f, 0.65f, 1f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Knob  {knob:F0} px", GUILayout.Width(110));
                Rect rKnob = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                DrawProgressBar(rKnob, knob / 320f, new Color(1f, 0.75f, 0.3f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            SectionTitle("Hình Ảnh (Sprite)");
            using (new EditorGUI.DisabledScope(prefabOn))
            {
                // Sprite preview với thumbnail 70px
                DrawSpriteField(_spriteNen,   new GUIContent("Sprite Nền BG"), 70);
                DrawSpriteField(_spriteKnob,  new GUIContent("Sprite Knob"),   70);
            }

            EditorGUILayout.Space(8);
            SectionTitle("Màu Sắc");
            using (new EditorGUI.DisabledScope(prefabOn))
            {
                EditorGUILayout.PropertyField(_mauNen,         new GUIContent("Màu Nền"));
                EditorGUILayout.PropertyField(_mauKnobHienThi, new GUIContent("Màu Knob"));
            }

            EditorGUILayout.Space(8);
            SectionTitle("Joystick — Nâng Cao");

            EditorGUI.BeginChangeCheck();
            _tuyChinhJoystickNangCao.boolValue = EditorGUILayout.ToggleLeft(
                "Bật Tuỳ Chỉnh Nâng Cao", _tuyChinhJoystickNangCao.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                setup._onJoystickAdvancedChanged();
            }

            using (new EditorGUI.DisabledScope(!_tuyChinhJoystickNangCao.boolValue))
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(_joystickAnchoredOrigin,
                    new GUIContent("Vị Trí Joystick (Anchored)", "Toạ độ anchoredPosition của joystick so với canvas."));
                EditorGUILayout.Slider(_joystickActiveOpacity,  0f, 1f,
                    new GUIContent("Active Opacity",  "Độ mờ khi đang chạm joystick."));
                _dynamicJoystickOverride.boolValue = EditorGUILayout.ToggleLeft(
                    "Dynamic Mode", _dynamicJoystickOverride.boolValue);
                EditorGUILayout.Slider(_idleOpacityOverride, 0f, 1f,
                    new GUIContent("Idle Opacity", "Độ mờ khi không chạm joystick."));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    setup._onJoystickAdvancedChanged();
                }
            }

            if (!_tuyChinhJoystickNangCao.boolValue)
                EditorGUILayout.HelpBox(
                    "Bật \"Tuỳ Chỉnh Nâng Cao\" để ghi đè vị trí, opacity và dynamic mode của joystick.",
                    MessageType.None);

            if (prefabOn)
                HelpBoxWarning("Đang dùng Joystick Prefab — các cài đặt trên bị bỏ qua.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: PHÂN VÙNG
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabPhanVung(TISMobileInputSetup setup)
        {
            SectionTitle("Bật / Tắt Thành Phần");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            _batJoystick.boolValue = EditorGUILayout.ToggleLeft("Virtual Joystick", _batJoystick.boolValue, GUILayout.Width(160));
            _batCamera.boolValue   = EditorGUILayout.ToggleLeft("Camera Zone",      _batCamera.boolValue);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                setup._onToggleJoystickCamera();
            }

            if (!_batJoystick.boolValue && !_batCamera.boolValue)
                HelpBoxWarning("⚠ Cả Joystick lẫn Camera đều đang tắt — màn hình sẽ không có vùng tương tác nào.");

            bool bothOn = _batJoystick.boolValue && _batCamera.boolValue;

            if (bothOn)
            {
                EditorGUILayout.Space(6);
                SectionTitle("Tỉ Lệ Joystick / Camera");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_tyLeJoystick, new GUIContent("Tỉ Lệ Joystick (0.25–0.75)"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    setup._onRatioChanged();
                }

                float ty = setup._tyLeJoystickHienThi;
                EditorGUILayout.LabelField($"◀  Joystick  {ty * 100f:F0}%        |        Camera  {(1f - ty) * 100f:F0}%  ▶",
                    EditorStyles.centeredGreyMiniLabel);

                // Progress bars
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Joystick", GUILayout.Width(60));
                Rect rJ = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                DrawProgressBar(rJ, ty, new Color(0.3f, 0.72f, 1f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Camera", GUILayout.Width(60));
                Rect rC = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                DrawProgressBar(rC, 1f - ty, new Color(0.2f, 0.55f, 0.95f));
                EditorGUILayout.EndHorizontal();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: CAMERA
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabCamera(TISMobileInputSetup setup)
        {
            SectionTitle("Xoay Camera (Pan)");

            EditorGUI.BeginChangeCheck();
            _batXoayCamera.boolValue = EditorGUILayout.ToggleLeft("Bật Xoay Camera", _batXoayCamera.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                setup._onCameraChanged();
            }

            using (new EditorGUI.DisabledScope(!_batXoayCamera.boolValue))
            {
                EditorGUILayout.PropertyField(_tocDoMuotXoay, new GUIContent("Tốc Độ Mượt Xoay"));
                EditorGUILayout.PropertyField(_quanTinhXoay,  new GUIContent("Quán Tính Xoay"));
            }

            EditorGUILayout.Space(8);
            SectionTitle("Thu Phóng Pinch (Zoom)");

            EditorGUI.BeginChangeCheck();
            _batZoom.boolValue = EditorGUILayout.ToggleLeft("Bật Zoom", _batZoom.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                setup._onCameraChanged();
            }

            using (new EditorGUI.DisabledScope(!_batZoom.boolValue))
            {
                EditorGUILayout.PropertyField(_tocDoZoom,    new GUIContent("Tốc Độ Zoom"));
                EditorGUILayout.PropertyField(_nguongPinchPx, new GUIContent("Ngưỡng Pinch (px)"));
            }

            if (!_batXoayCamera.boolValue && !_batZoom.boolValue)
                HelpBoxWarning("⚠ Cả Xoay Camera lẫn Zoom đều đang tắt. TISTouchCameraZone sẽ bị vô hiệu hoá.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB: RUNTIME
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabRuntime(TISMobileInputSetup setup)
        {
            SectionTitle("Công Cụ Lúc Chạy");

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Các nút bên dưới chỉ hoạt động khi đang Play Mode.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Bật/Tắt Xoay Camera")) setup.BatTatXoayCamera();
                if (GUILayout.Button("Bật/Tắt Zoom"))        setup.BatTatZoom();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Bật/Tắt Joystick"))    setup.BatTatJoystick();
                if (GUILayout.Button("Bật/Tắt Camera Zone")) setup.BatTatCameraZone();
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f);
                if (GUILayout.Button("Đồng Bộ Tỉ Lệ Vùng")) setup.DongBoTyLe();
                GUI.backgroundColor = Color.white;

                if (Application.isPlaying)
                {
                    EditorGUILayout.Space(8);
                    SectionTitle("Trạng Thái Hiện Tại");

                    ReadOnlyField("Xoay Camera",   setup._batXoayCamera  ? "✓ Đang bật" : "✗ Đang tắt");
                    ReadOnlyField("Zoom",          setup._batZoom         ? "✓ Đang bật" : "✗ Đang tắt");
                    ReadOnlyField("Joystick",      setup._batJoystick    ? "✓ Đang bật" : "✗ Đang tắt");
                    ReadOnlyField("Camera Zone",   setup._batCamera       ? "✓ Đang bật" : "✗ Đang tắt");
                    float ty = setup._tyLeJoystickHienThi;
                    ReadOnlyField("Vùng Hiệu Dụng", $"Joystick {ty * 100f:F0}%  |  Camera {(1f - ty) * 100f:F0}%");
                    ReadOnlyField("Joystick Input", setup.Joystick != null
                        ? $"✓ RawInput = {setup.Joystick.RawInput:F2}"
                        : "— Chưa khởi tạo");
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static void SectionTitle(string label)
        {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            EditorGUILayout.LabelField(label, style);
            Rect r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(2);
        }

        private static void HelpBoxWarning(string msg) =>
            EditorGUILayout.HelpBox(msg, MessageType.Warning);

        private static void ReadOnlyField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            EditorGUILayout.LabelField(value, EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawProgressBar(Rect rect, float t, Color col)
        {
            t = Mathf.Clamp01(t);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            var fill = new Rect(rect.x, rect.y, rect.width * t, rect.height);
            EditorGUI.DrawRect(fill, col);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.25f)); // subtle border overlay
        }

        /// <summary>Sprite field với thumbnail preview 70px bên trái label.</summary>
        private static void DrawSpriteField(SerializedProperty prop, GUIContent label, float thumbSize)
        {
            EditorGUILayout.BeginHorizontal();
            var sprite = (Sprite)prop.objectReferenceValue;
            if (sprite != null)
            {
                Rect thumbRect = GUILayoutUtility.GetRect(thumbSize, thumbSize,
                    GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                EditorGUI.DrawTextureTransparent(thumbRect, sprite.texture);
            }
            else
            {
                GUILayout.Space(thumbSize + 4);
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Space(thumbSize * 0.25f);
            EditorGUILayout.PropertyField(prop, label);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif