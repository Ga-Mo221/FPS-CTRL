#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public class BlenderEditorPreferences : EditorWindow
    {
        // ══════════════════════════════════════════════════════════════════════
        //  Menu item
        // ══════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/GaMo ToolKit/Blender Style Workflow/Setting Windown", priority = 0)]
        public static void OpenWindow()
        {
            var win = GetWindow<BlenderEditorPreferences>(false, "Blender Editor Tools", true);
            win.minSize = new Vector2(400f, 500f);
            win.Show();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EditorPrefs Keys
        // ══════════════════════════════════════════════════════════════════════

        // Blender features
        private const string K_TAB_INSPECTOR    = "BlenderEditor_TabInspector";
        private const string K_TRANSFORM_KEYS   = "BlenderEditor_TransformHotkeys";
        private const string K_SCENE_NAV        = "BlenderEditor_SceneNavigation";
        private const string K_PRECISION_FACTOR = "BlenderEditor_PrecisionFactor";

        // View Axis Pie Menu
        private const string K_VAPM_ENABLED         = "VAPM_Enabled";
        private const string K_VAPM_KEY_CODE         = "VAPM_KeyCode";
        private const string K_VAPM_USE_SHIFT        = "VAPM_UseShift";
        private const string K_VAPM_USE_ALT          = "VAPM_UseAlt";
        private const string K_VAPM_USE_CTRL         = "VAPM_UseCtrl";
        private const string K_VAPM_HOLD_THRESH      = "VAPM_HoldThreshold";
        private const string K_VAPM_DEADZONE         = "VAPM_DeadZone";
        private const string K_VAPM_SNAP_DUR         = "VAPM_SnapDuration";
        private const string K_VAPM_MENU_RADIUS      = "VAPM_MenuRadius";
        private const string K_VAPM_BUTTON_SIZE      = "VAPM_ButtonSize";
        private const string K_VAPM_ORTHO_ON_SNAP    = "VAPM_OrthoOnSnap";
        private const string K_VAPM_EXIT_ORTHO_ORBIT = "VAPM_ExitOrthoOnOrbit";
        private const string K_VAPM_BACKDROP_RADIUS  = "VAPM_BackdropRadius";
        private const string K_VAPM_CENTER_RADIUS    = "VAPM_CenterRadius";
        private const string K_VAPM_CENTER_FONT      = "VAPM_CenterFont";
        private const string K_VAPM_BTN_LABEL_FONT   = "VAPM_BtnLabelFont";
        private const string K_VAPM_BTN_SUB_FONT     = "VAPM_BtnSubFont";
        private const string K_VAPM_TOOLTIP_FONT     = "VAPM_TooltipFont";
        private const string K_VAPM_HINT_FONT        = "VAPM_HintFont";
        private const string K_VAPM_HOVER_SCALE      = "VAPM_HoverScale";
        private const string K_VAPM_BEAM_ARROW       = "VAPM_BeamArrow";

        // ══════════════════════════════════════════════════════════════════════
        //  Defaults
        // ══════════════════════════════════════════════════════════════════════

        public const bool    DEFAULT_TAB_INSPECTOR         = true;
        public const bool    DEFAULT_TRANSFORM_KEYS        = false;
        public const bool    DEFAULT_SCENE_NAV             = false;
        public const float   DEFAULT_PRECISION_FACTOR      = 0.2f;

        public const bool    DEFAULT_VAPM_ENABLED          = true;
        public const KeyCode DEFAULT_VAPM_KEY_CODE         = KeyCode.Q;
        public const bool    DEFAULT_VAPM_USE_SHIFT        = true;
        public const bool    DEFAULT_VAPM_USE_ALT          = true;
        public const bool    DEFAULT_VAPM_USE_CTRL         = false;
        public const float   DEFAULT_VAPM_HOLD_THRESH      = 0.18f;
        public const float   DEFAULT_VAPM_DEADZONE         = 28f;
        public const float   DEFAULT_VAPM_SNAP_DUR         = 0.32f;
        public const float   DEFAULT_VAPM_MENU_RADIUS      = 92f;
        public const float   DEFAULT_VAPM_BUTTON_SIZE      = 68f;
        public const bool    DEFAULT_VAPM_ORTHO_ON_SNAP    = false;
        public const bool    DEFAULT_VAPM_EXIT_ORTHO_ORBIT = true;
        public const float   DEFAULT_VAPM_BACKDROP_RADIUS  = 172f;
        public const float   DEFAULT_VAPM_CENTER_RADIUS    = 24f;
        public const int     DEFAULT_VAPM_CENTER_FONT      = 8;
        public const int     DEFAULT_VAPM_BTN_LABEL_FONT   = 12;
        public const int     DEFAULT_VAPM_BTN_SUB_FONT     = 7;
        public const int     DEFAULT_VAPM_TOOLTIP_FONT     = 11;
        public const int     DEFAULT_VAPM_HINT_FONT        = 9;
        public const float   DEFAULT_VAPM_HOVER_SCALE      = 1.18f;
        public const float   DEFAULT_VAPM_BEAM_ARROW       = 9f;

        // ══════════════════════════════════════════════════════════════════════
        //  Static accessors – Blender features
        // ══════════════════════════════════════════════════════════════════════

        public static bool TabInspectorEnabled
        {
            get => EditorPrefs.GetBool(K_TAB_INSPECTOR, DEFAULT_TAB_INSPECTOR);
            set => EditorPrefs.SetBool(K_TAB_INSPECTOR, value);
        }
        public static bool TransformHotkeysEnabled
        {
            get => EditorPrefs.GetBool(K_TRANSFORM_KEYS, DEFAULT_TRANSFORM_KEYS);
            set => EditorPrefs.SetBool(K_TRANSFORM_KEYS, value);
        }
        public static bool SceneNavigationEnabled
        {
            get => EditorPrefs.GetBool(K_SCENE_NAV, DEFAULT_SCENE_NAV);
            set => EditorPrefs.SetBool(K_SCENE_NAV, value);
        }
        public static float PrecisionFactor
        {
            get => EditorPrefs.GetFloat(K_PRECISION_FACTOR, DEFAULT_PRECISION_FACTOR);
            set => EditorPrefs.SetFloat(K_PRECISION_FACTOR, Mathf.Clamp(value, 0.01f, 1.0f));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Static accessors – View Axis Pie Menu
        // ══════════════════════════════════════════════════════════════════════

        public static bool PieMenuEnabled
        {
            get => EditorPrefs.GetBool(K_VAPM_ENABLED, DEFAULT_VAPM_ENABLED);
            set => EditorPrefs.SetBool(K_VAPM_ENABLED, value);
        }
        public static KeyCode PieMenuHotKey
        {
            get => (KeyCode)EditorPrefs.GetInt(K_VAPM_KEY_CODE, (int)DEFAULT_VAPM_KEY_CODE);
            set => EditorPrefs.SetInt(K_VAPM_KEY_CODE, (int)value);
        }
        public static bool PieMenuUseShift
        {
            get => EditorPrefs.GetBool(K_VAPM_USE_SHIFT, DEFAULT_VAPM_USE_SHIFT);
            set => EditorPrefs.SetBool(K_VAPM_USE_SHIFT, value);
        }
        public static bool PieMenuUseAlt
        {
            get => EditorPrefs.GetBool(K_VAPM_USE_ALT, DEFAULT_VAPM_USE_ALT);
            set => EditorPrefs.SetBool(K_VAPM_USE_ALT, value);
        }
        public static bool PieMenuUseCtrl
        {
            get => EditorPrefs.GetBool(K_VAPM_USE_CTRL, DEFAULT_VAPM_USE_CTRL);
            set => EditorPrefs.SetBool(K_VAPM_USE_CTRL, value);
        }
        public static float PieMenuHoldThreshold
        {
            get => EditorPrefs.GetFloat(K_VAPM_HOLD_THRESH, DEFAULT_VAPM_HOLD_THRESH);
            set => EditorPrefs.SetFloat(K_VAPM_HOLD_THRESH, Mathf.Clamp(value, 0.05f, 1.0f));
        }
        public static float PieMenuDeadZone
        {
            get => EditorPrefs.GetFloat(K_VAPM_DEADZONE, DEFAULT_VAPM_DEADZONE);
            set => EditorPrefs.SetFloat(K_VAPM_DEADZONE, Mathf.Clamp(value, 4f, 80f));
        }
        public static float PieMenuSnapDuration
        {
            get => EditorPrefs.GetFloat(K_VAPM_SNAP_DUR, DEFAULT_VAPM_SNAP_DUR);
            set => EditorPrefs.SetFloat(K_VAPM_SNAP_DUR, Mathf.Clamp(value, 0.05f, 1.5f));
        }
        public static float PieMenuRadius
        {
            get => EditorPrefs.GetFloat(K_VAPM_MENU_RADIUS, DEFAULT_VAPM_MENU_RADIUS);
            set => EditorPrefs.SetFloat(K_VAPM_MENU_RADIUS, Mathf.Clamp(value, 50f, 200f));
        }
        public static float PieMenuButtonSize
        {
            get => EditorPrefs.GetFloat(K_VAPM_BUTTON_SIZE, DEFAULT_VAPM_BUTTON_SIZE);
            set => EditorPrefs.SetFloat(K_VAPM_BUTTON_SIZE, Mathf.Clamp(value, 32f, 120f));
        }
        public static bool PieMenuOrthoOnSnap
        {
            get => EditorPrefs.GetBool(K_VAPM_ORTHO_ON_SNAP, DEFAULT_VAPM_ORTHO_ON_SNAP);
            set => EditorPrefs.SetBool(K_VAPM_ORTHO_ON_SNAP, value);
        }
        public static bool PieMenuExitOrthoOnOrbit
        {
            get => EditorPrefs.GetBool(K_VAPM_EXIT_ORTHO_ORBIT, DEFAULT_VAPM_EXIT_ORTHO_ORBIT);
            set => EditorPrefs.SetBool(K_VAPM_EXIT_ORTHO_ORBIT, value);
        }
        public static float PieMenuBackdropRadius
        {
            get => EditorPrefs.GetFloat(K_VAPM_BACKDROP_RADIUS, DEFAULT_VAPM_BACKDROP_RADIUS);
            set => EditorPrefs.SetFloat(K_VAPM_BACKDROP_RADIUS, Mathf.Clamp(value, 80f, 400f));
        }
        public static float PieMenuCenterRadius
        {
            get => EditorPrefs.GetFloat(K_VAPM_CENTER_RADIUS, DEFAULT_VAPM_CENTER_RADIUS);
            set => EditorPrefs.SetFloat(K_VAPM_CENTER_RADIUS, Mathf.Clamp(value, 10f, 60f));
        }
        public static int PieMenuCenterFontSize
        {
            get => EditorPrefs.GetInt(K_VAPM_CENTER_FONT, DEFAULT_VAPM_CENTER_FONT);
            set => EditorPrefs.SetInt(K_VAPM_CENTER_FONT, Mathf.Clamp(value, 6, 20));
        }
        public static int PieMenuBtnLabelFontSize
        {
            get => EditorPrefs.GetInt(K_VAPM_BTN_LABEL_FONT, DEFAULT_VAPM_BTN_LABEL_FONT);
            set => EditorPrefs.SetInt(K_VAPM_BTN_LABEL_FONT, Mathf.Clamp(value, 6, 28));
        }
        public static int PieMenuBtnSubFontSize
        {
            get => EditorPrefs.GetInt(K_VAPM_BTN_SUB_FONT, DEFAULT_VAPM_BTN_SUB_FONT);
            set => EditorPrefs.SetInt(K_VAPM_BTN_SUB_FONT, Mathf.Clamp(value, 5, 20));
        }
        public static int PieMenuTooltipFontSize
        {
            get => EditorPrefs.GetInt(K_VAPM_TOOLTIP_FONT, DEFAULT_VAPM_TOOLTIP_FONT);
            set => EditorPrefs.SetInt(K_VAPM_TOOLTIP_FONT, Mathf.Clamp(value, 6, 24));
        }
        public static int PieMenuHintFontSize
        {
            get => EditorPrefs.GetInt(K_VAPM_HINT_FONT, DEFAULT_VAPM_HINT_FONT);
            set => EditorPrefs.SetInt(K_VAPM_HINT_FONT, Mathf.Clamp(value, 6, 20));
        }
        public static float PieMenuHoverScale
        {
            get => EditorPrefs.GetFloat(K_VAPM_HOVER_SCALE, DEFAULT_VAPM_HOVER_SCALE);
            set => EditorPrefs.SetFloat(K_VAPM_HOVER_SCALE, Mathf.Clamp(value, 1.0f, 2.0f));
        }
        public static float PieMenuBeamArrowSize
        {
            get => EditorPrefs.GetFloat(K_VAPM_BEAM_ARROW, DEFAULT_VAPM_BEAM_ARROW);
            set => EditorPrefs.SetFloat(K_VAPM_BEAM_ARROW, Mathf.Clamp(value, 3f, 30f));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        public static string PieMenuHotKeyDisplayString()
        {
            var parts = new List<string>();
            if (PieMenuUseCtrl)  parts.Add("Ctrl");
            if (PieMenuUseShift) parts.Add("Shift");
            if (PieMenuUseAlt)   parts.Add("Alt");
            parts.Add(PieMenuHotKey.ToString());
            return string.Join(" + ", parts);
        }

        public static bool PieMenuMatchesHotKey(Event e)
        {
            if (e == null) return false;
            bool modOk = (e.shift   == PieMenuUseShift) &&
                         (e.alt     == PieMenuUseAlt)   &&
                         (e.control == PieMenuUseCtrl || e.command == PieMenuUseCtrl);
            return modOk && e.keyCode == PieMenuHotKey;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Reset all
        // ══════════════════════════════════════════════════════════════════════

        public static void ResetAllToDefaults()
        {
            TabInspectorEnabled     = DEFAULT_TAB_INSPECTOR;
            TransformHotkeysEnabled = DEFAULT_TRANSFORM_KEYS;
            SceneNavigationEnabled  = DEFAULT_SCENE_NAV;
            PrecisionFactor         = DEFAULT_PRECISION_FACTOR;

            PieMenuEnabled          = DEFAULT_VAPM_ENABLED;
            PieMenuHotKey           = DEFAULT_VAPM_KEY_CODE;
            PieMenuUseShift         = DEFAULT_VAPM_USE_SHIFT;
            PieMenuUseAlt           = DEFAULT_VAPM_USE_ALT;
            PieMenuUseCtrl          = DEFAULT_VAPM_USE_CTRL;
            PieMenuHoldThreshold    = DEFAULT_VAPM_HOLD_THRESH;
            PieMenuDeadZone         = DEFAULT_VAPM_DEADZONE;
            PieMenuSnapDuration     = DEFAULT_VAPM_SNAP_DUR;
            PieMenuRadius           = DEFAULT_VAPM_MENU_RADIUS;
            PieMenuButtonSize       = DEFAULT_VAPM_BUTTON_SIZE;
            PieMenuOrthoOnSnap      = DEFAULT_VAPM_ORTHO_ON_SNAP;
            PieMenuExitOrthoOnOrbit = DEFAULT_VAPM_EXIT_ORTHO_ORBIT;
            PieMenuBackdropRadius   = DEFAULT_VAPM_BACKDROP_RADIUS;
            PieMenuCenterRadius     = DEFAULT_VAPM_CENTER_RADIUS;
            PieMenuCenterFontSize   = DEFAULT_VAPM_CENTER_FONT;
            PieMenuBtnLabelFontSize = DEFAULT_VAPM_BTN_LABEL_FONT;
            PieMenuBtnSubFontSize   = DEFAULT_VAPM_BTN_SUB_FONT;
            PieMenuTooltipFontSize  = DEFAULT_VAPM_TOOLTIP_FONT;
            PieMenuHintFontSize     = DEFAULT_VAPM_HINT_FONT;
            PieMenuHoverScale       = DEFAULT_VAPM_HOVER_SCALE;
            PieMenuBeamArrowSize    = DEFAULT_VAPM_BEAM_ARROW;

            CreateObjectPieMenu.Enabled  = true;
            CreateObjectPieMenu.HotKey   = KeyCode.A;
            CreateObjectPieMenu.UseShift = true;
            CreateObjectPieMenu.UseAlt   = true;
            CreateObjectPieMenu.UseCtrl  = false;

            // Copy & Paste Transform giờ dùng CHUNG 1 tổ hợp phím (PasteTransformPieMenu.*
            // trỏ thẳng về CopyTransformPieMenu.* — xem ghi chú trong PasteTransformPieMenu.cs).
            // Chỉ cần set 1 lần ở đây, không cần set lại cho Paste.
            CopyTransformPieMenu.Enabled  = true;
            CopyTransformPieMenu.HotKey   = KeyCode.W;
            CopyTransformPieMenu.UseShift = true;
            CopyTransformPieMenu.UseAlt   = false;
            CopyTransformPieMenu.UseCtrl  = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Instance – UI state
        // ══════════════════════════════════════════════════════════════════════

        private Vector2 _scroll;
        private int     _activeTab       = 0;   // 0=Features  1=View Axis  2=Create Object

        private bool   _isCapturingVAPM  = false;
        private string _vapmConflictMsg  = "";
        private bool   _isCapturingCOPM  = false;
        private bool   _isCapturingCTPM  = false;
        private bool   _isCapturingPTPM  = false;

        private bool _foldVisual = true;
        private bool _foldFont   = false;
        private bool _foldFx     = false;

        // GUIStyles
        private GUIStyle _stTitle;
        private GUIStyle _stSection;
        private GUIStyle _stCapture;
        private GUIStyle _stHint;
        private GUIStyle _stBadge;
        private GUIStyle _stSub;

        // Accent color per tab
        private static readonly Color[] TAB_COLORS =
        {
            new Color(0.35f, 0.70f, 1.00f),   // 0 Features               – blue
            new Color(0.45f, 0.85f, 0.45f),   // 1 View Axis              – green
            new Color(0.95f, 0.68f, 0.22f),   // 2 Create                 – orange
            new Color(0.72f, 0.48f, 0.95f),   // 3 Copy / Paste Transform – purple
        };

        // ══════════════════════════════════════════════════════════════════════
        //  OnGUI
        // ══════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EnsureStyles();

            if (_isCapturingVAPM) CaptureVAPM();
            if (_isCapturingCOPM) CaptureCOPM();
            if (_isCapturingCTPM) CaptureCTPM();
            if (_isCapturingPTPM) CapturePTPM();

            DrawHeader();
            DrawTabBar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(12);

            switch (_activeTab)
            {
                case 0: DrawFeaturesTab();      break;
                case 1: DrawViewAxisTab();      break;
                case 2: DrawCreateObjectTab();  break;
                case 3: DrawCopyTransformTab(); break;
            }

            GUILayout.Space(20);
            DrawResetButton();
            GUILayout.Space(14);

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                SceneView.RepaintAll();
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Header
        // ══════════════════════════════════════════════════════════════════════

        private void DrawHeader()
        {
            float w = position.width;
            Rect bg = new Rect(0, 0, w, 50f);

            Color dark = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.14f)
                : new Color(0.80f, 0.80f, 0.82f);
            EditorGUI.DrawRect(bg, dark);

            // Accent bar theo tab đang chọn
            Color accent = TAB_COLORS[_activeTab];
            EditorGUI.DrawRect(new Rect(0, bg.yMax - 2.5f, w, 2.5f),
                new Color(accent.r, accent.g, accent.b, 0.90f));

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("⚙  Blender Editor Tools", _stTitle);
            GUILayout.FlexibleSpace();

            // Version badge
            GUIStyle ver = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                alignment = TextAnchor.MiddleRight
            };
            GUILayout.Label("GaMo Tools", ver);
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab bar
        // ══════════════════════════════════════════════════════════════════════

        private void DrawTabBar()
        {
            string[] labels = { "  Features  ", "  View Axis Pie  ", "  Create Object Pie  ", "  Copy / Paste Transform Pie  " };

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < labels.Length; i++)
            {
                bool active = _activeTab == i;

                // Highlight active tab với màu accent nhẹ
                Color prevBg  = GUI.backgroundColor;
                Color prevCol = GUI.contentColor;
                if (active)
                {
                    GUI.backgroundColor = new Color(
                        TAB_COLORS[i].r, TAB_COLORS[i].g, TAB_COLORS[i].b, 0.22f);
                    GUI.contentColor = TAB_COLORS[i];
                }

                GUIStyle st = active
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;

                if (GUILayout.Button(labels[i], st))
                {
                    _activeTab       = i;
                    _isCapturingVAPM = false;
                    _isCapturingCOPM = false;
                    _isCapturingCTPM = false;
                    _isCapturingPTPM = false;
                    _vapmConflictMsg = "";
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = prevBg;
                GUI.contentColor    = prevCol;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 0 – Features
        // ══════════════════════════════════════════════════════════════════════

        private void DrawFeaturesTab()
        {
            SectionHeader("Blender-style Features", TAB_COLORS[0]);

            EditorGUILayout.HelpBox(
                "Bật / tắt từng tính năng. Thay đổi có hiệu lực ngay lập tức.",
                MessageType.None);
            GUILayout.Space(10);

            FeatureBox(
                "Tab-Based Component Inspector",
                "• Component tabs để điều hướng nhanh\n" +
                "• Drag & drop script support\n" +
                "• Context menu cho component",
                () => TabInspectorEnabled, v => TabInspectorEnabled = v, TAB_COLORS[0]);

            GUILayout.Space(6);

            FeatureBox(
                "Blender Transform Hotkeys  (G / R / S)",
                "• G – Move/Grab    • R – Rotate    • S – Scale\n" +
                "• X/Y/Z – Axis constraint    • Shift+X/Y/Z – Plane constraint\n" +
                "• Enter / LMB – Confirm    • Escape / RMB – Cancel\n" +
                "• Giữ Shift khi transform → Precision Mode (giảm tốc độ)",
                () => TransformHotkeysEnabled, v => TransformHotkeysEnabled = v, TAB_COLORS[0]);

            using (new EditorGUI.DisabledScope(!TransformHotkeysEnabled))
            {
                GUILayout.Space(2);
                Slider("    Precision factor  (Shift)",
                    "Hệ số giảm tốc khi giữ Shift trong G/R/S.\n" +
                    "0.2 = chỉ 20% tốc độ bình thường.",
                    PrecisionFactor, 0.01f, 1.0f, v => PrecisionFactor = v);
            }

            GUILayout.Space(6);

            FeatureBox(
                "Blender Scene Navigation  (MMB)",
                "• MMB Drag – Orbit camera\n" +
                "• Shift + MMB – Pan camera\n" +
                "• Scroll Wheel – Zoom\n" +
                "• Ctrl + MMB Drag – Zoom chính xác",
                () => SceneNavigationEnabled, v => SceneNavigationEnabled = v, TAB_COLORS[0]);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 1 – View Axis Pie Menu
        // ══════════════════════════════════════════════════════════════════════

        private void DrawViewAxisTab()
        {
            SectionHeader("View Axis Pie Menu", TAB_COLORS[1]);

            Toggle("Bật View Axis Pie Menu",
                "Mở/tắt tính năng chuyển góc nhìn nhanh trong Scene View",
                PieMenuEnabled, v => PieMenuEnabled = v);

            if (!PieMenuEnabled)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("View Axis Pie Menu đang tắt.", MessageType.Warning);
                return;
            }

            GUILayout.Space(12);

            // ── Phím tắt ────────────────────────────────────────────────────
            SubHeader("Phím tắt");

            ModifierRow(PieMenuUseCtrl, PieMenuUseShift, PieMenuUseAlt,
                (c,s,a) => { PieMenuUseCtrl=c; PieMenuUseShift=s; PieMenuUseAlt=a; _vapmConflictMsg=""; });

            GUILayout.Space(4);
            KeyCaptureRow(
                PieMenuHotKey.ToString(), PieMenuHotKeyDisplayString(),
                _isCapturingVAPM,
                () => { _isCapturingVAPM = true; _vapmConflictMsg = ""; },
                () =>   _isCapturingVAPM = false);

            if (!string.IsNullOrEmpty(_vapmConflictMsg))
            { GUILayout.Space(2); EditorGUILayout.HelpBox(_vapmConflictMsg, MessageType.Warning); }

            GUILayout.Space(12);

            // ── Hành vi ─────────────────────────────────────────────────────
            SubHeader("Hành vi");

            Slider("Hold threshold (giây)",
                "Nhấn dưới ngưỡng → Toggle Mode\nNhấn lâu hơn → Hold Mode",
                PieMenuHoldThreshold, 0.05f, 0.8f, v => PieMenuHoldThreshold = v);

            Slider("Direction dead-zone (px)",
                "Vùng bất hoạt quanh tâm. Phải lia chuột qua ngưỡng này mới tính hướng.",
                PieMenuDeadZone, 4f, 80f, v => PieMenuDeadZone = v);

            Slider("Snap duration (giây)",
                "Thời gian animation camera snap về góc nhìn đã chọn",
                PieMenuSnapDuration, 0.05f, 1.5f, v => PieMenuSnapDuration = v);

            GUILayout.Space(4);

            Toggle("Chuyển Orthographic khi snap",
                "Tự chuyển sang Orthographic sau khi chọn trục từ Pie Menu",
                PieMenuOrthoOnSnap, v => PieMenuOrthoOnSnap = v);

            Toggle("Thoát Orthographic khi Orbit",
                "Tự chuyển về Perspective khi bắt đầu xoay (MMB drag không Shift)",
                PieMenuExitOrthoOnOrbit, v => PieMenuExitOrthoOnOrbit = v);

            GUILayout.Space(10);

            // ── Giao diện (foldouts) ─────────────────────────────────────────
            _foldVisual = Foldout(_foldVisual, "Kích thước & layout");
            if (_foldVisual)
            {
                EditorGUI.indentLevel++;
                Slider("Bán kính nút (px)",       "Khoảng cách tâm → trung tâm nút",          PieMenuRadius,        50f, 200f, v => PieMenuRadius        = v);
                Slider("Kích thước nút (px)",      "Đường kính mỗi nút trục",                  PieMenuButtonSize,    32f, 120f, v => PieMenuButtonSize    = v);
                Slider("Vòng ngoài – bán kính",    "Bán kính hình nền mờ bên ngoài",           PieMenuBackdropRadius,80f, 400f, v => PieMenuBackdropRadius = v);
                Slider("Vòng tâm – bán kính",      "Bán kính nút tròn VIEW ở trung tâm",       PieMenuCenterRadius,  10f,  60f, v => PieMenuCenterRadius  = v);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(4);
            _foldFont = Foldout(_foldFont, "Cỡ chữ");
            if (_foldFont)
            {
                EditorGUI.indentLevel++;
                IntSlider("Tâm \"VIEW\"",          "Cỡ chữ nhãn VIEW ở giữa menu",             PieMenuCenterFontSize,   6, 20, v => PieMenuCenterFontSize   = v);
                IntSlider("Nút – label chính",     "Ký hiệu trục lớn (+X, -Y…)",               PieMenuBtnLabelFontSize, 6, 28, v => PieMenuBtnLabelFontSize = v);
                IntSlider("Nút – sub-label",       "Tên hướng nhỏ (FRONT, TOP…)",              PieMenuBtnSubFontSize,   5, 20, v => PieMenuBtnSubFontSize   = v);
                IntSlider("Tooltip",               "Tên đầy đủ khi hover",                     PieMenuTooltipFontSize,  6, 24, v => PieMenuTooltipFontSize  = v);
                IntSlider("Hint (dòng hướng dẫn)", "Gợi ý phím phía trên menu",                PieMenuHintFontSize,     6, 20, v => PieMenuHintFontSize     = v);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(4);
            _foldFx = Foldout(_foldFx, "Hiệu ứng");
            if (_foldFx)
            {
                EditorGUI.indentLevel++;
                Slider("Hover scale",      "Hệ số phóng to nút khi hover (1.0 = không đổi)", PieMenuHoverScale,    1.0f, 2.0f, v => PieMenuHoverScale    = v);
                Slider("Beam arrow (px)",  "Kích thước đầu mũi tên direction beam",           PieMenuBeamArrowSize, 3f,   30f,  v => PieMenuBeamArrowSize = v);
                EditorGUI.indentLevel--;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 2 – Create Object Pie Menu
        // ══════════════════════════════════════════════════════════════════════

        private void DrawCreateObjectTab()
        {
            SectionHeader("Create Object Pie Menu", TAB_COLORS[2]);

            Toggle("Bật Create Object Pie Menu",
                "Mở/tắt menu tạo nhanh object 3D trong Scene View",
                CreateObjectPieMenu.Enabled, v => CreateObjectPieMenu.Enabled = v);

            if (!CreateObjectPieMenu.Enabled)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("Create Object Pie Menu đang tắt.", MessageType.Warning);
                return;
            }

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Tạo nhanh object 3D ngay trong Scene View.\n\n" +
                "① Cube   ② Sphere   ③ Capsule   ④ Cylinder\n" +
                "⑤ Plane  ⑥ Quad     ⑦ Text (TMP 3D)   ⑧ Empty\n\n" +
                "Không chọn gì trong Hierarchy  →  tạo ở Root (không có cha).\n" +
                "Đang chọn GameObject           →  tạo làm con của object đó.",
                MessageType.Info);

            GUILayout.Space(12);
            SubHeader("Phím tắt");

            ModifierRow(CreateObjectPieMenu.UseCtrl, CreateObjectPieMenu.UseShift, CreateObjectPieMenu.UseAlt,
                (c,s,a) => { CreateObjectPieMenu.UseCtrl=c; CreateObjectPieMenu.UseShift=s; CreateObjectPieMenu.UseAlt=a; });

            GUILayout.Space(4);
            KeyCaptureRow(
                CreateObjectPieMenu.HotKey.ToString(), CreateObjectPieMenu.HotKeyDisplayString(),
                _isCapturingCOPM,
                () => _isCapturingCOPM = true,
                () => _isCapturingCOPM = false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tab 3 – Copy / Paste Transform Pie Menu (ĐÃ GỘP — dùng chung 1 tổ hợp
        //  phím và 1 công tắc Enabled duy nhất cho cả 2 pie, xem
        //  CopyTransformPieMenu.cs / PasteTransformPieMenu.cs).
        // ══════════════════════════════════════════════════════════════════════

        private void DrawCopyTransformTab()
        {
            SectionHeader("Copy / Paste Transform Pie Menu", TAB_COLORS[3]);

            Toggle("Bật Copy / Paste Transform Pie Menu",
                "Mở/tắt cả 2 menu copy & dán nhanh Position/Rotation/Scale trong Scene View " +
                "(1 công tắc chung, vì 2 pie giờ dùng chung 1 tổ hợp phím)",
                CopyTransformPieMenu.Enabled, v => CopyTransformPieMenu.Enabled = v);

            if (!CopyTransformPieMenu.Enabled)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox("Copy / Paste Transform Pie Menu đang tắt.", MessageType.Warning);
                return;
            }

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Copy nhanh Transform của object đang chọn, rồi dán lại cho object khác " +
                "(yêu cầu có Selection).\n\n" +
                "Trái:   ① Local Position   ② Local Rotation   ③ Local Scale\n" +
                "Phải:   ⑤ World Position   ⑥ World Rotation   ⑦ World Scale\n" +
                "Trên:   ④ Local Transform (cả 3, không gian local)\n" +
                "Dưới:   ⑧ World Transform (cả 3, không gian world)\n\n" +
                "Bố cục 8 nút của Paste giống hệt Copy. Nút nào chưa từng được copy sẽ hiển\n" +
                "thị mờ ở Paste và không làm gì nếu chọn trúng — tránh dán nhầm giá trị rác.",
                MessageType.Info);

            GUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "★ Copy và Paste dùng CHUNG 1 tổ hợp phím (cấu hình ngay bên dưới) — không còn\n" +
                "phím tắt riêng cho Paste nữa.\n\n" +
                "Giữ modifier + gõ phím chính rồi THẢ ra → Copy hiện (ở lại màn hình).\n" +
                "Vẫn giữ modifier, gõ phím chính THÊM LẦN NỮA → chuyển ngay sang Paste tại\n" +
                "chỗ (không tắt/bật lại, không chớp). Gõ tiếp lại quay về Copy — cứ so le\n" +
                "như vậy mỗi lần gõ.",
                MessageType.None);

            GUILayout.Space(12);
            SubHeader("Phím tắt (dùng chung cho cả Copy & Paste)");

            ModifierRow(CopyTransformPieMenu.UseCtrl, CopyTransformPieMenu.UseShift, CopyTransformPieMenu.UseAlt,
                (c,s,a) => { CopyTransformPieMenu.UseCtrl=c; CopyTransformPieMenu.UseShift=s; CopyTransformPieMenu.UseAlt=a; });

            GUILayout.Space(4);
            KeyCaptureRow(
                CopyTransformPieMenu.HotKey.ToString(), CopyTransformPieMenu.HotKeyDisplayString(),
                _isCapturingCTPM,
                () => _isCapturingCTPM = true,
                () => _isCapturingCTPM = false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Reset button
        // ══════════════════════════════════════════════════════════════════════

        private void DrawResetButton()
        {
            DrawSeparator();
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.80f, 0.28f, 0.28f);
            if (GUILayout.Button("  ↺  Đặt lại tất cả về mặc định  ",
                    GUILayout.Height(28), GUILayout.MinWidth(220)))
            {
                if (EditorUtility.DisplayDialog(
                        "Đặt lại Blender Editor Tools",
                        "Toàn bộ cài đặt sẽ trở về mặc định. Tiếp tục?",
                        "Đặt lại", "Hủy"))
                {
                    ResetAllToDefaults();
                    _isCapturingVAPM = _isCapturingCOPM = _isCapturingCTPM = _isCapturingPTPM = false;
                    _vapmConflictMsg = "";
                    GUI.FocusControl(null);
                    GUI.changed = true;
                }
            }
            GUI.backgroundColor = prevBg;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Key capture
        // ══════════════════════════════════════════════════════════════════════

        private void CaptureVAPM()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (IsModifierOnly(e.keyCode)) return;

            if (e.keyCode == KeyCode.Escape) { _isCapturingVAPM = false; }
            else
            {
                _vapmConflictMsg = CheckConflict(e.keyCode, e.shift, e.alt, e.control);
                PieMenuHotKey    = e.keyCode;
                PieMenuUseShift  = e.shift;
                PieMenuUseAlt    = e.alt;
                PieMenuUseCtrl   = e.control || e.command;
                _isCapturingVAPM = false;
            }
            e.Use(); GUI.changed = true; Repaint();
        }

        private void CaptureCOPM()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (IsModifierOnly(e.keyCode)) return;

            if (e.keyCode == KeyCode.Escape) { _isCapturingCOPM = false; }
            else
            {
                CreateObjectPieMenu.HotKey   = e.keyCode;
                CreateObjectPieMenu.UseShift = e.shift;
                CreateObjectPieMenu.UseAlt   = e.alt;
                CreateObjectPieMenu.UseCtrl  = e.control || e.command;
                _isCapturingCOPM = false;
            }
            e.Use(); GUI.changed = true; Repaint();
        }

        private void CaptureCTPM()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (IsModifierOnly(e.keyCode)) return;

            if (e.keyCode == KeyCode.Escape) { _isCapturingCTPM = false; }
            else
            {
                CopyTransformPieMenu.HotKey   = e.keyCode;
                CopyTransformPieMenu.UseShift = e.shift;
                CopyTransformPieMenu.UseAlt   = e.alt;
                CopyTransformPieMenu.UseCtrl  = e.control || e.command;
                _isCapturingCTPM = false;
            }
            e.Use(); GUI.changed = true; Repaint();
        }

        private void CapturePTPM()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (IsModifierOnly(e.keyCode)) return;

            if (e.keyCode == KeyCode.Escape) { _isCapturingPTPM = false; }
            else
            {
                PasteTransformPieMenu.HotKey   = e.keyCode;
                PasteTransformPieMenu.UseShift = e.shift;
                PasteTransformPieMenu.UseAlt   = e.alt;
                PasteTransformPieMenu.UseCtrl  = e.control || e.command;
                _isCapturingPTPM = false;
            }
            e.Use(); GUI.changed = true; Repaint();
        }

        private static bool IsModifierOnly(KeyCode kc) =>
            kc == KeyCode.LeftShift   || kc == KeyCode.RightShift   ||
            kc == KeyCode.LeftAlt     || kc == KeyCode.RightAlt     ||
            kc == KeyCode.LeftControl || kc == KeyCode.RightControl ||
            kc == KeyCode.LeftCommand || kc == KeyCode.RightCommand ||
            kc == KeyCode.None;

        private static string CheckConflict(KeyCode kc, bool shift, bool alt, bool ctrl)
        {
            if (shift || alt || ctrl) return "";
            return kc switch
            {
                KeyCode.W or KeyCode.A or KeyCode.S or KeyCode.D
                or KeyCode.Q or KeyCode.E =>
                    $"Cảnh báo: '{kc}' không có modifier có thể xung đột với điều hướng Scene View mặc định.",
                KeyCode.F   => "Cảnh báo: 'F' là phím 'Frame Selection' mặc định.",
                KeyCode.Delete => "Cảnh báo: 'Delete' là phím xóa object mặc định.",
                _ => ""
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Reusable UI widgets
        // ══════════════════════════════════════════════════════════════════════

        private void SectionHeader(string title, Color accent)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(r, new Color(accent.r, accent.g, accent.b, 0.09f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height),
                new Color(accent.r, accent.g, accent.b, 0.90f));
            GUIStyle st = new GUIStyle(_stTitle) { normal = { textColor = accent } };
            GUI.Label(new Rect(r.x + 12f, r.y + 5f, r.width - 14f, r.height), title, st);
            GUILayout.Space(8);
        }

        private static void SubHeader(string label)
        {
            GUILayout.Space(2);
            GUILayout.Label(label, EditorStyles.boldLabel);
            DrawThinSeparator();
            GUILayout.Space(4);
        }

        private static bool Foldout(bool state, string label) =>
            EditorGUILayout.Foldout(state, label, true, EditorStyles.foldoutHeader);

        private void FeatureBox(string title, string desc,
                                System.Func<bool> getter, System.Action<bool> setter,
                                Color accent)
        {
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.BeginHorizontal();

            // Barra de color a la izquierda
            Rect bar = GUILayoutUtility.GetRect(3f, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(3f));
            EditorGUI.DrawRect(bar, new Color(accent.r, accent.g, accent.b,
                getter() ? 0.90f : 0.25f));
            GUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            bool v = EditorGUILayout.ToggleLeft(title, getter(), EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck()) { setter(v); GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(desc, _stSub);
            EditorGUILayout.EndVertical();
        }

        private static void ModifierRow(bool curCtrl, bool curShift, bool curAlt,
            System.Action<bool,bool,bool> setter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Modifier keys", GUILayout.Width(220));
            EditorGUI.BeginChangeCheck();
            bool c = GUILayout.Toggle(curCtrl,  "Ctrl",  "Button", GUILayout.Width(52));
            bool s = GUILayout.Toggle(curShift, "Shift", "Button", GUILayout.Width(52));
            bool a = GUILayout.Toggle(curAlt,   "Alt",   "Button", GUILayout.Width(52));
            if (EditorGUI.EndChangeCheck()) setter(c, s, a);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void KeyCaptureRow(string keyLabel, string comboLabel,
            bool isCapturing,
            System.Action startCapture, System.Action stopCapture)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Phím chính", GUILayout.Width(220));
            if (isCapturing)
            {
                GUI.SetNextControlName("KeyCaptureField");
                GUILayout.Label("  Nhấn phím bất kỳ… (Esc = hủy)", _stCapture,
                    GUILayout.Width(200));
                GUI.FocusControl("KeyCaptureField");
                if (GUILayout.Button("Hủy", GUILayout.Width(50))) stopCapture();
            }
            else
            {
                GUILayout.Label(keyLabel, _stBadge, GUILayout.Width(74));
                if (GUILayout.Button("Đổi phím", GUILayout.Width(80))) startCapture();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(224);
            GUILayout.Label("Tổ hợp: " + comboLabel, _stHint);
            EditorGUILayout.EndHorizontal();
        }

        private static void Slider(string label, string tooltip,
            float cur, float min, float max, System.Action<float> set)
        {
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.Slider(new GUIContent(label, tooltip), cur, min, max);
            if (EditorGUI.EndChangeCheck()) set(v);
        }

        private static void IntSlider(string label, string tooltip,
            int cur, int min, int max, System.Action<int> set)
        {
            EditorGUI.BeginChangeCheck();
            int v = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), cur, min, max);
            if (EditorGUI.EndChangeCheck()) set(v);
        }

        private static void Toggle(string label, string tooltip,
            bool cur, System.Action<bool> set)
        {
            EditorGUI.BeginChangeCheck();
            bool v = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), cur);
            if (EditorGUI.EndChangeCheck()) set(v);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Drawing helpers
        // ══════════════════════════════════════════════════════════════════════

        private static void DrawSeparator()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
        }

        private static void DrawThinSeparator()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.12f));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Style bootstrap
        // ══════════════════════════════════════════════════════════════════════

        private void EnsureStyles()
        {
            if (_stTitle != null) return;

            _stTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin   = new RectOffset(0, 0, 2, 2)
            };

            _stSection = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal   = { textColor = new Color(0.60f, 0.60f, 0.60f) }
            };

            _stCapture = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize  = 11, fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = new Color(0.95f, 0.78f, 0.22f) }
            };

            _stHint = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.52f, 0.52f, 0.52f) } };

            _stBadge = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize  = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = EditorGUIUtility.isProSkin
                                ? new Color(0.90f, 0.90f, 0.90f)
                                : new Color(0.10f, 0.10f, 0.10f) }
            };

            _stSub = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { fontSize = 10, padding = new RectOffset(8, 5, 2, 5) };
        }
    }
}
#endif