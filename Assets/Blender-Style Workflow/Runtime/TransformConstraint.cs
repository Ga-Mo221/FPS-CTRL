using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [AddComponentMenu("Copy Transform")]
    public class TransformConstraint : MonoBehaviour
    {
        public enum SpaceMode
        {
            World,
            Local
        }

        public enum ConstraintMode
        {
            Delta,
            Absolute
        }

        [System.Serializable]
        public class TargetData
        {
            [Tooltip(
                "ID định danh duy nhất cho Target này (Ví dụ: 'm4a1', 'pistol', 'shield'). Được dùng để gọi từ code khi cần chuyển đổi vũ khí/vật thể bám.")]
            public string id;

            [Tooltip(
                "Transform của vật thể đích mà đối tượng chịu ràng buộc (Constraint Object) sẽ bắt chước Tọa độ/Góc xoay theo.")]
            public Transform target;

            [Range(0f, 1f)]
            [Tooltip(
                "Trọng số ảnh hưởng riêng của Target này.\n• 1 = Chịu ảnh hưởng tối đa.\n• 0 = Hoàn toàn bỏ qua.\n• Nếu dùng nhiều Target (Multi-Target), hệ thống tự động hòa trộn vị trí/góc xoay theo tỷ lệ Weight giữa chúng.")]
            public float weight = 1f;

            // Cache mốc ban đầu phục vụ chế độ Delta (Quản lý riêng cho từng Target)
            [HideInInspector] public Vector3 initialLocalPos;
            [HideInInspector] public Quaternion initialLocalRot;

            // Rotation THẾ GIỚI của target tại thời điểm capture — dùng làm hệ trục cố định để
            // phân tích Delta Position (xem ComputeLocalDeltaPosition). Tách riêng khỏi
            // initialLocalRot vì initialLocalRot là rotation tương đối với parent của target,
            // trong khi hàm cần world rotation để chiếu targetDelta (vốn tính trong world space).
            [HideInInspector] public Quaternion initialWorldRot;
        }

        [Tooltip(
            "Danh sách các điểm đích cần bắt chước theo. Bạn có thể thêm nhiều mục tiêu tại đây và thay đổi Weight để blend mượt mà giữa các tư thế cầm nắm.")]
        public List<TargetData> targets = new List<TargetData>();

        [Tooltip(
            "Đối tượng trực tiếp chịu sự ràng buộc (Ví dụ: Xương bàn tay, xương cổ, hoặc báng súng). Vật thể này sẽ bị ép di chuyển và xoay theo các Target phía trên.")]
        public Transform constraintObject;

        [Tooltip(
            "✔ TRUE (mặc định, giữ hành vi cũ): constraintObject có Animator hoặc hệ thống khác ghi đè Transform mỗi frame " +
            "→ baseline khi Influence = 0 được đọc TRỰC TIẾP từ transform hiện tại mỗi frame (vì nó đã được driver reset sẵn).\n" +
            "❌ FALSE: Không có gì điều khiển constraintObject mỗi frame → nếu vẫn đọc transform hiện tại làm baseline thì baseline sẽ bị " +
            "\"ô nhiễm\" bởi chính output constraint frame trước, kéo Influence về 0 sẽ KHÔNG về đúng vị trí gốc (bug drift).\n" +
            "Khi tắt, hệ thống sẽ chụp lại Rest Pose (local pos/rot) MỘT LẦN lúc OnEnable — hoặc bất cứ lúc nào bạn bấm " +
            "\"Capture Rest Pose Now\" trong Inspector — và luôn dùng pose đó làm baseline cố định, không bao giờ tự trôi.")]
        public bool hasExternalPoseDriver = true;

        // Rest Pose được chụp MỘT LẦN (local space, tính theo parent của constraintObject) và dùng làm baseline
        // cố định khi hasExternalPoseDriver = false, thay vì đọc lại transform hiện tại mỗi frame (nguồn gây drift).
        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private bool _restPoseCaptured = false;

        [Tooltip("✔ TRUE: Bật tính năng bắt chước Vị trí (Position) từ Target.\n❌ FALSE: Bỏ qua không xử lý vị trí.")]
        public bool enablePositionConstraint = true;

        [Tooltip(
            "Không gian tính toán tọa độ:\n• World: Bám theo vị trí tuyệt đối trong thế giới game.\n• Local: Tính toán dựa trên khoảng cách tương đối với cha (Phù hợp làm chuỗi xương nhân vật, tránh lỗi giật khớp khi nhân vật di chuyển).")]
        public SpaceMode positionSpace = SpaceMode.World;

        [Tooltip(
            "Chế độ ràng buộc vị trí (Chỉ có tác dụng khi chọn Local Space):\n• Absolute: Ép thẳng tọa độ Local của vật thể trùng với Target.\n• Delta: Chỉ bắt chước lượng thay đổi (di chuyển bao nhiêu thì dịch chuyển bấy nhiêu), giữ nguyên khoảng cách gốc ban đầu giữa 2 vật thể (Khuyên dùng cho súng/vũ khí).")]
        public ConstraintMode positionMode = ConstraintMode.Delta;

        [Tooltip(
            "✔ TRUE (khuyên dùng): Tự động chuẩn hóa Weight của các Target trước khi blend.\n" +
            "Ví dụ: A=0.3, B=0.3 → blend 50/50 và influence luôn = positionInfluence/rotationInfluence.\n" +
            "❌ FALSE (hành vi cũ): totalWeight ảnh hưởng trực tiếp đến influence cuối cùng.\n" +
            "A=0.3, B=0.3 → totalWeight=0.6 → influence thực = 0.6 × positionInfluence.\n" +
            "Chế độ này cho phép influence tự nhiên giảm khi tổng weight < 1 (hữu ích cho blend-in mượt),\n" +
            "nhưng phải đảm bảo tổng weight = 1 nếu muốn đạt influence tối đa.")]
        public bool normalizeWeights = true;

        [Range(0f, 1f)]
        [Tooltip(
            "Mức độ ảnh hưởng tổng thể của liên kết vị trí:\n• 1 = Khớp khít hoàn toàn theo Target.\n• 0 = Trả về theo Animation gốc của nhân vật.\n• 0.5 = Nằm chính giữa góc của Animation và vị trí Target.")]
        public float positionInfluence = 1f;

        [Tooltip("✔ TRUE: Copy tọa độ trục X.\n❌ FALSE: Giữ nguyên trục X theo hoạt cảnh gốc.")]
        public bool posCopyX = true;

        [Tooltip("✔ TRUE: Copy tọa độ trục Y.\n❌ FALSE: Giữ nguyên trục Y theo hoạt cảnh gốc.")]
        public bool posCopyY = true;

        [Tooltip("✔ TRUE: Copy tọa độ trục Z.\n❌ FALSE: Giữ nguyên trục Z theo hoạt cảnh gốc.")]
        public bool posCopyZ = true;

        [Tooltip(
            "✔ TRUE: Bật tính năng bắt chước Góc xoay (Rotation) từ Target.\n❌ FALSE: Bỏ qua không xử lý góc xoay.")]
        public bool enableRotationConstraint = true;

        [Tooltip(
            "Không gian tính toán góc xoay:\n• World: Xoay hướng tuyệt đối theo thế giới game.\n• Local: Chỉ tính toán góc xoay tương đối so với vật thể cha.")]
        public SpaceMode rotationSpace = SpaceMode.World;

        [Tooltip(
            "Chế độ ràng buộc góc xoay (Chỉ có tác dụng khi chọn Local Space):\n• Absolute: Ép thẳng góc xoay Local trùng với góc Target.\n• Delta: Chỉ xoay thêm một lượng bằng đúng lượng Target vừa xoay, bảo toàn hướng xoay ban đầu của súng/tay cầm.")]
        public ConstraintMode rotationMode = ConstraintMode.Delta;

        [Range(0f, 1f)]
        [Tooltip(
            "Mức độ ảnh hưởng tổng thể của liên kết góc xoay:\n• 1 = Xoay trùng khít hoàn toàn theo Target.\n• 0 = Trả về theo hướng của Animation gốc.\n• 0.5 = Nằm chính giữa góc của Animation và hướng của Target.")]
        public float rotationInfluence = 1f;

        [Tooltip("✔ TRUE: Copy góc xoay quanh trục X.\n❌ FALSE: Giữ nguyên trục X.")]
        public bool rotCopyX = true;

        [Tooltip("✔ TRUE: Copy góc xoay quanh trục Y.\n❌ FALSE: Giữ nguyên trục Y.")]
        public bool rotCopyY = true;

        [Tooltip("✔ TRUE: Copy góc xoay quanh trục Z.\n❌ FALSE: Giữ nguyên trục Z.")]
        public bool rotCopyZ = true;

        [Tooltip(
            "✔ TRUE: Vẽ gizmo mũi tên 3 trục (X/Y/Z) trong Scene View cho Constraint Object và các Target đang có Weight > 0.\n❌ FALSE: Ẩn toàn bộ gizmo.")]
        public bool showGizmos = false;

        [Tooltip("Kích thước (độ dài) của mũi tên gizmo, tính theo đơn vị world-space.")]
        public float gizmoSize = 0.3f;

        [Tooltip("Màu của mũi tên trục X.")] public Color gizmoColorX = Color.red;

        [Tooltip("Màu của mũi tên trục Y.")] public Color gizmoColorY = Color.green;

        [Tooltip("Màu của mũi tên trục Z.")] public Color gizmoColorZ = Color.blue;

        private Coroutine switchCoroutine;

        // Cache để tránh alloc mỗi lần gọi SwitchTarget
        private float[] _cachedStartWeights = new float[0];

        // ── Frame Snapshot ────────────────────────────────────────────────────────────
        // Snapshot được chụp MỘT LẦN ở đầu LateUpdate, trước khi bất kỳ hàm Compute nào chạy.
        // Mục đích:
        //   • Loại bỏ feedback loop Position → Rotation: UpdatePosition không đọc lại
        //     rotation hiện tại của constraintObject (có thể đã bị Animator/IK thay đổi),
        //     UpdateRotation không đọc lại position đã bị UpdatePosition ghi đè.
        //   • basis (right/up/forward) được tính từ baseWorldRot một lần, dùng chung cho
        //     tất cả target trong vòng lặp → tránh gọi native property N lần/frame.
        private Vector3    _snapBaseWorldPos;
        private Quaternion _snapBaseWorldRot;
        private Vector3    _snapBaseLocalPos;
        private Quaternion _snapBaseLocalRot;
        // Các trục cục bộ của constraintObject tính từ rotation snapshot
        private Vector3    _snapRight;
        private Vector3    _snapUp;
        private Vector3    _snapForward;
        
        private bool _restPoseReady = false;

        void OnEnable()
        {
            CaptureInitialStates();
            
            _restPoseReady = false;
            
            if (!hasExternalPoseDriver)
                StartCoroutine(CaptureRestPoseNextFrame());
            else
                _restPoseReady = true;
        }

        private IEnumerator CaptureRestPoseNextFrame()
        {
            yield return null;   // chờ Animator/IK evaluate xong frame đầu
            CaptureRestPose();
            _restPoseReady = true;
        }

        /// <summary>
        /// Chụp lại Rest Pose (local position/rotation hiện tại của constraintObject) để dùng làm baseline
        /// cố định khi hasExternalPoseDriver = false. Gọi hàm này thủ công (nút "Capture Rest Pose Now" trong
        /// Inspector, hoặc từ code) bất cứ khi nào bạn muốn "khóa" lại pose gốc mới — tương tự nút Activate
        /// của Unity built-in Position/Rotation Constraint.
        /// </summary>
        public void CaptureRestPose()
        {
            if (constraintObject == null) return;

            _restLocalPos = constraintObject.localPosition;
            _restLocalRot = constraintObject.localRotation;
            _restPoseCaptured = true;
        }

        /// <summary>
        /// Baseline vị trí World tại Influence = 0.
        /// • hasExternalPoseDriver = true  → đọc trực tiếp transform hiện tại (Animator đã reset sẵn mỗi frame).
        /// • hasExternalPoseDriver = false → suy ra từ Rest Pose đã chụp (local), map qua parent hiện tại —
        ///   vẫn đúng ngay cả khi parent (ví dụ root nhân vật) di chuyển, mà KHÔNG bị trôi theo output constraint.
        /// </summary>
        private Vector3 GetBaselineWorldPos()
        {
            if (hasExternalPoseDriver) return constraintObject.position;
            if (!_restPoseCaptured) CaptureRestPose();

            Transform parent = constraintObject.parent;
            return parent != null ? parent.TransformPoint(_restLocalPos) : _restLocalPos;
        }

        private Vector3 GetBaselineLocalPos()
        {
            if (hasExternalPoseDriver) return constraintObject.localPosition;
            if (!_restPoseCaptured) CaptureRestPose();
            return _restLocalPos;
        }

        private Quaternion GetBaselineWorldRot()
        {
            if (hasExternalPoseDriver) return constraintObject.rotation;
            if (!_restPoseCaptured) CaptureRestPose();

            Transform parent = constraintObject.parent;
            return parent != null ? parent.rotation * _restLocalRot : _restLocalRot;
        }

        private Quaternion GetBaselineLocalRot()
        {
            if (hasExternalPoseDriver) return constraintObject.localRotation;
            if (!_restPoseCaptured) CaptureRestPose();
            return _restLocalRot;
        }

        /// <summary>
        /// Ghi lại tọa độ và góc xoay mặc định tại thời điểm kích hoạt.
        /// Rất quan trọng cho việc tính toán độ lệch ở chế độ Delta.
        /// </summary>
        public void CaptureInitialStates()
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null)
                {
                    t.initialLocalPos = t.target.localPosition;
                    t.initialLocalRot = t.target.localRotation;
                    t.initialWorldRot = t.target.rotation;
                }
            }
        }

        /// <summary>
        /// Recapture delta reference (initialLocalPos/Rot) của một Target cụ thể theo ID.
        /// Gọi hàm này sau khi:
        ///   • Target bị reparent sang parent mới — initialLocalPos cũ thuộc coordinate system
        ///     của parent cũ, dùng tiếp sẽ tính world position sai trong ComputeLocalDeltaPosition.
        ///   • Bạn muốn "reset" điểm xuất phát delta về pose hiện tại mà không switch target.
        /// </summary>
        /// <param name="targetId">ID của Target cần recapture</param>
        /// <returns>true nếu tìm thấy và recapture thành công</returns>
        public bool RecaptureTargetInitialState(string targetId)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.id == targetId)
                {
                    if (t.target == null)
                    {
                        Debug.LogWarning($"Target '{targetId}': Transform bị null, không thể recapture!", this);
                        return false;
                    }
                    t.initialLocalPos = t.target.localPosition;
                    t.initialLocalRot = t.target.localRotation;
                    t.initialWorldRot = t.target.rotation;
                    return true;
                }
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{targetId}' để recapture!", this);
            return false;
        }

        /// <summary>
        /// Thêm một Target mới vào hệ thống tại Runtime (Khi đang chơi game)
        /// </summary>
        /// <param name="id">ID định danh duy nhất (ví dụ: "m4a1")</param>
        /// <param name="targetTransform">Transform của vật thể đích</param>
        /// <param name="initialWeight">Trọng số ban đầu khi vừa thêm vào (mặc định là 0 để có thể blend từ từ lên)</param>
        public bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)
        {
            // Dùng vòng lặp thủ công thay vì Exists(lambda) để tránh closure allocation
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

            TargetData newTarget = new TargetData
            {
                id = id,
                target = targetTransform,
                weight = initialWeight,
                initialLocalPos = targetTransform.localPosition,
                initialLocalRot = targetTransform.localRotation,
                initialWorldRot = targetTransform.rotation
            };

            targets.Add(newTarget);
            return true;
        }

        /// <summary>
        /// Xóa một Target ra khỏi danh sách quản lý dựa theo ID
        /// </summary>
        public bool RemoveTarget(string id)
        {
            // Dùng vòng lặp thủ công thay vì Find(lambda) để tránh closure allocation
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    targets.RemoveAt(i);
                    return true;
                }
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{id}' để xóa!");
            return false;
        }

        /// <summary>
        /// Đặt trực tiếp trọng số (Weight) của một Target ngay lập tức theo ID.
        /// </summary>
        /// <param name="id">ID của Target cần chỉnh</param>
        /// <param name="weight">Giá trị weight mới (0 đến 1)</param>
        /// <returns>Trả về true nếu tìm thấy và set thành công, ngược lại trả về false</returns>
        public bool SetTargetWeight(string id, float weight)
        {
            // Dùng vòng lặp thủ công thay vì Find(lambda) để tránh closure allocation
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    targets[i].weight = Mathf.Clamp01(weight);
                    return true;
                }
            }

            Debug.LogWarning($"Không tìm thấy Target nào có ID '{id}' để set weight!");
            return false;
        }

        /// <summary>
        /// Thay đổi nhanh giá trị Position Influence của tổng thể Constraint
        /// </summary>
        public void SetPositionInfluence(float influence)
        {
            positionInfluence = Mathf.Clamp01(influence);
        }

        /// <summary>
        /// Thay đổi nhanh giá trị Rotation Influence của tổng thể Constraint
        /// </summary>
        public void SetRotationInfluence(float influence)
        {
            rotationInfluence = Mathf.Clamp01(influence);
        }

        /// <summary>
        /// Hàm chuyển đổi Target mượt mà bằng Coroutine.
        /// Target được chỉ định sẽ tăng dần Weight lên 1, các Target còn lại sẽ giảm dần Weight về 0.
        /// Delta reference (initialLocalPos/Rot) của target đích được recapture ngay khi switch —
        /// đảm bảo delta bắt đầu từ pose hiện tại thay vì pose lúc OnEnable/AddTarget (tránh giật khi
        /// equip weapon mới sau khi target đã di chuyển).
        /// </summary>
        /// <param name="targetId">ID của Target muốn kích hoạt</param>
        /// <param name="duration">Thời gian chuyển đổi (tính bằng giây)</param>
        public void SwitchTarget(string targetId, float duration)
        {
            if (switchCoroutine != null) StopCoroutine(switchCoroutine);
            switchCoroutine = StartCoroutine(SwitchTargetRoutine(targetId, duration));
        }

        private IEnumerator SwitchTargetRoutine(string targetId, float duration)
        {
            int count = targets.Count;

            // Recapture delta reference của target đích TRƯỚC khi blend bắt đầu.
            // Nếu không làm điều này, delta mode sẽ tính offset từ pose lúc OnEnable/AddTarget —
            // nếu target đã di chuyển từ đó đến giờ, constraintObject sẽ bị giật ngay khi blend vào.
            // Recapture tại đây = "điểm xuất phát của delta là pose hiện tại lúc equip".
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.id == targetId && t.target != null)
                {
                    t.initialLocalPos = t.target.localPosition;
                    t.initialLocalRot = t.target.localRotation;
                    t.initialWorldRot = t.target.rotation;
                    break;
                }
            }

            // duration <= 0: instant switch — set weight trực tiếp, không cần coroutine overhead
            if (duration <= 0f)
            {
                for (int i = 0; i < count; i++)
                    targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
                yield break;
            }

            float elapsed = 0f;

            // Tái sử dụng array cache, chỉ resize khi thực sự cần thiết → tránh alloc mỗi lần switch
            if (_cachedStartWeights.Length < count)
                _cachedStartWeights = new float[count];

            for (int i = 0; i < count; i++)
                _cachedStartWeights[i] = targets[i].weight;

            float invDuration = 1f / duration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pct = Mathf.Min(elapsed * invDuration, 1f);

                // FIX: dùng targets.Count LIVE mỗi iteration thay vì "count" snapshot lúc bắt đầu
                // coroutine. Nếu RemoveTarget()/AddTarget() được gọi trong lúc coroutine đang chạy
                // (ví dụ người chơi đổi vũ khí rồi lập tức unequip một target khác), "count" cũ có
                // thể lớn hơn targets.Count hiện tại → targets[i] ném ArgumentOutOfRangeException.
                // _cachedStartWeights được alloc theo count cũ nên vẫn an toàn để đọc tới index cũ,
                // nhưng ta chỉ ghi weight cho các index còn tồn tại trong danh sách.
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
            // Dùng targets.Count trực tiếp phòng trường hợp danh sách thay đổi trong lúc chạy
            int finalCount = targets.Count;
            for (int i = 0; i < finalCount; i++)
                targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
        }

        void LateUpdate()
        {
            if (!_restPoseReady) return;
            int count = targets.Count;
            if (constraintObject == null || count == 0) return;

            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null) totalWeight += t.weight;
            }

            if (totalWeight <= 0f)
            {
                if (enablePositionConstraint)
                    constraintObject.localPosition = GetBaselineLocalPos();
                if (enableRotationConstraint)
                    constraintObject.localRotation = GetBaselineLocalRot();
                return;
            }

            // ── normalizeWeights: tách blend ratio ra khỏi global influence ────────────
            // normalizeWeights = true (khuyên dùng):
            //   invTotalWeight = 1/totalWeight → mỗi target đóng góp weight/totalWeight.
            //   blendInfluence = positionInfluence/rotationInfluence (không bị kéo xuống bởi totalWeight).
            //   Ví dụ A=0.3, B=0.3 → blend 50/50 tại đúng 100% influence.
            //
            // normalizeWeights = false (hành vi cũ):
            //   invTotalWeight = 1/totalWeight vẫn normalize blend ratio.
            //   blendInfluence = Clamp01(totalWeight × influence) → influence co lại khi totalWeight < 1.
            //   Dùng khi muốn coroutine blend-in tự nhiên giảm influence (totalWeight tăng từ 0→1).
            float blendPosInfluence = normalizeWeights
                ? Mathf.Clamp01(positionInfluence) * totalWeight   // normalize blend ratio, nhưng vẫn fade theo totalWeight
                : Mathf.Clamp01(totalWeight * positionInfluence);  // giữ nguyên hành vi cũ

            float blendRotInfluence = normalizeWeights
                ? Mathf.Clamp01(rotationInfluence) * totalWeight
                : Mathf.Clamp01(totalWeight * rotationInfluence);

            float invTotalWeight = 1f / totalWeight;

            // ── Frame Snapshot ────────────────────────────────────────────────────────
            // Chụp toàn bộ state của constraintObject TRƯỚC khi bất kỳ Compute nào chạy.
            // Nhờ đó UpdatePosition và UpdateRotation đều đọc từ snapshot thuần (Animator/IK
            // đã apply xong), không phải từ output của nhau — loại bỏ order-dependency và
            // feedback loop trong cùng frame.
            _snapBaseWorldPos = GetBaselineWorldPos();
            _snapBaseWorldRot = GetBaselineWorldRot();
            _snapBaseLocalPos = GetBaselineLocalPos();
            _snapBaseLocalRot = GetBaselineLocalRot();

            // Basis từ rotation snapshot — tính một lần, dùng chung cho mọi target
            _snapRight   = _snapBaseWorldRot * Vector3.right;
            _snapUp      = _snapBaseWorldRot * Vector3.up;
            _snapForward = _snapBaseWorldRot * Vector3.forward;

            if (enablePositionConstraint)
            {
                UpdatePosition(invTotalWeight, blendPosInfluence);
            }

            if (enableRotationConstraint)
            {
                UpdateRotation(invTotalWeight, blendRotInfluence);
            }
        }

        private void UpdatePosition(float invTotalWeight, float finalInfluence)
        {
            Vector3 blendedPos = Vector3.zero;
            int count = targets.Count;

            if (positionSpace == SpaceMode.World)
            {
                Vector3 currentWorldPos = _snapBaseWorldPos;

                Vector3 blendedCopyAxes = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null || t.weight <= 0f) continue;
                    blendedCopyAxes += t.target.position * (t.weight * invTotalWeight);
                }

                // LƯU Ý: posCopyX/Y/Z ở World mode lọc theo trục WORLD (Vector3.right/up/forward),
                // KHÔNG phải trục local/basis của constraintObject — khác với Local mode (dùng
                // _snapRight/Up/Forward). Đây là chủ đích: World Absolute Position dùng để "khóa"
                // một trục world tuyệt đối (ví dụ giữ nguyên độ cao Y trong world dù target dịch
                // chuyển X/Z), phù hợp semantic "World" trong tên gọi. Nếu muốn lọc theo trục local
                // của constraintObject ở World mode, cần chiếu qua basis giống Local mode.
                // Ghép kết quả: trục được copy lấy từ weighted blend, trục bị khóa giữ baseline.
                Vector3 finalWorldPos = new Vector3(
                    posCopyX ? blendedCopyAxes.x : currentWorldPos.x,
                    posCopyY ? blendedCopyAxes.y : currentWorldPos.y,
                    posCopyZ ? blendedCopyAxes.z : currentWorldPos.z);

                constraintObject.position = Vector3.Lerp(currentWorldPos, finalWorldPos, finalInfluence);
            }
            else
            {
                Vector3 currentAnimLocalPos = _snapBaseLocalPos;
                bool isDelta = positionMode == ConstraintMode.Delta;
                Vector3 currentWorldPosForAbsolute = isDelta ? default : _snapBaseWorldPos;

                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null || t.weight <= 0f) continue;
                    Vector3 targetLocalPos;

                    if (isDelta)
                    {
                        targetLocalPos = ConstraintMath.ComputeLocalDeltaPosition(
                            t.target, constraintObject, t.initialLocalPos, t.initialWorldRot,
                            currentAnimLocalPos,
                            _snapRight, _snapUp, _snapForward,
                            posCopyX, posCopyY, posCopyZ);
                    }
                    else
                    {
                        targetLocalPos = ConstraintMath.ComputeLocalAbsolutePosition(
                            t.target, constraintObject, currentAnimLocalPos, currentWorldPosForAbsolute,
                            _snapRight, _snapUp, _snapForward,
                            posCopyX, posCopyY, posCopyZ);
                    }

                    blendedPos += targetLocalPos * (t.weight * invTotalWeight);
                }

                constraintObject.localPosition = Vector3.Lerp(currentAnimLocalPos, blendedPos, finalInfluence);
            }
        }

        private void UpdateRotation(float invTotalWeight, float finalInfluence)
        {
            int count = targets.Count;

            if (rotationSpace == SpaceMode.World)
            {
                Quaternion currentWorldRot = _snapBaseWorldRot;

                // Weighted NLerp: cộng dồn 4 thành phần quaternion theo normalized weight, normalize.
                // Order-independent, ổn định hơn Slerp tuần tự khi weight thay đổi liên tục.
                float accX = 0f, accY = 0f, accZ = 0f, accW = 0f;
                Quaternion refRot = Quaternion.identity;
                bool firstValid = true;

                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null || t.weight <= 0f) continue;

                    Quaternion targetWorldRot = ConstraintMath.ComputeWorldAbsoluteRotation(
                        t.target, constraintObject, currentWorldRot, rotCopyX, rotCopyY, rotCopyZ);

                    float w = t.weight * invTotalWeight;

                    if (firstValid)
                    {
                        refRot = targetWorldRot;
                        firstValid = false;
                    }
                    else if (Quaternion.Dot(refRot, targetWorldRot) < 0f)
                    {
                        // Hemisphere correction: flip quaternion về cùng phía với ref → tránh NLerp đi đường vòng
                        targetWorldRot = new Quaternion(-targetWorldRot.x, -targetWorldRot.y,
                                                        -targetWorldRot.z, -targetWorldRot.w);
                    }

                    accX += targetWorldRot.x * w;
                    accY += targetWorldRot.y * w;
                    accZ += targetWorldRot.z * w;
                    accW += targetWorldRot.w * w;
                }

                if (!firstValid)
                {
                    float mag = Mathf.Sqrt(accX * accX + accY * accY + accZ * accZ + accW * accW);
                    Quaternion blendedRot = mag > 1e-6f
                        ? new Quaternion(accX / mag, accY / mag, accZ / mag, accW / mag)
                        : currentWorldRot;

                    constraintObject.rotation = Quaternion.Slerp(currentWorldRot, blendedRot, finalInfluence);
                }
            }
            else
            {
                Quaternion currentLocalRot = _snapBaseLocalRot;
                bool isDelta = rotationMode == ConstraintMode.Delta;

                float accX = 0f, accY = 0f, accZ = 0f, accW = 0f;
                Quaternion refRot = Quaternion.identity;
                bool firstValid = true;

                for (int i = 0; i < count; i++)
                {
                    TargetData t = targets[i];
                    if (t.target == null || t.weight <= 0f) continue;
                    Quaternion targetLocalRot;

                    if (isDelta)
                    {
                        targetLocalRot = ConstraintMath.ComputeLocalDeltaRotation(
                            t.target, constraintObject, t.initialLocalRot, currentLocalRot,
                            rotCopyX, rotCopyY, rotCopyZ);
                    }
                    else
                    {
                        targetLocalRot = ConstraintMath.ComputeLocalAbsoluteRotation(
                            t.target, constraintObject, currentLocalRot, rotCopyX, rotCopyY, rotCopyZ);
                    }

                    float w = t.weight * invTotalWeight;

                    if (firstValid)
                    {
                        refRot = targetLocalRot;
                        firstValid = false;
                    }
                    else if (Quaternion.Dot(refRot, targetLocalRot) < 0f)
                    {
                        targetLocalRot = new Quaternion(-targetLocalRot.x, -targetLocalRot.y,
                                                        -targetLocalRot.z, -targetLocalRot.w);
                    }

                    accX += targetLocalRot.x * w;
                    accY += targetLocalRot.y * w;
                    accZ += targetLocalRot.z * w;
                    accW += targetLocalRot.w * w;
                }

                if (!firstValid)
                {
                    float mag = Mathf.Sqrt(accX * accX + accY * accY + accZ * accZ + accW * accW);
                    Quaternion blendedLocalRot = mag > 1e-6f
                        ? new Quaternion(accX / mag, accY / mag, accZ / mag, accW / mag)
                        : currentLocalRot;

                    constraintObject.localRotation = Quaternion.Lerp(currentLocalRot, blendedLocalRot, finalInfluence);
                }
            }
        }

        #region Gizmos

        void OnDrawGizmos()
        {
            if (!showGizmos || gizmoSize <= 0f) return;

            // Vẽ gizmo cho Constraint Object
            if (constraintObject != null)
            {
                DrawAxisGizmo(constraintObject);
            }

            // Vẽ gizmo cho các Target đang thực sự có ảnh hưởng (weight > 0)
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target != null && t.weight > 0f)
                {
                    DrawAxisGizmo(t.target);
                }
            }
        }

        /// <summary>
        /// Vẽ 3 mũi tên (right/up/forward) tại vị trí world của transform, dùng màu cấu hình sẵn cho từng trục.
        /// </summary>
        private void DrawAxisGizmo(Transform t)
        {
            Vector3 pos = t.position;

            Gizmos.color = gizmoColorX;
            DrawGizmoArrow(pos, t.right, gizmoSize);

            Gizmos.color = gizmoColorY;
            DrawGizmoArrow(pos, t.up, gizmoSize);

            Gizmos.color = gizmoColorZ;
            DrawGizmoArrow(pos, t.forward, gizmoSize);
        }

        /// <summary>
        /// Vẽ một mũi tên đơn (thân + 4 nét đầu mũi tên) từ origin theo hướng direction với độ dài length.
        /// Dùng Gizmos.color đã được set trước đó (không set màu bên trong để tránh gọi property thừa).
        /// </summary>
        private static void DrawGizmoArrow(Vector3 origin, Vector3 direction, float length)
        {
            if (direction == Vector3.zero) return;

            Vector3 dirNorm = direction.normalized;
            Vector3 end = origin + dirNorm * length;

            Gizmos.DrawLine(origin, end);

            // Tính 2 trục vuông góc với hướng mũi tên để dựng đầu mũi tên dạng chóp 4 cạnh
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

        #endregion
    }
}