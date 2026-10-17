// GaMoInspector.ComponentSelectionState.cs  -  part of CustomsEditor (GaMo Inspector)
// Luu/khoi phuc component dang chon, don dep Editor
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
        void SaveStateOfCurrent()
        {
            if (_go == null || _gos.Count > 1 || _selected.Count == 0) return;

            // Dùng lại HashSet đã có (không cấp phát mới): hàm này giờ còn được gọi trong OnBeforeSerialize
            if (!_perObject.TryGetValue(_go, out var set) || set == null) set = new HashSet<Type>();
            else set.Clear();
            foreach (var sel in _selected)
                if (sel != null) set.Add(sel.GetType());
            if (set.Count > 0) _perObject[_go] = set;

            if (_perObject.Count > 512) // hysteresis: dọn xuống <=256 (bỏ object đã xóa + mục cũ nhất) rồi mới dọn lại; trước đây >256 là dựng lại MỖI lần gọi
            {
                var alive = new Dictionary<GameObject, HashSet<Type>>(256);
                int skip = _perObject.Count - 256;
                foreach (var kv in _perObject)
                {
                    if (skip-- > 0) continue;
                    if (kv.Key != null) alive[kv.Key] = kv.Value;
                }
                if (_go != null && set.Count > 0) alive[_go] = set;
                _perObject = alive;
            }
        }

        // Chọn các component của object khớp danh sách type: đúng type trước, không có thì lấy type tương thích.
        void SelectByTypes(Component[] cs, HashSet<Type> types)
        {
            foreach (var t in types)
            {
                bool exact = false;
                foreach (var c in cs)
                    if (c != null && c.GetType() == t) { _selected.Add(c); exact = true; }
                if (exact) continue;

                // Không có đúng type => lấy component tương thích đầu tiên
                // (Transform <-> RectTransform, Box <-> Sphere Collider, Mesh <-> Skinned Mesh Renderer...)
                foreach (var c in cs)
                {
                    if (c == null || _selected.Contains(c) || !IsCompatible(t, c.GetType())) continue;
                    _selected.Add(c);
                    break;
                }
            }
        }

        static bool IsCompatible(Type a, Type b)
        {
            if (a.IsAssignableFrom(b) || b.IsAssignableFrom(a)) return true; // vd Transform <-> RectTransform
            return SameFamily<Renderer>(a, b) || SameFamily<Collider>(a, b) || SameFamily<Collider2D>(a, b);
        }

        static bool SameFamily<T>(Type a, Type b) => typeof(T).IsAssignableFrom(a) && typeof(T).IsAssignableFrom(b);

        // Ghi nhớ các tab đang mở (theo type) để áp lại khi đổi sang object khác, kể cả sau recompile / vào Play.
        void RememberSelectedTypes()
        {
            _lastTypes = new HashSet<Type>();
            _lastTypeNames = new List<string>();
            foreach (var sel in _selected)
            {
                if (sel == null) continue;
                var t = sel.GetType();
                if (_lastTypes.Add(t)) _lastTypeNames.Add(TypeName(t));
            }

            foreach (var g in _gos)
                if (g != null) _perObject[g] = new HashSet<Type>(_lastTypes);
        }

        void ClearEditors()
        {
            if (_scrollV != null) _scrollV.style.display = UIE.DisplayStyle.None; // SyncUI (đã xếp lịch) sẽ bật lại sau khi dựng cây mới
            foreach (var kv in _editors)
                if (kv.Value != null) DestroyImmediate(kv.Value);
            _editors.Clear();
            _editorTargets.Clear();
            foreach (var kv in _matEditors)
                if (kv.Value != null) DestroyImmediate(kv.Value);
            _matEditors.Clear();
            _chunkH.Clear();
        }

        void CleanupEditors()
        {
            _collapsed.RemoveWhere(cc => cc == null);
            _debug.RemoveWhere(cc => cc == null);
            List<Component> remove = null;
            foreach (var kv in _editors)
            {
                if (kv.Key == null || !_selected.Contains(kv.Key))
                {
                    if (remove == null) remove = new List<Component>();
                    remove.Add(kv.Key);
                }
            }
            if (remove == null) return;

            foreach (var key in remove)
            {
                if (_editors.TryGetValue(key, out var ed) && ed != null) DestroyImmediate(ed);
                _editors.Remove(key);
                _editorTargets.Remove(key);
            }
        }

        // =====================================================
        //  UI Toolkit shell
        //  Cửa sổ giờ là cây UI Toolkit: [IMGUIContainer: TopBar + Header] [ScrollView: thanh nút + từng panel] [Preview].
        //  Phần IMGUI cũ giữ nguyên (mỗi mảnh nằm trong một IMGUIContainer); editor chỉ có CreateInspectorGUI
        //  được nhúng bằng InspectorElement nên hiện đúng giao diện UI Toolkit của nó.
        // =====================================================
        sealed class PanelModel
        {
            public Component c;
            public Component[] targets;
            public bool collapsed;
            public bool debug;            // chế độ Debug (hiện toàn bộ field serialized)
            public UEditor ed;
            public List<Material> mats;   // material "thuộc" panel này (đã loại trùng với panel phía trên)
            public List<Material> dupes;  // material đã được vẽ ở Renderer phía trên: chỉ hiện 1 dòng ghi chú
        }

    }
}
#endif
