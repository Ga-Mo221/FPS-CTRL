// GaMoInspector.HeaderGameObject.cs  -  part of CustomsEditor (GaMo Inspector)
// Header GameObject (ten, tag, layer, static) khi multi-edit
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
        // ---------- Header GameObject (multi-edit) ----------
        void DrawHeader()
        {
            var gos = ValidGos();
            if (gos.Length == 0) return;
            var go0 = gos[0];

            // Mixed() quét mọi object (g.name/g.tag cấp phát string) => chỉ tính ở Layout, các event khác dùng lại.
            // Chọn nhiều object: chỉ tính lại khi dữ liệu đổi (_dataVer), số object đổi, hoặc quá hạn (Play mode 0.25s, Edit 2s).
            if (Event.current.type == EventType.Layout)
            {
                double now = EditorApplication.timeSinceStartup;
                if (gos.Length < 2)
                {
                    _mixActive = _mixName = _mixStatic = _mixTag = _mixLayer = false;
                    _mixVer = -1;
                }
                else if (_mixVer != _dataVer || _mixCount != gos.Length
                         || now - _mixAt > (EditorApplication.isPlaying ? 0.25 : 2.0))
                {
                    _mixActive = Mixed(gos, g => g.activeSelf);
                    _mixName   = Mixed(gos, g => g.name);
                    _mixStatic = Mixed(gos, g => GameObjectUtility.GetStaticEditorFlags(g));
                    _mixTag    = Mixed(gos, g => g.tag);
                    _mixLayer  = Mixed(gos, g => g.layer);
                    _mixVer = _dataVer; _mixCount = gos.Length; _mixAt = now;
                }
            }
            bool mixActive = _mixActive, mixName = _mixName, mixStatic = _mixStatic, mixTag = _mixTag, mixLayer = _mixLayer;

            bool activeCh, nameCh, tagCh, layerCh;
            bool active;
            string n, tag;
            int layer;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.showMixedValue = mixActive;
                EditorGUI.BeginChangeCheck();
                active = EditorGUILayout.Toggle(go0.activeSelf, GUILayout.Width(16));
                activeCh = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = mixName;
                EditorGUI.BeginChangeCheck();
                n = EditorGUILayout.DelayedTextField(go0.name); // chỉ ghi khi Enter / rời ô => không spam Undo mỗi phím
                nameCh = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = false;
                GUILayout.Label("Static", GUILayout.Width(34));
                DrawStaticDropdown(gos, mixStatic); // menu tự dựng: chỉ liệt kê flag không obsolete
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 32;

                EditorGUI.showMixedValue = mixTag;
                EditorGUI.BeginChangeCheck();
                tag = EditorGUILayout.TagField("Tag", go0.tag);
                tagCh = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = mixLayer;
                EditorGUI.BeginChangeCheck();
                layer = EditorGUILayout.LayerField("Layer", go0.layer);
                layerCh = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = false;
                EditorGUIUtility.labelWidth = 0;
            }

            // Mỗi ô chỉ ghi đè ĐÚNG giá trị của nó lên mọi object đang chọn (không đụng các ô khác).
            if (activeCh)
            {
                Undo.RecordObjects(gos, "GaMo Set Active");
                foreach (var g in gos) g.SetActive(active);
            }
            if (nameCh)
            {
                Undo.RecordObjects(gos, "GaMo Rename");
                foreach (var g in gos) g.name = n;
            }
            if (tagCh)
            {
                // Unity mặc định KHÔNG hỏi khi đổi Tag nên ở đây cũng không hỏi.
                Undo.RecordObjects(gos, "GaMo Change Tag");
                foreach (var g in gos) g.tag = tag;
            }
            if (layerCh) ApplyLayer(gos, layer);
            if (activeCh || nameCh || tagCh || layerCh) { _dataVer++; _mixAt = 0; }
        }

        static bool AnyChildren(GameObject[] gos)
        {
            foreach (var g in gos)
                if (g != null && g.transform.childCount > 0) return true;
            return false;
        }

        static GameObject[] WithChildren(GameObject[] gos)
        {
            var set = new HashSet<GameObject>();
            foreach (var g in gos)
            {
                if (g == null) continue;
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                    set.Add(t.gameObject);
            }
            var arr = new GameObject[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        // Giống Inspector gốc: có con thì hỏi "Yes, change children / No, this object only / Cancel".
        // DisplayDialogComplex trả về: 0 = ok (Yes), 1 = cancel, 2 = alt (No).
        // Gọi từ callback của GenericMenu (ngoài vòng OnGUI) nên không cần ExitGUI sau dialog.
        // Chỉ bật/tắt các bit trong mask trên từng object, giữ nguyên các bit khác (multi-edit không bị đè về cùng một giá trị).
        void ApplyStaticMask(GameObject[] gos, StaticEditorFlags mask, bool on)
        {
            var list = new List<GameObject>(gos.Length);
            foreach (var g in gos) if (g != null) list.Add(g);
            if (list.Count == 0) return;
            var arr = list.ToArray();

            bool withChildren = false;
            if (AnyChildren(arr))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Change Static Flags",
                    "Do you want to change the static flags for all the child objects as well?",
                    "Yes, change children", "Cancel", "No, this object only");
                if (choice == 1) return; // Cancel: không đổi gì
                withChildren = choice == 0;
            }

            var targets = withChildren ? WithChildren(arr) : arr;
            Undo.RecordObjects(targets, "Change Static Flags");
            foreach (var g in targets)
            {
                var f = GameObjectUtility.GetStaticEditorFlags(g);
                f = on ? (f | mask) : (f & ~mask);
                GameObjectUtility.SetStaticEditorFlags(g, f);
            }
            _dataVer++; _mixAt = 0;
            Repaint();
        }

        void ApplyLayer(GameObject[] gos, int layer)
        {
            bool dialog = false, withChildren = false;
            if (AnyChildren(gos))
            {
                dialog = true;
                string layerName = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(layerName)) layerName = layer.ToString();
                int choice = EditorUtility.DisplayDialogComplex(
                    "Change Layer",
                    "Do you want to set layer to " + layerName + " for all child objects as well?",
                    "Yes, change children", "Cancel", "No, this object only");
                if (choice == 1) GUIUtility.ExitGUI();   // Cancel
                withChildren = choice == 0;
            }

            var targets = withChildren ? WithChildren(gos) : gos;
            Undo.RecordObjects(targets, "Change Layer");
            foreach (var g in targets) g.layer = layer;

            if (dialog) GUIUtility.ExitGUI();
        }

    }
}
#endif
