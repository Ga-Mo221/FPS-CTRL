// GaMoInspector.ContextMenus.cs  -  part of CustomsEditor (GaMo Inspector)
// Menu chuot phai cho component (Copy/Paste/Reset/Remove...) va menu Missing Script
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
        static readonly List<MethodInfo> _ctxMethods = new List<MethodInfo>();
        static bool _ctxInit;
        static bool _warned;

        // Tab "Missing" (script bị mất): menu xóa các script thiếu
        void ShowMissingMenu(Rect at)
        {
            var go = _go;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Remove Missing Scripts"), false, () =>
            {
                if (go == null) return;
                Undo.RegisterFullObjectHierarchyUndo(go, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                InvalidateStruct();
                Repaint();
            });
            menu.DropDown(at);
        }

        // Chuột phải vào nút component: 6 tùy chọn nhanh.
        // (Menu đầy đủ của Unity vẫn có ở nút ⋮ trên tiêu đề panel.)
        void ShowQuickTabMenu(Component c, Rect at)
        {
            if (c == null) return;
            PopupWindow.Show(at, new TabMenuPopup(this, c)); // menu có phần Ghim màu (xem TabColors.cs)
        }

        // Chỉ hiện mục Paste khi thật sự dán được:
        //  - Chưa Copy component nào        => không hiện mục Paste nào.
        //  - Đã Copy component loại A       => luôn có "Paste ... As New"; chỉ khi bấm vào component CÙNG loại A mới có thêm "Paste ... Values".
        void AddPasteItems(GenericMenu menu, Component c, string newLabel, string valuesLabel)
        {
            var copied = CustomsComponentClipboard.CopiedType;
            if (copied == null || c == null) return;

            var go = c.gameObject;
            menu.AddItem(new GUIContent(newLabel), false, () =>
            {
                if (go == null) return;
                ComponentUtility.PasteComponentAsNew(go);
                InvalidateStruct(); Repaint(); RequestSync();
            });

            if (copied != c.GetType()) return; // khác loại: không thể dán giá trị
            menu.AddItem(new GUIContent(valuesLabel), false, () =>
            {
                if (c == null) return;
                Undo.RecordObject(c, "Paste Component Values");
                if (!ComponentUtility.PasteComponentValues(c))
                    ShowNotification(new GUIContent("Không dán được giá trị: clipboard hiện không phải component cùng loại"));
                Repaint();
            });
        }

        void ShowComponentMenu(Component c, Rect anchor)
        {
            if (TryNativeMenu(c, anchor)) return;

            if (!_warned)
            {
                _warned = true;
                Debug.Log("[GaMo] Không gọi được menu gốc của Unity, dùng menu tự dựng.");
            }
            BuildMenu(c, anchor).DropDown(anchor);
        }

        // Thử gọi menu gốc (đầy đủ cả submenu Odin) bằng reflection
        static bool TryNativeMenu(Component c, Rect anchor)
        {
            if (!_ctxInit)
            {
                _ctxInit = true;
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var m in typeof(EditorUtility).GetMethods(flags))
                {
                    if (m.Name != "DisplayObjectContextMenu") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(Rect))
                        _ctxMethods.Add(m);
                }
                // ưu tiên bản nhận 1 Object
                _ctxMethods.Sort((a, b) =>
                    (b.GetParameters()[1].ParameterType == typeof(UObject) ? 1 : 0)
                  - (a.GetParameters()[1].ParameterType == typeof(UObject) ? 1 : 0));
            }

            foreach (var m in _ctxMethods)
            {
                var ps = m.GetParameters();
                var objArg = ObjArg(ps[1].ParameterType, c);
                if (objArg == null || ps[2].ParameterType != typeof(int)) continue;

                try
                {
                    m.Invoke(null, new object[] { anchor, objArg, 0 });
                    return true;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is ExitGUIException)
                {
                    return true;
                }
                catch (Exception) { /* thử bản kế tiếp */ }
            }
            return false;
        }

        static object ObjArg(Type pt, UObject c)
        {
            if (pt == typeof(UObject)) return c;
            if (pt == typeof(UObject[])) return new UObject[] { c };
            if (pt == typeof(int)) return GetId(c, "GetInstanceID");
            if (pt.Name == "EntityId") return GetId(c, "GetEntityId");
            return null;
        }

        static object GetId(UObject c, string method)
        {
            var m = typeof(UObject).GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return m != null ? m.Invoke(c, null) : null;
        }

        // Menu tự dựng, giống menu component của Inspector gốc
        GenericMenu BuildMenu(Component c, Rect anchor)
        {
            var menu = new GenericMenu();
            var go = c.gameObject;
            var all = go.GetComponents<Component>();                 // danh sách thô (gồm component ẩn): dùng cho "bị yêu cầu bởi"
            var vis = new List<Component>(all.Length);               // danh sách như tab đang hiện (bỏ HideInInspector)
            foreach (var o in all)
                if (o != null && (o.hideFlags & HideFlags.HideInInspector) == 0) vis.Add(o);
            int idx = vis.IndexOf(c);
            bool isTransform = c is Transform;
            bool canMoveUp = !isTransform && idx > 1;
            bool canMoveDown = !isTransform && idx >= 0 && idx < vis.Count - 1;
            bool canRemove = !isTransform && !IsRequiredByOthers(c, all);

            // Reset
            menu.AddItem(new GUIContent("Reset"), false, () =>
            {
                Undo.RecordObject(c, "Reset " + c.GetType().Name);
                Unsupported.SmartReset(c);
            });
            menu.AddSeparator("");

            // Move to Front / Back
            AddItem(menu, "Move to Front", canMoveUp, () =>
            {
                while (ComponentUtility.MoveComponentUp(c)) { }
                InvalidateStruct();
                Repaint();
            });
            AddItem(menu, "Move to Back", canMoveDown, () =>
            {
                while (ComponentUtility.MoveComponentDown(c)) { }
                InvalidateStruct();
                Repaint();
            });
            menu.AddSeparator("");

            // Remove / Move
            AddItem(menu, "Remove Component", canRemove, () =>
            {
                Undo.DestroyObjectImmediate(c);
                Repaint();
            });
            AddItem(menu, "Move Up", canMoveUp, () => { MoveVisible(c, -1); InvalidateStruct(); Repaint(); });
            AddItem(menu, "Move Down", canMoveDown, () => { MoveVisible(c, +1); InvalidateStruct(); Repaint(); });

            // Copy / Paste
            menu.AddItem(new GUIContent("Copy Component"), false, () => CustomsComponentClipboard.Copy(c));
            AddPasteItems(menu, c, "Paste Component As New", "Paste Component Values");
            menu.AddSeparator("");

            // Find References In Scene
            var findRefs = FindRefsMethod();
            AddItem(menu, "Find References In Scene", findRefs != null, () => InvokeFindRefs(findRefs, c));
            menu.AddSeparator("");

            // Properties / Edit Script
            menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(c));
            var mb = c as MonoBehaviour;
            AddItem(menu, "Edit Script", mb != null, () =>
            {
                var script = MonoScript.FromMonoBehaviour(mb);
                if (script != null) AssetDatabase.OpenAsset(script);
            });
            menu.AddSeparator("");

            // Các mục plugin (Odin...) đăng ký qua [MenuItem("CONTEXT/...")]
            var pos = anchor;
            menu.AddItem(new GUIContent("Odin / Plugin..."), false, () =>
            {
                EditorApplication.delayCall += () =>
                    EditorUtility.DisplayPopupMenu(new Rect(pos.x, pos.y, 0, 0), "CONTEXT/Component",
                        new MenuCommand(c));
            });

            return menu;
        }

        // Di chuyển 1 bước theo thứ tự đang NHÌN THẤY: bước qua cả component ẩn nằm giữa để vị trí đổi thật sự.
        static void MoveVisible(Component c, int dir)
        {
            if (c == null) return;
            var go = c.gameObject;
            var raw = new List<Component>();
            go.GetComponents(raw);
            Component neighbor = null;
            int ci = raw.IndexOf(c);
            for (int i = ci + dir; i >= 0 && i < raw.Count; i += dir)
                if (raw[i] != null && (raw[i].hideFlags & HideFlags.HideInInspector) == 0) { neighbor = raw[i]; break; }
            if (neighbor == null) return;

            for (int guard = 0; guard < 512; guard++)
            {
                bool moved = dir < 0 ? ComponentUtility.MoveComponentUp(c) : ComponentUtility.MoveComponentDown(c);
                if (!moved) break;
                go.GetComponents(raw);
                int a = raw.IndexOf(c), b = raw.IndexOf(neighbor);
                if (dir < 0 ? a < b : a > b) break;
            }
        }

        static void AddItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction fn)
        {
            if (enabled) menu.AddItem(new GUIContent(label), false, fn);
            else menu.AddDisabledItem(new GUIContent(label));
        }

        static bool IsRequiredByOthers(Component c, Component[] all)
        {
            var ct = c.GetType();
            foreach (var o in all)
            {
                if (o == null || o == c) continue;
                foreach (RequireComponent rc in o.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(ct)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(ct)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(ct)))
                        return true;
                }
            }
            return false;
        }

        static MethodInfo FindRefsMethod()
        {
            var t = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow");
            if (t == null) return null;
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var m in t.GetMethods(flags))
            {
                if (m.Name != "SearchForReferencesToInstanceID") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && (ps[0].ParameterType == typeof(int) || ps[0].ParameterType.Name == "EntityId"))
                    return m;
            }
            return null;
        }

        static void InvokeFindRefs(MethodInfo m, Component c)
        {
            try
            {
                var arg = ObjArg(m.GetParameters()[0].ParameterType, c);
                if (arg != null) m.Invoke(null, new[] { arg });
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo] Find References lỗi: " + ex.Message); }
        }

    }
}
#endif