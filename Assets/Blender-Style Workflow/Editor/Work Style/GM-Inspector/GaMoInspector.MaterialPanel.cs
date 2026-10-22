// GaMoInspector.MaterialPanel.cs  -  part of CustomsEditor (GaMo Inspector)
// Khung Material duoi Renderer
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        // ---------- Material dưới Renderer ----------
        string GetShaderName(Material m)
        {
            if (!_matShader.TryGetValue(m, out var s))
            {
                s = m.shader != null ? m.shader.name : "No Shader";
                _matShader[m] = s;
            }
            return s;
        }

        bool IsMaterialReadOnly(Material m)
        {
            if (!_matRO.TryGetValue(m, out var ro))
            {
                ro = (m.hideFlags & HideFlags.NotEditable) != 0 || IsImmutablePath(AssetDatabase.GetAssetPath(m));
                _matRO[m] = ro;
            }
            return ro;
        }

        void DrawMaterialChunk(Material m)
        {
            if (m == null || _reloading) return;
            if (!_matEditors.TryGetValue(m, out var med) || med == null)
            {
                med = UEditor.CreateEditor(m);
                _matEditors[m] = med;
            }
            if (med == null) return;

            bool open = InternalEditorUtility.GetIsInspectorExpanded(m);
            bool newOpen;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                newOpen = GUILayout.Toggle(open, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));
                var oldIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(" " + m.name, AssetPreview.GetMiniThumbnail(m)), EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIconSize);
                GUILayout.FlexibleSpace();
                GUILayout.Label(GetShaderName(m), EditorStyles.miniLabel);
            }
            if (newOpen != open) InternalEditorUtility.SetIsInspectorExpanded(m, newOpen);
            if (!newOpen) return;

            // Material built-in (Default-Material...) hoặc nằm trong Package không sửa được => làm mờ
            bool ro = IsMaterialReadOnly(m);
            bool oldEnabled = GUI.enabled;
            if (ro) GUI.enabled = false;
            try
            {
                GUILayout.Space(4);
                med.DrawHeader(); // có ô chọn Shader
                DrawEditorLikeInspector(med);
                GUILayout.Space(4);
            }
            finally { GUI.enabled = oldEnabled; }
        }

        // Material này đã được vẽ ở Renderer phía trên (dùng chung) => chỉ một dòng ghi chú, không vẽ MaterialEditor lần nữa.
        void DrawMaterialDupeNote(Material m)
        {
            if (m == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(18);
                var oldIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(" " + m.name, AssetPreview.GetMiniThumbnail(m)), EditorStyles.label, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIconSize);
                GUILayout.FlexibleSpace();
                GUILayout.Label("dùng chung, đã hiện ở Renderer phía trên", EditorStyles.miniLabel);
            }
        }

        void CleanupMaterialEditors()
        {
            List<Material> remove = null;
            foreach (var kv in _matEditors)
            {
                if (kv.Key == null || kv.Value == null || !_matUsed.Contains(kv.Key))
                {
                    if (remove == null) remove = new List<Material>();
                    remove.Add(kv.Key);
                }
            }
            if (remove == null) return;

            foreach (var key in remove)
            {
                if (_matEditors.TryGetValue(key, out var ed) && ed != null) DestroyImmediate(ed);
                _matEditors.Remove(key);
            }
        }

    }
}
#endif
