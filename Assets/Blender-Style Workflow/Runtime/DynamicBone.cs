using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    // ─────────────────────────────────────────────────────────────
    //  ENUM PRESET
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Chọn kiểu chất liệu/vật lý để tự động nạp bộ thông số phù hợp.
    /// </summary>
    public enum ClothPreset
    {
        /// <summary>Vải mềm – mượt, chảy tự nhiên, quán tính lớn.</summary>
        SoftFabric,

        /// <summary>Lò xo – nảy, đàn hồi nhanh về rest pose.</summary>
        Spring,

        /// <summary>Áo cứng giữ form – ít dao động, giữ shape gần như nguyên.</summary>
        RigidForm,

        /// <summary>Tùy chỉnh hoàn toàn – không ghi đè thông số.</summary>
        Custom,
    }

    // ─────────────────────────────────────────────────────────────
    //  COMPONENT
    // ─────────────────────────────────────────────────────────────
    [AddComponentMenu("Dynamic Constraint")]
    [DisallowMultipleComponent]
    public class DynamicBone : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  PRESET
        // ══════════════════════════════════════════════════════════
        [Tooltip("Bộ thông số dựng sẵn. Chọn Custom để tự chỉnh tay hoàn toàn.")]
        public ClothPreset clothPreset = ClothPreset.SoftFabric;

        // ══════════════════════════════════════════════════════════
        //  BONES & COLLIDERS
        // ══════════════════════════════════════════════════════════
        [Tooltip("Danh sách các xương gốc muốn áp dụng hiệu ứng động (Tóc, đuôi, áo, v.v.)")]
        public List<Transform> rootBones = new List<Transform>();

        [Tooltip("Bật/Tắt tính năng va chạm với Collider.")]
        public bool enableColliders = true;

        [Tooltip("Bán kính vùng va chạm của từng hạt xương (Radius).")] [Range(0f, 0.5f)]
        public float particleRadius = 0.05f;

        [Tooltip("Danh sách SphereCollider / CapsuleCollider dùng để cản xương.")]
        public List<Collider> colliders = new List<Collider>();

        [Tooltip("[B4] Số vòng lặp giải va chạm mỗi frame. 1 = single-pass (kết quả có thể phụ " +
                 "thuộc thứ tự trong danh sách Colliders khi 2 collider chồng nhau). 2-3 giúp " +
                 "particle hội tụ ra ngoài TẤT CẢ collider ổn định hơn, giảm phụ thuộc thứ tự " +
                 "add collider vào list — đổi lại tốn thêm CPU tuyến tính theo số vòng lặp.")]
        [Range(1, 4)]
        public int collisionSolveIterations = 2;

        // ══════════════════════════════════════════════════════════
        //  CLOTH LINKS (nối ngang giữa các chain)
        // ══════════════════════════════════════════════════════════
        [Tooltip("Tự động nối các xương cùng cấp giữa các Root Bone kề nhau để tạo mặt vải liền, " +
                 "tránh các dải bị tách rời khi chạy. Cần ít nhất 2 Root Bone.\n" +
                 "Thứ tự nối đi theo đúng thứ tự trong danh sách Root Bones (trái → phải).")]
        public bool enableClothLinks = false;

        [Tooltip("Nối luôn chain cuối cùng ngược về chain đầu tiên, tạo thành vòng khép kín " +
                 "(dùng cho váy, ống tay, tà tròn).")]
        public bool clothLinkClosedLoop = false;

        [Tooltip("Độ cứng của liên kết ngang (0→1). 1 = giữ nguyên khoảng cách gốc giữa hai dải vải.")] [Range(0f, 1f)]
        public float clothLinkStiffness = 0.8f;

        [Tooltip("Số vòng lặp giải liên kết mỗi frame. Cao hơn = vải căng ổn định hơn nhưng tốn CPU hơn.")]
        [Range(1, 8)]
        public int clothLinkIterations = 2;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS — DAMPING & INERTIA
        // ══════════════════════════════════════════════════════════
        [Tooltip("Độ cản/hãm chuyển động (0→1). Tăng giúp xương nhanh dừng, tránh rung quá đà.")] [Range(0f, 1f)]
        public float damping = 0.2f;

        [Tooltip("Độ quán tính (0→1). Cao = khi di chuyển đột ngột xương con bị giật ngược mạnh.")] [Range(0f, 1f)]
        public float inertia = 0.8f;

        [Tooltip("Hệ số suy giảm collision truyền ngược lên parent (0→1).\n" +
                 "0.5 = mỗi cấp parent bị đẩy bằng 50% cấp con.\n" +
                 "0 = không truyền lên parent gì cả.\n" +
                 "1 = truyền nguyên vẹn lên mọi cấp (cứng như gỗ).")]
        [Range(0f, 1f)]
        public float collisionParentFalloff = 0.5f;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS — SPRING
        // ══════════════════════════════════════════════════════════
        [Tooltip("Độ đàn hồi – tốc độ kéo xương về vị trí ban đầu (0→1).")] [Range(0f, 1f)]
        public float elasticity = 0.1f;

        [Tooltip("Độ cứng lò xo. Cao + Damping thấp = nảy nhiều; cao + Damping cao = về nhanh không nảy.")]
        [Range(0f, 1f)]
        public float springStiffness = 0.3f;

        [Tooltip("Hệ số nhân lực đàn hồi. Tăng để xương bật mạnh hơn khi trở về rest pose.")] [Range(0f, 500f)]
        public float springForceMultiplier = 1f;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS — GRAVITY
        // ══════════════════════════════════════════════════════════
        [Tooltip("Trọng lực tác động lên toàn bộ các chuỗi xương.\n" +
                 "Nếu đổi HƯỚNG gravity lúc runtime ở preset Custom, gọi RefreshRestGravity() " +
                 "(rẻ hơn InitializeTrees()) để rest-pose-gravity cập nhật theo hướng mới — " +
                 "nếu không chỉ magnitude được dùng, hướng vẫn theo giá trị lúc InitializeTrees().")]
        public Vector3 gravity = new Vector3(0, -9.81f, 0);

        [Tooltip(
            "Bật chế độ giữ góc ban đầu (Rest Pose). Xương đàn hồi về đúng góc lúc setup thay vì bị kéo hoàn toàn bởi gravity.")]
        public bool useRestPoseGravity = false;

        [Tooltip("0 = gravity thẳng đứng thuần; 1 = chỉ giữ rest pose; 0.5 = kết hợp cả hai.")] [Range(0f, 1f)]
        public float restPoseGravityBlend = 1f;

        // ══════════════════════════════════════════════════════════
        //  DEBUG / GIZMOS
        // ══════════════════════════════════════════════════════════
        [Tooltip("Bật/Tắt hiển thị Gizmos debug trong Scene View.")]
        public bool showGizmos = true;

        [Tooltip("Kích thước hạt hình cầu Gizmo biểu diễn nút vật lý.")] [Range(0.005f, 0.2f)]
        public float nodeGizmoSize = 0.03f;

        [Tooltip("Tỷ lệ độ dài mũi tên Gizmo hiển thị hướng kéo.")] [Range(0.5f, 20f)]
        public float arrowLengthMultiplier = 5.0f;

        // ══════════════════════════════════════════════════════════
        //  PRESET LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tên hiển thị của từng preset (dùng cho Inspector).
        /// </summary>
        public static string GetPresetLabel(ClothPreset preset)
        {
            switch (preset)
            {
                case ClothPreset.SoftFabric: return "Vải Mềm";
                case ClothPreset.Spring: return "Lò Xo";
                case ClothPreset.RigidForm: return "Áo Cứng";
                case ClothPreset.Custom: return "Tùy Chỉnh";
                default: return preset.ToString();
            }
        }

        /// <summary>
        /// Mô tả ngắn gọn hành vi của từng preset (dùng cho Inspector).
        /// </summary>
        public static string GetPresetDescription(ClothPreset preset)
        {
            switch (preset)
            {
                case ClothPreset.SoftFabric:
                    return
                        "Vải mềm: mượt, chảy tự nhiên. Damping thấp, quán tính cao, spring yếu → xương lắc lơ lửng như lụa.";
                case ClothPreset.Spring:
                    return "Lò xo: nảy và đàn hồi nhanh về rest pose. Stiffness cao, damping trung bình → rung và bật.";
                case ClothPreset.RigidForm:
                    return
                        "Áo cứng giữ form: ít dao động, spring rất mạnh → shape gần như không thay đổi khi di chuyển.";
                case ClothPreset.Custom:
                    return "Chế độ tùy chỉnh: tất cả thông số do bạn chỉnh tay, preset không ghi đè.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Áp preset hiện tại (bỏ qua nếu đang ở chế độ Custom).
        /// </summary>
        public void ApplyCurrentPreset()
        {
            if (clothPreset != ClothPreset.Custom)
                ApplyPreset(clothPreset);
        }

        public void ApplyPreset(ClothPreset preset)
        {
            switch (preset)
            {
                // ── VẢI MỀM ──────────────────────────────────────
                // Mục tiêu: chảy lơ lửng, giật nhẹ, không nảy
                case ClothPreset.SoftFabric:
                    damping = 0.18f; // cản nhẹ → swing dài
                    elasticity = 0.05f; // đàn hồi rất yếu
                    springStiffness = 0.05f; // spring rất mềm
                    springForceMultiplier = 0.5f;
                    inertia = 0.85f; // quán tính lớn → kéo trễ nhiều
                    useRestPoseGravity = false;
                    gravity = new Vector3(0, -400f, 0);
                    enableClothLinks = true; // vải → nối ngang các dải cho liền mặt
                    clothLinkStiffness = 0.8f;
                    clothLinkIterations = 2;
                    break;

                // ── LÒ XO ────────────────────────────────────────
                // Mục tiêu: nảy về rest pose nhanh, rung đàn hồi
                case ClothPreset.Spring:
                    damping = 0.25f; // cản vừa → có một chút nảy
                    elasticity = 0.35f; // đàn hồi khá mạnh
                    springStiffness = 0.55f; // spring mạnh
                    springForceMultiplier = 20f; // nhân lực lớn → bật về mạnh
                    inertia = 0.5f; // quán tính trung bình
                    useRestPoseGravity = true;
                    restPoseGravityBlend = 0.6f;
                    gravity = new Vector3(0, -9.81f, 0);
                    enableClothLinks = false; // lò xo → mỗi chain dao động độc lập
                    break;

                // ── ÁO CỨNG GIỮ FORM ─────────────────────────────
                // Mục tiêu: giữ nguyên shape, rung tối thiểu
                case ClothPreset.RigidForm:
                    damping = 0.6f; // cản rất mạnh → tắt dao động nhanh
                    elasticity = 0.5f; // đàn hồi cao
                    springStiffness = 0.85f; // spring cứng → shape không đổi
                    springForceMultiplier = 3.0f; // nhân lực rất lớn
                    inertia = 0.2f; // quán tính thấp → ít bị giật
                    useRestPoseGravity = true;
                    restPoseGravityBlend = 1.0f; // hoàn toàn giữ rest pose
                    gravity = new Vector3(0, -2f, 0); // gravity yếu hơn
                    enableClothLinks = true; // giữ form → liên kết ngang rất cứng
                    clothLinkStiffness = 1.0f;
                    clothLinkIterations = 3;
                    break;
            }

            // Reinit trong runtime khi đổi preset
            if (Application.isPlaying) InitializeTrees();
        }

        // ══════════════════════════════════════════════════════════
        //  RUNTIME INTERNALS
        // ══════════════════════════════════════════════════════════
        private class BoneNode
        {
            public Transform transform;
            public Vector3 position;
            public Vector3 prevPosition;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public float length;
            public BoneNode parent;
            public List<BoneNode> children = new List<BoneNode>();

            public Vector3 restGravityLocal;
            public Vector3 restDirectionLocal;
        }

        /// <summary>
        /// Một liên kết ngang giữa hai node cùng cấp thuộc hai chain kề nhau.
        /// </summary>
        private class ClothLink
        {
            public BoneNode a;
            public BoneNode b;
            public float restLength;
        }

        // [0.3][A4] Cache phần BẤT BIẾN của mỗi CapsuleCollider (không phụ thuộc transform
        // world, chỉ phụ thuộc thiết lập local của collider: direction/radius/height).
        // Mỗi frame chỉ còn phải tính lại phần world-space (center, axis, scale) thay vì
        // làm lại toàn bộ ternary + property access cho từng particle x collider.
        private struct CapsuleLocalData
        {
            public Vector3 localAxis;     // Vector3.right / .up / .forward tuỳ capsule.direction
            public float radiusUnscaled;  // capsule.radius (chưa nhân lossyScale)
            public float heightUnscaled;  // capsule.height (chưa nhân lossyScale)
        }

        private readonly Dictionary<CapsuleCollider, CapsuleLocalData> _capsuleCache =
            new Dictionary<CapsuleCollider, CapsuleLocalData>();

        private CapsuleLocalData GetCapsuleLocalData(CapsuleCollider capsule)
        {
            if (_capsuleCache.TryGetValue(capsule, out var cached))
                return cached;

            var data = new CapsuleLocalData
            {
                localAxis = capsule.direction == 0 ? Vector3.right :
                    capsule.direction == 2 ? Vector3.forward : Vector3.up,
                radiusUnscaled = capsule.radius,
                heightUnscaled = capsule.height
            };
            _capsuleCache[capsule] = data;
            return data;
        }

        private List<BoneNode> rootNodes = new List<BoneNode>();
        private List<ClothLink> clothLinks = new List<ClothLink>();

        // [1.3][A3] Tập tối thiểu các node cần EnforceBoneLength khi giải cloth links —
        // xem giải thích chi tiết tại ComputeClothEnforcementRoots().
        private List<BoneNode> _clothEnforcementRoots = new List<BoneNode>();
        private readonly HashSet<BoneNode> _clothTouchedScratch = new HashSet<BoneNode>();

        private Vector3 dragDirection = Vector3.zero;
        private Vector3 lastManagerPosition;

        /// <summary>Số chuỗi xương đã khởi tạo (Inspector dùng để hiển thị runtime info).</summary>
        public int ActiveChainCount => rootNodes.Count;

        /// <summary>Số liên kết ngang đã dựng giữa các chain.</summary>
        public int ClothLinkCount => clothLinks.Count;

        void Start()
        {
            //ApplyCurrentPreset();

            InitializeTrees();
            lastManagerPosition = transform.position;
        }

        public void InitializeTrees()
        {
            _capsuleCache.Clear();
            rootNodes.Clear();
            foreach (Transform root in rootBones)
            {
                if (root != null)
                    rootNodes.Add(BuildBoneTree(root, null));
            }

            BuildClothLinks();
        }

        /// <summary>
        /// [0.5][B3] Cập nhật lại restGravityLocal của toàn bộ node theo hướng gravity hiện tại,
        /// KHÔNG rebuild lại cây/cloth links. Gọi hàm này sau khi đổi HƯỚNG gravity lúc runtime
        /// (ví dụ preset Custom, hoặc gameplay logic thay đổi gravity theo trạng thái nhân vật).
        /// Rẻ hơn nhiều so với gọi lại InitializeTrees() vì không rebuild tree/cloth links.
        /// Nếu không gọi hàm này, restGravityLocal vẫn giữ hướng cũ (chỉ magnitude của gravity
        /// mới có hiệu lực ngay, hướng thì không).
        /// </summary>
        public void RefreshRestGravity()
        {
            foreach (var root in rootNodes)
                if (root != null)
                    RefreshRestGravityRecursive(root);
        }

        private void RefreshRestGravityRecursive(BoneNode node)
        {
            if (node.parent != null)
                node.restGravityLocal = node.parent.transform.InverseTransformDirection(gravity.normalized);

            foreach (var child in node.children)
                RefreshRestGravityRecursive(child);
        }

        // ══════════════════════════════════════════════════════════
        //  CLOTH LINKS — BUILD
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Dựng liên kết ngang giữa các chain kề nhau theo đúng thứ tự trong rootBones.
        /// Node được gom theo độ sâu (depth) tính từ root: depth 1 của chain A nối với
        /// depth 1 của chain B, depth 2 nối depth 2, v.v.
        ///
        /// Ví dụ A = [A1,A2,A3], B = [B1,B2,B3,B4], C = [C1,C2,C3] sẽ cho:
        ///   A1-B1, B1-C1 | A2-B2, B2-C2 | A3-B3, B3-C3 | B4 đứng một mình.
        ///
        /// Root bone (depth 0) bị ghim cứng vào Transform nên không nối — nối cũng vô nghĩa
        /// vì vị trí của nó bị ghi đè lại mỗi frame.
        /// </summary>
        private void BuildClothLinks()
        {
            clothLinks.Clear();
            _clothEnforcementRoots.Clear();

            if (!enableClothLinks || rootNodes.Count < 2) return;

            // Gom node theo depth cho từng chain
            var chains = new List<List<List<BoneNode>>>(rootNodes.Count);
            int maxDepth = 0;

            foreach (var root in rootNodes)
            {
                var levels = new List<List<BoneNode>>();
                GatherLevels(root, 0, levels);
                chains.Add(levels);
                if (levels.Count > maxDepth) maxDepth = levels.Count;
            }

            int chainCount = chains.Count;
            // Vòng khép kín cần nối thêm cặp (cuối → đầu), và chỉ có nghĩa khi >= 3 chain
            int pairCount = (clothLinkClosedLoop && chainCount >= 3) ? chainCount : chainCount - 1;

            // depth bắt đầu từ 1 vì depth 0 là root bone bị ghim
            for (int depth = 1; depth < maxDepth; depth++)
            {
                for (int i = 0; i < pairCount; i++)
                {
                    // Tìm chain kế tiếp (bên phải) còn node ở depth này.
                    // Chain ngắn hơn bị bỏ qua để hai bên của nó vẫn dính vào nhau,
                    // thay vì để mặt vải bị thủng một khoảng.
                    List<BoneNode> left = GetLevel(chains[i], depth);
                    if (left == null) continue;

                    List<BoneNode> right = null;
                    for (int step = 1; step < chainCount; step++)
                    {
                        int j = (i + step) % chainCount;
                        if (!clothLinkClosedLoop && j <= i) break; // không vòng lại khi mở

                        right = GetLevel(chains[j], depth);
                        if (right != null) break;
                    }

                    if (right == null) continue;

                    // Nếu chain có nhánh (nhiều node cùng depth) thì ghép theo chỉ số
                    int pairs = Mathf.Min(left.Count, right.Count);
                    for (int k = 0; k < pairs; k++)
                    {
                        BoneNode na = left[k];
                        BoneNode nb = right[k];
                        if (na == null || nb == null || na == nb) continue;

                        clothLinks.Add(new ClothLink
                        {
                            a = na,
                            b = nb,
                            restLength = Vector3.Distance(na.position, nb.position)
                        });
                    }
                }
            }

            ComputeClothEnforcementRoots();
        }

        /// <summary>
        /// [1.3][A3] Precompute tập NODE TỐI THIỂU cần gọi EnforceBoneLength() từ đó mỗi
        /// cloth iteration, thay vì luôn duyệt lại TOÀN BỘ cây từ rootNodes.
        ///
        /// Lý do đúng: ngay sau SimulatePhysics, MỌI node (kể cả không liên quan cloth link)
        /// đã thỏa length constraint với parent (SimulatePhysics tự enforce length khi tích
        /// hợp). SolveClothLinks() sau đó CHỈ dịch chuyển đúng những node là 2 đầu của 1
        /// ClothLink — các node khác hoàn toàn không bị đụng tới, position của chúng vẫn còn
        /// hợp lệ, nên không cần enforce lại.
        ///
        /// Với mỗi node bị "touched" (là link.a hoặc link.b của ít nhất 1 ClothLink), enforce
        /// phải chạy từ nó xuống TOÀN BỘ subtree con (vì con của nó vẫn đang neo theo vị trí
        /// CŨ của nó, giờ nó dịch chuyển → con phải cascade theo) — quy tắc này giữ nguyên,
        /// KHÔNG bị rút gọn. Cái được rút gọn là: nếu node A bị touched và node B (con/cháu
        /// của A) cũng bị touched, ta chỉ cần bắt đầu traversal từ A — nó sẽ tự cascade qua B
        /// và toàn bộ nhánh dưới B, gọi enforce từ B riêng là dư thừa. Nên tập kết quả chỉ giữ
        /// lại "tổ tiên touched cao nhất" của mỗi nhánh, không giữ node touched nào có tổ tiên
        /// touched khác ở trên nó.
        ///
        /// Set này chỉ phụ thuộc CẤU TRÚC (node nào có link, không phải vị trí hiện tại), nên
        /// chỉ cần tính lại khi topology đổi (đổi Root Bones/Cloth Link options → gọi lại
        /// InitializeTrees()/BuildClothLinks()), KHÔNG cần tính lại mỗi frame.
        /// </summary>
        private void ComputeClothEnforcementRoots()
        {
            if (clothLinks.Count == 0) return;

            _clothTouchedScratch.Clear();
            foreach (var link in clothLinks)
            {
                if (link.a != null) _clothTouchedScratch.Add(link.a);
                if (link.b != null) _clothTouchedScratch.Add(link.b);
            }

            foreach (var node in _clothTouchedScratch)
            {
                bool ancestorAlsoTouched = false;
                for (BoneNode p = node.parent; p != null; p = p.parent)
                {
                    if (_clothTouchedScratch.Contains(p))
                    {
                        ancestorAlsoTouched = true;
                        break;
                    }
                }

                if (!ancestorAlsoTouched)
                    _clothEnforcementRoots.Add(node);
            }
        }

        private static List<BoneNode> GetLevel(List<List<BoneNode>> levels, int depth)
        {
            if (depth < 0 || depth >= levels.Count) return null;
            return levels[depth].Count > 0 ? levels[depth] : null;
        }

        private static void GatherLevels(BoneNode node, int depth, List<List<BoneNode>> levels)
        {
            while (levels.Count <= depth)
                levels.Add(new List<BoneNode>());

            levels[depth].Add(node);

            foreach (var child in node.children)
                GatherLevels(child, depth + 1, levels);
        }

        private BoneNode BuildBoneTree(Transform currentTransform, BoneNode parentNode)
        {
            BoneNode node = new BoneNode
            {
                transform = currentTransform,
                position = currentTransform.position,
                prevPosition = currentTransform.position,
                localPosition = currentTransform.localPosition,
                localRotation = currentTransform.localRotation,
                parent = parentNode
            };

            if (parentNode != null)
            {
                node.length = Vector3.Distance(currentTransform.position, parentNode.transform.position);
                node.restGravityLocal = parentNode.transform.InverseTransformDirection(gravity.normalized);
                Vector3 worldDir = currentTransform.position - parentNode.transform.position;
                node.restDirectionLocal = parentNode.transform.InverseTransformDirection(worldDir.normalized);
            }

            foreach (Transform child in currentTransform)
                node.children.Add(BuildBoneTree(child, node));

            return node;
        }

        void LateUpdate()
        {
            if (rootNodes.Count == 0) return;

            dragDirection = transform.position - lastManagerPosition;
            lastManagerPosition = transform.position;

            // [0.1][B1] Clamp dt: một frame hitch (ví dụ do load asset, GC spike) không
            // còn khiến gravity/spring bị nhân vọt lên và làm bone "nổ" ra xa rest pose.
            // 1/30s là ngưỡng an toàn (vẫn mượt ở 30 FPS trở lên, chặn được spike nặng).
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // 1. Mô phỏng từng chain độc lập
            foreach (var rootNode in rootNodes)
            {
                if (rootNode == null) continue;
                rootNode.position = rootNode.transform.position;
                SimulatePhysics(rootNode, dt);
            }

            // 2. Kéo các chain lại với nhau qua liên kết ngang (phải chạy sau khi
            //    tất cả chain đã bước xong, nếu không chain sau sẽ kéo chain trước lệch)
            SolveClothLinks();

            // 3. Chốt rotation từ vị trí cuối cùng
            foreach (var rootNode in rootNodes)
            {
                if (rootNode == null) continue;
                ApplyRotation(rootNode);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CLOTH LINKS — SOLVE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Giải liên kết ngang theo kiểu position-based: mỗi vòng lặp kéo hai đầu link về
        /// đúng khoảng cách gốc, rồi ép lại độ dài xương với parent để chuỗi không bị giãn.
        /// </summary>
        private void SolveClothLinks()
        {
            if (!enableClothLinks || clothLinks.Count == 0) return;

            float stiffness = Mathf.Clamp01(clothLinkStiffness);
            if (stiffness <= 0f) return;

            int iterations = Mathf.Max(1, clothLinkIterations);

            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < clothLinks.Count; i++)
                {
                    ClothLink link = clothLinks[i];
                    if (link.a == null || link.b == null) continue;

                    // Node gốc của chain bị ghim vào Transform → coi như khối lượng vô hạn
                    bool aFixed = link.a.parent == null;
                    bool bFixed = link.b.parent == null;
                    if (aFixed && bFixed) continue;

                    Vector3 delta = link.b.position - link.a.position;
                    float dist = delta.magnitude;
                    if (dist < 0.000001f) continue;

                    float error = (dist - link.restLength) / dist;
                    Vector3 correction = delta * (error * stiffness);

                    if (aFixed)
                    {
                        link.b.position -= correction;
                    }
                    else if (bFixed)
                    {
                        link.a.position += correction;
                    }
                    else
                    {
                        // Chia đôi cho hai bên
                        link.a.position += correction * 0.5f;
                        link.b.position -= correction * 0.5f;
                    }
                }

                // Liên kết ngang vừa làm lệch khoảng cách tới parent → ép lại.
                // [1.3][A3] Chỉ traverse từ _clothEnforcementRoots (các node "gốc bị ảnh
                // hưởng", xem ComputeClothEnforcementRoots()) thay vì duyệt lại TOÀN BỘ cây
                // từ rootNodes mỗi iteration — nhánh nào không có cloth link (và không phải
                // chain con của node có link) thì vị trí vẫn đang hợp lệ từ SimulatePhysics,
                // không cần enforce lại. Cùng kết quả, ít traversal hơn khi cloth link không
                // phủ hết toàn bộ chiều sâu của mọi chain.
                for (int r = 0; r < _clothEnforcementRoots.Count; r++)
                    EnforceBoneLength(_clothEnforcementRoots[r]);
            }
        }

        /// <summary>
        /// Ép mỗi node về đúng bán kính so với parent (parent giữ nguyên, chỉ node con dịch).
        /// </summary>
        private void EnforceBoneLength(BoneNode node)
        {
            if (node.parent != null && node.length > 0f)
            {
                Vector3 dir = node.position - node.parent.position;
                if (dir.sqrMagnitude > 0.000001f)
                    node.position = node.parent.position + dir.normalized * node.length;
            }

            foreach (var child in node.children)
                EnforceBoneLength(child);
        }

        private void SimulatePhysics(BoneNode node, float dt)
        {
            if (node.parent != null)
            {
                Vector3 velocity = node.position - node.prevPosition;
                node.prevPosition = node.position;

                // --- GRAVITY ---
                Vector3 effectiveGravity;
                if (useRestPoseGravity)
                {
                    Vector3 restGravityWorld = node.parent.transform.TransformDirection(node.restGravityLocal) *
                                               gravity.magnitude;
                    effectiveGravity = Vector3.Lerp(gravity, restGravityWorld, restPoseGravityBlend);
                }
                else
                {
                    effectiveGravity = gravity;
                }

                // --- INERTIA ---
                Vector3 parentDelta = node.parent.position - node.parent.prevPosition;
                Vector3 inertiaForce = -parentDelta * inertia;

                // --- INTEGRATE ---
                Vector3 nextPos = node.position + velocity + (effectiveGravity + inertiaForce) * (dt * dt);

                // --- LENGTH CONSTRAINT ---
                Vector3 toNext = nextPos - node.parent.position;
                nextPos = node.parent.position + toNext.normalized * node.length;

                // --- SPRING → REST POSE ---
                Transform grandParent = node.parent.transform.parent;
                Vector3 restDir;
                if (grandParent != null)
                    restDir = (grandParent.rotation * node.parent.localRotation * node.restDirectionLocal).normalized;
                else
                    restDir = (node.parent.localRotation * node.restDirectionLocal).normalized;

                Vector3 restOnSphere = node.parent.position + restDir * node.length;

                // [0.2][A5] Exponential response thay vì tuyến tính: springStiffness*mult*dt
                // tuyến tính sẽ cho hành vi lệ thuộc frame rate (ở FPS thấp, springT có thể
                // vọt qua 1 trong khi ở FPS cao lại quá nhỏ). Công thức mũ hội tụ về cùng
                // một "half-life" bất kể dt, nên spring cảm giác giống nhau ở mọi frame rate.
                // Lưu ý: giá trị mặc định của 3 preset (SoftFabric/Spring/RigidForm) nên được
                // artist review lại một lượt vì "cảm giác" spring sẽ đổi nhẹ so với công thức cũ.
                float springT = 1f - Mathf.Exp(-springStiffness * springForceMultiplier * dt);
                nextPos = Vector3.Lerp(nextPos, restOnSphere, springT);

                toNext = nextPos - node.parent.position;
                node.position = node.parent.position + toNext.normalized * node.length;

                // --- DAMPING (tangential) ---
                Vector3 radialDir = (node.position - node.parent.position).normalized;
                Vector3 velRadial = Vector3.Project(velocity, radialDir);
                Vector3 velTangential = velocity - velRadial;

                float tangentialDamp = Mathf.Clamp01(1f - damping * 15f * dt);
                float radialDamp = Mathf.Clamp01(1f - damping * 5f * dt);

                node.prevPosition = node.position
                                    - velRadial * radialDamp
                                    - velTangential * tangentialDamp;

                // --- COLLISIONS + PROPAGATE LÊN PARENT ---
                if (enableColliders && colliders.Count > 0)
                {
                    Vector3 beforeCollision = node.position;

                    // [5.5][B4] Nhiều vòng lặp thay vì single-pass: khi 2 collider chồng nhau,
                    // 1 pass duy nhất cho kết quả phụ thuộc thứ tự trong danh sách colliders
                    // (đẩy ra khỏi collider A trước có thể lại đẩy vào trong collider B).
                    // Lặp lại N lần giúp particle hội tụ dần về vị trí hợp lệ với TẤT CẢ
                    // collider, giống kiểu Gauss-Seidel relaxation — không tuyệt đối
                    // order-independent nhưng giảm mạnh sai khác giữa các thứ tự add.
                    int collisionIterations = Mathf.Max(1, collisionSolveIterations);
                    for (int i = 0; i < collisionIterations; i++)
                        SolveCollisions(ref node.position);

                    Vector3 correction = node.position - beforeCollision;

                    // Nếu có va chạm thật sự thì truyền correction ngược lên parent chain
                    // với falloff giảm dần: parent nhận correction * falloff^1, grandparent * falloff^2, ...
                    if (correction.sqrMagnitude > 0f)
                        PropagateCollisionToParents(node.parent, correction, collisionParentFalloff);
                }
            }

            foreach (var child in node.children)
                SimulatePhysics(child, dt);
        }

        /// <summary>
        /// Truyền correction vector ngược lên parent chain với hệ số falloff giảm dần mỗi cấp.
        /// Bone root (parent == null) không bao giờ bị dịch chuyển vì nó bị ghim vào Transform.
        /// </summary>
        private void PropagateCollisionToParents(BoneNode node, Vector3 correction, float falloff)
        {
            // Dừng khi hết parent hoặc magnitude quá nhỏ (tránh float noise vô tận)
            if (node == null || node.parent == null || correction.sqrMagnitude < 0.000001f)
                return;

            // Áp correction đã scale vào position và đồng thời dịch prevPosition để
            // velocity (position - prevPosition) không bị spike kỳ lạ sau collision.
            Vector3 scaledCorrection = correction * falloff;
            node.position += scaledCorrection;
            node.prevPosition += scaledCorrection;

            // Tiếp tục lên cấp trên với falloff nhân thêm một lần nữa
            PropagateCollisionToParents(node.parent, scaledCorrection, falloff);
        }

        private void SolveCollisions(ref Vector3 particlePos)
        {
            foreach (var col in colliders)
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                // [1.1][A1] Broadphase: Bounds check trước khi làm test hình học chính xác.
                // col.bounds là AABB world-space Unity tự maintain sẵn cho collider (đọc property
                // này rẻ hơn RẤT NHIỀU so với closest-point-on-segment + sqrt của exact test).
                // Coi particle như có 1 AABB nhỏ quanh vị trí hiện tại (bán kính = particleRadius);
                // nếu 2 AABB không giao nhau thì CHẮC CHẮN không va chạm — bỏ qua exact test luôn.
                // Đây là phần cắt giảm chi phí lớn nhất của A1: character có N collider nhưng
                // một bone tóc/đuôi thường chỉ thực sự cần test với 1-2 collider gần nó (Head/Neck),
                // các collider còn lại (tay, chân, hông...) bị loại ngay ở bước này mỗi frame.
                Bounds b = col.bounds;
                if (particlePos.x < b.min.x - particleRadius || particlePos.x > b.max.x + particleRadius ||
                    particlePos.y < b.min.y - particleRadius || particlePos.y > b.max.y + particleRadius ||
                    particlePos.z < b.min.z - particleRadius || particlePos.z > b.max.z + particleRadius)
                    continue;

                if (col is SphereCollider sphere)
                {
                    Vector3 center = sphere.transform.TransformPoint(sphere.center);
                    float radius = sphere.radius * Mathf.Max(sphere.transform.lossyScale.x,
                        sphere.transform.lossyScale.y, sphere.transform.lossyScale.z) + particleRadius;
                    Vector3 delta = particlePos - center;
                    float distSqr = delta.sqrMagnitude;
                    if (distSqr < radius * radius && distSqr > 0f)
                        particlePos = center + (delta / Mathf.Sqrt(distSqr)) * radius;
                }
                else if (col is CapsuleCollider capsule)
                {
                    CapsuleLocalData capsuleData = GetCapsuleLocalData(capsule);
                    GetCapsuleWorldPoints(capsule, out Vector3 p1, out Vector3 p2);
                    float radius = capsuleData.radiusUnscaled * GetCapsuleMaxScale(capsule, capsuleData) + particleRadius;
                    Vector3 closestPoint = ClosestPointOnSegment(p1, p2, particlePos);
                    Vector3 delta = particlePos - closestPoint;
                    float distSqr = delta.sqrMagnitude;
                    if (distSqr < radius * radius && distSqr > 0f)
                        particlePos = closestPoint + (delta / Mathf.Sqrt(distSqr)) * radius;
                }
            }
        }

        private void ApplyRotation(BoneNode node)
        {
            if (node.children.Count > 0)
            {
                node.transform.localRotation = node.localRotation;

                // [1.4][B2] Trung bình hướng của TẤT CẢ children thay vì chỉ children[0].
                // Trước đây node có > 1 con (branching) sẽ bị méo rotation ở điểm chia nhánh
                // vì các child khác ngoài children[0] không hề ảnh hưởng hướng xoay của cha.
                // Weighted theo rest length (node.length) của từng nhánh để nhánh dài hơn
                // (ảnh hưởng thị giác lớn hơn) đóng góp nhiều hơn vào hướng trung bình.
                // Với node chỉ có 1 con (trường hợp phổ biến: chain thẳng tóc/đuôi/dải áo),
                // kết quả giống hệt công thức cũ vì chỉ có một số hạng trong tổng.
                Vector3 defaultDirSum = Vector3.zero;
                Vector3 targetDirSum = Vector3.zero;

                foreach (var childNode in node.children)
                {
                    Vector3 defaultDir = node.transform.TransformDirection(childNode.localPosition);
                    Vector3 targetDir = childNode.position - node.position;
                    if (defaultDir == Vector3.zero || targetDir == Vector3.zero) continue;

                    float weight = Mathf.Max(childNode.length, 0.0001f);
                    defaultDirSum += defaultDir.normalized * weight;
                    targetDirSum += targetDir.normalized * weight;
                }

                if (defaultDirSum != Vector3.zero && targetDirSum != Vector3.zero)
                {
                    Quaternion aimRotation = Quaternion.FromToRotation(defaultDirSum, targetDirSum);
                    node.transform.rotation = aimRotation * node.transform.rotation;
                }
            }

            foreach (var child in node.children)
                ApplyRotation(child);
        }

        // ══════════════════════════════════════════════════════════
        //  CAPSULE HELPERS
        // ══════════════════════════════════════════════════════════
        private Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
            return Vector3.Lerp(a, b, Mathf.Clamp01(t));
        }

        private void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 p1, out Vector3 p2)
        {
            CapsuleLocalData data = GetCapsuleLocalData(capsule);
            Transform t = capsule.transform;

            Vector3 center = t.TransformPoint(capsule.center);
            Vector3 dir = t.TransformDirection(data.localAxis);

            float scale = GetCapsuleDirectionScale(capsule, data);
            float height = Mathf.Max(0f, (data.heightUnscaled * scale) / 2f -
                                          data.radiusUnscaled * GetCapsuleMaxScale(capsule, data));
            p1 = center + dir * height;
            p2 = center - dir * height;
        }

        // [0.3][A4] direction/radius/height giờ đọc từ cache thay vì từ capsule.* mỗi lần;
        // phần lossyScale vẫn phải đọc mỗi frame vì transform (kể cả scale) có thể đổi lúc
        // runtime (collider gắn theo bone animation), nhưng ternary chọn trục đã được cache.
        private float GetCapsuleDirectionScale(CapsuleCollider c, CapsuleLocalData data)
        {
            Vector3 s = c.transform.lossyScale;
            return data.localAxis == Vector3.right ? s.x : data.localAxis == Vector3.forward ? s.z : s.y;
        }

        private float GetCapsuleMaxScale(CapsuleCollider c, CapsuleLocalData data)
        {
            Vector3 s = c.transform.lossyScale;
            return data.localAxis == Vector3.right ? Mathf.Max(s.y, s.z) :
                data.localAxis == Vector3.forward ? Mathf.Max(s.x, s.y) : Mathf.Max(s.x, s.z);
        }

        // ══════════════════════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════════════════════
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (Application.isPlaying && rootNodes.Count > 0)
            {
                foreach (var rootNode in rootNodes)
                    if (rootNode != null)
                        DrawNodeGizmos(rootNode);

                if (enableClothLinks)
                {
                    Gizmos.color = Color.magenta;
                    for (int i = 0; i < clothLinks.Count; i++)
                    {
                        ClothLink link = clothLinks[i];
                        if (link.a == null || link.b == null) continue;
                        Gizmos.DrawLine(link.a.position, link.b.position);
                    }
                }

                if (dragDirection.sqrMagnitude > 0.00001f)
                {
                    Vector3 origin = transform.position;
                    Vector3 dragVector = dragDirection.normalized * arrowLengthMultiplier;
                    DrawArrowGizmo(origin, origin + dragVector, Color.cyan);
                    DrawArrowGizmo(origin, origin - dragVector * inertia, Color.green);
                }
            }
            else if (rootBones.Count > 0)
            {
                foreach (var rootBone in rootBones)
                    if (rootBone != null)
                        DrawTransformGizmos(rootBone);
            }
        }

        private void DrawNodeGizmos(BoneNode node)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(node.position, nodeGizmoSize);

            if (enableColliders && particleRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
                Gizmos.DrawWireSphere(node.position, particleRadius);
            }

            if (node.parent != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(node.parent.position, node.position);
                Vector3 restPos = node.parent.transform.TransformPoint(node.localPosition);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(restPos, nodeGizmoSize * 0.8f);
            }

            foreach (var child in node.children)
                DrawNodeGizmos(child);
        }

        private void DrawTransformGizmos(Transform current)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(current.position, nodeGizmoSize);
            foreach (Transform child in current)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(current.position, child.position);
                DrawTransformGizmos(child);
            }
        }

        private void DrawArrowGizmo(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 200, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 160, 0) * Vector3.forward;
            float arrowHeadSize = Mathf.Clamp(direction.magnitude * 0.2f, 0.05f, 0.5f);
            Gizmos.DrawLine(to, to + right * arrowHeadSize);
            Gizmos.DrawLine(to, to + left * arrowHeadSize);
        }
    }
}