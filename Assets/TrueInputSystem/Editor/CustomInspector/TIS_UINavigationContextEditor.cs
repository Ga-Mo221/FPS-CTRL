#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem
{
    /// <summary>
    /// Custom Editor cho UINavigationContext.
    /// Hiển thị 3 tab: Overview, Tab Mapping, SubTab Mapping.
    /// </summary>
    [CustomEditor(typeof(TISUINavigationContext))]
    public class TISUINavigationContextEditor : UnityEditor.Editor
    {
        // ── Tab state ─────────────────────────────────────────────────────────────
        private int _selectedTab = 0;
        private readonly string[] _tabLabels = { "Overview", "Tab Mapping", "SubTab Mapping" };

        // ── SerializedProperties ──────────────────────────────────────────────────
        private SerializedProperty _tabs;
        private SerializedProperty _subTabs;
        private SerializedProperty _defaultTab;
        private SerializedProperty _defaultSubTab;
        private SerializedProperty _defaultContent;

        // ── Foldout state ─────────────────────────────────────────────────────────
        private List<bool> _tabFoldouts = new();
        private List<bool> _subTabFoldouts = new();

        // ── Styles (lazy init) ────────────────────────────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInit;

        // ── Colors ────────────────────────────────────────────────────────────────
        private static readonly Color ColTab = new Color(0.32f, 0.29f, 0.72f, 1f); // purple
        private static readonly Color ColSubTab = new Color(0.12f, 0.43f, 0.33f, 1f); // teal
        private static readonly Color ColRemove = new Color(0.72f, 0.18f, 0.18f, 1f); // red
        private static readonly Color ColAdd = new Color(0.20f, 0.60f, 0.40f, 1f); // green

        // ─────────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _tabs = serializedObject.FindProperty("tabs");
            _subTabs = serializedObject.FindProperty("subTabs");
            _defaultTab = serializedObject.FindProperty("defaultTab");
            _defaultSubTab = serializedObject.FindProperty("defaultSubTab");
            _defaultContent = serializedObject.FindProperty("defaultContent");

            SyncFoldouts();
            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            string[] guids = AssetDatabase.FindAssets("UIbuttongamepadmanager t:Texture2D");
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

        private void SyncFoldouts()
        {
            while (_tabFoldouts.Count < _tabs.arraySize) _tabFoldouts.Add(true);
            while (_subTabFoldouts.Count < _subTabs.arraySize) _subTabFoldouts.Add(true);
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.9f, 0.9f, 0.9f)
                : new Color(0.1f, 0.1f, 0.1f);

            _sectionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            _sectionStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            _boxStyle = new GUIStyle("box")
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };
        }

        // ── Main draw ─────────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();
            SyncFoldouts();

            DrawHeaders();
            EditorGUILayout.Space(4);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels, GUILayout.Height(26));
            EditorGUILayout.Space(6);

            switch (_selectedTab)
            {
                case 0: DrawOverviewTab(); break;
                case 1: DrawTabMappingTab(); break;
                case 2: DrawSubTabTab(); break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Header ────────────────────────────────────────────────────────────────

        private void DrawHeaders()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("UINavigationContext", _headerStyle);
            GUILayout.FlexibleSpace();
            DrawColorDot(ColTab, "Tab");
            GUILayout.Space(4);
            DrawColorDot(ColSubTab, "SubTab");
            EditorGUILayout.EndHorizontal();

            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.3f));
        }

        private void DrawColorDot(Color color, string label)
        {
            var r = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10), GUILayout.Height(10));
            r.y += 3;
            EditorGUI.DrawRect(r, color);
            GUILayout.Label(label, _sectionStyle);
        }

        // ── Tab 0: Overview ───────────────────────────────────────────────────────

        private void DrawOverviewTab()
        {
            SectionLabel("TRẠNG THÁI HIỆN TẠI");
            EditorGUILayout.BeginVertical(_boxStyle);

            DrawStatRow("Tabs đã khai báo", _tabs.arraySize.ToString(), ColTab);
            DrawStatRow("SubTabs đã khai báo", _subTabs.arraySize.ToString(), ColSubTab);

            // Validate
            int issues = CountIssues();
            Color issueColor = issues == 0 ? ColAdd : ColRemove;
            DrawStatRow("Vấn đề phát hiện", issues.ToString(), issueColor);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            SectionLabel("MẶC ĐỊNH (INITIAL SELECTION)");
            EditorGUILayout.BeginVertical(_boxStyle);
            EditorGUILayout.PropertyField(_defaultTab, new GUIContent("Default Tab", "Tab được chọn khi scene start."));
            EditorGUILayout.PropertyField(_defaultSubTab,
                new GUIContent("Default SubTab", "SubTab được chọn khi scene start."));
            EditorGUILayout.PropertyField(_defaultContent,
                new GUIContent("Default Content", "Content button được focus khi scene start."));
            EditorGUILayout.EndVertical();

            // EditorGUILayout.Space(8);
            // SectionLabel("THÔNG TIN");
            // EditorGUILayout.BeginVertical(_boxStyle);
            // EditorGUILayout.HelpBox(
            //     "• LT / RT  →  đổi Tab, auto redirect SubTab + Content\n" +
            //     "• LB / RB  →  đổi SubTab, auto redirect Content\n\n" +
            //     "Đặt component này trên 1 GameObject duy nhất trong scene.\n" +
            //     "Khai báo đầy đủ TabEntry và SubTabEntry để navigation hoạt động đúng.",
            //     MessageType.Info);
            // EditorGUILayout.EndVertical();

            if (issues > 0)
            {
                EditorGUILayout.Space(4);
                DrawIssueBox();
            }
        }

        private void DrawStatRow(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.label);
            GUILayout.FlexibleSpace();
            var prev = GUI.color;
            GUI.color = valueColor;
            GUILayout.Label(value, EditorStyles.boldLabel);
            GUI.color = prev;
            EditorGUILayout.EndHorizontal();
        }

        // ── Tab 1: Tab Mapping ────────────────────────────────────────────────────

        private void DrawTabMappingTab()
        {
            // SectionLabel("TAB ENTRIES  —  mỗi entry là 1 Tab button trên UI");
            // EditorGUILayout.Space(2);

            for (int i = 0; i < _tabs.arraySize; i++)
            {
                var element = _tabs.GetArrayElementAtIndex(i);
                var tabBtn = element.FindPropertyRelative("tabButton");
                string entryName = tabBtn.objectReferenceValue != null
                    ? tabBtn.objectReferenceValue.name
                    : $"TabEntry {i + 1}";

                EditorGUILayout.BeginVertical(_boxStyle);

                // Foldout header row
                EditorGUILayout.BeginHorizontal();
                _tabFoldouts[i] = EditorGUILayout.Foldout(_tabFoldouts[i], entryName, true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                DrawBadge("TAB", ColTab);
                GUILayout.Space(4);
                if (DrawIconButton("−", ColRemove, 22))
                {
                    _tabs.DeleteArrayElementAtIndex(i);
                    _tabFoldouts.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (_tabFoldouts[i])
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(tabBtn, new GUIContent("Tab Button", "GamepadUIButton của Tab này."));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("firstSubTab"),
                        new GUIContent("First SubTab", "SubTab sẽ được highlight khi Tab này active."));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("firstContent"),
                        new GUIContent("First Content", "Content button sẽ được focus khi Tab này active."));
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            if (DrawAddButton("+ Thêm TabEntry", ColTab))
            {
                _tabs.InsertArrayElementAtIndex(_tabs.arraySize);
                _tabFoldouts.Add(true);
            }
        }

        // ── Tab 2: SubTab Mapping ─────────────────────────────────────────────────

        private void DrawSubTabTab()
        {
            // SectionLabel("SUBTAB ENTRIES  —  mỗi entry là 1 SubTab button trên UI");
            // EditorGUILayout.Space(2);

            for (int i = 0; i < _subTabs.arraySize; i++)
            {
                var element = _subTabs.GetArrayElementAtIndex(i);
                var subTabBtn = element.FindPropertyRelative("subTabButton");
                string entryName = subTabBtn.objectReferenceValue != null
                    ? subTabBtn.objectReferenceValue.name
                    : $"SubTabEntry {i + 1}";

                EditorGUILayout.BeginVertical(_boxStyle);

                EditorGUILayout.BeginHorizontal();
                _subTabFoldouts[i] =
                    EditorGUILayout.Foldout(_subTabFoldouts[i], entryName, true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                DrawBadge("SUBTAB", ColSubTab);
                GUILayout.Space(4);
                if (DrawIconButton("−", ColRemove, 22))
                {
                    _subTabs.DeleteArrayElementAtIndex(i);
                    _subTabFoldouts.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (_subTabFoldouts[i])
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(subTabBtn,
                        new GUIContent("SubTab Button", "GamepadUIButton của SubTab này."));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("firstContent"),
                        new GUIContent("First Content", "Content button sẽ được focus khi SubTab này active."));
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);
            if (DrawAddButton("+ Thêm SubTabEntry", ColSubTab))
            {
                _subTabs.InsertArrayElementAtIndex(_subTabs.arraySize);
                _subTabFoldouts.Add(true);
            }
        }

        // ── Validation ────────────────────────────────────────────────────────────

        private int CountIssues()
        {
            int count = 0;
            for (int i = 0; i < _tabs.arraySize; i++)
            {
                var el = _tabs.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("tabButton").objectReferenceValue == null) count++;
                if (el.FindPropertyRelative("firstSubTab").objectReferenceValue == null) count++;
                if (el.FindPropertyRelative("firstContent").objectReferenceValue == null) count++;
            }

            for (int i = 0; i < _subTabs.arraySize; i++)
            {
                var el = _subTabs.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("subTabButton").objectReferenceValue == null) count++;
                if (el.FindPropertyRelative("firstContent").objectReferenceValue == null) count++;
            }

            if (_defaultTab.objectReferenceValue == null) count++;
            if (_defaultSubTab.objectReferenceValue == null) count++;
            if (_defaultContent.objectReferenceValue == null) count++;
            return count;
        }

        private void DrawIssueBox()
        {
            var issues = new List<string>();

            for (int i = 0; i < _tabs.arraySize; i++)
            {
                var el = _tabs.GetArrayElementAtIndex(i);
                string name = $"TabEntry {i + 1}";
                if (el.FindPropertyRelative("tabButton").objectReferenceValue == null)
                    issues.Add($"{name}: thiếu tabButton");
                if (el.FindPropertyRelative("firstSubTab").objectReferenceValue == null)
                    issues.Add($"{name}: thiếu firstSubTab");
                if (el.FindPropertyRelative("firstContent").objectReferenceValue == null)
                    issues.Add($"{name}: thiếu firstContent");
            }

            for (int i = 0; i < _subTabs.arraySize; i++)
            {
                var el = _subTabs.GetArrayElementAtIndex(i);
                string name = $"SubTabEntry {i + 1}";
                if (el.FindPropertyRelative("subTabButton").objectReferenceValue == null)
                    issues.Add($"{name}: thiếu subTabButton");
                if (el.FindPropertyRelative("firstContent").objectReferenceValue == null)
                    issues.Add($"{name}: thiếu firstContent");
            }

            if (_defaultTab.objectReferenceValue == null) issues.Add("Default Tab chưa được gán");
            if (_defaultSubTab.objectReferenceValue == null) issues.Add("Default SubTab chưa được gán");
            if (_defaultContent.objectReferenceValue == null) issues.Add("Default Content chưa được gán");

            if (issues.Count == 0) return;
            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SectionLabel(string text)
        {
            GUILayout.Label(text, _sectionStyle);
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.2f));
            EditorGUILayout.Space(4);
        }

        private void DrawBadge(string label, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white, background = MakeTex(color) },
                padding = new RectOffset(6, 6, 2, 2),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label(label, style, GUILayout.Height(18));
        }

        private bool DrawIconButton(string label, Color color, int width)
        {
            var prev = GUI.color;
            GUI.color = color;
            bool clicked = GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(20));
            GUI.color = prev;
            return clicked;
        }

        private bool DrawAddButton(string label, Color color)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            bool clicked = GUILayout.Button(label, GUILayout.Height(24));
            GUI.backgroundColor = prev;
            return clicked;
        }

        private static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
#endif
