// GaMoInspector.PreviewSeek.cs  -  part of CustomsEditor (GaMo Inspector)
// Noi khung Preview animation voi GaMoPreviewTimeBridge (khi dang xem Motion cua 1 AnimatorState):
//  - Nghe SeekRequested: Inspector cua behaviour (vd MotionTimeEventBehaviour) keo / chon thoi diem => tua playhead + pose.
//  - La TimeSource: Inspector behaviour doc nguoc playhead hien tai (nut "lay thoi diem tu Preview").
//  - Dai marker o canh tren khung Preview (duoi thanh timeline): vach = thoi diem cac event, vach do = playhead; bam / keo tren dai = tua (bat dinh vach gan).
// Chi hoat dong khi Preview dang mo trong cua so nay (khong tach ra cua so goc) va dang xem Motion cua dung State.
// Moi thao tac chi doi trang thai Preview (TimeControl noi bo), khong ghi gi vao asset.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UEditor = UnityEditor.Editor;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        [NonSerialized] bool _psHasPending;
        [NonSerialized] float _psPending;
        [NonSerialized] int _psRetries;
        [NonSerialized] bool _psStripVisible;                 // dải marker có hiện ở lần Repaint trước không (để event chuột biết)
        [NonSerialized] GaMoPreviewTimeBridge.TimeSource _psTimeSource;
        [NonSerialized] GUIStyle _psLabelStyle;
        readonly List<GaMoPreviewTimeBridge.TimeMarker> _psMarkers = new List<GaMoPreviewTimeBridge.TimeMarker>();

        static readonly int PsStripHash = "GaMoPreviewMarkerStrip".GetHashCode();
        const float PsStripH = 14f, PsSnapPx = 5f;

        void PsHook()
        {
            GaMoPreviewTimeBridge.SeekRequested += PsOnSeekRequested;
            if (_psTimeSource == null) _psTimeSource = PsTryGetTime;
            GaMoPreviewTimeBridge.AddTimeSource(_psTimeSource);
        }

        void PsUnhook()
        {
            GaMoPreviewTimeBridge.SeekRequested -= PsOnSeekRequested;
            if (_psTimeSource != null) GaMoPreviewTimeBridge.RemoveTimeSource(_psTimeSource);
        }

        // Editor Preview của Motion khi cửa sổ này đang xem đúng State + Motion và Preview đang mở trong cửa sổ.
        UEditor PsEditorFor(AnimatorState state, Motion motion)
        {
            if (!_showPreview || CustomsNativePreview.HasFor(this)) return null;
            if (_asset == null || state == null || !ReferenceEquals(_asset, state)) return null;
            var pe = _motionPrevEditor;
            if (pe == null || pe.target == null || pe.target != motion) return null;
            return pe;
        }

        void PsOnSeekRequested(GaMoPreviewTimeBridge.SeekRequest req)
        {
            if (PsEditorFor(req.state, req.motion) == null) return;
            _psPending = req.normalizedTime;
            _psHasPending = true;
            _psRetries = 0;
            RepaintPreviewOnly(); // cả khi chỉ đổi event đang chọn: vạch nổi trên dải marker cũng cần vẽ lại
        }

        bool PsTryGetTime(AnimatorState state, Motion motion, out float n)
        {
            n = 0f;
            var pe = PsEditorFor(state, motion);
            return pe != null && CustomsNativePreview.TryGetNormalized(pe, out n);
        }

        // Gọi trong DrawPreviewPanel, ngay TRƯỚC khi vẽ Preview => pose của frame này đã ở đúng thời điểm.
        void PsApplyPending(UEditor ed)
        {
            if (!_psHasPending || Event.current.type != EventType.Repaint) return;
            if (!ReferenceEquals(ed, _motionPrevEditor)) { _psHasPending = false; return; }
            PsSeek(ed, _psPending);
        }

        void PsSeek(UEditor ed, float n)
        {
            // Clip: đặt đúng khoảng start/stop trước (lần đầu TimeControl còn khoảng mặc định) rồi mới quy đổi normalized.
            if (ed.target is AnimationClip clip && !CustomsNativePreview.InitClipRange(ed, clip)) { PsRetry(n); return; }
            if (CustomsNativePreview.SeekNormalized(ed, n, true)) _psHasPending = false;
            else PsRetry(n);
        }

        // TimeControl được AvatarPreview dựng lười: thử lại vài lần, quá thì bỏ (không vẽ lại vô hạn).
        void PsRetry(float n)
        {
            _psPending = n;
            _psHasPending = true;
            if (++_psRetries > 30) { _psHasPending = false; return; }
            RepaintPreviewOnly();
        }

        // =====================================================
        //  Dải marker ở cạnh trên khung Preview
        // =====================================================
        // Dải tự vẽ playhead của riêng nó (vạch đỏ) => luôn khớp với vạch event, không phụ thuộc vị trí timeline nội bộ của Unity.
        // Ngay dưới thanh Play / timeline của AvatarPreview (cạnh trên), không che dòng thời gian / frame ở đáy.
        const float PsTimeBarH = 21f;

        static Rect PsStripRect(Rect pr)
        {
            return new Rect(pr.x + 8f, pr.y + PsTimeBarH + 4f, Mathf.Max(0f, pr.width - 16f), PsStripH);
        }

        bool PsCollect(UEditor ed)
        {
            _psMarkers.Clear();
            if (!ReferenceEquals(ed, _motionPrevEditor) || !(_asset is AnimatorState st) || ed.target == null) return false;
            GaMoPreviewTimeBridge.CollectMarkers(st, ed.target as Motion, _psMarkers);
            return _psMarkers.Count > 0;
        }

        float PsTimeAt(Rect sr, float mouseX, bool snap)
        {
            float n = Mathf.Clamp01((mouseX - sr.x) / Mathf.Max(1f, sr.width));
            if (!snap) return n;
            float best = PsSnapPx;
            foreach (var m in _psMarkers)
            {
                float d = Mathf.Abs(sr.x + m.normalizedTime * sr.width - mouseX);
                if (d <= best) { best = d; n = m.normalizedTime; }
            }
            return n;
        }

        // Gọi TRƯỚC ed.DrawPreview: nuốt chuột trên dải để Preview không xoay camera khi đang tua.
        void PsStripInput(UEditor ed, Rect pr)
        {
            var ev = Event.current;
            int id = GUIUtility.GetControlID(PsStripHash, FocusType.Passive);
            if (!_psStripVisible || !ReferenceEquals(ed, _motionPrevEditor)) return;
            var sr = PsStripRect(pr);
            var hit = new Rect(sr.x - 4f, sr.y - 3f, sr.width + 8f, sr.height + 6f);
            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (ev.button != 0 || !hit.Contains(ev.mousePosition)) break;
                    GUIUtility.hotControl = id;
                    PsCollect(ed);
                    PsSeek(ed, PsTimeAt(sr, ev.mousePosition.x, true));
                    ev.Use();
                    RepaintPreviewOnly();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    PsSeek(ed, PsTimeAt(sr, ev.mousePosition.x, true));
                    ev.Use();
                    RepaintPreviewOnly();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    ev.Use();
                    break;
            }
        }

        // Gọi SAU ed.DrawPreview (chỉ ở Repaint): vẽ đè dải lên ảnh Preview.
        void PsStripPaint(UEditor ed, Rect pr)
        {
            if (Event.current.type != EventType.Repaint) return;
            _psStripVisible = PsCollect(ed);
            if (!_psStripVisible) return;
            var sr = PsStripRect(pr);
            if (sr.width < 24f) { _psStripVisible = false; return; }

            EditorGUIUtility.AddCursorRect(sr, MouseCursor.SlideArrow);
            EditorGUI.DrawRect(sr, new Color(0f, 0f, 0f, 0.45f));
            EditorGUI.DrawRect(new Rect(sr.x, sr.center.y, sr.width, 1f), new Color(1f, 1f, 1f, 0.18f));

            int sel = -1;
            for (int i = 0; i < _psMarkers.Count; i++)
            {
                if (_psMarkers[i].selected) { sel = i; continue; } // vạch đang chọn vẽ sau cùng => nằm trên
                float x = Mathf.Round(sr.x + _psMarkers[i].normalizedTime * sr.width);
                EditorGUI.DrawRect(new Rect(x - 1f, sr.y + 2f, 2f, sr.height - 4f), new Color(1f, 1f, 1f, 0.75f));
            }

            if (CustomsNativePreview.TryGetNormalized(ed, out float now))
            {
                float x = Mathf.Round(sr.x + now * sr.width);
                EditorGUI.DrawRect(new Rect(x - 0.5f, sr.y - 2f, 1f, sr.height + 4f), new Color(1f, 0.25f, 0.25f, 0.95f));
            }

            if (sel >= 0)
            {
                var m = _psMarkers[sel];
                float x = Mathf.Round(sr.x + m.normalizedTime * sr.width);
                EditorGUI.DrawRect(new Rect(x - 1.5f, sr.y - 1f, 3f, sr.height + 2f), new Color(Accent.r, Accent.g, Accent.b, 1f));
                if (!string.IsNullOrEmpty(m.label))
                {
                    if (_psLabelStyle == null) _psLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                    SetTextColor(_psLabelStyle, new Color(Accent.r, Accent.g, Accent.b, 1f));
                    var gc = new GUIContent(m.label);
                    var size = _psLabelStyle.CalcSize(gc);
                    float lx = Mathf.Clamp(x - size.x * 0.5f, pr.x + 2f, pr.xMax - size.x - 2f);
                    var lr = new Rect(lx, sr.yMax + 2f, size.x, size.y); // nhãn nằm DƯỚI dải: không đè thanh timeline phía trên
                    EditorGUI.DrawRect(lr, new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(lr, gc, _psLabelStyle);
                }
            }
        }
    }
}
#endif
