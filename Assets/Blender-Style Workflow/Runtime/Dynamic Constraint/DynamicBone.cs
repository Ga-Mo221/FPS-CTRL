using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    // ─────────────────────────────────────────────────────────────
    //  COMPONENT
    //  [KIẾN TRÚC MỚI] Component này KHÔNG còn tự mô phỏng vật lý trong LateUpdate() của
    //  chính nó. Nó chỉ giữ CẤU HÌNH (Inspector) + dữ liệu cây xương local, rồi đăng ký
    //  với DynamicBoneManager — nơi TOÀN BỘ DynamicBone trong scene được gộp lại và mô
    //  phỏng bằng Burst job đa luồng trong 1 lượt duy nhất mỗi frame. Xem DynamicBoneManager.cs.
    // ─────────────────────────────────────────────────────────────
    [AddComponentMenu("Dynamic Constraint")]
    [DisallowMultipleComponent]
    public partial class DynamicBone : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  BONES & COLLIDERS
        // ══════════════════════════════════════════════════════════
        [Tooltip("Danh sách các xương gốc muốn áp dụng hiệu ứng động (Tóc, đuôi, áo, v.v.)")]
        public List<Transform> rootBones = new List<Transform>();

        [Tooltip("Bật/Tắt tính năng va chạm với Collider.")]
        public bool enableColliders = false;

        [Tooltip("Bán kính 'độ dày' vật lý của từng particle khi giải va chạm — cộng thêm vào bán " +
                 "kính collider thật (radiusCollider + particleRadius), KHÔNG phải kích thước hiển " +
                 "thị của mesh tóc/vải. Đây là field của DynamicBone (không phải của Collider), nên " +
                 "1 collider dùng chung nhiều DynamicBone có thể mỗi bên 'dày' khác nhau.\n\n" +
                 "- Quá nhỏ (gần 0): tóc/vải trông như xuyên thấu qua da/vai vì chạm sát vào tâm " +
                 "collider mới bị chặn.\n" +
                 "- Quá lớn: chain bị đẩy bật ra xa collider bất thường ngay cả khi nhìn chưa chạm " +
                 "tới, hoặc kẹt cứng ở khe hẹp không lọt qua được.\n" +
                 "Nên set gần với bán kính lọn tóc/dải vải thật trong mesh, canh chỉnh trực quan " +
                 "bằng gizmo (Show Gizmos) khi Enable Colliders bật.")]
        [Range(0f, 0.5f)]
        public float particleRadius = 0.05f;

        public enum ColliderShapeType { Sphere, Capsule, Plane, Box }

        [Tooltip("Danh sách collider dùng để cản xương. Mỗi phần tử là 1 component DynamicBoneCollider " +
                 "ĐỘC LẬP gắn trực tiếp lên Transform của bone (VD vai, hông, cổ...) — kéo GameObject " +
                 "chứa component đó vào đây. Cùng 1 collider có thể được kéo vào NHIỀU DynamicBone khác " +
                 "nhau (nhiều chain tóc/khăn/đuôi cùng va vào 1 collider) mà không cần khai báo lại. Xem " +
                 "DynamicBoneCollider.cs.")]
        public List<DynamicBoneCollider> colliders = new List<DynamicBoneCollider>();

        // ══════════════════════════════════════════════════════════
        //  MANUAL BONE CACHING (loại trừ thủ công)
        //  [KIẾN TRÚC MỚI] BuildBoneTree() vẫn tự động duyệt ĐỆ QUY toàn bộ children của
        //  rootBones (để không bắt người dùng phải kéo tay từng xương con), nhưng danh sách
        //  dưới đây cho phép LOẠI TRỪ những Transform không phải bone thật (VD: phụ kiện,
        //  GameObject trang trí, bone kỹ thuật...) bị dính vào do nằm chung hierarchy với
        //  bone tóc/đuôi/áo. Loại 1 Transform ở đây sẽ loại LUÔN toàn bộ subtree bên dưới nó
        //  (không cần liệt kê từng con cháu). Danh sách này được SERIALIZE cùng component nên
        //  chỉ cần chỉnh 1 lần trong Editor (qua nút "Cache Bones"), runtime sau đó (kể cả
        //  trong build thật) sẽ luôn tôn trọng lựa chọn này khi InitializeTrees() chạy lại,
        //  không cần người chơi/Editor phải bấm nút mỗi lần bật scene.
        // ══════════════════════════════════════════════════════════
        [HideInInspector]
        public List<Transform> excludedBones = new List<Transform>();

        // ══════════════════════════════════════════════════════════
        //  CROSS LINKS — giữ liên kết giữa các chain kề nhau (VD lá cờ, rèm, tà áo nhiều dải)
        // ══════════════════════════════════════════════════════════
        public enum ChainLinkMode { Off, Adjacent, Loop }

        [Tooltip("Nối các chain KỀ NHAU trong danh sách Root Bones bằng ràng buộc khoảng cách để chúng không " +
                 "bị tách rời/xé toạc khi mô phỏng hoặc va chạm (lá cờ, rèm, tà áo...).\n\n" +
                 "- Off: mỗi chain hoàn toàn độc lập (hành vi cũ).\n" +
                 "- Adjacent: chain i nối với chain i+1 (A-B-C).\n" +
                 "- Loop: như Adjacent và nối thêm chain cuối với chain đầu (A-B-C-A) — cho vật khép kín/hình trụ.\n\n" +
                 "YÊU CẦU: thứ tự Root Bones phải trùng thứ tự liền kề thật của các chain trong không gian. " +
                 "Bone cùng độ sâu ở 2 chain kề nhau được nối với nhau; bone thừa (chain dài hơn) không được nối.")]
        public ChainLinkMode chainLinkMode = ChainLinkMode.Off;

        [Tooltip("Độ cứng liên kết: phần sai lệch khoảng cách được sửa mỗi vòng lặp.\n" +
                 "0 = không sửa; 1 = kéo cứng về đúng khoảng cách ban đầu; giá trị vừa (~0.5) cho vải mềm.")]
        [Range(0f, 1f)] public float linkStiffness = 0.5f;

        [Tooltip("Số vòng lặp giải liên kết mỗi frame (mỗi vòng: link → va chạm → ép độ dài với parent). " +
                 "Nhiều vòng hơn = liên kết cứng/ổn định hơn nhưng tốn CPU hơn (chỉ với component bật Cross Links).")]
        [Range(1, 8)] public int linkIterations = 3;

        [Tooltip("Số vòng lặp giải va chạm mỗi particle mỗi frame (kiểu PBD: đẩy ra khỏi collider → " +
                 "ép lại đúng chiều dài chain với parent → lặp lại). Nhiều vòng hơn giúp particle " +
                 "hội tụ đúng vị trí hợp lệ khi có NHIỀU collider chồng lấn hoặc particle di chuyển " +
                 "nhanh xuyên qua khe hẹp giữa 2 collider — 1 vòng thường không đủ để thỏa cả 2 ràng " +
                 "buộc cùng lúc, gây hiện tượng particle 'dính' lơ lửng sai vị trí 1-2 frame.\n\n" +
                 "- 1: rẻ nhất, đủ dùng khi chỉ va 1 collider đơn giản (VD chain tóc chỉ chạm 1 " +
                 "collider cổ).\n" +
                 "- 2 (mặc định): cân bằng tốt cho đa số trường hợp nhiều collider vừa phải (vai + " +
                 "cổ + ngực).\n" +
                 "- 3-4: dùng khi thấy chain vẫn xuyên/kẹt collider ở giá trị thấp hơn, đặc biệt " +
                 "hierarchy nhiều collider chồng lấn gần nhau — đổi lại tốn CPU hơn theo đúng tỉ lệ " +
                 "(x2 iterations ≈ x2 chi phí bước giải va chạm cho MỌI particle có bật collider).")]
        [Range(1, 4)]
        public int collisionSolveIterations = 2;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS
        // ══════════════════════════════════════════════════════════
        [Tooltip("Độ 'nhớt' làm tắt dần chuyển động của particle — giá trị càng cao, bone càng " +
                 "nhanh dừng đung đưa và bám sát rest-pose. Tách riêng 2 thành phần vận tốc mỗi " +
                 "frame: velTangential (vuông góc với chain, gây lắc qua-lại) bị hãm mạnh hơn " +
                 "(damping*15*dt) so với velRadial (dọc theo chain, gây co-giãn) chỉ hãm " +
                 "damping*5*dt — vì thực tế mắt người nhạy với rung lắc ngang hơn co giãn dọc.\n\n" +
                 "- 0: gần như không hãm gì → tóc/đuôi rung mãi không dừng, dễ bị giật/nổ vật lý.\n" +
                 "- 0.8 (mặc định): tắt dao động nhanh, hợp cho đa số tóc/váy/đuôi.\n" +
                 "- 1: gần như đứng hình ngay khi có lực tác động, chỉ còn xê dịch rất nhỏ — hợp " +
                 "phụ kiện cứng (dây chuyền kim loại, giáp) hơn là tóc mềm.")]
        [Range(0f, 1f)] public float damping = 0.8f;

        [Tooltip("Mức độ chain 'phản ứng ngược' lại chuyển động ĐỘT NGỘT của node cha (nhân vật " +
                 "quay đầu nhanh, chạy, nhảy...). Công thức: inertiaForce = -parentDelta * inertia, " +
                 "tức khi cha di chuyển 1 đoạn trong 1 frame, particle bị 1 lực ĐẨY NGƯỢC hướng " +
                 "tỉ lệ với inertia — tạo cảm giác 'trễ nhịp' tự nhiên như tóc/đuôi bị bỏ lại phía " +
                 "sau rồi mới đuổi kịp, thay vì bám cứng theo cha ngay lập tức.\n\n" +
                 "- 0: particle bỏ qua hoàn toàn chuyển động của cha, chỉ chịu gravity/spring → " +
                 "chain bám theo cha 'quá nhanh/cứng', mất cảm giác trọng lượng.\n" +
                 "- 0.8 (mặc định): độ trễ rõ, tự nhiên cho tóc dài/đuôi/tà áo.\n" +
                 "- 1 hoặc cao hơn phạm vi thường dùng: chain 'vẫy' rất mạnh mỗi khi cha đổi hướng, " +
                 "dễ overshoot/rung nếu animation gốc chuyển động giật cục.")]
        [Range(0f, 1f)] public float inertia = 0.8f;

        [Tooltip("Khi 1 particle bị collider đẩy ra (va chạm), phần lực đẩy đó có LAN NGƯỢC lên " +
                 "các node CHA trong cùng chain hay không, và lan bao nhiêu. Mỗi bậc cha, độ lớn " +
                 "correction bị nhân thêm 1 lần falloff (correction *= falloff) rồi mới cộng vào vị " +
                 "trí cha — nên càng lên cao trong chain, ảnh hưởng càng yếu dần theo cấp số nhân.\n\n" +
                 "- 0: chỉ đúng particle va chạm bị đẩy, các cha phía trên đứng yên → dễ thấy chain " +
                 "bị 'gãy khúc' đột ngột tại điểm va chạm khi collider lớn/đẩy mạnh.\n" +
                 "- 0.5 (mặc định): lan nhẹ lên vài node cha gần nhất, chain uốn mượt quanh " +
                 "collider thay vì gãy khúc.\n" +
                 "- 1: lan gần như nguyên vẹn lên toàn bộ cha phía trên → cả chain có thể bị đẩy " +
                 "lệch nhiều chỉ vì 1 điểm va chạm nhỏ ở ngọn, dễ trông giả nếu chain dài.")]
        [Range(0f, 1f)] public float collisionParentFalloff = 0.5f;

        [Tooltip("Hệ số 'độ cứng' của lò xo — nhân TRỰC TIẾP với Spring Force Multiplier bên dưới " +
                 "để ra 1 số mũ duy nhất (springStiffness * springForceMultiplier * dt) dùng cho " +
                 "công thức springT = 1 - e^(-số mũ). Hai field này KHÔNG có vai trò độc lập, tách " +
                 "làm 2 chỉ để tiện kéo thô (Multiplier, range rộng 0-500) và tinh (Stiffness, " +
                 "range 0-1) — cùng 1 kết quả có thể đạt bằng nhiều tổ hợp khác nhau.")]
        [Range(0f, 1f)] public float springStiffness = 0.8f;

        [Tooltip("Hệ số nhân biên độ cho lực lò xo — xem tooltip Spring Stiffness ở trên, 2 field " +
                 "này nhân trực tiếp với nhau.")]
        [Range(0f, 500f)] public float springForceMultiplier = 20f;

        [Tooltip("Giảm dần ảnh hưởng của spring từ GỐC (depth=0) tới NGỌN (depth=1) chain - tương tự " +
                 "depthWeight của Wind, nhưng ở đây spring MẠNH ở gốc và YẾU dần ở ngọn (ngược với " +
                 "wind).\n\n" +
                 "- 0 (mặc định): giữ NGUYÊN hành vi cũ - spring áp ĐỀU 100% weight cho mọi particle " +
                 "trong chain bất kể depth.\n" +
                 "- 1: hệ số spring tại particle bị nhân thêm (1-depth)² - gốc gần như không đổi " +
                 "(depth≈0 → hệ số≈1), nhưng ngọn chain (depth=1) gần như KHÔNG còn spring nào " +
                 "(hệ số≈0) → gravity/inertia được toàn quyền kéo phần ngọn rũ tự nhiên, chỉ gốc " +
                 "được 'ghim' cứng theo bind pose.\n" +
                 "- Giá trị giữa: nội suy tuyến tính giữa 2 hành vi trên.\n\n" +
                 "Công thức: normalizedDepth = depth / maxDepth (0..1 trong chain); " +
                 "quadratic = (1 - normalizedDepth)²; depthFactor = Lerp(1, quadratic, depthFalloff). " +
                 "depthFactor nhân vào springForceMultiplier trước khi tính springT — " +
                 "nên 1 giá trị depthFalloff duy nhất chi phối nhất quán toàn bộ lực spring.")]
        [Range(0f, 1f)] public float depthFalloff = 0f;

        [Tooltip("Vector trọng lực world-space áp cho MỌI particle trong chain, cộng thẳng vào gia " +
                 "tốc mỗi frame (effectiveGravity * dt²). Mặc định (0, -9.81, 0) là trọng lực Trái " +
                 "Đất chuẩn theo trục Y — với nhân vật tỉ lệ nhỏ/lớn khác thường hoặc muốn hiệu ứng " +
                 "cường điệu (tóc rũ mạnh hơn thực tế cho phong cách anime, hoặc gió thổi ngang thay " +
                 "vì rơi xuống) có thể chỉnh độ lớn/hướng tùy ý — không bắt buộc phải là (0,-9.81,0). " +
                 "Nếu Use Rest Pose Gravity bật, HƯỚNG của vector này (không phải độ lớn) còn được " +
                 "dùng để tính rest-pose gravity bên dưới.")]
        public Vector3 gravity = new Vector3(0, -9.81f, 0);

        [Tooltip("Bật: pha trộn thêm 1 thành phần gravity 'giả' hướng theo rest-pose (bind pose lúc " +
                 "InitializeTrees()) thay vì chỉ dùng đúng vector Gravity world tuyệt đối — giúp " +
                 "tóc/đuôi rũ đúng hướng bind pose ban đầu (VD tóc buộc chếch sang 1 bên) thay vì " +
                 "luôn bị kéo thẳng xuống dưới bất kể pose gốc được tạo hình thế nào — đặc biệt quan " +
                 "trọng khi bind pose không phải tư thế đứng thẳng chuẩn (T-pose/A-pose), hoặc chain " +
                 "vốn hướng ngang/lên trên theo thiết kế (VD tai thú, đuôi cong lên).\n\n" +
                 "Tắt: dùng đúng 100% vector Gravity world thật, mọi chain trên mọi nhân vật đều bị " +
                 "kéo cùng 1 hướng tuyệt đối — đơn giản, dễ đoán hơn nhưng có thể trông sai hướng " +
                 "với chain không thẳng đứng.")]
        public bool useRestPoseGravity = true;

        [Tooltip("Chỉ có tác dụng khi Use Rest Pose Gravity bật. Tỉ lệ pha trộn giữa gravity world " +
                 "thật và gravity 'giả' theo rest-pose: effectiveGravity = Lerp(gravity world, " +
                 "gravity theo hướng rest-pose, restPoseGravityBlend).\n\n" +
                 "- 0: tương đương tắt Use Rest Pose Gravity — dùng nguyên gravity world.\n" +
                 "- 0.6 (mặc định): thiên về rest-pose nhưng vẫn giữ phần nào lực rơi thật.\n" +
                 "- 1: bỏ hoàn toàn hướng gravity world, chain chỉ 'rũ' theo đúng hướng đã bind — " +
                 "hợp khi muốn chain luôn giữ dáng gốc dù nhân vật lộn ngược/nằm nghiêng (VD " +
                 "nhân vật trong game không trọng lực, hoặc animation nhào lộn nhiều).")]
        [Range(0f, 1f)] public float restPoseGravityBlend = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  LOD & CULLING
        // ══════════════════════════════════════════════════════════
        [Tooltip("Renderer dùng để kiểm tra visibility (Renderer.isVisible — do hệ thống culling " +
                 "camera của Unity tính sẵn, gần như miễn phí để đọc). Để trống sẽ tự tìm " +
                 "SkinnedMeshRenderer/Renderer trên chính GameObject, cha, hoặc con lúc OnEnable. " +
                 "Dùng bởi DynamicBoneManager.ApplyLodAndCulling() để đóng băng mô phỏng khi nhân " +
                 "vật ngoài khung hình hoặc quá xa camera — xem simulationLod* trên Manager.")]
        public Renderer cullingRenderer;

        // ══════════════════════════════════════════════════════════
        //  DEBUG / GIZMOS
        // ══════════════════════════════════════════════════════════
        public bool showGizmos = false;
        [Range(0.005f, 0.2f)] public float nodeGizmoSize = 0.03f;

        // ══════════════════════════════════════════════════════════
        //  LOCAL TREE DATA (xây 1 lần trong InitializeTrees(), sau đó được
        //  DynamicBoneManager copy vào bộ NativeArray toàn cục dùng chung).
        // ══════════════════════════════════════════════════════════
        private BoneNode[] _nodes = new BoneNode[0];
        private Transform[] _tf = new Transform[0];
        private Transform[] _rootParentTf = new Transform[0];
        private int[] _rootIndices = new int[0];
        private int _nodeCount;

        // Link cục bộ (index node LOCAL của component này) — manager cộng offset khi copy sang buffer toàn cục.
        private NodeLink[] _links = new NodeLink[0];
        public int LinkCount => _links.Length;

        private DynamicBoneCollider[] _validColliders = new DynamicBoneCollider[0]; // đã lọc component null

        public int ActiveChainCount => _rootIndices.Length;

        /// <summary>
        /// Danh sách Transform đã được cache vào cây mô phỏng ở lần InitializeTrees() gần nhất
        /// (đã áp dụng excludedBones). Chỉ dùng để HIỂN THỊ/preview trong Editor (nút "Cache
        /// Bones") — không dùng cho mô phỏng (mô phỏng dùng thẳng _tf/JobTransforms nội bộ).
        /// </summary>
        public IReadOnlyList<Transform> CachedBoneTransforms => _tf;

        /// <summary>
        /// Loại 1 Transform (và toàn bộ subtree bên dưới) ra khỏi cây mô phỏng, rồi build lại
        /// cây ngay để Editor/Gizmo thấy kết quả tức thì. Gọi từ nút [X] trong Inspector.
        /// </summary>
        public void ExcludeBone(Transform t)
        {
            if (t == null || excludedBones.Contains(t)) return;
            excludedBones.Add(t);
            InitializeTrees();
        }

        /// <summary>Đưa 1 Transform đã bị loại trừ trở lại cây mô phỏng (nút [+] trong Inspector).</summary>
        public void IncludeBone(Transform t)
        {
            if (excludedBones.Remove(t))
                InitializeTrees();
        }

        /// <summary>Xoá toàn bộ danh sách loại trừ, coi như chưa từng loại bone nào.</summary>
        public void ClearExclusions()
        {
            if (excludedBones.Count == 0) return;
            excludedBones.Clear();
            InitializeTrees();
        }

        // ── Truy cập cho DynamicBoneManager ─────────────────────────────
        internal int JobNodeCount => _nodeCount;
        internal BoneNode[] JobNodes => _nodes;
        internal Transform[] JobTransforms => _tf;
        internal int[] JobRootIndices => _rootIndices;
        internal Transform[] JobRootParentTf => _rootParentTf;
        internal int JobColliderCount => _validColliders.Length;
        internal NodeLink[] JobLinks => _links;

        internal Transform GetColliderTransformForActiveCheck(int localIndex) => _validColliders[localIndex].transform;

        internal BoneParams BuildJobParams(int colliderOffset, int colliderCount)
        {
            return new BoneParams
            {
                damping = damping,
                inertia = inertia,
                collisionParentFalloff = collisionParentFalloff,
                springStiffness = springStiffness,
                springForceMultiplier = springForceMultiplier,
                depthFalloff = depthFalloff,
                gravity = gravity,
                useRestPoseGravity = (byte)(useRestPoseGravity ? 1 : 0),
                restPoseGravityBlend = restPoseGravityBlend,
                enableColliders = (byte)(enableColliders ? 1 : 0),
                particleRadius = particleRadius,
                collisionSolveIterations = collisionSolveIterations,
                colliderStart = colliderOffset,
                colliderCount = colliderCount,
                linkStiffness = linkStiffness,
                linkIterations = linkIterations
            };
        }

        internal ColliderConfigData BuildColliderConfigData(int localIndex)
        {
            // Đọc size/offset/type từ chính DynamicBoneCollider component (nguồn dữ liệu duy nhất);
            // particleRadius là của DynamicBone SỞ HỮU chain này (không phải của collider), nên gán
            // đè ở đây — cho phép 1 collider dùng chung giữa nhiều DynamicBone có particleRadius khác nhau.
            ColliderConfigData data = _validColliders[localIndex].ToColliderConfigData();
            data.particleRadius = particleRadius;
            return data;
        }

#if UNITY_EDITOR
        internal void CopyGizmoSnapshot(NativeArray<BoneNode> global, int offset, int count)
        {
            if (_nodes.Length != count) return;
            for (int i = 0; i < count; i++)
            {
                BoneNode n = _nodes[i];
                BoneNode src = global[offset + i];
                n.position = src.position;
                n.prevPosition = src.prevPosition;
                _nodes[i] = n;
            }
        }
#endif

#if UNITY_EDITOR
        // [FIX] Cache Bones (_nodes/_tf/_links...) là dữ liệu runtime KHÔNG được serialize, và component
        // không chạy ở Edit mode → sau Play / recompile script / mở lại scene, cache ở Edit mode bị mất
        // (list "Chưa cache bone nào", gizmo biến mất) dù excludedBones vẫn còn. OnValidate được Unity gọi
        // sau mỗi lần deserialize (thoát Play, reload domain, mở scene, đổi field) → dựng lại cây ở Edit mode.
        // Dùng delayCall vì không được đọc Transform/hierarchy an toàn ngay trong OnValidate.
        private bool _editRebuildQueued;
        private void OnValidate()
        {
            if (Application.isPlaying || _editRebuildQueued) return;
            _editRebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _editRebuildQueued = false;
                if (this == null || Application.isPlaying) return;
                if (!gameObject.scene.IsValid()) return; // bỏ qua prefab asset chưa nằm trong scene
                InitializeTrees();
            };
        }
#endif

        private void OnEnable()
        {
            InitializeTrees();
            if (cullingRenderer == null)
            {
                // Tự tìm Renderer gần nhất để phục vụ LOD/Culling nếu người dùng chưa gán tay —
                // ưu tiên GetComponentInParent (thường là SkinnedMeshRenderer của cả nhân vật),
                // fallback sang children nếu không có (VD component đặt ngay trên root nhân vật).
                cullingRenderer = GetComponentInParent<Renderer>();
                if (cullingRenderer == null) cullingRenderer = GetComponentInChildren<Renderer>();
            }
            DynamicBoneManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instanceExists) DynamicBoneManager.Instance.Unregister(this);
        }

        // Tránh tạo Manager (và GameObject DontDestroyOnLoad của nó) khi domain đang unload / thoát Play.
        private static bool _instanceExists = true;
        private void OnApplicationQuit() => _instanceExists = false;
    }
}