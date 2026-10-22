// GaMoInspector.SceneHandles.cs  -  part of CustomsEditor (GaMo Inspector)
// Hook SceneView de an Scene Gizmos/Handles cua Inspector goc
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
        // ---------- Scene handles ----------
        // Editor tạo bằng CreateEditor không nằm trong tracker của Inspector => Unity không gọi OnSceneGUI
        // (handle Collider, Light, Cinemachine...). Tự gọi qua reflection. Bỏ qua nếu Inspector gốc đang mở
        // (nó đã tự vẽ handle, gọi thêm sẽ bị vẽ đôi).
        static readonly Dictionary<Type, MethodInfo> _sceneGuiCache = new Dictionary<Type, MethodInfo>();
        static Type _inspectorWinType;
        readonly List<UEditor> _sceneEds = new List<UEditor>();
        static double _nativeChk;            // dùng chung: FindObjectsOfTypeAll chỉ chạy 1 lần / 0.25s cho TẤT CẢ cửa sổ
        static bool _nativeInsp;
        bool _sceneErrLogged;

        static MethodInfo FindSceneGUI(Type t)
        {
            if (_sceneGuiCache.TryGetValue(t, out var found)) return found;
            for (var bt = t; bt != null && bt != typeof(UEditor); bt = bt.BaseType)
            {
                try
                {
                    found = bt.GetMethod("OnSceneGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                         null, Type.EmptyTypes, null);
                }
                catch (Exception) { found = null; }
                if (found != null) break;
            }
            _sceneGuiCache[t] = found;
            return found;
        }

        readonly HashSet<Type> _noSceneGui = new HashSet<Type>(); // type component mà editor của nó không có OnSceneGUI: khỏi dựng thử lại

        // Panel thu gọn thì không có editor => handle biến mất. Với component đã chọn nhưng đang thu gọn, dựng editor
        // (dùng chung _editors, mở panel lại thì tái sử dụng) chỉ khi editor của nó thật sự có OnSceneGUI.
        void EnsureCollapsedSceneEditors()
        {
            if (_collapsed.Count == 0) return;
            foreach (var c in _selected)
            {
                if (c == null || !_collapsed.Contains(c)) continue;
                if (_editors.TryGetValue(c, out var existing) && existing != null) continue;
                var ct = c.GetType();
                if (_noSceneGui.Contains(ct)) continue;
                var targets = GetTargets(c);
                if (targets == null) continue;

                var ed = EnsureEditor(c, targets);
                if (ed == null || FindSceneGUI(ed.GetType()) == null)
                {
                    _noSceneGui.Add(ct);
                    if (ed != null) DestroyImmediate(ed);
                    _editors.Remove(c);
                    _editorTargets.Remove(c);
                }
            }
        }

        // InspectorWindow ẩn + PreviewWindow gốc do CustomsNativePreview tạo không phải Inspector thật: không được tắt handle vì chúng.
        static bool AnyForeignInspector(UObject[] wins)
        {
            foreach (var w in wins)
                if (!CustomsNativePreview.Owns(w)) return true;
            return false;
        }

        void OnSceneHook(SceneView sv)
        {
            if (_reloading || (_go == null && _asset == null)) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _nativeChk > 0.25)
            {
                _nativeChk = now;
                if (_inspectorWinType == null) _inspectorWinType = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
                _nativeInsp = _inspectorWinType != null && AnyForeignInspector(Resources.FindObjectsOfTypeAll(_inspectorWinType));
            }
            if (_nativeInsp) return;

            _sceneEds.Clear(); // snapshot: handle có thể đổi selection
            if (_asset != null)
            {
                // Chế độ Asset: editor của asset / importer (vd handle của Avatar, Sprite Editor mini...)
                if (_objectEditor != null && _objectEditor.target != null) _sceneEds.Add(_objectEditor);
                if (_importerEditor != null && _importerEditor.target != null) _sceneEds.Add(_importerEditor);
            }
            else
            {
                EnsureCollapsedSceneEditors();
                foreach (var kv in _editors)
                    if (kv.Value != null && kv.Value.target != null) _sceneEds.Add(kv.Value);
            }

            foreach (var ed in _sceneEds)
            {
                if (ed == null || ed.target == null) continue;
                var m = FindSceneGUI(ed.GetType());
                if (m == null) continue;                          // đa số editor không có OnSceneGUI: bỏ qua ngay
                if (HandledByEarlierWindow(ed.target)) continue; // cửa sổ khác đã vẽ handle của object này
                try { m.Invoke(ed, null); }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is ExitGUIException) throw ex.InnerException;
                    if (!_sceneErrLogged)
                    {
                        _sceneErrLogged = true;
                        Debug.LogWarning("[GaMo] OnSceneGUI lỗi: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                    }
                }
                catch (Exception) { }
            }
        }

    }
}
#endif
