// GaMoPreviewTimeBridge.cs  -  part of GaMo Inspector tool
// Cau noi giua Inspector cua StateMachineBehaviour (vd MotionTimeEventBehaviourEditor) va khung Preview animation
// cua GM-Inspector: ben gui chi bao "State + Motion + normalized time", ben nghe (CustomsEditor) tu quyet dinh co tua hay khong
// (chi khi Preview dang mo va dang xem dung Motion do). Them 2 duong doc: TryGetPreviewTime (doc playhead, chi khi bam nut)
// va CollectMarkers (Preview hoi vach event de ve). Hai ben khong biet nhau => khong rang buoc chat, khong vong phan hoi.
// Chi doc du lieu + doi trang thai Preview (TimeControl noi bo): KHONG ghi gi vao asset / scene.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public static class GaMoPreviewTimeBridge
    {
        public struct SeekRequest
        {
            public AnimatorState state;     // State đang chứa behaviour (GM-Inspector đang xem đúng State này mới tua)
            public Motion motion;           // Motion của State (AnimationClip / BlendTree)
            public float normalizedTime;    // 0..1 trên khoảng thời gian Preview của Motion
        }

        /// <summary>Một vạch đánh dấu trên dải thời gian của Preview (vd thời điểm của 1 MotionTimeEvent).</summary>
        public struct TimeMarker
        {
            public float normalizedTime;
            public string label;            // hiện cạnh vạch khi selected
            public bool selected;           // event đang chọn trong Inspector => tô nổi
        }

        public delegate void MarkerProvider(AnimatorState state, Motion motion, List<TimeMarker> output);
        public delegate bool TimeSource(AnimatorState state, Motion motion, out float normalizedTime);

        /// <summary>Phát ra khi một Inspector muốn khung Preview tua tới thời điểm này.</summary>
        public static event Action<SeekRequest> SeekRequested;

        /// <summary>Preview hỏi: State + Motion này có vạch nào cần vẽ không (bên behaviour thêm vào output).</summary>
        public static event MarkerProvider MarkersRequested;

        // Nguồn đọc ngược thời gian hiện tại của Preview (mỗi cửa sổ GM-Inspector 1 nguồn). Dùng List thay event
        // vì cần dừng ở nguồn đầu tiên trả lời được (event multicast có out không làm được việc đó).
        static readonly List<TimeSource> s_TimeSources = new List<TimeSource>();

        public static void AddTimeSource(TimeSource src) { if (src != null && !s_TimeSources.Contains(src)) s_TimeSources.Add(src); }
        public static void RemoveTimeSource(TimeSource src) { s_TimeSources.Remove(src); }
        public static bool HasTimeSources => s_TimeSources.Count > 0;

        /// <summary>Đọc playhead (normalized) của Preview đang xem đúng State + Motion. Không có => false.</summary>
        public static bool TryGetPreviewTime(AnimatorState state, Motion motion, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (state == null || motion == null) return false;
            for (int i = 0; i < s_TimeSources.Count; i++)
            {
                try { if (s_TimeSources[i](state, motion, out normalizedTime)) return true; }
                catch (Exception) { }
            }
            return false;
        }

        public static void CollectMarkers(AnimatorState state, Motion motion, List<TimeMarker> output)
        {
            output.Clear();
            var h = MarkersRequested;
            if (h == null || state == null || motion == null) return;
            foreach (MarkerProvider d in h.GetInvocationList())
            {
                try { d(state, motion, output); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        /// <summary>Có cửa sổ nào đang nghe không (không có thì bên gửi khỏi tốn công dò State).</summary>
        public static bool HasListeners => SeekRequested != null;

        public static void RequestSeek(AnimatorState state, Motion motion, float normalizedTime)
        {
            var h = SeekRequested;
            if (h == null || state == null || motion == null) return;
            var req = new SeekRequest { state = state, motion = motion, normalizedTime = Mathf.Clamp01(normalizedTime) };
            foreach (Action<SeekRequest> d in h.GetInvocationList())
            {
                try { d(req); }
                catch (Exception ex) { Debug.LogException(ex); } // một cửa sổ lỗi không chặn các cửa sổ khác
            }
        }

        /// <summary>
        /// Tìm State chứa behaviour (behaviour gắn ở StateMachine thì không có Motion riêng => false).
        /// Dùng AnimatorController.FindStateMachineBehaviourContext (API Editor), chỉ gọi khi giá trị đổi nên không tốn.
        /// </summary>
        public static bool TryResolveState(StateMachineBehaviour behaviour, out AnimatorState state, out Motion motion)
        {
            state = null;
            motion = null;
            if (behaviour == null) return false;
            try
            {
                var ctx = AnimatorController.FindStateMachineBehaviourContext(behaviour);
                if (ctx == null) return false;
                foreach (var c in ctx)
                {
                    if (!(c.animatorObject is AnimatorState st)) continue;
                    state = st;
                    motion = st.motion;
                    return motion != null;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Độ dài (giây) của Motion để đổi Seconds -> normalized. BlendTree: độ dài trung bình theo trọng số hiện tại.</summary>
        public static float MotionLength(Motion motion)
        {
            if (motion is AnimationClip clip) return clip.length;
            return motion != null ? motion.averageDuration : 0f;
        }
    }
}
#endif
