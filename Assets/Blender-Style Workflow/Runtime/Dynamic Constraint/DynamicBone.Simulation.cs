using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        // ══════════════════════════════════════════════════════════
        //  MAIN LOOP
        // ══════════════════════════════════════════════════════════
        void LateUpdate()
        {
            if (_nodeCount == 0) return;

            // [LOG] Đếm frame để gắn nhãn thời điểm cho mỗi dòng collision log.
            _frameCounter++;

#if UNITY_EDITOR
            Vector3 managerPos = transform.position;
            dragDirection = managerPos - lastManagerPosition;
            lastManagerPosition = managerPos;
#endif

            // [0.1][B1] Clamp dt: một frame hitch (ví dụ do load asset, GC spike) không
            // còn khiến gravity/spring bị nhân vọt lên và làm bone "nổ" ra xa rest pose.
            // 1/30s là ngưỡng an toàn (vẫn mượt ở 30 FPS trở lên, chặn được spike nặng).
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // 0. Đọc Transform (position root, rotation các node có con) và dữ liệu collider
            //    ĐÚNG 1 LẦN. Trong suốt bước mô phỏng bên dưới không có native call nào nữa.
            ReadTransforms();
            GatherColliders();

            // 1. Mô phỏng tất cả chain
            SimulatePhysics(dt);

            // [FIX-TUNNEL-2] 1b. Chụp lại vị trí NGAY TRƯỚC khi cloth links dịch chuyển node,
            // để ResolveCollisionsAfterClothLinks() có thể swept-test đúng đoạn di chuyển do
            // cloth links gây ra, thay vì chỉ static-test vị trí cuối cùng.
            bool needClothCollisionResolve = enableClothLinks && _colCount > 0;
            if (needClothCollisionResolve) CapturePreClothPositions();

            // 2. Kéo các chain lại với nhau qua liên kết ngang (phải chạy sau khi
            //    tất cả chain đã bước xong, nếu không chain sau sẽ kéo chain trước lệch)
            SolveClothLinks(dt);

            // [FIX-TUNNEL] 2b. SolveClothLinks() (kéo link + EnforceBoneLength) dịch chuyển
            // position mà KHÔNG hề kiểm tra collider — một node vừa được đẩy ra ngoài collider
            // ở bước 1 hoàn toàn có thể bị link ngang / ép độ dài xương kéo NGƯỢC LẠI vào trong
            // collider ở bước 2. Cần 1 pass resolve áp dụng lại sau cùng — CÓ sweep test dựa trên
            // snapshot đã chụp ở 1b, để không bỏ lọt trường hợp cloth link kéo particle "lướt
            // xuyên" qua một collider mỏng chỉ trong bước này.
            if (needClothCollisionResolve)
                ResolveCollisionsAfterClothLinks(dt);

            // 3. Chốt rotation từ vị trí cuối cùng
            ApplyRotations();

            // [FIX-TUNNEL v2] 4. Lưu lại vị trí collider CUỐI frame này để làm mốc "vị trí
            // trước đó" cho swept test ở frame KẾ TIẾP. Phải làm SAU CÙNG (sau khi đã dùng
            // giá trị cache cũ ở bước 1).
            UpdateColliderMotionCache();
        }

        /// <summary>
        /// Đọc từ Transform những gì mô phỏng cần:
        ///  • position của root (bị ghim theo Transform),
        ///  • world rotation của mọi node CÓ CON (dùng làm rotation của "parent" khi tính rest
        ///    gravity và của "grandParent" khi tính rest direction),
        ///  • world rotation của Transform cha nằm ngoài cây (chỉ root cần).
        /// Trước đây mỗi node gọi riêng parent.transform.TransformDirection(...), transform.parent,
        /// grandParent.rotation → 2–3 native call / node / frame; giờ tối đa 1 call / node có con.
        /// </summary>
        private void ReadTransforms()
        {
            for (int r = 0; r < _rootIndices.Length; r++)
            {
                int i = _rootIndices[r];
                _nodes[i].position = _tf[i].position;

                if (_nodes[i].subtreeEnd > i + 1)
                {
                    Transform ext = _rootParentTf[i];
                    _extRot[i] = ext != null ? ext.rotation : Quaternion.identity;
                }
            }

            for (int i = 0; i < _nodeCount; i++)
            {
                if (_nodes[i].subtreeEnd > i + 1)
                    _rot[i] = _tf[i].rotation;
            }
        }

        /// <summary>
        /// [FIX-TUNNEL-2] Chụp vị trí hiện tại của mọi node để làm "sweepStart" thật cho
        /// ResolveCollisionsAfterClothLinks(). Rẻ (O(n) copy struct), chỉ chạy khi thật sự cần
        /// (enableClothLinks && có collider).
        /// </summary>
        private void CapturePreClothPositions()
        {
            NodeData[] nodes = _nodes;
            Vector3[] snap = _preClothPositions;
            for (int i = 0; i < _nodeCount; i++)
                snap[i] = nodes[i].position;
        }

        private void SimulatePhysics(float dt)
        {
            NodeData[] nodes = _nodes;

            // [PERF] Mọi giá trị không phụ thuộc node được tính 1 lần / frame thay vì / node
            // (trước đây Mathf.Exp, gravity.magnitude, Clamp01 … chạy lại cho từng node).
            float dt2 = dt * dt;
            Vector3 g = gravity;
            float gravMag = g.magnitude;
            bool restGrav = useRestPoseGravity;
            float restBlend = restPoseGravityBlend;
            float inertiaK = inertia;

            // [0.2][A5] Exponential response thay vì tuyến tính: springStiffness*mult*dt
            // tuyến tính sẽ cho hành vi lệ thuộc frame rate (ở FPS thấp, springT có thể
            // vọt qua 1 trong khi ở FPS cao lại quá nhỏ). Công thức mũ hội tụ về cùng
            // một "half-life" bất kể dt, nên spring cảm giác giống nhau ở mọi frame rate.
            float springT = 1f - Mathf.Exp(-springStiffness * springForceMultiplier * dt);
            float tangentialDamp = Mathf.Clamp01(1f - damping * 15f * dt);
            float radialDamp = Mathf.Clamp01(1f - damping * 5f * dt);

            bool doCollide = _colCount > 0;
            int collisionIterations = Mathf.Max(1, collisionSolveIterations);
            float falloff = collisionParentFalloff;

            for (int i = 0; i < _nodeCount; i++)
            {
                int pi = nodes[i].parent;
                if (pi < 0) continue; // root: chỉ bị ghim theo Transform

                // [FIX-TUNNEL] Vị trí ĐÃ ĐƯỢC GIẢI QUYẾT (non-penetrating) ở cuối frame trước —
                // đầu đoạn thẳng (segment) cho swept collision test bên dưới.
                Vector3 frameStartPos = nodes[i].position;
                Vector3 parentPos = nodes[pi].position;
                float length = nodes[i].length;

                Vector3 velocity = frameStartPos - nodes[i].prevPosition;

                // --- GRAVITY ---
                Vector3 effectiveGravity;
                if (restGrav)
                {
                    Vector3 restGravityWorld = (_rot[pi] * nodes[i].restGravityLocal) * gravMag;
                    effectiveGravity = Vector3.Lerp(g, restGravityWorld, restBlend);
                }
                else
                {
                    effectiveGravity = g;
                }

                // --- INERTIA ---
                Vector3 parentDelta = parentPos - nodes[pi].prevPosition;
                Vector3 inertiaForce = -parentDelta * inertiaK;

                // --- INTEGRATE ---
                Vector3 nextPos = frameStartPos + velocity + (effectiveGravity + inertiaForce) * dt2;

                // --- LENGTH CONSTRAINT ---
                Vector3 toNext = nextPos - parentPos;
                nextPos = parentPos + toNext.normalized * length;

                // --- SPRING → REST POSE ---
                // rotation của grandParent: node cha của parent nếu có, ngược lại là Transform ngoài cây.
                int gi = nodes[pi].parent;
                Quaternion gpRot = gi >= 0 ? _rot[gi] : _extRot[pi];
                Vector3 restDir = (gpRot * nodes[i].restDirBase).normalized;
                Vector3 restOnSphere = parentPos + restDir * length;

                nextPos = Vector3.Lerp(nextPos, restOnSphere, springT);

                toNext = nextPos - parentPos;
                Vector3 newPos = parentPos + toNext.normalized * length;

                // --- DAMPING (tangential) ---
                Vector3 radialDir = (newPos - parentPos).normalized;
                Vector3 velRadial = Vector3.Project(velocity, radialDir);
                Vector3 velTangential = velocity - velRadial;

                nodes[i].prevPosition = newPos
                                        - velRadial * radialDamp
                                        - velTangential * tangentialDamp;

                // --- COLLISIONS + PROPAGATE LÊN PARENT ---
                if (doCollide)
                {
                    Vector3 pos = newPos;
                    int hitCollider = -1;

                    // [5.5][B4] Nhiều vòng lặp thay vì single-pass (Gauss-Seidel relaxation).
                    // [FIX-TUNNEL] Iteration 0 dùng SWEPT test (đoạn frameStartPos -> pos),
                    // các iteration sau dùng test tĩnh.
                    // [PERF] Dừng sớm khi 1 iteration không thay đổi gì: iteration kế tiếp có
                    // cùng đầu vào + cùng phép test nên chắc chắn cũng không đổi → kết quả y hệt,
                    // nhưng trường hợp phổ biến (particle không chạm collider) chỉ tốn 1 vòng.
                    for (int it = 0; it < collisionIterations; it++)
                    {
                        if (!SolveCollisions(ref pos, frameStartPos, it == 0, out int hit)) break;
                        if (hit >= 0) hitCollider = hit;
                    }

                    Vector3 correction = pos - newPos;
                    nodes[i].position = pos;

                    // [FIX-JITTER] Va chạm là một RÀNG BUỘC VỊ TRÍ (constraint), không phải một cú
                    // đẩy có động năng. prevPosition ở trên đã được chốt DỰA TRÊN newPos (vị trí
                    // TRƯỚC khi bị đẩy ra khỏi collider) — nếu không dịch nó theo đúng correction,
                    // frame sau velocity = frameStartPos - prevPosition sẽ vô tình "nhìn thấy" toàn
                    // bộ độ dịch chuyển do va chạm như một vận tốc thật, cộng thẳng vào gia tốc mà
                    // không hề qua damping. Đây chính là nguyên nhân gây co giật/rung/bắn văng khi
                    // va chạm nhanh hoặc khi particle khởi đầu đã nằm sâu trong collider — mỗi frame
                    // vận tốc ảo lại được "làm mới" và khuếch đại thêm bởi spring kéo ngược.
                    if (correction.sqrMagnitude > 0f)
                    {
                        nodes[i].prevPosition += correction;

                        // [LOG] Va chạm trực tiếp lên chính node i.
                        if (enableCollisionLogging)
                            RecordCollision(i, hitCollider, correction, dt, CollisionLogStage.Main,
                                CollisionLogKind.Direct);

                        // Nếu có va chạm thật sự thì truyền correction ngược lên parent chain
                        // với falloff giảm dần: parent nhận correction * falloff^1, grandparent * falloff^2, ...
                        PropagateCollisionToParents(pi, correction, falloff, dt, CollisionLogStage.Main);
                    }
                }
                else
                {
                    nodes[i].position = newPos;
                }
            }
        }

        /// <summary>
        /// Truyền correction vector ngược lên parent chain với hệ số falloff giảm dần mỗi cấp.
        /// Bone root (parent == -1) không bao giờ bị dịch chuyển vì nó bị ghim vào Transform.
        /// (Vòng lặp thay cho đệ quy cũ — cùng điều kiện dừng.)
        /// </summary>

        // ══════════════════════════════════════════════════════════
        //  APPLY ROTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Chốt rotation cho các node CÓ CON, theo thứ tự cha → con.
        ///
        /// [PERF] Trước đây mỗi node gọi: set localRotation + (K lần) TransformDirection cho K con
        /// + get rotation + set rotation = 3+K native call, và mỗi lần get rotation lại buộc Unity
        /// tính lại world matrix vì hierarchy vừa bị dirty. Giờ world rotation được tự tính bằng
        /// parentApplied * localRotation (đúng công thức Unity dùng), nên mỗi node chỉ còn ĐÚNG
        /// 1 native call ghi (rotation hoặc localRotation).
        /// </summary>
        private void ApplyRotations()
        {
            NodeData[] nodes = _nodes;

            for (int i = 0; i < _nodeCount; i++)
            {
                int end = nodes[i].subtreeEnd;
                if (end == i + 1) continue; // leaf: không có gì để xoay

                int p = nodes[i].parent;

                // Rotation của node SAU KHI đặt lại localRotation gốc (chưa aim).
                Quaternion baseRot = (p >= 0 ? _rot[p] : _extRot[i]) * nodes[i].localRotation;
                Vector3 nodePos = nodes[i].position;

                // [1.4][B2] Trung bình hướng của TẤT CẢ children thay vì chỉ children[0]; weighted
                // theo rest length của từng nhánh. Node chỉ có 1 con → giống hệt công thức đơn.
                Vector3 defaultDirSum = Vector3.zero;
                Vector3 targetDirSum = Vector3.zero;

                for (int c = i + 1; c < end; c = nodes[c].subtreeEnd)
                {
                    Vector3 defaultDir = baseRot * nodes[c].localPosition;
                    Vector3 targetDir = nodes[c].position - nodePos;
                    if (defaultDir == Vector3.zero || targetDir == Vector3.zero) continue;

                    float weight = Mathf.Max(nodes[c].length, 0.0001f);
                    defaultDirSum += defaultDir.normalized * weight;
                    targetDirSum += targetDir.normalized * weight;
                }

                if (defaultDirSum != Vector3.zero && targetDirSum != Vector3.zero)
                {
                    Quaternion applied = Quaternion.FromToRotation(defaultDirSum, targetDirSum) * baseRot;
                    _rot[i] = applied;
                    _tf[i].rotation = applied;
                }
                else
                {
                    _rot[i] = baseRot;
                    _tf[i].localRotation = nodes[i].localRotation;
                }
            }
        }

    }
}