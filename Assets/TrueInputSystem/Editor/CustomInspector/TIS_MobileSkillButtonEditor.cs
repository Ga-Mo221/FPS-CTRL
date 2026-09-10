// ============================================================
//  TrueInputSystem — TIS_MobileSkillButtonEditor.cs  (Editor)
//
//  Custom Inspector cho TISMobileSkillButton.
//  Dùng tab system (Binding / Visual / Cooldown / Events / Debug)
//  nhất quán với TISDirectionalSkillButtonEditor.
//
//  THAY ĐỔI: Binding Tab dùng dropdown chọn action từ
//  TISInputProfileConfig thay vì gõ tay string skillName.
//
//  File này chỉ được biên dịch trong Unity Editor.
//  Đặt trong thư mục Editor/ để Unity nhận đúng.
// ============================================================

#if UNITY_EDITOR
namespace TrueInputSystem
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UI;

    [CustomEditor(typeof(TISMobileSkillButton))]
    public class TISMobileSkillButtonEditor : UnityEditor.Editor
    {
        // ── Tab state ─────────────────────────────────────────
        private int _tab = 0;
        private static readonly string[] _tabs = { "Binding", "Visual", "Cooldown", "Events", "Debug" };

        // ── Serialized properties ─────────────────────────────

        // Binding
        private SerializedProperty _skillName;
        private SerializedProperty _interactable;
        private SerializedProperty _OnTouchCamera;

        // Visual
        private SerializedProperty _visualMode;
        private SerializedProperty _flashDuration;

        // ColorTint
        private SerializedProperty _normalColor;
        private SerializedProperty _pressedColor;
        private SerializedProperty _disabledColor;
        private SerializedProperty _notInteractableColor;

        // SpriteSwap
        private SerializedProperty _spriteTintColor;
        private SerializedProperty _normalSprite;
        private SerializedProperty _pressedSprite;
        private SerializedProperty _disabledSprite;
        private SerializedProperty _notInteractableSprite;
        private SerializedProperty _disabledAlpha;
        private SerializedProperty _notInteractableAlpha;

        // Cooldown & Events
        private SerializedProperty _cooldownFill;
        private SerializedProperty _onPress;
        private SerializedProperty _onRelease;
        private SerializedProperty _onHold;
        private SerializedProperty _onHoldTimed;

        // ── Dropdown state ────────────────────────────────────

        /// <summary>
        /// Profile được chọn trong Inspector để lấy danh sách action name.
        /// Lưu bằng EditorPrefs theo instanceID của target để persist qua recompile.
        /// </summary>
        private TISInputProfileConfig _linkedProfile;
        private string[] _actionNames    = System.Array.Empty<string>();
        private int      _selectedIndex  = -1;

        // Key lưu EditorPrefs: "TIS_SkillBtn_{instanceID}_ProfileGUID"
        private string _profilePrefKey;

        // ── Styles ────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _badgeStyle;
        private bool     _stylesInitialized;

        // ── Colors ────────────────────────────────────────────
        private static readonly Color _accentColor  = new Color(0.25f, 0.70f, 0.95f);
        private static readonly Color _successColor = new Color(0.30f, 0.85f, 0.50f);
        private static readonly Color _warningColor = new Color(1.00f, 0.75f, 0.20f);
        private static readonly Color _dangerColor  = new Color(1.00f, 0.35f, 0.35f);

        // ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            _skillName             = serializedObject.FindProperty("_skillName");
            _interactable          = serializedObject.FindProperty("_interactable");
            _OnTouchCamera         = serializedObject.FindProperty("_OnTouchCamera");

            _visualMode            = serializedObject.FindProperty("_visualMode");
            _flashDuration         = serializedObject.FindProperty("_flashDuration");

            _normalColor           = serializedObject.FindProperty("_normalColor");
            _pressedColor          = serializedObject.FindProperty("_pressedColor");
            _disabledColor         = serializedObject.FindProperty("_disabledColor");
            _notInteractableColor  = serializedObject.FindProperty("_notInteractableColor");

            _spriteTintColor         = serializedObject.FindProperty("_spriteTintColor");
            _normalSprite            = serializedObject.FindProperty("_normalSprite");
            _pressedSprite           = serializedObject.FindProperty("_pressedSprite");
            _disabledSprite          = serializedObject.FindProperty("_disabledSprite");
            _notInteractableSprite   = serializedObject.FindProperty("_notInteractableSprite");
            _disabledAlpha           = serializedObject.FindProperty("_disabledAlpha");
            _notInteractableAlpha    = serializedObject.FindProperty("_notInteractableAlpha");

            _cooldownFill  = serializedObject.FindProperty("_cooldownFill");
            _onPress       = serializedObject.FindProperty("OnPress");
            _onRelease     = serializedObject.FindProperty("OnRelease");
            _onHold        = serializedObject.FindProperty("OnHold");
            _onHoldTimed   = serializedObject.FindProperty("OnHoldTimed");

            // ── Khởi tạo dropdown ──────────────────────────────
            _profilePrefKey = $"TIS_SkillBtn_{target.GetInstanceID()}_ProfileGUID";
            RestoreLinkedProfileFromPrefs();

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            string[] guids = AssetDatabase.FindAssets("button t:Texture2D");
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
                fontSize  = 10,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
                padding   = new RectOffset(6, 6, 2, 2),
            };

            _stylesInitialized = true;
        }

        // ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            DrawButtonHeader();
            EditorGUILayout.Space(4);

            _tab = DrawWrappedToolbar(_tab, _tabs);
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawBindingTab();  break;
                case 1: DrawVisualTab();   break;
                case 2: DrawCooldownTab(); break;
                case 3: DrawEventsTab();   break;
                case 4: DrawDebugTab();    break;
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

        private void DrawButtonHeader()
        {
            var btn = (TISMobileSkillButton)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("TIS Mobile Skill Button", EditorStyles.boldLabel,
                    GUILayout.ExpandWidth(true));

                if (Application.isPlaying)
                {
                    if (!btn.Interactable)
                        DrawColorBadge("DISABLED", _dangerColor);
                    else if (btn.HoldTime > 0f)
                        DrawColorBadge($"HOLD {btn.HoldTime:F1}s", _warningColor);
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

        private void DrawBindingTab()
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
            {
                SetLinkedProfile(newProfile);
            }

            // ── Dropdown hoặc text field ──────────────────────
            if (_linkedProfile != null && _actionNames.Length > 0)
            {
                // Sync index với giá trị hiện tại của _skillName
                string currentName = _skillName.stringValue;
                int syncedIndex = System.Array.IndexOf(_actionNames, currentName);

                // Nếu tên hiện tại không có trong list → hiển thị cảnh báo
                bool nameNotFound = syncedIndex < 0 && !string.IsNullOrEmpty(currentName);
                if (nameNotFound)
                {
                    DrawHelpBox(
                        $"Action \"{currentName}\" không tồn tại trong profile.\n" +
                        "Chọn lại từ dropdown hoặc cập nhật profile.",
                        MessageType.Warning);
                    syncedIndex = 0; // fallback để dropdown không crash
                }

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(
                    new GUIContent("Skill Name",
                        "Chọn action name từ TISInputProfileConfig.\n" +
                        "Phải khớp với bindingMobile hoặc actionName trong profile."),
                    Mathf.Max(syncedIndex, 0),
                    _actionNames);

                if (EditorGUI.EndChangeCheck())
                {
                    _selectedIndex = newIndex;
                    _skillName.stringValue = _actionNames[newIndex];
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                // Fallback: gõ tay như cũ khi chưa chọn profile
                EditorGUILayout.PropertyField(_skillName,
                    new GUIContent("Skill Name",
                        "Gõ tay tên action (fallback khi chưa chọn Profile).\n" +
                        "Phải khớp chính xác với actionName trong TISInputProfileConfig."));

                if (_linkedProfile == null)
                {
                    DrawHelpBox(
                        "Chọn một Profile bên trên để dùng dropdown thay cho gõ tay.\n" +
                        "Tránh lỗi typo và luôn đồng bộ với config.",
                        MessageType.Info);
                }
                else
                {
                    // Có profile nhưng không có action nào có bindingMobile
                    DrawHelpBox(
                        "Profile này chưa có action nào có Mobile Binding.\n" +
                        "Thêm bindingMobile vào TISInputActionDefinition trong profile.",
                        MessageType.Warning);
                }
            }

            // ── Interactable & Touch Routing ──────────────────
            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_interactable,
                new GUIContent("Interactable",
                    "false → không nhận touch và hiển thị visual Not Interactable.\n" +
                    "Ưu tiên cao hơn SetDisabled() và cooldown."));

            EditorGUILayout.Space(6);
            DrawSectionLabel("Touch Routing");

            EditorGUILayout.PropertyField(_OnTouchCamera,
                new GUIContent("Pass Touch to Camera",
                    "OFF = button block touch, camera zone không nhận ngón này.\n" +
                    "ON  = touch xuyên qua xuống camera zone."));

            // ── Usage hint ────────────────────────────────────
            string skillNameDisplay = _skillName.stringValue;
            DrawHelpBox(
                "Game script đọc trạng thái:\n" +
                $"  TISMobileButtonRegistry.WasPressedThisFrame(\"{skillNameDisplay}\")\n\n" +
                "Hold time:\n" +
                "  btn.HoldTime  // giây đã giữ",
                MessageType.Info);
        }

        // ══════════════════════════════════════════════════════
        //  TAB: VISUAL
        // ══════════════════════════════════════════════════════

        private void DrawVisualTab()
        {
            DrawSectionLabel("Visual Mode");

            EditorGUILayout.PropertyField(_visualMode,
                new GUIContent("Mode",
                    "ColorTint  — đổi màu tint theo trạng thái.\n" +
                    "SpriteSwap — đổi Sprite theo trạng thái, màu tint cố định."));

            bool isColorTint = (TISMobileSkillButton.VisualMode)_visualMode.enumValueIndex
                               == TISMobileSkillButton.VisualMode.ColorTint;

            EditorGUILayout.Space(6);

            if (isColorTint)
            {
                DrawSectionLabel("Color Tint");

                EditorGUILayout.PropertyField(_flashDuration,
                    new GUIContent("Flash Duration (s)",
                        "Thời gian flash màu _pressedColor khi nhấn."));

                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(_normalColor,
                    new GUIContent("Normal",          "Màu khi idle."));
                EditorGUILayout.PropertyField(_pressedColor,
                    new GUIContent("Pressed",         "Màu khi đang nhấn."));
                EditorGUILayout.PropertyField(_disabledColor,
                    new GUIContent("Disabled",        "Màu khi SetDisabled(true)."));
                EditorGUILayout.PropertyField(_notInteractableColor,
                    new GUIContent("Not Interactable","Màu khi Interactable = false."));
            }
            else
            {
                DrawSectionLabel("Sprite Swap");

                EditorGUILayout.PropertyField(_spriteTintColor,
                    new GUIContent("Tint Color",
                        "Màu tint cố định — thường để White để sprite nguyên màu."));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_normalSprite,          new GUIContent("Normal"));
                EditorGUILayout.PropertyField(_pressedSprite,         new GUIContent("Pressed"));
                EditorGUILayout.PropertyField(_disabledSprite,
                    new GUIContent("Disabled",
                        "Để null → dùng Normal + alpha _disabledAlpha."));
                EditorGUILayout.PropertyField(_notInteractableSprite,
                    new GUIContent("Not Interactable",
                        "Để null → dùng Normal + alpha _notInteractableAlpha."));
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Alpha Fallback (khi không có sprite riêng)", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(_disabledAlpha,
                        new GUIContent("Disabled",         "Alpha fallback khi _disabledSprite = null."));
                    EditorGUILayout.PropertyField(_notInteractableAlpha,
                        new GUIContent("Not Interactable", "Alpha fallback khi _notInteractableSprite = null."));
                }
                EditorGUI.indentLevel--;
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: COOLDOWN
        // ══════════════════════════════════════════════════════

        private void DrawCooldownTab()
        {
            DrawSectionLabel("Cooldown Fill");

            EditorGUILayout.PropertyField(_cooldownFill,
                new GUIContent("Fill Image",
                    "Image hiển thị cooldown fill (Type = Filled, tỉ lệ 0–1).\n" +
                    "Để null nếu không cần."));

            if (_cooldownFill.objectReferenceValue == null)
            {
                DrawHelpBox(
                    "Chưa assign Fill Image.\n" +
                    "SetCooldownFill() sẽ không có hiệu lực visual.",
                    MessageType.Warning);
            }
            else
            {
                DrawHelpBox(
                    "Gọi từ game script:\n" +
                    "  btn.SetCooldownFill(0f);   // sẵn sàng\n" +
                    "  btn.SetCooldownFill(1f);   // full cooldown",
                    MessageType.Info);
            }
        }

        // ══════════════════════════════════════════════════════
        //  TAB: EVENTS
        // ══════════════════════════════════════════════════════

        private void DrawEventsTab()
        {
            DrawSectionLabel("UnityEvents");

            EditorGUILayout.PropertyField(_onPress,
                new GUIContent("On Press",
                    "Gọi 1 lần đúng frame khi nút được nhấn xuống (PointerDown)."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onRelease,
                new GUIContent("On Release",
                    "Gọi 1 lần khi nút được nhả ra hoặc pointer thoát khỏi vùng nút."));
            EditorGUILayout.Space(6);

            DrawSectionLabel("Hold Events");

            EditorGUILayout.PropertyField(_onHold,
                new GUIContent("On Hold",
                    "Gọi mỗi frame khi đang giữ nút.\n" +
                    "Dùng cho: charge skill, hold-to-cast, auto-attack."));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_onHoldTimed,
                new GUIContent("On Hold Timed (float)",
                    "Gọi mỗi frame khi đang giữ, tham số = thời gian giữ (giây).\n" +
                    "Dùng cho: charge bar, progress indicator, hold threshold detection."));
        }

        // ══════════════════════════════════════════════════════
        //  TAB: DEBUG
        // ══════════════════════════════════════════════════════

        private void DrawDebugTab()
        {
            if (!Application.isPlaying)
            {
                DrawHelpBox("Dữ liệu runtime chỉ hiển thị khi đang Play Mode.", MessageType.Info);
                return;
            }

            var btn = (TISMobileSkillButton)target;

            DrawSectionLabel("Runtime State");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Interactable");
                GUI.color = btn.Interactable ? _successColor : _dangerColor;
                EditorGUILayout.LabelField(btn.Interactable ? "true" : "false", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Hold Time");
                bool isHolding = btn.HoldTime > 0f;
                GUI.color = isHolding ? _warningColor : Color.gray;
                EditorGUILayout.LabelField(isHolding ? $"{btn.HoldTime:F2} s" : "—", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            if (_cooldownFill.objectReferenceValue is Image fillImg)
            {
                EditorGUILayout.Space(4);
                DrawSectionLabel("Cooldown");
                Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(r, fillImg.fillAmount, $"Cooldown Fill: {fillImg.fillAmount:P0}");
            }

            EditorGUILayout.Space(6);
            DrawSectionLabel("Debug Controls");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Force Press"))
                    btn.SendMessage("PressButton",   SendMessageOptions.DontRequireReceiver);
                if (GUILayout.Button("Force Release"))
                    btn.SendMessage("ReleaseButton", SendMessageOptions.DontRequireReceiver);
            }

            Repaint();
        }

        // ══════════════════════════════════════════════════════
        //  DROPDOWN HELPERS
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Gán profile mới, rebuild danh sách action names, lưu GUID vào EditorPrefs.
        /// Chỉ lấy các action có bindingMobile không rỗng (những action button có thể bind trên mobile).
        /// </summary>
        private void SetLinkedProfile(TISInputProfileConfig profile)
        {
            _linkedProfile = profile;

            if (profile == null)
            {
                _actionNames   = System.Array.Empty<string>();
                _selectedIndex = -1;
                EditorPrefs.DeleteKey(_profilePrefKey);
                return;
            }

            // Lưu GUID để restore sau recompile
            string path = AssetDatabase.GetAssetPath(profile);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(_profilePrefKey, guid);

            RebuildActionNames();
        }

        /// <summary>
        /// Lấy toàn bộ action name trong profile có bindingMobile hợp lệ
        /// và tính toán _selectedIndex khớp với _skillName hiện tại.
        /// </summary>
        private void RebuildActionNames()
        {
            if (_linkedProfile == null)
            {
                _actionNames   = System.Array.Empty<string>();
                _selectedIndex = -1;
                return;
            }

            var names = new List<string>();
            foreach (var action in _linkedProfile.GetAllActions())
            {
                // Chỉ hiển thị action có mobile binding — những thứ button mobile có thể dùng
                if (!string.IsNullOrEmpty(action.bindingMobile))
                    names.Add(action.actionName);
            }

            _actionNames = names.ToArray();

            // Sync selected index với _skillName đang lưu
            string current = _skillName?.stringValue ?? "";
            _selectedIndex = System.Array.IndexOf(_actionNames, current);
        }

        /// <summary>
        /// Sau recompile/OnEnable: khôi phục profile từ GUID lưu trong EditorPrefs
        /// rồi rebuild dropdown.
        /// </summary>
        private void RestoreLinkedProfileFromPrefs()
        {
            string guid = EditorPrefs.GetString(_profilePrefKey, "");
            if (string.IsNullOrEmpty(guid))
            {
                // Không có pref → thử tìm profile đầu tiên trong project như gợi ý
                // (optional: có thể bỏ block này nếu muốn luôn để trống)
                TryAutoDetectProfile();
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                _linkedProfile = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(path);
            }

            RebuildActionNames();
        }

        /// <summary>
        /// Nếu trong project chỉ có đúng 1 TISInputProfileConfig,
        /// tự động chọn nó để tiện cho project nhỏ/mới.
        /// Không làm gì nếu có nhiều hơn 1 (tránh chọn sai).
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