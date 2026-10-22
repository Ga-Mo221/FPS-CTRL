// GaMoInspector.StaticFlags.cs  -  part of CustomsEditor (GaMo Inspector)
// Dropdown Static (StaticEditorFlags) o header GameObject
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
        static readonly List<KeyValuePair<StaticEditorFlags, string>> StaticFlagList = BuildStaticFlagList();
        static readonly StaticEditorFlags AllStaticFlags = ComputeAllStatic();
        static readonly Dictionary<int, GUIContent> _staticGC = new Dictionary<int, GUIContent>();
        static readonly GUIContent _staticMixedGC = new GUIContent("—", "Các object có giá trị Static khác nhau");

        static List<KeyValuePair<StaticEditorFlags, string>> BuildStaticFlagList()
        {
            var list = new List<KeyValuePair<StaticEditorFlags, string>>();
            var seen = new HashSet<int>();
            foreach (var f in typeof(StaticEditorFlags).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.IsDefined(typeof(ObsoleteAttribute), false)) continue;
                int v = (int)f.GetRawConstantValue();
                if (v <= 0 || (v & (v - 1)) != 0) continue; // Nothing / Everything / flag ghép
                if (!seen.Add(v)) continue;
                list.Add(new KeyValuePair<StaticEditorFlags, string>((StaticEditorFlags)v, SplitCamel(f.Name)));
            }
            return list;
        }

        // Không dùng ObjectNames.NicifyVariableName: Unity cấm gọi nó khi static constructor chạy trong lúc
        // ScriptableObject (cửa sổ này) đang được tạo => TypeInitializationException.
        static string SplitCamel(string s)
        {
            return Regex.Replace(s, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        }

        static StaticEditorFlags ComputeAllStatic()
        {
            int v = 0;
            foreach (var kv in StaticFlagList) v |= (int)kv.Key;
            return (StaticEditorFlags)v;
        }

        static GUIContent StaticLabel(StaticEditorFlags cur)
        {
            int key = (int)cur;
            if (_staticGC.TryGetValue(key, out var gc)) return gc;
            string s;
            if (cur == 0) s = "Nothing";
            else if (cur == AllStaticFlags) s = "Everything";
            else
            {
                var sb = new StringBuilder();
                foreach (var kv in StaticFlagList)
                    if ((cur & kv.Key) != 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(kv.Value); }
                s = sb.ToString();
            }
            gc = new GUIContent(s, s);
            _staticGC[key] = gc;
            return gc;
        }

        void DrawStaticDropdown(GameObject[] gos, bool mixed)
        {
            var cur = GameObjectUtility.GetStaticEditorFlags(gos[0]) & AllStaticFlags;
            var content = mixed ? _staticMixedGC : StaticLabel(cur);
            var r = GUILayoutUtility.GetRect(content, EditorStyles.popup, GUILayout.Width(92));
            if (EditorGUI.DropdownButton(r, content, FocusType.Keyboard, EditorStyles.popup))
                ShowStaticMenu(r, gos, cur, mixed);
        }

        void ShowStaticMenu(Rect r, GameObject[] gos, StaticEditorFlags cur, bool mixed)
        {
            var targets = (GameObject[])gos.Clone();
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Nothing"), !mixed && cur == 0, () => ApplyStaticMask(targets, AllStaticFlags, false));
            menu.AddItem(new GUIContent("Everything"), !mixed && cur == AllStaticFlags, () => ApplyStaticMask(targets, AllStaticFlags, true));
            menu.AddSeparator("");
            foreach (var kv in StaticFlagList)
            {
                var flag = kv.Key;
                bool all = true;
                foreach (var g in targets)
                    if (g != null && (GameObjectUtility.GetStaticEditorFlags(g) & flag) == 0) { all = false; break; }
                menu.AddItem(new GUIContent(kv.Value), all, () => ApplyStaticMask(targets, flag, !all));
            }
            menu.DropDown(r);
        }

        static bool Mixed<T>(GameObject[] gos, Func<GameObject, T> get)
        {
            if (gos.Length < 2) return false;
            var v = get(gos[0]);
            for (int i = 1; i < gos.Length; i++)
                if (!EqualityComparer<T>.Default.Equals(get(gos[i]), v)) return true;
            return false;
        }

    }
}
#endif
