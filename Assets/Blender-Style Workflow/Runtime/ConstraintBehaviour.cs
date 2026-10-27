using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Lớp cha chung cho mọi constraint (TransformConstraint, IKConstraint, DampedTrackConstraint,
    /// DampedTrackChainConstraint). Constraint KHÔNG còn tự chạy LateUpdate() riêng — mọi instance
    /// đăng ký vào <see cref="ConstraintScheduler"/> lúc OnEnable, và scheduler gọi <see cref="Solve"/>
    /// của tất cả trong ĐÚNG 1 LateUpdate theo thứ tự xác định:
    ///   1. Phụ thuộc (dependency): constraint đọc Transform mà constraint khác ghi (hoặc con cháu của
    ///      Transform đó) luôn chạy SAU constraint ghi → không còn trễ 1 frame.
    ///   2. Giữa các constraint không phụ thuộc nhau: <see cref="ExecutionOrder"/> (nhỏ chạy trước) →
    ///      <see cref="DefaultTypeOrder"/> (theo loại) → độ sâu hierarchy (cha trước con) → thứ tự đăng ký.
    ///
    /// Lớp con override OnEnable/OnDisable/OnValidate PHẢI gọi base.* để giữ việc đăng ký với scheduler.
    /// </summary>
    public abstract class ConstraintBehaviour : MonoBehaviour
    {
        // ── Thứ tự mặc định theo loại (khoảng cách 100 để chèn loại mới ở giữa) ─────────
        // Giữ đúng thứ tự cũ khi còn chạy LateUpdate riêng: DampedTrack (order 0) → TransformConstraint
        // (order 100) → IKConstraint (order 200) — IK chạy cuối để tay bám đúng pose cuối của thân/vũ khí.
        public const int TypeOrderDampedTrackChain = 100;
        public const int TypeOrderDampedTrack = 200;
        public const int TypeOrderTransform = 300;
        public const int TypeOrderIK = 400;

        [SerializeField]
        [Tooltip(
            "Thứ tự thực thi do bạn chỉ định (nhỏ chạy trước, mặc định 0).\n" +
            "• Phụ thuộc luôn được ưu tiên: constraint đọc Transform do constraint khác ghi sẽ luôn chạy sau nó, bất kể giá trị này.\n" +
            "• Giá trị này chỉ quyết định thứ tự giữa các constraint KHÔNG phụ thuộc nhau, hoặc dùng để phá vòng lặp phụ thuộc (cycle).\n" +
            "• Bằng nhau → xét tiếp thứ tự theo loại (DampedTrackChain → DampedTrack → Transform → IK), rồi độ sâu hierarchy (cha trước con).")]
        private int executionOrder = 0;

        // ── Trạng thái do ConstraintScheduler quản lý (không serialize) ─────────────────
        [System.NonSerialized] internal bool schedulerRegistered;
        [System.NonSerialized] internal int schedulerSerial;
        [System.NonSerialized] internal int resolvedIndex = -1;
        [System.NonSerialized] internal bool inDependencyCycle;

        /// <summary>
        /// Thứ tự do người dùng chỉ định (nhỏ chạy trước). Đổi giá trị lúc runtime sẽ tự đánh dấu
        /// scheduler sắp xếp lại ở frame kế tiếp.
        /// </summary>
        public int ExecutionOrder
        {
            get => executionOrder;
            set
            {
                if (executionOrder == value) return;
                executionOrder = value;
                ConstraintScheduler.MarkDirty();
            }
        }

        /// <summary>Thứ tự mặc định theo loại constraint (xem các hằng TypeOrder*).</summary>
        public abstract int DefaultTypeOrder { get; }

        /// <summary>
        /// true = scheduler BỎ QUA constraint này; một script khác tự gọi <see cref="Solve"/> đúng thời
        /// điểm mong muốn. Thường không cần nữa vì scheduler đã tự sắp theo phụ thuộc — giữ lại để tương
        /// thích code cũ (VD ConstraintManager tự Evaluate xương lựu đạn).
        /// </summary>
        public bool ExternallyDriven { get; set; }

        /// <summary>Vị trí thực tế trong danh sách thực thi của scheduler (-1 = chưa đăng ký / chưa sắp xếp).</summary>
        public int ResolvedExecutionIndex => resolvedIndex;

        /// <summary>true nếu lần sắp xếp gần nhất phát hiện constraint này nằm trong vòng lặp phụ thuộc.</summary>
        public bool IsInDependencyCycle => inDependencyCycle;

        /// <summary>true nếu constraint đang được scheduler quản lý.</summary>
        public bool IsRegisteredWithScheduler => schedulerRegistered;

        /// <summary>Tính và áp constraint cho frame hiện tại. Scheduler gọi hàm này mỗi LateUpdate.</summary>
        public abstract void Solve();

        /// <summary>
        /// Khai báo Transform mà constraint ĐỌC (inputs: target, pole, cha của xương bị ghi...) và GHI
        /// (outputs: constraintObject, các xương trong chain). Scheduler dùng để dựng đồ thị phụ thuộc:
        /// A đọc X, B ghi X hoặc tổ tiên của X → B chạy trước A. Chỉ gọi khi sắp xếp lại, không gọi mỗi frame.
        /// Được phép thêm null (scheduler tự bỏ qua).
        /// </summary>
        public abstract void CollectDependencies(List<Transform> inputs, List<Transform> outputs);

        /// <summary>
        /// Input chỉ đọc pose LOCAL (localRotation/localPosition) — world pose của cha không ảnh hưởng.
        /// Scheduler chỉ nối cạnh với constraint ghi ĐÚNG Transform đó, không đi ngược lên tổ tiên
        /// → tránh vòng lặp giả (VD Camera Shake copy localRotation xương RAW_Camera nằm dưới súng
        /// trong khi súng lại bám theo camera). Mặc định: không có.
        /// </summary>
        public virtual void CollectLocalDependencies(List<Transform> localInputs) { }

        /// <summary>
        /// Báo scheduler cấu trúc phụ thuộc đã đổi (thêm/xoá target, đổi bones, reparent...). Rẻ — chỉ bật
        /// cờ, việc sắp xếp lại chạy 1 lần ở LateUpdate kế tiếp.
        /// </summary>
        public static void MarkSchedulerDirty() => ConstraintScheduler.MarkDirty();

        protected virtual void OnEnable()
        {
            ConstraintScheduler.Register(this);
        }

        protected virtual void OnDisable()
        {
            ConstraintScheduler.Unregister(this);
        }

        protected virtual void OnValidate()
        {
            // Inspector đổi target/bones/order → sắp xếp lại (chỉ bật cờ, rẻ)
            ConstraintScheduler.MarkDirty();
        }

        protected virtual void OnTransformParentChanged()
        {
            ConstraintScheduler.MarkDirty();
        }
    }
}
