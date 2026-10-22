// GaMoInspector.UIToolkitShell.cs  -  part of CustomsEditor (GaMo Inspector)
// Dung cay UI Toolkit (CreateGUI), cuon chuot, keo-tha script vao cua so
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
        void CreateGUI()
        {
            _uiRoot = rootVisualElement;
            _uiRoot.Clear();
            _uiRoot.style.flexDirection = UIE.FlexDirection.Column;
            _uiSig.Clear();
            _bodyChunks.Clear();
            _cullList.Clear();
            _barChunk = null;

            _topIm = new UIE.IMGUIContainer(() => RunChunk(DrawTopChunk, 0));
            _topIm.style.flexShrink = 0; // mặc định UI Toolkit flexShrink = 1: nội dung cuộn quá dài sẽ bóp vùng này lại => bị ScrollView đè lên
            AddHoverRepaint(_topIm); // nút TopBar / Header / Prefab bar / Ping...: vẽ lại khi rê chuột để có highlight
            _uiRoot.Add(_topIm);

            _scrollV = new UIE.ScrollView(UIE.ScrollViewMode.Vertical);
            _scrollV.style.flexGrow = 1;
            _scrollV.style.flexShrink = 1;
            _scrollV.style.flexBasis = 0;  // chiếm phần còn lại, không lấy chiều cao nội dung làm cơ sở
            _scrollV.style.minHeight = 0;
            _uiRoot.Add(_scrollV);

            // Culling: panel nằm ngoài vùng nhìn thấy không vẽ IMGUI nữa (xem EvaluateCulling)
            _scrollV.verticalScroller.valueChanged += v => EvaluateCulling();
            _scrollV.contentViewport.RegisterCallback<UIE.GeometryChangedEvent>(e => EvaluateCulling());
            _uiRoot.schedule.Execute(EvaluateCulling).Every(150);

            _previewIm = new UIE.IMGUIContainer(() => RunChunk(DrawPreviewChunk, 2));
            _previewIm.style.display = UIE.DisplayStyle.None;
            _previewIm.style.flexShrink = 0;
            _previewSplit = BuildPreviewSplitter();
            _uiRoot.Add(_previewSplit);           // thanh kéo nằm NGAY TRÊN khung Preview, ngoài IMGUIContainer
            _uiRoot.Add(_previewIm);
            _uiRoot.RegisterCallback<UIE.GeometryChangedEvent>(e => ApplyPreviewHeight()); // đổi cỡ cửa sổ: kẹp lại chiều cao Preview

            // Ctrl/Cmd+cuộn chuột (cỡ nút) và Shift+cuộn chuột (lịch sử): bắt ở gốc (trước khi IMGUIContainer / ScrollView xử lý)
            _uiRoot.RegisterCallback<UIE.WheelEvent>(OnRootWheel, UIE.TrickleDown.TrickleDown);
            _uiRoot.RegisterCallback<UIE.MouseLeaveEvent>(e => { if (_hoverTab != -1) { _hoverTab = -1; RepaintBar(); } });

            // Kéo-thả script từ Project vào cửa sổ để thêm component
            _uiRoot.RegisterCallback<UIE.DragUpdatedEvent>(OnDragUpdated);
            _uiRoot.RegisterCallback<UIE.DragPerformEvent>(OnDragPerform);
            _uiRoot.RegisterCallback<UIE.DragLeaveEvent>(e => { _dropArmed = false; ClearDropTarget(); });
            _uiRoot.RegisterCallback<UIE.DragExitedEvent>(e => { _dropArmed = false; ClearDropTarget(); });

            SyncUI();
        }

        void OnRootWheel(UIE.WheelEvent e)
        {
            // Một số máy đổi Shift+cuộn sang trục ngang nên lấy trục nào có giá trị
            float d = Mathf.Abs(e.delta.y) > 0.001f ? e.delta.y : e.delta.x;
            if (Mathf.Approximately(d, 0f)) return;

            if (e.ctrlKey || e.commandKey)
            {
                SetTabScale(_tabScale - Mathf.Sign(d) * 0.15f);
                e.StopPropagation();
            }
            else if (e.shiftKey)
            {
                e.StopPropagation();
                double now = EditorApplication.timeSinceStartup;
                if (now - _histWheelAt < 0.15) return;
                _histWheelAt = now;
                try { StepHistory(d < 0 ? -1 : 1); } // cuộn lên = quay lại, cuộn xuống = tiến tới
                catch (ExitGUIException) { }
            }
        }

        // ---------- Kéo-thả script vào cửa sổ ----------
        static List<Type> DraggedScriptTypes()
        {
            var list = new List<Type>();
            foreach (var o in DragAndDrop.objectReferences)
            {
                var ms = o as MonoScript;
                if (ms == null) continue;
                var t = ms.GetClass();
                if (t != null && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType) list.Add(t);
            }
            return list;
        }

        void OnDragUpdated(UIE.DragUpdatedEvent e)
        {
            _dropArmed = false;
            if (_asset != null || _go == null) return;
            if (DraggedScriptTypes().Count == 0) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dropArmed = true;
        }

        void OnDragPerform(UIE.DragPerformEvent e)
        {
            if (!_dropArmed) return;
            _dropArmed = false;
            var types = DraggedScriptTypes();
            if (types.Count == 0 || _go == null) return;

            DragAndDrop.AcceptDrag();
            foreach (var g in ValidGos())
            {
                if (g == null) continue;
                foreach (var t in types) Undo.AddComponent(g, t); // thêm cho TẤT CẢ object đang chọn, giống nút Add Component
            }
            foreach (var t in types) CustomsRecentComponents.Record(t);
            e.StopPropagation();
            Repaint();
            RequestSync();
        }

    }
}
#endif