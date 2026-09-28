using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  GIZMOS (chỉ biên dịch trong Editor)
        //  Chỉ còn ĐÚNG 3 loại gizmo, dùng chung 1 vòng lặp cho cả Edit lẫn Play
        //  mode (không còn nhánh DrawTransformGizmos() riêng cho Edit mode nữa):
        //    1) Node sphere — trắng khi CHƯA Play; khi Play: đỏ = node "đứng yên"
        //       (root của chain, không bị mô phỏng), xanh lá = node "bị di chuyển"
        //       (được simulate). Màu suy TRỰC TIẾP từ node.parent — field này chỉ
        //       được set 1 LẦN lúc InitializeTrees() build cây và KHÔNG đổi trong
        //       lúc mô phỏng, nên đây không phải màu "chuyển runtime", chỉ là đọc
        //       lại giá trị tĩnh đã cache sẵn từ topology của cây.
        //    2) Bán kính va chạm — wire sphere cam, y như cũ.
        //    3) Line parent -> node — y như cũ, màu vàng.
        //  Đã bỏ: wire sphere rest-pose (xanh lá cũ) và 2 mũi tên drag/inertia, để
        //  gộp còn đúng 3 loại theo yêu cầu.
        // ══════════════════════════════════════════════════════════
        private void OnDrawGizmos()
        {
            if (!showGizmos || _nodeCount == 0) return;

            bool isPlaying = Application.isPlaying;
            bool drawParticleRadius = enableColliders && particleRadius > 0f;

            // [FIX] Không tin _nodeCount một mình: OnDrawGizmos có thể chạy khi _nodes/_tf chưa khớp
            // _nodeCount (sau reload script, lúc cây đang dựng lại, hoặc bone bị xoá) → dùng độ dài thật
            // của mảng để không bao giờ IndexOutOfRange.
            int count = Mathf.Min(_nodeCount, _nodes.Length);
            if (!isPlaying) count = Mathf.Min(count, _tf.Length);

            for (int i = 0; i < count; i++)
            {
                if (!isPlaying && _tf[i] == null) continue; // bone đã bị xoá khỏi scene
                int parent = _nodes[i].parent;
                if (parent >= count || (!isPlaying && parent >= 0 && _tf[parent] == null)) parent = -1;

                // Lúc Edit (chưa Play): đọc thẳng _tf[i].position (Transform thật) để
                // gizmo LUÔN bám theo vị trí bone hiện tại trong Scene, kể cả khi user
                // kéo bone sau lần "Cache Bones" gần nhất.
                // Lúc Play: dùng _nodes[i].position — đã được
                // DynamicBoneManager.CopyGizmoSnapshot() cập nhật mỗi frame sau khi job
                // mô phỏng chạy xong — để thấy đúng kết quả mô phỏng.
                Vector3 pos = isPlaying ? (Vector3)_nodes[i].position : _tf[i].position;

                // 1) Node sphere
                Gizmos.color = !isPlaying ? Color.white : (parent < 0 ? Color.red : Color.green);
                Gizmos.DrawSphere(pos, nodeGizmoSize);

                // 2) Bán kính va chạm
                if (drawParticleRadius)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
                    Gizmos.DrawWireSphere(pos, particleRadius);
                }

                // 3) Line parent -> node
                if (parent >= 0)
                {
                    Vector3 parentPos = isPlaying ? (Vector3)_nodes[parent].position : _tf[parent].position;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(parentPos, pos);
                }
            }

            // 4) Cross Links (chỉ có khi bật Cross Links) — đường cyan nối các cặp node liên kết ngang,
            //    giúp kiểm tra thứ tự Root Bones có nối đúng chain kề nhau hay không.
            if (_links.Length > 0)
            {
                Gizmos.color = Color.cyan;
                for (int li = 0; li < _links.Length; li++)
                {
                    int a = _links[li].a, b = _links[li].b;
                    if (a < 0 || b < 0 || a >= count || b >= count) continue;
                    if (!isPlaying && (_tf[a] == null || _tf[b] == null)) continue;
                    Vector3 pa = isPlaying ? (Vector3)_nodes[a].position : _tf[a].position;
                    Vector3 pb = isPlaying ? (Vector3)_nodes[b].position : _tf[b].position;
                    Gizmos.DrawLine(pa, pb);
                }
            }
        }
#endif
    }
}