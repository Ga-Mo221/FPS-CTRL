#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Rect Tool (phím T) cho context "Blender Precision Tools" — bản tự viết lại vì Unity
    /// không cho EditorToolContext tái sử dụng RectTool gốc (xem BlenderToolContext).
    ///
    /// Hành vi bám theo Rect Tool của Unity:
    /// • 1 RectTransform được chọn → khung chữ nhật nằm trên mặt phẳng local của chính nó,
    ///   kích thước = rt.rect (resize bằng SetSizeWithCurrentAnchors, không đổi scale).
    /// • Còn lại (Transform/Renderer, hoặc nhiều object) → bounds của toàn bộ Renderer (kể cả
    ///   con) + góc RectTransform, tính trong khung xoay theo Tools.pivotRotation (Local = theo
    ///   object active, Global = trục world). Mặt phẳng rect là mặt phẳng của khung hướng về
    ///   camera nhiều nhất (bỏ trục gần song song hướng nhìn nhất); chế độ 2D luôn là XY.
    /// • Kéo cạnh/góc → resize, kéo bên trong → di chuyển, click đứng yên bên trong → chọn
    ///   object dưới chuột (như Unity), kéo vòng pivot (chỉ 1 RectTransform, Pivot mode) → đổi
    ///   rt.pivot mà khung giữ nguyên.
    ///
    /// Phím bổ trợ (giữ trong lúc kéo):
    /// • Shift → precision mode, dùng chung BlenderEditorPreferences.PrecisionFactor như
    ///   Move/Rotate/Scale (nên KHÔNG dùng Shift = giữ tỉ lệ như Unity gốc — thay bằng tuỳ chọn
    ///   "Rect Tool: giữ tỉ lệ" trong Preferences).
    /// • Alt   → resize đối xứng quanh pivot (Pivot mode) / tâm khung (Center mode).
    /// • Ctrl/Cmd → snap theo bước EditorSnapSettings.move của Unity.
    /// </summary>
    class BlenderPrecisionRectTool : EditorTool
    {
        public static bool IsDragging { get; private set; }

        // ════════════════════════════════ KHUNG RECT ════════════════════════════════
        // Toạ độ (u,v) là toạ độ trên 2 trục u/v của khung (origin, rot), đơn vị world.
        struct RectFrame
        {
            public Vector3    origin;
            public Quaternion rot;
            public int        u, v, n;      // chỉ số trục khung (0=x,1=y,2=z); n = pháp tuyến mặt phẳng
            public Rect       rect;
            public float      depth;        // toạ độ của mặt phẳng rect trên trục n
            public Vector2    pivot;        // điểm pivot (Pivot mode) hoặc tâm (Center mode)
            public bool       lockU, lockV; // bounds suy biến trên trục đó → chỉ cho di chuyển
            public RectTransform singleRect; // != null khi khung là rect của đúng 1 RectTransform

            public Vector3 Normal => rot * (n == 0 ? Vector3.right : n == 1 ? Vector3.up : Vector3.forward);

            public Vector3 ToWorld(Vector2 p)
            {
                Vector3 l = Vector3.zero;
                l[u] = p.x; l[v] = p.y; l[n] = depth;
                return origin + rot * l;
            }

            public Vector2 ToUV(Vector3 w)
            {
                Vector3 l = Quaternion.Inverse(rot) * (w - origin);
                return new Vector2(l[u], l[v]);
            }
        }

        // Phép biến đổi affine trên mặt phẳng (u,v): p' = anchor + (p - anchor) * scale + offset.
        // Trục n (độ sâu) của từng điểm được giữ nguyên.
        struct RectMapping
        {
            public Vector2 anchor, scale, offset;

            public static RectMapping Identity => new RectMapping { scale = Vector2.one };

            public Vector2 MapUV(Vector2 p) => anchor + Vector2.Scale(p - anchor, scale) + offset;

            public Vector3 MapWorld(in RectFrame f, Vector3 w)
            {
                Vector3 l  = Quaternion.Inverse(f.rot) * (w - f.origin);
                Vector2 uv = MapUV(new Vector2(l[f.u], l[f.v]));
                l[f.u] = uv.x; l[f.v] = uv.y;
                return f.origin + f.rot * l;
            }

            public Rect MapRect(Rect r)
            {
                Vector2 a = MapUV(r.min), b = MapUV(r.max);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        class TargetState
        {
            public Transform     t;
            public RectTransform rt;
            public Vector3       pos;
            public Vector3       localScale;
            public Vector2       rectSize;
            public Vector2       rtPivot;
            public Rect          rtLocalRect;
            public Matrix4x4     localToWorld;
        }

        // ════════════════════════════════ HANDLE ════════════════════════════════
        // 0..3 góc (min-min, max-min, max-max, min-max), 4..7 cạnh (trái, phải, dưới, trên),
        // 8 = thân (di chuyển), 9 = pivot.
        const int H_BODY = 8, H_PIVOT = 9, H_COUNT = 10;
        static readonly Vector2Int[] HandleDir =
        {
            new Vector2Int(-1, -1), new Vector2Int( 1, -1), new Vector2Int( 1,  1), new Vector2Int(-1,  1),
            new Vector2Int(-1,  0), new Vector2Int( 1,  0), new Vector2Int( 0, -1), new Vector2Int( 0,  1),
        };

        static readonly int s_Hash = "BlenderPrecisionRectTool".GetHashCode();
        static readonly Color s_RectColor = new Color(0.35f, 0.6f, 1f, 0.9f);

        const float DRAG_THRESHOLD   = 4f;  // px — dưới ngưỡng này click trong thân = chọn object
        const float CORNER_PICK_PX   = 8f;
        const float PIVOT_PICK_PX    = 7f;
        const float BODY_PICK_DIST   = 4f;  // < 5px của Unity để thân chỉ thắng khi không gần cạnh/góc
        const float MIN_SCALE        = 1e-4f;

        readonly int[] ids = new int[H_COUNT];

        // Trạng thái lượt kéo
        int       activeHandle = -1;
        bool      moved;
        Vector2   mouseDownGui;
        Vector2   prevHitUV;
        Vector2   dampedDelta;
        RectFrame startFrame;
        Rect      currentRect;
        Vector2   currentPivot;
        Transform[] dragTargets;
        readonly List<TargetState> startStates = new List<TargetState>();

        public override void OnWillBeDeactivated()
        {
            if (activeHandle >= 0 && GUIUtility.hotControl == ids[activeHandle]) GUIUtility.hotControl = 0;
            activeHandle = -1;
            IsDragging   = false;
        }

        public override void OnToolGUI(EditorWindow window)
        {
            Event e = Event.current;
            for (int i = 0; i < H_COUNT; i++) ids[i] = GUIUtility.GetControlID(s_Hash, FocusType.Passive);

            // Mất hotControl giữa chừng (cửa sổ khác giành, mất MouseUp...) → tự nhả cờ, tránh
            // kẹt BlenderInputPriority.IsGizmoDragging = true mãi mãi.
            if (activeHandle >= 0 && GUIUtility.hotControl != ids[activeHandle]) EndDrag();

            Transform[] targets = activeHandle >= 0 ? dragTargets : GetTargets();
            if (targets == null || targets.Length == 0) return;

            RectFrame frame;
            if (activeHandle >= 0)
            {
                frame       = startFrame;
                frame.rect  = currentRect;
                frame.pivot = currentPivot;
            }
            else if (!TryBuildFrame(targets, window as SceneView, out frame)) return;

            switch (e.type)
            {
                case EventType.Layout:
                    if (activeHandle < 0) AddControls(frame);
                    break;

                case EventType.MouseDown:
                    if (e.button != 0 || activeHandle >= 0) break;
                    int h = System.Array.IndexOf(ids, HandleUtility.nearestControl);
                    if (h < 0 || !IsHandleEnabled(frame, h)) break;
                    BeginDrag(h, frame, targets, e);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (activeHandle < 0) break;
                    UpdateDrag(e);
                    window.Repaint();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (activeHandle < 0 || e.button != 0) break;
                    bool clickInBody = activeHandle == H_BODY && !moved;
                    EndDrag();
                    GUIUtility.hotControl = 0;
                    if (clickInBody) ClickSelect(e);
                    e.Use();
                    break;

                case EventType.Repaint:
                    Draw(frame);
                    if (activeHandle >= 0 && moved) BlenderGizmoShift.DrawDebugHUD(BlenderGizmoShift.CurrentFactor);
                    break;
            }
        }

        // ════════════════════════════════ TARGETS & BOUNDS ════════════════════════════════
        static Transform[] GetTargets() =>
            // TopLevel: tránh vừa chọn cha vừa chọn con thì con bị dịch/scale 2 lần.
            Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);

        static bool TryBuildFrame(Transform[] targets, SceneView sv, out RectFrame f)
        {
            f = default;
            Transform active = Selection.activeTransform != null ? Selection.activeTransform : targets[0];

            // 1 RectTransform duy nhất → dùng thẳng rect local của nó, giống Unity.
            if (targets.Length == 1 && targets[0] is RectTransform single)
            {
                Rect r     = single.rect;
                Vector3 ls = single.lossyScale;
                f.origin   = single.position;
                f.rot      = single.rotation;
                f.u = 0; f.v = 1; f.n = 2;
                f.depth    = 0f;
                f.rect     = Rect.MinMaxRect(
                    Mathf.Min(r.xMin * ls.x, r.xMax * ls.x), Mathf.Min(r.yMin * ls.y, r.yMax * ls.y),
                    Mathf.Max(r.xMin * ls.x, r.xMax * ls.x), Mathf.Max(r.yMin * ls.y, r.yMax * ls.y));
                f.pivot    = Tools.pivotMode == PivotMode.Pivot ? Vector2.zero : f.rect.center;
                f.singleRect = single;
                FinishDegenerate(ref f);
                return true;
            }

            f.origin = Tools.handlePosition;
            f.rot    = Tools.pivotRotation == PivotRotation.Local ? active.rotation : Quaternion.identity;

            // Bounds 3D trong khung (origin, rot).
            Quaternion inv    = Quaternion.Inverse(f.rot);
            Vector3    origin = f.origin; // local function không được bắt tham số out
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            var corners = new Vector3[4];
            void Encapsulate(Vector3 w)
            {
                Vector3 l = inv * (w - origin);
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
            }

            foreach (var t in targets)
            {
                if (t == null) continue;
                bool any = false;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    any = true;
                    // SkinnedMeshRenderer.localBounds tính theo rootBone → dùng AABB world cho chắc.
                    Bounds    b = r is SkinnedMeshRenderer ? r.bounds : r.localBounds;
                    Matrix4x4 m = r is SkinnedMeshRenderer ? Matrix4x4.identity : r.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                                (i & 2) == 0 ? b.min.y : b.max.y,
                                                (i & 4) == 0 ? b.min.z : b.max.z);
                        Encapsulate(m.MultiplyPoint3x4(c));
                    }
                }
                if (t is RectTransform rt)
                {
                    any = true;
                    rt.GetWorldCorners(corners);
                    foreach (var c in corners) Encapsulate(c);
                }
                if (!any) Encapsulate(t.position);
            }
            if (float.IsInfinity(min.x)) return false;

            // Chọn mặt phẳng: bỏ trục khung gần song song hướng nhìn nhất.
            if (sv != null && sv.in2DMode)
            {
                f.u = 0; f.v = 1; f.n = 2;
            }
            else
            {
                Vector3 viewDir = sv != null && sv.camera != null ? sv.camera.transform.forward : Vector3.forward;
                Vector3 lv = inv * viewDir;
                float ax = Mathf.Abs(lv.x), ay = Mathf.Abs(lv.y), az = Mathf.Abs(lv.z);
                if (az >= ax && az >= ay) { f.u = 0; f.v = 1; f.n = 2; }
                else if (ay >= ax)        { f.u = 0; f.v = 2; f.n = 1; }
                else                      { f.u = 2; f.v = 1; f.n = 0; }
            }

            f.rect  = Rect.MinMaxRect(min[f.u], min[f.v], max[f.u], max[f.v]);
            f.depth = (min[f.n] + max[f.n]) * 0.5f;
            f.pivot = Tools.pivotMode == PivotMode.Pivot ? f.ToUV(active.position) : f.rect.center;
            FinishDegenerate(ref f);
            return true;
        }

        // Bounds dẹt (VD Empty GameObject, Quad nhìn ngang) → khoá resize trục đó và nới khung
        // ra 1 kích thước hiển thị tối thiểu để vẫn kéo thân di chuyển được.
        static void FinishDegenerate(ref RectFrame f)
        {
            const float EPS = 1e-5f;
            float vis = HandleUtility.GetHandleSize(f.ToWorld(f.rect.center)) * 0.25f;
            f.lockU = f.rect.width  < EPS;
            f.lockV = f.rect.height < EPS;
            if (f.lockU) f.rect = new Rect(f.rect.center.x - vis, f.rect.y, vis * 2f, f.rect.height);
            if (f.lockV) f.rect = new Rect(f.rect.x, f.rect.center.y - vis, f.rect.width, vis * 2f);
        }

        // ════════════════════════════════ PICKING ════════════════════════════════
        static Vector2 RectPoint(Rect r, Vector2Int dir) => new Vector2(
            dir.x < 0 ? r.xMin : dir.x > 0 ? r.xMax : r.center.x,
            dir.y < 0 ? r.yMin : dir.y > 0 ? r.yMax : r.center.y);

        bool IsHandleEnabled(in RectFrame f, int h)
        {
            if (h == H_BODY) return true;
            if (h == H_PIVOT) return f.singleRect != null && Tools.pivotMode == PivotMode.Pivot;
            Vector2Int d = HandleDir[h];
            return !(d.x != 0 && f.lockU) && !(d.y != 0 && f.lockV);
        }

        void AddControls(in RectFrame f)
        {
            Vector2 mouse = Event.current.mousePosition;
            Vector2[] gui = new Vector2[4];
            for (int i = 0; i < 4; i++) gui[i] = HandleUtility.WorldToGUIPoint(f.ToWorld(RectPoint(f.rect, HandleDir[i])));

            // Thứ tự quan trọng: HandleUtility.AddControl ưu tiên control được thêm SAU khi
            // khoảng cách bằng nhau → thân trước, rồi cạnh, góc, pivot.
            HandleUtility.AddControl(ids[H_BODY], PointInQuad(mouse, gui) ? BODY_PICK_DIST : float.MaxValue);

            for (int h = 4; h < 8; h++)
            {
                if (!IsHandleEnabled(f, h)) continue;
                Vector2Int d = HandleDir[h];
                Vector2Int a = d.x != 0 ? new Vector2Int(d.x, -1) : new Vector2Int(-1, d.y);
                Vector2Int b = d.x != 0 ? new Vector2Int(d.x,  1) : new Vector2Int( 1, d.y);
                HandleUtility.AddControl(ids[h], HandleUtility.DistanceToLine(
                    f.ToWorld(RectPoint(f.rect, a)), f.ToWorld(RectPoint(f.rect, b))));
            }

            for (int h = 0; h < 4; h++)
                if (IsHandleEnabled(f, h))
                    HandleUtility.AddControl(ids[h], Mathf.Max(0f, Vector2.Distance(mouse, gui[h]) - CORNER_PICK_PX));

            if (IsHandleEnabled(f, H_PIVOT))
                HandleUtility.AddControl(ids[H_PIVOT], Mathf.Max(0f,
                    Vector2.Distance(mouse, HandleUtility.WorldToGUIPoint(f.ToWorld(f.pivot))) - PIVOT_PICK_PX));
        }

        static bool PointInQuad(Vector2 p, Vector2[] q)
        {
            bool hasPos = false, hasNeg = false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = q[i], b = q[(i + 1) % 4];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (cross > 0f) hasPos = true; else if (cross < 0f) hasNeg = true;
            }
            return !(hasPos && hasNeg);
        }

        // ════════════════════════════════ DRAG ════════════════════════════════
        void BeginDrag(int h, in RectFrame f, Transform[] targets, Event e)
        {
            activeHandle = h;
            moved        = false;
            mouseDownGui = e.mousePosition;
            dampedDelta  = Vector2.zero;
            startFrame   = f;
            currentRect  = f.rect;
            currentPivot = f.pivot;
            dragTargets  = targets;
            prevHitUV    = TryHitUV(f, e.mousePosition, out Vector2 uv) ? uv : Vector2.zero;

            startStates.Clear();
            foreach (var t in targets)
            {
                if (t == null) continue;
                var s = new TargetState { t = t, rt = t as RectTransform, pos = t.position, localScale = t.localScale };
                if (s.rt != null)
                {
                    s.rectSize     = s.rt.rect.size;
                    s.rtPivot      = s.rt.pivot;
                    s.rtLocalRect  = s.rt.rect;
                    s.localToWorld = s.rt.localToWorldMatrix;
                }
                startStates.Add(s);
            }

            GUIUtility.hotControl = ids[h];
            IsDragging = true;
            Undo.RecordObjects(targets, "Rect");
        }

        void EndDrag()
        {
            activeHandle = -1;
            IsDragging   = false;
            dragTargets  = null;
            startStates.Clear();
        }

        static bool TryHitUV(in RectFrame f, Vector2 guiPos, out Vector2 uv)
        {
            Ray   ray   = HandleUtility.GUIPointToWorldRay(guiPos);
            Plane plane = new Plane(f.Normal, f.ToWorld(f.rect.center));
            if (plane.Raycast(ray, out float dist)) { uv = f.ToUV(ray.GetPoint(dist)); return true; }
            uv = default;
            return false;
        }

        void UpdateDrag(Event e)
        {
            if (!moved && Vector2.Distance(e.mousePosition, mouseDownGui) <= DRAG_THRESHOLD) return;
            moved = true;

            // Precision: cộng dồn phần tăng THÔ của từng frame nhân hệ số Shift (giống Move tool),
            // nên nhấn/nhả Shift giữa chừng không làm khung nhảy.
            if (!TryHitUV(startFrame, e.mousePosition, out Vector2 hit)) return;
            dampedDelta += (hit - prevHitUV) * BlenderGizmoShift.CurrentFactor;
            prevHitUV = hit;

            Vector2 delta = dampedDelta;
            if (EditorGUI.actionKey)
            {
                Vector3 step = EditorSnapSettings.move;
                delta = new Vector2(Snap(delta.x, step[startFrame.u]), Snap(delta.y, step[startFrame.v]));
            }

            if (activeHandle == H_PIVOT) { ApplyPivot(delta); return; }

            RectMapping map = activeHandle == H_BODY
                ? new RectMapping { scale = Vector2.one, offset = delta }
                : BuildResizeMapping(HandleDir[activeHandle], delta, e.alt);

            currentRect  = map.MapRect(startFrame.rect);
            currentPivot = map.MapUV(startFrame.pivot);
            Apply(map);
        }

        static float Snap(float v, float step) => step > 1e-6f ? Mathf.Round(v / step) * step : v;

        RectMapping BuildResizeMapping(Vector2Int dir, Vector2 delta, bool symmetric)
        {
            Rect    r     = startFrame.rect;
            Vector2 pivot = startFrame.pivot;
            var     map   = RectMapping.Identity;
            map.anchor = r.center;

            float AxisScale(int d, float edgeMin, float edgeMax, float pv, float dd, out float anchor)
            {
                float edge = d > 0 ? edgeMax : edgeMin;
                anchor     = symmetric ? pv : (d > 0 ? edgeMin : edgeMax);
                float den  = edge - anchor;
                if (Mathf.Abs(den) < 1e-6f) return 1f;
                return (edge + dd - anchor) / den;
            }

            if (dir.x != 0) map.scale.x = AxisScale(dir.x, r.xMin, r.xMax, pivot.x, delta.x, out map.anchor.x);
            if (dir.y != 0) map.scale.y = AxisScale(dir.y, r.yMin, r.yMax, pivot.y, delta.y, out map.anchor.y);

            if (BlenderEditorPreferences.RectToolKeepAspect)
            {
                if (dir.x != 0 && dir.y != 0)
                {
                    // Góc: lấy trục thay đổi nhiều hơn làm chuẩn cho cả 2.
                    float s = Mathf.Abs(map.scale.x - 1f) >= Mathf.Abs(map.scale.y - 1f) ? map.scale.x : map.scale.y;
                    map.scale = new Vector2(s, s);
                }
                else if (dir.x != 0) { map.scale.y = map.scale.x; map.anchor.y = symmetric ? pivot.y : r.center.y; }
                else                 { map.scale.x = map.scale.y; map.anchor.x = symmetric ? pivot.x : r.center.x; }
            }

            if (startFrame.lockU) map.scale.x = 1f;
            if (startFrame.lockV) map.scale.y = 1f;
            map.scale.x = ClampScale(map.scale.x);
            map.scale.y = ClampScale(map.scale.y);
            return map;
        }

        static float ClampScale(float s) => Mathf.Abs(s) < MIN_SCALE ? (s < 0f ? -MIN_SCALE : MIN_SCALE) : s;

        // Hệ số scale mà phép map tác động lên 1 hướng world: chỉ hợp lệ khi hướng đó gần trùng
        // 1 trục của khung (object xoay lệch khung thì chỉ di chuyển, không scale — tránh méo).
        bool TryAxisScale(in RectMapping map, Vector3 worldDir, out float k)
        {
            Vector3 l = Quaternion.Inverse(startFrame.rot) * worldDir.normalized;
            float ax = Mathf.Abs(l.x), ay = Mathf.Abs(l.y), az = Mathf.Abs(l.z);
            int idx = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
            k = 1f;
            if (Mathf.Abs(l[idx]) < 0.999f) return false;
            if (idx == startFrame.u) k = map.scale.x;
            else if (idx == startFrame.v) k = map.scale.y;
            return true;
        }

        void Apply(in RectMapping map)
        {
            // Giống BlenderPrecisionMoveTool: record lại trước MỖI lần set để Undo bắt được.
            Undo.RecordObjects(dragTargets, "Rect");
            foreach (var s in startStates)
            {
                if (s.t == null) continue;
                Vector3 newPos = map.MapWorld(startFrame, s.pos);

                if (s.rt != null)
                {
                    // UI: đổi kích thước rect (không đổi scale) như Rect Tool gốc; lật qua cạnh
                    // đối diện thì lật dấu localScale.
                    Vector3 sc = s.localScale;
                    if (TryAxisScale(map, s.rt.rotation * Vector3.right, out float kx))
                    {
                        s.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, s.rectSize.x * Mathf.Abs(kx));
                        sc.x = s.localScale.x * Mathf.Sign(kx);
                    }
                    if (TryAxisScale(map, s.rt.rotation * Vector3.up, out float ky))
                    {
                        s.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s.rectSize.y * Mathf.Abs(ky));
                        sc.y = s.localScale.y * Mathf.Sign(ky);
                    }
                    s.rt.localScale = sc;
                    s.rt.position   = newPos;
                }
                else
                {
                    Vector3 sc = s.localScale;
                    if (TryAxisScale(map, s.t.rotation * Vector3.right,   out float k0)) sc.x *= k0;
                    if (TryAxisScale(map, s.t.rotation * Vector3.up,      out float k1)) sc.y *= k1;
                    if (TryAxisScale(map, s.t.rotation * Vector3.forward, out float k2)) sc.z *= k2;
                    s.t.localScale = sc;
                    s.t.position   = newPos;
                }
            }
        }

        // Kéo vòng pivot của 1 RectTransform: đổi rt.pivot, bù position để khung đứng yên.
        void ApplyPivot(Vector2 delta)
        {
            if (startStates.Count != 1 || startStates[0].rt == null) return;
            TargetState s = startStates[0];

            Vector3 world = startFrame.ToWorld(startFrame.pivot + delta);
            Vector3 local = s.localToWorld.inverse.MultiplyPoint3x4(world);
            Rect    r     = s.rtLocalRect;
            Vector2 newPivot = new Vector2(
                r.width  > 1e-6f ? (local.x - r.xMin) / r.width  : s.rtPivot.x,
                r.height > 1e-6f ? (local.y - r.yMin) / r.height : s.rtPivot.y);

            Vector3 newPos = s.localToWorld.MultiplyPoint3x4(new Vector3(
                r.xMin + newPivot.x * r.width, r.yMin + newPivot.y * r.height, 0f));

            Undo.RecordObject(s.rt, "Rect Pivot");
            s.rt.pivot    = newPivot;
            s.rt.position = newPos;
            currentPivot  = startFrame.ToUV(newPos);
        }

        static void ClickSelect(Event e)
        {
            // Click đứng yên trong thân khung → chọn object dưới chuột như Rect Tool gốc. Không
            // pick được gì thì giữ nguyên selection (khác click ra khoảng trống ngoài khung).
            GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picked == null) return;
            bool toggle = e.control || e.command;
            BlenderToolContext.ApplyClickSelection(picked, e.shift || toggle, toggle);
        }

        // ════════════════════════════════ VẼ ════════════════════════════════
        void Draw(in RectFrame f)
        {
            Vector3[] quad = new Vector3[4];
            for (int i = 0; i < 4; i++) quad[i] = f.ToWorld(RectPoint(f.rect, HandleDir[i]));

            int hot     = GUIUtility.hotControl;
            int nearest = HandleUtility.nearestControl;
            Color HandleColor(int h) =>
                hot == ids[h] ? Handles.selectedColor
                : hot == 0 && nearest == ids[h] ? Handles.preselectionColor
                : s_RectColor;

            using (new Handles.DrawingScope())
            {
                Color face = activeHandle == H_BODY || (hot == 0 && nearest == ids[H_BODY])
                    ? new Color(s_RectColor.r, s_RectColor.g, s_RectColor.b, 0.06f) : Color.clear;
                Handles.DrawSolidRectangleWithOutline(quad, face, s_RectColor);

                // Cạnh đang hover/kéo → tô đậm.
                for (int h = 4; h < 8; h++)
                {
                    if (!IsHandleEnabled(f, h) || (hot != ids[h] && !(hot == 0 && nearest == ids[h]))) continue;
                    Vector2Int d = HandleDir[h];
                    Vector2Int a = d.x != 0 ? new Vector2Int(d.x, -1) : new Vector2Int(-1, d.y);
                    Vector2Int b = d.x != 0 ? new Vector2Int(d.x,  1) : new Vector2Int( 1, d.y);
                    Handles.color = HandleColor(h);
                    Handles.DrawLine(f.ToWorld(RectPoint(f.rect, a)), f.ToWorld(RectPoint(f.rect, b)), 2f);
                }

                for (int h = 0; h < 4; h++)
                {
                    if (!IsHandleEnabled(f, h)) continue;
                    Handles.color = HandleColor(h);
                    float size = HandleUtility.GetHandleSize(quad[h]) * 0.04f;
                    Handles.DotHandleCap(ids[h], quad[h], f.rot, size, EventType.Repaint);
                }

                Vector3 pivotWorld = f.ToWorld(f.pivot);
                float   ps         = HandleUtility.GetHandleSize(pivotWorld);
                Handles.color = IsHandleEnabled(f, H_PIVOT) ? HandleColor(H_PIVOT) : s_RectColor;
                Handles.DrawWireDisc(pivotWorld, f.Normal, ps * 0.07f, 2f);
                Handles.DotHandleCap(0, pivotWorld, f.rot, ps * 0.015f, EventType.Repaint);
            }

            if (activeHandle < 0) AddCursors(f, quad);
        }

        void AddCursors(in RectFrame f, Vector3[] quad)
        {
            const float S = 16f;
            for (int h = 0; h < 8; h++)
            {
                if (!IsHandleEnabled(f, h)) continue;
                Vector2 p = HandleUtility.WorldToGUIPoint(f.ToWorld(RectPoint(f.rect, HandleDir[h])));
                MouseCursor cursor;
                if (h < 4)
                {
                    // Hướng chéo trên màn hình: góc so với tâm khung.
                    Vector2 c = HandleUtility.WorldToGUIPoint(f.ToWorld(f.rect.center));
                    Vector2 d = p - c;
                    cursor = d.x * d.y > 0f ? MouseCursor.ResizeUpLeft : MouseCursor.ResizeUpRight;
                }
                else
                {
                    Vector2Int dir = HandleDir[h];
                    Vector2 a = HandleUtility.WorldToGUIPoint(f.ToWorld(RectPoint(f.rect, dir.x != 0 ? new Vector2Int(dir.x, -1) : new Vector2Int(-1, dir.y))));
                    Vector2 b = HandleUtility.WorldToGUIPoint(f.ToWorld(RectPoint(f.rect, dir.x != 0 ? new Vector2Int(dir.x,  1) : new Vector2Int( 1, dir.y))));
                    Vector2 edge = b - a;
                    cursor = Mathf.Abs(edge.x) > Mathf.Abs(edge.y) ? MouseCursor.ResizeVertical : MouseCursor.ResizeHorizontal;
                }
                EditorGUIUtility.AddCursorRect(new Rect(p.x - S * 0.5f, p.y - S * 0.5f, S, S), cursor, ids[h]);
            }
        }
    }
}

/*
 * GHI CHÚ / GIỚI HẠN:
 *
 * 1) Kích hoạt: phím T hoặc nút Rect Tool trên toolbar Scene View khi context "Blender
 *    Precision Tools" đang active (BlenderToolContext.GetEditorToolType trả về tool này).
 *
 * 2) Multi-select: luôn 1 khung chung bao toàn bộ selection (giống Pivot Mode "Center" của
 *    Rect Tool gốc). Object nào xoay lệch khỏi trục khung thì chỉ được di chuyển theo, không
 *    bị scale (tránh méo) — chuyển Tools.pivotRotation sang Local để khung xoay theo object.
 *
 * 3) Khác Unity gốc: Shift = precision (nhất quán với Move/Rotate/Scale của bộ tool này),
 *    giữ tỉ lệ bật/tắt trong Blender Editor Tools > Features > "Rect Tool: giữ tỉ lệ".
 *    Không có chế độ Blueprint và không hỗ trợ kéo anchor của RectTransform.
 */
#endif
