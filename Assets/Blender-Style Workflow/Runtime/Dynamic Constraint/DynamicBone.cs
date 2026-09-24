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
    public partial class DynamicBone : MonoBehaviour
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
        //  RUNTIME INTERNALS
        // ══════════════════════════════════════════════════════════
        //
        // [PERF] Toàn bộ cây xương được "làm phẳng" (flatten) thành mảng struct theo thứ tự
        // DFS pre-order (cha luôn đứng trước con). Nhờ vậy:
        //   • Không còn đệ quy / List<BoneNode> / object rải rác trên heap → ít cache-miss, 0 GC.
        //   • Subtree của node i là đoạn liên tục [i, subtreeEnd) → EnforceBoneLength chỉ là
        //     1 vòng for, các node con của i = i+1, rồi nhảy qua subtreeEnd để tới anh em kế tiếp.
        //   • Thứ tự xử lý y hệt đệ quy pre-order cũ nên kết quả mô phỏng không đổi.
        private struct NodeData
        {
            public Vector3 position;
            public Vector3 prevPosition;
            public float length;
            public int parent;      // -1 = root (bị ghim vào Transform)
            public int subtreeEnd;  // exclusive; subtree = [i, subtreeEnd). Leaf: subtreeEnd == i + 1
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 restGravityLocal;

            // = parent.localRotation * restDirectionLocal. Cả 2 đều là hằng số → nhân sẵn lúc build,
            // mỗi frame chỉ còn (worldRot(grandParent) * restDirBase).
            public Vector3 restDirBase;
        }

        /// <summary>Một liên kết ngang giữa hai node cùng cấp thuộc hai chain kề nhau.</summary>
        private struct ClothLinkData
        {
            public int a;
            public int b;
            public float restLength;
            public bool aFixed; // node gốc chain → khối lượng vô hạn (tính sẵn, khỏi kiểm tra mỗi frame)
            public bool bFixed;
        }

        /// <summary>
        /// [PERF] Dữ liệu world-space của 1 collider, tính ĐÚNG 1 LẦN mỗi frame (GatherColliders)
        /// rồi dùng lại cho mọi particle × mọi iteration. Trước đây mỗi lần test 1 particle với
        /// 1 collider phải gọi lại col.enabled, gameObject.activeInHierarchy, col.bounds,
        /// TransformPoint, lossyScale (x3–x4) … → hàng chục native call cho mỗi cặp.
        /// </summary>
        private struct ColliderData
        {
            public bool isCapsule;
            public Vector3 a;        // sphere: tâm; capsule: đầu p1
            public Vector3 b;        // capsule: đầu p2
            public float radius;     // đã cộng particleRadius
            public float radiusSqr;
            public Vector3 min;      // AABB broadphase (đã cộng radius)
            public Vector3 max;
            public Vector3 motion;   // độ dịch anchor của collider trong frame này (cho swept test)
        }

        /// <summary>
        /// Cache theo từng slot trong danh sách colliders: tham chiếu component/transform đã lấy
        /// sẵn (tránh col.transform / col.gameObject mỗi frame), phần local bất biến của capsule,
        /// và anchor cuối frame trước (thay cho 2 Dictionary cũ — không còn hash lookup).
        /// </summary>
        private sealed class ColliderState
        {
            public readonly Collider collider;
            public readonly SphereCollider sphere;
            public readonly CapsuleCollider capsule;
            public readonly Transform transform;
            public readonly GameObject gameObject;

            // Phần BẤT BIẾN của capsule (giữ đúng hành vi cache cũ: đọc 1 lần cho tới InitializeTrees)
            public readonly Vector3 localAxis;
            public readonly int axis; // 0 = X, 1 = Y, 2 = Z
            public readonly float radiusUnscaled;
            public readonly float heightUnscaled;

            // [FIX-TUNNEL v2] anchor cuối frame trước
            public bool hasPrev;
            public Vector3 prevAnchor;

            public ColliderState(Collider col)
            {
                collider = col;
                transform = col.transform;
                gameObject = col.gameObject;
                sphere = col as SphereCollider;
                capsule = col as CapsuleCollider;

                if (capsule != null)
                {
                    axis = capsule.direction == 0 ? 0 : capsule.direction == 2 ? 2 : 1;
                    localAxis = axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up;
                    radiusUnscaled = capsule.radius;
                    heightUnscaled = capsule.height;
                }
            }

            public bool IsSupported => sphere != null || capsule != null;
        }

        private NodeData[] _nodes = new NodeData[0];
        private Transform[] _tf = new Transform[0];
        private Transform[] _rootParentTf = new Transform[0]; // chỉ khác null ở root có parent ngoài cây
        private int[] _rootIndices = new int[0];
        private int _nodeCount;

        // Scratch mỗi frame:
        //  _rot[i]    = world rotation đầu frame của node i (chỉ node có con), sau đó bị ghi đè
        //               bằng rotation đã áp dụng trong ApplyRotations().
        //  _extRot[i] = world rotation của Transform cha NGOÀI cây (chỉ dùng cho root).
        private Quaternion[] _rot = new Quaternion[0];
        private Quaternion[] _extRot = new Quaternion[0];

        private ClothLinkData[] _links = new ClothLinkData[0];
        private int _linkCount;

        // [1.3][A3] Tập tối thiểu các node cần EnforceBoneLength khi giải cloth links — xem giải
        // thích chi tiết tại ComputeClothEnforcementRoots().
        private int[] _clothEnforcementRoots = new int[0];

        private ColliderState[] _colStates = new ColliderState[0];
        private ColliderData[] _colData = new ColliderData[0];
        private int[] _colSourceSlot = new int[0]; // [LOG] index gốc trong `colliders` cho từng _colData[i]
        private int _colCount;

        // [FIX-JITTER][FIX-TUNNEL-2] Snapshot vị trí của mọi node NGAY TRƯỚC SolveClothLinks(),
        // dùng làm sweepStart thật cho ResolveCollisionsAfterClothLinks() — xem chi tiết ở đó.
        private Vector3[] _preClothPositions = new Vector3[0];

#if UNITY_EDITOR
        // Chỉ phục vụ Gizmo → không tốn gì trong bản build.
        private Vector3 dragDirection = Vector3.zero;
        private Vector3 lastManagerPosition;
#endif

        /// <summary>Số chuỗi xương đã khởi tạo (Inspector dùng để hiển thị runtime info).</summary>
        public int ActiveChainCount => _rootIndices.Length;

        /// <summary>Số liên kết ngang đã dựng giữa các chain.</summary>
        public int ClothLinkCount => _linkCount;

        void Start()
        {
            //ApplyCurrentPreset();

            InitializeTrees();
#if UNITY_EDITOR
            lastManagerPosition = transform.position;
#endif
        }

    }
}