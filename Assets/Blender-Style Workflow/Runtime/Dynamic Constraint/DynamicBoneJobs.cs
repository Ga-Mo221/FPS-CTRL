using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace GaMo.BlenderEditorTools
{
    // ─────────────────────────────────────────────────────────────
    //  JOB 1 — Đọc position + rotation của TOÀN BỘ transform (mọi node,
    //  mọi nhân vật) trong 1 lượt IJobParallelForTransform. Đây là job
    //  song song hoá đúng phần đắt nhất của bản CPU cũ: hàng ngàn native
    //  call transform.position/.rotation rải rác trên main-thread.
    // ─────────────────────────────────────────────────────────────
    [BurstCompile]
    public struct ReadNodeTransformsJob : IJobParallelForTransform
    {
        [WriteOnly] public NativeArray<float3> outPosition;
        [WriteOnly] public NativeArray<quaternion> outRotation;

        public void Execute(int index, TransformAccess transform)
        {
            outPosition[index] = transform.position;
            outRotation[index] = transform.rotation;
        }
    }

    // [PERF] ReadExternalParentJob đã bị loại bỏ. Danh sách external-parent đã dedup xuống còn
    // rất ít entry (≈ 1 / nhân vật, không phải 1 / chain), nên đọc thẳng transform.rotation trên
    // main-thread (vòng lặp thường trong DynamicBoneManager.ScheduleFrame) rẻ hơn nhiều so với
    // chi phí Schedule() + sync 1 IJobParallelForTransform riêng cho từng ấy entry.

    // ─────────────────────────────────────────────────────────────
    //  JOB 2 — Gom dữ liệu collider world-space, y hệt GatherColliders()
    //  gốc nhưng chạy song song trên TransformAccessArray riêng của
    //  danh sách collider toàn cục.
    // ─────────────────────────────────────────────────────────────
    [BurstCompile]
    public struct GatherCollidersJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<ColliderConfigData> configs;
        [ReadOnly] public NativeArray<byte> activeFlags;      // activeInHierarchy, đọc trên main-thread trước
        [ReadOnly] public NativeArray<float3> prevAnchor;
        [ReadOnly] public NativeArray<byte> hasPrevAnchor;

        public NativeArray<JobColliderData> outData;

        public void Execute(int index, TransformAccess t)
        {
            JobColliderData cd = default;
            cd.active = activeFlags[index];
            if (cd.active == 0) { outData[index] = cd; return; }

            ColliderConfigData cfg = configs[index];
            quaternion worldRot = t.rotation;

            // [FIX] KHÔNG còn trích scale từ localToWorldMatrix (m.c0/c1/c2) để nhân bán kính/chiều
            // dài — cách đó cộng dồn scale qua TOÀN BỘ chuỗi cha, nên 1 bone kỹ thuật ở giữa hierarchy
            // bị scale bất thường (VD 100,100,100) sẽ phóng to sai collider dù bản thân root/collider
            // vẫn scale bình thường. cfg.rootScale = transform.root.localScale, đọc mỗi frame trên
            // main-thread bởi DynamicBoneCollider.GetSizeScale() — xem comment ở đó để biết chi tiết.
            // Cả KÍCH THƯỚC lẫn center offset đều dùng rootScale (xem worldCenter bên dưới).
            float3 scale = cfg.rootScale;
            // [FIX] Center = position + rotation * (centerOffset * rootScale). KHÔNG dùng
            // math.transform(localToWorldMatrix, offset) vì nó nhân dồn scale mọi bone cha
            // (parent 2 scale 100 → offset bị x100). Khớp DynamicBoneCollider.GetWorldCenter().
            float3 tPos = t.position;
            float3 centerLocalScaled = cfg.centerOffset * scale;
            float3 worldCenter = tPos + math.mul(worldRot, centerLocalScaled);

            float pr = cfg.particleRadius;

            switch (cfg.type)
            {
                case 0: // Sphere
                {
                    float3 center = worldCenter;
                    float radius = cfg.size.x * math.cmax(math.abs(scale)) + pr;

                    cd.isCapsule = 0;
                    cd.isPlane = 0;
                    cd.a = center;
                    cd.b = center;
                    cd.radius = radius;
                    cd.startRadius = radius;
                    cd.endRadius = radius;
                    cd.anchor = center;
                    cd.motion = hasPrevAnchor[index] != 0 ? center - prevAnchor[index] : float3.zero;
                    break;
                }
                case 1: // Capsule
                {
                    // Trục dài của capsule giờ CHỌN ĐƯỢC (Direction: X/Y/Z-Axis trên Inspector),
                    // không còn cố định local up như trước — xem DynamicBoneCollider.direction.
                    float3 axisLocal = cfg.axis == 0 ? new float3(1, 0, 0)
                        : cfg.axis == 1 ? new float3(0, 1, 0)
                        : new float3(0, 0, 1);
                    float scaleAlongAxis = cfg.axis == 0 ? scale.x : cfg.axis == 1 ? scale.y : scale.z;
                    // Scale trung bình của 2 trục VUÔNG GÓC với axis — dùng để scale bán kính, y hệt
                    // cách bản gốc luôn dùng avg(scale.x, scale.z) khi axis cố định = Y.
                    float scalePerp = cfg.axis == 0 ? (math.abs(scale.y) + math.abs(scale.z)) * 0.5f
                        : cfg.axis == 1 ? (math.abs(scale.x) + math.abs(scale.z)) * 0.5f
                        : (math.abs(scale.x) + math.abs(scale.y)) * 0.5f;

                    float length = cfg.size.z * math.abs(scaleAlongAxis);
                    float3 center = worldCenter;
                    float3 axis = math.mul(worldRot, axisLocal);

                    float3 p1, p2;
                    if (cfg.alignedOnCenter != 0)
                    {
                        // Mặc định: 2 đầu đối xứng qua Center (hành vi cũ).
                        p1 = center + axis * (length * 0.5f);
                        p2 = center - axis * (length * 0.5f);
                    }
                    else
                    {
                        // Aligned On Center tắt: 1 đầu NEO tại Center, đầu còn lại kéo dài full
                        // length theo axis — giống Dynamic Bone Collider gốc khi bỏ tick "Aligned".
                        p1 = center;
                        p2 = center + axis * length;
                    }

                    if (cfg.reverseDirection != 0)
                    {
                        float3 tmp = p1; p1 = p2; p2 = tmp;
                    }

                    float startRadius = cfg.size.x * scalePerp + pr;
                    float endRadius = cfg.size.y * scalePerp + pr;

                    cd.isCapsule = 1;
                    cd.isPlane = 0;
                    cd.a = p1;
                    cd.b = p2;
                    cd.startRadius = startRadius;
                    cd.endRadius = endRadius;
                    cd.radius = math.max(startRadius, endRadius);
                    cd.anchor = center;
                    cd.motion = hasPrevAnchor[index] != 0 ? center - prevAnchor[index] : float3.zero;
                    break;
                }
                case 3: // Box (có thể côn: mặt trên ≠ mặt dưới)
                {
                    float3 ax = math.normalize(math.mul(worldRot, new float3(1, 0, 0)));
                    float3 ay = math.normalize(math.mul(worldRot, new float3(0, 1, 0)));
                    float3 az = math.normalize(math.mul(worldRot, new float3(0, 0, 1)));

                    float2 sXZ = new float2(math.abs(scale.x), math.abs(scale.z));
                    float2 topHalf = cfg.boxTopSize * sXZ * 0.5f;
                    float2 botHalf = cfg.boxBottomSize * sXZ * 0.5f;
                    float hy = cfg.size.y * math.abs(scale.y) * 0.5f;

                    // Aligned On Center TẮT: Center = tâm MẶT TRÊN (+Y), khối rủ xuống phía -Y.
                    float3 center = cfg.alignedOnCenter != 0 ? worldCenter : worldCenter - ay * hy;

                    // Cộng particleRadius vào mọi kích thước.
                    topHalf += new float2(pr, pr);
                    botHalf += new float2(pr, pr);
                    float hyP = hy + pr;

                    float2 k = hyP > 1e-6f ? (topHalf - botHalf) / (2f * hyP) : float2.zero;

                    cd.isCapsule = 0;
                    cd.isPlane = 0;
                    cd.isBox = 1;
                    cd.a = center;
                    cd.b = center;
                    cd.boxAxisX = ax;
                    cd.boxAxisY = ay;
                    cd.boxAxisZ = az;
                    cd.boxHalf = new float3(math.max(topHalf.x, botHalf.x), hyP, math.max(topHalf.y, botHalf.y));
                    cd.boxK = k;
                    cd.boxC = (topHalf + botHalf) * 0.5f;
                    cd.boxInvNorm = new float2(1f / math.sqrt(1f + k.x * k.x), 1f / math.sqrt(1f + k.y * k.y));
                    // radius = bán kính cầu bao ngoài → tái dùng code tính min/max AABB phía dưới.
                    cd.radius = math.length(cd.boxHalf);
                    cd.startRadius = cd.radius;
                    cd.endRadius = cd.radius;
                    cd.anchor = center;
                    cd.motion = hasPrevAnchor[index] != 0 ? center - prevAnchor[index] : float3.zero;
                    break;
                }
                default: // Plane
                {
                    // Direction (cfg.axis) = trục PHÁP TUYẾN của plane (giống Dynamic Bone gốc).
                    // Hai trục còn lại: width (ngang) / height (dọc):
                    //   X-Axis: width = Z, height = Y | Y-Axis: width = X, height = Z | Z-Axis: width = X, height = Y
                    float3 nLocal, wLocal, hLocal;
                    float sw, sh;
                    if (cfg.axis == 0)      { nLocal = new float3(1, 0, 0); wLocal = new float3(0, 0, 1); hLocal = new float3(0, 1, 0); sw = scale.z; sh = scale.y; }
                    else if (cfg.axis == 1) { nLocal = new float3(0, 1, 0); wLocal = new float3(1, 0, 0); hLocal = new float3(0, 0, 1); sw = scale.x; sh = scale.z; }
                    else                    { nLocal = new float3(0, 0, 1); wLocal = new float3(1, 0, 0); hLocal = new float3(0, 1, 0); sw = scale.x; sh = scale.y; }

                    float3 planePos = worldCenter;
                    float3 planeNormal = math.normalize(math.mul(worldRot, nLocal));
                    if (cfg.reverseDirection != 0) planeNormal = -planeNormal;

                    cd.isCapsule = 0;
                    cd.isPlane = 1;
                    cd.planeNormal = planeNormal;
                    cd.anchor = planePos;
                    cd.motion = float3.zero;

                    if (cfg.planeInfinite != 0)
                    {
                        cd.planeFinite = 0;
                        cd.planePoint = planePos + planeNormal * pr;
                    }
                    else
                    {
                        float3 wAxis = math.normalize(math.mul(worldRot, wLocal));
                        float3 hAxis = math.normalize(math.mul(worldRot, hLocal));
                        float w = cfg.size.x * math.abs(sw);
                        float h = cfg.size.y * math.abs(sh);

                        // Aligned On Center BẬT : Center ở chính giữa hình chữ nhật.
                        // Aligned On Center TẮT : Center = TRUNG ĐIỂM CẠNH TRÊN (đầu +height axis),
                        //                         hình chữ nhật rủ xuống phía -height axis (rộng w đối xứng 2 bên).
                        float3 rectCenter = cfg.alignedOnCenter != 0
                            ? planePos
                            : planePos - hAxis * (h * 0.5f);

                        cd.planeFinite = 1;
                        cd.planeRectCenter = rectCenter;
                        cd.planePoint = rectCenter + planeNormal * pr;
                        cd.planeRight = wAxis;
                        cd.planeForward = hAxis;
                        cd.planeHalfSize = new float2(w * 0.5f + pr, h * 0.5f + pr);
                    }
                    break;
                }
            }

            cd.radiusSqr = cd.radius * cd.radius;
            if (cd.isPlane == 0)
            {
                cd.min = math.min(cd.a, cd.b) - new float3(cd.radius, cd.radius, cd.radius);
                cd.max = math.max(cd.a, cd.b) + new float3(cd.radius, cd.radius, cd.radius);
            }

            outData[index] = cd;
        }
    }

    /// <summary>Chốt anchor hiện tại → prevAnchor cho swept-test của frame KẾ TIẾP.</summary>
    [BurstCompile]
    public struct CacheColliderAnchorsJob : IJob
    {
        [ReadOnly] public NativeArray<JobColliderData> data;
        [ReadOnly] public NativeArray<byte> activeFlags;
        public NativeArray<float3> prevAnchor;
        public NativeArray<byte> hasPrevAnchor;

        public void Execute()
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (activeFlags[i] == 0 || data[i].isPlane != 0) continue;
                prevAnchor[i] = data[i].anchor;
                hasPrevAnchor[i] = 1;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  JOB 3 — MÔ PHỎNG VẬT LÝ, song song hoá theo CHAIN (1 rootBone).
    //  Mỗi lần Execute(chainIndex) xử lý TOÀN BỘ 1 chuỗi xương tuần tự
    //  (cha trước con, y hệt vòng lặp gốc) nhưng các chain khác nhau
    //  chạy trên các worker thread khác nhau, hoàn toàn độc lập.
    //
    //  [NativeDisableParallelForRestriction] bắt buộc phải có trên `nodes`
    //  vì PropagateCollisionToParents ghi vào các index KHÁC với index
    //  hiện tại của Execute() (ghi ngược lên chuỗi cha trong CÙNG chain).
    //  Điều này AN TOÀN vì mỗi ChainRange sở hữu 1 đoạn [start,end) rời
    //  rạc, không chain nào đụng vào vùng nhớ của chain khác.
    // ─────────────────────────────────────────────────────────────
    // [PERF] SimulateChainJob (mô phỏng vị trí) và ApplyRotationChainJob (chốt rotation) đã được
    // GỘP thành 1 job duy nhất — xem SimulateAndApplyRotationChainJob phía dưới `ApplyRotationChainJob`
    // gốc. Hai job cũ vẫn là 2 pass TUẦN TỰ y hệt bên trong 1 Execute() (Pass A: toàn bộ vị trí
    // trong chain phải được mô phỏng xong trước khi Pass B đọc vị trí CON để tính rotation — xem
    // comment trong SimulateAndApplyRotationChainJob), chỉ khác là không còn schedule 2 job + 1
    // sync barrier + 240-item reschedule riêng biệt giữa 2 pass nữa.
    internal static class ChainSimCore
    {
        public static void Simulate(ChainRange chain, BoneParams p,
            NativeArray<float3> rootPosition, NativeArray<quaternion> nodeRotation,
            NativeArray<quaternion> externalRotation, NativeArray<JobColliderData> colliders,
            NativeArray<BoneNode> nodes, float dt)
        {

            // Root: chỉ bị ghim theo Transform, không mô phỏng.
            // [FIX] prevPosition của root PHẢI được cập nhật mỗi frame = vị trí root của
            // FRAME TRƯỚC (chính là giá trị .position cũ trước khi bị ghi đè bên dưới).
            // Trước đây prevPosition bị bỏ quên, chỉ được set 1 lần lúc BuildBoneTree() (hoặc
            // lúc LOD hồi sinh qua ResetInstancePositions()) rồi đứng yên MÃI MÃI — khiến
            // parentDelta = parentPos - parentNode.prevPosition ở bone con đầu tiên (chain.start+1)
            // phình to dần theo TOÀN BỘ quãng đường root đã đi kể từ lúc spawn, thay vì chỉ 1
            // frame, sinh ra inertiaForce khổng lồ/sai và gây dính/bật văng đúng từ bone thứ 2.
            BoneNode rootNode = nodes[chain.start];
            rootNode.prevPosition = rootNode.position;
            rootNode.position = rootPosition[chain.start];
            nodes[chain.start] = rootNode;

            float dt2 = dt * dt;
            float3 g = p.gravity;
            float gravMag = math.length(g);
            bool restGrav = p.useRestPoseGravity != 0;
            float restBlend = p.restPoseGravityBlend;
            float inertiaK = p.inertia;

            // springT được tính PER-NODE bên trong vòng lặp (xem bên dưới) khi depthFalloff > 0,
            // để depthFactor (1-normalizedDepth)² giảm dần lực spring từ gốc tới ngọn.
            // Khi depthFalloff = 0: depthFactor = 1 mọi node → springT không đổi (hành vi cũ).
            float springBaseExponent = p.springStiffness * p.springForceMultiplier * dt; // tái dùng per-node
            float tangentialDamp = math.clamp(1f - p.damping * 15f * dt, 0f, 1f);
            float radialDamp = math.clamp(1f - p.damping * 5f * dt, 0f, 1f);

            bool doCollide = p.enableColliders != 0 && p.colliderCount > 0;
            int collisionIterations = math.max(1, p.collisionSolveIterations);
            float falloff = p.collisionParentFalloff;

            for (int i = chain.start + 1; i < chain.end; i++)
            {
                BoneNode node = nodes[i];
                int pi = node.parent;

                BoneNode parentNode = nodes[pi];
                float3 frameStartPos = node.position;
                float3 parentPos = parentNode.position;
                node.frameStartPos = frameStartPos; // dùng cho sweep ở SolveLinks
                float length = node.length;

                float3 velocity = frameStartPos - node.prevPosition;

                // ── Dead-zone velocity ──────────────────────────────────────────────
                // Verlet tích lũy floating-point noise mỗi frame: velocity không bao giờ
                // về đúng 0 dù nhân vật đứng yên hoàn toàn → spring liên tục "sửa" một
                // lượng nhỏ, damping "sửa" ngược lại → giật nhẹ vô tận.
                // Giải pháp: zero hóa velocity khi lengthsq < epsilon²; node "nghỉ" hoàn
                // toàn, prevPosition được chốt bằng position → vòng lặp hội tụ.
                // Ngưỡng 1e-9 (≈ 0.032 mm)²: dưới mức mắt người có thể thấy ở 60 fps,
                // nhưng đủ để dập noise IEEE-754 float32 (~1e-14 sau vài frame).
                if (math.lengthsq(velocity) < 1e-9f)
                    velocity = float3.zero;

                // gpRot: rotation của grandparent node (cha của parent) — dùng kết hợp với
                // restDirBase và restGravityLocal (cả 2 đã được "pre-baked" với localRotation
                // của parent trong BuildBoneTree) để ra world-space đúng:
                //   gpRot × restDirBase = gpRot × (parentLocalRot × restDirLocal) = parentWorldRot × restDirLocal
                quaternion gpRot = ResolveParentRot(pi, nodes, chain.start, externalRotation, chain.externalIndex, nodeRotation);

                float3 effectiveGravity;
                if (restGrav)
                {
                    float3 restGravityWorld = math.mul(gpRot, node.restGravityLocal) * gravMag;
                    effectiveGravity = math.lerp(g, restGravityWorld, restBlend);
                }
                else
                {
                    effectiveGravity = g;
                }

                float3 parentDelta = parentPos - parentNode.prevPosition;
                float3 inertiaForce = -parentDelta * inertiaK;

                float3 nextPos = frameStartPos + velocity + (effectiveGravity + inertiaForce) * dt2;

                float3 toNext = nextPos - parentPos;
                nextPos = parentPos + math.normalizesafe(toNext) * length;

                // depthFactor: Lerp(1, (1-normalizedDepth)², depthFalloff)
                // - depthFalloff=0 → factor=1 mọi node (hành vi cũ, springT không đổi).
                // - depthFalloff=1 → factor=(1-nd)²: root (nd=0)→1.0, ngọn (nd=1)→0.0.
                // maxDepth=0 chỉ xảy ra khi chain chỉ có 1 node (root leaf) — không vào
                // vòng lặp này vì i = chain.start+1, nên guard < 1 là đủ.
                float nd = node.maxDepth > 0
                    ? (float)node.depth / node.maxDepth
                    : 0f;
                float quadratic = (1f - nd) * (1f - nd);
                float depthFactor = math.lerp(1f, quadratic, p.depthFalloff);
                float springT = 1f - math.exp(-springBaseExponent * depthFactor);

                float3 restDir = math.normalizesafe(math.mul(gpRot, node.restDirBase));
                float3 restOnSphere = parentPos + restDir * length;
                nextPos = math.lerp(nextPos, restOnSphere, springT);

                toNext = nextPos - parentPos;
                float3 newPos = parentPos + math.normalizesafe(toNext) * length;

                float3 radialDir = math.normalizesafe(newPos - parentPos);
                float3 velRadial = math.dot(velocity, radialDir) * radialDir;
                float3 velTangential = velocity - velRadial;

                float3 dampedVelRadial     = velRadial * radialDamp;
                float3 dampedVelTangential = velTangential * tangentialDamp;

                // Nếu velocity còn lại sau damping cũng cực nhỏ thì chốt prevPosition = newPos
                // (velocity frame sau = 0) thay vì để noise tích lũy tiếp.
                float3 residualVel = dampedVelRadial + dampedVelTangential;
                float3 newPrevPos = math.lengthsq(residualVel) < 1e-9f
                    ? newPos
                    : newPos - dampedVelRadial - dampedVelTangential;

                float3 finalPos = newPos;

                if (doCollide)
                {
                    float3 pos = newPos;
                    for (int it = 0; it < collisionIterations; it++)
                    {
                        bool mod = SolveCollisions(ref pos, frameStartPos, it == 0, colliders, p.colliderStart, p.colliderCount);

                        // [FIX] Đẩy va chạm (SolveCollisions) phá vỡ ràng buộc "cách parent đúng
                        // `length`" đã ép ở trên mà không bao giờ ép lại — vô hại khi parent gần
                        // như đứng yên (sai số nhỏ, tự hội tụ), nhưng khi PARENT di chuyển nhanh
                        // (nhân vật/flag đi xuyên qua collider tĩnh) 2 ràng buộc "cách parent
                        // đúng length" và "nằm ngoài collider" mâu thuẫn nhau mỗi frame, và vì
                        // collision luôn thắng (ghi đè, không ép lại length), node bị neo cứng
                        // vào điểm va chạm gần nhất thay vì bám theo parent → nhìn như "dính"
                        // tại chỗ trong khi cả chain lướt qua. Ép lại length quanh parent SAU mỗi
                        // vòng giải va chạm (PBD-style, cùng vòng lặp collisionSolveIterations có
                        // sẵn) để 2 ràng buộc hội tụ cùng nhau — node vẫn bị chặn bởi collider
                        // nhưng trượt dọc bề mặt theo hướng parent di chuyển, thay vì đứng khựng.
                        float3 toParentAfter = pos - parentPos;
                        float distToParentAfter = math.length(toParentAfter);
                        if (distToParentAfter > 0.0001f)
                            pos = parentPos + (toParentAfter / distToParentAfter) * length;

                        if (!mod) break;
                    }

                    // Va chạm luôn là bước CUỐI: vòng lặp trên kết thúc bằng ép length, có thể đẩy
                    // node vào lại collider. Sai lệch length nhỏ vô hình (chỉ rotation được ghi ra).
                    SolveCollisions(ref pos, pos, false, colliders, p.colliderStart, p.colliderCount);

                    float3 correction = pos - newPos;
                    finalPos = pos;

                    if (math.lengthsq(correction) > 0f)
                    {
                        newPrevPos += correction;
                        PropagateCollisionToParents(nodes, pi, correction, falloff);
                    }
                }

                node.position = finalPos;
                node.prevPosition = newPrevPos;
                nodes[i] = node;
            }
        }

        /// <summary>
        /// Giải Cross Links cho 1 NHÓM chain (chạy SAU Pass A của mọi chain trong nhóm, TRƯỚC Pass B).
        /// Mỗi vòng lặp (PBD): (1) kéo mỗi cặp node liên kết về restLength, (2) ép lại độ dài với parent rồi va chạm
        /// có sweep (cha trước con, va chạm là bước cuối) để 2 ràng buộc hội tụ cùng nhau. Mọi hiệu chỉnh vị
        /// trí đều cộng cùng lượng vào prevPosition → vận tốc Verlet KHÔNG đổi, tránh bơm năng lượng/rung.
        /// nodeStart..nodeEnd = vùng node liền nhau của cả nhóm; link chỉ tham chiếu node trong vùng này.
        /// </summary>
        public static void SolveLinks(int nodeStart, int nodeEnd, BoneParams p,
            NativeArray<NodeLink> links, int linkStart, int linkCount,
            NativeArray<JobColliderData> colliders, NativeArray<BoneNode> nodes)
        {
            float k = math.saturate(p.linkStiffness);
            if (k <= 0f || linkCount <= 0) return;

            int iterations = math.max(1, p.linkIterations);
            bool doCollide = p.enableColliders != 0 && p.colliderCount > 0;

            for (int it = 0; it < iterations; it++)
            {
                for (int li = linkStart; li < linkStart + linkCount; li++)
                {
                    NodeLink l = links[li];
                    BoneNode na = nodes[l.a];
                    BoneNode nb = nodes[l.b];

                    float3 d = nb.position - na.position;
                    float dist = math.length(d);
                    if (dist < 1e-6f) continue;

                    // Chia đều cho 2 node; stiffness = tỉ lệ sai số được sửa mỗi vòng.
                    float3 corr = d * (((dist - l.restLength) / dist) * 0.5f * k);
                    na.position += corr; na.prevPosition += corr;
                    nb.position -= corr; nb.prevPosition -= corr;
                    nodes[l.a] = na;
                    nodes[l.b] = nb;
                }

                // Ép lại độ dài với parent (+ va chạm) theo thứ tự cha trước con. Root (parent < 0) bị ghim.
                for (int i = nodeStart; i < nodeEnd; i++)
                {
                    BoneNode n = nodes[i];
                    if (n.parent < 0) continue;

                    float3 parentPos = nodes[n.parent].position;
                    float3 pos = n.position;

                    // 1) Ép length với parent TRƯỚC.
                    float3 toParent = pos - parentPos;
                    float dl = math.length(toParent);
                    if (dl > 1e-4f) pos = parentPos + (toParent / dl) * n.length;

                    // 2) Va chạm SAU CÙNG (collider luôn thắng), có sweep từ đầu frame để node bị
                    //    link kéo đi nhanh không lọt qua collider mỏng.
                    if (doCollide)
                        SolveCollisions(ref pos, n.frameStartPos, true, colliders, p.colliderStart, p.colliderCount);

                    float3 delta = pos - n.position;
                    n.position = pos;
                    n.prevPosition += delta;
                    nodes[i] = n;
                }
            }
        }

        private static quaternion ResolveParentRot(int pi, NativeArray<BoneNode> nodes, int chainStart,
            NativeArray<quaternion> externalRotation, int externalIndex, NativeArray<quaternion> nodeRotation)
        {
            int gi = nodes[pi].parent;
            if (gi >= 0) return nodeRotation[gi];
            // pi là root của chain → dùng rotation của Transform cha ngoài cây (nếu có).
            return externalIndex >= 0 ? externalRotation[externalIndex] : quaternion.identity;
        }

        private static void PropagateCollisionToParents(NativeArray<BoneNode> nodes, int nodeIdx, float3 correction, float falloff)
        {
            while (nodeIdx >= 0 && nodes[nodeIdx].parent >= 0 && math.lengthsq(correction) >= 0.000001f)
            {
                correction *= falloff;
                BoneNode n = nodes[nodeIdx];
                n.position += correction;
                n.prevPosition += correction;
                nodes[nodeIdx] = n;
                nodeIdx = n.parent;
            }
        }

        private static bool SolveCollisions(ref float3 particlePos, float3 sweepStart, bool useSweep,
            NativeArray<JobColliderData> data, int colliderStart, int colliderCount)
        {
            float3 sweepDelta = particlePos - sweepStart;
            bool hasSweepMotion = useSweep && math.lengthsq(sweepDelta) > 1e-10f;
            bool modified = false;

            for (int ci = 0; ci < colliderCount; ci++)
            {
                int c = colliderStart + ci;
                JobColliderData cd = data[c];
                if (cd.active == 0) continue;

                if (cd.isPlane != 0)
                {
                    float d = math.dot(particlePos - cd.planePoint, cd.planeNormal);
                    if (d < 0f && cd.planeFinite != 0)
                    {
                        // Plane hữu hạn: chỉ chặn khi hình chiếu của particle nằm trong hình chữ nhật.
                        float3 rel = particlePos - cd.planeRectCenter;
                        if (math.abs(math.dot(rel, cd.planeRight)) > cd.planeHalfSize.x ||
                            math.abs(math.dot(rel, cd.planeForward)) > cd.planeHalfSize.y)
                            continue;
                    }
                    if (d < 0f)
                    {
                        particlePos -= cd.planeNormal * d;
                        modified = true;
                    }
                    continue;
                }

                float lx = particlePos.x, ly = particlePos.y, lz = particlePos.z;
                float hx = lx, hy = ly, hz = lz;
                if (hasSweepMotion)
                {
                    if (sweepStart.x < lx) lx = sweepStart.x; else hx = math.max(hx, sweepStart.x);
                    if (sweepStart.y < ly) ly = sweepStart.y; else hy = math.max(hy, sweepStart.y);
                    if (sweepStart.z < lz) lz = sweepStart.z; else hz = math.max(hz, sweepStart.z);
                }

                if (hx < cd.min.x || lx > cd.max.x || hy < cd.min.y || ly > cd.max.y || hz < cd.min.z || lz > cd.max.z)
                    continue;

                if (cd.isBox != 0)
                {
                    if (hasSweepMotion)
                    {
                        float3 adjStart = sweepStart + cd.motion;
                        float3 adjDelta = particlePos - adjStart;
                        if (RayBoxEntryT(adjStart, adjDelta, cd, out float tBox))
                        {
                            particlePos = sweepStart + sweepDelta * tBox;
                            modified = true;
                        }
                    }

                    // Đẩy ra mặt gần nhất nếu particle đang nằm TRONG khối (6 mặt phẳng, mặt bên có thể nghiêng).
                    float3 rel = particlePos - cd.a;
                    float3 lp = new float3(math.dot(rel, cd.boxAxisX), math.dot(rel, cd.boxAxisY), math.dot(rel, cd.boxAxisZ));
                    float minPen = float.MaxValue;
                    float3 minN = float3.zero;
                    bool inside = true;
                    for (int fi = 0; fi < 6; fi++)
                    {
                        BoxPlane(cd, fi, out float3 planeN, out float planeC);
                        float pen = planeC + math.dot(planeN, lp);
                        if (pen <= 0f) { inside = false; break; }
                        if (pen < minPen) { minPen = pen; minN = planeN; }
                    }
                    if (inside)
                    {
                        float3 push = -minN * minPen; // pháp tuyến hướng RA = -n
                        particlePos += cd.boxAxisX * push.x + cd.boxAxisY * push.y + cd.boxAxisZ * push.z;
                        modified = true;
                    }
                    continue;
                }

                float radius = cd.radius;

                if (cd.isCapsule == 0)
                {
                    float3 center = cd.a;

                    if (hasSweepMotion)
                    {
                        float3 adjStart = sweepStart + cd.motion;
                        float3 adjDelta = particlePos - adjStart;
                        if (RaySphereEntryT(adjStart, adjDelta, center, radius, out float tHit))
                        {
                            particlePos = sweepStart + sweepDelta * tHit;
                            modified = true;
                        }
                    }

                    float3 delta = particlePos - center;
                    float distSqr = math.lengthsq(delta);
                    if (distSqr < cd.radiusSqr && distSqr > 0f)
                    {
                        particlePos = center + (delta / math.sqrt(distSqr)) * radius;
                        modified = true;
                    }
                }
                else
                {
                    float3 p1 = cd.a;
                    float3 p2 = cd.b;

                    if (hasSweepMotion)
                    {
                        float3 adjStart = sweepStart + cd.motion;
                        float3 adjDelta = particlePos - adjStart;
                        if (SweepCapsuleEntry(adjStart, adjDelta, p1, p2, radius, out float tHit))
                        {
                            particlePos = sweepStart + sweepDelta * tHit;
                            modified = true;
                        }
                    }

                    float t = ClosestPointOnSegmentRatio(particlePos, p1, p2);
                    float rAtPoint = math.lerp(cd.startRadius, cd.endRadius, t);
                    float3 closestPoint = math.lerp(p1, p2, t);
                    float3 deltaC = particlePos - closestPoint;
                    float distSqrC = math.lengthsq(deltaC);
                    if (distSqrC < rAtPoint * rAtPoint && distSqrC > 0f)
                    {
                        particlePos = closestPoint + (deltaC / math.sqrt(distSqrC)) * rAtPoint;
                        modified = true;
                    }
                }
            }

            return modified;
        }

        // Mặt phẳng thứ i (0..5) của Box, dạng "độ sâu vào trong" pen = c + dot(n, p_local) (> 0 = phía trong).
        // 0/1: mặt +X/-X (nghiêng theo boxK.x) — 2/3: mặt +Z/-Z (nghiêng theo boxK.y) — 4/5: mặt trên/dưới.
        private static void BoxPlane(JobColliderData cd, int i, out float3 n, out float c)
        {
            switch (i)
            {
                case 0: n = new float3(-1f, cd.boxK.x, 0f) * cd.boxInvNorm.x; c = cd.boxC.x * cd.boxInvNorm.x; break;
                case 1: n = new float3(1f, cd.boxK.x, 0f) * cd.boxInvNorm.x; c = cd.boxC.x * cd.boxInvNorm.x; break;
                case 2: n = new float3(0f, cd.boxK.y, -1f) * cd.boxInvNorm.y; c = cd.boxC.y * cd.boxInvNorm.y; break;
                case 3: n = new float3(0f, cd.boxK.y, 1f) * cd.boxInvNorm.y; c = cd.boxC.y * cd.boxInvNorm.y; break;
                case 4: n = new float3(0f, -1f, 0f); c = cd.boxHalf.y; break;
                default: n = new float3(0f, 1f, 0f); c = cd.boxHalf.y; break;
            }
        }

        // Swept-test đoạn p0→p0+d với khối lồi (cắt theo 6 mặt phẳng trong local space).
        // Chỉ báo trúng khi p0 nằm NGOÀI khối và điểm vào nằm trong [0,1] (giống RaySphereEntryT).
        private static bool RayBoxEntryT(float3 p0, float3 d, JobColliderData cd, out float t)
        {
            t = 0f;
            float3 rel = p0 - cd.a;
            float3 o = new float3(math.dot(rel, cd.boxAxisX), math.dot(rel, cd.boxAxisY), math.dot(rel, cd.boxAxisZ));
            float3 dl = new float3(math.dot(d, cd.boxAxisX), math.dot(d, cd.boxAxisY), math.dot(d, cd.boxAxisZ));

            float tEnter = float.NegativeInfinity, tExit = float.PositiveInfinity;
            for (int i = 0; i < 6; i++)
            {
                BoxPlane(cd, i, out float3 n, out float c);
                float pen0 = c + math.dot(n, o);
                float dp = math.dot(n, dl);
                if (math.abs(dp) < 1e-9f)
                {
                    if (pen0 <= 0f) return false;
                }
                else
                {
                    float tb = -pen0 / dp;
                    if (dp > 0f) tEnter = math.max(tEnter, tb);
                    else tExit = math.min(tExit, tb);
                    if (tEnter > tExit) return false;
                }
            }

            if (tEnter < 0f || tEnter > 1f) return false;
            t = tEnter;
            return true;
        }

        private static bool RaySphereEntryT(float3 p0, float3 d, float3 center, float radius, out float t)
        {
            t = 0f;
            float a = math.dot(d, d);
            if (a < 1e-12f) return false;

            float3 f = p0 - center;
            float c = math.dot(f, f) - radius * radius;
            if (c < 0f) return false;

            float b = 2f * math.dot(f, d);
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;

            float sqrtDisc = math.sqrt(disc);
            float t0 = (-b - sqrtDisc) / (2f * a);
            if (t0 < 0f || t0 > 1f) return false;

            t = t0;
            return true;
        }

        private static bool SweepCapsuleEntry(float3 p0, float3 d, float3 a, float3 bPt, float radius, out float bestT)
        {
            bestT = float.MaxValue;
            bool found = false;

            if (RaySphereEntryT(p0, d, a, radius, out float tA) && tA < bestT) { bestT = tA; found = true; }
            if (RaySphereEntryT(p0, d, bPt, radius, out float tB) && tB < bestT) { bestT = tB; found = true; }

            float3 axis = bPt - a;
            float axisLenSqr = math.dot(axis, axis);
            if (axisLenSqr > 1e-12f)
            {
                float axisLen = math.sqrt(axisLenSqr);
                float3 axisN = axis / axisLen;

                float3 p0a = p0 - a;
                float3 dPerp = d - axisN * math.dot(d, axisN);
                float3 p0aPerp = p0a - axisN * math.dot(p0a, axisN);

                float A = math.dot(dPerp, dPerp);
                if (A > 1e-12f)
                {
                    float C = math.dot(p0aPerp, p0aPerp) - radius * radius;
                    if (C >= 0f)
                    {
                        float B = 2f * math.dot(p0aPerp, dPerp);
                        float disc = B * B - 4f * A * C;
                        if (disc >= 0f)
                        {
                            float sqrtDisc = math.sqrt(disc);
                            float t = (-B - sqrtDisc) / (2f * A);
                            if (t >= 0f && t <= 1f && t < bestT)
                            {
                                float3 pt = p0 + d * t;
                                float axisProj = math.dot(pt - a, axisN);
                                if (axisProj >= 0f && axisProj <= axisLen)
                                {
                                    bestT = t;
                                    found = true;
                                }
                            }
                        }
                    }
                }
            }

            return found;
        }

        private static float ClosestPointOnSegmentRatio(float3 p, float3 a, float3 b)
        {
            float3 ab = b - a;
            float abLenSq = math.dot(ab, ab);
            if (abLenSq < 1e-10f) return 0f;
            float t = math.dot(p - a, ab) / abLenSq;
            return math.clamp(t, 0f, 1f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  JOB 3+4 GỘP — Mô phỏng vị trí (Pass A) rồi chốt rotation (Pass B) cho CÙNG 1 chain,
    //  trong CÙNG 1 lần Execute(chainIndex). Trước đây đây là 2 IJobParallelFor riêng
    //  (Schedule 2 lần, 1 JobHandle dependency giữa 2 lần Schedule) — với chain ngắn (8 bone)
    //  và 240 chain, overhead sync + reschedule giữa 2 job chiếm tỷ trọng lớn so với việc
    //  thực (mỗi Execute chỉ ~8 node). Gộp lại còn 1 Schedule/1 sync barrier.
    //
    //  ĐÚNG THỨ TỰ PHẢI GIỮ NGUYÊN: Pass B (rotation) đọc `.position` của các NODE CON
    //  (index > i trong cùng chain) để tính targetDir — nghĩa là Pass A phải mô phỏng XONG
    //  TOÀN BỘ chain (mọi index từ start..end) trước khi Pass B bắt đầu xử lý bất kỳ node nào
    //  trong CHÍNH chain đó. Vì chain sở hữu 1 đoạn [start,end) riêng và code dưới đây luôn
    //  chạy Pass A xong hoàn toàn rồi mới sang Pass B (trong cùng Execute, cho cùng 1 chain),
    //  bất biến này được giữ nguyên 100% so với bản 2-job cũ dù không còn sync barrier giữa
    //  2 job riêng biệt nữa.
    // ─────────────────────────────────────────────────────────────
    [BurstCompile]
    public struct SimulateAndApplyRotationChainJob : IJobParallelFor
    {
        // [CROSS LINKS] 1 index của job = 1 NHÓM chain (ChainGroup), không còn = 1 chain. Nhóm mặc định
        // chỉ có 1 chain (y hệt trước đây). Nhóm nhiều chain = toàn bộ chain của 1 DynamicBone bật Cross
        // Links — cùng 1 worker xử lý tuần tự nên link chéo giữa các chain KHÔNG gây race. Bất biến an
        // toàn cho [NativeDisableParallelForRestriction]: các nhóm không chồng lấn vùng node, và mọi
        // NodeLink chỉ trỏ vào node của CHÍNH nhóm chứa nó (manager kiểm tra lúc rebuild).
        [ReadOnly] public NativeArray<ChainGroup> groups;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<NodeLink> links;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<ChainRange> chains;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<BoneParams> boneParams;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> rootPosition;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<quaternion> externalRotation;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<JobColliderData> colliders;

        // nodeRotation: in/out. Đầu Pass A dùng giá trị ĐẦU FRAME (từ ReadNodeTransformsJob) làm
        // gpRot cho vật lý; Pass B ghi đè bằng rotation MỚI tính được — y hệt hành vi bản 2-job cũ
        // (Simulate đọc trước khi Apply cùng chain ghi, vì Pass A luôn chạy trước Pass B).
        [NativeDisableParallelForRestriction] public NativeArray<quaternion> nodeRotation;

        [NativeDisableParallelForRestriction] public NativeArray<BoneNode> nodes;
        [NativeDisableParallelForRestriction] public NativeArray<quaternion> outRotation; // để WriteTransformRotationJob dùng
        [NativeDisableParallelForRestriction] public NativeArray<byte> writeMode;         // 0=skip,1=world rot,2=local rot

        public void Execute(int groupIndex)
        {
            ChainGroup g = groups[groupIndex];
            int chainEndIdx = g.chainStart + g.chainCount;
            ChainRange firstChain = chains[g.chainStart];
            // Mọi chain trong 1 nhóm thuộc CÙNG 1 DynamicBone → cùng paramIndex/BoneParams.
            BoneParams p = boneParams[firstChain.paramIndex];

            // [LOD/CULLING] p.active = 0 ⇒ nhân vật ngoài frustum hoặc quá xa camera (xem
            // DynamicBoneManager.ApplyLodAndCulling). Bỏ qua HOÀN TOÀN Pass A + Pass B — giữ
            // nguyên nodes[].position/prevPosition của frame gần nhất ("đóng băng" pose hiện tại).
            //
            // [PERF FIX] Set writeMode = 0 (skip) cho cả nhóm để WriteTransformRotationJob bỏ qua hẳn
            // — set Transform.rotation KHÔNG rẻ dù value không đổi (Transform IO là phần tốn kém,
            // không phải phép tính vật lý bị skip ở đây).
            if (p.active == 0)
            {
                for (int ci = g.chainStart; ci < chainEndIdx; ci++)
                {
                    ChainRange ch = chains[ci];
                    for (int i = ch.start; i < ch.end; i++)
                        writeMode[i] = 0;
                }
                return;
            }

            // Pass A — mô phỏng vị trí của TỪNG chain trong nhóm (cha trước con).
            for (int ci = g.chainStart; ci < chainEndIdx; ci++)
                ChainSimCore.Simulate(chains[ci], p, rootPosition, nodeRotation, externalRotation, colliders, nodes, p.dt);

            // Pass A' — giải Cross Links giữa các chain trong nhóm (chỉ khi nhóm có link).
            if (g.linkCount > 0)
            {
                int nodeStart = firstChain.start;
                int nodeEnd = chains[chainEndIdx - 1].end;
                ChainSimCore.SolveLinks(nodeStart, nodeEnd, p, links, g.linkStart, g.linkCount, colliders, nodes);
            }

            // Pass B — chốt rotation toàn bộ chain, dùng vị trí MỚI (sau Pass A và Pass A').
            for (int ci = g.chainStart; ci < chainEndIdx; ci++)
                ChainRotationCore.ApplyRotation(chains[ci], externalRotation, nodes, nodeRotation, outRotation, writeMode);
        }
    }

    // ChainSimCore.ApplyRotation — pass B của job gộp (xem SimulateAndApplyRotationChainJob).
    // Đây là phần thân gốc của ApplyRotationChainJob, chỉ đổi từ Execute(chainIndex) struct
    // riêng thành 1 static method dùng chung `nodes`/buffer đã truyền vào từ Pass A.
    internal static class ChainRotationCore
    {
        public static void ApplyRotation(ChainRange chain, NativeArray<quaternion> externalRotation,
            NativeArray<BoneNode> nodes, NativeArray<quaternion> nodeRotation,
            NativeArray<quaternion> outRotation, NativeArray<byte> writeMode)
        {
            for (int i = chain.start; i < chain.end; i++)
            {
                BoneNode node = nodes[i];
                int end = node.subtreeEnd;
                if (end == i + 1) continue; // leaf, không xoay

                int p = node.parent;
                quaternion parentRot = p >= 0 ? nodeRotation[p]
                    : (chain.externalIndex >= 0 ? externalRotation[chain.externalIndex] : quaternion.identity);

                quaternion baseRot = math.mul(parentRot, node.localRotation);
                float3 nodePos = node.position;

                float3 defaultDirSum = float3.zero;
                float3 targetDirSum = float3.zero;

                for (int c = i + 1; c < end; c = nodes[c].subtreeEnd)
                {
                    BoneNode child = nodes[c];
                    float3 defaultDir = math.mul(baseRot, child.localPosition);
                    float3 targetDir = child.position - nodePos;
                    if (math.all(defaultDir == float3.zero) || math.all(targetDir == float3.zero)) continue;

                    float weight = math.max(child.length, 0.0001f);
                    defaultDirSum += math.normalizesafe(defaultDir) * weight;
                    targetDirSum += math.normalizesafe(targetDir) * weight;
                }

                if (!math.all(defaultDirSum == float3.zero) && !math.all(targetDirSum == float3.zero))
                {
                    quaternion fromTo = FromToRotation(defaultDirSum, targetDirSum);
                    quaternion applied = math.mul(fromTo, baseRot);
                    nodeRotation[i] = applied;
                    outRotation[i] = applied;
                    writeMode[i] = 1; // world rotation
                }
                else
                {
                    nodeRotation[i] = baseRot;
                    outRotation[i] = node.localRotation;
                    writeMode[i] = 2; // local rotation fallback
                }
            }
        }

        private static quaternion FromToRotation(float3 from, float3 to)
        {
            from = math.normalizesafe(from);
            to = math.normalizesafe(to);
            float d = math.clamp(math.dot(from, to), -1f, 1f);
            if (d > 0.999999f) return quaternion.identity;
            if (d < -0.999999f)
            {
                float3 axis = math.cross(new float3(1, 0, 0), from);
                if (math.lengthsq(axis) < 1e-6f) axis = math.cross(new float3(0, 1, 0), from);
                axis = math.normalizesafe(axis);
                return quaternion.AxisAngle(axis, math.PI);
            }
            float3 c = math.cross(from, to);
            quaternion q = new quaternion(c.x, c.y, c.z, 1f + d);
            return math.normalize(q);
        }
    }

    /// <summary>
    /// Ghi rotation đã tính (writeMode 1 = world, 2 = local) trở lại Transform thật.
    /// [PERF fix #2] Chạy trên TransformAccessArray CHỈ CHỨA node non-leaf (node có con — chỉ
    /// những node này mới có writeMode != 0, xem ChainRotationCore.ApplyRotation: leaf luôn
    /// `continue` và giữ writeMode = 0). `nodeIndexMap[index]` trỏ từ slot trong mảng rút gọn này
    /// về index thật trong `outRotation`/`writeMode` (vẫn giữ nguyên kích thước = tổng số node,
    /// để không phải sửa lại indexing trong ChainRotationCore/SimulateAndApplyRotationChainJob).
    /// Với 60 player × 4 chain × 8 bone: ~1920 node nhưng chỉ ~240 non-leaf → Execute() giảm ~87%.
    /// </summary>
    [BurstCompile]
    public struct WriteTransformRotationJob : IJobParallelForTransform
    {
        // [FIX] Các NativeArray dưới đây được đọc qua `nodeIndexMap[index]` (index vào mảng
        // node toàn cục) chứ KHÔNG phải `index` (index vào TransformAccessArray) — safety system
        // của IJobParallelForTransform mặc định chỉ cho phép truy cập đúng `index` hiện tại, nên
        // BẮT BUỘC phải có [NativeDisableParallelForRestriction] mới không bị FailOutOfRangeError.
        // An toàn vì cả 3 mảng đều [ReadOnly] — không worker nào ghi, chỉ đọc chéo index.
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int> nodeIndexMap;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<quaternion> outRotation;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<byte> writeMode;

        public void Execute(int index, TransformAccess transform)
        {
            int nodeIndex = nodeIndexMap[index];
            byte mode = writeMode[nodeIndex];
            if (mode == 1) transform.rotation = outRotation[nodeIndex];
            else if (mode == 2) transform.localRotation = outRotation[nodeIndex];
        }
    }

    /// <summary>Ghi position của root (bị ghim vào Transform) — chỉ đọc, không cần job riêng vì
    /// root luôn dùng transform.position trực tiếp, không có gì để "ghi ngược" ở bước này.</summary>
}