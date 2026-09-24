using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        public void InitializeTrees()
        {
            _colStates = new ColliderState[0];
            _colCount = 0;

            var nodes = new List<NodeData>(64);
            var tfs = new List<Transform>(64);
            var roots = new List<int>(rootBones.Count);

            for (int r = 0; r < rootBones.Count; r++)
            {
                Transform root = rootBones[r];
                if (root == null) continue;
                roots.Add(nodes.Count);
                BuildBoneTree(root, -1, nodes, tfs);
            }

            _nodeCount = nodes.Count;
            _nodes = nodes.ToArray();
            _tf = tfs.ToArray();
            _rootIndices = roots.ToArray();
            _rot = new Quaternion[_nodeCount];
            _extRot = new Quaternion[_nodeCount];
            _preClothPositions = new Vector3[_nodeCount];
            _rootParentTf = new Transform[_nodeCount];
            for (int r = 0; r < _rootIndices.Length; r++)
                _rootParentTf[_rootIndices[r]] = _tf[_rootIndices[r]].parent;

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
            Vector3 gDir = gravity.normalized;
            for (int i = 0; i < _nodeCount; i++)
            {
                int p = _nodes[i].parent;
                if (p >= 0)
                    _nodes[i].restGravityLocal = _tf[p].InverseTransformDirection(gDir);
            }
        }

        private void BuildBoneTree(Transform t, int parentIdx, List<NodeData> nodes, List<Transform> tfs)
        {
            Vector3 worldPos = t.position;

            var node = new NodeData
            {
                position = worldPos,
                prevPosition = worldPos,
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                parent = parentIdx
            };

            if (parentIdx >= 0)
            {
                Transform pt = tfs[parentIdx];
                Vector3 parentPos = pt.position;
                Vector3 worldDir = worldPos - parentPos;

                node.length = Vector3.Distance(worldPos, parentPos);
                node.restGravityLocal = pt.InverseTransformDirection(gravity.normalized);
                Vector3 restDirectionLocal = pt.InverseTransformDirection(worldDir.normalized);
                node.restDirBase = nodes[parentIdx].localRotation * restDirectionLocal;
            }

            int idx = nodes.Count;
            nodes.Add(node);
            tfs.Add(t);

            int childCount = t.childCount;
            for (int c = 0; c < childCount; c++)
                BuildBoneTree(t.GetChild(c), idx, nodes, tfs);

            NodeData self = nodes[idx];
            self.subtreeEnd = nodes.Count;
            nodes[idx] = self;
        }

    }
}