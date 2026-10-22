// GaMoInspector.IconSelectorAndEnabled.cs  -  part of CustomsEditor (GaMo Inspector)
// Nut chon icon GameObject, helper bat/tat component
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
        // ---------- Icon selector (nút icon ở đầu header, giống Inspector gốc) ----------
        static MethodInfo _iconSelectorShow;
        static bool _iconSelectorSearched;
        static GUIStyle _iconBtnStyle;

        static void DrawIconSelector(GameObject targetObj)
        {
            if (targetObj == null) return;

            if (_iconBtnStyle == null)
                _iconBtnStyle = new GUIStyle(EditorStyles.label) { padding = new RectOffset(0, 0, 0, 0) };

            var r = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));

            var oldIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(16, 16));
            var content = EditorGUIUtility.ObjectContent(targetObj, targetObj.GetType());
            content.text = null;
            content.tooltip = "Chọn icon cho GameObject";
            bool clicked = EditorGUI.DropdownButton(r, content, FocusType.Passive, _iconBtnStyle);
            EditorGUIUtility.SetIconSize(oldIconSize);

            if (clicked) ShowIconSelector(targetObj, r);
        }

        static void ShowIconSelector(UObject obj, Rect rect)
        {
            if (!_iconSelectorSearched)
            {
                _iconSelectorSearched = true;
                var t = typeof(EditorGUIUtility).Assembly.GetType("UnityEditor.IconSelector");
                if (t != null)
                    _iconSelectorShow = t.GetMethod("ShowAtPosition", BindingFlags.NonPublic | BindingFlags.Static,
                        null, new[] { typeof(UObject), typeof(Rect), typeof(bool) }, null);
            }
            if (_iconSelectorShow == null)
            {
                Debug.LogWarning("[GaMo] Không tìm thấy IconSelector nội bộ của Unity (có thể đã đổi API ở bản Unity này).");
                return;
            }
            try { _iconSelectorShow.Invoke(null, new object[] { obj, rect, true }); }
            catch (Exception ex) { Debug.LogWarning("[GaMo] Mở Icon Selector lỗi: " + ex.Message); }
        }

        // ---------- enabled helper ----------
        static bool GetEnabled(Component c) =>
            c is Behaviour b ? b.enabled : c is Renderer r ? r.enabled : c is Collider col && col.enabled;

        static void SetEnabled(Component c, bool v)
        {
            Undo.RecordObject(c, "GaMo Toggle Enabled");
            if (c is Behaviour b) b.enabled = v;
            else if (c is Renderer r) r.enabled = v;
            else if (c is Collider col) col.enabled = v;
        }

        // =====================================================
        //  Popup Add Component: tìm kiếm + nhóm theo menu + Recent + New Script
        // =====================================================
    }
}
#endif
