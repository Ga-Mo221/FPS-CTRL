// GaMoInspector.UitkEditors.cs  -  part of CustomsEditor (GaMo Inspector)
// Nhan dien/ve Editor chi co CreateInspectorGUI (UI Toolkit)
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
        // ---------- Editor UI Toolkit (chỉ có CreateInspectorGUI, không có OnInspectorGUI) ----------
        static readonly Dictionary<Type, bool> _uitkOnly = new Dictionary<Type, bool>();

        static bool IsUitkOnly(Type t)
        {
            if (_uitkOnly.TryGetValue(t, out var r)) return r;
            r = false;
            try
            {
                var create = t.GetMethod("CreateInspectorGUI", AnyInst, null, Type.EmptyTypes, null);
                var onGui = t.GetMethod("OnInspectorGUI", AnyInst, null, Type.EmptyTypes, null);
                r = create != null && create.DeclaringType != typeof(UEditor)
                    && (onGui == null || onGui.DeclaringType == typeof(UEditor));
            }
            catch (Exception) { }
            _uitkOnly[t] = r;
            return r;
        }

        // Editor tự dựng giao diện bằng UI Toolkit (có override CreateInspectorGUI): Inspector gốc ưu tiên nó hơn OnInspectorGUI.
        static readonly Dictionary<Type, bool> _usesUitk = new Dictionary<Type, bool>();

        static bool UsesUitk(Type t)
        {
            if (_usesUitk.TryGetValue(t, out var r)) return r;
            r = false;
            try
            {
                var create = t.GetMethod("CreateInspectorGUI", AnyInst, null, Type.EmptyTypes, null);
                r = create != null && create.DeclaringType != typeof(UEditor);
            }
            catch (Exception) { }
            _usesUitk[t] = r;
            return r;
        }

        // Inspector gốc bật hierarchyMode + wideMode, tự tính labelWidth theo bề rộng và bọc editor trong
        // lề inspectorDefaultMargins. Cửa sổ tự dựng không có những thứ này nên Vector3, label... bị chia sai tỉ lệ.
        void DrawEditorLikeInspector(UEditor ed, bool uitkNotice = false)
        {
            // Chỉ còn dùng cho editor KHÔNG nhúng được bằng InspectorElement (importer): hiện bản IMGUI mặc định + báo rõ.
            if (uitkNotice && IsUitkOnly(ed.GetType()))
            {
                EditorGUILayout.HelpBox("Editor này dựng bằng UI Toolkit (CreateInspectorGUI): đang hiện bản IMGUI mặc định, " +
                                        "có thể thiếu phần giao diện tùy biến.", MessageType.None);
                if (GUILayout.Button("Mở bản gốc (Properties...)", EditorStyles.miniButton))
                {
                    EditorUtility.OpenPropertyEditor(ed.target);
                    GUIUtility.ExitGUI();
                }
            }

            bool oldHier = EditorGUIUtility.hierarchyMode;
            bool oldWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.hierarchyMode = true;
            EditorGUIUtility.wideMode = position.width >= 330f; // ngưỡng wide mode của Inspector
            EditorGUIUtility.labelWidth = 0;                     // 0 = tự tính theo bề rộng cửa sổ
            try
            {
                ResetFoldoutHeaderGroup(); // editor trước (cùng chunk) lỡ để FoldoutHeaderGroup mở thì không lây sang editor này
                using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                    ed.OnInspectorGUI();
            }
            finally
            {
                ResetFoldoutHeaderGroup(); // ExitGUI/exception giữa Begin...End của editor này: không để kẹt cho lần vẽ sau
                EditorGUIUtility.hierarchyMode = oldHier;
                EditorGUIUtility.wideMode = oldWide;
                EditorGUIUtility.labelWidth = 0;
            }
        }

        // =====================================================
        //  Hover · culling · kéo-thả sắp xếp · prefab override · nút header (?, Presets, Normal/Debug)
        // =====================================================
    }
}
#endif
