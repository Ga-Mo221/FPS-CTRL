using Unity.Mathematics;

namespace GaMo.BlenderEditorTools
{
    // ─────────────────────────────────────────────────────────────
    //  Tất cả struct trong file này PHẢI blittable (chỉ chứa kiểu
    //  giá trị: float3/quaternion/int/byte...) để dùng được trong
    //  NativeArray<T> và Burst-compiled job. Không được chứa
    //  Transform/class/managed reference nào ở đây.
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bản Burst-hoá của NodeData gốc. Ý nghĩa từng field giữ nguyên 100% so với
    /// DynamicBone.cs bản CPU-only, chỉ đổi Vector3→float3, Quaternion→quaternion.
    /// Mảng BoneNode toàn cục là PERSISTENT (Allocator.Persistent), sống xuyên suốt
    /// nhiều frame giống hệt field _nodes cũ — position/prevPosition được giữ lại
    /// qua từng frame, KHÔNG bị rebuild mỗi frame.
    /// </summary>
    public struct BoneNode
    {
        public float3 position;
        public float3 prevPosition;
        public float length;
        public int parent;      // -1 = root của 1 chain
        public int subtreeEnd;  // exclusive
        public float3 localPosition;
        public quaternion localRotation;
        public float3 restGravityLocal;
        public float3 restDirBase;

        // ── Depth trong chain (set 1 lần lúc BuildBoneTree, không đổi) ──────────────
        // depth     : 0 = root của chain, tăng 1 mỗi cấp con.
        // maxDepth  : độ sâu lớn nhất của TOÀN BỘ subtree bắt đầu tại chain root.
        //             Cả 2 được lưu vào node để job tính normalizedDepth = depth/maxDepth
        //             mà không cần đếm lại hay đọc thêm mảng phụ — O(1) / node, Burst-safe.
        public int depth;
        public int maxDepth; // = maxDepth của chain root (copy xuống mọi node trong chain)

        // Vị trí node ở ĐẦU frame (trước khi Simulate/SolveLinks dịch chuyển). Ghi bởi ChainSimCore.Simulate
        // mỗi frame; SolveLinks dùng làm điểm bắt đầu của swept-test để node bị link kéo đi nhanh
        // vẫn không xuyên qua collider mỏng.
        public float3 frameStartPos;
    }

    /// <summary>
    /// 1 chain = 1 rootBone = phần việc của đúng 1 lần gọi IJobParallelFor.
    /// start..end là đoạn liên tục trong mảng BoneNode toàn cục (chain của các
    /// nhân vật/DynamicBone khác nhau không bao giờ chồng lấn).
    /// </summary>
    public struct ChainRange
    {
        public int start;        // = index của root node
        public int end;          // = subtreeEnd của root (exclusive), toàn bộ subtree
        public int paramIndex;   // trỏ vào BoneParams[] — thông số của DynamicBone sở hữu chain này
        public int externalIndex; // trỏ vào mảng externalParent rotation (-1 nếu root không có parent ngoài cây)
    }

    /// <summary>
    /// 1 liên kết ngang (cross link) giữa 2 node thuộc 2 chain KỀ NHAU của CÙNG 1 DynamicBone.
    /// a/b là index node TOÀN CỤC (manager đã cộng offset). restLength = khoảng cách 2 node ở bind pose.
    /// </summary>
    public struct NodeLink
    {
        public int a;
        public int b;
        public float restLength;
    }

    /// <summary>
    /// 1 ĐƠN VỊ CÔNG VIỆC của job mô phỏng (1 index của IJobParallelFor) = 1 nhóm chain liên tiếp trong
    /// mảng ChainRange toàn cục. Mặc định 1 nhóm = 1 chain (hành vi cũ, độc lập hoàn toàn). Khi
    /// DynamicBone bật Cross Links, TOÀN BỘ chain của DynamicBone đó gộp thành 1 nhóm để cùng 1
    /// worker thread xử lý tuần tự → không có race giữa các chain liên kết với nhau. Các nhóm khác
    /// nhau (nhân vật/component khác) vẫn chạy song song và KHÔNG BAO GIỜ chồng lấn vùng node.
    /// </summary>
    public struct ChainGroup
    {
        public int chainStart;  // index đầu trong ChainRange[]
        public int chainCount;  // số chain liên tiếp thuộc nhóm
        public int linkStart;   // range vào NodeLink[] toàn cục (chỉ link nội bộ nhóm)
        public int linkCount;
    }

    /// <summary>
    /// Thông số cấu hình của 1 DynamicBone component. Phần "vật lý gốc" (damping/gravity/...)
    /// copy từ Inspector; phần "active"/"dt" do DynamicBoneManager tính LẠI MỖI FRAME dựa trên
    /// LOD & Culling (khoảng cách tới camera, có nằm trong frustum hay không) — xem
    /// DynamicBoneManager.ApplyLodAndCulling(). Việc build lại toàn bộ struct này mỗi frame cho
    /// tất cả instance (chỉ vài chục/trăm struct nhỏ) RẺ HƠN RẤT NHIỀU so với 1 frame bỏ lỡ cơ
    /// hội LOD, nên không còn gate theo "dirty" nữa như comment cũ.
    /// </summary>
    public struct BoneParams
    {
        public float damping;
        public float inertia;
        public float collisionParentFalloff;
        public float springStiffness;
        public float springForceMultiplier;
        // ── Spring depth falloff (từ Inspector DynamicBone.depthFalloff) ─────────────
        // 0 = áp đều 100% lực spring mọi particle (hành vi cũ).
        // 1 = spring giảm theo (1-normalizedDepth)² — gốc mạnh, ngọn gần như 0.
        // Dùng cho CẢ springT lẫn phần giảm gravity (xem ChainSimCore.Simulate).
        public float depthFalloff;
        public float3 gravity;
        public byte useRestPoseGravity;   // bool dạng byte cho blittable
        public float restPoseGravityBlend;
        public byte enableColliders;
        public float particleRadius;
        public int collisionSolveIterations;
        public int colliderStart;  // range vào mảng JobColliderData toàn cục
        public int colliderCount;

        // ── Cross Links (giữ liên kết giữa các chain kề nhau) ───────────────────────────
        public float linkStiffness;  // 0..1, phần khoảng cách sai được sửa mỗi vòng lặp
        public int linkIterations;   // số vòng lặp giải link (mỗi vòng: link → ép length → va chạm)

        // ── LOD & CULLING (gán bởi DynamicBoneManager.ApplyLodAndCulling() mỗi frame) ──────
        // active = 0 ⇒ SimulateAndApplyRotationChainJob bỏ qua HOÀN TOÀN chain này (return sớm),
        // giữ nguyên position/rotation của frame gần nhất — tức "đóng băng" pose hiện tại, không
        // tốn chi phí mô phỏng/va chạm. Dùng cho nhân vật ngoài frustum hoặc quá xa camera.
        public byte active;
        // dt = delta-time THỰC DÙNG cho chain này ở frame này (không phải Time.deltaTime toàn
        // cục) — khi LOD giảm tần suất mô phỏng (VD: 1/3 frame), dt sẽ là tổng thời gian đã trôi
        // qua kể từ lần mô phỏng gần nhất (dt tích luỹ), giúp vật lý vẫn phản ứng đúng biên độ dù
        // update thưa hơn, thay vì "chạy chậm" đi 3 lần.
        public float dt;
    }

    /// <summary>
    /// Copy Burst-safe của DynamicBoneCollider (type/centerOffset/size/reverseDirection), đọc lại
    /// từ component DynamicBoneCollider mỗi frame trên main-thread qua ToColliderConfigData()
    /// (rẻ, KHÔNG đụng Transform) rồi đưa vào job — y hệt cách bản gốc đọc cfg.size/.../cfg.type
    /// trực tiếp mỗi frame trong GatherColliders(). particleRadius được DynamicBone gán đè sau
    /// (xem DynamicBone.BuildColliderConfigData) vì đó là thuộc tính của DynamicBone SỞ HỮU chain,
    /// không phải của bản thân collider (1 collider có thể dùng chung nhiều DynamicBone).
    /// </summary>
    public struct ColliderConfigData
    {
        public byte type; // 0 = Sphere, 1 = Capsule, 2 = Plane, 3 = Box
        public float3 centerOffset;
        public float3 size;
        public byte reverseDirection;
        public byte axis;            // 0 = X, 1 = Y, 2 = Z — trục local dùng làm trục dài của Capsule
        public byte alignedOnCenter; // 1 = 2 đầu đối xứng qua Center (mặc định); 0 = 1 đầu neo tại
                                      // Center, đầu còn lại kéo dài full "length" theo axis
        // Scale dùng để nhân bán kính/chiều dài (= transform.root.localScale, đọc mỗi frame trên
        // main-thread bởi DynamicBoneCollider.GetSizeScale()/ToColliderConfigData()) — CỐ Ý KHÔNG
        // dùng lossyScale cộng dồn qua từng Transform cha, để tránh 1 bone kỹ thuật ở giữa hierarchy
        // bị scale bất thường (VD 100,100,100) làm phóng to sai bán kính/chiều dài collider dù bản
        // thân root và collider vẫn scale bình thường. Xem GetSizeScale() để biết chi tiết lý do.
        public byte planeInfinite;   // 1 = Plane vô hạn (size bỏ qua); 0 = hữu hạn, size.x = width, size.y = height
        // Box: kích thước mặt TRÊN (+Y) / mặt DƯỚI (-Y), mỗi mặt = (x: rộng theo local X, y: dài theo local Z).
        // Chế độ Uniform: 2 mặt bằng nhau. Chiều cao nằm ở size.y.
        public float2 boxTopSize;
        public float2 boxBottomSize;
        public float3 rootScale;
        public float particleRadius; // = particleRadius của DynamicBone sở hữu collider này
    }

    /// <summary>Bản Burst-hoá của ColliderData gốc (world-space, tính 1 lần/frame).</summary>
    public struct JobColliderData
    {
        public byte isCapsule;
        public byte isPlane;
        public byte isBox;
        // Box (khối cụt, mặt trên/dưới là hình chữ nhật): tâm = a, 3 trục world-space đã chuẩn hoá.
        // boxHalf = (nửa rộng max, nửa cao, nửa dài max) — chỉ để bao ngoài. Mọi kích thước đã cộng particleRadius.
        // Mỗi mặt bên là 1 mặt phẳng: pen = c + dot(n, p_local) với n = (∓1, k, 0)/√(1+k²) — xem BoxPlane() trong Jobs.
        public float3 boxAxisX;
        public float3 boxAxisY;
        public float3 boxAxisZ;
        public float3 boxHalf;
        public float2 boxK;        // độ dốc nửa-rộng theo Y (x: cho trục X, y: cho trục Z)
        public float2 boxC;        // nửa-rộng tại y = 0
        public float2 boxInvNorm;  // 1/√(1+k²)
        public byte active; // = activeInHierarchy tại thời điểm gather (kiểm tra trên main-thread)
        public float3 a;
        public float3 b;
        public float radius;
        public float radiusSqr;
        public float startRadius;
        public float endRadius;
        public float3 min;
        public float3 max;
        public float3 motion;
        public float3 planeNormal;
        public float3 planePoint;
        // Plane hữu hạn (planeFinite = 1): hình chữ nhật tâm planeRectCenter, nửa kích thước
        // planeHalfSize.x dọc planeRight, planeHalfSize.y dọc planeForward (đã cộng particleRadius).
        public byte planeFinite;
        public float3 planeRectCenter;
        public float3 planeRight;
        public float3 planeForward;
        public float2 planeHalfSize;
        public float3 anchor; // tâm collider world-space, dùng làm mốc swept-test frame sau
    }
}