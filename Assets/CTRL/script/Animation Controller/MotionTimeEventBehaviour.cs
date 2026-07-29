using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// StateMachineBehaviour để kích hoạt event tại thời điểm cụ thể trong animation.
/// Hỗ trợ 2 chế độ: Seconds (giây) và Normalized (0.0 → 1.0).
/// Mỗi event nhận Animator làm tham số — dùng để leo hierarchy tìm component, không cần cache.
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

    [Serializable]
    public class MotionTimeEvent
    {
        [Tooltip("Seconds: nhập giây (vd: 1.5s)\nNormalized: nhập tỉ lệ 0→1 (vd: 0.5 = giữa motion)")]
        public TriggerMode mode = TriggerMode.Seconds;

        [Tooltip("Giá trị thời điểm kích hoạt theo mode đã chọn")]
        public float triggerValue = 0f;

        [Tooltip("Chỉ kích hoạt 1 lần mỗi lần vào state (nếu false: kích hoạt mỗi lần loop qua)")]
        public bool fireOnce = true;

        [Tooltip("Event được gọi khi đến thời điểm triggerValue. Tham số truyền vào là Animator hiện tại.")]
        public UnityEvent<Animator> onTriggered;

        // Runtime state
        [NonSerialized] public bool hasFired;

        /// <summary>Chuyển triggerValue về giây dựa trên clipLength</summary>
        public float GetTriggerTimeSeconds(float clipLength)
        {
            return mode == TriggerMode.Normalized
                ? Mathf.Clamp01(triggerValue) * clipLength
                : triggerValue;
        }
    }

    [Header("Danh sách Events theo thời gian")]
    [SerializeField] private List<MotionTimeEvent> motionEvents = new List<MotionTimeEvent>();

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    // Lưu normalizedTime của frame trước để detect loop
    private float previousNormalizedTime;

    // ─── Unity Animator Callbacks ─────────────────────────────────────────────

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        previousNormalizedTime = stateInfo.normalizedTime;
        float clipLength = stateInfo.length;

        foreach (var evt in motionEvents)
        {
            evt.hasFired = false;

            // triggerValue = 0 phải bắn ngay tại OnStateEnter
            // vì OnStateUpdate frame đầu có previousTime = 0, điều kiện > sẽ không bao giờ đúng
            float triggerSeconds = evt.GetTriggerTimeSeconds(clipLength);
            if (Mathf.Approximately(triggerSeconds, 0f))
            {
                if (showDebugLog)
                    Debug.Log($"[MotionTimeEvent] Fired at 0 [{evt.mode}] (OnStateEnter)");

                evt.onTriggered?.Invoke(animator);
                evt.hasFired = true;
            }
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float clipLength = stateInfo.length;
        float normalizedTime = stateInfo.normalizedTime;

        float currentTime  = (normalizedTime % 1f) * clipLength;
        float previousTime = (previousNormalizedTime % 1f) * clipLength;

        bool looped = Mathf.Floor(normalizedTime) > Mathf.Floor(previousNormalizedTime);

        // Reset hasFired khi loop để event fireOnce=false bắn lại chu kỳ mới
        if (looped)
        {
            foreach (var evt in motionEvents)
                if (!evt.fireOnce)
                    evt.hasFired = false;
        }

        foreach (var evt in motionEvents)
        {
            if (evt.fireOnce && evt.hasFired) continue;

            float t = evt.GetTriggerTimeSeconds(clipLength);
            bool shouldFire;

            if (looped)
                shouldFire = t >= previousTime || t <= currentTime;
            else
                shouldFire = t > previousTime && t <= currentTime;

            if (shouldFire)
            {
                if (showDebugLog)
                {
                    string label = evt.mode == TriggerMode.Normalized
                        ? $"{evt.triggerValue:F2} normalized ({t:F2}s)"
                        : $"{t:F2}s";
                    Debug.Log($"[MotionTimeEvent] Fired at {label} | clipLength={clipLength:F2}s | normalizedTime={normalizedTime:F3}");
                }

                evt.onTriggered?.Invoke(animator);
                evt.hasFired = true;
            }
        }

        previousNormalizedTime = normalizedTime;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        foreach (var evt in motionEvents)
            evt.hasFired = false;
    }

    // ─── Editor Utility ───────────────────────────────────────────────────────

#if UNITY_EDITOR
    public void AddEvent(float triggerValue, UnityAction<Animator> callback, TriggerMode mode = TriggerMode.Seconds, bool fireOnce = true)
    {
        var evt = new MotionTimeEvent
        {
            mode = mode,
            triggerValue = triggerValue,
            fireOnce = fireOnce
        };
        evt.onTriggered.AddListener(callback);
        motionEvents.Add(evt);
    }
#endif
}