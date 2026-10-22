using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Damped Track cho CẢ CHUỖI xương (VD: Spine → Chest → UpperChest → Neck → Head).
    ///
    /// • Danh sách bones có thể nhập theo thứ tự bất kỳ → tự sắp xếp theo độ sâu hierarchy:
    ///   [0] = xương gần root nhất (dưới cùng, VD hông), [cuối] = xương sâu nhất (đầu) — xương này là
    ///   xương AIM: trục Track Axis của nó sẽ hướng về Target.
    /// • Đầu xoay trước tới giới hạn của đầu; phần góc còn thiếu (overflow) chuyển xuống cổ, tới giới
    ///   hạn của cổ thì chuyển xuống upper chest, ... cho tới xương dưới cùng.
    /// • Mọi xương cùng xoay quanh MỘT trục swing chung (trục của swing nhỏ nhất đầu → target) nên
    ///   góc của các xương cộng lại chính xác bằng góc cần thiết, không dư/thiếu, không gimbal.
    /// • Vượt hết ngưỡng của cả chuỗi: Clamp (dừng ở tư thế xa nhất có thể) hoặc ReleaseToBaseline
    ///   (chững lại rồi nhả về pose gốc — giống DampedTrackConstraint 1 xương).
    ///
    /// • Normalize Weights: chuẩn hóa Weight của các Target (xem field normalizeWeights).
    /// • Check Obstacles: raycast từ xương AIM (đầu) tới từng Target; Target bị che không kéo hướng nhìn,
    ///   nếu TẤT CẢ Target đều bị che thì cả chuỗi chững lại rồi nhả về pose gốc.
    /// • Auto Tracking: tự chọn Target gần nhất mà cả chuỗi với tới được (và không bị che).
    ///
    /// Dùng lại <see cref="DampedTrackMath"/> và <see cref="DampedTrackConstraint.TargetData"/>.
    /// </summary>
    [AddComponentMenu("Gà Mờ/Damped Track Chain Constraint")]
    public class DampedTrackChainConstraint : ConstraintBehaviour
    {
        public enum OverflowMode { Clamp, ReleaseToBaseline }
        public enum DistributionMode { HeadFirstOverflow, Weighted }

        [System.Serializable]
        public class BoneData
        {
            [Tooltip("Xương thuộc chuỗi. Thứ tự trong list không quan trọng, tool tự sắp xếp theo hierarchy.")]
            public Transform bone;

            [Range(0f, 1f)]
            [Tooltip("Độ ảnh hưởng của xương này (chỉ dùng khi Distribution = Weighted). VD: đầu 1, cổ 0.6, hông 0.1. " +
                     "Góc cần xoay chia theo tỉ lệ weight; xương chạm giới hạn thì phần dư chia cho các xương còn lại. 0 = không tham gia.")]
            public float weight = 1f;

            [Range(0f, 180f)] [Tooltip("Góc tối đa xương NÀY đóng góp khi quay sang TRÁI (độ).")]
            public float maxYawLeft = 40f;

            [Range(0f, 180f)] [Tooltip("Góc tối đa xương NÀY đóng góp khi quay sang PHẢI (độ).")]
            public float maxYawRight = 40f;

            [Range(0f, 90f)] [Tooltip("Góc tối đa xương NÀY đóng góp khi ngẩng LÊN (độ).")]
            public float maxPitchUp = 25f;

            [Range(0f, 90f)] [Tooltip("Góc tối đa xương NÀY đóng góp khi cúi XUỐNG (độ).")]
            public float maxPitchDown = 20f;
        }

        [Tooltip("Các điểm đích. Hướng nhìn = trung bình có trọng số (Weight) của các Target.")]
        public List<DampedTrackConstraint.TargetData> targets = new List<DampedTrackConstraint.TargetData>();

        [Tooltip(
            "✔ TRUE (khuyên dùng): Chuẩn hóa Weight của các Target — Weight chỉ quyết định TỈ LỆ blend giữa các Target, " +
            "còn mức ảnh hưởng cuối cùng luôn đúng bằng Influence (VD: A=0.3, B=0.3 → blend 50/50, influence vẫn đầy đủ).\n" +
            "❌ FALSE (hành vi cũ): tổng Weight nhân thẳng vào influence cuối cùng — hữu ích khi muốn Influence tự " +
            "giảm theo tổng Weight (VD: fade-in một Target bằng cách tăng Weight từ 0 lên 1).")]
        public bool normalizeWeights = true;

        [Tooltip(
            "Các xương trong chuỗi + giới hạn góc RIÊNG của từng xương. Thứ tự nhập tuỳ ý — tool tự " +
            "sắp xếp: xương gần root nhất = dưới cùng, xương sâu nhất = xương AIM (đầu).\n" +
            "Các xương phải nằm trên cùng 1 nhánh cha-con (có thể bỏ qua xương trung gian).")]
        public List<BoneData> bones = new List<BoneData>();

        [Tooltip("Trục cục bộ của XƯƠNG CUỐI CHUỖI (đầu) sẽ hướng về Target. Xem DampedTrackConstraint.")]
        public DampedTrackConstraint.TrackAxis trackAxis = DampedTrackConstraint.TrackAxis.Z;

        [Tooltip("Trục cục bộ của xương cuối chuỗi (đầu) được coi là 'hướng lên' (trục yaw). " +
                 "Không được trùng Track Axis. Dùng chung để đo yaw/pitch cho mọi xương trong chuỗi.")]
        public DampedTrackConstraint.TrackAxis limitUpAxis = DampedTrackConstraint.TrackAxis.Y;

        [Tooltip("✔ TRUE: các xương có Animator/IK ghi đè Rotation mỗi frame. ❌ FALSE: dùng Rest Pose chụp 1 lần.")]
        public bool hasExternalPoseDriver = true;

        [Range(0f, 1f)] [Tooltip("Mức ảnh hưởng tổng thể (0 = pose gốc, 1 = bám Target).")]
        public float influence = 1f;

        [Range(0.5f, MaxFollowSpeed)]
        [Tooltip("Tốc độ bám Target (làm mượt). 30 = tức thì. Thấp hơn = trễ/mượt hơn.")]
        public float followSpeed = MaxFollowSpeed;

        [Tooltip(
            "• HeadFirstOverflow: đầu nhận hết tới giới hạn, phần dư mới chuyển xuống cổ, ngực... (bỏ qua Weight).\n" +
            "• Weighted: chia góc cho mọi xương theo Weight của từng xương (đầu nhiều nhất, hông ít nhất); " +
            "xương chạm giới hạn thì phần dư chia tiếp cho các xương còn lại.")]
        public DistributionMode distribution = DistributionMode.Weighted;

        [Tooltip(
            "Khi Target vượt quá TỔNG giới hạn của cả chuỗi:\n" +
            "• Clamp: mỗi xương dừng ở giới hạn của mình, đầu nhìn tới hướng xa nhất có thể.\n" +
            "• ReleaseToBaseline: chững lại (nếu bật Hold) rồi nhả cả chuỗi về pose gốc.")]
        public OverflowMode overflowMode = OverflowMode.ReleaseToBaseline;

        [Range(0f, 30f)] [Tooltip("Vùng đệm (độ) chống rung ở biên: sau khi vượt ngưỡng, phải vào trong thêm chừng này mới bám lại.")]
        public float limitMargin = 5f;

        [Min(0.01f)] [Tooltip("Thời gian (giây) nhả về pose gốc / bám lại (chỉ dùng cho ReleaseToBaseline).")]
        public float returnDuration = 0.6f;

        [Tooltip("✔ TRUE: khi vượt ngưỡng, giữ nguyên tư thế Hold Duration giây rồi mới nhả về gốc.")]
        public bool useHold = false;

        [Min(0f)] public float holdDuration = 0.5f;
        
        [Tooltip(
            "✔ TRUE: Bắn raycast từ xương AIM (đầu) tới từng Target. Nếu có vật cản (collider) chắn giữa " +
            "→ Target đó bị coi là KHÔNG nhìn thấy (không kéo hướng nhìn). Khi TẤT CẢ Target bị che, cả chuỗi " +
            "chững lại (nếu bật Occlusion Hold) rồi nhả về pose gốc.\n" +
            "Chỉ hỗ trợ Physics 3D (Collider, không phải Collider2D).")]
        public bool checkObstacles = false;

        [Tooltip(
            "Các Layer được coi là vật cản. Nên LOẠI layer của chính nhân vật và layer của Target " +
            "(hoặc để Ignore Own Hierarchy bật). Trigger Collider luôn bị bỏ qua.")]
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [Tooltip(
            "✔ TRUE (khuyên dùng): Bỏ qua mọi collider thuộc hierarchy của nhân vật (root của xương đầu) " +
            "→ collider thân/đầu của chính nhân vật không bị tính là vật cản.\n" +
            "Collider nằm trong chính Target luôn được bỏ qua bất kể tuỳ chọn này.")]
        public bool ignoreOwnHierarchy = true;

        [Min(0.01f)]
        [Tooltip("Thời gian (giây) để Target chuyển hẳn từ 'thấy' sang 'bị che' (nhả về pose gốc) và ngược lại. " +
                 "Giá trị lớn giúp tránh giật khi vật cản mỏng lướt qua tầm nhìn.")]
        public float occlusionBlendTime = 0.4f;

        [Tooltip(
            "✔ TRUE: Khi TẤT CẢ Target đang có Weight bị vật cản che, cả chuỗi GIỮ NGUYÊN tư thế (chững lại) " +
            "trong Occlusion Hold Duration giây rồi mới bắt đầu từ từ xoay về pose gốc.")]
        public bool useOcclusionHold = false;

        [Min(0f)]
        [Tooltip("Thời gian (giây) chuỗi chững lại tại chỗ sau khi bị che, trước khi xoay về pose gốc.")]
        public float occlusionHoldDuration = 0.5f;

        [Min(0f)]
        [Tooltip("Khoảng cách tối thiểu (giây) giữa 2 lần raycast kiểm tra vật cản của MỖI Target. 0 = raycast mỗi frame. " +
                 "Độ 'nhìn thấy' vẫn được làm mượt mỗi frame nên giá trị nhỏ (0.03–0.1) gần như không khác biệt " +
                 "nhưng giảm đáng kể chi phí Physics.")]
        public float obstacleCheckInterval = 0.05f;
        
        [Tooltip(
            "✔ TRUE: Tự động chọn Target để bám theo. CHỈ hoạt động khi có từ 2 Target hợp lệ trở lên " +
            "(Transform khác null và Weight > 0); ít hơn thì chạy bình thường (blend theo Weight).\n" +
            "Ưu tiên: Target GẦN NHẤT trong số các Target mà CẢ CHUỖI xương với tới được (không vượt tổng giới hạn) " +
            "và không bị vật cản che (nếu bật Check Obstacles).\n" +
            "• Không còn Target hợp lệ nào (ngoài tầm với hoặc bị che): không bám Target nào, chững lại (nếu bật Hold) rồi nhả về pose gốc.\n" +
            "• Weight vẫn là mức ảnh hưởng riêng của Target; Weight = 0 loại Target khỏi Auto Tracking.")]
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
                 "hợp lệ (ngoài tầm với của chuỗi / bị che) thì đổi ngay, không chờ.")]
        public float autoMinSwitchInterval = 0.3f;
        
        public bool showGizmos = false;
        public Color gizmoChainColor = Color.cyan;
        public Color gizmoTargetColor = new Color(1f, 1f, 0f, 0.6f);
        [Tooltip("Màu đường nối tới Target đang BỊ VẬT CẢN CHE (chỉ thấy khi Play và bật Check Obstacles).")]
        public Color gizmoBlockedColor = new Color(1f, 0.3f, 0.2f, 0.8f);
        [Tooltip("Màu đường nối tới Target đang được Auto Tracking chọn (chỉ thấy khi Play).")]
        public Color gizmoSelectedColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        private const float MaxFollowSpeed = 30f;

        // ── Chuỗi đã sắp xếp (gốc → đầu) ──
        private readonly List<BoneData> _sorted = new List<BoneData>();
        private readonly Dictionary<Transform, Quaternion> _restMap = new Dictionary<Transform, Quaternion>();
        private bool _chainValid;
        private bool _restReady;

        private Quaternion[] _baseLocal, _restLocal, _lastOut, _lastBase;
        private Quaternion[] _liveQ, _aimQ, _smoothQ;
        private float[] _alloc, _cap;
        private bool[] _sat;
        private bool _hasLastOutput;

        // ── Trạng thái gate (giống EvaluateGate 1 xương, nhưng cho cả chuỗi) ──
        private bool _aimValid, _wasBlocked, _reacquiring, _smoothValid, _outOfRange;
        private float _gate = 1f, _holdTimer;
        private float _gateFade = 0.5f; // thời gian nhả/bám lại của lần chặn gần nhất (returnDuration hoặc occlusionBlendTime)

        // ── Check Obstacles ──
        private const float ObstacleEndPadding = 0.05f; // dừng ray sát Target, tránh dính mặt đất/bề mặt dưới chân Target
        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

        // ── Auto Tracking ──
        // Target Auto Tracking đang KHÔNG tham gia blend chỉ cần raycast thưa (phục vụ việc chọn Target).
        private const float AutoProbeInterval = 0.1f;
        private DampedTrackConstraint.TargetData _autoSelected;
        private bool _autoActive;
        private bool _autoNoValid; // Auto Tracking đang chạy nhưng KHÔNG còn Target hợp lệ nào (ngoài tầm với / bị che)
        private float _autoSwitchTimer;

        private Coroutine _influenceRoutine;

        // ── Cache hiệu năng (không cấp phát trong Solve) ──
        private readonly List<Transform> _sortedBones = new List<Transform>();
#if UNITY_EDITOR
        private bool _chainBuilt; // chỉ code Editor đọc (vẽ gizmo / preview) → bản build không cần
#endif
        private float _probePhase; // 0..1 ngẫu nhiên mỗi instance: lệch pha lịch raycast giữa các NPC
        private Vector3[] _tgtPos = new Vector3[8];                                   // vị trí Target, đọc 1 lần/frame
        private DampedTrackConstraint.TargetData[] _reachRef = new DampedTrackConstraint.TargetData[8];
        private bool[] _reachOk = new bool[8];                                        // cache IsTargetReachable cho Target ứng viên
        private float[] _reachNext = new float[8];

        private void EnsureTargetBuffers(int count)
        {
            if (_tgtPos.Length >= count) return;
            int size = Mathf.Max(count, _tgtPos.Length * 2);
            System.Array.Resize(ref _tgtPos, size);
            System.Array.Resize(ref _reachRef, size);
            System.Array.Resize(ref _reachOk, size);
            System.Array.Resize(ref _reachNext, size);
        }

        /// <summary>Các xương theo thứ tự đã tự sắp xếp: [0] = dưới cùng, [cuối] = đầu.</summary>
        public IReadOnlyList<Transform> SortedBones
        {
            get { return _sortedBones; }
        }

        /// <summary>true khi list bones hợp lệ (có xương, cùng 1 nhánh cha-con, không trùng).</summary>
        public bool IsChainValid => _chainValid;

        /// <summary>true khi Target nằm ngoài TỔNG giới hạn của cả chuỗi.</summary>
        public bool IsOutOfRange => _outOfRange;

        /// <summary>true khi Auto Tracking đang thực sự chạy (đã bật và có từ 2 Target hợp lệ).</summary>
        public bool IsAutoTrackingActive => autoTracking && _autoActive;

        /// <summary>ID của Target đang được Auto Tracking chọn (null nếu Auto Tracking không chạy).</summary>
        public string AutoTargetId => (IsAutoTrackingActive && _autoSelected != null) ? _autoSelected.id : null;

        // ─────────────────────────────────────────── Setup ───────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable(); // đăng ký với ConstraintScheduler
            RebuildChain();
            _probePhase = Random.value;
            _restReady = false;
            if (!hasExternalPoseDriver) StartCoroutine(CaptureRestPoseNextFrame());
            else _restReady = true;
        }

        protected override void OnValidate()
        {
            base.OnValidate(); // báo scheduler sắp xếp lại
            RebuildChain();
        }

        private IEnumerator CaptureRestPoseNextFrame()
        {
            yield return null; // chờ Animator/IK evaluate xong frame đầu
            CaptureRestPose();
            _restReady = true;
        }

        /// <summary>Chụp lại local rotation hiện tại của mọi xương làm Rest Pose (khi hasExternalPoseDriver = false).</summary>
        [ContextMenu("Capture Rest Pose Now")]
        public void CaptureRestPose()
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                Transform b = _sorted[i].bone;
                _restMap[b] = b.localRotation;
                if (_restLocal != null && i < _restLocal.Length) _restLocal[i] = b.localRotation;
            }
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { t = t.parent; d++; }
            return d;
        }

        /// <summary>Sắp xếp lại chuỗi theo hierarchy và kiểm tra hợp lệ. Gọi lại nếu đổi list bones lúc runtime.</summary>
        [ContextMenu("Rebuild Chain")]
        public void RebuildChain()
        {
            ConstraintScheduler.MarkDirty(); // bones đổi → sắp xếp lại phụ thuộc
            _sorted.Clear();
            for (int i = 0; i < bones.Count; i++)
                if (bones[i] != null && bones[i].bone != null) _sorted.Add(bones[i]);

            _sorted.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));

            _chainValid = _sorted.Count > 0;
            for (int i = 0; i + 1 < _sorted.Count && _chainValid; i++)
            {
                Transform parent = _sorted[i].bone, child = _sorted[i + 1].bone;
                if (parent == child || !child.IsChildOf(parent))
                {
                    _chainValid = false;
                    Debug.LogWarning($"[DampedTrackChain] '{name}': '{child.name}' không nằm dưới '{parent.name}' — " +
                                     "các xương phải cùng 1 nhánh cha-con, không trùng nhau.", this);
                }
            }

            int n = _sorted.Count;
            _sortedBones.Clear();
            for (int i = 0; i < n; i++) _sortedBones.Add(_sorted[i].bone);

            if (_baseLocal == null || _baseLocal.Length != n) // chỉ cấp phát lại khi số xương đổi (OnValidate gọi rất thường xuyên)
            {
                _baseLocal = new Quaternion[n]; _restLocal = new Quaternion[n];
                _lastOut = new Quaternion[n]; _lastBase = new Quaternion[n];
                _liveQ = new Quaternion[n]; _aimQ = new Quaternion[n]; _smoothQ = new Quaternion[n];
                _alloc = new float[n]; _cap = new float[n]; _sat = new bool[n];
            }
#if UNITY_EDITOR
            _chainBuilt = true;
#endif

            for (int i = 0; i < n; i++)
            {
                _liveQ[i] = _aimQ[i] = _smoothQ[i] = Quaternion.identity;
                Transform b = _sorted[i].bone;
                if (!_restMap.TryGetValue(b, out Quaternion rest)) { rest = b.localRotation; _restMap[b] = rest; }
                _restLocal[i] = rest;
            }

            _hasLastOutput = false;
            _aimValid = false;
            _smoothValid = false;
            _outOfRange = false;
            _gate = 1f;
        }

        // ─────────────────────────────────────────── Core ───────────────────────────────────────────

        private static bool BitEqual(Quaternion a, Quaternion b)
            => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

        private static float Smooth01(float v) => v * v * (3f - 2f * v);

        /// <summary>Swing quanh <paramref name="axis"/> một góc <paramref name="angle"/> có nằm trong hộp yaw/pitch của xương không.</summary>
        private static bool InsideBox(BoneData b, float angle, Vector3 axis, Quaternion headAnim,
            Vector3 trackL, Vector3 upL, float shrink)
        {
            Quaternion outRot = Quaternion.AngleAxis(angle, axis) * headAnim;
            DampedTrackMath.GetYawPitch(headAnim, outRot, trackL, upL, out float yaw, out float pitch);

            return yaw   >= -Mathf.Max(0f, b.maxYawLeft   - shrink) &&
                   yaw   <=  Mathf.Max(0f, b.maxYawRight  - shrink) &&
                   pitch <=  Mathf.Max(0f, b.maxPitchUp   - shrink) &&
                   pitch >= -Mathf.Max(0f, b.maxPitchDown - shrink);
        }

        /// <summary>Góc lớn nhất (≤ maxAngle) mà xương này có thể nhận mà không vượt hộp giới hạn.</summary>
        private static float MaxAngleInBox(BoneData b, float maxAngle, Vector3 axis, Quaternion headAnim,
            Vector3 trackL, Vector3 upL, float shrink)
        {
            if (maxAngle <= 0f) return 0f;
            if (InsideBox(b, maxAngle, axis, headAnim, trackL, upL, shrink)) return maxAngle;

            float lo = 0f, hi = maxAngle;
            for (int it = 0; it < 12 && hi - lo > 0.01f; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (InsideBox(b, mid, axis, headAnim, trackL, upL, shrink)) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// Chia góc theo Weight (water-filling): mỗi lượt chia phần còn lại theo tỉ lệ weight cho các xương
        /// chưa chạm giới hạn; xương chạm giới hạn thì khoá lại, phần dư chia tiếp lượt sau. Trả về góc chưa chia được.
        /// </summary>
        private float DistributeWeighted(int n, float total, Vector3 axis, Quaternion headAnim,
            Vector3 trackL, Vector3 upL, float shrink)
        {
            for (int i = 0; i < n; i++)
            {
                _alloc[i] = 0f;
                _cap[i] = _sorted[i].weight > 0f
                    ? MaxAngleInBox(_sorted[i], total, axis, headAnim, trackL, upL, shrink) : 0f;
                _sat[i] = _cap[i] <= 0f;
            }

            float rem = total;
            for (int pass = 0; pass <= n && rem > 1e-4f; pass++)
            {
                float wSum = 0f;
                for (int i = 0; i < n; i++) if (!_sat[i]) wSum += _sorted[i].weight;
                if (wSum <= 1e-6f) break;

                float r = rem, used = 0f;
                bool anySat = false;
                for (int i = 0; i < n; i++)
                {
                    if (_sat[i]) continue;
                    float add = r * _sorted[i].weight / wSum;
                    float room = _cap[i] - _alloc[i];
                    if (add >= room) { _alloc[i] = _cap[i]; used += room; _sat[i] = true; anySat = true; }
                    else { _alloc[i] += add; used += add; }
                }
                rem -= used;
                if (!anySat) break;
            }
            return Mathf.Max(0f, rem);
        }

        private void ReleaseAll(int n)
        {
            for (int i = 0; i < n; i++)
            {
                _smoothQ[i] = Quaternion.identity;
                _lastBase[i] = _baseLocal[i];
                _lastOut[i] = _sorted[i].bone.localRotation;
            }
            _hasLastOutput = true;
            _smoothValid = true;
            _aimValid = false;
        }

        /// <summary>Swing nhỏ nhất (world) từ pose animation của đầu tới hướng nhìn về <paramref name="targetPos"/>: góc (0..180) + trục.</summary>
        private static void GetSwing(Vector3 headPos, Vector3 targetPos, Quaternion headAnim, Vector3 trackL,
            out float angle, out Vector3 axis)
        {
            Quaternion swing = DampedTrackMath.ComputeDampedTrackRotation(headPos, targetPos, headAnim, trackL)
                               * Quaternion.Inverse(headAnim);
            swing.ToAngleAxis(out angle, out axis);
            if (angle > 180f) { angle = 360f - angle; axis = -axis; }
            if (angle < 1e-3f || float.IsNaN(axis.x)) { angle = 0f; axis = Vector3.up; }
        }

        /// <summary>
        /// true nếu hướng về Target nằm trong TỔNG giới hạn của cả chuỗi (không gây overflow). Chỉ đọc, không đổi
        /// trạng thái. <paramref name="shrink"/> thu hẹp giới hạn (limitMargin cho Target ứng viên → chống nhấp nháy ở biên).
        /// </summary>
        private bool IsTargetReachable(int n, Vector3 headPos, Vector3 targetPos, Quaternion headAnim,
            Vector3 trackL, Vector3 upL, float shrink)
        {
            GetSwing(headPos, targetPos, headAnim, trackL, out float angle, out Vector3 axis);
            if (angle <= 0.05f) return true;

            bool weighted = distribution == DistributionMode.Weighted;
            float rem = angle;
            for (int i = n - 1; i >= 0 && rem > 0.05f; i--)
            {
                if (weighted && _sorted[i].weight <= 0f) continue; // Weighted: xương weight 0 không tham gia
                rem -= MaxAngleInBox(_sorted[i], rem, axis, headAnim, trackL, upL, shrink);
            }
            return rem <= 0.05f;
        }

        /// <summary>
        /// true nếu có collider chắn giữa from và Target (bỏ qua collider của chính Target và,
        /// nếu bật, của hierarchy nhân vật).
        /// </summary>
        private bool IsOccluded(Vector3 from, Vector3 targetPos, Transform targetTf, Transform ownRoot)
        {
            Vector3 diff = targetPos - from;
            float dist = diff.magnitude;
            if (dist < 0.01f) return false;

            float checkDist = Mathf.Max(0f, dist - ObstacleEndPadding);
            int count = Physics.RaycastNonAlloc(from, diff / dist, _hitBuffer, checkDist,
                obstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Transform hitTf = _hitBuffer[i].collider.transform;
                if (hitTf.IsChildOf(targetTf)) continue;                    // collider của chính Target
                if (ownRoot != null && hitTf.IsChildOf(ownRoot)) continue;  // collider của chính nhân vật
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────── Auto Tracking ───────────────────────────────────────────

        private static bool IsEligible(DampedTrackConstraint.TargetData t) => t != null && t.target != null && t.weight > 0f;

        /// <summary>Weight hiệu dụng: khi Auto Tracking chạy = Weight × autoBlend, ngược lại = Weight gốc.</summary>
        private static float EffWeight(DampedTrackConstraint.TargetData t, bool autoActive)
            => autoActive ? t.weight * t.autoBlend : t.weight;

        private void ResetAutoState()
        {
            _autoActive = false;
            _autoNoValid = false;
            _autoSelected = null;
        }

        /// <summary>
        /// Chọn Target theo quy tắc: gần nhất trong số Target hợp lệ (cả chuỗi với tới được + không bị che), rồi
        /// cập nhật autoBlend để chuyển mượt giữa Target cũ và mới (tổng autoBlend luôn = 1 → không tụt
        /// influence giữa chừng). Trả về true nếu Auto Tracking đang chạy (bật + có từ 2 Target hợp lệ).
        /// Phải gọi SAU khi đã cập nhật Target.occluded.
        /// </summary>
        private bool UpdateAutoTracking(int boneCount, Vector3 headPos, Quaternion headAnim, Vector3 trackL, Vector3 upL,
            float dt, float now)
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
                    DampedTrackConstraint.TargetData t = targets[i];
                    if (t != null) t.autoBlend = (!coldInit && IsEligible(t)) ? share : 0f;
                }
            }

            _autoSwitchTimer += dt;

            // ── Quét ứng viên ──
            DampedTrackConstraint.TargetData bestValid = null, nearestAny = null;
            float bestValidSqr = float.MaxValue, nearestSqr = float.MaxValue;
            bool curEligible = false, curValid = false;
            float curSqr = 0f;

            for (int i = 0; i < count; i++)
            {
                DampedTrackConstraint.TargetData t = targets[i];
                if (!IsEligible(t)) continue;

                float sqr = (_tgtPos[i] - headPos).sqrMagnitude;
                if (sqr < nearestSqr) { nearestSqr = sqr; nearestAny = t; }

                bool isCur = t == _autoSelected;

                // Target xa hơn ứng viên hợp lệ tốt nhất hiện có (và không phải Target đang chọn) thì
                // không thể thành bestValid → bỏ qua kiểm tra tầm với/Obstacle (tiết kiệm bisection).
                bool valid = (isCur || sqr < bestValidSqr) && !(checkObstacles && t.occluded);
                if (valid)
                {
                    if (isCur)
                        valid = IsTargetReachable(boneCount, headPos, _tgtPos[i], headAnim, trackL, upL, 0f);
                    else if (_reachRef[i] == t && now < _reachNext[i])
                        valid = _reachOk[i]; // Target ứng viên: dùng kết quả cache (làm tươi thưa)
                    else
                    {
                        valid = IsTargetReachable(boneCount, headPos, _tgtPos[i], headAnim, trackL, upL, limitMargin);
                        _reachRef[i] = t; _reachOk[i] = valid;
                        _reachNext[i] = now + AutoProbeInterval * (1f + _probePhase) + (i & 3) * 0.025f;
                    }
                }

                if (isCur) { curEligible = true; curValid = valid; curSqr = sqr; }
                if (valid && sqr < bestValidSqr) { bestValidSqr = sqr; bestValid = t; }
            }

            // Không Target nào hợp lệ → Solve sẽ chặn (nhả về pose gốc) thay vì bám Target đang giữ.
            _autoNoValid = bestValid == null;

            // ── Quyết định Target ──
            DampedTrackConstraint.TargetData next = _autoSelected;
            if (!curEligible)
            {
                // Chưa có / Target cũ bị xóa hoặc Weight = 0 → chọn ngay (không còn hợp lệ nào thì lấy gần nhất).
                next = bestValid != null ? bestValid : nearestAny;
            }
            else if (!curValid)
            {
                // Target hiện tại ngoài tầm với / bị che → đổi ngay nếu còn Target hợp lệ khác,
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

            DampedTrackConstraint.TargetData sel = _autoSelected;
            if (coldInit) sel.autoBlend = 1f;

            // ── Cập nhật autoBlend: sel tăng dần về 1, các Target khác co lại theo tỷ lệ ──
            float step = autoSwitchBlendTime > 0.001f ? dt / autoSwitchBlendTime : 1f;

            float othersSum = 0f;
            for (int i = 0; i < count; i++)
            {
                DampedTrackConstraint.TargetData t = targets[i];
                if (t == null || t == sel) continue;
                if (!IsEligible(t)) { t.autoBlend = 0f; continue; }
                othersSum += t.autoBlend;
            }

            float total = sel.autoBlend + othersSum;
            float selNorm = total > 1e-5f ? sel.autoBlend / total : 1f; // chuẩn hóa về tổng = 1
            float selNew = Mathf.MoveTowards(selNorm, 1f, step);
            float kScale = 0f;
            if (othersSum > 1e-5f) kScale = (1f - selNew) / othersSum;
            else selNew = 1f;

            for (int i = 0; i < count; i++)
            {
                DampedTrackConstraint.TargetData t = targets[i];
                if (t == null || t == sel || !IsEligible(t)) continue;
                t.autoBlend *= kScale;
            }
            sel.autoBlend = selNew;

            return true;
        }

        // Influence = 0 và mọi xương đã về đúng pose gốc → Solve không có việc gì phải làm.
        private bool IsIdle(int n)
        {
            if (influence > 0f || !_smoothValid) return false;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(_smoothQ[i].w) < 0.9999999f) return false; // còn đang nhả dở
            return true;
        }

        // ══════════════════════════════ SCHEDULER ══════════════════════════════
        // Không còn LateUpdate() riêng: ConstraintScheduler gọi Solve() theo thứ tự phụ thuộc.

        public override int DefaultTypeOrder => TypeOrderDampedTrackChain;

        public override void CollectDependencies(List<Transform> inputs, List<Transform> outputs)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                Transform b = bones[i] != null ? bones[i].bone : null;
                if (b == null) continue;
                outputs.Add(b);
                inputs.Add(b.parent); // cha của xương thấp nhất là input thật; các cha nội bộ chuỗi tự bị bỏ (tự phụ thuộc)
            }
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) inputs.Add(targets[i].target);
        }

        /// <summary>Tính và áp constraint cho frame hiện tại. ConstraintScheduler gọi mỗi LateUpdate.</summary>
        public override void Solve()
        {
            if (!_chainValid || !_restReady) return;
            int n = _sorted.Count;
            if (n == 0) return;

            if (IsIdle(n))
            {
                // Không đụng Transform nào. Reset trạng thái bám để khi Influence tăng lại thì khởi động sạch
                // (không dùng _lastOut/_lastBase cũ, không giữ gate cũ).
                _hasLastOutput = false;
                _aimValid = false;
                return;
            }

            // 1. Baseline: reset mọi xương về pose gốc (animation/rest) trước khi đọc world rotation.
            for (int i = 0; i < n; i++)
            {
                Transform b = _sorted[i].bone;
                Quaternion cur = b.localRotation; // đọc 1 lần
                Quaternion baseLocal;
                if (hasExternalPoseDriver)
                {
                    bool skipped = _hasLastOutput && BitEqual(cur, _lastOut[i]); // driver không ghi đè → tránh drift
                    baseLocal = skipped ? _lastBase[i] : cur;
                }
                else baseLocal = _restLocal[i];

                _baseLocal[i] = baseLocal;
                if (!BitEqual(cur, baseLocal)) b.localRotation = baseLocal; // bằng nhau thì khỏi ghi (ghi Transform làm dirty cả cây con)
            }

            Transform head = _sorted[n - 1].bone;
            Quaternion headAnim = head.rotation;           // pose animation của đầu (chưa bị constraint này đụng)
            Vector3 headPos = head.position;
            Vector3 trackL = DampedTrackMath.GetTrackAxisVector(trackAxis);
            Vector3 upL = DampedTrackMath.GetTrackAxisVector(limitUpAxis);
            float dt = Time.deltaTime;
            float now = Time.time;

            // Đọc position của Target đúng 1 lần/frame (Transform.position là native call).
            int tc = targets.Count;
            EnsureTargetBuffers(tc);
            for (int i = 0; i < tc; i++)
            {
                var tt = targets[i];
                if (tt != null && tt.target != null) _tgtPos[i] = tt.target.position;
            }

            // 2. Không còn Target nào đang bật → nhả hết về pose gốc.
            bool anyActive = false;
            for (int i = 0; i < targets.Count; i++)
                if (IsEligible(targets[i])) { anyActive = true; break; }

            if (!anyActive) { ResetAutoState(); ReleaseAll(n); return; }

            // 2b. Obstacle Check: cập nhật occluded + độ "nhìn thấy" từng Target (fade mượt).
            if (checkObstacles)
            {
                float visStep = dt / Mathf.Max(0.01f, occlusionBlendTime);
                Transform ownRoot = ignoreOwnHierarchy ? head.root : null;
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    if (!IsEligible(t)) continue; // Target đang tắt: không tốn raycast

                    // Auto Tracking: Target không được chọn và không còn tham gia blend → raycast thưa.
                    bool sparse = _autoActive && t != _autoSelected && t.autoBlend <= 0.001f;
                    if (now >= t.nextProbeTime)
                    {
                        bool firstProbe = t.nextProbeTime <= 0f; // lần đầu: probe ngay, nhưng lệch pha các lần sau
                        t.occluded = IsOccluded(headPos, _tgtPos[i], t.target, ownRoot);

                        float iv = sparse ? Mathf.Max(AutoProbeInterval, obstacleCheckInterval) : obstacleCheckInterval;
                        float phase = firstProbe ? iv * _probePhase : 0f;
                        t.nextProbeTime = now + iv + phase + (sparse ? (i & 3) * 0.025f : 0f);
                    }

                    t.visibility = Mathf.MoveTowards(t.visibility, t.occluded ? 0f : 1f, visStep);
                }
            }

            // 2c. Auto Tracking: chọn Target gần nhất hợp lệ (cần t.occluded đã cập nhật ở trên).
            bool autoActive = UpdateAutoTracking(n, headPos, headAnim, trackL, upL, dt, now);

            // 2d. Hướng mục tiêu = trung bình có trọng số các Target.
            //   sumRaw: theo weight hiệu dụng. sumVis: nhân thêm visibility (Target đang bị che không kéo hướng).
            //   totalW: tổng weight hiệu dụng (KHÔNG nhân visibility) — dùng cho influence.
            Vector3 sumRaw = Vector3.zero, sumVis = Vector3.zero;
            float totalW = 0f, effTotal = 0f;
            int activeCount = 0, occludedCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || t.target == null) continue;

                float tw = EffWeight(t, autoActive);

                // Đếm Target đang THỰC SỰ tham gia hướng nhìn: khi Auto Tracking chạy chỉ tính Target còn
                // autoBlend (Target không được chọn bị bỏ qua, nếu không thì Target ngoài tầm với nhưng không
                // bị che sẽ khiến occludedAll = false dù Target đang bám đã bị che).
                bool contributes = autoActive ? tw > 0.001f : t.weight > 0f;
                if (checkObstacles && contributes)
                {
                    activeCount++;
                    if (t.occluded) occludedCount++;
                }

                if (tw <= 0f) continue;

                Vector3 d = _tgtPos[i] - headPos;
                float m = d.magnitude;
                if (m < 1e-4f) continue;

                float vis = checkObstacles ? Smooth01(t.visibility) : 1f;
                Vector3 u = d / m * tw;
                sumRaw += u;
                sumVis += u * vis;
                totalW += tw;
                effTotal += tw * vis;
            }

            bool occludedAll = checkObstacles && activeCount > 0 && occludedCount == activeCount;

            // Nếu tất cả bị che (effTotal ≈ 0) thì dùng weight gốc để hướng vẫn xác định được.
            bool useEffWeights = checkObstacles && effTotal > 1e-4f;
            Vector3 sum = useEffWeights ? sumVis : sumRaw;

            if (totalW <= 0f || sum.sqrMagnitude < 1e-8f) { ReleaseAll(n); return; }

            // 3. Swing nhỏ nhất đầu → target (world). Tất cả xương sẽ xoay quanh CÙNG trục này.
            GetSwing(headPos, headPos + sum.normalized, headAnim, trackL, out float totalAngle, out Vector3 axis);

            // 4. Phân bổ góc: ĐẦU trước → overflow xuống cổ → ngực → ... → xương dưới cùng.
            float shrink = _outOfRange ? limitMargin : 0f;
            float rem;
            if (distribution == DistributionMode.Weighted)
                rem = DistributeWeighted(n, totalAngle, axis, headAnim, trackL, upL, shrink);
            else
            {
                rem = totalAngle;
                for (int i = n - 1; i >= 0; i--)
                {
                    float a = MaxAngleInBox(_sorted[i], rem, axis, headAnim, trackL, upL, shrink);
                    _alloc[i] = a;
                    rem -= a;
                }
            }
            _outOfRange = rem > 0.05f;

            Quaternion invHead = Quaternion.Inverse(headAnim);
            for (int i = 0; i < n; i++)
                _liveQ[i] = _alloc[i] > 1e-4f
                    ? invHead * Quaternion.AngleAxis(_alloc[i], axis) * headAnim // lưu trong frame của đầu → bám theo animation thân
                    : Quaternion.identity;

            // 5. Gate (ReleaseToBaseline hoặc TẤT CẢ Target bị che mới chặn): đóng băng → hold → nhả về gốc;
            //    hết chặn → bám lại mượt.
            // Auto Tracking không còn Target hợp lệ (mà không phải do bị che): chặn như overflow, kể cả ở chế độ Clamp.
            bool autoNoTarget = autoActive && _autoNoValid && !occludedAll;
            bool overflowBlocked = (_outOfRange && overflowMode == OverflowMode.ReleaseToBaseline) || autoNoTarget;
            bool blocked = overflowBlocked || occludedAll;
            if (!_aimValid)
            {
                _aimValid = true;
                for (int i = 0; i < n; i++) _aimQ[i] = blocked ? Quaternion.identity : _liveQ[i];
                _gate = blocked ? 0f : 1f;
                _reacquiring = blocked;
                _wasBlocked = blocked;
                _holdTimer = 0f;
            }

            if (blocked)
            {
                float hold = 0f, fade = 0.01f;
                if (overflowBlocked)
                {
                    if (useHold) hold = Mathf.Max(hold, holdDuration);
                    fade = Mathf.Max(fade, returnDuration);
                }
                if (occludedAll)
                {
                    if (useOcclusionHold) hold = Mathf.Max(hold, occlusionHoldDuration);
                    fade = Mathf.Max(fade, occlusionBlendTime);
                }
                _gateFade = fade;

                if (!_wasBlocked) { _wasBlocked = true; _holdTimer = 0f; }
                _reacquiring = true;
                _holdTimer += dt;
                if (_holdTimer > hold)
                    _gate = Mathf.MoveTowards(_gate, 0f, dt / fade);   // _aimQ giữ nguyên (đóng băng)
            }
            else
            {
                float fadeBack = Mathf.Max(0.01f, _gateFade);
                _wasBlocked = false;
                _holdTimer = 0f;
                _gate = Mathf.MoveTowards(_gate, 1f, dt / fadeBack);

                if (_reacquiring)
                {
                    float k = 1f - Mathf.Exp(-dt * 5f / fadeBack);
                    float maxDiff = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        _aimQ[i] = Quaternion.Slerp(_aimQ[i], _liveQ[i], k);
                        maxDiff = Mathf.Max(maxDiff, Quaternion.Angle(_aimQ[i], _liveQ[i]));
                    }
                    if (maxDiff < 0.5f)
                    {
                        for (int i = 0; i < n; i++) _aimQ[i] = _liveQ[i];
                        _reacquiring = false;
                    }
                }
                else
                {
                    for (int i = 0; i < n; i++) _aimQ[i] = _liveQ[i];
                }
            }

            // normalizeWeights: TRUE → influence độc lập với tổng Weight (Weight chỉ là tỉ lệ blend);
            // FALSE → nhân thêm tổng Weight (hành vi cũ). Clamp TRƯỚC khi nhân gate để fade về pose gốc luôn mượt.
            float infl = Mathf.Clamp01(influence);
            float blend = normalizeWeights ? infl : infl * Mathf.Clamp01(totalW);
            float w = blend * Smooth01(_gate);

            // 6. Áp dụng từ gốc → đầu. Mỗi xương xoay trong WORLD space quanh trục swing chung;
            //    bone.rotation đã bao gồm swing của các xương cha vừa áp nên góc cộng dồn đúng.
            bool instant = followSpeed >= MaxFollowSpeed;
            float ks = 1f - Mathf.Exp(-followSpeed * dt);

            for (int i = 0; i < n; i++)
            {
                Quaternion desired = w >= 0.9999f ? _aimQ[i]
                                   : w <= 0.0001f ? Quaternion.identity
                                   : Quaternion.Slerp(Quaternion.identity, _aimQ[i], w);

                if (instant || !_smoothValid) _smoothQ[i] = desired;
                else _smoothQ[i] = Quaternion.Slerp(_smoothQ[i], desired, ks);

                Transform b = _sorted[i].bone;
                bool swung = Mathf.Abs(_smoothQ[i].w) < 0.9999999f; // ≈ identity → không cần ghi rotation
                if (swung)
                {
                    Quaternion swingW = headAnim * _smoothQ[i] * invHead;
                    b.rotation = swingW * b.rotation;
                }

                _lastBase[i] = _baseLocal[i];
                _lastOut[i] = swung ? b.localRotation : _baseLocal[i]; // không ghi → localRotation chính là baseline
            }

            _smoothValid = true;
            _hasLastOutput = true;
        }

        // ─────────────────────────────────────────── Public API ───────────────────────────────────────────

        public bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)
        {
            if (string.IsNullOrEmpty(id) || targetTransform == null) return false;
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null && targets[i].id == id) return false;

            targets.Add(new DampedTrackConstraint.TargetData
            {
                id = id, target = targetTransform, weight = Mathf.Clamp01(initialWeight)
            });
            ConstraintScheduler.MarkDirty();
            return true;
        }

        public bool SetTargetWeight(string id, float weight)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null && targets[i].id == id)
                {
                    targets[i].weight = Mathf.Clamp01(weight);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Bật/tắt Auto Tracking tại runtime (chỉ chạy khi có từ 2 Target hợp lệ).</summary>
        public void SetAutoTracking(bool enabled) => autoTracking = enabled;

        public void SetInfluence(float value) => influence = Mathf.Clamp01(value);

        public void SetInfluence(float value, float duration)
        {
            if (_influenceRoutine != null) StopCoroutine(_influenceRoutine);
            if (duration <= 0f) { SetInfluence(value); return; }
            _influenceRoutine = StartCoroutine(InfluenceRoutine(Mathf.Clamp01(value), duration));
        }

        private IEnumerator InfluenceRoutine(float goal, float duration)
        {
            float start = influence, t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                influence = Mathf.Lerp(start, goal, Mathf.Clamp01(t / duration));
                yield return null;
            }
            influence = goal;
            _influenceRoutine = null;
        }

        // ─────────────────────────────────────────── Gizmos ───────────────────────────────────────────
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (!_chainBuilt) RebuildChain();
            int n = _sorted.Count;
            if (n == 0) return;

            Gizmos.color = _outOfRange ? Color.red : gizmoChainColor;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = _sorted[i].bone.position;
                Gizmos.DrawWireSphere(p, 0.03f);
                if (i + 1 < n) Gizmos.DrawLine(p, _sorted[i + 1].bone.position);
            }

            Transform head = _sorted[n - 1].bone;
            Gizmos.DrawLine(head.position,
                head.position + head.rotation * DampedTrackMath.GetTrackAxisVector(trackAxis) * 0.3f);

            Gizmos.color = gizmoTargetColor;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || t.target == null || t.weight <= 0f) continue;

                if (checkObstacles && t.occluded) Gizmos.color = gizmoBlockedColor;
                else if (IsAutoTrackingActive && t == _autoSelected) Gizmos.color = gizmoSelectedColor;
                else Gizmos.color = gizmoTargetColor;

                Gizmos.DrawLine(head.position, t.target.position);
                Gizmos.DrawWireSphere(t.target.position, 0.08f);
            }
        }
#endif
    }
}