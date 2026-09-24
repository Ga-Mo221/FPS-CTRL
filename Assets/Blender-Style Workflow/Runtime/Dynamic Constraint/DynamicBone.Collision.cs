using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        // ══════════════════════════════════════════════════════════
        //  COLLIDER DATA (per-frame)
        // ══════════════════════════════════════════════════════════
        private ColliderState GetColliderState(int slot, Collider col)
        {
            ColliderState st = _colStates[slot];
            if (st != null && ReferenceEquals(st.collider, col)) return st;

            st = new ColliderState(col);
            _colStates[slot] = st;
            return st;
        }

        private void EnsureColliderCapacity(int n)
        {
            if (_colStates.Length < n) System.Array.Resize(ref _colStates, n);
            if (_colData.Length < n) System.Array.Resize(ref _colData, n);
            if (_colSourceSlot.Length < n) System.Array.Resize(ref _colSourceSlot, n);
        }

        /// <summary>
        /// Tính world-space data của mọi collider hợp lệ MỘT LẦN cho cả frame (collider không di
        /// chuyển trong bước mô phỏng). Bỏ qua collider null / disabled / inactive như cũ, và chỉ
        /// hỗ trợ Sphere + Capsule như cũ.
        /// </summary>
        private void GatherColliders()
        {
            _colCount = 0;
            if (!enableColliders) return;

            int total = colliders.Count;
            if (total == 0) return;

            EnsureColliderCapacity(total);
            float pr = particleRadius;
            int count = 0;

            for (int i = 0; i < total; i++)
            {
                Collider col = colliders[i];
                if (col == null) continue;

                ColliderState st = GetColliderState(i, col);
                if (!st.IsSupported) continue;
                if (!col.enabled || !st.gameObject.activeInHierarchy) continue;

                Transform t = st.transform;
                Vector3 s = t.lossyScale;
                ColliderData cd = new ColliderData();

                if (st.sphere != null)
                {
                    SphereCollider sphere = st.sphere;
                    Vector3 center = t.TransformPoint(sphere.center);
                    float radius = sphere.radius * Mathf.Max(Mathf.Max(s.x, s.y), s.z) + pr;

                    cd.isCapsule = false;
                    cd.a = center;
                    cd.b = center;
                    cd.radius = radius;
                    cd.motion = st.hasPrev ? center - st.prevAnchor : Vector3.zero;
                }
                else
                {
                    CapsuleCollider capsule = st.capsule;
                    Vector3 center = t.TransformPoint(capsule.center);
                    Vector3 dir = t.TransformDirection(st.localAxis);

                    float dirScale, maxScale;
                    if (st.axis == 0)
                    {
                        dirScale = s.x;
                        maxScale = Mathf.Max(s.y, s.z);
                    }
                    else if (st.axis == 2)
                    {
                        dirScale = s.z;
                        maxScale = Mathf.Max(s.x, s.y);
                    }
                    else
                    {
                        dirScale = s.y;
                        maxScale = Mathf.Max(s.x, s.z);
                    }

                    float height = Mathf.Max(0f, (st.heightUnscaled * dirScale) / 2f - st.radiusUnscaled * maxScale);

                    cd.isCapsule = true;
                    cd.a = center + dir * height;
                    cd.b = center - dir * height;
                    cd.radius = st.radiusUnscaled * maxScale + pr;
                    // Anchor = trung điểm p1-p2 = tâm capsule
                    cd.motion = st.hasPrev ? center - st.prevAnchor : Vector3.zero;
                }

                cd.radiusSqr = cd.radius * cd.radius;
                cd.min = Vector3.Min(cd.a, cd.b) - new Vector3(cd.radius, cd.radius, cd.radius);
                cd.max = Vector3.Max(cd.a, cd.b) + new Vector3(cd.radius, cd.radius, cd.radius);

                // [LOG] Ghi lại slot GỐC trong danh sách `colliders` (Inspector) — _colData bị
                // nén (bỏ qua null/disabled) nên index của nó khác index gốc; cần bảng ánh xạ
                // này để log collision truy ngược đúng tên collider.
                _colSourceSlot[count] = i;
                _colData[count++] = cd;
            }

            _colCount = count;
        }

        /// <summary>
        /// [FIX-TUNNEL v2] Chụp lại vị trí "anchor" hiện tại của từng collider (tâm sphere /
        /// tâm capsule) để dùng làm mốc "vị trí frame trước" cho swept test tương đối
        /// ở frame kế tiếp. Chi phí O(số collider), không phải O(particle × collider).
        /// </summary>
        private void UpdateColliderMotionCache()
        {
            if (!enableColliders) return;
            int total = colliders.Count;
            if (total == 0) return;

            EnsureColliderCapacity(total);

            for (int i = 0; i < total; i++)
            {
                Collider col = colliders[i];
                if (col == null) continue;

                ColliderState st = GetColliderState(i, col);
                if (st.sphere != null)
                {
                    st.prevAnchor = st.transform.TransformPoint(st.sphere.center);
                    st.hasPrev = true;
                }
                else if (st.capsule != null)
                {
                    st.prevAnchor = st.transform.TransformPoint(st.capsule.center);
                    st.hasPrev = true;
                }
            }
        }

        /// <summary>
        /// [FIX-TUNNEL][FIX-TUNNEL-2] Chạy lại collision resolve sau khi cloth links đã kéo vị
        /// trí các node lệch đi. Dùng đúng logic Gauss-Seidel + parent propagation như
        /// SimulatePhysics() để nhất quán hành vi.
        ///
        /// [FIX-TUNNEL-2] Iteration đầu tiên giờ CÓ sweep test, lấy sweepStart từ
        /// _preClothPositions (vị trí node NGAY TRƯỚC SolveClothLinks) thay vì từ vị trí hiện tại
        /// — nếu không, đoạn di chuyển do chính cloth links/EnforceBoneLength gây ra hoàn toàn
        /// không được kiểm tra continuous, và particle có thể "lướt xuyên" một collider mỏng chỉ
        /// trong bước này dù bước 1 (SimulatePhysics) đã sweep-test đúng.
        /// </summary>
        private void ResolveCollisionsAfterClothLinks(float dt)
        {
            int collisionIterations = Mathf.Max(1, collisionSolveIterations);
            float falloff = collisionParentFalloff;
            NodeData[] nodes = _nodes;
            Vector3[] preClothPos = _preClothPositions;

            for (int i = 0; i < _nodeCount; i++)
            {
                int p = nodes[i].parent;
                if (p < 0) continue;

                Vector3 pos = nodes[i].position;
                Vector3 before = pos;
                Vector3 sweepStart = preClothPos[i];
                int hitCollider = -1;

                for (int it = 0; it < collisionIterations; it++)
                {
                    if (!SolveCollisions(ref pos, sweepStart, it == 0, out int hit)) break;
                    if (hit >= 0) hitCollider = hit;
                }

                nodes[i].position = pos;

                Vector3 correction = pos - before;
                if (correction.sqrMagnitude > 0f)
                {
                    // [FIX-JITTER] Cùng lý do như trong SimulatePhysics(): correction do va chạm
                    // là ràng buộc vị trí, không phải vận tốc thật. Không dịch prevPosition theo
                    // đây sẽ khiến frame sau "nhìn thấy" một vận tốc ảo giống hệt bug đã sửa ở
                    // SimulatePhysics(), chỉ khác là xảy ra ở pass resolve-sau-cloth-links này.
                    nodes[i].prevPosition += correction;

                    if (enableCollisionLogging)
                        RecordCollision(i, hitCollider, correction, dt, CollisionLogStage.AfterClothLinks,
                            CollisionLogKind.Direct);

                    PropagateCollisionToParents(p, correction, falloff, dt, CollisionLogStage.AfterClothLinks);
                }
            }
        }

        /// <summary>
        /// Truyền correction vector ngược lên parent chain với hệ số falloff giảm dần mỗi cấp.
        /// Bone root (parent == -1) không bao giờ bị dịch chuyển vì nó bị ghim vào Transform.
        /// (Vòng lặp thay cho đệ quy cũ — cùng điều kiện dừng.)
        ///
        /// [LOG] dt/stage chỉ dùng để ghi log (RecordCollision) khi enableCollisionLogging bật;
        /// không ảnh hưởng gì tới vật lý.
        /// </summary>
        private void PropagateCollisionToParents(int nodeIdx, Vector3 correction, float falloff, float dt,
            CollisionLogStage stage)
        {
            NodeData[] nodes = _nodes;

            // Dừng khi hết parent hoặc magnitude quá nhỏ (tránh float noise vô tận)
            while (nodeIdx >= 0 && nodes[nodeIdx].parent >= 0 && correction.sqrMagnitude >= 0.000001f)
            {
                // Áp correction đã scale vào position và đồng thời dịch prevPosition để
                // velocity (position - prevPosition) không bị spike kỳ lạ sau collision.
                correction *= falloff;
                nodes[nodeIdx].position += correction;
                nodes[nodeIdx].prevPosition += correction;

                if (enableCollisionLogging)
                    RecordCollision(nodeIdx, -1, correction, dt, stage, CollisionLogKind.Propagated);

                nodeIdx = nodes[nodeIdx].parent;
            }
        }

        /// <summary>
        /// Giải va chạm cho 1 particle với toàn bộ colliders (dữ liệu đã tính sẵn ở GatherColliders).
        /// </summary>
        /// <param name="particlePos">Vị trí particle — bị ghi đè nếu có va chạm.</param>
        /// <param name="sweepStart">
        /// [FIX-TUNNEL] Điểm xuất phát của đoạn di chuyển trong frame này. Dựng đoạn thẳng
        /// sweepStart -> particlePos cho continuous collision test.
        /// </param>
        /// <param name="useSweep">
        /// True: chạy swept test TRƯỚC rồi mới static projection. False: chỉ static test.
        /// </param>
        /// <param name="hitColliderSlot">
        /// [LOG] Index (trong danh sách `colliders` ở Inspector) của collider GẦN NHẤT đã sửa
        /// particlePos trong lệnh gọi này, hoặc -1 nếu không va chạm. Chỉ để phục vụ logging/debug
        /// — nếu nhiều collider cùng đụng trong 1 lần gọi, đây là collider ghi đè SAU CÙNG (không
        /// nhất thiết là collider gây correction lớn nhất).
        /// </param>
        /// <returns>True nếu particlePos đã bị thay đổi (dùng để dừng sớm vòng relaxation).</returns>
        private bool SolveCollisions(ref Vector3 particlePos, Vector3 sweepStart, bool useSweep, out int hitColliderSlot)
        {
            hitColliderSlot = -1;

            // Đoạn di chuyển của PARTICLE trong frame này (chỉ có ý nghĩa khi useSweep = true).
            Vector3 sweepDelta = particlePos - sweepStart;
            bool hasSweepMotion = useSweep && sweepDelta.sqrMagnitude > 1e-10f;
            bool modified = false;

            ColliderData[] data = _colData;
            int[] sourceSlot = _colSourceSlot;
            int n = _colCount;

            for (int c = 0; c < n; c++)
            {
                // [1.1][A1] Broadphase: AABB (đã có sẵn, đã cộng radius) trước khi test hình học chính xác.
                float lx = particlePos.x, ly = particlePos.y, lz = particlePos.z;
                float hx = lx, hy = ly, hz = lz;
                if (hasSweepMotion)
                {
                    if (sweepStart.x < lx) lx = sweepStart.x; else hx = sweepStart.x > hx ? sweepStart.x : hx;
                    if (sweepStart.y < ly) ly = sweepStart.y; else hy = sweepStart.y > hy ? sweepStart.y : hy;
                    if (sweepStart.z < lz) lz = sweepStart.z; else hz = sweepStart.z > hz ? sweepStart.z : hz;
                }

                if (hx < data[c].min.x || lx > data[c].max.x ||
                    hy < data[c].min.y || ly > data[c].max.y ||
                    hz < data[c].min.z || lz > data[c].max.z)
                    continue;

                float radius = data[c].radius;

                if (!data[c].isCapsule)
                {
                    Vector3 center = data[c].a;

                    if (hasSweepMotion)
                    {
                        // [FIX-TUNNEL v2] Quy về hệ quy chiếu CHUYỂN ĐỘNG TƯƠNG ĐỐI: dịch điểm đầu
                        // đoạn sweep theo đúng độ dịch chuyển của collider trong frame
                        // (motion = anchor hiện tại - anchor frame trước). Bắt được cả trường hợp
                        // collider "lướt" qua particle gần như đứng yên.
                        Vector3 adjStart = sweepStart + data[c].motion;
                        Vector3 adjDelta = particlePos - adjStart;

                        if (RaySphereEntryT(adjStart, adjDelta, center, radius, out float tHit))
                        {
                            // t giống nhau giữa đường thật và đường đã quy chiếu (chỉ lệch nhau
                            // 1 phép tịnh tiến hằng số) → cắt đúng vị trí THẬT trên đường đi thật.
                            particlePos = sweepStart + sweepDelta * tHit;
                            modified = true;
                            hitColliderSlot = sourceSlot[c];
                        }
                    }

                    // Static projection: LUÔN chạy sau sweep để đảm bảo kết quả cuối cùng
                    // CHẮC CHẮN nằm ngoài mặt cầu hiện tại.
                    Vector3 delta = particlePos - center;
                    float distSqr = delta.sqrMagnitude;
                    if (distSqr < data[c].radiusSqr && distSqr > 0f)
                    {
                        particlePos = center + (delta / Mathf.Sqrt(distSqr)) * radius;
                        modified = true;
                        hitColliderSlot = sourceSlot[c];
                    }
                }
                else
                {
                    Vector3 p1 = data[c].a;
                    Vector3 p2 = data[c].b;

                    if (hasSweepMotion)
                    {
                        // [FIX-TUNNEL v2] Cùng nguyên lý chuyển động tương đối như sphere.
                        Vector3 adjStart = sweepStart + data[c].motion;
                        Vector3 adjDelta = particlePos - adjStart;

                        if (SweepCapsuleEntry(adjStart, adjDelta, p1, p2, radius, out float tHit))
                        {
                            particlePos = sweepStart + sweepDelta * tHit;
                            modified = true;
                            hitColliderSlot = sourceSlot[c];
                        }
                    }

                    // Static fallback / projection cuối cùng — luôn chạy.
                    Vector3 closestPoint = ClosestPointOnSegment(p1, p2, particlePos);
                    Vector3 deltaC = particlePos - closestPoint;
                    float distSqrC = deltaC.sqrMagnitude;
                    if (distSqrC < data[c].radiusSqr && distSqrC > 0f)
                    {
                        particlePos = closestPoint + (deltaC / Mathf.Sqrt(distSqrC)) * radius;
                        modified = true;
                        hitColliderSlot = sourceSlot[c];
                    }
                }
            }

            return modified;
        }

        /// <summary>
        /// [FIX-TUNNEL] Ray/segment vs sphere — tìm t (0..1) của điểm ĐẦU TIÊN mà đoạn thẳng
        /// p0 -> p0+d đi VÀO bên trong sphere (center, radius). Trả về false nếu:
        /// - Đoạn thẳng không cắt sphere trong khoảng [0,1], hoặc
        /// - p0 đã nằm sẵn trong sphere (c &lt; 0) — trường hợp này để static test xử lý,
        ///   vì "điểm bắt đầu" ở đây không phải vị trí vật lý thật ngoài collider.
        /// </summary>
        private static bool RaySphereEntryT(Vector3 p0, Vector3 d, Vector3 center, float radius, out float t)
        {
            t = 0f;
            float a = Vector3.Dot(d, d);
            if (a < 1e-12f) return false;

            Vector3 f = p0 - center;
            float c = Vector3.Dot(f, f) - radius * radius;
            if (c < 0f) return false; // xuất phát đã ở trong sphere → không phải tunneling

            float b = 2f * Vector3.Dot(f, d);
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false; // đường thẳng (kéo dài vô hạn) không chạm sphere

            float sqrtDisc = Mathf.Sqrt(disc);
            float t0 = (-b - sqrtDisc) / (2f * a);
            if (t0 < 0f || t0 > 1f) return false; // điểm chạm nằm ngoài đoạn di chuyển frame này

            t = t0;
            return true;
        }

        /// <summary>
        /// [FIX-TUNNEL] Ray/segment vs capsule (đoạn trục a-b, bán kính radius) — capsule =
        /// hình trụ (thân) + 2 chỏm cầu (2 đầu). Trả về t nhỏ nhất trong [0,1] (điểm chạm ĐẦU
        /// TIÊN dọc theo đoạn di chuyển), hoặc false nếu đoạn di chuyển không cắt capsule.
        /// </summary>
        private static bool SweepCapsuleEntry(Vector3 p0, Vector3 d, Vector3 a, Vector3 bPt, float radius,
            out float bestT)
        {
            bestT = float.MaxValue;
            bool found = false;

            // --- 1. Hai chỏm cầu ở 2 đầu capsule ---
            if (RaySphereEntryT(p0, d, a, radius, out float tA) && tA < bestT)
            {
                bestT = tA;
                found = true;
            }
            if (RaySphereEntryT(p0, d, bPt, radius, out float tB) && tB < bestT)
            {
                bestT = tB;
                found = true;
            }

            // --- 2. Thân hình trụ (infinite cylinder dọc trục a->bPt, sau đó clamp theo chiều dài) ---
            Vector3 axis = bPt - a;
            float axisLenSqr = Vector3.Dot(axis, axis);
            if (axisLenSqr > 1e-12f)
            {
                float axisLen = Mathf.Sqrt(axisLenSqr);
                Vector3 axisN = axis / axisLen;

                Vector3 p0a = p0 - a;
                // Chiếu ray và điểm p0 xuống mặt phẳng vuông góc với trục capsule.
                Vector3 dPerp = d - axisN * Vector3.Dot(d, axisN);
                Vector3 p0aPerp = p0a - axisN * Vector3.Dot(p0a, axisN);

                float A = Vector3.Dot(dPerp, dPerp);
                if (A > 1e-12f)
                {
                    float C = Vector3.Dot(p0aPerp, p0aPerp) - radius * radius;
                    // Chỉ quan tâm nếu điểm xuất phát đang ở NGOÀI bán kính trụ (C >= 0) —
                    // giống RaySphereEntryT, tránh nhầm trường hợp đã ở trong với tunneling.
                    if (C >= 0f)
                    {
                        float B = 2f * Vector3.Dot(p0aPerp, dPerp);
                        float disc = B * B - 4f * A * C;
                        if (disc >= 0f)
                        {
                            float sqrtDisc = Mathf.Sqrt(disc);
                            float t = (-B - sqrtDisc) / (2f * A);
                            if (t >= 0f && t <= 1f && t < bestT)
                            {
                                Vector3 pt = p0 + d * t;
                                float axisProj = Vector3.Dot(pt - a, axisN);
                                // Điểm chạm phải nằm trong đoạn thân trụ (giữa 2 chỏm cầu),
                                // phần ngoài 2 đầu đã được 2 sphere test ở trên xử lý.
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

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / Vector3.Dot(ab, ab);
            return Vector3.Lerp(a, b, Mathf.Clamp01(t));
        }

    }
}