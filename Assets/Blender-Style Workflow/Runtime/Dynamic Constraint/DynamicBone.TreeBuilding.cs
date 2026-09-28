using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        /// <summary>
        /// Xây lại toàn bộ cây xương LOCAL của component này (BoneNode[] Burst-safe) và danh
        /// sách collider đã lọc null. KHÔNG tự chạy mô phỏng — dữ liệu này sẽ được
        /// DynamicBoneManager copy vào bộ NativeArray toàn cục ở lần rebuild kế tiếp.
        /// Gọi hàm này bất cứ khi nào thay đổi rootBones/cấu trúc cây lúc runtime, sau đó gọi
        /// DynamicBoneManager.Instance.MarkDirty() (InitializeTrees() tự làm việc này nếu component
        /// đã đăng ký) để manager biết cần rebuild lại buffer toàn cục.
        /// </summary>
        internal bool _rebuildingFromManager = false;

        public void InitializeTrees()
        {
            // Build HashSet 1 lần/lần rebuild thay vì Contains() trên List cho từng Transform trong
            // hierarchy (List.Contains là O(n), với hierarchy vài trăm bone sẽ tốn dần theo O(n²)).
            HashSet<Transform> excludedSet = excludedBones.Count > 0
                ? new HashSet<Transform>(excludedBones)
                : null;

            var nodes = new List<BoneNode>(64);
            var tfs = new List<Transform>(64);
            var roots = new List<int>(rootBones.Count);

            for (int r = 0; r < rootBones.Count; r++)
            {
                Transform root = rootBones[r];
                if (root == null) continue;
                // Phòng trường hợp người dùng lỡ loại trừ đúng root bone — bỏ qua cả chain đó
                // thay vì build 1 chain rỗng/lỗi.
                if (excludedSet != null && excludedSet.Contains(root)) continue;
                int chainStart = nodes.Count;
                roots.Add(chainStart);
                BuildBoneTree(root, -1, 0, nodes, tfs, excludedSet);

                // Tính maxDepth của chain vừa build rồi copy xuống mọi node trong chain.
                // maxDepth cần biết TOÀN BỘ subtree nên phải làm sau khi BuildBoneTree() xong.
                int chainEnd = nodes.Count;
                int maxD = 0;
                for (int ni = chainStart; ni < chainEnd; ni++)
                    if (nodes[ni].depth > maxD) maxD = nodes[ni].depth;
                for (int ni = chainStart; ni < chainEnd; ni++)
                {
                    BoneNode n = nodes[ni];
                    n.maxDepth = maxD;
                    nodes[ni] = n;
                }
            }

            _nodeCount = nodes.Count;
            _nodes = nodes.ToArray();
            _tf = tfs.ToArray();
            _rootIndices = roots.ToArray();
            _rootParentTf = new Transform[_nodeCount];
            for (int r = 0; r < _rootIndices.Length; r++)
                _rootParentTf[_rootIndices[r]] = _tf[_rootIndices[r]].parent;

            var valid = new List<DynamicBoneCollider>(colliders.Count);
            for (int i = 0; i < colliders.Count; i++)
                if (colliders[i] != null)
                    valid.Add(colliders[i]);
            _validColliders = valid.ToArray();

            BuildChainLinks();

            if (!_rebuildingFromManager && Application.isPlaying && DynamicBoneManagerExistsAndHasThis())
                DynamicBoneManager.Instance.MarkDirty();
        }

        /// <summary>
        /// Dựng danh sách Cross Links từ các chain KỀ NHAU theo thứ tự rootBones (đã bỏ qua root null/bị
        /// loại trừ). Với mỗi cặp chain kề nhau, node cùng độ sâu (depth ≥ 1; root bị ghim nên bỏ) được nối:
        /// node thứ k ở depth d của chain X nối node thứ k ở depth d của chain Y (theo thứ tự duyệt cây —
        /// với chain thẳng mỗi depth chỉ có 1 node). Chain dài hơn thì phần thừa không có cặp → không nối.
        ///   A1-B1-C1 / A2-B2-C2 / A3-B3 / B4  →  (A1B1)(B1C1)(A2B2)(B2C2)(A3B3), B4 để tự do.
        /// restLength = khoảng cách 2 node ở bind pose, đo bằng world position lúc build (cùng cách node.length).
        /// </summary>
        private void BuildChainLinks()
        {
            int chainCount = _rootIndices.Length;
            if (chainLinkMode == ChainLinkMode.Off || chainCount < 2)
            {
                _links = new NodeLink[0];
                return;
            }

            // Danh sách node theo độ sâu cho từng chain (thứ tự duyệt DFS = thứ tự index).
            var byDepth = new List<int>[chainCount][];
            for (int c = 0; c < chainCount; c++)
            {
                int start = _rootIndices[c];
                int end = _nodes[start].subtreeEnd;
                int maxD = _nodes[start].maxDepth;
                var lists = new List<int>[maxD + 1];
                for (int i = start; i < end; i++)
                {
                    int d = _nodes[i].depth;
                    if (lists[d] == null) lists[d] = new List<int>(1);
                    lists[d].Add(i);
                }
                byDepth[c] = lists;
            }

            var links = new List<NodeLink>(64);
            for (int c = 0; c < chainCount - 1; c++)
                AddLinksBetweenChains(byDepth[c], byDepth[c + 1], links);

            // Loop: nối chain cuối về chain đầu. Với đúng 2 chain thì trùng Adjacent → bỏ để khỏi nối đôi.
            if (chainLinkMode == ChainLinkMode.Loop && chainCount > 2)
                AddLinksBetweenChains(byDepth[chainCount - 1], byDepth[0], links);

            _links = links.ToArray();
        }

        private void AddLinksBetweenChains(List<int>[] x, List<int>[] y, List<NodeLink> outLinks)
        {
            int maxShared = Mathf.Min(x.Length, y.Length);
            for (int d = 1; d < maxShared; d++) // d = 0 là root (bị ghim) → không nối
            {
                List<int> lx = x[d], ly = y[d];
                if (lx == null || ly == null) continue;
                int n = Mathf.Min(lx.Count, ly.Count);
                for (int k = 0; k < n; k++)
                {
                    int a = lx[k], b = ly[k];
                    outLinks.Add(new NodeLink
                    {
                        a = a,
                        b = b,
                        restLength = Vector3.Distance(_nodes[a].position, _nodes[b].position)
                    });
                }
            }
        }

        private bool DynamicBoneManagerExistsAndHasThis()
        {
            // Tránh tạo Manager chỉ để đánh dấu dirty khi component chưa từng đăng ký
            // (ví dụ gọi InitializeTrees() thủ công trước khi OnEnable chạy).
            return isActiveAndEnabled;
        }

        /// <summary>
        /// Cập nhật restGravityLocal theo hướng gravity hiện tại mà KHÔNG rebuild cây.
        /// Rẻ hơn InitializeTrees(). Sau khi gọi, nhớ báo manager rebuild nhẹ nếu cần đồng bộ
        /// ngay (thường không cần — BoneParams.gravity đã được refresh mỗi frame, chỉ hướng
        /// dùng cho rest-pose-gravity mới cần hàm này).
        /// </summary>
        public void RefreshRestGravity()
        {
            Vector3 gDir = gravity.normalized;
            for (int i = 0; i < _nodeCount; i++)
            {
                int p = _nodes[i].parent;
                if (p >= 0)
                {
                    BoneNode n = _nodes[i];
                    // Pre-bake với localRotation của parent, nhất quán với BuildBoneTree.
                    quaternion parentLocalRot = _nodes[p].localRotation;
                    float3 gravDirLocal = _tf[p].InverseTransformDirection(gDir);
                    n.restGravityLocal = math.mul(parentLocalRot, gravDirLocal);
                    _nodes[i] = n;
                }
            }

            if (!_rebuildingFromManager && Application.isPlaying && isActiveAndEnabled)
                DynamicBoneManager.Instance.MarkDirty();
        }

        private void BuildBoneTree(Transform t, int parentIdx, int depth, List<BoneNode> nodes, List<Transform> tfs,
            HashSet<Transform> excludedSet)
        {
            Vector3 worldPos = t.position;

            var node = new BoneNode
            {
                position = worldPos,
                prevPosition = worldPos,
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                parent = parentIdx,
                depth = depth,
                // maxDepth chưa biết lúc này (subtree chưa build xong) — sẽ được
                // ghi đè bởi vòng lặp trong InitializeTrees() sau khi chain hoàn tất.
                maxDepth = 0
            };

            if (parentIdx >= 0)
            {
                Transform pt = tfs[parentIdx];
                Vector3 parentPos = pt.position;
                Vector3 worldDir = worldPos - parentPos;

                node.length = Vector3.Distance(worldPos, parentPos);

                // Cả restGravityLocal lẫn restDirBase đều được pre-bake với localRotation của parent
                // để trong job: gpRot (grandparent world rot) × prebakedValue = parentWorldRot × localValue
                // tức: gpRot × (parentLocalRot × localDir) = parentWorldRot × localDir = world-space đúng.
                quaternion parentLocalRot = (quaternion)nodes[parentIdx].localRotation;
                Vector3 gravDirLocal = pt.InverseTransformDirection(gravity.normalized);
                node.restGravityLocal = math.mul(parentLocalRot, (float3)gravDirLocal);

                Vector3 restDirectionLocal = pt.InverseTransformDirection(worldDir.normalized);
                node.restDirBase = math.mul(parentLocalRot, (float3)restDirectionLocal);
            }

            int idx = nodes.Count;
            nodes.Add(node);
            tfs.Add(t);

            int childCount = t.childCount;
            for (int c = 0; c < childCount; c++)
            {
                Transform child = t.GetChild(c);
                // Bỏ qua GameObject phụ (phụ kiện, decal...) mà người dùng đã loại trừ thủ công
                // qua nút "Cache Bones" — KHÔNG đệ quy vào child này nên toàn bộ subtree bên dưới
                // (nếu có) cũng tự động bị loại theo, không cần liệt kê từng con cháu.
                if (excludedSet != null && excludedSet.Contains(child)) continue;
                BuildBoneTree(child, idx, depth + 1, nodes, tfs, excludedSet);
            }

            BoneNode self = nodes[idx];
            self.subtreeEnd = nodes.Count;
            nodes[idx] = self;
        }
    }
}