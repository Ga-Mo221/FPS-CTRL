using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GaMo.BlenderEditorTools
{
// ─── Custom Event Types ───────────────────────────────────────────────────────

    [Serializable]
    public class AnimatorEvent : UnityEvent<Animator>
    {
    }

    [Serializable]
    public class AnimatorIntEvent : UnityEvent<Animator, int>
    {
    }

    [Serializable]
    public class AnimatorFloatEvent : UnityEvent<Animator, float>
    {
    }

    [Serializable]
    public class AnimatorStringEvent : UnityEvent<Animator, string>
    {
    }

    [Serializable]
    public class AnimatorBoolEvent : UnityEvent<Animator, bool>
    {
    }

    /// <summary>
    /// StateMachineBehaviour kích hoạt UnityEvent tại thời điểm cụ thể trong animation.
    /// Hỗ trợ 2 chế độ: Seconds (giây) và Normalized (0.0 → 1.0).
    ///
    /// ══════════════════════════════════════════════════════
    ///  THIẾT KẾ v8  —  Fixes so với v7
    /// ══════════════════════════════════════════════════════
    ///
    /// [CRITICAL #1 — Clip Speed Bị Bỏ Qua]
    ///   animator.speed chỉ là global multiplier. State trong Animator Controller
    ///   có thể có speed riêng (âm hoặc khác 1). Kết hợp: effectiveSpeed = stateInfo.speed.
    ///   stateInfo.speed = animator.speed × state_speed_multiplier (Unity tính sẵn).
    ///   isReverse = effectiveSpeed < 0 — đây là nguồn duy nhất đáng tin cậy.
    ///   FIX: Dùng stateInfo.speed thay vì animator.speed cho isReverse detection.
    ///
    /// [CRITICAL #2 — stateInfo.length Không Tính State Speed]
    ///   Khi State speed ≠ 1, normalizedTime vẫn chạy đúng (Unity handle),
    ///   nhưng quy đổi Seconds → normalized dùng stateInfo.length (wall-clock length)
    ///   phải điều chỉnh theo Mathf.Abs(effectiveSpeed) nếu dùng Seconds mode.
    ///   Thực ra stateInfo.length là độ dài clip gốc (không scale), trong khi
    ///   normalizedTime tăng tỉ lệ với effectiveSpeed. Khi speed=2, clip kết thúc
    ///   ở normalizedTime=1 sau 0.5s, nhưng stateInfo.length vẫn là 1.0s.
    ///   FIX: Seconds trigger → convert về normalized dùng stateInfo.length (duration gốc).
    ///   Phần normalized đã đúng vì Unity chuẩn hóa normalizedTime trên [0,1] per-loop
    ///   bất kể speed. Không cần thêm scale — nhưng cần đảm bảo direction đúng (#1).
    ///   Ghi chú: stateInfo.length = duration thực của clip (không phụ thuộc speed);
    ///   normalizedTime rate phụ thuộc speed. Quy đổi triggerSeconds/length cho ra
    ///   normalized đúng trên [0,1]. Vấn đề thực là direction, đã fix ở #1.
    ///
    /// [HIGH #3 — Early-Return Bỏ Qua Khoảng Trigger]
    ///   Khi curr < prev (forward) do transition seed, code return sớm và gán
    ///   prevNorm = currNorm — bỏ qua khoảng [prev..curr]. Event trong khoảng đó miss.
    ///   Thực ra khi transition seed: normalizedTime có thể nhảy từ 0 về 0
    ///   hoặc bắt đầu từ giá trị khác 0. Hành vi đúng: KHÔNG skip khoảng này
    ///   mà để nó chạy qua algorithm bình thường. Nếu curr < prev forward → không có
    ///   khoảng hợp lệ nào (clip không loop theo chiều ngược khi speed > 0) → safe to skip.
    ///   FIX: Giữ early-return nhưng KHÔNG update prevNorm trong trường hợp này —
    ///   giữ prevNorm cũ để frame sau tiếp tục từ điểm đó. Điều này đúng vì nếu
    ///   normalizedTime thực sự jump backward mà không phải reverse mode, đó là
    ///   seeding artifact và prevNorm nên giữ nguyên.
    ///
    /// [HIGH #4 — Overflow Reset Gây Double-Fire]
    ///   lastFiredLoop = -1 sau reset. prevNorm = phần dư. Frame sau loopIdx = 0,
    ///   lastFiredLoop = -1 → pass check → fire lại event vừa fire ở loop 999999.
    ///   FIX: Sau overflow reset, gán lastFiredLoop = 0 (loopIdx mới sau reset là 0)
    ///   thay vì -1. Đảm bảo frame ngay sau reset không fire lại.
    ///   Thực ra vấn đề sâu hơn: prevNorm sau reset = currNorm - intPart = currPos [0,1).
    ///   Frame sau, currNorm = prevNorm + delta (nhỏ). loopIdx = 0 cho cả hai.
    ///   lastFiredLoop = 0 → check rt.lastFiredLoop == 0 → skip đúng.
    ///
    /// [HIGH #5 — OnStateExit Miss End-Boundary fireOnce=false]
    ///   !rt.hasFired dùng chung cho cả fireOnce=true và fireOnce=false.
    ///   fireOnce=false: hasFired=true sau loop đầu → OnStateExit skip → exit miss.
    ///   FIX: Tách logic: fireOnce=true → dùng !hasFired (giữ nguyên).
    ///   fireOnce=false → luôn fire IsAtEnd tại Exit (vì Exit là "kết thúc state").
    ///
    /// [MEDIUM #6 — OnStateMachineExit Cleanup Sai Vấn Đề]
    ///   OnStateMachineExit fire khi thoát sub-state-machine, KHÔNG khi Animator bị destroy.
    ///   Nếu Animator bị destroy mid-state, OnStateExit không được gọi → leak.
    ///   FIX: Không dùng OnStateMachineExit cho cleanup. Thay bằng hướng dẫn
    ///   gọi CleanupInstance(animator) từ OnDisable/OnDestroy của MonoBehaviour
    ///   sở hữu Animator. Public API để caller cleanup chủ động.
    ///   OnStateMachineExit giữ lại nhưng chỉ dùng để giải phóng khi exit sub-FSM.
    ///
    /// [MEDIUM #7 — Reverse from/to Tính Sai Tại Biên Loop]
    ///   Reverse nhánh: from = hi - hiLoopIdx, to = lo - loLoopIdx.
    ///   Với intermediate loops (loop < hiLoopIdx và loop > loLoopIdx):
    ///   from nên = 1f (đỉnh loop), to nên = 0f (đáy loop). Code hiện tại đúng.
    ///   Nhưng với loop == hiLoopIdx: from = hi - hiLoopIdx = phần dư của prev (cao).
    ///   Với loop == loLoopIdx: to = lo - loLoopIdx = phần dư của curr (thấp). Đúng.
    ///   Vấn đề: nhánh reverse dùng biến hi/lo tính từ swap — đảm bảo comment rõ ràng.
    ///   FIX: Rename prevPos/currPos để tránh nhầm lẫn, document rõ trong code.
    ///
    /// [PERF #8 — motionEvents.Count Gọi Nhiều Lần]
    ///   FIX: Cache count một lần đầu OnStateUpdate.
    ///
    /// [PERF #9 — String Interpolation Trước Khi Check showDebugLog]
    ///   FIX: Toàn bộ string build nằm bên trong if (showDebugLog).
    ///   Compiler không build string nếu nhánh không được vào.
    ///
    /// [PERF #10 — Dictionary TryGetValue Mỗi Frame]
    ///   FIX: Không thể tránh hoàn toàn với multi-instance, nhưng early-exit
    ///   khi data.events.Length == 0 tránh work không cần thiết.
    ///   Không cache vào field vì StateMachineBehaviour là shared — field sẽ
    ///   overwrite giữa instances (đây là lý do dùng Dictionary ngay từ đầu).
    ///
    /// ══════════════════════════════════════════════════════
    ///  v8 — Fixes mới (phát hiện sau review speed ≠ 1)
    /// ══════════════════════════════════════════════════════
    ///
    /// [CRITICAL v8-A — lastFiredLoop Gán Sai Khi Không Fire]
    ///   Dòng: `if (rt.lastFiredLoop != loop) rt.lastFiredLoop = rt.hasFired ? loop : rt.lastFiredLoop;`
    ///   hasFired là CUMULATIVE — nếu đã fire ở loop trước, hasFired=true → loop hiện tại
    ///   bị đánh dấu lastFiredLoop=loop dù trigNorm không nằm trong khoảng [from,to].
    ///   Kết quả: event bị skip oan ở loop tiếp theo vì lastFiredLoop == loop đó.
    ///   FIX: Xoá dòng logic sai. lastFiredLoop chỉ được gán ngay sau FireEvent thực sự.
    ///   Thêm biến lokal `firedInThisLoop` để track và gán đúng chỗ.
    ///
    /// [CRITICAL v8-B — OnStateEnter/Exit Boundary Sai Khi speed < 0]
    ///   Khi speed < 0, animation bắt đầu tại normalizedTime ≈ 1 (cuối clip), không phải 0.
    ///   OnStateEnter hiện fire cachedBoundary==0 (IsAtStart/norm≈0) — sai với reverse:
    ///   clip chưa đi qua norm=0, nó đang bắt đầu từ norm=1.
    ///   OnStateExit tương tự chỉ handle boundary==1 — sai với reverse exit tại norm≈0.
    ///   FIX: OnStateEnter fire boundary đúng với chiều: forward→0, reverse→1.
    ///        OnStateExit fire boundary đúng với chiều: forward→1 (tại Exit), reverse→0.
    ///
    /// [HIGH v8-C — speed == 0 Không Guard → Lãng Phí Mỗi Frame]
    ///   Khi animation bị pause (speed=0), normalizedTime không thay đổi.
    ///   currNormTime == prevNormTime → mọi from>=to → không fire, nhưng vẫn chạy
    ///   toàn bộ vòng lặp events + điều kiện mỗi frame.
    ///   FIX: Early-return khi stateInfo.speed == 0f.
    ///
    /// [MEDIUM v8-D — Seconds Mode + Reverse: Thiếu Warning UX]
    ///   triggerSeconds=0.5s trong Seconds mode bake ra norm=0.5.
    ///   Reverse animation bắt đầu từ norm=1 → event fire khi đi qua 0.5 chiều giảm.
    ///   Người dùng nhập "0.5s" với kỳ vọng "0.5 giây sau khi clip bắt đầu",
    ///   nhưng với reverse, t=0s tương ứng norm=1 (cuối clip gốc), không phải norm=0.
    ///   FIX: Warning Editor 1 lần khi BakeCache nhận isReverse=true + Seconds mode.
    ///
    /// [PERF v8-E — Mathf.FloorToInt → Cast (int) Nhanh Hơn]
    ///   Mathf.FloorToInt gọi native method. Với giá trị đã guard >= 0,
    ///   cast (int) cho kết quả tương đương và nhanh hơn ~2-3x trên IL2CPP.
    ///   FIX: Thay 4 lần Mathf.FloorToInt bằng (int) trong OnStateUpdate.
    ///
    /// [PERF v8-F — GetCurrentAnimatorStateInfo Trong Debug Log]
    ///   FireEvent gọi animator.GetCurrentAnimatorStateInfo(0) chỉ để lấy speed cho log.
    ///   Đây là native call đắt, không cần thiết vì stateInfo đã có sẵn tại caller.
    ///   FIX: Truyền stateInfo.speed vào FireEvent qua tham số cachedSpeed (float).
    ///        Trong OnStateEnter/Exit truyền 0f (speed không quan trọng ở boundary log).
    ///
    /// ══════════════════════════════════════════════════════
    ///  v9 — Fixes mới (sau review v8)
    /// ══════════════════════════════════════════════════════
    ///
    /// [CRITICAL v9-A — trigNorm Dùng Trước Khi Gán Trong BakeCache Warning]
    ///   Ở nhánh Seconds, `trigNorm` chỉ được gán thật ở cuối hàm (sau warning reverse),
    ///   nên warning "Seconds mode với speed âm" luôn in trigNorm=0 (giá trị mặc định).
    ///   FIX: Tính trigNorm (clamped/clipLength) TRƯỚC, rồi mới log warning dùng giá trị đó.
    ///
    /// [HIGH v9-B — prevNormalizedTime Bị Clamp Về 0 Làm Sai Lệch Reverse]
    ///   `Mathf.Max(0f, ...)` áp cho cả OnStateEnter lẫn OnStateUpdate giả định
    ///   normalizedTime luôn >= 0, nhưng với reverse, Unity's normalizedTime có thể
    ///   là số âm liên tục qua nhiều loop. Việc kẹp về 0 tạo ra khoảng quét suy biến
    ///   (from == to == 0) ở gần biên, khiến event bị bỏ lỡ mà không có cảnh báo nào.
    ///   FIX: Không còn clamp — giữ nguyên giá trị thô (có thể âm) xuyên suốt tính toán.
    ///   Thay indexer loop `(int)x` (truncate về 0, SAI với số âm) bằng hàm `FloorInt`
    ///   tính floor toán học đúng cho cả số âm, đồng thời nhanh hơn Mathf.FloorToInt
    ///   (không gọi native).
    ///
    /// [MEDIUM v9-C — Overflow Reset Chỉ Xử Lý Chiều Dương]
    ///   `currNormTime > LoopResetThreshold` bỏ sót trường hợp reverse trôi rất âm
    ///   qua thời gian dài (currNormTime rất nhỏ, âm). FIX: dùng `Mathf.Abs(...)`.
    ///
    /// [LOW v9-D — effectiveSpeed == 0f Dùng So Sánh Float Tuyệt Đối]
    ///   Speed cực nhỏ (vd 1e-7f do driven bởi curve/animation) không bị early-return,
    ///   vẫn chạy work thừa mỗi frame dù thực chất không di chuyển đáng kể.
    ///   FIX: `Mathf.Abs(effectiveSpeed) < SpeedEpsilon` thay vì so sánh `== 0f`.
    ///
    /// [PERF v9-E — motionEvents (List&lt;T&gt;) Trong Hot Path]
    ///   List&lt;T&gt; indexer có bounds-check + 1 lớp gián tiếp so với array thuần.
    ///   FIX: Cache `motionEvents` thành `MotionTimeEvent[]`, rebuild khi Inspector
    ///   đổi (OnValidate) hoặc khi Count lệch (fallback runtime an toàn).
    ///
    /// [LOW v9-F — Mathf.Floor Không Nhất Quán Với Cast (int) Ở Overflow Reset]
    ///   Overflow reset dùng Mathf.Floor trong khi hot path dùng (int)/FloorInt.
    ///   FIX: Dùng FloorInt cho nhất quán (giá trị ở đây có thể âm với reverse trôi dài).
    ///
    /// ══════════════════════════════════════════════════════
    ///  v10 — Revert v8-B (phát hiện sai từ log thực tế trên Animator Controller)
    /// ══════════════════════════════════════════════════════
    ///
    /// [CRITICAL v10 — v8-B Hoán Đổi Boundary Theo Speed Là SAI]
    ///   v8-B giả định: reverse (speed&lt;0) → Unity seed normalizedTime≈1 lúc OnStateEnter,
    ///   và về ≈0 lúc OnStateExit → nên hoán đổi enterBoundary/exitBoundary theo dấu speed.
    ///   THỰC TẾ: OnStateEnter/OnStateExit là sự kiện của State Machine (FSM) — luôn xảy ra
    ///   theo đúng thứ tự thời gian thực (Enter trước, Exit sau), HOÀN TOÀN không phụ thuộc
    ///   speed của clip bên trong state. speed&lt;0 chỉ đổi chiều Unity sample clip (normalizedTime
    ///   tăng hay giảm theo thời gian), không đảo ngược trình tự Enter/Exit của FSM.
    ///   Giả định v8-B chỉ đúng trong 1 trường hợp hẹp: cố tình cấu hình Transition Offset=1
    ///   để giả lập "phát ngược thật sự" (clip visually chạy từ cuối về đầu) — mặc định Unity
    ///   vẫn Enter ở normalizedTime≈0 dù speed âm (xác nhận bằng log thực tế: OnStateExit với
    ///   speed=-1.00 fire event tại norm=0.000, đúng theo boundary=Start cố định, không phải
    ///   do hoán đổi).
    ///   FIX: Enter LUÔN fire boundary Start (0). Exit LUÔN fire boundary End (1).
    ///        Bỏ hoàn toàn nhánh isReverse trong việc chọn enterBoundary/exitBoundary.
    /// </summary>
    public class MotionTimeEventBehaviour : StateMachineBehaviour
    {
        // ─── Enums ────────────────────────────────────────────────────────────────

        public enum TriggerMode
        {
            [InspectorName("Seconds (s)")] Seconds,
            [InspectorName("Normalized (0 → 1)")] Normalized
        }

        public enum EventPayloadType
        {
            [InspectorName("Animator only")] None,
            [InspectorName("Animator + int")] Int,
            [InspectorName("Animator + float")] Float,
            [InspectorName("Animator + string")] String,
            [InspectorName("Animator + bool")] Bool
        }

        // ─── Per-Instance Runtime ─────────────────────────────────────────────────

        public sealed class EventRuntime
        {
            public bool hasFired;

            /// <summary>
            /// Loop index của lần fire gần nhất. -1 = chưa fire.
            /// Sau overflow reset, gán = 0 (loop đầu tiên của window mới).
            /// </summary>
            public int lastFiredLoop = -1;

            public float cachedTriggerNorm;

            /// <summary>-1 = midpoint | 0 = boundary start | 1 = boundary end</summary>
            public int cachedBoundary = -1;

            public bool hasWarnedInvalidTrigger;
        }

        private sealed class PerInstanceData
        {
            /// <summary>
            /// normalizedTime frame trước trên TRỤC LIÊN TỤC.
            /// Chỉ reset khi vượt LoopResetThreshold.
            /// </summary>
            public float prevNormalizedTime;

            /// <summary>stateInfo.length tại lần bake gần nhất (clip duration gốc, không scale).</summary>
            public float cachedClipLength;

            /// <summary>parallel array với motionEvents, indexed giống nhau.</summary>
            public EventRuntime[] events;
        }

        private readonly Dictionary<int, PerInstanceData> _instanceStates
            = new Dictionary<int, PerInstanceData>();

        // Ngưỡng overflow (~1 triệu loops).
        private const float LoopResetThreshold = 1_000_000f;

        // Rebake Seconds-mode chỉ khi clip length drift > 10ms.
        private const float ClipLengthRebakeEpsilon = 0.01f;

        // FIX [LOW v9-D]: ngưỡng coi speed là "gần như 0" (paused) thay vì so sánh == 0f tuyệt đối.
        private const float SpeedEpsilon = 1e-5f;

        // FIX [PERF v9-E]: cache motionEvents (List) thành array cho hot path.
        // Rebuild khi OnValidate (Editor) hoặc khi phát hiện Count lệch (an toàn runtime).
        private MotionTimeEvent[] _eventsArray;

        private MotionTimeEvent[] GetEventsArray()
        {
            if (_eventsArray == null || _eventsArray.Length != motionEvents.Count)
                _eventsArray = motionEvents.ToArray();
            return _eventsArray;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Danh sách có thể bị sửa trong Inspector — buộc rebuild cache ở lần dùng tiếp theo.
            _eventsArray = null;
        }
#endif

        /// <summary>
        /// FIX [CRITICAL v9-B / HIGH v9-C]: Floor toán học đúng cho cả số âm, không gọi native.
        /// (int)x trong C# truncate về phía 0 (vd (int)(-0.3f) == 0) — SAI với floor (-1).
        /// Cách này nhanh hơn Mathf.FloorToInt vì không có interop call, đúng cho mọi dấu.
        /// </summary>
        private static int FloorInt(float v)
        {
            int t = (int)v;
            return (v < 0f && t != v) ? t - 1 : t;
        }

        /// <summary>Phần dư [0,1) đúng cho cả số âm, dựa trên FloorInt.</summary>
        private static float Frac01(float v) => v - FloorInt(v);

        // ─── MotionTimeEvent ──────────────────────────────────────────────────────

        [Serializable]
        public class MotionTimeEvent
        {
            [Tooltip("Seconds: nhập giây (vd: 1.5s)\nNormalized: nhập tỉ lệ 0→1 (vd: 0.85 = 85% clip)")]
            public TriggerMode mode = TriggerMode.Normalized;

            [Min(0f)]
            [Tooltip("Thời điểm kích hoạt (giây tính theo clip gốc, không phụ thuộc speed).\n" +
                     "Nếu > clipLength sẽ bị clamp (log warning 1 lần ở Editor).")]
            public float triggerSeconds = 0f;

            [Range(0f, 1f)]
            [Tooltip("Thời điểm kích hoạt (0 = đầu clip, 1 = cuối clip). Không phụ thuộc speed.")]
            public float normalizedValue = 0f;

            [Tooltip("true  = chỉ fire 1 lần trong suốt lifetime của state.\n" +
                     "false = fire mỗi lần animation loop qua vị trí này.\n\n" +
                     "Lưu ý: polling-based — event có thể miss nếu Animator.speed nhảy quá lớn\n" +
                     "khiến trigger bị skip trong 1 frame (giống Unity Animation Event).")]
            public bool fireOnce = true;

            public EventPayloadType payloadType = EventPayloadType.None;
            

            public int intValue;

            public float floatValue;

            public string stringValue;

            public bool boolValue;
            
            public AnimatorEvent onTriggered;

            public AnimatorIntEvent onTriggeredInt;

            public AnimatorFloatEvent onTriggeredFloat;

            public AnimatorStringEvent onTriggeredString;

            public AnimatorBoolEvent onTriggeredBool;

            // Epsilon tuyệt đối cho phân loại biên.
            public const float BoundaryEpsilon = 1e-4f;

            /// <summary>
            /// Tính cachedTriggerNorm + cachedBoundary. Không đụng hasFired/lastFiredLoop.
            /// clipLength = stateInfo.length (clip duration gốc, không scale theo speed).
            /// Normalized trigger không cần clipLength. Seconds trigger: norm = sec / length.
            /// Cả hai đều đúng bất kể speed vì normalizedTime của Unity đã được chuẩn hóa.
            ///
            /// FIX [MEDIUM v8-D]: isReverse dùng để cảnh báo khi Seconds mode + reverse.
            /// triggerSeconds=0.5s bake ra norm=0.5. Với reverse, t=0s tương ứng norm=1
            /// (cuối clip gốc) → người dùng có thể nhập sai kỳ vọng.
            /// </summary>
            public void BakeCache(EventRuntime rt, float clipLength, bool isReverse = false)
            {
                float trigNorm = 0;
                if (mode == TriggerMode.Normalized)
                {
                    trigNorm = Mathf.Clamp01(normalizedValue);
                }
                else
                {
                    float clamped = Mathf.Clamp(triggerSeconds, 0f, clipLength);
#if UNITY_EDITOR
                    if (!Mathf.Approximately(clamped, triggerSeconds) && !rt.hasWarnedInvalidTrigger)
                    {
                        Debug.LogWarning(
                            $"[MotionTimeEvent] triggerSeconds={triggerSeconds:F3}s vượt ngoài " +
                            $"clipLength={clipLength:F3}s → clamp về {clamped:F3}s. " +
                            $"(1 lần per state enter)");
                        rt.hasWarnedInvalidTrigger = true;
                    }
#endif
                    // FIX [CRITICAL v9-A]: tính trigNorm TRƯỚC khi dùng trong warning bên dưới —
                    // ở v8, warning đọc trigNorm khi nó còn là giá trị mặc định (0), in sai số liệu.
                    trigNorm = clipLength > 0f ? clamped / clipLength : 0f;
#if UNITY_EDITOR
                    // FIX [MEDIUM v8-D]: Seconds mode với speed âm — t=0s là cuối clip gốc (norm=1),
                    // không phải đầu clip. triggerSeconds=0.5s → event fire tại norm=0.5 theo chiều giảm.
                    // Cảnh báo 1 lần để người dùng không nhầm kỳ vọng về thứ tự thời gian.
                    if (isReverse && !rt.hasWarnedInvalidTrigger)
                    {
                        Debug.LogWarning(
                            $"[MotionTimeEvent] Seconds mode với speed âm (reverse): " +
                            $"triggerSeconds={triggerSeconds:F3}s tương ứng norm={trigNorm:F3} theo chiều GIẢM. " +
                            $"t=0s = norm=1 (cuối clip gốc), t=clipLength = norm=0 (đầu clip gốc). " +
                            $"Dùng Normalized mode nếu muốn kiểm soát chính xác hơn. (1 lần per enter)");
                        rt.hasWarnedInvalidTrigger = true;
                    }
#endif
                }

                rt.cachedTriggerNorm = trigNorm;
                rt.cachedBoundary = trigNorm <= BoundaryEpsilon ? 0
                    : trigNorm >= 1f - BoundaryEpsilon ? 1
                    : -1;
            }

            public void Invoke(Animator animator)
            {
                switch (payloadType)
                {
                    case EventPayloadType.None: onTriggered?.Invoke(animator); break;
                    case EventPayloadType.Int: onTriggeredInt?.Invoke(animator, intValue); break;
                    case EventPayloadType.Float: onTriggeredFloat?.Invoke(animator, floatValue); break;
                    case EventPayloadType.String: onTriggeredString?.Invoke(animator, stringValue); break;
                    case EventPayloadType.Bool: onTriggeredBool?.Invoke(animator, boolValue); break;
                }
            }
        }

        // ─── Inspector ────────────────────────────────────────────────────────────

        [SerializeField]
        private List<MotionTimeEvent> motionEvents = new List<MotionTimeEvent>();

        [SerializeField] private bool showDebugLog = false;

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// FIX [MEDIUM #6]: Gọi từ OnDisable/OnDestroy của MonoBehaviour sở hữu Animator
        /// để tránh memory leak khi Animator bị destroy mid-state.
        ///
        /// Ví dụ:
        ///   void OnDestroy() { foreach (var b in GetBehaviours&lt;MotionTimeEventBehaviour&gt;()) b.CleanupInstance(GetComponent&lt;Animator&gt;()); }
        /// </summary>
        public void CleanupInstance(Animator animator)
        {
            if (animator != null)
                _instanceStates.Remove(animator.GetInstanceID());
        }

        // ─── Callbacks ────────────────────────────────────────────────────────────

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            int id = animator.GetInstanceID();

            // FIX [PERF v9-E]: dùng array cache thay vì List<T> trong hot path.
            var evts = GetEventsArray();
            int count = evts.Length;

            // isReverseOnEnter chỉ dùng để cảnh báo Seconds-mode+reverse trong BakeCache (v8-D).
            // KHÔNG còn dùng để chọn boundary Enter/Exit (xem FIX [v10] bên dưới).
            bool isReverseOnEnter = stateInfo.speed < 0f;

            var data = new PerInstanceData
            {
                // FIX [HIGH v9-B]: KHÔNG còn Mathf.Max(0f, ...). Với reverse, Unity không đảm bảo
                // seed normalizedTime ≈ 1 (phụ thuộc transition offset trong Animator Controller) —
                // giữ nguyên giá trị thô (có thể ≈0 hoặc bất kỳ) để OnStateUpdate tính đúng, tránh
                // tạo khoảng quét suy biến (from==to==0) do bị kẹp về 0.
                prevNormalizedTime = stateInfo.normalizedTime,
                cachedClipLength = stateInfo.length,
                events = new EventRuntime[count]
            };

            for (int i = 0; i < count; i++)
            {
                var rt = new EventRuntime();
                // FIX [MEDIUM v8-D]: Truyền isReverse để BakeCache có thể cảnh báo Seconds+reverse.
                evts[i].BakeCache(rt, data.cachedClipLength, isReverseOnEnter);
                data.events[i] = rt;
            }

            _instanceStates[id] = data;

            // FIX [v10]: REVERT v8-B. Enter/Exit là sự kiện của FSM (State Machine), luôn theo
            // đúng thứ tự thời gian thực (Enter trước, Exit sau) — KHÔNG phụ thuộc speed của clip
            // bên trong state. speed<0 chỉ đổi chiều sample của clip, không đảo Enter/Exit của FSM.
            // Giả định v8-B (reverse seed norm≈1 lúc Enter) chỉ đúng khi cố tình set Transition
            // Offset=1 để giả lập "phát ngược" — mặc định Unity vẫn Enter ở norm≈0 dù speed âm.
            // → Enter luôn là boundary Start (0), Exit luôn là boundary End (1).
            const int enterBoundary = 0;
            for (int i = 0; i < count; i++)
            {
                if (data.events[i].cachedBoundary == enterBoundary)
                    FireEvent(evts[i], data.events[i], animator, stateInfo.speed, data.cachedClipLength);
            }
        }

        /// <summary>
        /// Thuật toán v8 — thay đổi chính so với v7:
        ///
        ///   isReverse = stateInfo.speed &lt; 0
        ///     stateInfo.speed = animator.speed × state_speed_multiplier (Unity tính sẵn).
        ///     Đây là giá trị THỰC TẾ quyết định chiều chạy của normalizedTime.
        ///     FIX [CRITICAL #1]: Bao gồm cả State speed âm.
        ///
        ///   FIX [HIGH v8-C]: Early-return khi speed == 0 (animation paused).
        ///
        ///   Early-return khi jump ngược chiều (artifact):
        ///     FIX [HIGH #3]: KHÔNG update prevNorm → frame sau tiếp tục từ vị trí cũ.
        ///
        ///   FIX [PERF v8-E]: Mathf.FloorToInt → cast (int) cho giá trị đã guard >= 0.
        ///
        ///   FIX [CRITICAL v8-A]: lastFiredLoop chỉ gán ngay sau FireEvent thực sự,
        ///     không dùng hasFired cumulative để tránh đánh dấu sai loop chưa fire.
        ///
        ///   FIX [PERF v8-F]: Truyền stateInfo.speed vào FireEvent thay vì gọi
        ///     GetCurrentAnimatorStateInfo lại bên trong FireEvent.
        ///
        ///   Overflow reset: lastFiredLoop = 0 (không phải -1).
        ///     FIX [HIGH #4]: Tránh double-fire ngay sau reset.
        ///
        ///   OnStateExit IsAtEnd: fireOnce=false luôn fire.
        ///     FIX [HIGH #5]: Xem OnStateExit.
        ///
        ///   PERF [#8]: count cache 1 lần.
        ///   PERF [#9]: string build lazy trong if (showDebugLog).
        /// </summary>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            int id = animator.GetInstanceID();
            if (!_instanceStates.TryGetValue(id, out var data)) return;

            // FIX [PERF v9-E]: array cache thay vì List<T> trong hot path.
            var evts = GetEventsArray();

            // PERF [#8]: cache count một lần, dùng cho cả rebake và main loop.
            int count = data.events.Length;
            if (count == 0) return; // PERF [#10]: skip work sớm.

            if (data.cachedClipLength <= 0f) return;

            // FIX [CRITICAL #1 — GIỮ LẠI ĐỂ LOG/BAKECACHE]: stateInfo.speed = animator.speed ×
            // state_speed_multiplier. Giá trị này CHỈ dùng để log/debug và cảnh báo BakeCache —
            // KHÔNG còn dùng để quyết định chiều quét (xem FIX [v11] bên dưới).
            float effectiveSpeed = stateInfo.speed;

            // FIX [LOW v9-D]: dùng ngưỡng epsilon thay vì so sánh == 0f tuyệt đối —
            // speed cực nhỏ (vd điều khiển bởi curve) vẫn được coi là "paused".
            if (Mathf.Abs(effectiveSpeed) < SpeedEpsilon) return;

            // FIX [HIGH v9-B]: KHÔNG còn Mathf.Max(0f, ...) — giữ nguyên giá trị thô kể cả âm,
            // để không tạo khoảng quét suy biến khi reverse chưa "leo" lên trên 0.
            float currNormTime = stateInfo.normalizedTime;
            float prevNormTime = data.prevNormalizedTime;

            // FIX [v11 — CRITICAL, phát hiện qua log thực tế 'Gost_FBX']:
            // stateInfo.speed < 0 KHÔNG đảm bảo normalizedTime thực sự giảm dần — quan sát
            // thực tế cho thấy có state (đặc biệt Blend Tree) báo speed âm nhưng normalizedTime
            // vẫn tăng đều mỗi frame suốt cả animation (không phải hiện tượng "seed" thoáng qua
            // vài frame như giả định cũ). Tin theo dấu effectiveSpeed trong trường hợp này khiến
            // code luôn rơi vào nhánh "seeding" và KHÔNG BAO GIỜ fire event.
            // → Xác định chiều quét trực tiếp từ delta normalizedTime QUAN SÁT ĐƯỢC, không tin
            // dấu speed. Nhờ vậy state nào có speed âm nhưng thực chạy xuôi sẽ tự động được xử lý
            // đúng bằng nhánh xuôi (forward) — không cần các guard "seeding/skip" cũ nữa.
            bool isReverse = currNormTime < prevNormTime;

#if UNITY_EDITOR
            // Cảnh báo 1 lần khi dấu effectiveSpeed và chiều quan sát được KHÔNG khớp nhau —
            // đây chính xác là triệu chứng của bug đã tìm thấy ở 'Gost_FBX'.
            if (showDebugLog && (effectiveSpeed < 0f) != isReverse)
            {
                Debug.LogWarning(
                    $"[MotionTimeEvent][SPEED MISMATCH] '{animator.name}' frame={Time.frameCount} " +
                    $"effectiveSpeed={effectiveSpeed:F3} (dấu gợi ý {(effectiveSpeed < 0f ? "reverse" : "forward")}) " +
                    $"nhưng normalizedTime quan sát được lại {(isReverse ? "giảm (reverse)" : "tăng (forward)")} " +
                    $"(prevNorm={prevNormTime:F4} → currNorm={currNormTime:F4}). " +
                    "Đang xử lý theo chiều quan sát được, không theo dấu speed.");
            }
#endif

            // Rebake Seconds-mode khi Blend Tree làm clip length thay đổi đáng kể.
            // FIX [CRITICAL #2 note]: length là clip duration gốc, không scale theo speed.
            // Quy đổi triggerSeconds / length vẫn cho normalized đúng vì normalizedTime
            // Unity chuẩn hóa trên [0,1] per-loop độc lập với speed.
            float currentClipLength = stateInfo.length;
            if (Mathf.Abs(currentClipLength - data.cachedClipLength) > ClipLengthRebakeEpsilon)
            {
                data.cachedClipLength = currentClipLength;
                for (int i = 0; i < count; i++)
                {
                    var mEvt = evts[i];
                    if (mEvt.mode != TriggerMode.Seconds) continue;
                    var rt = data.events[i];
                    if (mEvt.fireOnce && rt.hasFired) continue; // FIX [BUG #4 từ v6]
                    // FIX [MEDIUM v8-D]: Truyền isReverse (nay tính từ delta thực tế) để BakeCache
                    // có thể cảnh báo đúng, kể cả khi effectiveSpeed báo sai dấu.
                    mEvt.BakeCache(rt, currentClipLength, isReverse);
                }
            }

#if UNITY_EDITOR
            if (isReverse && showDebugLog)
            {
                Debug.Log(
                    $"[MotionTimeEvent][REVERSE] '{animator.name}' " +
                    $"frame={Time.frameCount} " +
                    $"prevNorm={prevNormTime:F4} currNorm={currNormTime:F4} " +
                    $"effectiveSpeed={effectiveSpeed:F3} " +
                    $"prevPos={Frac01(data.prevNormalizedTime):F4} currPos={Frac01(stateInfo.normalizedTime):F4}");
            }
#endif

            // FIX [v11]: đã bỏ 2 guard "forward jump discard" / "reverse seed skip" cũ —
            // với isReverse tính từ delta thực tế, cả hai điều kiện đó không còn xảy ra được
            // nữa (isReverse == true CHÍNH LÀ currNormTime < prevNormTime, nên không thể vừa
            // isReverse=true vừa currNormTime > prevNormTime, và ngược lại). Không còn frame
            // nào bị "nuốt" một cách âm thầm nữa.

            // lo <= hi luôn đúng (bất kể chiều chạy).
            float lo = isReverse ? currNormTime : prevNormTime;
            float hi = isReverse ? prevNormTime : currNormTime;

            // FIX [HIGH v9-B]: FloorInt thay cho (int) cast — (int) truncate về 0, sai với số âm.
            // FloorInt vẫn nhanh hơn Mathf.FloorToInt (không native call) và đúng cho mọi dấu.
            int loLoopIdx = FloorInt(lo);
            int hiLoopIdx = FloorInt(hi);
            bool didLoop = hiLoopIdx > loLoopIdx;

            // Phần dư [0,1) của vị trí thực sự (không phụ thuộc chiều đặt lo/hi), đúng cho cả số âm.
            float prevPos = Frac01(prevNormTime); // vị trí frame trước
            float currPos = Frac01(currNormTime); // vị trí frame này
            
#if UNITY_EDITOR
            if (isReverse && showDebugLog)
            {
                Debug.Log(
                    $"[MotionTimeEvent][REVERSE SCAN] frame={Time.frameCount} " +
                    $"lo={lo:F4} hi={hi:F4} loIdx={loLoopIdx} hiIdx={hiLoopIdx} " +
                    $"prevPos={prevPos:F4} currPos={currPos:F4} didLoop={didLoop}");
            }
#endif

            for (int i = 0; i < count; i++)
            {
                var evt = evts[i];
                var rt = data.events[i];

                if (evt.fireOnce && rt.hasFired) continue;

                // ── Boundary events ────────────────────────────────────────────────
                if (rt.cachedBoundary != -1)
                {
                    // GHI CHÚ [doc v9 / Bug #3]: với fireOnce=false, đường này CHỈ xử lý boundary
                    // khi đã loop qua trong Update (didLoop). Nếu clip không loop, boundary
                    // fireOnce=false vẫn được đảm bảo fire ở OnStateEnter/OnStateExit — hành vi
                    // này là chủ đích (Update chỉ lo phần "loop qua biên nhiều lần trong 1 frame").
                    if (!didLoop || evt.fireOnce) continue;

                    int completedLoops = hiLoopIdx - loLoopIdx;
                    for (int l = 0; l < completedLoops; l++)
                    {
                        if (rt.cachedBoundary == 1) // end: cuối loop l
                        {
                            int endIdx = loLoopIdx + l;
                            if (rt.lastFiredLoop == endIdx) continue;
                            FireEvent(evt, rt, animator, effectiveSpeed, data.cachedClipLength);
                            rt.lastFiredLoop = endIdx;
                        }
                        else // start: đầu loop l+1
                        {
                            int startIdx = loLoopIdx + l + 1;
                            if (rt.lastFiredLoop == startIdx) continue;
                            FireEvent(evt, rt, animator, effectiveSpeed, data.cachedClipLength);
                            rt.lastFiredLoop = startIdx;
                        }
                    }

                    continue;
                }

                // ── Midpoint events ────────────────────────────────────────────────
                float trigNorm = rt.cachedTriggerNorm;

                if (!isReverse)
                {
                    // Chiều xuôi: prevPos → currPos, qua 0..n loops.
                    for (int loop = loLoopIdx; loop <= hiLoopIdx; loop++)
                    {
                        if (evt.fireOnce && rt.hasFired) break;
                        if (rt.lastFiredLoop == loop) continue;

                        // from/to là phần dư [0,1) của khoảng quét trong loop này.
                        float from = (loop == loLoopIdx) ? prevPos : 0f;
                        float to = (loop == hiLoopIdx) ? currPos : 1f;

                        if (from >= to) continue;
                        if (trigNorm > from && trigNorm <= to)
                        {
                            // FIX [CRITICAL v8-A]: lastFiredLoop chỉ gán khi thực sự fire trong loop này.
                            // KHÔNG dùng hasFired cumulative (bug cũ gán loop dù trigNorm nằm ngoài range).
                            FireEvent(evt, rt, animator, effectiveSpeed, data.cachedClipLength);
                            rt.lastFiredLoop = loop;
                        }
                    }
                }
                else
                {
                    // Chiều ngược: prevPos → currPos (prevPos > currPos trong loop).
                    // lo = currNormTime, hi = prevNormTime.
                    // loop chạy từ hiLoopIdx (loop chứa prevNorm) xuống loLoopIdx (loop chứa currNorm).
                    for (int loop = hiLoopIdx; loop >= loLoopIdx; loop--)
                    {
                        if (evt.fireOnce && rt.hasFired) break;
                        if (rt.lastFiredLoop == loop) continue;

                        // from = vị trí bắt đầu quét (cao hơn), to = vị trí kết thúc (thấp hơn).
                        // loop == hiLoopIdx: bắt đầu từ prevPos.
                        // loop == loLoopIdx: kết thúc tại currPos.
                        // intermediate: quét toàn bộ [0,1) chiều giảm → from=1f, to=0f.
                        float from = (loop == hiLoopIdx) ? prevPos : 1f;
                        float to = (loop == loLoopIdx) ? currPos : 0f;

                        if (from <= to) continue;
                        
#if UNITY_EDITOR
                        if (showDebugLog)
                        {
                            Debug.Log(
                                $"[MotionTimeEvent][REVERSE LOOP] frame={Time.frameCount} " +
                                $"loop={loop} from={from:F4} to={to:F4} trigNorm={trigNorm:F4} " +
                                $"inRange={trigNorm >= to && trigNorm < from} lastFiredLoop={rt.lastFiredLoop}");
                        }
#endif
                        
                        // Chiều ngược: trigger khi trigNorm ∈ [to, from) (inclusive to, exclusive from).
                        if (trigNorm >= to && trigNorm < from)
                        {
                            // FIX [CRITICAL v8-A]: lastFiredLoop chỉ gán khi thực sự fire trong loop này.
                            FireEvent(evt, rt, animator, effectiveSpeed, data.cachedClipLength);
                            rt.lastFiredLoop = loop;
                        }
                    }
                }
            }

            // FIX [HIGH #4]: Sau overflow reset, gán lastFiredLoop = 0 (loop đầu của window mới).
            // Đảm bảo frame ngay sau reset không fire lại event vừa fire ở loop cuối window cũ.
            // FIX [MEDIUM v9-C]: dùng Mathf.Abs — reverse có thể trôi rất ÂM qua thời gian dài,
            // bản v8 chỉ check chiều dương nên bỏ sót overflow phía âm.
            if (Mathf.Abs(currNormTime) > LoopResetThreshold)
            {
                // FIX [LOW v9-F]: FloorInt thay Mathf.Floor — nhất quán với hot path, không native call,
                // và đúng cho cả currNormTime âm (Mathf.Floor vẫn đúng nhưng FloorInt nhanh hơn).
                int intPart = FloorInt(currNormTime);
                data.prevNormalizedTime = currNormTime - intPart; // phần dư [0,1)
                for (int i = 0; i < count; i++)
                {
                    var rt = data.events[i];
                    // Sau reset, loopIdx bắt đầu lại từ 0.
                    // Nếu lastFiredLoop == intPart (loop cuối), normalize về 0.
                    // Mọi trường hợp khác cũng gán 0 vì window cũ không còn ý nghĩa.
                    rt.lastFiredLoop = 0;
                }
            }
            else
            {
                data.prevNormalizedTime = currNormTime;
            }
        }

        /// <summary>
        /// FIX [HIGH #5]: IsAtEnd fire:
        ///   fireOnce=true  → chỉ fire nếu chưa fire lần nào (!hasFired).
        ///   fireOnce=false → luôn fire tại Exit, vì đây là "kết thúc state"
        ///                    bất kể đã fire trong loop trước hay chưa.
        /// IsAtEnd = OnStateExit — không đảm bảo clip đã chạy tới 100%.
        ///
        /// FIX [v10 — revert v8-B]: Exit LUÔN là boundary End (1), Enter LUÔN là boundary
        /// Start (0) — đây là sự kiện FSM (thứ tự thời gian thực), không phụ thuộc speed
        /// của clip bên trong. v8-B từng hoán đổi theo speed dựa trên giả định sai rằng
        /// Unity luôn seed normalizedTime≈1 khi Enter với speed âm — sai trừ khi cố tình
        /// set Transition Offset=1 để giả lập phát ngược thật sự.
        /// </summary>
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            int id = animator.GetInstanceID();
            if (!_instanceStates.TryGetValue(id, out var data)) return;

            // FIX [v10]: REVERT v8-B — xem giải thích chi tiết ở OnStateEnter.
            // Exit luôn là boundary End (1) của FSM, không phụ thuộc speed của clip bên trong.
            const int exitBoundary = 1;

            var evts = GetEventsArray();
            int count = data.events.Length;
            for (int i = 0; i < count; i++)
            {
                var evt = evts[i];
                var rt = data.events[i];

                if (rt.cachedBoundary != exitBoundary) continue;

                bool shouldFire = evt.fireOnce
                    ? !rt.hasFired // fireOnce: chỉ khi chưa fire
                    : true; // fireOnce=false: luôn fire tại Exit
                if (shouldFire)
                    // FIX [PERF v8-F]: Truyền stateInfo.speed thay vì gọi GetCurrentAnimatorStateInfo.
                    FireEvent(evt, rt, animator, stateInfo.speed, data.cachedClipLength);
            }

            _instanceStates.Remove(id);
        }

        /// <summary>
        /// Cleanup khi thoát sub-state-machine (không thay thế CleanupInstance).
        /// </summary>
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            _instanceStates.Remove(animator.GetInstanceID());
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Fire event. lastFiredLoop được ghi bởi CALLER sau FireEvent,
        /// ngoại trừ hasFired được ghi tại đây (source of truth cho fireOnce).
        /// PERF [#9]:  string interpolation hoàn toàn lazy — chỉ build khi showDebugLog=true.
        /// PERF [v8-F]: cachedSpeed truyền từ caller (stateInfo.speed) thay vì gọi lại
        ///              GetCurrentAnimatorStateInfo bên trong — tránh native call đắt.
        ///              OnStateEnter/Exit truyền stateInfo.speed trực tiếp.
        /// </summary>
        private void FireEvent(MotionTimeEvent evt, EventRuntime rt, Animator animator, float cachedSpeed,
            float clipLength)
        {
#if UNITY_EDITOR
            if (showDebugLog)
            {
                string pos = evt.mode == TriggerMode.Normalized
                    ? $"{rt.cachedTriggerNorm:F3} norm"
                    : $"{rt.cachedTriggerNorm * clipLength:F3}s";
                string ctx = rt.cachedBoundary == 0 ? "[Enter/LoopStart]"
                    : rt.cachedBoundary == 1 ? "[Exit/LoopEnd]"
                    : $"[mid norm={rt.cachedTriggerNorm:F3}]";
                // FIX [PERF v8-F]: Dùng cachedSpeed từ caller, không gọi GetCurrentAnimatorStateInfo.
                Debug.Log(
                    $"[MotionTimeEvent] '{animator.name}' @ {pos} {ctx} clip={clipLength:F3}s speed={cachedSpeed:F2}");
            }
#endif

            evt.Invoke(animator);
            rt.hasFired = true;
            // lastFiredLoop được ghi bởi caller để tránh coupling với loop context.
        }
    }
}