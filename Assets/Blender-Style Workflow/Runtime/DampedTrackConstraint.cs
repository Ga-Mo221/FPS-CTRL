using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Damped Track Constraint — tương đương "Damped Track" trong Blender.
    /// Xoay <see cref="constraintObject"/> sao cho một trục cục bộ (<see cref="trackAxis"/>) luôn
    /// hướng thẳng về Target, dùng swing rotation NHỎ NHẤT (xem DampedTrackMath) thay vì
    /// LookRotation kiểu "Track To" → không cần Up Axis, không gimbal/flip, roll liên tục mượt.
    /// Hỗ trợ Multi-Target (blend nhiều target theo Weight), API runtime giống TransformConstraint.
    /// </summary>
    [AddComponentMenu("Gà Mờ/Damped Track Constraint")]
    public class DampedTrackConstraint : ConstraintBehaviour
    {
        public enum TrackAxis { X, Y, Z, NegX, NegY, NegZ }

        [System.Serializable]
        public class TargetData
        {
            [Tooltip(
                "ID định danh duy nhất cho Target này (VD: 'enemy', 'player', 'muzzle_point'). " +
                "Dùng để gọi từ code khi cần đổi mục tiêu bám theo.")]
            public string id;

            [Tooltip("Transform của vật thể đích mà Constraint Object sẽ hướng Track Axis về phía nó.")]
            public Transform target;

            [Range(0f, 1f)]
            [Tooltip(
                "Trọng số ảnh hưởng riêng của Target này.\n• 1 = Chịu ảnh hưởng tối đa.\n• 0 = Hoàn toàn bỏ qua.\n" +
                "Nếu dùng nhiều Target (Multi-Target), hệ thống tự động hòa trộn hướng nhìn theo tỷ lệ Weight giữa chúng.\n" +
                "Khi bật Auto Tracking: Weight > 0 = Target đủ điều kiện để được chọn; Weight = 0 = loại khỏi Auto Tracking.")]
            public float weight = 1f;

            // Trạng thái runtime của Obstacle Check (không serialize).
            // visibility: 1 = thấy rõ, 0 = bị che hoàn toàn (fade mượt theo occlusionBlendTime).
            [System.NonSerialized] public float visibility = 1f;
            [System.NonSerialized] public bool occluded;

            // Trạng thái runtime của Auto Tracking (không serialize).
            // autoBlend: tỷ lệ tham gia blend do Auto Tracking quyết định (0..1, tổng các Target = 1).
            [System.NonSerialized] public float autoBlend;

            // Mốc thời gian được raycast lại kế tiếp (chỉ dùng để giãn tần suất cho Target không tham gia blend).
            [System.NonSerialized] public float nextProbeTime;
        }

        [Tooltip(
            "Danh sách các điểm đích để hướng theo. Có thể thêm nhiều mục tiêu và blend bằng Weight " +
            "(VD: xoay đầu súng blend giữa 2 kẻ địch, hoặc mắt nhân vật liếc qua lại nhiều điểm).")]
        public List<TargetData> targets = new List<TargetData>();

        [Tooltip(
            "Đối tượng trực tiếp chịu ràng buộc (VD: xương nòng súng, turret, xương cổ/mắt). " +
            "Trục Track Axis của vật thể này sẽ bị xoay để hướng về Target.")]
        public Transform constraintObject;

        [Tooltip(
            "✔ TRUE (mặc định, giữ hành vi cũ): constraintObject có Animator/IK ghi đè Rotation mỗi frame " +
            "→ baseline khi Influence = 0 đọc TRỰC TIẾP transform hiện tại (đã được driver reset sẵn).\n" +
            "❌ FALSE: Không có gì điều khiển constraintObject mỗi frame → nếu vẫn đọc transform hiện tại " +
            "làm baseline thì baseline sẽ bị \"ô nhiễm\" bởi chính output constraint frame trước, kéo " +
            "Influence về 0 sẽ KHÔNG về đúng hướng gốc (bug trôi dần).\n" +
            "Khi tắt, hệ thống sẽ chụp lại Rest Pose (local rotation) MỘT LẦN lúc OnEnable — hoặc bất cứ " +
            "lúc nào bạn bấm \"Capture Rest Pose Now\" trong Inspector — và luôn dùng pose đó làm baseline " +
            "cố định, không bao giờ tự trôi.")]
        public bool hasExternalPoseDriver = true;

        // Rest Pose chụp MỘT LẦN (local rotation) làm baseline cố định khi hasExternalPoseDriver = false.
        private Quaternion _restLocalRot;
        private bool _restPoseCaptured = false;
        private bool _restPoseReady = false;

        [Tooltip(
            "Trục cục bộ của Constraint Object sẽ được xoay để hướng về Target.\n" +
            "VD: model súng hướng nòng theo +Z thì chọn Z; nếu model hướng ngược (hay gặp khi import " +
            "từ Blender) thì đổi sang -Z. Thử lần lượt nếu không chắc model hướng trục nào.")]
        public TrackAxis trackAxis = TrackAxis.Z;

        [Tooltip(
            "✔ TRUE (khuyên dùng): Tự động chuẩn hóa Weight của các Target trước khi blend.\n" +
            "Ví dụ A=0.3, B=0.3 → blend 50/50 và influence luôn đúng bằng giá trị Influence.\n" +
            "❌ FALSE (hành vi cũ): totalWeight ảnh hưởng trực tiếp đến influence cuối cùng — hữu ích " +
            "khi muốn Influence tự giảm theo tổng Weight (VD: blend-in nhiều target dần dần).")]
        public bool normalizeWeights = true;

        [Range(0f, 1f)]
        [Tooltip(
            "Mức độ ảnh hưởng tổng thể của constraint.\n• 1 = Hướng khớp hoàn toàn theo Target.\n" +
            "• 0 = Trả về theo Animation/Rest Pose gốc.\n• 0.5 = Nằm chính giữa hướng Animation và hướng Target.")]
        public float influence = 1f;

        [Range(0.5f, MaxFollowSpeed)]
        [Tooltip(
            "Tốc độ bám theo Target (làm mượt kiểu quán tính).\n" +
            "• 30 (tối đa) = bám TỨC THÌ, không làm mượt (hành vi cũ).\n" +
            "• Giá trị thấp hơn = đầu xoay trễ/mượt hơn. VD: 5 ≈ khoảng 0.2s để bắt kịp, 1 ≈ rất lờ đờ.\n" +
            "Áp dụng cho cả lúc bám Target lẫn lúc nhả về pose gốc, và vẫn đi theo animation thân.")]
        public float followSpeed = MaxFollowSpeed;

        private const float MaxFollowSpeed = 30f;
        private Quaternion _smoothDelta = Quaternion.identity; // output tương đối với pose gốc (đã làm mượt)
        private bool _smoothValid;

        [Tooltip(
            "Offset góc xoay (độ, Euler X/Y/Z) cộng thêm SAU khi đã track về Target, tính theo hệ trục LOCAL " +
            "của Constraint Object.\n" +
            "• Xoay quanh Track Axis = lệch roll; quanh 2 trục còn lại = nhìn lệch khỏi Target " +
            "(VD: bù trục xương không khớp hướng mặt, hoặc cho mắt/đầu nhìn hơi chếch).\n" +
            "Offset tự giảm theo Influence và về 0 khi nhả về pose gốc, không ảnh hưởng việc kiểm tra giới hạn góc.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip(
            "✔ TRUE: Giới hạn góc quay 4 hướng (trái/phải/lên/xuống) so với pose gốc (animation/rest).\n" +
            "Vượt ngưỡng → đầu dừng tại tư thế đó rồi từ từ xoay về pose gốc (không bám theo Target ra ngoài vùng cho phép, không xoay ngược ra sau).")]
        public bool useLimits = false;

        [Tooltip(
            "Trục cục bộ của Constraint Object được coi là 'hướng lên' (trục quay yaw). " +
            "Xương đầu thường là Y; nếu trái/phải hoặc lên/xuống bị ngược thì thử trục khác. " +
            "Không được trùng với Track Axis.")]
        public TrackAxis limitUpAxis = TrackAxis.Y;

        [Range(0f, 180f)] [Tooltip("Góc tối đa quay sang TRÁI (độ).")]
        public float maxYawLeft = 70f;

        [Range(0f, 180f)] [Tooltip("Góc tối đa quay sang PHẢI (độ).")]
        public float maxYawRight = 70f;

        [Range(0f, 90f)] [Tooltip("Góc tối đa ngẩng LÊN (độ).")]
        public float maxPitchUp = 40f;

        [Range(0f, 90f)] [Tooltip("Góc tối đa cúi XUỐNG (độ).")]
        public float maxPitchDown = 30f;

        [Range(0f, 30f)]
        [Tooltip("Vùng đệm (độ): sau khi vượt ngưỡng, target phải quay vào trong ngưỡng thêm chừng này " +
                 "thì đầu mới bám lại. Chống rung ở biên.")]
        public float limitMargin = 5f;

        [Min(0.01f)]
        [Tooltip("Thời gian (giây) để nhả hoàn toàn về pose gốc / bám lại hoàn toàn khi ra/vào ngưỡng.")]
        public float returnDuration = 0.6f;

        [Tooltip(
            "✔ TRUE: Khi vượt ngưỡng, đầu GIỮ NGUYÊN tư thế (chững lại) trong Limit Hold Duration giây " +
            "rồi mới bắt đầu từ từ xoay về pose gốc.\n" +
            "❌ FALSE: Vẫn đứng yên tại tư thế ngay lúc vượt ngưỡng nhưng bắt đầu xoay về gốc ngay lập tức.")]
        public bool useLimitHold = false;

        [Min(0f)]
        [Tooltip("Thời gian (giây) đầu chững lại tại chỗ sau khi vượt ngưỡng, trước khi xoay về pose gốc.")]
        public float limitHoldDuration = 0.5f;

        // ── Trạng thái cổng (gate) chung cho Limits + Obstacle Check ──
        // Khi bị chặn (vượt ngưỡng / mọi Target bị che): đầu ĐÓNG BĂNG tại _aimDelta (tương đối với
        // pose gốc, nên vẫn đi theo animation thân), chờ hold, rồi _gateFactor tụt dần về 0.
        private float _gateFactor = 1f;
        private float _gateFade = 0.5f;
        private float _holdTimer;
        private bool _gateBlocked;
        private bool _outOfLimit;
        private bool _aimValid;
        private bool _aimReacquiring;
        private Quaternion _aimDelta = Quaternion.identity;

        [Tooltip(
            "✔ TRUE: Bắn raycast từ Constraint Object tới từng Target. Nếu có vật cản (collider) chắn giữa " +
            "→ Target đó bị coi là KHÔNG nhìn thấy, đầu từ từ xoay về pose gốc thay vì nhìn xuyên qua vật cản.\n" +
            "Chỉ hỗ trợ Physics 3D (Collider, không phải Collider2D).")]
        public bool checkObstacles = false;

        [Tooltip(
            "Các Layer được coi là vật cản. Nên LOẠI layer của chính nhân vật và layer của Target " +
            "(hoặc để Ignore Own Hierarchy bật). Trigger Collider luôn bị bỏ qua.")]
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [Tooltip(
            "✔ TRUE (khuyên dùng): Bỏ qua mọi collider thuộc hierarchy của nhân vật (root của Constraint Object) " +
            "→ collider thân/đầu của chính nhân vật không bị tính là vật cản.\n" +
            "Collider nằm trong chính Target luôn được bỏ qua bất kể tuỳ chọn này.")]
        public bool ignoreOwnHierarchy = true;

        [Min(0.01f)]
        [Tooltip("Thời gian (giây) để Target chuyển hẳn từ 'thấy' sang 'bị che' (nhả về pose gốc) và ngược lại. " +
                 "Giá trị lớn giúp tránh giật khi vật cản mỏng lướt qua tầm nhìn.")]
        public float occlusionBlendTime = 0.4f;

        [Tooltip(
            "✔ TRUE: Khi TẤT CẢ Target đang có Weight bị vật cản che, đầu GIỮ NGUYÊN tư thế (chững lại) " +
            "trong Occlusion Hold Duration giây rồi mới bắt đầu từ từ xoay về pose gốc.\n" +
            "Hữu ích để vật cản lướt qua nhanh không làm đầu quay đi quay lại.")]
        public bool useOcclusionHold = false;

        [Min(0f)]
        [Tooltip("Thời gian (giây) đầu chững lại tại chỗ sau khi bị che, trước khi xoay về pose gốc.")]
        public float occlusionHoldDuration = 0.5f;

        private const float ObstacleEndPadding = 0.05f; // dừng ray sát Target, tránh dính mặt đất/bề mặt dưới chân Target
        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

        // ── Auto Tracking ──
        [Tooltip(
            "✔ TRUE: Tự động chọn Target để bám theo. CHỈ hoạt động khi có từ 2 Target hợp lệ trở lên " +
            "(Transform khác null và Weight > 0); ít hơn thì chạy bình thường (blend theo Weight).\n" +
            "Ưu tiên: Target GẦN NHẤT trong số các Target nằm trong giới hạn góc (nếu bật Use Limits) " +
            "và không bị vật cản che (nếu bật Check Obstacles).\n" +
            "• Không còn Target hợp lệ nào: giữ Target hiện tại, Limits/Obstacle tự xử lý (chững lại rồi nhả về pose gốc).\n" +
            "• Weight vẫn là mức ảnh hưởng riêng của Target; Weight = 0 loại Target khỏi Auto Tracking.\n" +
            "Lưu ý: SwitchTarget đưa các Target khác về Weight 0 nên sẽ tạm dừng Auto Tracking.")]
        public bool autoTracking = false;

        [Min(0f)]
        [Tooltip("Thời gian (giây) chuyển mượt giữa 2 Target khi Auto Tracking đổi mục tiêu. 0 = đổi tức thì.")]
        public float autoSwitchBlendTime = 0.3f;

        [Min(0f)]
        [Tooltip("Target mới phải GẦN HƠN Target hiện tại ít nhất chừng này (đơn vị world) thì mới đổi " +
                 "(khi cả hai đều hợp lệ). Chống nhảy qua lại giữa 2 Target có khoảng cách gần bằng nhau.")]
        public float autoSwitchMargin = 0.5f;

        [Min(0f)]
        [Tooltip("Thời gian tối thiểu (giây) giữa 2 lần đổi Target vì 'gần hơn'. Khi Target hiện tại không còn " +
                 "hợp lệ (ra ngoài giới hạn / bị che) thì đổi ngay, không chờ.")]
        public float autoMinSwitchInterval = 0.3f;

        // Target Auto Tracking đang KHÔNG tham gia blend chỉ cần raycast thưa (phục vụ việc chọn Target).
        // Mỗi Target lệch nhau chút ít (xem Solve) để các raycast không dồn vào cùng 1 frame.
        private const float AutoProbeInterval = 0.1f;

        // Cache mỗi frame cho kiểm tra giới hạn của Auto Tracking (tránh tính lại cho từng Target)
        private Quaternion _limInvBase;
        private Vector3 _limRight, _limUp;
        private bool _limBasisOk;

        private TargetData _autoSelected;
        private bool _autoActive;
        private float _autoSwitchTimer;

        /// <summary>true khi Auto Tracking đang thực sự chạy (đã bật và có từ 2 Target hợp lệ).</summary>
        public bool IsAutoTrackingActive => autoTracking && _autoActive;

        /// <summary>ID của Target đang được Auto Tracking chọn (null nếu Auto Tracking không chạy).</summary>
        public string AutoTargetId => (IsAutoTrackingActive && _autoSelected != null) ? _autoSelected.id : null;

        [Tooltip("✔ TRUE: Vẽ gizmo trục Track Axis + đường nối tới các Target đang có Weight > 0.")]
        public bool showGizmos = false;

        [Tooltip("Kích thước mũi tên gizmo Track Axis, tính theo đơn vị world-space.")]
        public float gizmoSize = 0.3f;

        [Tooltip("Màu mũi tên Track Axis.")]
        public Color gizmoAxisColor = Color.cyan;

        [Tooltip("Màu đường nối từ Constraint Object tới các Target đang có Weight > 0.")]
        public Color gizmoLineColor = new Color(1f, 1f, 0f, 0.6f);

        [Tooltip("Màu đường nối tới Target đang BỊ VẬT CẢN CHE (chỉ thấy khi Play và bật Check Obstacles).")]
        public Color gizmoBlockedColor = new Color(1f, 0.2f, 0.2f, 0.8f);

        [Tooltip("Màu đường nối tới Target đang được Auto Tracking chọn (chỉ thấy khi Play).")]
        public Color gizmoSelectedColor = new Color(0.2f, 1f, 0.3f, 0.9f);

        private Coroutine switchCoroutine;
        private Coroutine _influenceRoutine; // fade Influence (SetInfluence có duration)

        // Coroutine fade weight riêng cho từng Target (SetTargetWeight có duration), key = id
        private readonly Dictionary<string, Coroutine> _weightRoutines = new Dictionary<string, Coroutine>();

        // Cache để tránh alloc mỗi lần gọi SwitchTarget
        private float[] _cachedStartWeights = new float[0];

        // ── Chống drift khi hasExternalPoseDriver = true nhưng driver KHÔNG ghi lại rotation ──
        // Cùng vấn đề/giải pháp như TransformConstraint.cs, chỉ áp dụng cho rotation vì
        // Damped Track không đụng tới Position.
        private bool _hasLastOutput;
        private bool _driverSkippedThisFrame;
        private Quaternion _lastOutLocalRot;
        private Quaternion _lastBaseLocalRot;

        protected override void OnEnable()
        {
            base.OnEnable(); // đăng ký với ConstraintScheduler
            _hasLastOutput = false;
            _restPoseReady = false;
            _gateFactor = 1f;
            _holdTimer = 0f;
            _gateBlocked = false;
            _outOfLimit = false;
            _aimValid = false;
            _aimReacquiring = false;
            _smoothValid = false;
            _autoSelected = null;
            _autoActive = false;
            _autoSwitchTimer = 0f;
            _weightRoutines.Clear();
            _influenceRoutine = null;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                targets[i].visibility = 1f;
                targets[i].occluded = false;
                targets[i].autoBlend = 0f;
                targets[i].nextProbeTime = 0f;
            }

            if (!hasExternalPoseDriver)
                StartCoroutine(CaptureRestPoseNextFrame());
            else
                _restPoseReady = true;
        }

        private IEnumerator CaptureRestPoseNextFrame()
        {
            yield return null; // chờ Animator/IK evaluate xong frame đầu
            CaptureRestPose();
            _restPoseReady = true;
        }

        /// <summary>
        /// Chụp lại Rest Pose (local rotation hiện tại của constraintObject) để dùng làm baseline
        /// cố định khi hasExternalPoseDriver = false. Gọi hàm này thủ công (nút "Capture Rest Pose Now"
        /// trong Inspector, hoặc từ code) bất cứ khi nào muốn "khóa" lại pose gốc mới.
        /// </summary>
        public void CaptureRestPose()
        {
            if (constraintObject == null) return;
            _restLocalRot = constraintObject.localRotation;
            _restPoseCaptured = true;
        }

        private Quaternion GetBaselineWorldRot()
        {
            if (hasExternalPoseDriver && !_driverSkippedThisFrame) return constraintObject.rotation;

            Quaternion localRot = GetBaselineLocalRot();
            Transform parent = constraintObject.parent;
            return parent != null ? parent.rotation * localRot : localRot;
        }

        private Quaternion GetBaselineLocalRot()
        {
            if (hasExternalPoseDriver)
                return _driverSkippedThisFrame ? _lastBaseLocalRot : constraintObject.localRotation;
            if (!_restPoseCaptured) CaptureRestPose();
            return _restLocalRot;
        }

        private static bool BitEqual(Quaternion a, Quaternion b)
            => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

        /// <summary>
        /// true nếu transform vẫn giữ nguyên output constraint frame trước → driver (Animator) đã
        /// KHÔNG ghi đè rotation frame này (xem giải thích chi tiết trong TransformConstraint.cs).
        /// </summary>
        private bool DetectDriverSkipped()
        {
            if (!hasExternalPoseDriver || !_hasLastOutput) return false;
            return BitEqual(constraintObject.localRotation, _lastOutLocalRot);
        }

        /// <summary>
        /// true nếu hướng của <paramref name="liveRot"/> nằm trong giới hạn 4 hướng (có hysteresis limitMargin).
        /// </summary>
        private bool IsInsideLimits(Quaternion baseWorldRot, Quaternion liveRot, Vector3 localAxis)
        {
            DampedTrackMath.GetYawPitch(baseWorldRot, liveRot, localAxis,
                DampedTrackMath.GetTrackAxisVector(limitUpAxis), out float yaw, out float pitch);

            float m = _outOfLimit ? limitMargin : 0f; // hysteresis chống rung ở biên
            bool inside =
                yaw   >= -Mathf.Max(0f, maxYawLeft   - m) &&
                yaw   <=  Mathf.Max(0f, maxYawRight  - m) &&
                pitch <=  Mathf.Max(0f, maxPitchUp   - m) &&
                pitch >= -Mathf.Max(0f, maxPitchDown - m);

            _outOfLimit = !inside;
            return inside;
        }

        /// <summary>
        /// Cổng chung cho Limits + Obstacle Check. Trả về rotation đích (world) và hệ số nhân influence.
        /// • Bình thường: bám đúng liveRot, gateMul = 1.
        /// • Bị chặn (vượt ngưỡng hoặc mọi Target bị che): ĐÓNG BĂNG tư thế tại thời điểm bắt đầu bị chặn
        ///   (lưu tương đối với pose gốc → vẫn đi theo animation thân), chờ hold (nếu bật), rồi gateMul tụt
        ///   dần về 0 → đầu xoay về pose gốc mà KHÔNG bám theo Target đang ở ngoài vùng cho phép.
        /// • Hết bị chặn: gateMul tăng lại và aim chuyển mượt từ tư thế đóng băng sang liveRot (không giật).
        /// </summary>
        private Quaternion EvaluateGate(Quaternion baseWorldRot, Quaternion liveRot, Vector3 localAxis,
            bool occludedAll, out float gateMul)
        {
            bool limitOut = useLimits && !IsInsideLimits(baseWorldRot, liveRot, localAxis);
            bool blocked = limitOut || occludedAll;
            float dt = Time.deltaTime;

            Quaternion liveDelta = Quaternion.Inverse(baseWorldRot) * liveRot;

            if (!_aimValid)
            {
                // Lần đầu: nếu đã bị chặn ngay từ đầu thì bắt đầu từ pose gốc, không giật tới tư thế bị cấm.
                _aimValid = true;
                _aimDelta = blocked ? Quaternion.identity : liveDelta;
                _gateFactor = blocked ? 0f : 1f;
                _aimReacquiring = blocked;
                _gateBlocked = blocked;
                _holdTimer = 0f;
            }

            if (blocked)
            {
                float hold = 0f;
                float fade = 0.01f;
                if (limitOut)
                {
                    if (useLimitHold) hold = Mathf.Max(hold, limitHoldDuration);
                    fade = Mathf.Max(fade, returnDuration);
                }
                if (occludedAll)
                {
                    if (useOcclusionHold) hold = Mathf.Max(hold, occlusionHoldDuration);
                    fade = Mathf.Max(fade, occlusionBlendTime);
                }

                _gateFade = fade;

                if (!_gateBlocked)
                {
                    _gateBlocked = true;
                    _holdTimer = 0f;
                }

                _aimReacquiring = true;
                _holdTimer += dt;

                // Hết thời gian chững → bắt đầu nhả về pose gốc. _aimDelta giữ nguyên (đóng băng).
                if (_holdTimer > hold)
                    _gateFactor = Mathf.MoveTowards(_gateFactor, 0f, dt / fade);
            }
            else
            {
                _gateBlocked = false;
                _holdTimer = 0f;
                _gateFactor = Mathf.MoveTowards(_gateFactor, 1f, dt / _gateFade);

                if (_aimReacquiring)
                {
                    // Làm mượt từ tư thế đóng băng về hướng live (tránh nhảy khi gateFactor chưa về 1).
                    float k = 1f - Mathf.Exp(-dt * 5f / _gateFade);
                    _aimDelta = Quaternion.Slerp(_aimDelta, liveDelta, k);
                    if (Quaternion.Angle(_aimDelta, liveDelta) < 0.5f)
                    {
                        _aimDelta = liveDelta;
                        _aimReacquiring = false;
                    }
                }
                else
                {
                    _aimDelta = liveDelta;
                }
            }

            gateMul = Smooth01(_gateFactor);
            return baseWorldRot * _aimDelta;
        }

        /// <summary>
        /// true nếu có collider chắn giữa from và Target (bỏ qua collider của chính Target và,
        /// nếu bật, của hierarchy nhân vật).
        /// </summary>
        private bool IsOccluded(Vector3 from, TargetData t)
        {
            Vector3 diff = t.target.position - from;
            float dist = diff.magnitude;
            if (dist < 0.01f) return false;

            float checkDist = Mathf.Max(0f, dist - ObstacleEndPadding);
            int n = Physics.RaycastNonAlloc(from, diff / dist, _hitBuffer, checkDist,
                obstacleMask, QueryTriggerInteraction.Ignore);

            Transform ownRoot = ignoreOwnHierarchy ? constraintObject.root : null;

            for (int i = 0; i < n; i++)
            {
                Transform hitTf = _hitBuffer[i].collider.transform;
                if (hitTf.IsChildOf(t.target)) continue;                    // collider của chính Target
                if (ownRoot != null && hitTf.IsChildOf(ownRoot)) continue;  // collider của chính nhân vật
                return true;
            }

            return false;
        }

        private static float Smooth01(float v) => v * v * (3f - 2f * v);

        private void RecordOutput(Quaternion baseLocalRot)
        {
            _lastBaseLocalRot = baseLocalRot;
            _lastOutLocalRot = constraintObject.localRotation;
            _hasLastOutput = true;
        }

        #region Auto Tracking

        private static bool IsEligible(TargetData t) => t != null && t.target != null && t.weight > 0f;

        /// <summary>Weight hiệu dụng: khi Auto Tracking chạy = Weight × autoBlend, ngược lại = Weight gốc.</summary>
        private static float EffWeight(TargetData t, bool autoActive)
            => autoActive ? t.weight * t.autoBlend : t.weight;

        /// <summary>Tính 1 lần/frame các giá trị dùng chung cho mọi lần gọi IsTargetInsideLimits.</summary>
        private void PrepareLimitBasis(Quaternion baseWorldRot, Vector3 localAxis)
        {
            Vector3 up = DampedTrackMath.GetTrackAxisVector(limitUpAxis);
            Vector3 right = Vector3.Cross(up, localAxis);
            _limBasisOk = right.sqrMagnitude >= 1e-6f; // up trùng track axis → giới hạn không xác định
            if (!_limBasisOk) return;

            right.Normalize();
            _limRight = right;
            _limUp = Vector3.Cross(localAxis, right);
            _limInvBase = Quaternion.Inverse(baseWorldRot);
        }

        /// <summary>
        /// Kiểm tra (KHÔNG đổi trạng thái hysteresis của gate) xem hướng về Target có nằm trong giới hạn
        /// góc không. Kết quả tương đương GetYawPitch nhưng tính thẳng từ hướng tới Target (hướng output của
        /// Damped Track chính là hướng này) → không cần dựng quaternion cho từng Target ứng viên.
        /// <paramref name="shrink"/> thu hẹp giới hạn — dùng limitMargin cho Target ứng viên để nó phải vào
        /// sâu trong vùng cho phép mới được chọn (chống nhấp nháy ở biên). Cần gọi PrepareLimitBasis trước.
        /// </summary>
        private bool IsTargetInsideLimits(Vector3 selfWorldPos, TargetData t, Vector3 localAxis, float shrink)
        {
            if (!_limBasisOk) return true;

            Vector3 diff = t.target.position - selfWorldPos;
            if (diff.sqrMagnitude < 1e-10f) return true;

            Vector3 d = _limInvBase * diff; // không cần normalize: atan2 chỉ phụ thuộc tỷ lệ
            float f = Vector3.Dot(d, localAxis);
            float r = Vector3.Dot(d, _limRight);
            float u = Vector3.Dot(d, _limUp);

            float yaw = Mathf.Atan2(r, f) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(u, Mathf.Sqrt(f * f + r * r)) * Mathf.Rad2Deg;

            return yaw   >= -Mathf.Max(0f, maxYawLeft   - shrink) &&
                   yaw   <=  Mathf.Max(0f, maxYawRight  - shrink) &&
                   pitch <=  Mathf.Max(0f, maxPitchUp   - shrink) &&
                   pitch >= -Mathf.Max(0f, maxPitchDown - shrink);
        }

        private void ResetAutoState()
        {
            _autoActive = false;
            _autoSelected = null;
        }

        /// <summary>
        /// Chọn Target theo quy tắc: gần nhất trong số Target hợp lệ (trong Limits + không bị che), rồi
        /// cập nhật autoBlend để chuyển mượt giữa Target cũ và mới (tổng autoBlend luôn = 1 → không tụt
        /// influence giữa chừng). Trả về true nếu Auto Tracking đang chạy (bật + có từ 2 Target hợp lệ).
        /// Phải gọi SAU khi đã cập nhật Target.occluded.
        /// </summary>
        private bool UpdateAutoTracking(Vector3 selfWorldPos, Quaternion baseWorldRot, Vector3 localAxis)
        {
            if (!autoTracking) { ResetAutoState(); return false; }

            int count = targets.Count;
            int eligible = 0;
            for (int i = 0; i < count; i++)
                if (IsEligible(targets[i])) eligible++;

            if (eligible < 2) { ResetAutoState(); return false; }

            // ── Vừa chuyển sang trạng thái hoạt động: khởi tạo autoBlend ──
            // Khởi động lạnh (chưa có output frame nào): snap thẳng vào Target được chọn.
            // Bật giữa chừng: bắt đầu chia đều để KHÔNG nhảy hướng, rồi chuyển mượt sang Target được chọn.
            bool coldInit = false;
            if (!_autoActive)
            {
                _autoActive = true;
                _autoSelected = null;
                _autoSwitchTimer = autoMinSwitchInterval; // cho phép chọn ngay
                coldInit = !_hasLastOutput;
                float share = 1f / eligible;
                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t != null) t.autoBlend = (!coldInit && IsEligible(t)) ? share : 0f;
                }
            }

            _autoSwitchTimer += Time.deltaTime;

            if (useLimits) PrepareLimitBasis(baseWorldRot, localAxis);

            // ── Quét ứng viên ──
            TargetData bestValid = null, nearestAny = null;
            float bestValidSqr = float.MaxValue, nearestSqr = float.MaxValue;
            bool curEligible = false, curValid = false;
            float curSqr = 0f;

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (!IsEligible(t)) continue;

                float sqr = (t.target.position - selfWorldPos).sqrMagnitude;
                if (sqr < nearestSqr) { nearestSqr = sqr; nearestAny = t; }

                bool isCur = t == _autoSelected;

                // Target xa hơn ứng viên hợp lệ tốt nhất hiện có (và không phải Target đang chọn) thì
                // không thể thành bestValid → bỏ qua kiểm tra Limits/Obstacle.
                bool valid = (isCur || sqr < bestValidSqr)
                             && !(checkObstacles && t.occluded)
                             && (!useLimits || IsTargetInsideLimits(selfWorldPos, t, localAxis,
                                 isCur ? 0f : limitMargin));

                if (isCur) { curEligible = true; curValid = valid; curSqr = sqr; }
                if (valid && sqr < bestValidSqr) { bestValidSqr = sqr; bestValid = t; }
            }

            // ── Quyết định Target ──
            TargetData next = _autoSelected;
            if (!curEligible)
            {
                // Chưa có / Target cũ bị xóa hoặc Weight = 0 → chọn ngay (không còn hợp lệ nào thì lấy gần nhất).
                next = bestValid != null ? bestValid : nearestAny;
            }
            else if (!curValid)
            {
                // Target hiện tại ra ngoài Limits / bị che → đổi ngay nếu còn Target hợp lệ khác,
                // không thì giữ nguyên để gate xử lý (chững lại rồi nhả về pose gốc).
                if (bestValid != null) next = bestValid;
            }
            else if (bestValid != null && bestValid != _autoSelected
                     && _autoSwitchTimer >= autoMinSwitchInterval
                     && Mathf.Sqrt(bestValidSqr) < Mathf.Sqrt(curSqr) - autoSwitchMargin)
            {
                next = bestValid; // cả hai đều hợp lệ → chỉ đổi khi gần hơn đủ margin và hết cooldown
            }

            if (next != _autoSelected)
            {
                _autoSelected = next;
                _autoSwitchTimer = 0f;
            }

            TargetData sel = _autoSelected;
            if (coldInit) sel.autoBlend = 1f;

            // ── Cập nhật autoBlend: sel tăng dần về 1, các Target khác co lại theo tỷ lệ ──
            float step = autoSwitchBlendTime > 0.001f ? Time.deltaTime / autoSwitchBlendTime : 1f;

            float othersSum = 0f;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t == null || t == sel) continue;
                if (!IsEligible(t)) { t.autoBlend = 0f; continue; }
                othersSum += t.autoBlend;
            }

            float total = sel.autoBlend + othersSum;
            float selNorm = total > 1e-5f ? sel.autoBlend / total : 1f; // chuẩn hóa về tổng = 1
            float selNew = Mathf.MoveTowards(selNorm, 1f, step);
            float k = 0f;
            if (othersSum > 1e-5f) k = (1f - selNew) / othersSum;
            else selNew = 1f;

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t == null || t == sel || !IsEligible(t)) continue;
                t.autoBlend *= k;
            }
            sel.autoBlend = selNew;

            return true;
        }

        #endregion

        #region Public API

        /// <summary>Thêm một Target mới vào hệ thống tại Runtime.</summary>
        /// <param name="id">ID định danh duy nhất (VD: "enemy_01")</param>
        /// <param name="targetTransform">Transform của vật thể đích</param>
        /// <param name="initialWeight">Trọng số ban đầu (mặc định 0 để blend dần lên). Khi bật Auto Tracking,
        /// Target chỉ được xét chọn khi Weight &gt; 0.</param>
        public bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    Debug.LogWarning($"Target với ID '{id}' đã tồn tại trong hệ thống!", this);
                    return false;
                }
            }

            if (targetTransform == null)
            {
                Debug.LogError("Transform của Target truyền vào bị null!");
                return false;
            }

            targets.Add(new TargetData { id = id, target = targetTransform, weight = initialWeight });
            ConstraintScheduler.MarkDirty();
            return true;
        }

        /// <summary>Xóa một Target ra khỏi danh sách quản lý dựa theo ID.</summary>
        public bool RemoveTarget(string id)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    CancelWeightRoutine(id);
                    targets.RemoveAt(i);
                    ConstraintScheduler.MarkDirty();
                    return true;
                }
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{id}' để xóa!");
            return false;
        }

        /// <summary>Đặt trực tiếp Weight của một Target ngay lập tức theo ID.</summary>
        public bool SetTargetWeight(string id, float weight)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    CancelWeightRoutine(id);
                    targets[i].weight = Mathf.Clamp01(weight);
                    return true;
                }
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{id}' để set weight!");
            return false;
        }

        /// <summary>
        /// Đổi Weight của một Target MƯỢT MÀ (lerp) từ giá trị hiện tại tới <paramref name="weight"/> trong
        /// <paramref name="duration"/> giây. Chỉ ảnh hưởng Target có id này (khác SwitchTarget: không đụng
        /// các Target khác). Gọi lại cùng id sẽ thay thế lần fade đang chạy. duration &lt;= 0 (hoặc đang ở
        /// Edit Mode / component bị tắt) → set ngay lập tức.
        /// Lưu ý: SwitchTarget đang chạy sẽ bị huỷ khi gọi hàm này.
        /// </summary>
        /// <returns>false nếu không tìm thấy Target có id này.</returns>
        public bool SetTargetWeight(string id, float weight, float duration)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                TargetData data = targets[i];
                if (data.id != id) continue;

                float goal = Mathf.Clamp01(weight);
                CancelWeightRoutine(id);

                if (duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
                {
                    data.weight = goal;
                    return true;
                }

                // Tránh SwitchTarget đang chạy ghi đè weight song song
                if (switchCoroutine != null)
                {
                    StopCoroutine(switchCoroutine);
                    switchCoroutine = null;
                }

                _weightRoutines[id] = StartCoroutine(SetWeightRoutine(data, id, goal, duration));
                return true;
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{id}' để set weight!");
            return false;
        }

        private IEnumerator SetWeightRoutine(TargetData data, string id, float goal, float duration)
        {
            float startWeight = data.weight;
            float invDuration = 1f / duration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                data.weight = Mathf.Lerp(startWeight, goal, Mathf.Min(elapsed * invDuration, 1f));
                yield return null;
            }

            data.weight = goal; // giá trị chính xác cuối cùng
            _weightRoutines.Remove(id);
        }

        private void CancelWeightRoutine(string id)
        {
            if (_weightRoutines.TryGetValue(id, out Coroutine routine))
            {
                if (routine != null) StopCoroutine(routine);
                _weightRoutines.Remove(id);
            }
        }

        private void CancelAllWeightRoutines()
        {
            foreach (var kv in _weightRoutines)
                if (kv.Value != null) StopCoroutine(kv.Value);
            _weightRoutines.Clear();
        }

        /// <summary>Thay đổi nhanh giá trị Influence tổng thể của Constraint (huỷ fade Influence đang chạy nếu có).</summary>
        public void SetInfluence(float value)
        {
            CancelInfluenceRoutine();
            influence = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Đổi Influence tổng thể MƯỢT MÀ (lerp) từ giá trị hiện tại tới <paramref name="value"/> trong
        /// <paramref name="duration"/> giây. Gọi lại sẽ thay thế lần fade đang chạy.
        /// duration &lt;= 0 (hoặc đang ở Edit Mode / component bị tắt) → set ngay lập tức.
        /// </summary>
        public void SetInfluence(float value, float duration)
        {
            CancelInfluenceRoutine();
            float goal = Mathf.Clamp01(value);

            if (duration <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                influence = goal;
                return;
            }

            _influenceRoutine = StartCoroutine(SetInfluenceRoutine(goal, duration));
        }

        private IEnumerator SetInfluenceRoutine(float goal, float duration)
        {
            float startValue = influence;
            float invDuration = 1f / duration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                influence = Mathf.Lerp(startValue, goal, Mathf.Min(elapsed * invDuration, 1f));
                yield return null;
            }

            influence = goal; // giá trị chính xác cuối cùng
            _influenceRoutine = null;
        }

        private void CancelInfluenceRoutine()
        {
            if (_influenceRoutine == null) return;
            StopCoroutine(_influenceRoutine);
            _influenceRoutine = null;
        }

        /// <summary>Đổi trục Track Axis tại runtime (VD: đổi hướng nòng khi swap model súng).</summary>
        public void SetTrackAxis(TrackAxis axis) => trackAxis = axis;

        /// <summary>Bật/tắt Auto Tracking tại runtime (chỉ chạy khi có từ 2 Target hợp lệ).</summary>
        public void SetAutoTracking(bool enabled) => autoTracking = enabled;

        /// <summary>
        /// Chuyển đổi Target mượt mà bằng Coroutine. Target được chỉ định tăng dần Weight lên 1,
        /// các Target còn lại giảm dần Weight về 0.
        /// </summary>
        /// <param name="targetId">ID của Target muốn kích hoạt</param>
        /// <param name="duration">Thời gian chuyển đổi (giây)</param>
        public void SwitchTarget(string targetId, float duration)
        {
            CancelAllWeightRoutines(); // tránh fade weight riêng lẻ ghi đè song song
            if (switchCoroutine != null) StopCoroutine(switchCoroutine);
            switchCoroutine = StartCoroutine(SwitchTargetRoutine(targetId, duration));
        }

        private IEnumerator SwitchTargetRoutine(string targetId, float duration)
        {
            int count = targets.Count;

            // duration <= 0: instant switch — set weight trực tiếp, không cần coroutine overhead
            if (duration <= 0f)
            {
                for (int i = 0; i < count; i++)
                    targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
                yield break;
            }

            float elapsed = 0f;

            if (_cachedStartWeights.Length < count)
                _cachedStartWeights = new float[count];

            for (int i = 0; i < count; i++)
                _cachedStartWeights[i] = targets[i].weight;

            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pct = Mathf.Min(elapsed * invDuration, 1f);

                // Dùng targets.Count LIVE mỗi iteration — tránh lỗi nếu Add/RemoveTarget được gọi
                // trong lúc coroutine đang chạy (xem giải thích chi tiết trong TransformConstraint.cs).
                int liveCount = targets.Count;
                int iterCount = Mathf.Min(count, liveCount);
                for (int i = 0; i < iterCount; i++)
                {
                    float goal = (targets[i].id == targetId) ? 1f : 0f;
                    targets[i].weight = Mathf.Lerp(_cachedStartWeights[i], goal, pct);
                }

                yield return null;
            }

            // Set giá trị chính xác cuối cùng — tránh floating point error tích lũy từ Lerp
            int finalCount = targets.Count;
            for (int i = 0; i < finalCount; i++)
                targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
        }

        #endregion

        /// <summary>Trả constraintObject về pose gốc ngay (không có Target nào ảnh hưởng).</summary>
        private void ReleaseToBaseline()
        {
            Quaternion baseLocalRot = GetBaselineLocalRot();
            constraintObject.localRotation = baseLocalRot;
            RecordOutput(baseLocalRot);

            // Đang ở pose gốc → lần có Target sau sẽ làm mượt bắt đầu từ pose gốc
            _smoothDelta = Quaternion.identity;
            _smoothValid = true;
        }

        // ══════════════════════════════ SCHEDULER ══════════════════════════════
        // Không còn LateUpdate() riêng: ConstraintScheduler gọi Solve() theo thứ tự phụ thuộc.

        public override int DefaultTypeOrder => TypeOrderDampedTrack;

        public override void CollectDependencies(List<Transform> inputs, List<Transform> outputs)
        {
            if (constraintObject == null) return;
            outputs.Add(constraintObject);
            inputs.Add(constraintObject.parent); // baseline world rotation phụ thuộc cha
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) inputs.Add(targets[i].target);
        }

        /// <summary>Tính và áp constraint cho frame hiện tại. ConstraintScheduler gọi mỗi LateUpdate.</summary>
        public override void Solve()
        {
            if (!_restPoseReady) return;
            int count = targets.Count;
            if (constraintObject == null || count == 0) return;

            // Tổng Weight gốc — chỉ để biết có Target nào đang bật không.
            float rawWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null) rawWeight += t.weight;
            }

            _driverSkippedThisFrame = DetectDriverSkipped();

            if (rawWeight <= 0f)
            {
                ResetAutoState();
                ReleaseToBaseline();
                return;
            }

            Quaternion baseLocalRotSnapshot = GetBaselineLocalRot();
            Quaternion baseWorldRot = GetBaselineWorldRot();

            // Position không bị constraint này ghi đè → đọc trực tiếp transform hiện tại luôn an toàn,
            // không cần cơ chế baseline/anti-drift như Rotation.
            Vector3 selfWorldPos = constraintObject.position;
            Vector3 localAxis = DampedTrackMath.GetTrackAxisVector(trackAxis);

            // ── Obstacle Check: cập nhật occluded + độ "nhìn thấy" từng Target (fade mượt) ──
            if (checkObstacles)
            {
                float visStep = Time.deltaTime / Mathf.Max(0.01f, occlusionBlendTime);
                float now = Time.time;
                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null || t.weight <= 0f) continue; // Target đang tắt: không tốn raycast

                    // Auto Tracking: Target không được chọn và không còn tham gia blend → raycast thưa.
                    bool sparse = _autoActive && t != _autoSelected && t.autoBlend <= 0.001f;
                    if (!sparse || now >= t.nextProbeTime)
                    {
                        t.occluded = IsOccluded(selfWorldPos, t);
                        t.nextProbeTime = now + AutoProbeInterval + (i & 3) * 0.025f;
                    }

                    t.visibility = Mathf.MoveTowards(t.visibility, t.occluded ? 0f : 1f, visStep);
                }
            }

            // ── Auto Tracking: chọn Target gần nhất hợp lệ (cần t.occluded đã cập nhật ở trên) ──
            bool autoActive = UpdateAutoTracking(selfWorldPos, baseWorldRot, localAxis);

            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null) totalWeight += EffWeight(t, autoActive);
            }

            if (totalWeight <= 0f)
            {
                ReleaseToBaseline();
                return;
            }

            // ── normalizeWeights: tách blend ratio ra khỏi global influence (xem TransformConstraint.cs) ──
            float blendInfluence = normalizeWeights
                ? Mathf.Clamp01(influence) * totalWeight
                : Mathf.Clamp01(totalWeight * influence);

            float invTotalWeight = 1f / totalWeight;

            // effTotal = Σ(weight hiệu dụng × visibility đã smooth) — chỉ dùng để chia weight khi blend HƯỚNG nhìn
            // (Target đang bị che không kéo hướng). Việc nhả về pose gốc do EvaluateGate xử lý.
            // occludedAll: tính trên mọi Target có Weight > 0 (kể cả Target Auto Tracking chưa chọn).
            float effTotal = 0f;
            bool occludedAll = false;
            if (checkObstacles)
            {
                int activeCount = 0, occludedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null) continue;

                    effTotal += EffWeight(t, autoActive) * Smooth01(t.visibility);

                    if (t.weight > 0f)
                    {
                        activeCount++;
                        if (t.occluded) occludedCount++;
                    }
                }

                occludedAll = activeCount > 0 && occludedCount == activeCount;
            }

            // Nếu tất cả bị che (effTotal ≈ 0) thì dùng weight gốc để hướng blend vẫn xác định được.
            bool useEffWeights = checkObstacles && effTotal > 1e-4f;
            float invEffTotal = useEffWeights ? 1f / effTotal : 0f;

            // Weighted NLerp: mỗi target cho ra 1 quaternion hướng về nó (swing nhỏ nhất tính từ cùng
            // baseWorldRot), cộng dồn theo weight rồi normalize. Order-independent, ổn định hơn Slerp
            // tuần tự khi weight thay đổi liên tục (giống UpdateRotation World mode trong TransformConstraint).
            float accX = 0f, accY = 0f, accZ = 0f, accW = 0f;
            Quaternion refRot = Quaternion.identity;
            bool firstValid = true;

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                float tw = EffWeight(t, autoActive);
                if (t.target == null || tw <= 0f) continue;

                Quaternion targetRot = DampedTrackMath.ComputeDampedTrackRotation(
                    selfWorldPos, t.target.position, baseWorldRot, localAxis);

                float w = useEffWeights
                    ? tw * Smooth01(t.visibility) * invEffTotal
                    : tw * invTotalWeight;

                if (firstValid)
                {
                    refRot = targetRot;
                    firstValid = false;
                }
                else if (Quaternion.Dot(refRot, targetRot) < 0f)
                {
                    // Hemisphere correction: flip quaternion về cùng phía với ref → tránh NLerp đi đường vòng
                    targetRot = new Quaternion(-targetRot.x, -targetRot.y, -targetRot.z, -targetRot.w);
                }

                accX += targetRot.x * w;
                accY += targetRot.y * w;
                accZ += targetRot.z * w;
                accW += targetRot.w * w;
            }

            if (!firstValid)
            {
                float mag = Mathf.Sqrt(accX * accX + accY * accY + accZ * accZ + accW * accW);
                Quaternion blendedRot = mag > 1e-6f
                    ? new Quaternion(accX / mag, accY / mag, accZ / mag, accW / mag)
                    : baseWorldRot;

                float gateMul = 1f;
                Quaternion targetRot = blendedRot;

                if (useLimits || checkObstacles)
                    targetRot = EvaluateGate(baseWorldRot, blendedRot, localAxis, occludedAll, out gateMul);
                else
                    _aimValid = false; // tắt cả 2 tính năng → lần bật lại sẽ khởi tạo gate từ đầu

                // Offset góc xoay (local của Constraint Object) — áp sau gate/limit nên offset không làm
                // lệch việc kiểm tra giới hạn, và được nhân theo influence/gate bên dưới (về 0 khi nhả về gốc).
                if (rotationOffset != Vector3.zero)
                    targetRot *= Quaternion.Euler(rotationOffset);

                Quaternion desiredRot = Quaternion.Slerp(baseWorldRot, targetRot, blendInfluence * gateMul);

                if (followSpeed >= MaxFollowSpeed)
                {
                    // Bám tức thì (mặc định) — không làm mượt
                    _smoothValid = false;
                    constraintObject.rotation = desiredRot;
                }
                else
                {
                    // Làm mượt kiểu quán tính trên rotation TƯƠNG ĐỐI với pose gốc → vẫn đi theo animation thân.
                    // Exponential smoothing: độc lập framerate.
                    Quaternion desiredDelta = Quaternion.Inverse(baseWorldRot) * desiredRot;
                    if (!_smoothValid)
                    {
                        _smoothDelta = desiredDelta;
                        _smoothValid = true;
                    }
                    else
                    {
                        _smoothDelta = Quaternion.Slerp(_smoothDelta, desiredDelta,
                            1f - Mathf.Exp(-followSpeed * Time.deltaTime));
                    }

                    constraintObject.rotation = baseWorldRot * _smoothDelta;
                }
            }

            RecordOutput(baseLocalRotSnapshot);
        }

        #region Gizmos
#if UNITY_EDITOR

        void OnDrawGizmos()
        {
            if (!showGizmos || constraintObject == null) return;

            Vector3 pos = constraintObject.position;
            Vector3 axisDir = constraintObject.rotation * DampedTrackMath.GetTrackAxisVector(trackAxis);

            if (gizmoSize > 0f)
            {
                Gizmos.color = gizmoAxisColor;
                DrawGizmoArrow(pos, axisDir, gizmoSize);
            }

            Gizmos.color = gizmoLineColor;
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null && t.weight > 0f)
                {
                    if (checkObstacles && t.occluded) Gizmos.color = gizmoBlockedColor;
                    else if (IsAutoTrackingActive && t == _autoSelected) Gizmos.color = gizmoSelectedColor;
                    else Gizmos.color = gizmoLineColor;
                    Gizmos.DrawLine(pos, t.target.position);
                    Gizmos.DrawWireSphere(t.target.position, gizmoSize * 0.25f);
                }
            }
        }

        /// <summary>
        /// Vẽ một mũi tên đơn (thân + 4 nét đầu mũi tên) từ origin theo hướng direction với độ dài length.
        /// </summary>
        private static void DrawGizmoArrow(Vector3 origin, Vector3 direction, float length)
        {
            if (direction == Vector3.zero) return;

            Vector3 dirNorm = direction.normalized;
            Vector3 end = origin + dirNorm * length;

            Gizmos.DrawLine(origin, end);

            Vector3 upRef = (Mathf.Abs(Vector3.Dot(dirNorm, Vector3.up)) > 0.99f) ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(dirNorm, upRef).normalized;
            Vector3 up = Vector3.Cross(right, dirNorm).normalized;

            float headLength = length * 0.2f;
            Vector3 back = -dirNorm * headLength;

            Vector3 p1 = end + back + right * (headLength * 0.5f);
            Vector3 p2 = end + back - right * (headLength * 0.5f);
            Vector3 p3 = end + back + up * (headLength * 0.5f);
            Vector3 p4 = end + back - up * (headLength * 0.5f);

            Gizmos.DrawLine(end, p1);
            Gizmos.DrawLine(end, p2);
            Gizmos.DrawLine(end, p3);
            Gizmos.DrawLine(end, p4);
        }

#endif
        #endregion
    }
}