using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

// ─── Custom Event Types ───────────────────────────────────────────────────────
// Khai báo ở đây để Unity serialize được và hiện tham số trong Inspector

[Serializable] public class AnimatorEvent         : UnityEvent<Animator>         { }
[Serializable] public class AnimatorIntEvent      : UnityEvent<Animator, int>    { }
[Serializable] public class AnimatorFloatEvent    : UnityEvent<Animator, float>  { }
[Serializable] public class AnimatorStringEvent   : UnityEvent<Animator, string> { }
[Serializable] public class AnimatorBoolEvent     : UnityEvent<Animator, bool>   { }

/// <summary>
/// StateMachineBehaviour để kích hoạt event tại thời điểm cụ thể trong animation.
/// Hỗ trợ 2 chế độ: Seconds (giây) và Normalized (0.0 → 1.0).
///
/// Xử lý đặc biệt:
///   - triggerValue = 0 hoặc normalizedValue = 0 → fire ngay trong OnStateEnter
///   - triggerValue = clipLength hoặc normalizedValue = 1 → fire ngay trong OnStateExit
///     (đảm bảo không bị miss khi transition xảy ra trước khi Update kịp bắt)
/// </summary>
public class MotionTimeEventBehaviour : StateMachineBehaviour
{
    public enum TriggerMode
    {
        [InspectorName("Seconds (s)")]
        Seconds,

        [InspectorName("Normalized (0 → 1)")]
        Normalized
    }

    /// <summary>Loại tham số thêm vào event</summary>
    public enum EventPayloadType
    {
        [InspectorName("Animator only")]
        None,

        [InspectorName("Animator || int")]
        Int,

        [InspectorName("Animator || float")]
        Float,

        [InspectorName("Animator || string")]
        String,

        [InspectorName("Animator || bool")]
        Bool
    }

    [Serializable]
    public class MotionTimeEvent
    {
        [Tooltip("Seconds: nhập giây (vd: 1.5s)\nNormalized: nhập tỉ lệ 0→1 (vd: 0.5 = giữa motion)")]
        public TriggerMode mode = TriggerMode.Seconds;

        [ShowIf("mode", TriggerMode.Seconds)]
        [Tooltip("Kích hoạt event tại thời điểm bạn chọn (s)")]
        public float triggerValue = 0f;

        [ShowIf("mode", TriggerMode.Normalized)]
        [Tooltip("Kích hoạt event tại thời điểm bạn chọn (%)")]
        [Range(0f, 1f)]
        public float normalizedValue = 0f;

        [Tooltip("Chỉ kích hoạt 1 lần mỗi lần vào state (nếu false: kích hoạt mỗi lần loop qua)")]
        public bool fireOnce = true;

        // ── Payload ────────────────────────────────────────────────────────────
        [Header("Extra Parameter")]
        [Tooltip("Chọn loại tham số thêm muốn truyền vào hàm callback")]
        public EventPayloadType payloadType = EventPayloadType.None;

        [ShowIf("payloadType", EventPayloadType.Int)]
        [Tooltip("Giá trị int truyền vào (chỉ dùng khi payloadType = Int)")]
        public int intValue;

        [ShowIf("payloadType", EventPayloadType.Float)]
        [Tooltip("Giá trị float truyền vào (chỉ dùng khi payloadType = Float)")]
        public float floatValue;

        [ShowIf("payloadType", EventPayloadType.String)]
        [Tooltip("Giá trị string truyền vào (chỉ dùng khi payloadType = String)")]
        public string stringValue;

        [ShowIf("payloadType", EventPayloadType.Bool)]
        [Tooltip("Giá trị bool truyền vào (chỉ dùng khi payloadType = Bool)")]
        public bool boolValue;

        // ── Events (chỉ 1 loại được dùng tùy payloadType) ─────────────────────
        [Header("Events")]
        [ShowIf("payloadType", EventPayloadType.None)]
        public AnimatorEvent       onTriggered;        // None

        [ShowIf("payloadType", EventPayloadType.Int)]
        public AnimatorIntEvent    onTriggeredInt;     // Int

        [ShowIf("payloadType", EventPayloadType.Float)]
        public AnimatorFloatEvent  onTriggeredFloat;   // Float

        [ShowIf("payloadType", EventPayloadType.String)]
        public AnimatorStringEvent onTriggeredString;  // String

        [ShowIf("payloadType", EventPayloadType.Bool)]
        public AnimatorBoolEvent   onTriggeredBool;    // Bool

        // ── Runtime ────────────────────────────────────────────────────────────
        [NonSerialized] public bool hasFired;

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Chuyển triggerValue về giây dựa trên clipLength</summary>
        public float GetTriggerTimeSeconds(float clipLength)
        {
            return mode == TriggerMode.Normalized
                ? Mathf.Clamp01(normalizedValue) * clipLength
                : triggerValue;
        }

        /// <summary>
        /// Trả về true nếu event này thuộc nhóm "fire tại t=0" (xử lý trong OnStateEnter).
        /// </summary>
        public bool IsAtStart()
        {
            return mode == TriggerMode.Normalized
                ? Mathf.Approximately(normalizedValue, 0f)
                : Mathf.Approximately(triggerValue, 0f);
        }

        /// <summary>
        /// Trả về true nếu event này thuộc nhóm "fire tại t=end" (xử lý trong OnStateExit).
        /// </summary>
        public bool IsAtEnd(float clipLength)
        {
            return mode == TriggerMode.Normalized
                ? Mathf.Approximately(normalizedValue, 1f)
                : Mathf.Approximately(triggerValue, clipLength);
        }

        /// <summary>Invoke đúng event theo payloadType đã chọn</summary>
        public void Invoke(Animator animator)
        {
            switch (payloadType)
            {
                case EventPayloadType.None:   onTriggered?.Invoke(animator);                    break;
                case EventPayloadType.Int:    onTriggeredInt?.Invoke(animator, intValue);       break;
                case EventPayloadType.Float:  onTriggeredFloat?.Invoke(animator, floatValue);   break;
                case EventPayloadType.String: onTriggeredString?.Invoke(animator, stringValue); break;
                case EventPayloadType.Bool:   onTriggeredBool?.Invoke(animator, boolValue);     break;
            }
        }
    }

    // ─── Inspector Fields ─────────────────────────────────────────────────────

    [Header("Danh sách Events theo thời gian")]
    [SerializeField] private List<MotionTimeEvent> motionEvents = new List<MotionTimeEvent>();

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    // ─── Runtime State ────────────────────────────────────────────────────────

    private float previousNormalizedTime;

    // ─── Unity Animator Callbacks ─────────────────────────────────────────────

    /// <summary>
    /// Gọi 1 lần khi vào state.
    /// Xử lý đặc biệt: fire ngay các event có trigger time = 0.
    /// </summary>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        previousNormalizedTime = stateInfo.normalizedTime;
        float clipLength = stateInfo.length;

        foreach (var evt in motionEvents)
        {
            evt.hasFired = false;

            if (evt.IsAtStart())
            {
                FireEvent(evt, animator, "0 (OnStateEnter)", clipLength);
            }
        }
    }

    /// <summary>
    /// Gọi mỗi frame khi đang ở state.
    /// Kiểm tra và fire các event có trigger time nằm trong khoảng thời gian vừa trôi qua.
    /// Bỏ qua các event tại t=0 (đã xử lý ở OnStateEnter) và t=end (sẽ xử lý ở OnStateExit).
    /// </summary>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float clipLength     = stateInfo.length;
        float normalizedTime = stateInfo.normalizedTime;
        float currentTime    = (normalizedTime % 1f) * clipLength;
        float previousTime   = (previousNormalizedTime % 1f) * clipLength;
        bool  looped         = Mathf.Floor(normalizedTime) > Mathf.Floor(previousNormalizedTime);

        // Khi loop xảy ra: reset hasFired cho các event không phải fireOnce
        if (looped)
        {
            foreach (var evt in motionEvents)
                if (!evt.fireOnce)
                    evt.hasFired = false;
        }

        foreach (var evt in motionEvents)
        {
            if (evt.fireOnce && evt.hasFired) continue;

            // Bỏ qua t=0 và t=end — đã/sẽ được xử lý ở Enter/Exit
            if (evt.IsAtStart() || evt.IsAtEnd(clipLength)) continue;

            float t = evt.GetTriggerTimeSeconds(clipLength);

            bool shouldFire = looped
                ? (t >= previousTime || t <= currentTime)   // qua loop boundary
                : (t >  previousTime && t <= currentTime);  // bình thường

            if (shouldFire)
            {
                string label = evt.mode == TriggerMode.Normalized
                    ? $"{evt.normalizedValue:F2} normalized ({t:F2}s)"
                    : $"{t:F2}s";

                FireEvent(evt, animator, label, clipLength);
            }
        }

        previousNormalizedTime = normalizedTime;
    }

    /// <summary>
    /// Gọi 1 lần khi thoát state.
    /// Xử lý đặc biệt: fire các event có trigger time = clipLength (normalizedValue = 1)
    /// mà chưa được fire, đảm bảo không bị miss khi transition xảy ra sớm.
    /// Sau đó reset toàn bộ hasFired.
    /// </summary>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float clipLength = stateInfo.length;

        foreach (var evt in motionEvents)
        {
            if (!evt.hasFired && evt.IsAtEnd(clipLength))
            {
                FireEvent(evt, animator, "1 (OnStateExit)", clipLength);
            }

            evt.hasFired = false;
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private void FireEvent(MotionTimeEvent evt, Animator animator, string debugLabel, float clipLength)
    {
        if (showDebugLog)
            Debug.Log($"[MotionTimeEvent] Fired at {debugLabel} | clipLength={clipLength:F2}s");

        evt.Invoke(animator);
        evt.hasFired = true;
    }
}