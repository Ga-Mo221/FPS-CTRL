#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Cho phép giữ Shift để làm chậm (precision mode) khi click-kéo trực tiếp gizmo
    /// Move/Rotate/Scale — dùng chung BlenderEditorPreferences.PrecisionFactor với session
    /// G/R/S (BlenderTransformSession).
    ///
    /// v2 — sửa 2 lỗi so với bản trước:
    /// 1) Mất khả năng click chọn object trong Scene View: nguyên nhân là hành vi "click để
    ///    chọn" của Unity nằm trong EditorToolContext (GameObjectToolContext), không nằm
    ///    trong từng EditorTool. Khi thay hẳn context bằng BlenderToolContext mà không tự
    ///    viết lại phần này, Unity không còn tự chọn object khi click nữa. Đã thêm lại logic
    ///    click-select cơ bản trong BlenderToolContext.OnToolGUI.
    /// 2) Shift chưa làm chậm được: đổi sang cách tự theo dõi hoàn toàn độc lập (dùng
    ///    GUIUtility.hotControl để biết đang giữa 1 lượt kéo), không dựa vào giả định về
    ///    cách Unity nội bộ tái sử dụng tham số truyền vào Handles.PositionHandle /
    ///    RotationHandle giữa các frame — giống hệt cách đã áp dụng ổn cho Scale.
    ///    Đồng thời thêm 1 dòng HUD nhỏ hiện ngay trong lúc kéo để xác nhận trực quan
    ///    Shift có đang được nhận và hệ số đang áp dụng là bao nhiêu.
    /// </summary>

    // ════════════════════════════════ CONTEXT ════════════════════════════════
    [EditorToolContext("Blender Precision Tools")]
    class BlenderToolContext : EditorToolContext
    {
        protected override Type GetEditorToolType(Tool tool)
        {
            switch (tool)
            {
                case Tool.Move:   return typeof(BlenderPrecisionMoveTool);
                case Tool.Rotate: return typeof(BlenderPrecisionRotateTool);
                case Tool.Scale:  return typeof(BlenderPrecisionScaleTool);
                case Tool.Rect:   return typeof(BlenderPrecisionRectTool); // xem BlenderPrecisionRectTool.cs
                // Transform Tool (gộp) không được override ở đây nên sẽ bị tắt trong lúc
                // context này đang active — giới hạn của API Unity.
                default: return null;
            }
        }

        // Khôi phục lại hành vi "click để chọn object" mà Unity KHÔNG tự cung cấp nữa
        // một khi context mặc định (GameObjectToolContext) bị thay thế.
        //
        // QUAN TRỌNG: OnToolGUI của context luôn chạy TRƯỚC OnToolGUI của tool (Move/Rotate/
        // Scale) trong cùng 1 frame. Vì vậy ngay tại thời điểm MouseDown, gizmo chưa kịp được
        // vẽ nên GUIUtility.hotControl vẫn = 0 dù người dùng đang click trúng gizmo — nếu xử lý
        // chọn/bỏ-chọn ngay lúc này sẽ nhầm. Giải pháp: KHÔNG quyết định ở MouseDown, mà đợi
        // đến MouseDrag/MouseUp (khi hotControl đã kịp phản ánh đúng do gizmo grab từ event
        // trước) để biết chắc đây là: kéo gizmo (bỏ qua), click đứng yên (chọn 1 object),
        // hay có kéo rê một đoạn (quét chọn nhiều bằng PickRectObjects).
        bool       awaitingClickResolution;
        bool       boxSelectActive;
        Vector2    mouseDownScreenPos;
        bool       mouseDownAdditive;
        bool       mouseDownToggle;
        GameObject mouseDownPickedObject;

        const float DRAG_THRESHOLD = 4f;

        // FIX (mất quét chọn do BonePicking giành hotControl giữa chừng): trước đây
        // BlenderInputPriority.IsGizmoDragging chỉ phản ánh trạng thái của 3 EditorTool
        // Move/Rotate/Scale (BlenderGizmoDragState.AnyDragging), KHÔNG hề biết rằng
        // BlenderToolContext đang giữa 1 cử chỉ click/box-select (từ MouseDown tới khi
        // được resolve ở MouseDrag/MouseUp). Khi Selection rỗng (hoặc đơn giản là chưa
        // kịp kéo trúng gizmo), 3 tool kia không "dragging", nên IsGizmoDragging() trả
        // false suốt lúc đang quét chọn -> BonePicking (ưu tiên thấp hơn GizmoDrag) tưởng
        // đường trống, tự do giành GUIUtility.hotControl ngay giữa cử chỉ kéo của người
        // dùng -> nhánh "GUIUtility.hotControl != 0" bên dưới hiểu nhầm là đang giữ gizmo,
        // âm thầm huỷ toàn bộ click/box-select. Sửa: Context tự báo cáo trạng thái của
        // chính nó qua cờ này, gộp vào BlenderInputPriority.IsGizmoDragging (xem
        // BlenderToolContextActivator) để BonePicking/SceneNavigation biết nhường đúng lúc.
        public static bool IsResolvingClick { get; private set; }

        public override void OnToolGUI(EditorWindow window)
        {
            Event e = Event.current;
            if (e == null || e.button != 0 || e.alt) return;

            //if (e.rawType == EventType.MouseDown || e.rawType == EventType.MouseDrag || e.rawType == EventType.MouseUp)
                //Debug.Log($"[BoxSelectDebug] ENTER rawType={e.rawType} type={e.type} hotControl={GUIUtility.hotControl} IsFreeFor(GizmoDrag)={BlenderInputPriority.IsFreeFor(BlenderInputPriority.Owner.GizmoDrag)}");

            // Pie menu (Create Object / View Axis) có ưu tiên cao hơn: nếu đang mở, click-select
            // ở đây phải im lặng hoàn toàn, không được ăn/Use() sự kiện của pie menu.
            if (!BlenderInputPriority.IsFreeFor(BlenderInputPriority.Owner.GizmoDrag)) return;

            // Lưới an toàn: nếu vì lý do gì đó (chuột rời khỏi Scene View giữa lúc kéo, mất
            // sự kiện MouseUp, v.v.) awaitingClickResolution đã tắt mà IsResolvingClick lỡ
            // chưa kịp đồng bộ, ép về đúng trạng thái ngay đầu mỗi frame — tránh việc cờ báo
            // cho BlenderInputPriority bị kẹt "true" vĩnh viễn và chặn nhầm BonePicking/
            // SceneNavigation mãi mãi.
            if (!awaitingClickResolution) IsResolvingClick = false;

            // FIX QUAN TRỌNG: dùng e.rawType thay vì e.type để phát hiện MouseDrag/MouseUp.
            // e.type có thể đã bị đổi thành EventType.Used bởi MỘT handler KHÁC chạy trước đó
            // trong cùng SceneView.duringSceneGui/OnToolGUI pass (VD SimpleBoneRendererEditor
            // gọi e.Use() khi nhả 1 bone đang được pick). Vì Event.current là 1 object dùng
            // chung cho toàn bộ frame, dùng e.type ở đây có thể đọc nhầm "Used" thay vì
            // "MouseUp" thật, khiến logic click/drag/box-select bên dưới chạy sai (hoặc bỏ lỡ
            // MouseUp, khiến awaitingClickResolution / boxSelectActive bị kẹt sang frame sau).
            // e.rawType luôn phản ánh loại sự kiện gốc, không bị Use() ở nơi khác che mất.
            switch (e.rawType)
            {
                case EventType.MouseDown:
                    // Chỉ ghi nhận, CHƯA đụng gì tới Selection — chờ xem là click hay kéo.
                    awaitingClickResolution = true;
                    IsResolvingClick        = true; // báo cho BlenderInputPriority: đang bận
                    boxSelectActive         = false;
                    mouseDownScreenPos      = e.mousePosition;
                    mouseDownAdditive       = e.shift || e.control || e.command;
                    mouseDownToggle         = e.control || e.command;
                    mouseDownPickedObject   = HandleUtility.PickGameObject(e.mousePosition, false);
                    break;

                case EventType.MouseDrag:
                    //Debug.Log($"[BoxSelectDebug] DRAG awaiting={awaitingClickResolution} boxActive={boxSelectActive} hotControl={GUIUtility.hotControl} dist={Vector2.Distance(e.mousePosition, mouseDownScreenPos):F1}");
                    if (!awaitingClickResolution) break;

                    // FIX QUAN TRỌNG (lỗi kẹt hotControl sau khi kéo gizmo trên 1 bone):
                    // TRƯỚC ĐÂY code chỉ tin GUIUtility.hotControl khi MouseDown trúng 1 object
                    // có COLLIDER VẬT LÝ (qua HandleUtility.PickGameObject) — nếu trượt ra
                    // "khoảng trống" thì mặc định coi là box-select bất kể hotControl là gì.
                    // Nhưng bone (SimpleBoneRendererEditor) và rất nhiều Transform hợp lệ khác
                    // (Empty GameObject, bone rig...) KHÔNG có collider, nên PickGameObject luôn
                    // trả về null cho chúng → mọi lần kéo gizmo Move/Rotate/Scale trên 1 bone đều
                    // bị hiểu nhầm là box-select. Hậu quả: lúc thả chuột, code bên dưới gọi
                    // HandleUtility.PickRectObjects(...) rồi e.Use() ngay trên sự kiện MouseUp
                    // đó — mà đoạn này (BlenderToolContext) luôn chạy TRƯỚC EditorTool.OnToolGUI
                    // (BlenderPrecisionMoveTool/Rotate/Scale) trong cùng frame. e.Use() đổi
                    // Event.current.type thành Used cho phần còn lại của frame, khiến
                    // Handles.PositionHandle nội bộ của Unity (chạy sau) không còn nhận đúng
                    // MouseUp để tự nhả hotControl của chính nó → GUIUtility.hotControl bị KẸT
                    // vĩnh viễn ở control id của gizmo → mất khả năng click chọn mọi object cho
                    // tới khi đổi góc nhìn/zoom (khiến Unity tự reset hotControl).
                    //
                    // Sửa: chỉ tin GUIUtility.hotControl (tín hiệu chuẩn, đáng tin cậy nhất từ
                    // chính Unity cho biết "có 1 control đang thực sự giữ chuột"), bỏ hẳn việc
                    // dựa vào PickGameObject để suy đoán — vì gizmo/bone-picking không cần
                    // collider vật lý để giữ hotControl.
                    if (GUIUtility.hotControl != 0)
                    {
                        awaitingClickResolution = false;
                        IsResolvingClick        = false;
                        boxSelectActive         = false;
                        break;
                    }

                    if (!boxSelectActive
                        && Vector2.Distance(e.mousePosition, mouseDownScreenPos) > DRAG_THRESHOLD)
                        boxSelectActive = true;

                    if (boxSelectActive)
                    {
                        // FIX (không bao giờ quét chọn được / không thấy ô xanh nào cả):
                        // TRƯỚC ĐÂY không gọi e.Use() ở đây — sự kiện MouseDrag "sống" này
                        // tiếp tục lọt xuống BlenderPrecisionMoveTool/Rotate/Scale.OnToolGUI
                        // (luôn chạy SAU Context trong cùng frame, xem class comment ở trên).
                        // Handles.PositionHandle/RotationHandle/ScaleHandle của Unity, khi
                        // được gọi với 1 MouseDrag CHƯA bị Use(), có thể tự giành
                        // GUIUtility.hotControl (qua handle "free move" ở tâm gizmo hay các
                        // sub-control ẩn khác) NGAY CẢ KHI con trỏ đang ở rất xa gizmo trên
                        // màn hình — vì trong lúc không có control nào khác cạnh tranh gần
                        // hơn, Unity vẫn xem nó là nearestControl. Hậu quả: ngay frame
                        // MouseDrag kế tiếp, "if (GUIUtility.hotControl != 0)" ở trên hiểu
                        // nhầm là đang có thứ khác được kéo, tự huỷ boxSelectActive giữa
                        // chừng -> ô xanh không bao giờ được vẽ, PickRectObjects không bao
                        // giờ chạy tới. Sửa: ngay khi đã xác định chắc chắn đây là cử chỉ
                        // box-select, phải Use() sự kiện MouseDrag của MỌI frame tiếp theo để
                        // chặn nó lan xuống các gizmo/tool khác cho tới khi thả chuột.
                        window.Repaint();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    //Debug.Log($"[BoxSelectDebug] UP awaiting={awaitingClickResolution} boxActive={boxSelectActive} hotControl={GUIUtility.hotControl}");
                    if (!awaitingClickResolution) break;
                    awaitingClickResolution = false;
                    IsResolvingClick        = false;

                    // Xem giải thích đầy đủ ở nhánh MouseDrag phía trên — chỉ tin hotControl,
                    // không còn suy đoán qua PickGameObject.
                    if (GUIUtility.hotControl != 0)
                    {
                        // Vừa thả gizmo (hoặc control khác, VD đang chọn 1 bone) — không đụng
                        // Selection, và QUAN TRỌNG: không gọi e.Use() ở đây, để nhường lại cho
                        // chính hệ thống đang giữ hotControl (Handles.PositionHandle nội bộ của
                        // Unity, hoặc HandleBonePicking) tự xử lý MouseUp và tự nhả hotControl
                        // của chính nó một cách bình thường.
                        boxSelectActive = false;
                        break;
                    }

                    if (boxSelectActive)
                    {
                        Rect rect = RectFromPoints(mouseDownScreenPos, e.mousePosition);
                        ApplyBoxSelection(HandleUtility.PickRectObjects(rect), mouseDownAdditive, mouseDownToggle);
                        boxSelectActive = false;
                    }
                    else
                    {
                        ApplyClickSelection(mouseDownPickedObject, mouseDownAdditive, mouseDownToggle);
                    }
                    e.Use();
                    break;

                case EventType.Repaint:
                    if (boxSelectActive)
                        DrawBoxSelectRect(RectFromPoints(mouseDownScreenPos, e.mousePosition));
                    break;
            }
        }

        static Rect RectFromPoints(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x), yMin = Mathf.Min(a.y, b.y);
            float xMax = Mathf.Max(a.x, b.x), yMax = Mathf.Max(a.y, b.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        // internal: BlenderPrecisionRectTool dùng lại khi click đứng yên trong khung rect.
        internal static void ApplyClickSelection(GameObject picked, bool additive, bool toggle)
        {
            if (picked != null)
            {
                if (additive)
                {
                    var list = new List<UnityEngine.Object>(Selection.objects);
                    if (toggle && list.Contains(picked)) list.Remove(picked);
                    else if (!list.Contains(picked)) list.Add(picked);
                    Selection.objects = list.ToArray();
                }
                else
                {
                    Selection.activeObject = picked;
                }
            }
            else if (!additive)
            {
                Selection.activeObject = null; // click vào khoảng trống → bỏ chọn
            }
        }

        static void ApplyBoxSelection(GameObject[] picked, bool additive, bool toggle)
        {
            picked ??= new GameObject[0];

            if (!additive)
            {
                Selection.objects = picked;
                return;
            }

            var set = new List<UnityEngine.Object>(Selection.objects);
            foreach (var go in picked)
            {
                if (toggle && set.Contains(go)) set.Remove(go);
                else if (!set.Contains(go)) set.Add(go);
            }
            Selection.objects = set.ToArray();
        }

        static void DrawBoxSelectRect(Rect rect)
        {
            Handles.BeginGUI();
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.55f, 1f, 0.15f));

            Color border = new Color(0.35f, 0.6f, 1f, 0.9f);
            const float t = 1f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), border);
            Handles.EndGUI();
        }
    }

    // ════════════════════════════════ SHARED HELPERS ════════════════════════════════
    static class BlenderGizmoShift
    {
        /// 1f = tốc độ bình thường, < 1f = đang giữ Shift (precision mode).
        public static float CurrentFactor
        {
            get
            {
                // FIX: trước đây gate theo BlenderEditorPreferences.TransformHotkeysEnabled —
                // đó là cờ riêng của tính năng phím tắt G/R/S (BlenderControls), không liên
                // quan tới Gizmo Precision Tools (bật/tắt qua BlenderToolContextActivator).
                // Vì TransformHotkeysEnabled mặc định = false, Shift-precision khi kéo gizmo
                // trực tiếp bị vô hiệu hoàn toàn (luôn trả 1f) dù PrecisionFactor đã được cấu
                // hình đúng. Các EditorTool (Move/Rotate/Scale) ở đây chỉ chạy khi
                // BlenderToolContext đang active, nên bản thân điều đó đã là điều kiện bật/tắt
                // rồi — không cần (và không nên) check thêm cờ của tính năng khác ở đây.
                bool shift = Event.current != null && Event.current.shift;
                return shift ? Mathf.Clamp(BlenderEditorPreferences.PrecisionFactor, 0.001f, 1f) : 1f;
            }
        }

        /// HUD nhỏ hiện ngay trong lúc kéo gizmo, để xác nhận trực quan Shift có được
        /// nhận hay không và hệ số đang áp dụng — hữu ích khi cần kiểm tra/soi lỗi.
        public static void DrawDebugHUD(float factor)
        {
            Handles.BeginGUI();
            bool active = factor < 0.999f;
            string text = active
                ? $"[SHIFT] PRECISE  ×{factor:F2}"
                : "kéo thường (giữ Shift để làm chậm)";

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = active ? Color.yellow : new Color(0.8f, 0.8f, 0.8f) }
            };

            GUI.Label(new Rect(10, 10, 300, 20), text, style);
            Handles.EndGUI();
        }
    }

    // ════════════════════════════════ SHARED DRAG STATE (cho ưu tiên) ════════════════════════════════
    // Bất kỳ tool nào trong các tool Move/Rotate/Scale/Rect đang thực sự giữ hotControl (đang kéo)
    // đều báo lên đây, để BlenderInputPriority biết "GizmoDrag" đang active và các hệ thống ưu
    // tiên thấp hơn (BonePicking, SceneNavigation) nhường lại đúng lúc.
    static class BlenderGizmoDragState
    {
        public static bool AnyDragging =>
            BlenderPrecisionMoveTool.IsDragging || BlenderPrecisionRotateTool.IsDragging || BlenderPrecisionScaleTool.IsDragging
            || BlenderPrecisionRectTool.IsDragging;
    }

    // ════════════════════════════════ MOVE ════════════════════════════════
    class BlenderPrecisionMoveTool : EditorTool
    {
        public static bool IsDragging { get; private set; }

        int     trackedHotControl = 0;
        Vector3 dragStartHandlePos;
        Vector3 rawTotal;
        Vector3 dampedTotal;
        readonly Dictionary<Transform, Vector3> startPos = new Dictionary<Transform, Vector3>();

        public override void OnToolGUI(EditorWindow window)
        {
            Transform[] targets = Selection.transforms;
            if (targets == null || targets.Length == 0) { trackedHotControl = 0; IsDragging = false; return; }

            Vector3    handlePos = Tools.handlePosition;
            Quaternion handleRot = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(handlePos, handleRot);
            bool changed = EditorGUI.EndChangeCheck();

            int hc = GUIUtility.hotControl;
            if (hc == 0) { trackedHotControl = 0; IsDragging = false; return; }
            IsDragging = true;

            if (hc != trackedHotControl)
            {
                // Bắt đầu lượt kéo mới → chỉ ghi nhận baseline, chưa áp dụng gì.
                trackedHotControl  = hc;
                dragStartHandlePos = handlePos;
                rawTotal           = handlePos;
                dampedTotal        = handlePos;
                startPos.Clear();
                foreach (var t in targets)
                    if (t != null) startPos[t] = t.position;
                Undo.RecordObjects(targets, "Move");
                return;
            }

            if (!changed) return;

            // Phần tăng thêm THÔ của riêng frame này (không phụ thuộc việc ta đã chỉnh gì trước đó).
            Vector3 frameDelta = newPos - rawTotal;
            rawTotal = newPos;

            float factor = BlenderGizmoShift.CurrentFactor;
            dampedTotal += frameDelta * factor;

            Vector3 totalDelta = dampedTotal - dragStartHandlePos;

            // FIX: trước đây chỉ RecordObjects 1 lần duy nhất ở frame bắt đầu kéo, các frame
            // apply sau đó thay đổi transform ngoài phạm vi được Undo ghi nhận -> Ctrl+Z không
            // hoàn tác được. Phải gọi lại RecordObjects ngay trước mỗi lần set giá trị; Unity
            // tự gộp các lần ghi liên tiếp trong cùng 1 group thao tác thành đúng 1 bước Undo.
            Undo.RecordObjects(targets, "Move");
            foreach (var kv in startPos)
                if (kv.Key != null) kv.Key.position = kv.Value + totalDelta;

            BlenderGizmoShift.DrawDebugHUD(factor);
        }
    }

    // ════════════════════════════════ ROTATE ════════════════════════════════
    class BlenderPrecisionRotateTool : EditorTool
    {
        public static bool IsDragging { get; private set; }

        int        trackedHotControl = 0;
        Vector3    dragStartPos;
        Quaternion dragStartRot;
        Quaternion rawTotal;
        Quaternion dampedTotal;
        readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> startState
            = new Dictionary<Transform, (Vector3, Quaternion)>();

        public override void OnToolGUI(EditorWindow window)
        {
            Transform[] targets = Selection.transforms;
            if (targets == null || targets.Length == 0) { trackedHotControl = 0; IsDragging = false; return; }

            Vector3    handlePos = Tools.handlePosition;
            Quaternion handleRot = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(handleRot, handlePos);
            bool changed = EditorGUI.EndChangeCheck();

            int hc = GUIUtility.hotControl;
            if (hc == 0) { trackedHotControl = 0; IsDragging = false; return; }
            IsDragging = true;

            if (hc != trackedHotControl)
            {
                trackedHotControl = hc;
                dragStartPos = handlePos;
                dragStartRot = handleRot;
                rawTotal     = handleRot;
                dampedTotal  = handleRot;
                startState.Clear();
                foreach (var t in targets)
                    if (t != null) startState[t] = (t.position, t.rotation);
                Undo.RecordObjects(targets, "Rotate");
                return;
            }

            if (!changed) return;

            Quaternion frameDelta = newRot * Quaternion.Inverse(rawTotal);
            rawTotal = newRot;

            float factor = BlenderGizmoShift.CurrentFactor;
            Quaternion dampedFrameDelta = Quaternion.Slerp(Quaternion.identity, frameDelta, factor);
            dampedTotal = dampedFrameDelta * dampedTotal;

            Quaternion totalDelta = dampedTotal * Quaternion.Inverse(dragStartRot);

            // FIX: xem giải thích ở BlenderPrecisionMoveTool — phải record lại mỗi frame apply.
            Undo.RecordObjects(targets, "Rotate");
            foreach (var kv in startState)
            {
                if (kv.Key == null) continue;
                Vector3 relative = kv.Value.pos - dragStartPos;
                kv.Key.position = dragStartPos + totalDelta * relative;
                kv.Key.rotation = totalDelta * kv.Value.rot;
            }

            BlenderGizmoShift.DrawDebugHUD(factor);
        }
    }

    // ════════════════════════════════ SCALE ════════════════════════════════
    class BlenderPrecisionScaleTool : EditorTool
    {
        public static bool IsDragging { get; private set; }

        int     trackedHotControl = 0;
        Vector3 prevRawTotal      = Vector3.one;
        Vector3 dampedTotal       = Vector3.one;
        readonly Dictionary<Transform, Vector3> startScale = new Dictionary<Transform, Vector3>();

        public override void OnToolGUI(EditorWindow window)
        {
            Transform[] targets = Selection.transforms;
            if (targets == null || targets.Length == 0) { trackedHotControl = 0; IsDragging = false; return; }

            Vector3    handlePos = Tools.handlePosition;
            Quaternion handleRot = Tools.handleRotation;
            float      size      = HandleUtility.GetHandleSize(handlePos);

            EditorGUI.BeginChangeCheck();
            Vector3 rawScale = Handles.ScaleHandle(Vector3.one, handlePos, handleRot, size);
            bool changed = EditorGUI.EndChangeCheck();

            int hc = GUIUtility.hotControl;
            if (hc == 0) { trackedHotControl = 0; IsDragging = false; return; }
            IsDragging = true;

            if (hc != trackedHotControl)
            {
                trackedHotControl = hc;
                prevRawTotal = Vector3.one;
                dampedTotal  = Vector3.one;
                startScale.Clear();
                foreach (var t in targets)
                    if (t != null) startScale[t] = t.localScale;
                Undo.RecordObjects(targets, "Scale");
                return;
            }

            if (!changed) return;

            float factor = BlenderGizmoShift.CurrentFactor;

            Vector3 frameRatio = new Vector3(
                SafeRatio(rawScale.x, prevRawTotal.x),
                SafeRatio(rawScale.y, prevRawTotal.y),
                SafeRatio(rawScale.z, prevRawTotal.z));
            prevRawTotal = rawScale;

            dampedTotal = new Vector3(
                dampedTotal.x * (1f + (frameRatio.x - 1f) * factor),
                dampedTotal.y * (1f + (frameRatio.y - 1f) * factor),
                dampedTotal.z * (1f + (frameRatio.z - 1f) * factor));

            // FIX: xem giải thích ở BlenderPrecisionMoveTool — phải record lại mỗi frame apply.
            Undo.RecordObjects(targets, "Scale");
            foreach (var kv in startScale)
                if (kv.Key != null) kv.Key.localScale = Vector3.Scale(kv.Value, dampedTotal);

            BlenderGizmoShift.DrawDebugHUD(factor);
        }

        static float SafeRatio(float a, float b) => Mathf.Approximately(b, 0f) ? 1f : a / b;
    }

    // ════════════════════════════════ ACTIVATION ════════════════════════════════
    [InitializeOnLoad]
    static class BlenderToolContextActivator
    {
        const string MENU_ENABLE  = "Tools/GaMo ToolKit/Blender Style Workflow/Gizmo Precision (Shift)/Bật";
        const string MENU_DISABLE = "Tools/GaMo ToolKit/Blender Style Workflow/Gizmo Precision (Shift)/Tắt (dùng gizmo mặc định Unity)";

        static BlenderToolContextActivator()
        {
            EditorApplication.delayCall += () =>
            {
                if (ToolManager.activeContextType != typeof(BlenderToolContext))
                    ToolManager.SetActiveContext<BlenderToolContext>();
            };

            // Báo cáo trạng thái "đang kéo gizmo Move/Rotate/Scale" HOẶC "đang giữa 1 cử chỉ
            // click/box-select chưa resolve" cho trọng tài ưu tiên trung tâm — để BonePicking
            // và SceneNavigation biết nhường lại đúng lúc. Trước đây chỉ có AnyDragging (3
            // EditorTool), không bao gồm giai đoạn Context đang chờ MouseDrag/MouseUp để biết
            // là click hay box-select — dẫn tới BonePicking có thể giành hotControl ngay giữa
            // cử chỉ quét chọn khi Selection đang rỗng (3 tool kia chưa từng "dragging").
            BlenderInputPriority.IsGizmoDragging = () =>
                BlenderGizmoDragState.AnyDragging || BlenderToolContext.IsResolvingClick;
        }

        [MenuItem(MENU_ENABLE)]
        static void Enable() => ToolManager.SetActiveContext<BlenderToolContext>();

        [MenuItem(MENU_ENABLE, true)]
        static bool EnableValidate() => ToolManager.activeContextType != typeof(BlenderToolContext);

        [MenuItem(MENU_DISABLE)]
        static void Disable() => ToolManager.SetActiveContext<GameObjectToolContext>();

        [MenuItem(MENU_DISABLE, true)]
        static bool DisableValidate() => ToolManager.activeContextType == typeof(BlenderToolContext);
    }
}

/*
 * GHI CHÚ / GIỚI HẠN:
 *
 * 1) Kích hoạt: tự bật context "Blender Precision Tools" khi Editor khởi động. Bật/tắt tay
 *    qua menu Tools > GaMo Blender Tools > Gizmo Precision (Shift), hoặc icon dropdown nhỏ
 *    bên trái nhóm nút Move/Rotate/Scale ở toolbar Scene View.
 *
 * 2) Click-select đã được thêm lại, hỗ trợ cả click từng object (kèm Shift/Ctrl để
 *    cộng/trừ vào selection) LẪN kéo-để-chọn-nhiều (rect/box select) như Unity gốc.
 *    (v3 — sửa lỗi BonePicking giành hotControl giữa chừng cử chỉ khi Selection rỗng,
 *    xem BlenderToolContext.IsResolvingClick).
 *
 * 3) Rect Tool (phím T) đã có bản riêng: BlenderPrecisionRectTool.cs. Transform Tool (tool
 *    gộp, phím Y) vẫn không khả dụng khi context này active — giới hạn của EditorToolContext
 *    trong Unity. Muốn dùng lại: chọn menu "Tắt".
 *
 * 4) Multi-select với Pivot Mode "Individual" (mỗi object 1 gizmo riêng): bản này vẫn chỉ
 *    vẽ 1 gizmo dùng chung (giống Pivot Mode "Center").
 *
 * 5) Trong lúc kéo, góc trên-trái Scene View sẽ hiện dòng chữ nhỏ báo hệ số đang áp dụng —
 *    dùng để xác nhận Shift có được nhận đúng không. Có thể xoá lời gọi
 *    BlenderGizmoShift.DrawDebugHUD(...) trong 3 tool nếu không muốn hiện nữa.
 */
#endif