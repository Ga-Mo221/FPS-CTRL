using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Copy Transform")]
public class TransformConstraint : MonoBehaviour
{
    public enum SpaceMode { World, Local }
    public enum ConstraintMode { Delta, Absolute }

    [System.Serializable]
    public class TargetData
    {
        [Tooltip("ID định danh duy nhất cho Target này (Ví dụ: 'm4a1', 'pistol', 'shield'). Được dùng để gọi từ code khi cần chuyển đổi vũ khí/vật thể bám.")]
        public string id;

        [Tooltip("Transform của vật thể đích mà đối tượng chịu ràng buộc (Constraint Object) sẽ bắt chước Tọa độ/Góc xoay theo.")]
        public Transform target;

        [Range(0f, 1f)]
        [Tooltip("Trọng số ảnh hưởng riêng của Target này.\n• 1 = Chịu ảnh hưởng tối đa.\n• 0 = Hoàn toàn bỏ qua.\n• Nếu dùng nhiều Target (Multi-Target), hệ thống tự động hòa trộn vị trí/góc xoay theo tỷ lệ Weight giữa chúng.")]
        public float weight = 1f;

        // Cache mốc ban đầu phục vụ chế độ Delta (Quản lý riêng cho từng Target)
        [HideInInspector] public Vector3 initialLocalPos;
        [HideInInspector] public Quaternion initialLocalRot;
    }
    
    [Tooltip("Danh sách các điểm đích cần bắt chước theo. Bạn có thể thêm nhiều mục tiêu tại đây và thay đổi Weight để blend mượt mà giữa các tư thế cầm nắm.")]
    public List<TargetData> targets = new List<TargetData>();

    [Tooltip("Đối tượng trực tiếp chịu sự ràng buộc (Ví dụ: Xương bàn tay, xương cổ, hoặc báng súng). Vật thể này sẽ bị ép di chuyển và xoay theo các Target phía trên.")]
    public Transform constraintObject;

    [Tooltip("✔ TRUE (mặc định, giữ hành vi cũ): constraintObject có Animator hoặc hệ thống khác ghi đè Transform mỗi frame " +
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

    [Tooltip("Không gian tính toán tọa độ:\n• World: Bám theo vị trí tuyệt đối trong thế giới game.\n• Local: Tính toán dựa trên khoảng cách tương đối với cha (Phù hợp làm chuỗi xương nhân vật, tránh lỗi giật khớp khi nhân vật di chuyển).")]
    public SpaceMode positionSpace = SpaceMode.World;

    [Tooltip("Chế độ ràng buộc vị trí (Chỉ có tác dụng khi chọn Local Space):\n• Absolute: Ép thẳng tọa độ Local của vật thể trùng với Target.\n• Delta: Chỉ bắt chước lượng thay đổi (di chuyển bao nhiêu thì dịch chuyển bấy nhiêu), giữ nguyên khoảng cách gốc ban đầu giữa 2 vật thể (Khuyên dùng cho súng/vũ khí).")]
    public ConstraintMode positionMode = ConstraintMode.Delta;

    [Range(0f, 1f)]
    [Tooltip("Mức độ ảnh hưởng tổng thể của liên kết vị trí:\n• 1 = Khớp khít hoàn toàn theo Target.\n• 0 = Trả về theo Animation gốc của nhân vật.\n• 0.5 = Nằm chính giữa góc của Animation và vị trí Target.")]
    public float positionInfluence = 1f;

    [Tooltip("✔ TRUE: Copy tọa độ trục X.\n❌ FALSE: Giữ nguyên trục X theo hoạt cảnh gốc.")]
    public bool posCopyX = true;
    [Tooltip("✔ TRUE: Copy tọa độ trục Y.\n❌ FALSE: Giữ nguyên trục Y theo hoạt cảnh gốc.")]
    public bool posCopyY = true;
    [Tooltip("✔ TRUE: Copy tọa độ trục Z.\n❌ FALSE: Giữ nguyên trục Z theo hoạt cảnh gốc.")]
    public bool posCopyZ = true;
    
    [Tooltip("✔ TRUE: Bật tính năng bắt chước Góc xoay (Rotation) từ Target.\n❌ FALSE: Bỏ qua không xử lý góc xoay.")]
    public bool enableRotationConstraint = true;

    [Tooltip("Không gian tính toán góc xoay:\n• World: Xoay hướng tuyệt đối theo thế giới game.\n• Local: Chỉ tính toán góc xoay tương đối so với vật thể cha.")]
    public SpaceMode rotationSpace = SpaceMode.World;

    [Tooltip("Chế độ ràng buộc góc xoay (Chỉ có tác dụng khi chọn Local Space):\n• Absolute: Ép thẳng góc xoay Local trùng với góc Target.\n• Delta: Chỉ xoay thêm một lượng bằng đúng lượng Target vừa xoay, bảo toàn hướng xoay ban đầu của súng/tay cầm.")]
    public ConstraintMode rotationMode = ConstraintMode.Delta;

    [Range(0f, 1f)]
    [Tooltip("Mức độ ảnh hưởng tổng thể của liên kết góc xoay:\n• 1 = Xoay trùng khít hoàn toàn theo Target.\n• 0 = Trả về theo hướng của Animation gốc.\n• 0.5 = Nằm chính giữa góc của Animation và hướng của Target.")]
    public float rotationInfluence = 1f;

    [Tooltip("✔ TRUE: Copy góc xoay quanh trục X.\n❌ FALSE: Giữ nguyên trục X.")]
    public bool rotCopyX = true;
    [Tooltip("✔ TRUE: Copy góc xoay quanh trục Y.\n❌ FALSE: Giữ nguyên trục Y.")]
    public bool rotCopyY = true;
    [Tooltip("✔ TRUE: Copy góc xoay quanh trục Z.\n❌ FALSE: Giữ nguyên trục Z.")]
    public bool rotCopyZ = true;

    [Tooltip("✔ TRUE: Vẽ gizmo mũi tên 3 trục (X/Y/Z) trong Scene View cho Constraint Object và các Target đang có Weight > 0.\n❌ FALSE: Ẩn toàn bộ gizmo.")]
    public bool showGizmos = false;

    [Tooltip("Kích thước (độ dài) của mũi tên gizmo, tính theo đơn vị world-space.")]
    public float gizmoSize = 0.3f;

    [Tooltip("Màu của mũi tên trục X.")]
    public Color gizmoColorX = Color.red;

    [Tooltip("Màu của mũi tên trục Y.")]
    public Color gizmoColorY = Color.green;

    [Tooltip("Màu của mũi tên trục Z.")]
    public Color gizmoColorZ = Color.blue;

    private Coroutine switchCoroutine;

    // Cache để tránh alloc mỗi lần gọi SwitchTarget
    private float[] _cachedStartWeights = new float[0];

    void OnEnable()
    {
        CaptureInitialStates();

        // Nếu không có Animator/hệ thống nào khác điều khiển constraintObject mỗi frame,
        // phải chụp Rest Pose ngay tại đây (trước khi LateUpdate đầu tiên chạy) để làm baseline
        // cố định — tránh việc baseline tự trôi theo output của chính constraint (xem ghi chú ở
        // hasExternalPoseDriver).
        if (!hasExternalPoseDriver)
        {
            CaptureRestPose();
        }
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
            }
        }
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
            initialLocalRot = targetTransform.localRotation
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
        float elapsed = 0f;
        int count = targets.Count;

        // Tái sử dụng array cache, chỉ resize khi thực sự cần thiết → tránh alloc mỗi lần switch
        if (_cachedStartWeights.Length < count)
            _cachedStartWeights = new float[count];

        for (int i = 0; i < count; i++)
            _cachedStartWeights[i] = targets[i].weight;

        float invDuration = duration > 0f ? 1f / duration : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pct = elapsed * invDuration;

            for (int i = 0; i < count; i++)
            {
                float targetGoal = (targets[i].id == targetId) ? 1f : 0f;
                targets[i].weight = Mathf.Lerp(_cachedStartWeights[i], targetGoal, pct);
            }

            yield return null;
        }

        // Dùng targets.Count trực tiếp ở đây để an toàn nếu danh sách thay đổi trong lúc Coroutine chạy
        int finalCount = targets.Count;
        for (int i = 0; i < finalCount; i++)
            targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
    }

    void LateUpdate()
    {
        int count = targets.Count;
        if (constraintObject == null || count == 0) return;

        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
        {
            TargetData t = targets[i];
            if (t.target != null) totalWeight += t.weight;
        }
        if (totalWeight <= 0f) return; 

        if (enablePositionConstraint)
        {
            UpdatePosition(totalWeight);
        }

        if (enableRotationConstraint)
        {
            UpdateRotation(totalWeight);
        }
    }

    private void UpdatePosition(float totalWeight)
    {
        Vector3 blendedPos = Vector3.zero;
        int count = targets.Count;
        float invTotalWeight = 1f / totalWeight;

        if (positionSpace == SpaceMode.World)
        {
            // Cache world position trước vòng lặp — tránh đọc lại property sau khi blend xong
            // VÀ tránh việc ConstraintMath phải tự đọc lại constraintObject.position mỗi target.
            // Dùng GetBaselineWorldPos() thay vì đọc thẳng constraintObject.position: nếu không có
            // driver ngoài (Animator...), đọc live ở đây chính là bug drift baseline đã phân tích.
            Vector3 currentWorldPos = GetBaselineWorldPos();
            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target == null || t.weight <= 0f) continue;
                Vector3 targetWorldPos = ConstraintMath.ComputeWorldAbsolutePosition(
                    t.target, constraintObject, currentWorldPos, posCopyX, posCopyY, posCopyZ);
                blendedPos += targetWorldPos * (t.weight * invTotalWeight);
            }
            
            float finalInfluence = Mathf.Clamp01(totalWeight * positionInfluence);
            constraintObject.position = Vector3.Lerp(currentWorldPos, blendedPos, finalInfluence);
        }
        else
        {
            // Baseline local: từ Rest Pose đã chụp (nếu không có driver ngoài) thay vì đọc live —
            // xem ghi chú ở hasExternalPoseDriver.
            Vector3 currentAnimLocalPos = GetBaselineLocalPos();
            bool isDelta = positionMode == ConstraintMode.Delta;

            // Chỉ đọc baseline world (native, tốn hơn localPosition) khi thực sự
            // cần cho chế độ Absolute — chế độ Delta không dùng đến giá trị này
            Vector3 currentWorldPosForAbsolute = isDelta ? default : GetBaselineWorldPos();

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target == null || t.weight <= 0f) continue;
                Vector3 targetLocalPos;

                if (isDelta)
                {
                    targetLocalPos = ConstraintMath.ComputeLocalDeltaPosition(
                        t.target, constraintObject, t.initialLocalPos, currentAnimLocalPos, posCopyX, posCopyY, posCopyZ);
                }
                else
                {
                    targetLocalPos = ConstraintMath.ComputeLocalAbsolutePosition(
                        t.target, constraintObject, currentAnimLocalPos, currentWorldPosForAbsolute, posCopyX, posCopyY, posCopyZ);
                }
                blendedPos += targetLocalPos * (t.weight * invTotalWeight);
            }
            
            float finalInfluence = Mathf.Clamp01(totalWeight * positionInfluence);
            constraintObject.localPosition = Vector3.Lerp(currentAnimLocalPos, blendedPos, finalInfluence);
        }
    }

    private void UpdateRotation(float totalWeight)
    {
        int count = targets.Count;

        if (rotationSpace == SpaceMode.World)
        {
            // Cache world rotation trước vòng lặp — tránh đọc lại property khi Lerp.
            // Dùng GetBaselineWorldRot() để tránh drift khi không có driver ngoài (Animator...).
            Quaternion currentWorldRot = GetBaselineWorldRot();
            Quaternion blendedRot = Quaternion.identity;
            float currentSubWeight = 0f;
            bool firstValid = true;

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target == null || t.weight <= 0f) continue;
                Quaternion targetWorldRot = ConstraintMath.ComputeWorldAbsoluteRotation(
                    t.target, constraintObject, currentWorldRot, rotCopyX, rotCopyY, rotCopyZ);

                if (firstValid)
                {
                    blendedRot = targetWorldRot;
                    currentSubWeight = t.weight;
                    firstValid = false;
                }
                else
                {
                    currentSubWeight += t.weight;
                    blendedRot = Quaternion.Slerp(blendedRot, targetWorldRot, t.weight / currentSubWeight);
                }
            }

            if (!firstValid) 
            {
                float finalInfluence = Mathf.Clamp01(totalWeight * rotationInfluence);
                constraintObject.rotation = Quaternion.Lerp(currentWorldRot, blendedRot, finalInfluence);
            }
        }
        else
        {
            // Baseline local rotation: từ Rest Pose đã chụp (nếu không có driver ngoài) — xem ghi chú
            // ở hasExternalPoseDriver.
            Quaternion currentLocalRot = GetBaselineLocalRot();
            Quaternion blendedLocalRot = Quaternion.identity;
            float currentSubWeight = 0f;
            bool firstValid = true;
            bool isDelta = rotationMode == ConstraintMode.Delta;

            for (int i = 0; i < count; i++)
            {
                TargetData t = targets[i];
                if (t.target == null || t.weight <= 0f) continue;
                Quaternion targetLocalRot;

                if (isDelta)
                {
                    targetLocalRot = ConstraintMath.ComputeLocalDeltaRotation(
                        t.target, constraintObject, t.initialLocalRot, currentLocalRot, rotCopyX, rotCopyY, rotCopyZ);
                }
                else
                {
                    targetLocalRot = ConstraintMath.ComputeLocalAbsoluteRotation(
                        t.target, constraintObject, currentLocalRot, rotCopyX, rotCopyY, rotCopyZ);
                }

                if (firstValid)
                {
                    blendedLocalRot = targetLocalRot;
                    currentSubWeight = t.weight;
                    firstValid = false;
                }
                else
                {
                    currentSubWeight += t.weight;
                    blendedLocalRot = Quaternion.Slerp(blendedLocalRot, targetLocalRot, t.weight / currentSubWeight);
                }
            }

            if (!firstValid)
            {
                float finalInfluence = Mathf.Clamp01(totalWeight * rotationInfluence);
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