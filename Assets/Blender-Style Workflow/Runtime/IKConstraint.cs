using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("IK Constraint")]
[ExecuteAlways]
public class IKConstraint : MonoBehaviour
{
    [System.Serializable]
    public class IKTargetData
    {
        [Tooltip("ID định danh duy nhất cho Target này (Ví dụ: 'Left_Hand', 'Right_Hand'). Dùng để gọi lệnh chuyển đổi mục tiêu (Switch Target) mượt mà từ mã code.")]
        public string id;

        [Tooltip("Transform của vật thể đích mà chuỗi xương sẽ cố gắng di chuyển phần ngọn tới và bám khít vào.")]
        public Transform target;

        [Range(0f, 1f)]
        [Tooltip("Trọng số ảnh hưởng của riêng Target này. Nếu cấu hình nhiều Target (Multi-Target), hệ thống sẽ tự động hòa trộn vị trí và góc xoay dựa trên tỷ lệ giữa các Weight.")]
        public float weight = 0f;
    }

    [System.Serializable]
    public class IKPoleTargetData
    {
        [Tooltip("ID định danh duy nhất cho Pole Target này (Ví dụ: 'Left_Elbow', 'Right_Knee'). Dùng để gọi lệnh chuyển đổi Pole Target (Switch Pole Target) mượt mà từ mã code.")]
        public string id;

        [Tooltip("Transform của vật thể định hướng khớp phụ (Khuỷu tay / Đầu gối). Ép chuỗi xương luôn bẻ cong về hướng này, tránh hiện tượng gập khớp ngược dị dạng.")]
        public Transform poleTarget;

        [Range(0f, 1f)]
        [Tooltip("Trọng số ảnh hưởng của riêng Pole Target này. Hệ thống sẽ hòa trộn hướng bẻ cong dựa trên tỷ lệ giữa các Weight.")]
        public float weight = 0f;
    }

    [HideInInspector] 
    public bool preview;

    [Tooltip("Danh sách các điểm đích cần hướng tới. Bạn có thể thêm nhiều điểm và thay đổi Weight để blend (hòa trộn) mượt mà giữa các vị trí/góc xoay.")]
    public List<IKTargetData> targets = new List<IKTargetData>();

    [Tooltip("Danh sách các Pole Target định hướng bẻ cong khớp phụ (Khuỷu tay / Đầu gối). Hệ thống sẽ hòa trộn hướng bẻ cong dựa trên tỷ lệ Weight của từng Pole Target.")]
    public List<IKPoleTargetData> poleTargets = new List<IKPoleTargetData>();

    [Tooltip("Danh sách các khớp xương xếp theo thứ tự liên tục từ GỐC đến NGỌN.\n• Phần tử [0] phải là xương gốc (Ví dụ: Vai/UpperArm).\n• Phần tử CUỐI CÙNG phải là xương ngọn (Ví dụ: Cổ tay/Hand) - khớp sẽ bám khít và copy rotation từ Target.")]
    public Transform[] bones;

    [Range(0f, 1f)]
    [Tooltip("Mức độ ảnh hưởng tổng thể của giải thuật IK:\n• 1 = Ăn hoàn toàn theo vị trí/góc xoay của Target.\n• 0 = Trả về trạng thái hoạt cảnh (Animation) gốc của nhân vật.\n• 0.5 = Hòa trộn chính giữa góc của Animation và IK.")]
    public float ikInfluence = 1f;

    [Range(1, 50)]
    [Tooltip("Số lần lặp tối đa của giải thuật FABRIK trong một Frame:\n• Từ 5 - 15: Phù hợp cho thiết bị di động (Mobile) / Game Runtime số lượng lớn (Tối ưu hiệu năng).\n• Từ 15 - 30: Khớp bám cực kỳ chính xác (Phù hợp PC/Console hoặc các phân cảnh Cutscene cần độ tỉ mỉ cao).")]
    public int iterations = 10;

    [Tooltip("Độ sai số khoảng cách chấp nhận được giữa Xương Ngọn và Target (tính bằng mét).\n• Khuyên dùng từ 0.001 (1mm) đến 0.01 (1cm).\n• Nếu khoảng cách nhỏ hơn mức này, thuật toán sẽ tự động dừng lặp sớm để TIẾT KIỆM HIỆU NĂNG CPU.")]
    public float delta = 0.001f;

    [Tooltip("Bật/tắt vẽ Gizmo minh hoạ trong Scene View.")]
    public bool drawGizmos = false;

    [Tooltip("Kích thước cạnh đáy vuông của hình chóp vẽ tại các Target đang có Weight > 0.")]
    public float targetGizmoSize = 0.06f;

    [Tooltip("Chiều dài (chiều cao) hình chóp, tính từ đáy lên tới đỉnh chóp.")]
    public float targetGizmoHeight = 0.12f;

    [Tooltip("Bán kính hình cầu vẽ tại vị trí Pole Target.")]
    public float elbowGizmoRadius = 0.04f;

    [Tooltip("Màu hình chóp vuông tại Target")]
    public Color targetGizmoColor = Color.red;

    [Tooltip("Màu hình cầu tại Pole Target")]
    public Color elbowGizmoColor  = Color.green;

    // ── Cache tính toán nội bộ ────────────────────────────────────────────────────
    private float[]      bonesLength;
    private Vector3[]    positions;
    private Quaternion[] startRotationBone;
    private Vector3[]    startDirectionSucceded;
    // Offset twist của từng bone so với hướng chỉ về xương kế tiếp lúc Init.
    // Bất biến kể cả khi character xoay — dùng để reconstruct rotation đúng mỗi frame.
    private Quaternion[] startRotationOffset;
    private bool         isInitialized;
    private float        totalChainLength;

    // ── Runtime switching ─────────────────────────────────────────────────────────
    private Coroutine _switchTargetCoroutine;
    private Coroutine _switchPoleTargetCoroutine;
    private float[]   _cachedStartWeights     = new float[0];
    private float[]   _cachedPoleStartWeights = new float[0];

    void OnEnable()
    {
        InitIKChain();
    }

    // ══════════════════════════════ PUBLIC API — TARGET ══════════════════════════════

    public bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)
    {
        int count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            if (targets[i].id == id)
            {
                Debug.LogWarning($"[IKConstraint] Target với ID '{id}' đã tồn tại!");
                return false;
            }
        }

        if (targetTransform == null)
        {
            Debug.LogError("[IKConstraint] Transform của Target truyền vào bị null!");
            return false;
        }

        targets.Add(new IKTargetData
        {
            id     = id,
            target = targetTransform,
            weight = Mathf.Clamp01(initialWeight)
        });
        return true;
    }

    public bool RemoveTarget(string id)
    {
        int count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            if (targets[i].id == id)
            {
                targets.RemoveAt(i);
                return true;
            }
        }

        Debug.LogWarning($"[IKConstraint] Không tìm thấy Target nào có ID '{id}' để xóa!");
        return false;
    }

    public bool SetTargetWeight(string id, float weight)
    {
        int count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            if (targets[i].id == id)
            {
                targets[i].weight = Mathf.Clamp01(weight);
                return true;
            }
        }

        Debug.LogWarning($"[IKConstraint] Không tìm thấy Target nào có ID '{id}' để set weight!");
        return false;
    }

    public void SetIKInfluence(float influence)
    {
        ikInfluence = Mathf.Clamp01(influence);
    }

    public void SwitchTarget(string targetId, float duration = 0.5f)
    {
        bool found = false;
        int  count = targets.Count;
        for (int i = 0; i < count; i++)
            if (targets[i].id == targetId) { found = true; break; }

        if (!found)
        {
            Debug.LogWarning($"[IKConstraint] SwitchTarget: Không tìm thấy Target có ID '{targetId}'!");
            return;
        }

        if (!Application.isPlaying)
        {
            int cnt = targets.Count;
            for (int i = 0; i < cnt; i++)
                targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
            return;
        }

        if (_switchTargetCoroutine != null)
            StopCoroutine(_switchTargetCoroutine);
        _switchTargetCoroutine = StartCoroutine(SwitchTargetRoutine(targetId, duration));
    }

    private IEnumerator SwitchTargetRoutine(string targetId, float duration)
    {
        float elapsed = 0f;
        int   count   = targets.Count;

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
                float goal = (targets[i].id == targetId) ? 1f : 0f;
                targets[i].weight = Mathf.Lerp(_cachedStartWeights[i], goal, pct);
            }

            yield return null;
        }

        int finalCount = targets.Count;
        for (int i = 0; i < finalCount; i++)
            targets[i].weight = (targets[i].id == targetId) ? 1f : 0f;
    }

    // ══════════════════════════════ PUBLIC API — POLE TARGET ══════════════════════════════

    public bool AddPoleTarget(string id, Transform poleTransform, float initialWeight = 0f)
    {
        int count = poleTargets.Count;
        for (int i = 0; i < count; i++)
        {
            if (poleTargets[i].id == id)
            {
                Debug.LogWarning($"[IKConstraint] Pole Target với ID '{id}' đã tồn tại!");
                return false;
            }
        }

        if (poleTransform == null)
        {
            Debug.LogError("[IKConstraint] Transform của Pole Target truyền vào bị null!");
            return false;
        }

        poleTargets.Add(new IKPoleTargetData
        {
            id         = id,
            poleTarget = poleTransform,
            weight     = Mathf.Clamp01(initialWeight)
        });
        return true;
    }

    public bool RemovePoleTarget(string id)
    {
        int count = poleTargets.Count;
        for (int i = 0; i < count; i++)
        {
            if (poleTargets[i].id == id)
            {
                poleTargets.RemoveAt(i);
                return true;
            }
        }

        Debug.LogWarning($"[IKConstraint] Không tìm thấy Pole Target nào có ID '{id}' để xóa!");
        return false;
    }

    public bool SetPoleTargetWeight(string id, float weight)
    {
        int count = poleTargets.Count;
        for (int i = 0; i < count; i++)
        {
            if (poleTargets[i].id == id)
            {
                poleTargets[i].weight = Mathf.Clamp01(weight);
                return true;
            }
        }

        Debug.LogWarning($"[IKConstraint] Không tìm thấy Pole Target nào có ID '{id}' để set weight!");
        return false;
    }

    public void SwitchPoleTarget(string poleTargetId, float duration = 0.5f)
    {
        bool found = false;
        int  count = poleTargets.Count;
        for (int i = 0; i < count; i++)
            if (poleTargets[i].id == poleTargetId) { found = true; break; }

        if (!found)
        {
            Debug.LogWarning($"[IKConstraint] SwitchPoleTarget: Không tìm thấy Pole Target có ID '{poleTargetId}'!");
            return;
        }

        if (!Application.isPlaying)
        {
            int cnt = poleTargets.Count;
            for (int i = 0; i < cnt; i++)
                poleTargets[i].weight = (poleTargets[i].id == poleTargetId) ? 1f : 0f;
            return;
        }

        if (_switchPoleTargetCoroutine != null)
            StopCoroutine(_switchPoleTargetCoroutine);
        _switchPoleTargetCoroutine = StartCoroutine(SwitchPoleTargetRoutine(poleTargetId, duration));
    }

    private IEnumerator SwitchPoleTargetRoutine(string poleTargetId, float duration)
    {
        float elapsed = 0f;
        int   count   = poleTargets.Count;

        if (_cachedPoleStartWeights.Length < count)
            _cachedPoleStartWeights = new float[count];

        for (int i = 0; i < count; i++)
            _cachedPoleStartWeights[i] = poleTargets[i].weight;

        float invDuration = duration > 0f ? 1f / duration : 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pct = elapsed * invDuration;

            for (int i = 0; i < count; i++)
            {
                float goal = (poleTargets[i].id == poleTargetId) ? 1f : 0f;
                poleTargets[i].weight = Mathf.Lerp(_cachedPoleStartWeights[i], goal, pct);
            }

            yield return null;
        }

        int finalCount = poleTargets.Count;
        for (int i = 0; i < finalCount; i++)
            poleTargets[i].weight = (poleTargets[i].id == poleTargetId) ? 1f : 0f;
    }

    // ══════════════════════════════ PUBLIC API — PAIRED ══════════════════════════════

    public bool AddPair(string id, Transform targetTransform, Transform poleTransform, float initialWeight = 0f)
    {
        bool addedTarget = AddTarget(id, targetTransform, initialWeight);
        bool addedPole   = AddPoleTarget(id, poleTransform, initialWeight);
        return addedTarget && addedPole;
    }

    public bool RemovePair(string id)
    {
        bool removedTarget = RemoveTarget(id);
        bool removedPole   = RemovePoleTarget(id);
        return removedTarget && removedPole;
    }

    public bool SetPairWeight(string id, float weight)
    {
        bool setTarget = SetTargetWeight(id, weight);
        bool setPole   = SetPoleTargetWeight(id, weight);
        return setTarget && setPole;
    }

    public void SwitchPair(string id, float duration = 0.5f)
    {
        SwitchTarget(id, duration);
        SwitchPoleTarget(id, duration);
    }

    // ══════════════════════════════ CORE ══════════════════════════════

    /// <summary>
    /// Khởi tạo và đo đạc thông số ban đầu của chuỗi xương.
    /// </summary>
    public void InitIKChain()
    {
        if (bones == null || bones.Length < 2)
        {
            isInitialized = false;
            return;
        }

        positions              = new Vector3[bones.Length];
        bonesLength            = new float[bones.Length - 1];
        startRotationBone      = new Quaternion[bones.Length];
        startDirectionSucceded = new Vector3[bones.Length - 1];
        startRotationOffset    = new Quaternion[bones.Length - 1];

        totalChainLength = 0f;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) { isInitialized = false; return; }

            startRotationBone[i] = bones[i].rotation;
            if (i < bones.Length - 1)
            {
                if (bones[i + 1] == null) { isInitialized = false; return; }
                Vector3 dir               = bones[i + 1].position - bones[i].position;
                bonesLength[i]            = dir.magnitude;
                startDirectionSucceded[i] = dir;
                totalChainLength         += bonesLength[i];

                // Tính offset twist của bone so với hướng chỉ về xương kế tiếp.
                // startRotationOffset = Inverse(LookRotation(startDir)) * bones[i].rotation
                // Khi apply: targetRot = LookRotation(targetDir) * startRotationOffset
                // => hoạt động đúng bất kể character xoay bao nhiêu vì offset là local/relative.
                Vector3 dirNorm = dir.normalized;
                startRotationOffset[i] = Quaternion.Inverse(Quaternion.LookRotation(dirNorm))
                                         * bones[i].rotation;
            }
        }
        isInitialized = true;
    }
    
    void LateUpdate()
    {
        // Kiểm tra điều kiện Preview ở Edit Mode (Khi chưa bấm Play)
        if (!Application.isPlaying && !preview) return;
        
        ApplyIK();
    }

    /// <summary>
    /// Thực thi toàn bộ logic giải IK (FABRIK + Pole Target + Apply bone).
    /// </summary>
    public void ApplyIK()
    {
        if (!isInitialized || bones == null || bones.Length < 2 || targets.Count == 0)
        {
            if (bones != null && bones.Length >= 2 && !isInitialized) InitIKChain();
            return;
        }

        // 1. Tính vị trí Blend của các Target theo Weight
        float   totalWeight       = 0f;
        Vector3 weightedTargetPos = Vector3.zero;

        foreach (var t in targets)
        {
            if (t.target == null || t.weight <= 0f) continue;
            totalWeight       += t.weight;
            weightedTargetPos += t.target.position * t.weight;
        }
        if (totalWeight <= 0f) return;

        Vector3 blendedTargetPos = weightedTargetPos / totalWeight;

        // 2. Nạp vị trí hiện tại của chuỗi xương
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) return;
            positions[i] = bones[i].position;
        }

        Vector3 rootPos = positions[0];

        // 3. Giải IK bằng FABRIK
        IKMath.SolveFABRIK(positions, bonesLength, blendedTargetPos, rootPos, iterations, delta, totalChainLength);

        // 4. Xử lý Pole Target
        float   totalPoleWeight = 0f;
        Vector3 weightedPolePos = Vector3.zero;
        bool    hasPole         = false;

        foreach (var p in poleTargets)
        {
            if (p.poleTarget == null || p.weight <= 0f) continue;
            totalPoleWeight += p.weight;
            weightedPolePos += p.poleTarget.position * p.weight;
            hasPole          = true;
        }

        if (hasPole && totalPoleWeight > 0f)
        {
            Vector3 blendedPolePos = weightedPolePos / totalPoleWeight;

            for (int i = 1; i < positions.Length - 1; i++)
            {
                Plane   plane         = new Plane(positions[i + 1] - positions[i - 1], positions[i - 1]);
                Vector3 projectedPole = plane.ClosestPointOnPlane(blendedPolePos);
                Vector3 projectedBone = plane.ClosestPointOnPlane(positions[i]);
                float   angle         = Vector3.SignedAngle(
                                            projectedBone - positions[i - 1],
                                            projectedPole - positions[i - 1],
                                            plane.normal);
                positions[i] = Quaternion.AngleAxis(angle, plane.normal)
                               * (positions[i] - positions[i - 1]) + positions[i - 1];
            }
        }

        // 5. Apply Rotation/Position lên các xương
        float finalInfluence = Mathf.Clamp01(totalWeight * ikInfluence);
        bool  syncPosition   = finalInfluence >= 0.99f;

        for (int i = 0; i < bones.Length; i++)
        {
            Transform  bone   = bones[i];
            Quaternion newRot = bone.rotation;

            // Xương thân (Gốc → Áp út): xoay về phía khớp tiếp theo
            if (i < bones.Length - 1)
            {
                Vector3 targetDir = (positions[i + 1] - positions[i]).normalized;
                // LookRotation(targetDir) * startRotationOffset cho kết quả đúng kể cả
                // khi character xoay, vì startRotationOffset là twist-offset bất biến
                // tương đối so với hướng của bone — không bị "đóng băng" trong world space.
                Quaternion targetRot = Quaternion.LookRotation(targetDir) * startRotationOffset[i];
                newRot = Quaternion.Lerp(bone.rotation, targetRot, finalInfluence);
            }
            // Xương NGỌN: copy rotation từ Target
            else
            {
                Quaternion blendedTargetRot = Quaternion.identity;
                bool       firstValidRot    = true;
                float      currentSubWeight = 0f;

                foreach (var t in targets)
                {
                    if (t.target == null || t.weight <= 0f) continue;

                    if (firstValidRot)
                    {
                        blendedTargetRot = t.target.rotation;
                        currentSubWeight = t.weight;
                        firstValidRot    = false;
                    }
                    else
                    {
                        currentSubWeight += t.weight;
                        blendedTargetRot  = Quaternion.Slerp(
                            blendedTargetRot, t.target.rotation,
                            t.weight / currentSubWeight);
                    }
                }

                if (!firstValidRot)
                    newRot = Quaternion.Lerp(bone.rotation, blendedTargetRot, finalInfluence);
            }

            if (i > 0 && syncPosition)
                bone.SetPositionAndRotation(positions[i], newRot);
            else
                bone.rotation = newRot;
        }
    }

    void OnValidate()
    {
        InitIKChain();
    }

    // ══════════════════════════════ GIZMOS ══════════════════════════════

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        DrawTargetPyramidGizmos();
        DrawPoleTargetSphereGizmos();
    }

    private void DrawTargetPyramidGizmos()
    {
        if (targets == null) return;

        Gizmos.color = targetGizmoColor;
        foreach (var t in targets)
        {
            if (t == null || t.target == null || t.weight <= 0f) continue;
            DrawPyramidGizmo(t.target.position, t.target.rotation, targetGizmoSize, targetGizmoHeight);
        }
    }

    private void DrawPyramidGizmo(Vector3 basePosition, Quaternion rotation, float baseSize, float height)
    {
        float   half    = baseSize * 0.5f;
        Vector3 up      = rotation * Vector3.up;
        Vector3 right   = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;

        Vector3 apex = basePosition + up * height;
        Vector3 p1   = basePosition + right * half + forward * half;
        Vector3 p2   = basePosition + right * half - forward * half;
        Vector3 p3   = basePosition - right * half - forward * half;
        Vector3 p4   = basePosition - right * half + forward * half;

        Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);

        Gizmos.DrawLine(p1, apex); Gizmos.DrawLine(p2, apex);
        Gizmos.DrawLine(p3, apex); Gizmos.DrawLine(p4, apex);
    }

    private void DrawPoleTargetSphereGizmos()
    {
        if (poleTargets == null) return;

        Gizmos.color = elbowGizmoColor;
        foreach (var p in poleTargets)
        {
            if (p == null || p.poleTarget == null || p.weight <= 0f) continue;

            Vector3 center = p.poleTarget.position;
            DrawCircleGizmo(center, p.poleTarget.forward, elbowGizmoRadius);
            DrawCircleGizmo(center, p.poleTarget.right,   elbowGizmoRadius);
            DrawCircleGizmo(center, p.poleTarget.up,      elbowGizmoRadius);
        }
    }

    private void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments = 24)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        Vector3 prevPoint = center + tangent * radius;
        float   angleStep = (Mathf.PI * 2f) / segments;

        for (int s = 1; s <= segments; s++)
        {
            float   angle     = s * angleStep;
            Vector3 nextPoint = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}