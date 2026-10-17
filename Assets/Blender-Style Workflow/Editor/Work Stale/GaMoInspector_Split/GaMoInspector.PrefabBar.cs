// GaMoInspector.PrefabBar.cs  -  part of CustomsEditor (GaMo Inspector)
// Thanh Prefab (Open/Select/Overrides) va menu Overrides
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
        // ---------- Prefab bar ----------
        void DrawPrefabBar()
        {
            if (_go == null || _gos.Count > 1) return;
            var go = _go;
            if (Event.current.type == EventType.Layout) // gọi PrefabUtility 1 lần / Layout thay vì mọi event
            {
                _pfInstance = PrefabUtility.IsPartOfPrefabInstance(go);
                _pfAsset = !_pfInstance && PrefabUtility.IsPartOfPrefabAsset(go);
                _pfPath = _pfInstance ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)
                        : (_pfAsset ? AssetDatabase.GetAssetPath(go) : "");
                _pfHasRoot = _pfInstance && PrefabUtility.GetNearestPrefabInstanceRoot(go) != null; // root gần nhất (kể cả prefab lồng)
            }
            bool isInstance = _pfInstance, isAsset = _pfAsset;
            if (!isInstance && !isAsset) return;

            string path = _pfPath;
            bool hasPath = !string.IsNullOrEmpty(path);
            bool hasRoot = _pfHasRoot;

            using (new EditorGUILayout.HorizontalScope())
            {
                var oldIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(isInstance ? " Prefab" : " Prefab Asset", EditorGUIUtility.IconContent("Prefab Icon").image),
                    GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIconSize);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!hasPath))
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        var openAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (openAsset != null) AssetDatabase.OpenAsset(openAsset);
                        GUIUtility.ExitGUI();
                    }

                if (isInstance)
                {
                    using (new EditorGUI.DisabledScope(!hasPath))
                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(48)))
                        {
                            var selAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (selAsset != null)
                            {
                                Selection.activeObject = selAsset;
                                EditorGUIUtility.PingObject(selAsset);
                            }
                            GUIUtility.ExitGUI();
                        }

                    using (new EditorGUI.DisabledScope(!hasRoot))
                    {
                        var oc = new GUIContent("Overrides…", "Apply / Revert override của prefab instance chứa object này (kể cả prefab lồng)");
                        var orect = GUILayoutUtility.GetRect(oc, EditorStyles.miniButton, GUILayout.Width(76));
                        if (GUI.Button(orect, oc, EditorStyles.miniButton)) ShowOverridesMenu(go, orect);
                    }
                }
                GUILayout.Space(4);
            }
        }

        void ShowOverridesMenu(GameObject go, Rect r)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            if (root == null) return;
            string rootPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            bool hasPath = !string.IsNullOrEmpty(rootPath);
            bool has = PrefabUtility.HasPrefabInstanceAnyOverrides(root, false);

            var menu = new GenericMenu();
            AddItem(menu, "Apply All", has && hasPath, () =>
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                MarkOverridesDirty(true);
            });
            AddItem(menu, "Revert All", has, () =>
            {
                PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
                MarkOverridesDirty(true);
            });

            // Override riêng từng component của object đang xem
            if (go == _go)
            {
                bool sep = false;
                foreach (var cmp in GetComps())
                {
                    if (cmp == null) continue;
                    bool added = _ovrAdded.Contains(cmp);
                    if (!added && !_ovrMod.Contains(cmp)) continue;
                    if (!sep) { menu.AddSeparator(""); sep = true; }

                    var cc = cmp;
                    string n = GetCompName(cc.GetType());
                    string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cc);
                    string suffix = added ? " (added)" : "";
                    if (!string.IsNullOrEmpty(p))
                        menu.AddItem(new GUIContent("Apply Component/" + n + suffix), false, () => ApplyComponentOverride(cc, p, added));
                    else menu.AddDisabledItem(new GUIContent("Apply Component/" + n + suffix));
                    menu.AddItem(new GUIContent("Revert Component/" + n + suffix), false, () => RevertComponentOverride(cc, added));
                }
            }
            menu.DropDown(r);
        }

        void ApplyComponentOverride(Component c, string path, bool added)
        {
            if (c == null) return;
            if (added) PrefabUtility.ApplyAddedComponent(c, path, InteractionMode.UserAction);
            else PrefabUtility.ApplyObjectOverride(c, path, InteractionMode.UserAction);
            MarkOverridesDirty(true);
        }

        void RevertComponentOverride(Component c, bool added)
        {
            if (c == null) return;
            if (added) PrefabUtility.RevertAddedComponent(c, InteractionMode.UserAction);
            else PrefabUtility.RevertObjectOverride(c, InteractionMode.UserAction);
            MarkOverridesDirty(true);
        }

    }
}
#endif
