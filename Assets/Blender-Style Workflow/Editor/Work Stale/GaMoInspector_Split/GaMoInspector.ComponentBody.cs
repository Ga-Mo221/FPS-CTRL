// GaMoInspector.ComponentBody.cs  -  part of CustomsEditor (GaMo Inspector)
// Than cac component dang mo, tieu de panel component
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
        // ---------- Thân: các component đang mở ----------
        void BuildGoBody(UIE.VisualElement cc)
        {
            _barChunk = GoChunk(DrawComponentBarChunk, true);
            cc.Add(_barChunk);
            foreach (var m in _models) cc.Add(BuildPanel(m));
            if (_models.Count == 0)
                cc.Add(GoChunk(() => EditorGUILayout.HelpBox("Chọn một component ở hàng nút phía trên.", MessageType.Info)));
        }

        void DrawComponentBarChunk()
        {
            if (_go != null) DrawComponentBar();
        }

        UIE.VisualElement BuildPanel(PanelModel m)
        {
            var panel = new UIE.VisualElement();
            panel.style.marginBottom = 8;

            var c = m.c;
            panel.Add(GoChunk(() => DrawPanelTitle(c), true));
            if (m.collapsed) return panel;

            var body = new UIE.VisualElement();
            body.style.marginTop = 4;
            panel.Add(body);

            var ed = m.ed;
            if (m.debug)
            {
                // Chế độ Debug: vẽ mọi field serialized (kể cả m_..., private, HideInInspector) bằng SerializedObject ở InspectorMode.Debug
                var dso = new SerializedObject((UObject[])m.targets);
                SetDebugMode(dso);
                _debugSOs.Add(dso);
                body.Add(GoChunk(() => DrawDebugChunk(dso), false, true, dso));
            }
            else if (ed != null)
            {
                bool multiBad = m.targets.Length > 1 && !ed.GetType().IsDefined(typeof(CanEditMultipleObjects), true);
                if (multiBad)
                    body.Add(GoChunk(() => EditorGUILayout.HelpBox("Multi-object editing not supported.", MessageType.Info)));
                else
                {
                    // Editor có CreateInspectorGUI => nhúng bằng InspectorElement (giao diện UI Toolkit đầy đủ, như Inspector gốc).
                    var ve = UsesUitk(ed.GetType()) ? TryCreateInspectorElement(ed) : null;
                    if (ve != null) body.Add(ve);
                    else body.Add(GoChunk(() => DrawEditorChunk(ed), false, true, ed));
                }
            }

            if (m.mats != null)
            {
                foreach (var mat in m.mats) { var mm = mat; body.Add(GoChunk(() => DrawMaterialChunk(mm), false, true, mm)); }
                foreach (var mat in m.dupes) { var mm = mat; body.Add(GoChunk(() => DrawMaterialDupeNote(mm))); }
            }
            return panel;
        }

        void DrawEditorChunk(UEditor ed)
        {
            if (ed == null || ed.target == null) return;
            DrawEditorLikeInspector(ed);
            if (Event.current.type == EventType.Repaint && ed.RequiresConstantRepaint()) Repaint(); // Animator, ParticleSystem...
        }

        // Thanh tiêu đề component: mũi tên thu/mở (Alt+Click = thu/mở tất cả), kéo tên để đổi thứ tự,
        // nút ? (tài liệu), Presets, Normal/Debug, ⋮ mở menu đầy đủ. Thanh xanh bên trái = override của prefab.
        void DrawPanelTitle(Component c)
        {
            if (c == null) return;
            var targets = GetTargets(c);
            if (targets == null) return;
            if (Event.current.type == EventType.Layout) RefreshOverrides();

            bool collapsed = _collapsed.Contains(c);
            bool alt = Event.current.alt;
            bool open;
            bool single = _gos.Count <= 1;
            bool overAdded = single && _ovrAdded.Contains(c);
            bool overMod = single && _ovrMod.Contains(c);
            Rect tr, lr;

            using (var hs = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tr = hs.rect;
                open = GUILayout.Toggle(!collapsed, new GUIContent("", "Thu gọn / mở rộng"),
                    EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));

                var icon = GetCompIcon(c);
                var oldIconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                string title = " " + GetCompName(c.GetType())
                               + (targets.Length > 1 ? "  (" + targets.Length + " objects)" : "");
                string tip = single ? "Kéo-thả để đổi thứ tự component" : "";
                if (overAdded) tip += "\nComponent thêm vào prefab instance (override)";
                else if (overMod) tip += "\nCó override so với prefab gốc";
                GUILayout.Label(new GUIContent(title, icon, tip), EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIconSize);
                lr = GUILayoutUtility.GetLastRect();
                GUILayout.FlexibleSpace();

                DrawHeaderButtons(c, targets);

                var br = GUILayoutUtility.GetRect(26, 18, GUILayout.Width(26));
                if (GUI.Button(br, EditorGUIUtility.IconContent("_Menu"), EditorStyles.toolbarButton))
                {
                    ShowComponentMenu(c, br);
                    GUIUtility.ExitGUI();
                }
            }

            HandleTitleDrag(c, lr);
            HandleReorderDrop(tr, c);
            if (Event.current.type == EventType.Repaint)
            {
                if (overAdded || overMod) EditorGUI.DrawRect(new Rect(tr.x, tr.y, 3f, tr.height), OverrideBlue);
                if (_dropTarget == c) DrawDropMark(tr, false, DropAfter(c));
            }

            if (open == collapsed) // vừa bấm mũi tên
            {
                bool nowCollapsed = !open;
                if (alt)
                {
                    foreach (var sel in _selected)
                    {
                        if (sel == null) continue;
                        if (nowCollapsed) _collapsed.Add(sel); else _collapsed.Remove(sel);
                    }
                }
                else if (nowCollapsed) _collapsed.Add(c);
                else _collapsed.Remove(c);
                RequestSync(); // đổi cấu trúc cây (thân panel hiện/ẩn)
            }
        }

    }
}
#endif