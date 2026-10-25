// GaMoInspector.HeaderButtons.cs  -  part of CustomsEditor (GaMo Inspector)
// Nut '?', Presets, Normal/Debug o tieu de panel component
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
        // ---------- Nút header: ? / Presets / Normal-Debug ----------
        static GUIContent _helpContent, _presetContent;
        const string ModeTip = "Chế độ hiển thị: Normal = giao diện thường, Debug = toàn bộ field serialized (kể cả m_..., private, ẩn)";
        static readonly GUIContent _modeNormalGC = new GUIContent("Normal", ModeTip);
        static readonly GUIContent _modeDebugGC = new GUIContent("Debug", ModeTip);
        static MethodInfo _presetShow, _presetExcluded;
        static bool _presetInit;
        static PropertyInfo _soInspectorMode;
        static bool _soInspectorModeInit;

        static GUIContent IconOrText(string icon, string fallback, string tip)
        {
            var tex = EditorGUIUtility.FindTexture(icon); // trả null (không log lỗi) nếu icon không có ở bản Unity này
            return tex != null ? new GUIContent(tex, tip) : new GUIContent(fallback, tip);
        }

        void DrawHeaderButtons(Component c, Component[] targets)
        {
            if (HasHelp(c))
            {
                if (_helpContent == null) _helpContent = IconOrText("_Help", "?", "Mở tài liệu hướng dẫn của component");
                if (GUILayout.Button(_helpContent, EditorStyles.toolbarButton, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    Help.ShowHelpForObject(c);
                    GUIUtility.ExitGUI();
                }
            }

            if (PresetsAvailable(c))
            {
                if (_presetContent == null) _presetContent = IconOrText("Preset.Context", "P", "Presets: áp / lưu preset cho component");
                if (GUILayout.Button(_presetContent, EditorStyles.toolbarButton, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    ShowPresetSelector(targets);
                    GUIUtility.ExitGUI();
                }
            }

            bool dbg = _debug.Contains(c);
            var mode = dbg ? _modeDebugGC : _modeNormalGC;
            bool newDbg = GUILayout.Toggle(dbg, mode, EditorStyles.toolbarButton, GUILayout.Width(52), GUILayout.Height(18));
            if (newDbg != dbg)
            {
                if (newDbg) _debug.Add(c); else _debug.Remove(c);
                RequestSync(); // thân panel đổi loại => dựng lại cây
            }
        }

        bool HasHelp(Component c)
        {
            var t = c.GetType();
            if (!_helpCache.TryGetValue(t, out var r))
            {
                try { r = Help.HasHelpForObject(c); } catch (Exception) { r = false; }
                _helpCache[t] = r;
            }
            return r;
        }

        static void InitPresets()
        {
            if (_presetInit) return;
            _presetInit = true;
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                var sel = asm.GetType("UnityEditor.Presets.PresetSelector");
                var preset = asm.GetType("UnityEditor.Presets.Preset");
                if (sel != null)
                {
                    // Chọn overload ngắn nhất: ShowSelector(Object[] targets, Preset current, bool createNewAllowed, ...)
                    foreach (var m in sel.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (m.Name != "ShowSelector") continue;
                        var ps = m.GetParameters();
                        if (ps.Length < 3 || ps[0].ParameterType != typeof(UObject[]) || ps[2].ParameterType != typeof(bool)) continue;
                        if (_presetShow == null || ps.Length < _presetShow.GetParameters().Length) _presetShow = m;
                    }
                }
                if (preset != null)
                    _presetExcluded = preset.GetMethod("IsObjectExcludedFromPresets",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(UObject) }, null);
            }
            catch (Exception) { }
        }

        bool PresetsAvailable(Component c)
        {
            InitPresets();
            if (_presetShow == null) return false;
            var t = c.GetType();
            if (!_presetCache.TryGetValue(t, out var ok))
            {
                ok = true;
                try { if (_presetExcluded != null) ok = !(bool)_presetExcluded.Invoke(null, new object[] { c }); }
                catch (Exception) { ok = true; }
                _presetCache[t] = ok;
            }
            return ok;
        }

        static void ShowPresetSelector(Component[] targets)
        {
            InitPresets();
            if (_presetShow == null) return;
            try
            {
                var ps = _presetShow.GetParameters();
                var args = new object[ps.Length];
                args[0] = (UObject[])targets;
                args[1] = null;
                args[2] = true;
                for (int i = 3; i < ps.Length; i++)
                {
                    object d = ps[i].IsOptional ? ps[i].DefaultValue : null;
                    args[i] = d is DBNull ? null : d;
                }
                _presetShow.Invoke(null, args);
            }
            catch (Exception ex) { Debug.LogWarning("[GaMo] Mở Preset Selector lỗi: " + ex.Message); }
        }

        // ---------- Debug mode ----------
        static void SetDebugMode(SerializedObject so)
        {
            if (!_soInspectorModeInit)
            {
                _soInspectorModeInit = true;
                _soInspectorMode = typeof(SerializedObject).GetProperty("inspectorMode", AnyInst); // internal ở nhiều bản Unity
            }
            try { if (_soInspectorMode != null) _soInspectorMode.SetValue(so, InspectorMode.Debug, null); }
            catch (Exception) { }
        }

        void DisposeDebugSOs()
        {
            foreach (var so in _debugSOs)
            {
                try { if (so != null) so.Dispose(); } catch (Exception) { }
            }
            _debugSOs.Clear();
        }

        void DrawDebugChunk(SerializedObject so)
        {
            if (so == null || so.targetObject == null) return;
            bool oldHier = EditorGUIUtility.hierarchyMode;
            bool oldWide = EditorGUIUtility.wideMode;
            EditorGUIUtility.hierarchyMode = true;
            EditorGUIUtility.wideMode = position.width >= 330f;
            EditorGUIUtility.labelWidth = 0;
            try
            {
                so.Update();
                using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                {
                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.Next(enter))
                    {
                        enter = false;
                        using (new EditorGUI.DisabledScope(it.propertyPath == "m_Script"))
                            EditorGUILayout.PropertyField(it, true);
                    }
                }
                so.ApplyModifiedProperties();
            }
            finally
            {
                EditorGUIUtility.hierarchyMode = oldHier;
                EditorGUIUtility.wideMode = oldWide;
                EditorGUIUtility.labelWidth = 0;
            }
        }

    }
}
#endif
