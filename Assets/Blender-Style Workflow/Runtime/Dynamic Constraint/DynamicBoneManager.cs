using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// 1 manager duy nhất cho TOÀN BỘ scene (mọi nhân vật, mọi DynamicBone). Thay vì mỗi
    /// DynamicBone tự LateUpdate() riêng (bản CPU cũ), mọi instance đăng ký (Register) vào đây;
    /// manager gộp hết vào 1 bộ NativeArray dùng chung rồi chạy ĐÚNG 1 chuỗi job Burst / frame
    /// cho tất cả — cách duy nhất để Job System có đủ khối lượng công việc lớn (hàng trăm chain
    /// song song) mà chia đều cho các worker thread hiệu quả.
    ///
    /// Vòng đời 1 frame (xem LateUpdate):
    ///   1. Nếu có thay đổi cấu trúc (thêm/xoá nhân vật, đổi rootBones, đổi số collider) →
    ///      RebuildNativeArrays() — chỉ tốn khi structure đổi, KHÔNG chạy mỗi frame.
    ///   2. Refresh nhẹ mỗi frame: BoneParams (damping/gravity/...) + ColliderConfigData
    ///      (size/offset/type) — cho phép tinh chỉnh Inspector lúc Play mà không cần rebuild.
    ///   3. Schedule chuỗi job: Read transforms → Gather colliders → Simulate (song song theo
    ///      chain) → Apply rotation (song song theo chain) → Ghi rotation ra Transform thật.
    ///   4. Complete() ngay trong LateUpdate (đơn giản, đúng, dễ debug). Muốn overlap sâu hơn
    ///      với các hệ thống khác, có thể tách Schedule ở Update() và Complete() ở LateUpdate()
    ///      — xem ScheduleOnly()/CompletePending() bên dưới.
    /// </summary>
    [DefaultExecutionOrder(500)] // chạy sau Animator (mặc định ~0) để đọc đúng pose vừa animate
    public sealed class DynamicBoneManager : MonoBehaviour
    {
        private static DynamicBoneManager _instance;

        public static DynamicBoneManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                
                _instance = FindFirstObjectByType<DynamicBoneManager>();
                if (_instance != null) return _instance;

                var go = new GameObject("~DynamicBoneManager");
                _instance = go.AddComponent<DynamicBoneManager>(); // Awake() bên dưới sẽ thấy _instance == this, không tự huỷ
                return _instance;
            }
        }

        [Tooltip("Nếu tắt (khuyến nghị): Schedule ở Update(), Complete() ở LateUpdate() — job chạy song song với engine.")]
        public bool completeImmediately = false;

        [Tooltip("Số chain/batch IJobParallelFor. Chain ngắn (<=8 bone): 8-16. Chain dài: 1-4.")]
        [Range(1, 64)]
        public int simulationBatchSize = 8;

        [Tooltip("Bật để log rebuild.")]
        public bool debugLogging = false;

        // ══════════════════════════════════════════════════════════
        //  LOD & CULLING
        //  Mục tiêu: KHÔNG tốn chi phí mô phỏng/va chạm cho nhân vật ngoài khung hình hoặc quá xa
        //  camera. Tính mỗi frame trên main-thread (chỉ vài chục iteration cho vài chục/trăm nhân
        //  vật — rẻ), rồi ghi kết quả (active/dt/số vòng va chạm/bật-tắt collider) vào BoneParams
        //  TRƯỚC khi schedule job — job Burst không biết gì về camera, chỉ đọc cờ có sẵn.
        // ══════════════════════════════════════════════════════════
        [Header("LOD & Culling")]
        [Tooltip("Bật hệ thống LOD + Culling. Tắt = luôn mô phỏng full chất lượng mọi nhân vật mọi frame (hành vi cũ).")]
        public bool enableLodAndCulling = true;

        [Tooltip("Điểm tham chiếu để tính khoảng cách LOD (thường là camera người chơi). " +
                 "Để trống sẽ dùng Camera.main mỗi frame.")]
        public Transform lodReferenceOverride;

        [Tooltip("Trong khoảng cách này, KHÔNG bao giờ đóng băng/giảm chất lượng dù ngoài khung hình " +
                 "— tránh nhân vật đứng sát camera bị 'đứng hình' khi camera quay nhanh qua nó.")]
        public float cullMinDistance = 2f;

        [Tooltip("Dưới khoảng cách này: LOD0 — mô phỏng full chất lượng (đúng như không có LOD).")]
        public float lod1Distance = 8f;

        [Tooltip("Dưới khoảng cách này (và >= lod1Distance): LOD1 — vẫn mô phỏng mỗi frame nhưng " +
                 "giảm collisionSolveIterations xuống 1.")]
        public float lod2Distance = 15f;

        [Tooltip("Dưới khoảng cách này (và >= lod2Distance): LOD2 — chỉ mô phỏng cách " +
                 "lod2FrameInterval frame/lần (dt tích luỹ để bù), tắt hẳn collider. " +
                 "Quá khoảng cách này (hoặc ngoài khung hình, xem cullWhenNotVisible): đóng băng hoàn toàn (LOD3).")]
        public float lod3Distance = 25f;

        [Tooltip("Tần suất mô phỏng ở LOD2 — mô phỏng 1 frame rồi bỏ qua (interval-1) frame kế tiếp.")]
        [Range(2, 8)]
        public int lod2FrameInterval = 3;

        [Tooltip("Nếu bật: nhân vật ngoài frustum camera (Renderer.isVisible == false, đọc qua " +
                 "DynamicBone.cullingRenderer — gần như miễn phí vì Unity đã tính sẵn) sẽ bị coi " +
                 "như LOD3 (đóng băng hoàn toàn) bất kể khoảng cách, miễn là > cullMinDistance.")]
        public bool cullWhenNotVisible = true;

        [Tooltip("Trần dt khi 'bắt kịp' sau LOD2/đóng băng dài — tránh 1 bước dt quá lớn gây giật vật lý.")]
        public float maxCatchupDt = 0.1f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME INFO (chỉ đọc) — phục vụ DynamicBoneManagerEditor, không dùng cho logic.
        // ══════════════════════════════════════════════════════════
        /// <summary>Số DynamicBone đang đăng ký với manager này.</summary>
        public int RegisteredCount => _registered.Count;
        /// <summary>Tổng số node (particle) trong buffer toàn cục hiện tại.</summary>
        public int TotalNodeCount => _nodes.IsCreated ? _nodes.Length : 0;
        /// <summary>Tổng số chain (= tổng số root bone của mọi DynamicBone).</summary>
        public int TotalChainCount => _chains.IsCreated ? _chains.Length : 0;
        /// <summary>Tổng số collider hợp lệ đang được dùng bởi ít nhất 1 DynamicBone.</summary>
        public int TotalColliderCount => _colliderData.IsCreated ? _colliderData.Length : 0;
        /// <summary>true nếu instance này chính là singleton đang hoạt động (không phải bản dư thừa).</summary>
        public bool IsActiveInstance => _instance == this;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Đã có 1 manager khác (được đặt sẵn trong scene hoặc tự sinh) — GameObject này
                // là bản thừa, huỷ đi để tránh 2 manager tồn tại song song (1 cái chạy job thật,
                // 1 cái đứng im nhưng gây rối khi debug).
                Debug.LogWarning($"[DynamicBoneManager] Phát hiện 2 DynamicBoneManager trong scene " +
                                  $"('{_instance.gameObject.name}' và '{gameObject.name}'). Chỉ giữ lại 1. " +
                                  $"Bạn không cần tự đặt DynamicBoneManager vào scene — nó tự sinh khi cần.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private readonly List<DynamicBone> _registered = new List<DynamicBone>();
        // [PERF fix #6] HashSet chỉ dùng để tăng tốc Contains() trong Register() lên O(1).
        // KHÔNG thay List bằng HashSet: thứ tự trong _registered quyết định paramIndex/offset
        // trong RebuildNativeArrays(), phải giữ nguyên thứ tự ổn định.
        private readonly HashSet<DynamicBone> _registeredSet = new HashSet<DynamicBone>();
        private bool _dirty = true;

        // ── Global persistent buffers ──────────────────────────────────────
        private NativeArray<BoneNode> _nodes;
        private NativeArray<ChainRange> _chains;
        // [CROSS LINKS] 1 nhóm = 1 index của job mô phỏng: 1 chain (mặc định) hoặc toàn bộ chain của 1
        // DynamicBone bật Cross Links. _links = liên kết ngang (index node toàn cục), chỉ nội bộ nhóm.
        private NativeArray<ChainGroup> _groups;
        private NativeArray<NodeLink> _links;
        private NativeArray<BoneParams> _params;
        private NativeArray<ColliderConfigData> _colliderConfigs;
        private NativeArray<JobColliderData> _colliderData;
        private NativeArray<byte> _colliderActive;
        private NativeArray<float3> _colliderPrevAnchor;
        private NativeArray<byte> _colliderHasPrevAnchor;

        private NativeArray<float3> _readPosition;
        private NativeArray<quaternion> _readRotation;
        private NativeArray<quaternion> _externalRotation;
        private NativeArray<quaternion> _outRotation;
        private NativeArray<byte> _writeMode;
        // [PERF fix #2] map slot-trong-_writeTransforms → index-node-thật, xem WriteTransformRotationJob.
        private NativeArray<int> _writeNodeMap;

        private TransformAccessArray _allTransforms;
        // [PERF fix #2] chỉ chứa Transform của node NON-LEAF (node có con) — đây là toàn bộ tập
        // node mà WriteTransformRotationJob thực sự cần ghi (leaf luôn writeMode = 0).
        private TransformAccessArray _writeTransforms;
        private TransformAccessArray _colliderTransforms;

        // [PERF fix #3] Danh sách Transform cha-ngoài-cây đã dedup, đọc rotation trên MAIN THREAD
        // mỗi frame (rất ít entry — ~1/nhân vật, không phải 1/chain) thay vì 1 IJobParallelForTransform
        // + sync barrier riêng (ReadExternalParentJob cũ đã bị xoá khỏi DynamicBoneJobs.cs).
        private Transform[] _externalParentTf = new Transform[0];

        // [LOD] Bản sao Transform thường (song song 1:1 với _nodes) — CHỈ dùng bởi
        // ResetInstancePositions() khi 1 nhân vật vừa hồi sinh sau khi bị đóng băng dài, để đọc
        // lại world position hiện tại của từng bone và snap position/prevPosition (tránh nổ vật lý
        // do prevPosition quá cũ). Không dùng trong job — job chỉ cần _nodes/_readPosition.
        private Transform[] _allTfArray = new Transform[0];

        // [LOD] Trạng thái LOD/culling của từng instance, song song với _registered (index = i,
        // KHÔNG phải paramIndex — nhưng hiện paramIndex luôn = i nên tương đương).
        private struct LodState
        {
            public float accumDt;  // dt tích luỹ khi đang ở chế độ giảm tần suất (LOD2)
            public bool frozen;    // đang bị đóng băng hoàn toàn (LOD3/ngoài khung hình) hay không
        }
        private LodState[] _lodState = new LodState[0];

        // ── Per-instance offsets (song song với _registered) ───────────────
        private struct InstanceRange
        {
            public int nodeOffset, nodeCount;
            public int chainOffset, chainCount;
            public int colliderOffset, colliderCount;
            public int paramIndex;
        }
        private readonly List<InstanceRange> _ranges = new List<InstanceRange>();

        //private bool _paramsDirty = true;
#if UNITY_EDITOR
        private bool _anyShowGizmos = false; // cache: có bất kỳ bone nào showGizmos=true không
#endif

        private JobHandle _pendingHandle;
        private bool _hasPending;

        public void Register(DynamicBone bone)
        {
            if (!_registeredSet.Add(bone)) return; // đã có sẵn — Add() trả false, O(1)
            _registered.Add(bone);
            _dirty = true;
        }

        public void Unregister(DynamicBone bone)
        {
            _registeredSet.Remove(bone);
            if (_registered.Remove(bone)) _dirty = true;
        }

        /// <summary>Gọi khi 1 DynamicBone thay đổi cấu trúc cây.</summary>
        public void MarkDirty() { _dirty = true; }

        /// <summary>Gọi khi chỉ đổi physics params (damping/gravity...) không đổi cấu trúc.</summary>
        //public void MarkParamsDirty() => _paramsDirty = true;

        private void Update()
        {
            if (!completeImmediately) ScheduleFrame();
        }

        private void LateUpdate()
        {
            if (completeImmediately)
            {
                ScheduleFrame();
                CompletePending();
            }
            else
            {
                CompletePending();
            }
        }

        private void ScheduleFrame()
        {
            if (_registered.Count == 0) return;

            if (_dirty)
            {
                RebuildNativeArrays();
                _dirty = false;
            }

            if (_nodes.Length == 0) return;

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // [LOD] BoneParams giờ được rebuild MỖI FRAME (không còn gate theo _paramsDirty) vì
            // ApplyLodAndCulling() cần ghi đè active/dt/iterations lên baseline mỗi frame — làm
            // vậy chỉ tốn thêm vài trăm struct copy nhỏ, không đáng kể so với chi phí job đang cố
            // tiết kiệm. _paramsDirty vẫn được set bởi MarkParamsDirty()/OnValidate() cho tương
            // thích ngược nhưng không còn gate gì (an toàn để giữ nguyên, không dùng cũng không sao).
            RefreshPerFrameParams();
            ApplyLodAndCulling(dt);

            // [PERF fix #3] Đọc rotation của các Transform cha-ngoài-cây (đã dedup, ~1/nhân vật)
            // ngay trên main thread — rẻ hơn nhiều so với Schedule() + sync 1 job riêng cho từng
            // ấy entry, và loại bỏ hẳn 1 job khỏi pipeline.
            for (int i = 0; i < _externalParentTf.Length; i++)
            {
                Transform t = _externalParentTf[i];
                _externalRotation[i] = t != null ? (quaternion)t.rotation : quaternion.identity;
            }

            JobHandle readNodes = new ReadNodeTransformsJob
            {
                outPosition = _readPosition,
                outRotation = _readRotation
            }.Schedule(_allTransforms);

            JobHandle gatherColliders = default;
            if (_colliderTransforms.length > 0)
            {
                UpdateColliderActiveFlags();
                gatherColliders = new GatherCollidersJob
                {
                    configs = _colliderConfigs,
                    activeFlags = _colliderActive,
                    prevAnchor = _colliderPrevAnchor,
                    hasPrevAnchor = _colliderHasPrevAnchor,
                    outData = _colliderData
                }.Schedule(_colliderTransforms);
            }

            JobHandle simulateDeps = JobHandle.CombineDependencies(readNodes, gatherColliders);

            // [PERF fix #1] Simulate + ApplyRotation gộp thành 1 Schedule duy nhất (xem
            // SimulateAndApplyRotationChainJob) — bỏ 1 sync barrier + 1 lần reschedule 240 item
            // giữa 2 job trước đây.
            // [LOD] dt KHÔNG còn truyền chung 1 giá trị cho toàn bộ job — mỗi chain tự đọc
            // BoneParams.dt (đã được ApplyLodAndCulling() tính riêng, có thể = 0 nếu bị đóng băng
            // frame này, hoặc = dt tích luỹ nếu vừa "bắt kịp" sau LOD2).
            JobHandle simulateAndApply = new SimulateAndApplyRotationChainJob
            {
                groups = _groups,
                links = _links,
                chains = _chains,
                boneParams = _params,
                rootPosition = _readPosition,
                nodeRotation = _readRotation,
                externalRotation = _externalRotation,
                colliders = _colliderData,
                nodes = _nodes,
                outRotation = _outRotation,
                writeMode = _writeMode
            }.Schedule(_groups.Length, simulationBatchSize, simulateDeps);

            // [PERF fix #2] Chỉ ghi Transform cho node NON-LEAF (_writeTransforms), qua map về
            // index node thật — giảm số Execute() từ tổng-số-node xuống chỉ còn số node có con.
            JobHandle write = new WriteTransformRotationJob
            {
                nodeIndexMap = _writeNodeMap,
                outRotation = _outRotation,
                writeMode = _writeMode
            }.Schedule(_writeTransforms, simulateAndApply);

            JobHandle cacheAnchors = _colliderTransforms.length > 0
                ? new CacheColliderAnchorsJob
                {
                    data = _colliderData,
                    activeFlags = _colliderActive,
                    prevAnchor = _colliderPrevAnchor,
                    hasPrevAnchor = _colliderHasPrevAnchor
                }.Schedule(JobHandle.CombineDependencies(gatherColliders, write))
                : default;

            _pendingHandle = JobHandle.CombineDependencies(write, cacheAnchors);
            _hasPending = true;
        }

        private void CompletePending()
        {
            if (!_hasPending) return;
            _pendingHandle.Complete();
            _hasPending = false;

#if UNITY_EDITOR
            if (_anyShowGizmos) // skip loop hoàn toàn nếu không có gizmos
            {
                for (int i = 0; i < _registered.Count; i++)
                {
                    DynamicBone b = _registered[i];
                    if (b == null || !b.showGizmos) continue;
                    InstanceRange r = _ranges[i];
                    b.CopyGizmoSnapshot(_nodes, r.nodeOffset, r.nodeCount);
                }
            }
#endif
        }

        private void RefreshPerFrameParams()
        {
            for (int i = 0; i < _registered.Count; i++)
            {
                DynamicBone b = _registered[i];
                InstanceRange r = _ranges[i];
                if (b == null) continue;
                _params[r.paramIndex] = b.BuildJobParams(r.colliderOffset, r.colliderCount);

                for (int c = 0; c < r.colliderCount; c++)
                {
                    _colliderConfigs[r.colliderOffset + c] = b.BuildColliderConfigData(c);
                }
            }
        }

        /// <summary>
        /// [LOD & CULLING] Chạy SAU RefreshPerFrameParams() mỗi frame — ghi đè active/dt/số vòng
        /// va chạm/bật-tắt collider lên _params dựa trên khoảng cách tới camera + visibility.
        /// CHỈ ĐƯỢC GIẢM chất lượng so với baseline Inspector, không bao giờ tăng (VD: người dùng
        /// tắt collider thủ công thì LOD không được bật lại).
        /// </summary>
        private void ApplyLodAndCulling(float dt)
        {
            if (!enableLodAndCulling)
            {
                // Tắt LOD: đảm bảo mọi chain luôn active=1, dt=dt toàn cục — y hệt hành vi cũ
                // (không có LOD) dù _params vừa được RefreshPerFrameParams() ghi baseline.
                for (int i = 0; i < _registered.Count; i++)
                {
                    InstanceRange r = _ranges[i];
                    BoneParams p = _params[r.paramIndex];
                    p.active = 1;
                    p.dt = dt;
                    _params[r.paramIndex] = p;
                }
                return;
            }

            Transform lodRef = lodReferenceOverride;
            if (lodRef == null)
            {
                Camera cam = Camera.main; // gọi 1 lần/frame, không phải 1 lần/nhân vật
                lodRef = cam != null ? cam.transform : null;
            }
            Vector3 refPos = lodRef != null ? lodRef.position : Vector3.zero;

            if (_lodState.Length != _registered.Count) System.Array.Resize(ref _lodState, _registered.Count);

            for (int i = 0; i < _registered.Count; i++)
            {
                DynamicBone b = _registered[i];
                if (b == null) continue;
                InstanceRange r = _ranges[i];
                BoneParams p = _params[r.paramIndex];
                LodState st = _lodState[i];

                float dist = lodRef != null ? Vector3.Distance(b.transform.position, refPos) : 0f;

                bool visible = true;
                if (cullWhenNotVisible && b.cullingRenderer != null && dist > cullMinDistance)
                    visible = b.cullingRenderer.isVisible;

                bool shouldFreeze = (cullWhenNotVisible && !visible && dist > cullMinDistance) || dist > lod3Distance;

                if (shouldFreeze)
                {
                    p.active = 0;
                    p.dt = 0f;
                    st.frozen = true;
                    st.accumDt = 0f;
                }
                else if (st.frozen)
                {
                    // Vừa thoát trạng thái đóng băng (vào lại gần/vào lại khung hình) — vị trí
                    // node có thể đã "đứng yên" rất lâu trong khi Transform thật (do Animator) đã
                    // di chuyển đi xa → snap position/prevPosition để tránh velocity giả tạo làm
                    // nổ vật lý ở frame đầu tiên hồi sinh.
                    ResetInstancePositions(r);
                    p.active = 1;
                    p.dt = dt; // 1 frame bình thường, không dùng accumDt cũ
                    st.frozen = false;
                    st.accumDt = 0f;
                }
                else if (dist <= lod1Distance)
                {
                    // LOD0 — full chất lượng, y hệt baseline Inspector.
                    p.active = 1;
                    p.dt = dt;
                    st.accumDt = 0f;
                }
                else if (dist <= lod2Distance)
                {
                    // LOD1 — vẫn mô phỏng mỗi frame, chỉ giảm số vòng giải va chạm.
                    p.active = 1;
                    p.dt = dt;
                    p.collisionSolveIterations = math.min(p.collisionSolveIterations, 1);
                    st.accumDt = 0f;
                }
                else
                {
                    // LOD2 — mô phỏng cách quãng (lod2FrameInterval frame/lần), tắt hẳn collider,
                    // dt tích luỹ để bù cho các frame bị bỏ qua (vật lý vẫn phản ứng đúng biên độ).
                    st.accumDt += dt;
                    int interval = math.max(2, lod2FrameInterval);
                    bool tick = ((Time.frameCount + i) % interval) == 0;
                    if (tick)
                    {
                        p.active = 1;
                        p.dt = math.min(st.accumDt, maxCatchupDt);
                        p.enableColliders = 0;
                        st.accumDt = 0f;
                    }
                    else
                    {
                        p.active = 0;
                        p.dt = 0f;
                    }
                }

                _params[r.paramIndex] = p;
                _lodState[i] = st;
            }
        }

        /// <summary>
        /// Snap position/prevPosition của toàn bộ node thuộc 1 instance về world position HIỆN TẠI
        /// của Transform thật (velocity = 0). Chỉ gọi khi 1 instance vừa thoát trạng thái đóng
        /// băng dài (xem ApplyLodAndCulling) — số instance thoả điều kiện này mỗi frame thường là
        /// 0, hiếm khi hơn vài, nên vòng lặp trên _allTfArray ở đây không đáng kể.
        /// </summary>
        private void ResetInstancePositions(InstanceRange range)
        {
            for (int n = 0; n < range.nodeCount; n++)
            {
                int idx = range.nodeOffset + n;
                Transform tf = _allTfArray[idx];
                if (tf == null) continue;
                BoneNode node = _nodes[idx];
                float3 pos = tf.position;
                node.position = pos;
                node.prevPosition = pos;
                _nodes[idx] = node;
            }
        }

        private void UpdateColliderActiveFlags()
        {
            for (int i = 0; i < _registered.Count; i++)
            {
                DynamicBone b = _registered[i];
                if (b == null) continue;
                InstanceRange r = _ranges[i];
                for (int c = 0; c < r.colliderCount; c++)
                {
                    Transform t = b.GetColliderTransformForActiveCheck(c);
                    _colliderActive[r.colliderOffset + c] = (byte)((t != null && t.gameObject.activeInHierarchy) ? 1 : 0);
                }
            }
        }

        private void RebuildNativeArrays()
        {
            DisposeAll();

            // [FIX] Loại bỏ entry null TRƯỚC khi loop, để index i luôn khớp 1:1 giữa
            // _registered / _ranges / _params. Trước đây nếu có null ở giữa danh sách
            // (component bị destroy nhưng chưa kịp Unregister), _ranges sẽ bị lệch so với
            // _registered cho MỌI entry phía sau → sai offset collider/param của các
            // nhân vật đứng sau trong danh sách.
            _registered.RemoveAll(b => b == null);
            _registeredSet.RemoveWhere(b => b == null); // giữ đồng bộ với _registered (xem fix #6)

            int totalNodes = 0, totalChains = 0, totalColliders = 0;
            _ranges.Clear();

            var perInstanceColliderTf = new List<Transform>();
            var allTf = new List<Transform>();
            var extTf = new List<Transform>();
            var extTfIndex = new Dictionary<Transform, int>(); // dedup: cùng Transform cha dùng 1 slot
            var chainList = new List<ChainRange>();
            var groupList = new List<ChainGroup>();
            var linkList = new List<NodeLink>();
            var nodeList = new List<BoneNode>();
            // [PERF fix #2] node NON-LEAF (có con) — tập node duy nhất WriteTransformRotationJob
            // thực sự cần ghi. writeTf[k] tương ứng với node global index writeNodeMap[k].
            var writeTf = new List<Transform>();
            var writeNodeMap = new List<int>();

            // [CHẨN ĐOÁN] Phát hiện 2 DynamicBone khác nhau cùng điều khiển 1 Transform xương
            // thật — nguyên nhân PHỔ BIẾN NHẤT khiến "thêm nhân vật thứ 2 thì chỉ 1 cái chạy":
            // thường do rootBones/colliders của bản sao vẫn còn trỏ vào Transform của bản gốc
            // (lỗi kéo-thả tham chiếu khi Duplicate/Instantiate). Cả 2 chain sẽ ghi đè lên CÙNG
            // 1 Transform mỗi frame — xương thật của nhân vật kia không có ai điều khiển.
            var ownerOfTransform = new Dictionary<Transform, DynamicBone>();

            for (int i = 0; i < _registered.Count; i++)
            {
                DynamicBone b = _registered[i];
                b._rebuildingFromManager = true;
                b.InitializeTrees();
                b._rebuildingFromManager = false;

                var range = new InstanceRange
                {
                    nodeOffset = totalNodes,
                    nodeCount = b.JobNodeCount,
                    chainOffset = totalChains,
                    chainCount = b.JobRootIndices.Length,
                    colliderOffset = totalColliders,
                    colliderCount = b.JobColliderCount,
                    paramIndex = i
                };

                for (int n = 0; n < b.JobNodeCount; n++)
                {
                    Transform tf = b.JobTransforms[n];
                    BoneNode node = b.JobNodes[n];
                    if (node.parent >= 0) node.parent += totalNodes;
                    node.subtreeEnd += totalNodes;
                    int globalIndex = totalNodes + n;
                    nodeList.Add(node);
                    allTf.Add(tf);

                    // Non-leaf ⇔ subtreeEnd != globalIndex+1 (leaf's subtree is just itself).
                    if (node.subtreeEnd != globalIndex + 1)
                    {
                        writeTf.Add(tf);
                        writeNodeMap.Add(globalIndex);
                    }

                    if (ownerOfTransform.TryGetValue(tf, out DynamicBone otherOwner) && otherOwner != b)
                    {
                        Debug.LogWarning(
                            $"[DynamicBoneManager] Transform '{tf.name}' đang được điều khiển bởi CẢ 2 " +
                            $"DynamicBone: '{otherOwner.name}' và '{b.name}'. Đây gần như chắc chắn là do " +
                            $"rootBones của 1 trong 2 vẫn trỏ nhầm sang xương của nhân vật kia (thường gặp " +
                            $"khi Duplicate/Instantiate mà quên gán lại rootBones cho bản sao) — nhân vật " +
                            $"bị 'mất quyền điều khiển' xương này sẽ đứng yên. Kiểm tra lại rootBones/colliders " +
                            $"của 2 component trên.", b);
                    }
                    else
                    {
                        ownerOfTransform[tf] = b;
                    }
                }

                int chainBase = chainList.Count; // index chain toàn cục đầu tiên của instance này

                for (int r = 0; r < b.JobRootIndices.Length; r++)
                {
                    int localRoot = b.JobRootIndices[r];
                    Transform extParent = b.JobRootParentTf[localRoot];
                    int extIndex = -1;
                    if (extParent != null)
                    {
                        // Dedup: cùng Transform cha dùng chung 1 slot thay vì thêm N lần
                        if (!extTfIndex.TryGetValue(extParent, out extIndex))
                        {
                            extIndex = extTf.Count;
                            extTfIndex[extParent] = extIndex;
                            extTf.Add(extParent);
                        }
                    }

                    chainList.Add(new ChainRange
                    {
                        start = totalNodes + localRoot,
                        end = totalNodes + b.JobNodes[localRoot].subtreeEnd,
                        paramIndex = i,
                        externalIndex = extIndex
                    });
                }

                // ── Nhóm job ──────────────────────────────────────────────────────────
                // Có Cross Links hợp lệ → TOÀN BỘ chain của instance gộp 1 nhóm (cùng 1 worker, không race).
                // Ngược lại → mỗi chain 1 nhóm như trước (độc lập, song song hoàn toàn).
                NodeLink[] localLinks = b.JobLinks;
                bool useLinks = localLinks.Length > 0 && range.chainCount > 1;
                if (useLinks)
                {
                    // Kiểm tra bất biến an toàn: mọi đầu mút link phải nằm trong vùng node của instance
                    // (NativeDisableParallelForRestriction đã tắt safety check nên phải tự đảm bảo).
                    for (int li = 0; li < localLinks.Length && useLinks; li++)
                    {
                        if (localLinks[li].a < 0 || localLinks[li].a >= b.JobNodeCount ||
                            localLinks[li].b < 0 || localLinks[li].b >= b.JobNodeCount)
                        {
                            Debug.LogError($"[DynamicBoneManager] Cross Link của '{b.name}' trỏ ra ngoài vùng node " +
                                           $"của chính nó — bỏ qua toàn bộ link của component này.", b);
                            useLinks = false;
                        }
                    }
                }

                if (useLinks)
                {
                    int linkBase = linkList.Count;
                    for (int li = 0; li < localLinks.Length; li++)
                    {
                        NodeLink l = localLinks[li];
                        l.a += totalNodes;
                        l.b += totalNodes;
                        linkList.Add(l);
                    }
                    groupList.Add(new ChainGroup
                    {
                        chainStart = chainBase,
                        chainCount = range.chainCount,
                        linkStart = linkBase,
                        linkCount = localLinks.Length
                    });
                }
                else
                {
                    for (int r = 0; r < range.chainCount; r++)
                        groupList.Add(new ChainGroup { chainStart = chainBase + r, chainCount = 1, linkStart = 0, linkCount = 0 });
                }

                for (int c = 0; c < b.JobColliderCount; c++)
                {
                    perInstanceColliderTf.Add(b.GetColliderTransformForActiveCheck(c));
                }

                totalNodes += b.JobNodeCount;
                totalChains += range.chainCount;
                totalColliders += b.JobColliderCount;

                _ranges.Add(range);
            }

            _nodes = new NativeArray<BoneNode>(nodeList.ToArray(), Allocator.Persistent);
            _chains = new NativeArray<ChainRange>(chainList.ToArray(), Allocator.Persistent);
            _groups = new NativeArray<ChainGroup>(groupList.ToArray(), Allocator.Persistent);
            _links = new NativeArray<NodeLink>(math.max(1, linkList.Count), Allocator.Persistent);
            for (int i = 0; i < linkList.Count; i++) _links[i] = linkList[i];
            _params = new NativeArray<BoneParams>(math.max(1, _registered.Count), Allocator.Persistent);

            _colliderConfigs = new NativeArray<ColliderConfigData>(math.max(1, totalColliders), Allocator.Persistent);
            _colliderData = new NativeArray<JobColliderData>(math.max(1, totalColliders), Allocator.Persistent);
            _colliderActive = new NativeArray<byte>(math.max(1, totalColliders), Allocator.Persistent);
            _colliderPrevAnchor = new NativeArray<float3>(math.max(1, totalColliders), Allocator.Persistent);
            _colliderHasPrevAnchor = new NativeArray<byte>(math.max(1, totalColliders), Allocator.Persistent);

            _readPosition = new NativeArray<float3>(math.max(1, totalNodes), Allocator.Persistent);
            _readRotation = new NativeArray<quaternion>(math.max(1, totalNodes), Allocator.Persistent);
            _outRotation = new NativeArray<quaternion>(math.max(1, totalNodes), Allocator.Persistent);
            _writeMode = new NativeArray<byte>(math.max(1, totalNodes), Allocator.Persistent);
            _externalRotation = new NativeArray<quaternion>(math.max(1, extTf.Count), Allocator.Persistent);
            _writeNodeMap = new NativeArray<int>(math.max(1, writeNodeMap.Count), Allocator.Persistent);
            for (int i = 0; i < writeNodeMap.Count; i++) _writeNodeMap[i] = writeNodeMap[i];

            _allTransforms = new TransformAccessArray(allTf.ToArray());
            _allTfArray = allTf.ToArray(); // [LOD] bản sao thường, dùng bởi ResetInstancePositions()
            _writeTransforms = new TransformAccessArray(writeTf.ToArray());
            // [PERF fix #3] không còn TransformAccessArray cho external parent — chỉ giữ mảng
            // Transform thường để đọc rotation trên main thread mỗi frame (xem ScheduleFrame()).
            _externalParentTf = extTf.ToArray();

            // [LOD] Reset trạng thái LOD theo số instance mới — mất "lịch sử đóng băng" cũ là
            // chấp nhận được vì rebuild (thêm/xoá nhân vật, đổi rootBones) vốn đã là sự kiện hiếm.
            _lodState = new LodState[_registered.Count];

            var validColliderTf = new List<Transform>();
            for (int i = 0; i < perInstanceColliderTf.Count; i++)
                validColliderTf.Add(perInstanceColliderTf[i]); // đã lọc null ở phía DynamicBone (compacted list)
            _colliderTransforms = new TransformAccessArray(validColliderTf.ToArray());

            //_paramsDirty = true;
#if UNITY_EDITOR
            _anyShowGizmos = false;
            for (int i = 0; i < _registered.Count; i++)
                if (_registered[i] != null && _registered[i].showGizmos) { _anyShowGizmos = true; break; }
#endif

            if (debugLogging)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[DynamicBoneManager] Rebuild: {_registered.Count} instance(s), " +
                               $"{totalNodes} node(s), {chainList.Count} chain(s) in {groupList.Count} group(s), " +
                               $"{linkList.Count} cross link(s), {totalColliders} collider(s).");
                for (int i = 0; i < _registered.Count; i++)
                {
                    DynamicBone b = _registered[i];
                    InstanceRange r = _ranges[i];
                    sb.AppendLine($"  [{i}] '{b.name}' — nodes {r.nodeOffset}..{r.nodeOffset + r.nodeCount} " +
                                   $"({r.nodeCount}), chains {r.chainCount}, colliders {r.colliderCount}");
                }
                Debug.Log(sb.ToString());
            }
        }

        private void DisposeAll()
        {
            if (_nodes.IsCreated) _nodes.Dispose();
            if (_chains.IsCreated) _chains.Dispose();
            if (_groups.IsCreated) _groups.Dispose();
            if (_links.IsCreated) _links.Dispose();
            if (_params.IsCreated) _params.Dispose();
            if (_colliderConfigs.IsCreated) _colliderConfigs.Dispose();
            if (_colliderData.IsCreated) _colliderData.Dispose();
            if (_colliderActive.IsCreated) _colliderActive.Dispose();
            if (_colliderPrevAnchor.IsCreated) _colliderPrevAnchor.Dispose();
            if (_colliderHasPrevAnchor.IsCreated) _colliderHasPrevAnchor.Dispose();
            if (_readPosition.IsCreated) _readPosition.Dispose();
            if (_readRotation.IsCreated) _readRotation.Dispose();
            if (_outRotation.IsCreated) _outRotation.Dispose();
            if (_writeMode.IsCreated) _writeMode.Dispose();
            if (_externalRotation.IsCreated) _externalRotation.Dispose();
            if (_writeNodeMap.IsCreated) _writeNodeMap.Dispose();
            if (_allTransforms.isCreated) _allTransforms.Dispose();
            if (_writeTransforms.isCreated) _writeTransforms.Dispose();
            if (_colliderTransforms.isCreated) _colliderTransforms.Dispose();
            _externalParentTf = System.Array.Empty<Transform>();
            _allTfArray = System.Array.Empty<Transform>(); // [LOD] managed array, không cần Dispose()
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                if (_hasPending) _pendingHandle.Complete();
                DisposeAll();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            if (_hasPending) _pendingHandle.Complete();
            DisposeAll();
        }
    }
}