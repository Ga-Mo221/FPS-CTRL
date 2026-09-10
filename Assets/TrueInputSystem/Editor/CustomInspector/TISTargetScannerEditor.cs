// ============================================================
//  TrueInputSystem — TISTargetScannerEditor.cs
//
//  Custom Inspector cho TISTargetScanner.
//  Đặt file này trong thư mục Editor/ bất kỳ trong project.
//  Ví dụ: Assets/TrueInputSystem/Editor/TISTargetScannerEditor.cs
// ============================================================

using UnityEngine;
using UnityEditor;

namespace TrueInputSystem
{
    [CustomEditor(typeof(TISTargetScanner))]
    public class TISTargetScannerEditor : UnityEditor.Editor
    {
        // ── Tab state ────────────────────────────────────────
        private int _tab = 0;
        private static readonly string[] _tabs = { "Filter", "Scan", "Aim Point", "Debug" };

        // ── Serialized properties ─────────────────────────────

        // Filter
        private SerializedProperty _useLayerInsteadOfTag;
        private SerializedProperty _targetTags;
        private SerializedProperty _targetLayers;

        // Scan
        private SerializedProperty _maxDistance;
        private SerializedProperty _scanAngle;
        private SerializedProperty _scanInterval;
        private SerializedProperty _requireLineOfSight;
        private SerializedProperty _obstacleLayers;

        // Aim Point
        private SerializedProperty _aimPointMode;
        private SerializedProperty _aimPointOffset;

        // ── Styles ────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _badgeStyle;

        private bool _stylesInitialized;
        
        private SerializedProperty _showDebugOverlay;
        private SerializedProperty _overlayPosition;

        // ── Colors ────────────────────────────────────────────
        private static readonly Color _accentColor   = new Color(0.25f, 0.70f, 0.95f);
        private static readonly Color _warningColor  = new Color(1.00f, 0.75f, 0.20f);
        private static readonly Color _successColor  = new Color(0.30f, 0.85f, 0.50f);
        private static readonly Color _dangerColor   = new Color(1.00f, 0.35f, 0.35f);

        // ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Guard: target có thể null nếu component bị missing hoặc
            // Unity gọi OnEnable trước khi hoàn tất khởi tạo Editor.
            if (target == null) return;

            // Filter
            _useLayerInsteadOfTag = serializedObject.FindProperty("_useLayerInsteadOfTag");
            _targetTags           = serializedObject.FindProperty("_targetTags");
            _targetLayers         = serializedObject.FindProperty("_targetLayers");

            // Scan
            _maxDistance          = serializedObject.FindProperty("_maxDistance");
            _scanAngle            = serializedObject.FindProperty("_scanAngle");
            _scanInterval         = serializedObject.FindProperty("_scanInterval");
            _requireLineOfSight   = serializedObject.FindProperty("_requireLineOfSight");
            _obstacleLayers       = serializedObject.FindProperty("_obstacleLayers");

            // Aim Point
            _aimPointMode         = serializedObject.FindProperty("_aimPointMode");
            _aimPointOffset       = serializedObject.FindProperty("_aimPointOffset");
            
            _showDebugOverlay = serializedObject.FindProperty("_showDebugOverlay");
            _overlayPosition  = serializedObject.FindProperty("_overlayPosition");
            
            SetComponentIcon();
        }
        
        private void SetComponentIcon()
        {
            // Tìm file icon theo tên, không cần biết nằm ở folder nào
            string[] guids = AssetDatabase.FindAssets("targetDetec t:Texture2D");
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

        // ── Styles init (delayed — requires GUISkin) ──────────
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleLeft,
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
            };

            _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize        = 10,
                alignment       = TextAnchor.MiddleCenter,
                normal          = { textColor = Color.white },
                fontStyle       = FontStyle.Bold,
                padding         = new RectOffset(6, 6, 2, 2),
            };

            _stylesInitialized = true;
        }

        // ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            if (target == null) return;

            InitStyles();
            serializedObject.Update();

            DrawScannerHeader();
            EditorGUILayout.Space(4);

            _tab = DrawWrappedToolbar(_tab, _tabs);
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawFilterTab();   break;
                case 1: DrawScanTab();     break;
                case 2: DrawAimPointTab(); break;
                case 3: DrawDebugTab();    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════════════════════
        //  WRAPPED TOOLBAR
        // ══════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════
        //  HEADER
        // ══════════════════════════════════════════════════════

        private void DrawScannerHeader()
        {
            var scanner = (TISTargetScanner)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("TIS Target Scanner", EditorStyles.boldLabel,
                    GUILayout.ExpandWidth(true));

                // Badge: trạng thái runtime
                if (Application.isPlaying)
                {
                    bool hasTarget = scanner.CurrentTarget != null;
                    Color badgeCol = hasTarget ? _successColor : _dangerColor;
                    string badgeText = hasTarget
                        ? "[O] LOCKED"
                        : "[x] NO TARGET";

                    DrawColorBadge(badgeText, badgeCol);
                }
                else
                {
                    DrawColorBadge("EDIT MODE", _accentColor * 0.7f);
                }
            }

            // Divider
            var lineRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        // ══════════════════════════════════════════════════════
        //  TAB: FILTER
        // ══════════════════════════════════════════════════════

        private void DrawFilterTab()
        {
            DrawSectionLabel("Target Detection Mode");

            EditorGUILayout.PropertyField(_useLayerInsteadOfTag,
                new GUIContent("Use Layer Mask",
                    "ON = quét theo Layer (OverlapSphere, hiệu suất tốt hơn)\n" +
                    "OFF = quét theo Tag list (FindGameObjectsWithTag)"));

            EditorGUILayout.Space(6);

            bool useLayer = _useLayerInsteadOfTag.boolValue;

            if (!useLayer)
            {
                // ── Tag mode ──────────────────────────────────
                DrawSectionLabel("Tag List");
                EditorGUILayout.PropertyField(_targetTags, new GUIContent("Target Tags"),
                    includeChildren: true);

                if (_targetTags.arraySize == 0)
                    DrawHelpBox("Chưa có tag nào. Thêm ít nhất 1 tag.", MessageType.Warning);
            }
            else
            {
                // ── Layer mode ────────────────────────────────
                DrawSectionLabel("Layer Mask");
                EditorGUILayout.PropertyField(_targetLayers,
                    new GUIContent("Target Layers",
                        "Layer mask chứa các GameObject cần tìm. Dùng OverlapSphere — hiệu suất tốt hơn FindGameObjectsWithTag khi có nhiều target."));

                if (_targetLayers.intValue == 0)
                    DrawHelpBox("Layer mask = Nothing. Sẽ không tìm thấy target nào.", MessageType.Warning);
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: SCAN
        // ══════════════════════════════════════════════════════

        private void DrawScanTab()
        {
            DrawSectionLabel("Range");

            EditorGUILayout.PropertyField(_maxDistance,
                new GUIContent("Max Distance (m)",
                    "Khoảng cách tối đa để xét target. 0 = không giới hạn."));

            if (_maxDistance.floatValue == 0f)
                DrawHelpBox("Max Distance = 0: quét toàn bộ scene — có thể ảnh hưởng hiệu suất.", MessageType.Info);

            EditorGUILayout.PropertyField(_scanAngle,
                new GUIContent("Scan Angle (°)",
                    "Góc nửa nón từ tâm màn hình. Nên ≤ AimAssist.SnapRadius."));

            EditorGUILayout.Space(6);
            DrawSectionLabel("Timing");

            EditorGUILayout.PropertyField(_scanInterval,
                new GUIContent("Scan Interval (s)",
                    "0 = quét mỗi frame. Giá trị 0.1~0.2 là đủ cho gameplay thông thường."));

            if (_scanInterval.floatValue == 0f)
                DrawHelpBox("Scan mỗi frame: phù hợp với ít target. Nếu scene có nhiều object, dùng 0.05~0.1s.", MessageType.Info);

            EditorGUILayout.Space(6);
            DrawSectionLabel("Line of Sight");

            EditorGUILayout.PropertyField(_requireLineOfSight,
                new GUIContent("Require Line of Sight",
                    "Bật để Raycast kiểm tra vật cản trước khi lock target."));

            // ── Chỉ hiện obstacle layers khi LoS bật ─────────
            if (_requireLineOfSight.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_obstacleLayers,
                    new GUIContent("Obstacle Layers",
                        "Layer nào được coi là vật cản. Mặc định: Everything (~0)."));
                EditorGUI.indentLevel--;
            }
            else
            {
                DrawHelpBox("Line of Sight tắt: target sau tường vẫn bị lock.", MessageType.Warning);
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: AIM POINT
        // ══════════════════════════════════════════════════════

        private void DrawAimPointTab()
        {
            DrawSectionLabel("Aim Point Mode");

            EditorGUILayout.PropertyField(_aimPointMode,
                new GUIContent("Mode",
                    "Center       = gốc Transform\n" +
                    "ColliderCenter = tâm Bounds Collider (khuyên dùng)\n" +
                    "Custom       = offset tùy chỉnh trong Local Space"));

            EditorGUILayout.Space(6);

            var mode = (TISTargetScanner.AimPointMode)_aimPointMode.intValue;

            switch (mode)
            {
                case TISTargetScanner.AimPointMode.Center:
                    DrawHelpBox("Dùng Transform.position — nhanh nhất, nhưng có thể không chính xác nếu pivot của model không ở tâm.", MessageType.Info);
                    break;

                case TISTargetScanner.AimPointMode.ColliderCenter:
                    DrawHelpBox("Dùng Collider.bounds.center — chính xác hơn Center, tự động bù pivot lệch.", MessageType.Info);
                    break;

                case TISTargetScanner.AimPointMode.Custom:
                    DrawSectionLabel("Custom Offset (Local Space)");
                    EditorGUILayout.PropertyField(_aimPointOffset,
                        new GUIContent("Aim Point Offset",
                            "Offset tính theo Local Space của target. (0, 1.4, 0) thường là vùng đầu/ngực nhân vật."));

                    DrawHelpBox("Offset được tính theo Local Space của target — xoay theo target.", MessageType.Info);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: DEBUG
        // ══════════════════════════════════════════════════════

        private void DrawDebugTab()
        {
            DrawSectionLabel("Game View Overlay");

            EditorGUILayout.PropertyField(_showDebugOverlay,
                new GUIContent("Show Overlay",
                    "Hiện thông tin target và vòng tròn phạm vi lên Game View."));

            if (_showDebugOverlay.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_overlayPosition,
                    new GUIContent("Position (px)", "Góc trên trái = (10, 10)."));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            DrawGizmoNote();

            // ── Runtime state chỉ khi Play ────────────────────────────
            if (!Application.isPlaying)
            {
                DrawHelpBox("Dữ liệu runtime chỉ hiển thị khi đang Play Mode.", MessageType.Info);
                return;
            }

            var scanner = (TISTargetScanner)target;

            DrawSectionLabel("Runtime State");

            // ── Current Target ────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Current Target");
                if (scanner.CurrentTarget != null)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField(scanner.CurrentTarget, typeof(Transform), true);
                }
                else
                {
                    GUI.color = _dangerColor;
                    EditorGUILayout.LabelField("— no target —", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
            }

            // ── Aim Point ─────────────────────────────────────
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Aim Point (World)",
                    scanner.CurrentTarget != null ? scanner.CurrentAimPoint : Vector3.zero);
            }

            // ── Angle ─────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Angle to Target");
                string angleStr = scanner.CurrentAngle >= 0f
                    ? $"{scanner.CurrentAngle:F2}°"
                    : "—";

                Color angleCol = scanner.CurrentAngle < 0f   ? Color.gray
                               : scanner.CurrentAngle < 3f   ? _successColor
                               : scanner.CurrentAngle < 8f   ? _warningColor
                               : _dangerColor;

                GUI.color = angleCol;
                EditorGUILayout.LabelField(angleStr, EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(8);

            // ── Buttons ───────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Scan", GUILayout.Height(26)))
                    scanner.ForceScan();

                GUI.backgroundColor = _dangerColor;
                if (GUILayout.Button("Clear Target", GUILayout.Height(26)))
                    scanner.ClearTarget();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(4);
            
            DrawSectionLabel("Game View Overlay");

            EditorGUILayout.PropertyField(_showDebugOverlay,
                new GUIContent("Show Overlay",
                    "Hiện thông tin target lên Game View khi Play Mode."));

            if (_showDebugOverlay.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_overlayPosition,
                    new GUIContent("Position (px)", "Góc trên trái = (10, 10)."));
                EditorGUI.indentLevel--;
            }
            
            DrawGizmoNote();

            // Repaint liên tục khi play để cập nhật dữ liệu realtime
            Repaint();
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════

        private void DrawSectionLabel(string label)
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 14f, rect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.2f));
            EditorGUI.LabelField(rect, label, _headerStyle);
            EditorGUILayout.Space(2);
        }

        private static void DrawHelpBox(string msg, MessageType type)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(msg, type);
            EditorGUILayout.Space(2);
        }

        private void DrawColorBadge(string text, Color color)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, _badgeStyle,
                GUILayout.Height(18f), GUILayout.MinWidth(80f));
            GUI.backgroundColor = prevBg;
        }

        private static void DrawGizmoNote()
        {
            EditorGUILayout.HelpBox(
                "Gizmo: Chọn object trong Scene để thấy vòng nón Scan Angle (cyan) và đường thẳng tới CurrentTarget (xanh lá).",
                MessageType.None);
        }
        
        [DrawGizmo(GizmoType.Selected)]
        static void DrawGizmos(TISTargetScanner scanner, GizmoType gizmoType)
        {
            // Lấy các field private qua reflection vì chúng nằm trong Runtime DLL
            var fieldMaxDist  = typeof(TISTargetScanner).GetField("_maxDistance",  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldScanAngle = typeof(TISTargetScanner).GetField("_scanAngle",   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            float maxDistance = (float)(fieldMaxDist?.GetValue(scanner)  ?? 50f);
            float scanAngle   = (float)(fieldScanAngle?.GetValue(scanner) ?? 10f);

            float coneLen    = maxDistance > 0f ? Mathf.Min(maxDistance, 20f) : 20f;
            float coneRadius = Mathf.Tan(scanAngle * Mathf.Deg2Rad) * coneLen;

            Gizmos.color  = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.matrix = Matrix4x4.TRS(scanner.transform.position, scanner.transform.rotation, Vector3.one);
            Gizmos.DrawWireSphere(Vector3.forward * coneLen, coneRadius);
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawRay(scanner.transform.position, scanner.transform.forward * coneLen);

            // Vẽ đường tới target đang lock
            if (scanner.CurrentTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(scanner.transform.position, scanner.CurrentAimPoint);
                Gizmos.DrawWireSphere(scanner.CurrentAimPoint, 0.15f);
            }
        }
    }
}