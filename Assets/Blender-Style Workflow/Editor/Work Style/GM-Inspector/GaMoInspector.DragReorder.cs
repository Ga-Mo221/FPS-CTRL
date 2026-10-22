// GaMoInspector.DragReorder.cs  -  part of CustomsEditor (GaMo Inspector)
// Hover, culling, keo-tha doi thu tu component
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
        static readonly Color OverrideBlue = new Color(0.10f, 0.55f, 0.95f);
        const string ReorderKey = "GaMo.ReorderComponent";
        static readonly int TitleDragHash = "GaMoTitleDrag".GetHashCode();
        const float CullMargin = 240f; // vùng đệm quanh vùng nhìn thấy: panel sắp lọt vào khung đã được vẽ sẵn

        UIE.IMGUIContainer _barChunk;                          // chunk thanh nút component: hover / kéo-thả chỉ vẽ lại riêng nó
        Component _pressTab, _dropTarget;                      // tab đang nhấn (chờ kéo) / mục tiêu thả đang được đánh dấu
        Vector2 _pressPos;

        // Prefab override
        readonly HashSet<Component> _ovrMod = new HashSet<Component>();   // component có property bị override
        readonly HashSet<Component> _ovrAdded = new HashSet<Component>(); // component thêm vào instance
        bool _ovrDirty = true;
        double _ovrAt;

        // Debug mode
        readonly HashSet<Component> _debug = new HashSet<Component>();
        readonly List<SerializedObject> _debugSOs = new List<SerializedObject>();
        readonly Dictionary<Type, bool> _helpCache = new Dictionary<Type, bool>();
        readonly Dictionary<Type, bool> _presetCache = new Dictionary<Type, bool>();

        // Culling
        sealed class ChunkCull { public UIE.IMGUIContainer c; public object key; public float h; public bool culled; }
        readonly List<ChunkCull> _cullList = new List<ChunkCull>();
        readonly Dictionary<object, float> _chunkH = new Dictionary<object, float>();

        // ---------- Hover ----------
        // IMGUIContainer chỉ vẽ lại khi có lý do. Rê chuột không phải lý do => nút mất highlight.
        // Bắt sự kiện con trỏ ở cấp UI Toolkit rồi đánh dấu vẽ lại RIÊNG chunk đó (không đụng các editor nặng bên dưới).
        void AddHoverRepaint(UIE.IMGUIContainer c)
        {
            c.RegisterCallback<UIE.PointerMoveEvent>(e => c.MarkDirtyRepaint());
            c.RegisterCallback<UIE.PointerLeaveEvent>(e => c.MarkDirtyRepaint());
            c.RegisterCallback<UIE.DragLeaveEvent>(e => ClearDropTarget());
        }

        void RepaintBar()
        {
            if (_barChunk != null) _barChunk.MarkDirtyRepaint(); else Repaint();
        }

        void ClearDropTarget()
        {
            if (_dropTarget == null) return;
            _dropTarget = null;
            Repaint();
        }

        // ---------- Culling ----------
        // Chunk (editor/material/debug) nằm ngoài vùng cuộn nhìn thấy: ngừng gọi OnGUI, giữ chỗ bằng chiều cao đã đo.
        // Đổi trạng thái ở đây (ngoài vòng IMGUI) để Layout và Repaint của cùng một frame không bao giờ lệch nhau.
        void EvaluateCulling()
        {
            if (_scrollV == null) return;
            for (int i = 0; i < _cullList.Count; i++)
            {
                var cs = _cullList[i];
                if (cs.c == null) continue;
                bool want = WantCull(cs);
                if (want == cs.culled) continue;
                cs.culled = want;
                if (want) cs.c.style.height = cs.h;
                else
                {
                    cs.c.style.height = UIE.StyleKeyword.Null;
                    cs.c.MarkDirtyRepaint();
                }
            }
        }

        bool WantCull(ChunkCull cs)
        {
            if (cs.h <= 1f) return false;                                   // chưa đo được chiều cao thật => chưa cull
            if (cs.c.panel == null) return false;
            if (EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0) return false; // đang gõ / kéo slider
            var fc = cs.c.focusController;
            if (fc != null && fc.focusedElement == cs.c) return false;      // đang giữ focus bàn phím

            var vp = _scrollV.contentViewport.worldBound;
            var wb = cs.c.worldBound;
            if (!(vp.width > 1f && vp.height > 1f) || float.IsNaN(wb.y) || float.IsNaN(wb.height)) return false;
            vp.yMin -= CullMargin;
            vp.yMax += CullMargin;
            return !wb.Overlaps(vp);
        }

        void MeasureChunk(ChunkCull cs)
        {
            if (cs.culled || cs.c == null) return;
            float h = cs.c.layout.height;
            if (float.IsNaN(h) || h <= 1f) return;
            cs.h = h;
            if (cs.key != null) _chunkH[cs.key] = h;
        }

        // ---------- Kéo-thả đổi thứ tự component (tab + tiêu đề panel) ----------
        // Dùng DragAndDrop của Unity (có ghost + con trỏ), dữ liệu kèm theo là Component đang kéo.
        static void StartReorderDrag(Component c, string label)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(ReorderKey, c);
            DragAndDrop.StartDrag(label);
        }

        bool CanReorder(Component dragged, Component target)
        {
            if (dragged == null || target == null || dragged == target) return false;
            if (_go == null || _gos.Count > 1) return false;               // chọn nhiều object: không đổi thứ tự
            if (dragged.gameObject != _go || target.gameObject != _go) return false;
            return !(dragged is Transform);
        }

        bool DropAfter(Component target)
        {
            var dragged = DragAndDrop.GetGenericData(ReorderKey) as Component;
            if (dragged == null) return false;
            var all = GetComps();
            return Array.IndexOf(all, dragged) < Array.IndexOf(all, target);
        }

        static void DrawDropMark(Rect r, bool vertical, bool after)
        {
            var rc = vertical
                ? new Rect(after ? r.xMax - 1f : r.x - 2f, r.y - 1f, 3f, r.height + 2f)
                : new Rect(r.x, after ? r.yMax - 2f : r.y - 1f, r.width, 3f);
            EditorGUI.DrawRect(rc, Accent);
        }

        // Xử lý DragUpdated / DragPerform của một vùng thả. Trả về true nếu vùng này nhận sự kiện.
        bool HandleReorderDrop(Rect area, Component target)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return false;
            if (!area.Contains(e.mousePosition)) return false;
            var dragged = DragAndDrop.GetGenericData(ReorderKey) as Component;
            if (dragged == null) return false;

            bool ok = CanReorder(dragged, target);
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
                if (_dropTarget != (ok ? target : null)) { _dropTarget = ok ? target : null; Repaint(); }
                e.Use();
                return true;
            }

            // DragPerform
            e.Use();
            if (ok)
            {
                DragAndDrop.AcceptDrag();
                _dropTarget = null;
                MoveComponentTo(dragged, target);
                GUIUtility.ExitGUI();
            }
            return true;
        }

        // Nhấn + kéo trên tên component ở tiêu đề panel thì bắt đầu kéo (vùng mũi tên / nút bên phải vẫn dùng bình thường)
        void HandleTitleDrag(Component c, Rect lr)
        {
            int id = GUIUtility.GetControlID(TitleDragHash, FocusType.Passive, lr);
            if (_gos.Count > 1 || c is Transform) return;
            var ev = Event.current;
            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (ev.button == 0 && lr.Contains(ev.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        _pressPos = ev.mousePosition;
                        ev.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if ((ev.mousePosition - _pressPos).sqrMagnitude > 25f)
                        {
                            GUIUtility.hotControl = 0;
                            StartReorderDrag(c, GetCompName(c.GetType()));
                        }
                        ev.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; ev.Use(); }
                    break;
            }
        }

        // Số thứ tự của c trong danh sách component HIỆN trên Inspector (bỏ HideInInspector)
        int VisibleIndexOf(GameObject go, Component c)
        {
            go.GetComponents(_tmpB);
            int idx = 0;
            foreach (var cc in _tmpB)
            {
                if (cc != null && (cc.hideFlags & HideFlags.HideInInspector) != 0) continue;
                if (cc == c) return idx;
                idx++;
            }
            return -1;
        }

        // Đưa c về đúng vị trí đang có của target. MoveComponentUp/Down đi 1 bước trong danh sách THÔ (gồm cả
        // component ẩn) nên lặp tới khi vị trí HIỆN đúng như mong muốn (có chặn vòng lặp vô hạn).
        void MoveComponentTo(Component c, Component target)
        {
            if (c == null || target == null) return;
            var go = c.gameObject;
            int want = target is Transform ? 1 : VisibleIndexOf(go, target);
            if (want < 1) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            for (int guard = 0; guard < 256; guard++)
            {
                int ci = VisibleIndexOf(go, c);
                if (ci < 0 || ci == want) break;
                bool moved = ci > want ? ComponentUtility.MoveComponentUp(c) : ComponentUtility.MoveComponentDown(c);
                if (!moved) break;
            }
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Reorder Component");

            InvalidateStruct();
            Repaint();
            RequestSync();
        }

    }
}
#endif
