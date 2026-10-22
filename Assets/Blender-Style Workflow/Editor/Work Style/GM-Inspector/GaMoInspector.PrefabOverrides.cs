// GaMoInspector.PrefabOverrides.cs  -  part of CustomsEditor (GaMo Inspector)
// Theo doi override cua Prefab instance
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
        // ---------- Prefab override ----------
        void OnPrefabUpdated(GameObject instance) { MarkOverridesDirty(true); }

        void MarkOverridesDirty(bool now)
        {
            _ovrDirty = true;
            if (now) _ovrAt = 0;
            Repaint();
            RequestSync();
        }

        // Quét override của object đang xem. Gộp đợt 0.25s (kéo slider liên tục không quét lại mỗi frame).
        void RefreshOverrides()
        {
            if (!_ovrDirty) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _ovrAt < 0.25) return;
            _ovrAt = now;
            _ovrDirty = false;
            _ovrMod.Clear();
            _ovrAdded.Clear();

            if (_go == null || _asset != null || _gos.Count > 1 || !PrefabUtility.IsPartOfPrefabInstance(_go)) return;
            try
            {
                // Quét từng prefab instance lồng nhau, từ trong ra ngoài
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(_go);
                for (int guard = 0; root != null && guard < 16; guard++)
                {
                    foreach (var o in PrefabUtility.GetObjectOverrides(root, false))
                    {
                        var comp = o.instanceObject as Component;
                        if (comp != null && comp.gameObject == _go) _ovrMod.Add(comp);
                    }
                    var parent = root.transform.parent;
                    root = parent != null ? PrefabUtility.GetNearestPrefabInstanceRoot(parent.gameObject) : null;
                }
                foreach (var comp in GetComps())
                    if (comp != null && PrefabUtility.IsAddedComponentOverride(comp)) _ovrAdded.Add(comp);
            }
            catch (Exception) { /* API override lệch theo bản Unity: bỏ qua, chỉ mất chỉ báo xanh */ }
        }

    }
}
#endif
