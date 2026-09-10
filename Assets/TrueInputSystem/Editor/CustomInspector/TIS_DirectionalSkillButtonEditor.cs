// ============================================================
//  TrueInputSystem — TIS_DirectionalSkillButtonEditor.cs
//
//  Custom Editor cho TISDirectionalSkillButton.
//  File này chỉ được biên dịch trong Unity Editor (không vào build).
//
//  THAY ĐỔI: Binding Tab dùng dropdown chọn action từ
//  TISInputProfileConfig thay vì gõ tay string skillName.
//  Direction Key cũng có dropdown (tất cả action trong profile)
//  với option "Auto" đầu tiên → dùng skillName + "_Dir".
// ============================================================

#if UNITY_EDITOR
namespace TrueInputSystem
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(TISDirectionalSkillButton))]
    public class TISDirectionalSkillButtonEditor : UnityEditor.Editor
    {
        // ── Tab state ─────────────────────────────────────────
        private int _tab = 0;
        private static readonly string[] _tabs = { "Binding", "Behaviour", "Visual", "Events", "Debug" };

        private int _visualSubTab = 0;
        private static readonly string[] _visualSubTabs = { "Button", "Indicator", "Cancel Zone" };

        // ── Serialized properties ─────────────────────────────

        // Binding
        private SerializedProperty _skillName;
        private SerializedProperty _directionActionName;
        private SerializedProperty _OnTouchCamera;

        // Behaviour
        private SerializedProperty _castMode;
        private SerializedProperty _dragThresholdPx;
        private SerializedProperty _maxDragRadiusPx;
        private SerializedProperty _allowQuickTap;
        private SerializedProperty _quickTapMaxSeconds;

        // Visual — Button
        private SerializedProperty _buttonVisualMode;
        private SerializedProperty _normalColor;
        private SerializedProperty _normalSprite;
        private SerializedProperty _draggingColor;
        private SerializedProperty _draggingSprite;
        private SerializedProperty _castColor;
        private SerializedProperty _castSprite;
        private SerializedProperty _disabledColor;
        private SerializedProperty _disabledSprite;
        private SerializedProperty _castFlashDuration;
        private SerializedProperty _cooldownFill;
        private SerializedProperty _rangeCircleImage;

        // Visual — Indicator
        private SerializedProperty _indicatorVisualMode;
        private SerializedProperty _indicatorStyle;
        private SerializedProperty _indicatorRoot;
        private SerializedProperty _indicatorSprite;
        private SerializedProperty _indicatorLength;
        private SerializedProperty _indicatorWidth;
        private SerializedProperty _indicatorColor;
        private SerializedProperty _indicatorDraggingSprite;
        private SerializedProperty _indicatorReadyColor;
        private SerializedProperty _indicatorReadySprite;
        private SerializedProperty _knobSize;

        // Cancel Zone
        private SerializedProperty _cancelZoneVisualMode;
        private SerializedProperty _cancelZone;
        private SerializedProperty _cancelZoneImage;
        private SerializedProperty _cancelZoneNormalColor;
        private SerializedProperty _cancelZoneNormalSprite;
        private SerializedProperty _cancelZoneActiveColor;
        private SerializedProperty _cancelZoneActiveSprite;
        private SerializedProperty _indicatorCancelColor;
        private SerializedProperty _indicatorCancelSprite;

        // Events
        private SerializedProperty _onDragStarted;
        private SerializedProperty _onDirectionChanged;
        private SerializedProperty _onCast;
        private SerializedProperty _onDirectionCast;
        private SerializedProperty _onCancelled;

        // ── Dropdown state ────────────────────────────────────
        private TISInputProfileConfig _linkedProfile;

        // Skill Name dropdown — action có bindingMobile
        private string[] _actionNames   = System.Array.Empty<string>();
        private int      _selectedIndex = -1;

        // Direction Key dropdown — tất cả action + entry [0] = Auto
        // Entry [0] luôn là AUTO_ENTRY (→ _directionActionName = "")
        private string[] _dirKeyDisplayNames = System.Array.Empty<string>(); // hiển thị trong popup
        private string[] _dirKeyValues       = System.Array.Empty<string>(); // giá trị ghi vào prop
        private int      _dirKeyIndex        = 0;
        private const    string AUTO_LABEL   = "— Auto  (skillName + \"_Dir\") —";

        private string   _profilePrefKey;

        // ── Styles ────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _badgeStyle;
        private bool     _stylesInitialized;

        // ── Colors ────────────────────────────────────────────
        private static readonly Color _accentColor  = new Color(0.25f, 0.70f, 0.95f);
        private static readonly Color _successColor = new Color(0.30f, 0.85f, 0.50f);
        private static readonly Color _warningColor = new Color(1.00f, 0.75f, 0.20f);
        private static readonly Color _dangerColor  = new Color(1.00f, 0.35f, 0.35f);
        private static readonly Color _cancelColor  = new Color(1.00f, 0.30f, 0.30f);

        // ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            _skillName           = serializedObject.FindProperty("_skillName");
            _directionActionName = serializedObject.FindProperty("_directionActionName");
            _OnTouchCamera       = serializedObject.FindProperty("_OnTouchCamera");

            _castMode           = serializedObject.FindProperty("_castMode");
            _dragThresholdPx    = serializedObject.FindProperty("_dragThresholdPx");
            _maxDragRadiusPx    = serializedObject.FindProperty("_maxDragRadiusPx");
            _allowQuickTap      = serializedObject.FindProperty("_allowQuickTap");
            _quickTapMaxSeconds = serializedObject.FindProperty("_quickTapMaxSeconds");

            _buttonVisualMode  = serializedObject.FindProperty("_buttonVisualMode");
            _normalColor       = serializedObject.FindProperty("_normalColor");
            _normalSprite      = serializedObject.FindProperty("_normalSprite");
            _draggingColor     = serializedObject.FindProperty("_draggingColor");
            _draggingSprite    = serializedObject.FindProperty("_draggingSprite");
            _castColor         = serializedObject.FindProperty("_castColor");
            _castSprite        = serializedObject.FindProperty("_castSprite");
            _disabledColor     = serializedObject.FindProperty("_disabledColor");
            _disabledSprite    = serializedObject.FindProperty("_disabledSprite");
            _castFlashDuration = serializedObject.FindProperty("_castFlashDuration");
            _cooldownFill      = serializedObject.FindProperty("_cooldownFill");
            _rangeCircleImage  = serializedObject.FindProperty("_rangeCircleImage");

            _indicatorVisualMode     = serializedObject.FindProperty("_indicatorVisualMode");
            _indicatorStyle          = serializedObject.FindProperty("_indicatorStyle");
            _indicatorRoot           = serializedObject.FindProperty("_indicatorRoot");
            _indicatorSprite         = serializedObject.FindProperty("_indicatorSprite");
            _indicatorLength         = serializedObject.FindProperty("_indicatorLength");
            _indicatorWidth          = serializedObject.FindProperty("_indicatorWidth");
            _indicatorColor          = serializedObject.FindProperty("_indicatorColor");
            _indicatorDraggingSprite = serializedObject.FindProperty("_indicatorDraggingSprite");
            _indicatorReadyColor     = serializedObject.FindProperty("_indicatorReadyColor");
            _indicatorReadySprite    = serializedObject.FindProperty("_indicatorReadySprite");
            _knobSize                = serializedObject.FindProperty("_knobSize");

            _onDragStarted      = serializedObject.FindProperty("OnDragStarted");
            _onDirectionChanged = serializedObject.FindProperty("OnDirectionChanged");
            _onCast             = serializedObject.FindProperty("OnCast");
            _onDirectionCast    = serializedObject.FindProperty("OnDirectionCast");
            _onCancelled        = serializedObject.FindProperty("OnCancelled");

            _cancelZoneVisualMode    = serializedObject.FindProperty("_cancelZoneVisualMode");
            _cancelZone              = serializedObject.FindProperty("_cancelZone");
            _cancelZoneImage         = serializedObject.FindProperty("_cancelZoneImage");
            _cancelZoneNormalColor   = serializedObject.FindProperty("_cancelZoneNormalColor");
            _cancelZoneNormalSprite  = serializedObject.FindProperty("_cancelZoneNormalSprite");
            _cancelZoneActiveColor   = serializedObject.FindProperty("_cancelZoneActiveColor");
            _cancelZoneActiveSprite  = serializedObject.FindProperty("_cancelZoneActiveSprite");
            _indicatorCancelColor    = serializedObject.FindProperty("_indicatorCancelColor");
            _indicatorCancelSprite   = serializedObject.FindProperty("_indicatorCancelSprite");

            // ── Khởi tạo dropdown ──────────────────────────────
            _profilePrefKey = $"TIS_DirBtn_{target.GetInstanceID()}_ProfileGUID";
            RestoreLinkedProfileFromPrefs();

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            string[] guids = AssetDatabase.FindAssets("directionbuttton t:Texture2D");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
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

        // ── Styles init ───────────────────────────────────────
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleLeft,
            };

            _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize      = 10,
                alignment     = TextAnchor.MiddleCenter,
                normal        = { textColor = Color.white },
                fontStyle     = FontStyle.Bold,
                padding       = new RectOffset(6, 6, 2, 2),
            };

            _stylesInitialized = true;
        }

        // ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            string key = string.IsNullOrEmpty(_directionActionName.stringValue)
                ? _skillName.stringValue + "_Dir"
                : _directionActionName.stringValue;

            DrawButtonHeader(key);
            EditorGUILayout.Space(4);

            _tab = DrawWrappedToolbar(_tab, _tabs);
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawBindingTab(key);  break;
                case 1: DrawBehaviourTab();   break;
                case 2: DrawVisualTab();      break;
                case 3: DrawEventsTab();      break;
                case 4: DrawDebugTab(key);    break;
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
            float viewWidth   = EditorGUIUtility.currentViewWidth - 20f; // trừ padding
            float tabMinWidth = 60f;   // chiều rộng tối thiểu mỗi tab
            float tabPadding  = 4f;    // khoảng cách giữa các tab

            // Tính số tab vừa trên một hàng
            int tabsPerRow = Mathf.Max(1, Mathf.FloorToInt(
                (viewWidth + tabPadding) / (tabMinWidth + tabPadding)));

            // Chia tabs thành các nhóm hàng
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
                        // Phân bổ đều chiều rộng cho các tab trong hàng
                        float tabWidth = (viewWidth - (count - 1) * tabPadding) / count;

                        bool isSelected = (selected == i);
                        bool clicked    = GUILayout.Toggle(
                            isSelected, tabs[i],
                            isSelected ? EditorStyles.toolbarButton : EditorStyles.toolbarButton,
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

        private void DrawButtonHeader(string key)
        {
            var btn = (TISDirectionalSkillButton)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("TIS Directional Skill Button", EditorStyles.boldLabel,
                    GUILayout.ExpandWidth(true));

                if (Application.isPlaying)
                {
                    if (btn.IsInCancelZone)
                        DrawColorBadge("CANCELLING", _cancelColor);
                    else if (btn.IsDragging)
                        DrawColorBadge("DRAGGING", _warningColor);
                    else if (btn.WasCastThisFrame)
                        DrawColorBadge("CAST!", _successColor);
                    else if (!btn.Interactable)
                        DrawColorBadge("DISABLED", _dangerColor);
                    else
                        DrawColorBadge("READY", _accentColor);
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
        //  TAB: BINDING
        // ══════════════════════════════════════════════════════

        private void DrawBindingTab(string key)
        {
            DrawSectionLabel("Skill Action");

            // ── Profile picker ────────────────────────────────
            EditorGUI.BeginChangeCheck();
            var newProfile = (TISInputProfileConfig)EditorGUILayout.ObjectField(
                new GUIContent("Profile",
                    "Chọn TISInputProfileConfig để lấy danh sách action name.\n" +
                    "Không ảnh hưởng runtime — chỉ dùng để chọn tên action ở Editor."),
                _linkedProfile,
                typeof(TISInputProfileConfig),
                allowSceneObjects: false);

            if (EditorGUI.EndChangeCheck())
                SetLinkedProfile(newProfile);

            // ── Skill Name: dropdown hoặc text field ──────────
            if (_linkedProfile != null && _actionNames.Length > 0)
            {
                string currentName = _skillName.stringValue;
                int syncedIndex    = System.Array.IndexOf(_actionNames, currentName);

                if (syncedIndex < 0 && !string.IsNullOrEmpty(currentName))
                {
                    DrawHelpBox(
                        $"Action \"{currentName}\" không tồn tại trong profile.\n" +
                        "Chọn lại từ dropdown hoặc cập nhật profile.",
                        MessageType.Warning);
                    syncedIndex = 0;
                }

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(
                    new GUIContent("Skill Name",
                        "Chọn action name từ TISInputProfileConfig.\n" +
                        "GetSkillAction(skillName) trả về true đúng 1 frame khi cast."),
                    Mathf.Max(syncedIndex, 0),
                    _actionNames);

                if (EditorGUI.EndChangeCheck())
                {
                    _selectedIndex         = newIndex;
                    _skillName.stringValue = _actionNames[newIndex];
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUILayout.PropertyField(_skillName,
                    new GUIContent("Skill Name",
                        "Gõ tay tên action (fallback khi chưa chọn Profile).\n" +
                        "Phải khớp chính xác với actionName trong TISInputProfileConfig."));

                if (_linkedProfile == null)
                    DrawHelpBox(
                        "Chọn một Profile bên trên để dùng dropdown thay cho gõ tay.\n" +
                        "Tránh lỗi typo và luôn đồng bộ với config.",
                        MessageType.Info);
                else
                    DrawHelpBox(
                        "Profile này chưa có action nào có Mobile Binding.\n" +
                        "Thêm bindingMobile vào TISInputActionDefinition trong profile.",
                        MessageType.Warning);
            }

            // ── Direction Key ─────────────────────────────────
            EditorGUILayout.Space(6);
            DrawSectionLabel("Direction Key");

            if (_linkedProfile != null && _dirKeyDisplayNames.Length > 0)
            {
                // Sync index với _directionActionName hiện tại
                string currentDirKey = _directionActionName.stringValue;
                int syncedDirIndex;

                if (string.IsNullOrEmpty(currentDirKey))
                {
                    // Đang ở Auto mode → index 0
                    syncedDirIndex = 0;
                }
                else
                {
                    // Tìm trong _dirKeyValues (bắt đầu từ index 1 vì 0 = Auto)
                    syncedDirIndex = System.Array.IndexOf(_dirKeyValues, currentDirKey);
                    if (syncedDirIndex < 0)
                    {
                        // Tên hiện tại không có trong profile → cảnh báo + fallback Auto
                        DrawHelpBox(
                            $"Direction Key \"{currentDirKey}\" không tồn tại trong profile.\n" +
                            "Chọn lại từ dropdown hoặc giữ Auto.",
                            MessageType.Warning);
                        syncedDirIndex = 0;
                    }
                }

                EditorGUI.BeginChangeCheck();
                int newDirIndex = EditorGUILayout.Popup(
                    new GUIContent("Direction Key",
                        "Chọn action name làm key lưu vào TISDirectionalRegistry.\n" +
                        "Auto → tự dùng skillName + \"_Dir\" (khuyên dùng nếu không cần override)."),
                    syncedDirIndex,
                    _dirKeyDisplayNames);

                if (EditorGUI.EndChangeCheck())
                {
                    _dirKeyIndex = newDirIndex;
                    // index 0 = Auto → ghi chuỗi rỗng
                    _directionActionName.stringValue = newDirIndex == 0 ? "" : _dirKeyValues[newDirIndex];
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                // Fallback text field khi chưa có profile
                EditorGUILayout.PropertyField(_directionActionName,
                    new GUIContent("Direction Key",
                        "Key đọc từ TISDirectionalRegistry.GetDirection(key).\n" +
                        "Để trống = tự dùng skillName + \"_Dir\"."));
            }

            // Effective key (read-only preview) — luôn hiển thị
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(
                new GUIContent("Effective Key",
                    "Key thực tế đang dùng để ghi/đọc direction registry."),
                $"\"{key}\"");
            EditorGUI.EndDisabledGroup();

            // ── Touch Routing ─────────────────────────────────
            EditorGUILayout.Space(6);
            DrawSectionLabel("Touch Routing");

            EditorGUILayout.PropertyField(_OnTouchCamera,
                new GUIContent("Pass Touch to Camera",
                    "OFF = button block touch, camera zone không nhận ngón này.\n" +
                    "ON  = touch xuyên qua xuống camera zone."));

            // ── Usage hint ────────────────────────────────────
            string sn = _skillName.stringValue;
            DrawHelpBox(
                "Game script đọc hướng:\n" +
                $"  Vector2 dir = TISDirectionalRegistry.GetDirection(\"{key}\");\n\n" +
                "Hoặc check cast + direction:\n" +
                $"  if (TISMobileButtonRegistry.WasPressedThisFrame(\"{sn}\"))\n" +
                $"      dir = TISDirectionalRegistry.GetDirection(\"{key}\");",
                MessageType.Info);
        }

        // ══════════════════════════════════════════════════════
        //  TAB: BEHAVIOUR
        // ══════════════════════════════════════════════════════

        private void DrawBehaviourTab()
        {
            DrawSectionLabel("Cast Mode");

            EditorGUILayout.PropertyField(_castMode,
                new GUIContent("Cast Mode",
                    "DragRelease: nhả tay mới cast (chuẩn Liên Quân / Tốc Chiến).\n" +
                    "Always: luôn cast khi nhả, kể cả tap."));

            EditorGUILayout.Space(6);
            DrawSectionLabel("Drag");

            EditorGUILayout.PropertyField(_dragThresholdPx,
                new GUIContent("Drag Threshold (px)",
                    "Khoảng cách pixel tối thiểu phải kéo mới tính là có hướng."));
            EditorGUILayout.PropertyField(_maxDragRadiusPx,
                new GUIContent("Max Drag Radius (px)",
                    "Giới hạn khoảng cách kéo từ tâm button. 0 = không giới hạn."));

            if (_maxDragRadiusPx.floatValue == 0f)
                DrawHelpBox("Max Drag Radius = 0: ngón tay có thể kéo ra không giới hạn.", MessageType.Info);

            EditorGUILayout.Space(6);
            DrawSectionLabel("Quick Tap");

            EditorGUILayout.PropertyField(_allowQuickTap,
                new GUIContent("Allow Quick Tap",
                    "Bật → tap nhanh cast ngay với direction = zero (auto-target)."));

            if (_allowQuickTap.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_quickTapMaxSeconds,
                    new GUIContent("Max Duration (s)",
                        "Thời gian tối đa để nhận là 'tap nhanh'."));
                EditorGUI.indentLevel--;
            }
            else
            {
                DrawHelpBox("Quick Tap tắt: phải kéo đủ threshold mới cast được.", MessageType.Warning);
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: VISUAL
        // ══════════════════════════════════════════════════════

        private void DrawVisualTab()
        {
            EditorGUI.BeginChangeCheck();
            _visualSubTab = GUILayout.Toolbar(_visualSubTab, _visualSubTabs,
                EditorStyles.toolbarButton, GUILayout.Height(22f));
            EditorGUILayout.Space(6);

            switch (_visualSubTab)
            {
                case 0: DrawVisualSubTab_Button();     break;
                case 1: DrawVisualSubTab_Indicator();  break;
                case 2: DrawVisualSubTab_CancelZone(); break;
            }
        }

        // ── Sub-tab: Button ───────────────────────────────────
        private void DrawVisualSubTab_Button()
        {
            DrawSectionLabel("Button States");

            EditorGUILayout.PropertyField(_buttonVisualMode,
                new GUIContent("Visual Mode",
                    "ColorTint: đổi màu Image, giữ sprite gốc.\n" +
                    "SpriteSwap: đổi sprite theo state, color luôn = white."));

            var bMode      = (TISDirectionalSkillButton.VisualMode)_buttonVisualMode.enumValueIndex;
            bool showColor  = bMode == TISDirectionalSkillButton.VisualMode.ColorTint;
            bool showSprite = bMode == TISDirectionalSkillButton.VisualMode.SpriteSwap;

            EditorGUILayout.Space(4);

            if (showColor)
                EditorGUILayout.HelpBox(
                    "ColorTint: mỗi state chỉ đổi màu tint. Sprite gốc trên Image không đổi.",
                    MessageType.None);
            else
                EditorGUILayout.HelpBox(
                    "SpriteSwap: mỗi state đổi Sprite. Image.color luôn = white (không tint).",
                    MessageType.None);

            EditorGUILayout.Space(2);

            // Column headers
            using (new EditorGUILayout.HorizontalScope())
            {
                float totalW  = EditorGUIUtility.currentViewWidth - 20f;
                float labelW  = totalW * 0.35f;
                float colorW  = totalW * 0.30f;
                float spriteW = totalW * 0.35f;

                EditorGUILayout.LabelField("State", EditorStyles.boldLabel, GUILayout.Width(labelW));
                if (showColor)
                    EditorGUILayout.LabelField("Color",  EditorStyles.boldLabel, GUILayout.Width(colorW));
                if (showSprite)
                    EditorGUILayout.LabelField("Sprite", EditorStyles.boldLabel, GUILayout.Width(spriteW));
            }

            DrawButtonStateRow("Normal",     _normalColor,   _normalSprite,   showColor, showSprite);
            DrawButtonStateRow("Dragging",   _draggingColor, _draggingSprite, showColor, showSprite);
            DrawButtonStateRow("Cast Flash", _castColor,     _castSprite,     showColor, showSprite);
            DrawButtonStateRow("Disabled",   _disabledColor, _disabledSprite, showColor, showSprite);

            EditorGUILayout.Space(6);
            DrawSectionLabel("Timing");
            EditorGUILayout.PropertyField(_castFlashDuration, new GUIContent("Flash Duration (s)",
                "Thời gian giữ màu/sprite cast trước khi về Normal."));

            EditorGUILayout.Space(6);
            DrawSectionLabel("UI References");
            EditorGUILayout.PropertyField(_cooldownFill,     new GUIContent("Cooldown Fill Image",
                "Image hiển thị fill cooldown (Type = Filled). Để null nếu không cần."));
            EditorGUILayout.PropertyField(_rangeCircleImage, new GUIContent("Range Circle Image",
                "Image vòng tròn tầm kỹ năng — hiện khi đang kéo, ẩn khi nhả."));
        }

        private void DrawButtonStateRow(string label,
            SerializedProperty colorProp, SerializedProperty spriteProp,
            bool showColor, bool showSprite)
        {
            EditorGUILayout.Space(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                float totalW = EditorGUIUtility.currentViewWidth - 20f;
                float labelW = totalW * 0.35f;
                float fieldW = totalW * 0.60f;

                EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

                if (showColor)
                {
                    colorProp.colorValue = EditorGUI.ColorField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        colorProp.colorValue,
                        showEyedropper: true, showAlpha: true, hdr: false);
                }
                else if (showSprite)
                {
                    spriteProp.objectReferenceValue = EditorGUI.ObjectField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        spriteProp.objectReferenceValue,
                        typeof(Sprite), false);
                }
            }
            EditorGUILayout.Space(1);
        }

        // ── Sub-tab: Indicator ────────────────────────────────
        private void DrawVisualSubTab_Indicator()
        {
            DrawSectionLabel("Style & Layout");

            EditorGUILayout.PropertyField(_indicatorStyle, new GUIContent("Style",
                "Arrow = mũi tên xoay.\nLine = đường thẳng.\nKnob = dot di chuyển theo ngón tay.\nNone = ẩn."));

            var style = (TISDirectionalSkillButton.IndicatorStyle)_indicatorStyle.enumValueIndex;

            if (style == TISDirectionalSkillButton.IndicatorStyle.None)
            {
                DrawHelpBox("Indicator ẩn: người chơi sẽ không thấy hướng đang chọn.", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(_indicatorVisualMode,
                new GUIContent("Visual Mode",
                    "ColorTint: đổi màu indicator theo state.\n" +
                    "SpriteSwap: đổi sprite, color luôn = white."));

            var iMode       = (TISDirectionalSkillButton.VisualMode)_indicatorVisualMode.enumValueIndex;
            bool showColor  = iMode == TISDirectionalSkillButton.VisualMode.ColorTint;
            bool showSprite = iMode == TISDirectionalSkillButton.VisualMode.SpriteSwap;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_indicatorRoot, new GUIContent("Custom Root (optional)",
                "Để null → tự tạo indicator.\nAssign GameObject có Image để dùng sprite custom."));
            EditorGUILayout.PropertyField(_indicatorSprite, new GUIContent("Default Sprite (optional)",
                "Sprite mặc định của indicator.\nĐể null → solid color."));

            if (style == TISDirectionalSkillButton.IndicatorStyle.Knob)
                EditorGUILayout.PropertyField(_knobSize, new GUIContent("Knob Size (px)",
                    "Đường kính dot tròn di chuyển theo ngón tay."));
            else
            {
                EditorGUILayout.PropertyField(_indicatorLength, new GUIContent("Length (px)",
                    "Độ dài indicator từ tâm button."));
                EditorGUILayout.PropertyField(_indicatorWidth, new GUIContent("Width (px)"));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(6);
            DrawSectionLabel("Indicator States");

            if (showColor)
                EditorGUILayout.HelpBox("ColorTint: chỉ đổi màu tint theo state.", MessageType.None);
            else
                EditorGUILayout.HelpBox("SpriteSwap: chỉ đổi sprite theo state, color = white.", MessageType.None);

            DrawIndicatorStateRow("Dragging (below threshold)", _indicatorColor,      _indicatorDraggingSprite, showColor, showSprite);
            DrawIndicatorStateRow("Ready (threshold reached)",  _indicatorReadyColor, _indicatorReadySprite,    showColor, showSprite);
        }

        private void DrawIndicatorStateRow(string label,
            SerializedProperty colorProp, SerializedProperty spriteProp,
            bool showColor, bool showSprite)
        {
            EditorGUILayout.Space(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                float totalW = EditorGUIUtility.currentViewWidth - 20f;
                float labelW = totalW * 0.45f;
                float fieldW = totalW * 0.50f;

                EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

                if (showColor)
                {
                    colorProp.colorValue = EditorGUI.ColorField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        colorProp.colorValue,
                        showEyedropper: true, showAlpha: true, hdr: false);
                }
                else if (showSprite)
                {
                    spriteProp.objectReferenceValue = EditorGUI.ObjectField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        spriteProp.objectReferenceValue,
                        typeof(Sprite), false);
                }
            }
            EditorGUILayout.Space(1);
        }

        // ── Sub-tab: Cancel Zone ──────────────────────────────
        private void DrawVisualSubTab_CancelZone()
        {
            DrawSectionLabel("Cancel Zone Setup");

            EditorGUILayout.PropertyField(_cancelZone,
                new GUIContent("Cancel Zone (RectTransform)",
                    "Vùng UI hủy kỹ năng kiểu Liên Quân.\n" +
                    "Kéo ngón tay vào đây → indicator đỏ + cancel zone active.\n" +
                    "Nhả tay trong vùng → hủy, không cast.\n" +
                    "Để null = tắt tính năng hủy."));

            if (_cancelZone.objectReferenceValue == null)
            {
                DrawHelpBox(
                    "Assign Cancel Zone để bật tính năng hủy kỹ năng kiểu Liên Quân.\n" +
                    "Ví dụ: một Image hình X đặt giữa màn hình, SetActive(false) ban đầu.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(_cancelZoneImage, new GUIContent("Cancel Zone Image",
                "Image để đổi màu/sprite khi hover/active.\nĐể null = chỉ detect vùng, không đổi visual."));

            EditorGUILayout.PropertyField(_cancelZoneVisualMode,
                new GUIContent("Visual Mode",
                    "ColorTint: đổi màu cancel zone theo state.\n" +
                    "SpriteSwap: đổi sprite, color luôn = white."));

            var czMode      = (TISDirectionalSkillButton.VisualMode)_cancelZoneVisualMode.enumValueIndex;
            bool showColor  = czMode == TISDirectionalSkillButton.VisualMode.ColorTint;
            bool showSprite = czMode == TISDirectionalSkillButton.VisualMode.SpriteSwap;

            DrawHelpBox(
                "Script tự SetActive(true) khi bắt đầu hold, SetActive(false) khi nhả.\n" +
                "Cancel Zone nên để SetActive(false) ban đầu trong Hierarchy.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            DrawSectionLabel("Cancel Zone States");

            if (showColor)
                EditorGUILayout.HelpBox("ColorTint: chỉ đổi màu tint. Sprite gốc không đổi.", MessageType.None);
            else
                EditorGUILayout.HelpBox("SpriteSwap: chỉ đổi sprite. Image.color luôn = white.", MessageType.None);

            DrawCancelStateRow("Normal (idle)",     _cancelZoneNormalColor, _cancelZoneNormalSprite, showColor, showSprite);
            DrawCancelStateRow("Active (hovering)", _cancelZoneActiveColor, _cancelZoneActiveSprite, showColor, showSprite);

            EditorGUILayout.Space(6);
            DrawSectionLabel("Indicator Cancel State");

            var iMode        = (TISDirectionalSkillButton.VisualMode)_indicatorVisualMode.enumValueIndex;
            bool iShowColor  = iMode == TISDirectionalSkillButton.VisualMode.ColorTint;
            bool iShowSprite = iMode == TISDirectionalSkillButton.VisualMode.SpriteSwap;

            EditorGUILayout.HelpBox(
                "State này dùng Indicator Visual Mode (tab Indicator) để quyết định Color hay Sprite.",
                MessageType.None);

            DrawCancelStateRow("Indicator (cancel)", _indicatorCancelColor, _indicatorCancelSprite, iShowColor, iShowSprite);
        }

        private void DrawCancelStateRow(string label,
            SerializedProperty colorProp, SerializedProperty spriteProp,
            bool showColor, bool showSprite)
        {
            EditorGUILayout.Space(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                float totalW = EditorGUIUtility.currentViewWidth - 20f;
                float labelW = totalW * 0.40f;
                float fieldW = totalW * 0.55f;

                EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

                if (showColor)
                {
                    colorProp.colorValue = EditorGUI.ColorField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        colorProp.colorValue,
                        showEyedropper: true, showAlpha: true, hdr: false);
                }
                else if (showSprite)
                {
                    spriteProp.objectReferenceValue = EditorGUI.ObjectField(
                        GUILayoutUtility.GetRect(fieldW, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(fieldW)),
                        GUIContent.none,
                        spriteProp.objectReferenceValue,
                        typeof(Sprite), false);
                }
            }
            EditorGUILayout.Space(1);
        }

        // ══════════════════════════════════════════════════════
        //  TAB: EVENTS
        // ══════════════════════════════════════════════════════

        private void DrawEventsTab()
        {
            DrawSectionLabel("UnityEvents");

            EditorGUILayout.PropertyField(_onDragStarted,
                new GUIContent("On Drag Started",
                    "Gọi 1 lần khi bắt đầu kéo vượt dragThreshold."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onDirectionChanged,
                new GUIContent("On Direction Changed (Vector2)",
                    "Gọi mỗi frame khi đang kéo. Param = hướng hiện tại (normalized)."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onCast,
                new GUIContent("On Cast (Vector2)",
                    "Gọi đúng frame khi cast. Param = hướng cast (zero = quick tap)."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onDirectionCast,
                new GUIContent("On Direction Cast (string, Vector2)",
                    "Gọi khi cast — kèm tên skill.\nDùng khi 1 handler xử lý nhiều directional button."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onCancelled,
                new GUIContent("On Cancelled",
                    "Gọi khi người dùng kéo vào Cancel Zone và nhả tay.\n" +
                    "Không kèm tham số — chỉ báo hiệu kỹ năng bị hủy."));
        }

        // ══════════════════════════════════════════════════════
        //  TAB: DEBUG
        // ══════════════════════════════════════════════════════

        private void DrawDebugTab(string key)
        {
            if (!Application.isPlaying)
            {
                DrawHelpBox("Dữ liệu runtime chỉ hiển thị khi đang Play Mode.", MessageType.Info);
                return;
            }

            var btn = (TISDirectionalSkillButton)target;

            DrawSectionLabel("Runtime State");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("IsDragging");
                GUI.color = btn.IsDragging ? _warningColor : Color.gray;
                EditorGUILayout.LabelField(btn.IsDragging ? "true" : "false", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("IsInCancelZone");
                GUI.color = btn.IsInCancelZone ? _cancelColor : Color.gray;
                EditorGUILayout.LabelField(btn.IsInCancelZone ? "true — will CANCEL" : "false", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("WasCastThisFrame");
                GUI.color = btn.WasCastThisFrame ? _successColor : Color.gray;
                EditorGUILayout.LabelField(btn.WasCastThisFrame ? "true" : "false", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.Space(4);
            DrawSectionLabel("Direction");

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2Field("Direction (live)", btn.Direction);
                EditorGUILayout.Vector2Field("CastDirection",    btn.CastDirection);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Direction Angle");
                bool hasDir = btn.Direction.sqrMagnitude > 0.001f;
                GUI.color = hasDir ? _accentColor : Color.gray;
                EditorGUILayout.LabelField(hasDir ? $"{btn.DirectionAngle:F1}°" : "—", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Registry Dir");
                GUI.color = _accentColor * 0.8f;
                EditorGUILayout.LabelField(
                    TISDirectionalRegistry.GetDirection(key).ToString("F3"), EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            DrawHelpBox(
                $"Đọc direction trong game script:\n" +
                $"  TISDirectionalRegistry.GetDirection(\"{key}\")",
                MessageType.None);

            Repaint();
        }

        // ══════════════════════════════════════════════════════
        //  DROPDOWN HELPERS
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Gán profile mới, rebuild danh sách action names, lưu GUID vào EditorPrefs.
        /// Chỉ lấy các action có bindingMobile không rỗng.
        /// </summary>
        private void SetLinkedProfile(TISInputProfileConfig profile)
        {
            _linkedProfile = profile;

            if (profile == null)
            {
                _actionNames        = System.Array.Empty<string>();
                _dirKeyDisplayNames = System.Array.Empty<string>();
                _dirKeyValues       = System.Array.Empty<string>();
                _selectedIndex      = -1;
                _dirKeyIndex        = 0;
                EditorPrefs.DeleteKey(_profilePrefKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(profile);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(_profilePrefKey, guid);

            RebuildActionNames();
        }

        /// <summary>
        /// Lấy toàn bộ action name trong profile:
        ///   · _actionNames     — action có bindingMobile  (cho Skill Name dropdown)
        ///   · _dirKeyValues    — tất cả action            (cho Direction Key dropdown, index 0 = Auto)
        ///   · _dirKeyDisplayNames — nhãn hiển thị tương ứng
        /// </summary>
        private void RebuildActionNames()
        {
            if (_linkedProfile == null)
            {
                _actionNames        = System.Array.Empty<string>();
                _dirKeyDisplayNames = System.Array.Empty<string>();
                _dirKeyValues       = System.Array.Empty<string>();
                _selectedIndex      = -1;
                _dirKeyIndex        = 0;
                return;
            }

            // ── Skill Name: chỉ action có bindingMobile ────────
            var names = new List<string>();
            foreach (var action in _linkedProfile.GetAllActions())
            {
                if (!string.IsNullOrEmpty(action.bindingMobile))
                    names.Add(action.actionName);
            }
            _actionNames   = names.ToArray();
            string current = _skillName?.stringValue ?? "";
            _selectedIndex = System.Array.IndexOf(_actionNames, current);

            // ── Direction Key: tất cả action + Auto ở đầu ──────
            var dirDisplays = new List<string> { AUTO_LABEL };
            var dirValues   = new List<string> { "" };          // "" = Auto
            foreach (var action in _linkedProfile.GetAllActions())
            {
                dirDisplays.Add(action.actionName);
                dirValues.Add(action.actionName);
            }
            _dirKeyDisplayNames = dirDisplays.ToArray();
            _dirKeyValues       = dirValues.ToArray();

            // Sync _dirKeyIndex với _directionActionName hiện tại
            string currentDir = _directionActionName?.stringValue ?? "";
            _dirKeyIndex = string.IsNullOrEmpty(currentDir)
                ? 0
                : System.Array.IndexOf(_dirKeyValues, currentDir);
            if (_dirKeyIndex < 0) _dirKeyIndex = 0; // fallback Auto nếu không tìm thấy
        }

        /// <summary>
        /// Sau recompile/OnEnable: khôi phục profile từ GUID lưu trong EditorPrefs.
        /// </summary>
        private void RestoreLinkedProfileFromPrefs()
        {
            string guid = EditorPrefs.GetString(_profilePrefKey, "");
            if (string.IsNullOrEmpty(guid))
            {
                TryAutoDetectProfile();
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                _linkedProfile = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(path);

            RebuildActionNames();
        }

        /// <summary>
        /// Nếu trong project chỉ có đúng 1 TISInputProfileConfig, tự động chọn nó.
        /// </summary>
        private void TryAutoDetectProfile()
        {
            string[] guids = AssetDatabase.FindAssets("t:TISInputProfileConfig");
            if (guids.Length == 1)
            {
                string path    = AssetDatabase.GUIDToAssetPath(guids[0]);
                var    profile = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(path);
                if (profile != null)
                    SetLinkedProfile(profile);
            }
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

        private void DrawColorSpriteRow(string label,
            SerializedProperty colorProp, SerializedProperty spriteProp,
            string colorTooltip = "", string spriteTooltip = "")
        {
            EditorGUILayout.Space(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                float totalW  = EditorGUIUtility.currentViewWidth - 20f;
                float labelW  = totalW * 0.35f;
                float colorW  = totalW * 0.30f;
                float spriteW = totalW * 0.35f;

                EditorGUILayout.LabelField(new GUIContent(label), GUILayout.Width(labelW));

                colorProp.colorValue = EditorGUI.ColorField(
                    GUILayoutUtility.GetRect(colorW, EditorGUIUtility.singleLineHeight,
                        GUILayout.Width(colorW)),
                    new GUIContent("", colorTooltip),
                    colorProp.colorValue,
                    showEyedropper: true, showAlpha: true, hdr: false);

                GUILayout.Space(4f);

                spriteProp.objectReferenceValue = EditorGUI.ObjectField(
                    GUILayoutUtility.GetRect(spriteW, EditorGUIUtility.singleLineHeight,
                        GUILayout.Width(spriteW)),
                    new GUIContent("", spriteTooltip),
                    spriteProp.objectReferenceValue,
                    typeof(Sprite), false);
            }
            EditorGUILayout.Space(1);
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
    }
}
#endif