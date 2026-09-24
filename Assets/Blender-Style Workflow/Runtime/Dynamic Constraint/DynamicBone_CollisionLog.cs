using System.IO;
using System.Text;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        // ══════════════════════════════════════════════════════════
        //  COLLISION LOGGING (debug / tuning)
        // ══════════════════════════════════════════════════════════
        //
        // Ghi lại mỗi lần va chạm đẩy 1 node ra khỏi collider — cả va chạm TRỰC TIẾP (node
        // đó tự chạm collider) lẫn va chạm TRUYỀN LÊN (correction được lan lên parent/grandparent
        // qua PropagateCollisionToParents). Dùng để debug "vì sao xương bị giật ở đây",
        // "collider nào đang đẩy quá mạnh", hoặc để tinh chỉnh collisionParentFalloff /
        // particleRadius bằng số liệu thay vì đoán.
        //
        // [QUAN TRỌNG] Hệ mô phỏng này là Position-Based Dynamics (Verlet) — không có khối
        // lượng hay lực tường minh cho từng node. Va chạm được giải bằng cách "kẹp" position ra
        // ngoài bề mặt collider (constraint), không phải bằng cách tích phân một lực thật.
        // "estimatedForce" dưới đây chỉ là |correction| / dt² (coi như gia tốc, giả định mass = 1
        // cho MỌI node) — dùng để SO SÁNH TƯƠNG ĐỐI độ mạnh giữa các va chạm/collider/frame với
        // nhau, KHÔNG phải giá trị lực chính xác theo đơn vị Newton.

        [Header("Collision Logging (Debug)")]
        [Tooltip("Bật ghi log lực va chạm/đẩy giữa xương và collider để debug/tuning. Tốn thêm " +
                 "một chút CPU + RAM cho ring buffer trong lúc Play — nên tắt khi build release.")]
        public bool enableCollisionLogging = false;

        [Tooltip("Số dòng log tối đa giữ trong bộ nhớ (ring buffer). Log cũ nhất tự bị ghi đè khi đầy.")]
        public int collisionLogCapacity = 4000;

        [Tooltip("Tên file CSV khi bấm Export — lưu trong Application.persistentDataPath.")]
        public string collisionLogFileName = "DynamicBoneCollisionLog.csv";

        public enum CollisionLogStage : byte
        {
            Main = 0,             // va chạm giải trong SimulatePhysics (bước 1 mỗi frame)
            AfterClothLinks = 1,  // va chạm giải lại SAU KHI toàn bộ SolveClothLinks() đã xong (bước 2b, có sweep)

            // [FIX-TUG] va chạm giải XEN KẼ giữa từng vòng lặp BÊN TRONG SolveClothLinks(), để
            // chặn node lún sâu thêm vào collider qua nhiều vòng lặp trước khi bước 2b chạy.
            // Xem ResolveCollisionsForEnforcementRoots() trong DynamicBone_ClothLinks.cs.
            DuringClothLinks = 2
        }

        public enum CollisionLogKind : byte
        {
            Direct = 0,     // node tự va chạm trực tiếp với 1 collider
            Propagated = 1  // node bị dịch chuyển do nhận correction TRUYỀN LÊN từ con
        }

        public struct CollisionLogEntry
        {
            public int frame;
            public float time;
            public int nodeIndex;
            public int colliderSlot;       // index trong `colliders` (Inspector); -1 nếu là Propagated hoặc không xác định
            public CollisionLogStage stage;
            public CollisionLogKind kind;
            public Vector3 correction;     // độ dịch chuyển vị trí do va chạm gây ra (world space, mét)
            public float correctionMagnitude;
            public float estimatedForce;   // |correction| / dt² — xem ghi chú ở đầu file
        }

        private CollisionLogEntry[] _collisionLog = new CollisionLogEntry[0];
        private int _collisionLogHead;  // vị trí sẽ ghi dòng KẾ TIẾP
        private int _collisionLogCount; // số dòng đang có (<= capacity)
        private int _frameCounter;

        private void EnsureCollisionLogCapacity()
        {
            int cap = Mathf.Max(1, collisionLogCapacity);
            if (_collisionLog.Length == cap) return;

            _collisionLog = new CollisionLogEntry[cap];
            _collisionLogHead = 0;
            _collisionLogCount = 0;
        }

        /// <summary>
        /// Ghi 1 dòng log va chạm vào ring buffer. Không làm gì nếu enableCollisionLogging = false
        /// (chi phí gọi hàm này khi tắt chỉ là 1 lần kiểm tra bool).
        /// </summary>
        private void RecordCollision(int nodeIndex, int colliderSlot, Vector3 correction, float dt,
            CollisionLogStage stage, CollisionLogKind kind)
        {
            if (!enableCollisionLogging) return;
            if (_collisionLog.Length != Mathf.Max(1, collisionLogCapacity)) EnsureCollisionLogCapacity();

            float mag = correction.magnitude;
            float dt2 = dt * dt;
            float estForce = dt2 > 1e-12f ? mag / dt2 : 0f;

            _collisionLog[_collisionLogHead] = new CollisionLogEntry
            {
                frame = _frameCounter,
                time = Time.time,
                nodeIndex = nodeIndex,
                colliderSlot = colliderSlot,
                stage = stage,
                kind = kind,
                correction = correction,
                correctionMagnitude = mag,
                estimatedForce = estForce
            };

            _collisionLogHead++;
            if (_collisionLogHead >= _collisionLog.Length) _collisionLogHead = 0;
            if (_collisionLogCount < _collisionLog.Length) _collisionLogCount++;
        }

        /// <summary>Xoá sạch log hiện có trong RAM (không đụng tới file CSV đã export trước đó).</summary>
        public void ClearCollisionLog()
        {
            _collisionLogHead = 0;
            _collisionLogCount = 0;
        }

        /// <summary>
        /// Xuất toàn bộ log hiện có ra Application.persistentDataPath/collisionLogFileName.
        /// Gọi từ context menu component trong Editor, hoặc gọi thẳng bằng code lúc runtime
        /// (ví dụ gán vào 1 phím tắt debug) — không giới hạn chỉ trong Editor.
        /// </summary>
        [ContextMenu("Export Collision Log to CSV")]
        public void ExportCollisionLogToCsv()
        {
            ExportCollisionLogToCsv(Path.Combine(Application.persistentDataPath, collisionLogFileName));
        }

        public void ExportCollisionLogToCsv(string path)
        {
            if (_collisionLogCount == 0)
            {
                Debug.LogWarning("[DynamicBone] Collision log rỗng — không có gì để export. " +
                                  "Kiểm tra enableCollisionLogging đã bật và đã Play ít nhất vài frame có va chạm.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("frame,time,nodeIndex,nodeName,colliderSlot,colliderName,stage,kind," +
                          "correctionX,correctionY,correctionZ,correctionMagnitude,estimatedForce");

            // Đọc ring buffer theo đúng thứ tự thời gian (cũ → mới).
            int start = _collisionLogCount < _collisionLog.Length ? 0 : _collisionLogHead;
            for (int k = 0; k < _collisionLogCount; k++)
            {
                int idx = (start + k) % _collisionLog.Length;
                CollisionLogEntry e = _collisionLog[idx];

                string nodeName = (e.nodeIndex >= 0 && _tf != null && e.nodeIndex < _tf.Length && _tf[e.nodeIndex] != null)
                    ? _tf[e.nodeIndex].name
                    : "?";

                string colliderName = string.Empty;
                if (e.colliderSlot >= 0 && e.colliderSlot < colliders.Count && colliders[e.colliderSlot] != null)
                    colliderName = colliders[e.colliderSlot].name;

                sb.Append(e.frame).Append(',')
                  .Append(e.time.ToString("F4")).Append(',')
                  .Append(e.nodeIndex).Append(',')
                  .Append(EscapeCsv(nodeName)).Append(',')
                  .Append(e.colliderSlot).Append(',')
                  .Append(EscapeCsv(colliderName)).Append(',')
                  .Append(e.stage).Append(',')
                  .Append(e.kind).Append(',')
                  .Append(e.correction.x.ToString("F6")).Append(',')
                  .Append(e.correction.y.ToString("F6")).Append(',')
                  .Append(e.correction.z.ToString("F6")).Append(',')
                  .Append(e.correctionMagnitude.ToString("F6")).Append(',')
                  .Append(e.estimatedForce.ToString("F6"))
                  .AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[DynamicBone] Đã export {_collisionLogCount} dòng collision log ra: {path}");
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}