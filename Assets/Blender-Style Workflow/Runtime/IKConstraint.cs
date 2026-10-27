using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [ExecuteAlways]
    public class IKConstraint : ConstraintBehaviour
    {
        [System.Serializable]
        public class IKTargetData
        {
            [Tooltip(
                "ID định danh duy nhất cho Target này (Ví dụ: 'Left_Hand', 'Right_Hand'). Dùng để gọi lệnh chuyển đổi mục tiêu (Switch Target) mượt mà từ mã code.")]
            public string id;

            [Tooltip("Transform của vật thể đích mà chuỗi xương sẽ cố gắng di chuyển phần ngọn tới và bám khít vào.")]
            public Transform target;

            [Range(0f, 1f)]
            [Tooltip(
                "Trọng số ảnh hưởng của riêng Target này. Nếu cấu hình nhiều Target (Multi-Target), hệ thống sẽ tự động hòa trộn vị trí và góc xoay dựa trên tỷ lệ giữa các Weight.")]
            public float weight = 0f;
        }

        [System.Serializable]
        public class IKPoleTargetData
        {
            [Tooltip(
                "ID định danh duy nhất cho Pole Target này (Ví dụ: 'Left_Elbow', 'Right_Knee'). Dùng để gọi lệnh chuyển đổi Pole Target (Switch Pole Target) mượt mà từ mã code.")]
            public string id;

            [Tooltip(
                "Transform của vật thể định hướng khớp phụ (Khuỷu tay / Đầu gối). Ép chuỗi xương luôn bẻ cong về hướng này, tránh hiện tượng gập khớp ngược dị dạng.")]
            public Transform poleTarget;

            [Range(0f, 1f)]
            [Tooltip(
                "Trọng số ảnh hưởng của riêng Pole Target này. Hệ thống sẽ hòa trộn hướng bẻ cong dựa trên tỷ lệ giữa các Weight.")]
            public float weight = 0f;
        }

        [HideInInspector] public bool preview;

        /// <summary>Pose local của 1 Transform (bone / target / pole target) tại thời điểm lưu.</summary>
        [System.Serializable]
        public struct PoseSnapshot
        {
            public Transform transform;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        // Pose của TẤT CẢ Transform liên quan: bones trong chain + Target + Pole Target (đã loại trùng),
        // chụp lúc bấm Preview In Editor từ INACTIVE → ACTIVE. Được serialize cùng component
        // (scene/prefab) nên vẫn còn sau khi đóng/mở lại Unity (miễn là đã Save scene) — không phụ thuộc Undo stack.
        [HideInInspector, SerializeField, UnityEngine.Serialization.FormerlySerializedAs("savedBonePose")]
        private PoseSnapshot[] savedPose = new PoseSnapshot[0];

        /// <summary>true nếu đã có pose được lưu (bởi <see cref="SavePose"/>).</summary>
        public bool HasSavedPose => savedPose != null && savedPose.Length > 0;

        [Tooltip(
            "Danh sách các điểm đích cần hướng tới. Bạn có thể thêm nhiều điểm và thay đổi Weight để blend (hòa trộn) mượt mà giữa các vị trí/góc xoay.")]
        public List<IKTargetData> targets = new List<IKTargetData>();

        [Tooltip(
            "Danh sách các Pole Target định hướng bẻ cong khớp phụ (Khuỷu tay / Đầu gối). Hệ thống sẽ hòa trộn hướng bẻ cong dựa trên tỷ lệ Weight của từng Pole Target.")]
        public List<IKPoleTargetData> poleTargets = new List<IKPoleTargetData>();

        [Tooltip(
            "Offset góc (độ) cộng thêm vào hướng bẻ cong tại từng khớp giữa của chuỗi (Element 0 = khớp giữa đầu tiên tính từ Gốc, ví dụ Khuỷu Tay/Đầu Gối). " +
            "Tương đương tham số 'Pole Angle' trong Blender IK — dùng để tinh chỉnh hướng bẻ cong (ví dụ khuỷu tay hơi chếch ra ngoài/vào trong) mà KHÔNG cần di chuyển Pole Target. " +
            "Mảng này tự động khớp số lượng với số khớp giữa trong chuỗi Bones, không cần chỉnh tay kích thước.")]
        public float[] poleAngleOffsets = new float[0];

        [Tooltip(
            "Danh sách các khớp xương xếp theo thứ tự liên tục từ GỐC đến NGỌN.\n• Phần tử [0] phải là xương gốc (Ví dụ: Vai/UpperArm).\n• Phần tử CUỐI CÙNG phải là xương ngọn (Ví dụ: Cổ tay/Hand) - khớp sẽ bám khít và copy rotation từ Target.")]
        public Transform[] bones;

        [Range(0f, 1f)]
        [Tooltip(
            "Mức độ ảnh hưởng tổng thể của giải thuật IK:\n• 1 = Ăn hoàn toàn theo vị trí/góc xoay của Target.\n• 0 = Trả về trạng thái hoạt cảnh (Animation) gốc của nhân vật.\n• 0.5 = Hòa trộn chính giữa góc của Animation và IK.")]
        public float ikInfluence = 1f;

        [Range(1, 50)]
        [Tooltip(
            "Số lần lặp tối đa của giải thuật FABRIK trong một Frame:\n• Từ 5 - 15: Phù hợp cho thiết bị di động (Mobile) / Game Runtime số lượng lớn (Tối ưu hiệu năng).\n• Từ 15 - 30: Khớp bám cực kỳ chính xác (Phù hợp PC/Console hoặc các phân cảnh Cutscene cần độ tỉ mỉ cao).")]
        public int iterations = 10;

        [Tooltip(
            "Độ sai số khoảng cách chấp nhận được giữa Xương Ngọn và Target (tính bằng mét).\n• Khuyên dùng từ 0.001 (1mm) đến 0.01 (1cm).\n• Nếu khoảng cách nhỏ hơn mức này, thuật toán sẽ tự động dừng lặp sớm để TIẾT KIỆM HIỆU NĂNG CPU.")]
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
        public Color elbowGizmoColor = Color.green;

        // ── Cache tính toán nội bộ ────────────────────────────────────────────────────
        private float[] bonesLength;
        private Vector3[] positions;
        private Quaternion[] startRotationBone;
        private Vector3[] startDirectionSucceded;
        private bool isInitialized;
        private float totalChainLength;

        // Hướng của từng bone (bone[i] -> bone[i+1]) TRƯỚC khi FABRIK chỉnh trong frame hiện tại.
        // Dùng để tính swing-only rotation (Quaternion.FromToRotation), tránh xoay twist ngoài ý muốn
        // — khác với cách dùng Quaternion.LookRotation() vốn tự suy ra "up" từ Vector3.up của world
        // nên phần twist bị tính lại không liên tục mỗi frame, gây vặn xoắn xương khi target di chuyển.
        private Vector3[] preSolveDirections;

        // Snapshot world-rotation của từng bone TRƯỚC khi vòng lặp Apply (bước 5) bắt đầu ghi đè.
        // BẮT BUỘC phải chụp trước, không được đọc "sống" bone.rotation ngay trong loop apply:
        // vì bone[i+1] là CON của bone[i] trong hierarchy, khi ta SetPositionAndRotation cho bone[i]
        // (cha) thì world-rotation của bone[i+1] (con) TỰ ĐỘNG đổi theo ngay lập tức dù chưa xử lý tới.
        // Nếu đọc bone.rotation sống lúc này để tính swing, giá trị đã bị "nhiễm" bởi phép xoay của
        // cha vừa set xong -> lỗi cộng dồn qua từng frame -> xương con xoay tròn không dừng.
        private Quaternion[] preSolveRotations;

        // ── Runtime switching ─────────────────────────────────────────────────────────
        private Coroutine _switchTargetCoroutine;
        private Coroutine _switchPoleTargetCoroutine;
        private Coroutine _influenceCoroutine;
        private float[] _cachedStartWeights = new float[0];
        private float[] _cachedPoleStartWeights = new float[0];

        protected override void OnEnable()
        {
            base.OnEnable(); // đăng ký với ConstraintScheduler (kể cả Edit Mode nhờ [ExecuteAlways])
            InitIKChain();
        }

        // [Aemy 2026-10-26] Gia tri dich cua cac transition dang chay.
        private float _influenceGoal = -1f;
        private string _switchTargetId;
        private string _switchPoleTargetId;

        protected override void OnDisable()
        {
            // Coroutine tu dung khi GameObject bi tat -> ikInfluence / weight ket o giua (vd 0.4) mai mai.
            // Chot ve gia tri dich truoc khi tat.
            FinishTransitions();
            base.OnDisable();
        }

        private void FinishTransitions()
        {
            if (_influenceCoroutine != null)
            {
                StopCoroutine(_influenceCoroutine);
                _influenceCoroutine = null;
                if (_influenceGoal >= 0f) ikInfluence = _influenceGoal;
            }
            if (_switchTargetCoroutine != null)
            {
                StopCoroutine(_switchTargetCoroutine);
                _switchTargetCoroutine = null;
                SnapTargetWeights(_switchTargetId);
            }
            if (_switchPoleTargetCoroutine != null)
            {
                StopCoroutine(_switchPoleTargetCoroutine);
                _switchPoleTargetCoroutine = null;
                SnapPoleWeights(_switchPoleTargetId);
            }
        }

        private void SnapTargetWeights(string id)
        {
            if (string.IsNullOrEmpty(id) || !targets.Exists(t => t.id == id)) return;
            for (int i = 0; i < targets.Count; i++)
                targets[i].weight = targets[i].id == id ? 1f : 0f;
        }

        private void SnapPoleWeights(string id)
        {
            if (string.IsNullOrEmpty(id) || !poleTargets.Exists(p => p.id == id)) return;
            for (int i = 0; i < poleTargets.Count; i++)
                poleTargets[i].weight = poleTargets[i].id == id ? 1f : 0f;
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
                id = id,
                target = targetTransform,
                weight = Mathf.Clamp01(initialWeight)
            });
            ConstraintScheduler.MarkDirty();
            return true;
        }

        public bool RemoveTarget(string id)
        {
            int count = targets.Count;
            for (int i = 0; i < count; i++)
            {
                if (targets[i].id == id)
                {
                    // Dừng coroutine chuyển Target đang chạy, tránh nó duyệt list đã bị thu nhỏ / lệch index
                    bool wasSwitching = _switchTargetCoroutine != null;
                    if (wasSwitching)
                    {
                        StopCoroutine(_switchTargetCoroutine);
                        _switchTargetCoroutine = null;
                    }

                    targets.RemoveAt(i);
                    // [Aemy 2026-10-26] Chot weight ve dich thay vi de ket nua chung.
                    if (wasSwitching) SnapTargetWeights(_switchTargetId);
                    ConstraintScheduler.MarkDirty();
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

        /// <summary>
        /// Bật IK bằng cách tăng <see cref="ikInfluence"/> từ giá trị hiện tại lên 1
        /// trong khoảng thời gian <paramref name="duration"/> giây.
        /// Nếu đang có transition thì huỷ và bắt đầu transition mới từ giá trị hiện tại.
        /// </summary>
        /// <param name="duration">Thời gian (giây) để influence chạy từ hiện tại → 1. Truyền 0 để set ngay lập tức.</param>
        public void TurnOn(float duration = 0.25f)
        {
            if (!Application.isPlaying)
            {
                ikInfluence = 1f;
                return;
            }

            if (_influenceCoroutine != null)
                StopCoroutine(_influenceCoroutine);
            _influenceGoal = 1f;
            if (!isActiveAndEnabled) { ikInfluence = 1f; _influenceCoroutine = null; return; } // khong chay coroutine duoc
            _influenceCoroutine = StartCoroutine(InfluenceRoutine(1f, duration));
        }

        /// <summary>
        /// Tắt IK bằng cách giảm <see cref="ikInfluence"/> từ giá trị hiện tại xuống 0
        /// trong khoảng thời gian <paramref name="duration"/> giây.
        /// Nếu đang có transition thì huỷ và bắt đầu transition mới từ giá trị hiện tại.
        /// </summary>
        /// <param name="duration">Thời gian (giây) để influence chạy từ hiện tại → 0. Truyền 0 để set ngay lập tức.</param>
        public void TurnOff(float duration = 0.25f)
        {
            if (!Application.isPlaying)
            {
                ikInfluence = 0f;
                return;
            }

            if (_influenceCoroutine != null)
                StopCoroutine(_influenceCoroutine);
            _influenceGoal = 0f;
            if (!isActiveAndEnabled) { ikInfluence = 0f; _influenceCoroutine = null; return; } // khong chay coroutine duoc
            _influenceCoroutine = StartCoroutine(InfluenceRoutine(0f, duration));
        }

        private IEnumerator InfluenceRoutine(float target, float duration)
        {
            float startValue = ikInfluence;
            float elapsed = 0f;
            float invDuration = duration > 0f ? 1f / duration : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ikInfluence = Mathf.Lerp(startValue, target, elapsed * invDuration);
                yield return null;
            }

            ikInfluence = target;
            _influenceCoroutine = null;
        }

        public void SwitchTarget(string targetId, float duration = 0.25f)
        {
            bool found = false;
            int count = targets.Count;
            for (int i = 0; i < count; i++)
                if (targets[i].id == targetId)
                {
                    found = true;
                    break;
                }

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
            _switchTargetId = targetId;
            if (!isActiveAndEnabled) { _switchTargetCoroutine = null; SnapTargetWeights(targetId); return; }
            _switchTargetCoroutine = StartCoroutine(SwitchTargetRoutine(targetId, duration));
        }

        private IEnumerator SwitchTargetRoutine(string targetId, float duration)
        {
            float elapsed = 0f;
            int count = targets.Count;

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

            _switchTargetCoroutine = null;
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
                id = id,
                poleTarget = poleTransform,
                weight = Mathf.Clamp01(initialWeight)
            });
            ConstraintScheduler.MarkDirty();
            return true;
        }

        public bool RemovePoleTarget(string id)
        {
            int count = poleTargets.Count;
            for (int i = 0; i < count; i++)
            {
                if (poleTargets[i].id == id)
                {
                    // Dừng coroutine chuyển Pole Target đang chạy, tránh nó duyệt list đã bị thu nhỏ / lệch index
                    bool wasSwitching = _switchPoleTargetCoroutine != null;
                    if (wasSwitching)
                    {
                        StopCoroutine(_switchPoleTargetCoroutine);
                        _switchPoleTargetCoroutine = null;
                    }

                    poleTargets.RemoveAt(i);
                    // [Aemy 2026-10-26] Chot weight ve dich thay vi de ket nua chung.
                    if (wasSwitching) SnapPoleWeights(_switchPoleTargetId);
                    ConstraintScheduler.MarkDirty();
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

        public void SwitchPoleTarget(string poleTargetId, float duration = 0.25f)
        {
            bool found = false;
            int count = poleTargets.Count;
            for (int i = 0; i < count; i++)
                if (poleTargets[i].id == poleTargetId)
                {
                    found = true;
                    break;
                }

            if (!found)
            {
                Debug.LogWarning(
                    $"[IKConstraint] SwitchPoleTarget: Không tìm thấy Pole Target có ID '{poleTargetId}'!");
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
            _switchPoleTargetId = poleTargetId;
            if (!isActiveAndEnabled) { _switchPoleTargetCoroutine = null; SnapPoleWeights(poleTargetId); return; }
            _switchPoleTargetCoroutine = StartCoroutine(SwitchPoleTargetRoutine(poleTargetId, duration));
        }

        private IEnumerator SwitchPoleTargetRoutine(string poleTargetId, float duration)
        {
            float elapsed = 0f;
            int count = poleTargets.Count;

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

            _switchPoleTargetCoroutine = null;
        }

        // ══════════════════════════════ PUBLIC API — PAIRED ══════════════════════════════

        public bool AddPair(string id, Transform targetTransform, Transform poleTransform, float initialWeight = 0f)
        {
            bool addedTarget = AddTarget(id, targetTransform, initialWeight);
            bool addedPole = AddPoleTarget(id, poleTransform, initialWeight);
            return addedTarget && addedPole;
        }

        public bool RemovePair(string id)
        {
            bool removedTarget = RemoveTarget(id);
            bool removedPole = RemovePoleTarget(id);
            return removedTarget && removedPole;
        }

        public bool SetPairWeight(string id, float weight)
        {
            bool setTarget = SetTargetWeight(id, weight);
            bool setPole = SetPoleTargetWeight(id, weight);
            return setTarget && setPole;
        }

        public void SwitchPair(string id, float duration = 0.25f)
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

            positions = new Vector3[bones.Length];
            bonesLength = new float[bones.Length - 1];
            startRotationBone = new Quaternion[bones.Length];
            startDirectionSucceded = new Vector3[bones.Length - 1];
            preSolveDirections = new Vector3[bones.Length - 1];
            preSolveRotations = new Quaternion[bones.Length];

            totalChainLength = 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    isInitialized = false;
                    return;
                }

                startRotationBone[i] = bones[i].rotation;
                if (i < bones.Length - 1)
                {
                    if (bones[i + 1] == null)
                    {
                        isInitialized = false;
                        return;
                    }

                    Vector3 dir = bones[i + 1].position - bones[i].position;
                    bonesLength[i] = dir.magnitude;
                    startDirectionSucceded[i] = dir;
                    totalChainLength += bonesLength[i];
                }
            }

            isInitialized = true;
            ConstraintScheduler.MarkDirty(); // bones có thể vừa đổi → sắp xếp lại phụ thuộc (chỉ khi init thành công, không lặp mỗi frame)

            // Tự đồng bộ kích thước mảng poleAngleOffsets với số khớp giữa hiện có trong chuỗi
            // (positions.Length - 2, vì khớp Gốc và khớp Ngọn không có bẻ cong bằng Pole Target).
            // Dùng Array.Resize để GIỮ NGUYÊN các giá trị offset người dùng đã chỉnh trước đó
            // khi số lượng xương không đổi; chỉ khi thêm/bớt xương mới cắt/nới mảng.
            int jointCount = Mathf.Max(0, bones.Length - 2);
            if (poleAngleOffsets == null)
                poleAngleOffsets = new float[jointCount];
            else if (poleAngleOffsets.Length != jointCount)
                System.Array.Resize(ref poleAngleOffsets, jointCount);
        }

        // ══════════════════════════════ SCHEDULER ══════════════════════════════
        // Không còn LateUpdate() riêng: ConstraintScheduler gọi Solve() — mặc định IK chạy SAU
        // DampedTrack/TransformConstraint (TypeOrderIK) và sau mọi constraint ghi lên target/pole/cha chuỗi xương.

        public override int DefaultTypeOrder => TypeOrderIK;

        public override void Solve()
        {
            // Kiểm tra điều kiện Preview ở Edit Mode (Khi chưa bấm Play)
            if (!Application.isPlaying && !preview) return;

            ApplyIK();
        }

        public override void CollectDependencies(List<Transform> inputs, List<Transform> outputs)
        {
            if (bones != null && bones.Length > 0)
            {
                for (int i = 0; i < bones.Length; i++) outputs.Add(bones[i]);
                if (bones[0] != null) inputs.Add(bones[0].parent); // world pose gốc chuỗi phụ thuộc cha
            }
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) inputs.Add(targets[i].target);
            for (int i = 0; i < poleTargets.Count; i++)
                if (poleTargets[i] != null) inputs.Add(poleTargets[i].poleTarget);
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
            float totalWeight = 0f;
            Vector3 weightedTargetPos = Vector3.zero;

            foreach (var t in targets)
            {
                if (t.target == null || t.weight <= 0f) continue;
                totalWeight += t.weight;
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

            // 2b. Lưu hướng + rotation từng bone TRƯỚC khi FABRIK/loop apply chỉnh sửa (chính là trạng thái
            // theo animation/pose hiện tại). Đây là điểm mốc "zero-twist" đúng của frame này — vì nó đã bao
            // gồm animation + twist tích lũy từ các frame trước, không phải world-up như Quaternion.LookRotation(),
            // và KHÔNG bị nhiễm bởi việc set rotation của bone cha xảy ra sau đó trong cùng frame.
            for (int i = 0; i < positions.Length - 1; i++)
            {
                preSolveDirections[i] = (positions[i + 1] - positions[i]).normalized;
                preSolveRotations[i] = bones[i].rotation;
            }

            // 3. Giải IK bằng FABRIK
            IKMath.SolveFABRIK(positions, bonesLength, blendedTargetPos, rootPos, iterations, delta, totalChainLength);

            // 4. Xử lý Pole Target
            float totalPoleWeight = 0f;
            Vector3 weightedPolePos = Vector3.zero;
            bool hasPole = false;

            foreach (var p in poleTargets)
            {
                if (p.poleTarget == null || p.weight <= 0f) continue;
                totalPoleWeight += p.weight;
                weightedPolePos += p.poleTarget.position * p.weight;
                hasPole = true;
            }

            if (hasPole && totalPoleWeight > 0f)
            {
                Vector3 blendedPolePos = weightedPolePos / totalPoleWeight;

                for (int i = 1; i < positions.Length - 1; i++)
                {
                    Plane plane = new Plane(positions[i + 1] - positions[i - 1], positions[i - 1]);
                    Vector3 projectedPole = plane.ClosestPointOnPlane(blendedPolePos);
                    Vector3 projectedBone = plane.ClosestPointOnPlane(positions[i]);
                    float angle = Vector3.SignedAngle(
                        projectedBone - positions[i - 1],
                        projectedPole - positions[i - 1],
                        plane.normal);

                    // Cộng thêm offset tùy chỉnh (giống "Pole Angle" của Blender) cho riêng khớp giữa này.
                    // Element 0 ứng với khớp giữa đầu tiên (i = 1), Element 1 ứng với khớp giữa thứ hai (i = 2), v.v.
                    int offsetIndex = i - 1;
                    if (poleAngleOffsets != null && offsetIndex < poleAngleOffsets.Length)
                        angle += poleAngleOffsets[offsetIndex];

                    positions[i] = Quaternion.AngleAxis(angle, plane.normal)
                        * (positions[i] - positions[i - 1]) + positions[i - 1];
                }
            }

            // 5. Apply Rotation/Position lên các xương
            float finalInfluence = Mathf.Clamp01(totalWeight * ikInfluence);
            bool syncPosition = finalInfluence >= 0.99f;

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                Quaternion newRot = bone.rotation;

                // Xương thân (Gốc → Áp út): xoay về phía khớp tiếp theo
                if (i < bones.Length - 1)
                {
                    Vector3 targetDir = (positions[i + 1] - positions[i]).normalized;

                    // Swing-only: chỉ xoay tối thiểu từ hướng TRƯỚC KHI FABRIK chỉnh (preSolveDirections[i])
                    // sang hướng SAU KHI FABRIK chỉnh (targetDir), rồi nhân vào rotation ĐÃ CHỤP SNAPSHOT
                    // (preSolveRotations[i]) — KHÔNG dùng bone.rotation đọc sống, vì tại thời điểm này
                    // bone cha (i-1) có thể đã được SetPositionAndRotation ở vòng lặp trước, khiến
                    // world-rotation của bone[i] (con) tự đổi theo qua hierarchy -> nếu đọc sống sẽ
                    // nhân chồng thêm phép xoay của cha vào mỗi frame -> xoay tròn không dừng.
                    Quaternion swing = Quaternion.FromToRotation(preSolveDirections[i], targetDir);
                    Quaternion targetRot = swing * preSolveRotations[i];
                    newRot = Quaternion.Lerp(preSolveRotations[i], targetRot, finalInfluence);
                }
                // Xương NGỌN: copy rotation từ Target
                else
                {
                    Quaternion blendedTargetRot = Quaternion.identity;
                    bool firstValidRot = true;
                    float currentSubWeight = 0f;

                    foreach (var t in targets)
                    {
                        if (t.target == null || t.weight <= 0f) continue;

                        if (firstValidRot)
                        {
                            blendedTargetRot = t.target.rotation;
                            currentSubWeight = t.weight;
                            firstValidRot = false;
                        }
                        else
                        {
                            currentSubWeight += t.weight;
                            blendedTargetRot = Quaternion.Slerp(
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

        protected override void OnValidate()
        {
            base.OnValidate(); // báo scheduler sắp xếp lại
            InitIKChain();
        }

        // ══════════════════════════════ POSE SNAPSHOT / RESET ══════════════════════════════

        /// <summary>
        /// Gom các Transform cần lưu/reset: bones + Target + Pole Target (bỏ null, loại trùng, giữ thứ tự).
        /// </summary>
        private List<Transform> CollectPoseTransforms()
        {
            var result = new List<Transform>();
            var seen = new HashSet<Transform>();

            if (bones != null)
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != null && seen.Add(bones[i])) result.Add(bones[i]);

            if (targets != null)
                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] != null && targets[i].target != null && seen.Add(targets[i].target))
                        result.Add(targets[i].target);

            if (poleTargets != null)
                for (int i = 0; i < poleTargets.Count; i++)
                    if (poleTargets[i] != null && poleTargets[i].poleTarget != null && seen.Add(poleTargets[i].poleTarget))
                        result.Add(poleTargets[i].poleTarget);

            return result;
        }

        /// <summary>
        /// Lưu local position/rotation/scale của bones trong chain + tất cả Target + Pole Target.
        /// Ghi đè pose đã lưu trước đó. Editor tự gọi khi bấm Preview In Editor từ INACTIVE sang ACTIVE.
        /// </summary>
        public void SavePose()
        {
            List<Transform> list = CollectPoseTransforms();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObject(this, "Save IK Pose");
#endif

            var snapshot = new PoseSnapshot[list.Count];
            for (int i = 0; i < snapshot.Length; i++)
            {
                Transform t = list[i];
                snapshot[i].transform = t;
                snapshot[i].localPosition = t.localPosition;
                snapshot[i].localRotation = t.localRotation;
                snapshot[i].localScale = t.localScale;
            }

            savedPose = snapshot;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        /// <summary>
        /// Trả bones, Target và Pole Target về pose đã lưu bởi <see cref="SavePose"/>.
        /// Ở Edit Mode có hỗ trợ Undo.
        /// </summary>
        /// <returns>false nếu chưa có pose nào được lưu.</returns>
        public bool ResetToSavedPose()
        {
            if (!HasSavedPose) return false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var objs = new List<Object>(savedPose.Length);
                for (int i = 0; i < savedPose.Length; i++)
                    if (savedPose[i].transform != null) objs.Add(savedPose[i].transform);
                UnityEditor.Undo.RecordObjects(objs.ToArray(), "Reset IK Pose");
            }
#endif

            // Chỉ ghi local transform nên thứ tự không quan trọng (kể cả khi Target là cha của bone).
            for (int i = 0; i < savedPose.Length; i++)
            {
                PoseSnapshot snap = savedPose[i];
                if (snap.transform == null) continue;

                snap.transform.localPosition = snap.localPosition;
                snap.transform.localRotation = snap.localRotation;
                snap.transform.localScale = snap.localScale;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(snap.transform);
#endif
            }

            return true;
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
            float half = baseSize * 0.5f;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;

            Vector3 apex = basePosition + up * height;
            Vector3 p1 = basePosition + right * half + forward * half;
            Vector3 p2 = basePosition + right * half - forward * half;
            Vector3 p3 = basePosition - right * half - forward * half;
            Vector3 p4 = basePosition - right * half + forward * half;

            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);

            Gizmos.DrawLine(p1, apex);
            Gizmos.DrawLine(p2, apex);
            Gizmos.DrawLine(p3, apex);
            Gizmos.DrawLine(p4, apex);
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
                DrawCircleGizmo(center, p.poleTarget.right, elbowGizmoRadius);
                DrawCircleGizmo(center, p.poleTarget.up, elbowGizmoRadius);
            }
        }

        private void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments = 24)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            Vector3 prevPoint = center + tangent * radius;
            float angleStep = (Mathf.PI * 2f) / segments;

            for (int s = 1; s <= segments; s++)
            {
                float angle = s * angleStep;
                Vector3 nextPoint = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }
    }
}