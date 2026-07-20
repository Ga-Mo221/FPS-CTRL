using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace TrueInputSystem
{
    [CustomEditor(typeof(TISGamepadUIButtonGroup))]
    public class TISGamepadUIButtonGroupEditor : UnityEditor.Editor
    {
        private SerializedProperty layoutTypeProp;
        private SerializedProperty gridColumnsProp;
        private SerializedProperty gridVerticalWrapProp;

        private void OnEnable()
        {
            layoutTypeProp = serializedObject.FindProperty("layoutType");
            gridColumnsProp = serializedObject.FindProperty("gridColumns");
            gridVerticalWrapProp = serializedObject.FindProperty("gridVerticalWrap");

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            // Tìm file icon theo tên, không cần biết nằm ở folder nào
            string[] guids = AssetDatabase.FindAssets("GridGamepadButton t:Texture2D");
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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TISGamepadUIButtonGroup group = (TISGamepadUIButtonGroup)target;
            TISGamepadUIButtonGroup.GroupLayoutType layout =
                (TISGamepadUIButtonGroup.GroupLayoutType)layoutTypeProp.enumValueIndex;

            EditorGUILayout.PropertyField(layoutTypeProp);

            if (layout == TISGamepadUIButtonGroup.GroupLayoutType.Grid)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(gridColumnsProp);
                EditorGUILayout.PropertyField(gridVerticalWrapProp);

                TISGamepadUIButtonGroup.GridVerticalWrapMode wrapMode =
                    (TISGamepadUIButtonGroup.GridVerticalWrapMode)gridVerticalWrapProp.enumValueIndex;
                DrawWrapModeHint(wrapMode);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(12);

            if (GUILayout.Button("Rebuild Navigation", GUILayout.Height(36)))
            {
                Undo.RecordObject(group, "Rebuild Group Navigation");

                List<TISGamepadUIButton> childButtons = new List<TISGamepadUIButton>();
                foreach (Transform child in group.transform)
                {
                    TISGamepadUIButton btn = child.GetComponent<TISGamepadUIButton>();
                    if (btn != null)
                    {
                        Undo.RecordObject(btn, "Rebuild Target Link");
                        childButtons.Add(btn);
                    }
                }

                group.RebuildNavigation();

                foreach (var btn in childButtons)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(btn);
                    EditorUtility.SetDirty(btn);
                }

                EditorUtility.SetDirty(group);

                string wrapInfo = (layout == TISGamepadUIButtonGroup.GroupLayoutType.Grid)
                    ? $" | Wrap: <b>{(TISGamepadUIButtonGroup.GridVerticalWrapMode)gridVerticalWrapProp.enumValueIndex}</b>"
                    : "";
                Debug.Log($"<color=cyan><b>[GamepadUIButtonGroup]</b></color> " +
                          $"Layout: <b>{layout}</b>{wrapInfo} — " +
                          $"Đã liên kết <b>{childButtons.Count}</b> nút thành công!");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawWrapModeHint(TISGamepadUIButtonGroup.GridVerticalWrapMode mode)
        {
            EditorGUILayout.Space(3);

            string icon, desc;
            Color bg;

            if (mode == TISGamepadUIButtonGroup.GridVerticalWrapMode.ColumnWrap)
            {
                icon = "↕";
                desc = "Column Wrap — mỗi cột tự wrap độc lập.\n" +
                       "Nút đầu cột  ↑  →  nút cuối cột đó.";
                bg = new Color(0.22f, 0.22f, 0.22f, 0.85f);
            }
            else
            {
                icon = "⇅";
                desc = "Global Wrap — Left/Right tuyến tính qua hàng.\n" +
                       "Up/Down vẫn wrap trong cùng cột.";
                bg = new Color(0.25f, 0.25f, 0.25f, 0.85f);
            }

            Rect boxRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(boxRect, bg);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15 + 6);
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(24));
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
    }
}