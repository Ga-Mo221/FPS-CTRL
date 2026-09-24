using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
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
            _linkCount = 0;
            _links = new ClothLinkData[0];
            _clothEnforcementRoots = new int[0];

            if (!enableClothLinks || _rootIndices.Length < 2) return;

            // Gom node theo depth cho từng chain
            var chains = new List<List<List<int>>>(_rootIndices.Length);
            int maxDepth = 0;

            for (int r = 0; r < _rootIndices.Length; r++)
            {
                var levels = new List<List<int>>();
                GatherLevels(_rootIndices[r], 0, levels);
                chains.Add(levels);
                if (levels.Count > maxDepth) maxDepth = levels.Count;
            }

            int chainCount = chains.Count;
            // Vòng khép kín cần nối thêm cặp (cuối → đầu), và chỉ có nghĩa khi >= 3 chain
            int pairCount = (clothLinkClosedLoop && chainCount >= 3) ? chainCount : chainCount - 1;

            var links = new List<ClothLinkData>();

            // depth bắt đầu từ 1 vì depth 0 là root bone bị ghim
            for (int depth = 1; depth < maxDepth; depth++)
            {
                for (int i = 0; i < pairCount; i++)
                {
                    // Tìm chain kế tiếp (bên phải) còn node ở depth này.
                    // Chain ngắn hơn bị bỏ qua để hai bên của nó vẫn dính vào nhau,
                    // thay vì để mặt vải bị thủng một khoảng.
                    List<int> left = GetLevel(chains[i], depth);
                    if (left == null) continue;

                    List<int> right = null;
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
                        int na = left[k];
                        int nb = right[k];
                        if (na == nb) continue;

                        links.Add(new ClothLinkData
                        {
                            a = na,
                            b = nb,
                            restLength = Vector3.Distance(_nodes[na].position, _nodes[nb].position),
                            aFixed = _nodes[na].parent < 0,
                            bFixed = _nodes[nb].parent < 0
                        });
                    }
                }
            }

            _links = links.ToArray();
            _linkCount = _links.Length;

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
        /// CŨ của nó, giờ nó dịch chuyển → con phải cascade theo). Nếu node A bị touched và
        /// node B (con/cháu của A) cũng bị touched, chỉ cần bắt đầu từ A — nó tự cascade qua B.
        /// Nên tập kết quả chỉ giữ "tổ tiên touched cao nhất" của mỗi nhánh.
        ///
        /// [PERF] Do node được xếp DFS (cha trước con), cờ "có tổ tiên touched" lan từ cha xuống
        /// con chỉ với 1 lượt duyệt tuyến tính O(n), thay vì leo ngược cây cho từng node.
        /// </summary>
        private void ComputeClothEnforcementRoots()
        {
            if (_linkCount == 0) return;

            var touched = new bool[_nodeCount];
            for (int l = 0; l < _linkCount; l++)
            {
                touched[_links[l].a] = true;
                touched[_links[l].b] = true;
            }

            var ancestorTouched = new bool[_nodeCount];
            var roots = new List<int>();
            for (int i = 0; i < _nodeCount; i++)
            {
                int p = _nodes[i].parent;
                ancestorTouched[i] = p >= 0 && (touched[p] || ancestorTouched[p]);

                if (touched[i] && !ancestorTouched[i])
                    roots.Add(i);
            }

            _clothEnforcementRoots = roots.ToArray();
        }

        private static List<int> GetLevel(List<List<int>> levels, int depth)
        {
            if (depth < 0 || depth >= levels.Count) return null;
            return levels[depth].Count > 0 ? levels[depth] : null;
        }

        private void GatherLevels(int nodeIdx, int depth, List<List<int>> levels)
        {
            while (levels.Count <= depth)
                levels.Add(new List<int>());

            levels[depth].Add(nodeIdx);

            int end = _nodes[nodeIdx].subtreeEnd;
            for (int c = nodeIdx + 1; c < end; c = _nodes[c].subtreeEnd)
                GatherLevels(c, depth + 1, levels);
        }

        //  CLOTH LINKS — SOLVE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Giải liên kết ngang theo kiểu position-based: mỗi vòng lặp kéo hai đầu link về
        /// đúng khoảng cách gốc, rồi ép lại độ dài xương với parent để chuỗi không bị giãn.
        ///
        /// [FIX-TUG] Trước đây toàn bộ clothLinkIterations chạy xong HẾT rồi collision mới được
        /// giải lại đúng 1 lần ở ResolveCollisionsAfterClothLinks() (bước 2b, cuối frame). Nếu
        /// clothLinkStiffness cao và đang đè lên 1 collider, MỖI vòng lặp ở đây có thể kéo node
        /// lún sâu THÊM vào collider mà không ai kiểm tra — tới lúc bước 2b chạy, node đã lún rất
        /// sâu, và sweep-test 1 đoạn thẳng duy nhất (từ vị trí TRƯỚC cả vòng lặp tới vị trí SAU
        /// CÙNG) không phản ánh đúng đường đi thật (ngoằn ngoèo qua 2-3 vòng). Hệ quả quan sát
        /// được trong collision log: stage AfterClothLinks phải giải một khối lượng correction
        /// lớn, và cùng 1 node có thể bị lún-đẩy lặp lại nhiều frame liên tiếp ("giằng co" giữa
        /// cloth link và collider).
        ///
        /// Fix: sau MỖI vòng lặp (không chỉ ở cuối), chặn ngay việc lún sâu thêm bằng 1 lần giải
        /// va chạm static (không sweep — sweep chính xác đã có ở bước 2b cuối frame) trên đúng
        /// tập node có thể đã bị dịch chuyển, tái dùng _clothEnforcementRoots hiện có nên chi phí
        /// thêm chỉ tỉ lệ với số node "touched", không phải toàn bộ cây.
        /// </summary>
        private void SolveClothLinks(float dt)
        {
            if (!enableClothLinks || _linkCount == 0) return;

            float stiffness = Mathf.Clamp01(clothLinkStiffness);
            if (stiffness <= 0f) return;

            int iterations = Mathf.Max(1, clothLinkIterations);
            NodeData[] nodes = _nodes;
            ClothLinkData[] links = _links;
            int[] enforceRoots = _clothEnforcementRoots;
            bool doCollide = _colCount > 0;

            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < links.Length; i++)
                {
                    bool aFixed = links[i].aFixed;
                    bool bFixed = links[i].bFixed;
                    if (aFixed && bFixed) continue;

                    int ia = links[i].a;
                    int ib = links[i].b;

                    Vector3 delta = nodes[ib].position - nodes[ia].position;
                    float dist = delta.magnitude;
                    if (dist < 0.000001f) continue;

                    float error = (dist - links[i].restLength) / dist;
                    Vector3 correction = delta * (error * stiffness);

                    if (aFixed)
                    {
                        nodes[ib].position -= correction;
                    }
                    else if (bFixed)
                    {
                        nodes[ia].position += correction;
                    }
                    else
                    {
                        // Chia đôi cho hai bên
                        nodes[ia].position += correction * 0.5f;
                        nodes[ib].position -= correction * 0.5f;
                    }
                }

                // Liên kết ngang vừa làm lệch khoảng cách tới parent → ép lại.
                // [1.3][A3] Chỉ enforce từ _clothEnforcementRoots (xem ComputeClothEnforcementRoots()).
                for (int r = 0; r < enforceRoots.Length; r++)
                    EnforceBoneLengthSubtree(enforceRoots[r]);

                // [FIX-TUG] Chặn lún sâu thêm qua từng vòng lặp — xem giải thích ở đầu hàm.
                if (doCollide)
                    ResolveCollisionsForEnforcementRoots(dt);
            }
        }

        /// <summary>
        /// [FIX-TUG] Giải va chạm static (không sweep) cho đúng tập node có thể vừa bị dịch
        /// chuyển bởi 1 vòng lặp cloth-link (subtree của _clothEnforcementRoots). Chạy xen kẽ
        /// GIỮA các vòng lặp trong SolveClothLinks(), khác với ResolveCollisionsAfterClothLinks()
        /// (bước 2b) chỉ chạy đúng 1 lần SAU KHI toàn bộ SolveClothLinks() đã xong với sweep-test
        /// đầy đủ dựa trên _preClothPositions.
        /// </summary>
        private void ResolveCollisionsForEnforcementRoots(float dt)
        {
            NodeData[] nodes = _nodes;
            int[] roots = _clothEnforcementRoots;
            float falloff = collisionParentFalloff;

            for (int r = 0; r < roots.Length; r++)
            {
                int root = roots[r];
                int end = nodes[root].subtreeEnd;

                for (int i = root; i < end; i++)
                {
                    int p = nodes[i].parent;
                    if (p < 0) continue;

                    Vector3 before = nodes[i].position;
                    Vector3 pos = before;

                    // useSweep = false: đây chỉ là bước chặn tạm giữa các vòng lặp, không phải
                    // bước resolve chính thức cuối frame — sweep chính xác dành cho bước 2b.
                    if (!SolveCollisions(ref pos, before, false, out int hit)) continue;

                    nodes[i].position = pos;
                    Vector3 correction = pos - before;
                    if (correction.sqrMagnitude <= 0f) continue;

                    // [FIX-JITTER] Cùng lý do như SimulatePhysics()/ResolveCollisionsAfterClothLinks():
                    // correction là ràng buộc vị trí, phải dịch prevPosition theo để không tạo vận
                    // tốc ảo ở frame sau.
                    nodes[i].prevPosition += correction;

                    if (enableCollisionLogging)
                        RecordCollision(i, hit, correction, dt, CollisionLogStage.DuringClothLinks,
                            CollisionLogKind.Direct);

                    PropagateCollisionToParents(p, correction, falloff, dt, CollisionLogStage.DuringClothLinks);
                }
            }
        }

        /// <summary>
        /// Ép mỗi node trong subtree về đúng bán kính so với parent (parent giữ nguyên, chỉ node
        /// con dịch). Subtree là đoạn liên tục [root, subtreeEnd) theo DFS pre-order nên chỉ cần
        /// 1 vòng for — cha luôn được cập nhật trước con, đúng như đệ quy cũ.
        /// </summary>
        private void EnforceBoneLengthSubtree(int root)
        {
            NodeData[] nodes = _nodes;
            int end = nodes[root].subtreeEnd;

            for (int i = root; i < end; i++)
            {
                int p = nodes[i].parent;
                float length = nodes[i].length;
                if (p < 0 || length <= 0f) continue;

                Vector3 pp = nodes[p].position;
                Vector3 dir = nodes[i].position - pp;
                if (dir.sqrMagnitude > 0.000001f)
                    nodes[i].position = pp + dir.normalized * length;
            }
        }

    }
}