#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GaMo.TrueInputSystem
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TISGamepadUIButtonGroup))]
    [InitializeOnLoad]
    public class TISGamepadUIButtonGroupEditor : UnityEditor.Editor
    {
        private SerializedProperty layoutTypeProp;
        private SerializedProperty gridColumnsProp;
        private SerializedProperty gridVerticalWrapProp;
        
        static TISGamepadUIButtonGroupEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }
        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<TISGamepadUIButtonGroup>("GridGamepadButton");


        private void OnEnable()
        {
            layoutTypeProp = serializedObject.FindProperty("layoutType");
            gridColumnsProp = serializedObject.FindProperty("gridColumns");
            gridVerticalWrapProp = serializedObject.FindProperty("gridVerticalWrap");

            //SetComponentIcon();
        }

        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "GridGamepadButton");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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

            if (GUILayout.Button("Rebuild Navigation", GUILayout.Height(20)))
            {
                // Multi-edit: rebuild cho tất cả group đang chọn
                int totalLinked = 0;
                foreach (var tg in targets)
                {
                    var g = tg as TISGamepadUIButtonGroup;
                    if (g != null) totalLinked += RebuildOneGroup(g);
                }

                string wrapInfo = (layout == TISGamepadUIButtonGroup.GroupLayoutType.Grid)
                    ? $" | Wrap: <b>{(TISGamepadUIButtonGroup.GridVerticalWrapMode)gridVerticalWrapProp.enumValueIndex}</b>"
                    : "";
                Debug.Log($"<color=cyan><b>[GamepadUIButtonGroup]</b></color> " +
                          $"Layout: <b>{layout}</b>{wrapInfo} — " +
                          $"Đã liên kết <b>{totalLinked}</b> nút ({targets.Length} group) thành công!");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static int RebuildOneGroup(TISGamepadUIButtonGroup group)
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
            return childButtons.Count;
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
#endif