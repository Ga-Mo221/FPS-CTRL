using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public static class ConstraintMath
    {
        #region Position Logic

        // World Absolute Position được xử lý trực tiếp trong UpdatePosition (TransformConstraint.cs)
        // thay vì qua hàm riêng — tránh N lần call hàm mỗi frame và cho phép tối ưu trục bị khóa
        // mà không cần truyền thêm tham số. Các hàm còn lại phục vụ Local mode.

        public static Vector3 ComputeLocalDeltaPosition(
            Transform target, Transform constraintObject, Vector3 targetInitialLocalPos,
            Quaternion targetInitialWorldRot,
            Vector3 currentAnimationLocalPos,
            Vector3 basisRight, Vector3 basisUp, Vector3 basisForward,
            bool copyX, bool copyY, bool copyZ)
        {
            // Tính delta trong world space thay vì localPosition space:
            // localPosition bị nhân bởi scale của parent, dùng trực tiếp sẽ khiến delta
            // bị phóng to/thu nhỏ sai khi parent có scale != 1 → constraintObject bị trôi.
            Transform targetParent = target.parent;
            Vector3 initialWorldPos = targetParent != null
                ? targetParent.TransformPoint(targetInitialLocalPos)
                : targetInitialLocalPos;
            Vector3 targetDelta = target.position - initialWorldPos;

            // Chiếu targetDelta về hệ trục LOCAL BAN ĐẦU của target (targetInitialWorldRot,
            // snapshot tại thời điểm capture), rồi map sang các trục basis của constraintObject.
            // FIX: bản cũ dùng target.rotation HIỆN TẠI để phân tích delta → nếu target vừa xoay
            // vừa di chuyển, hệ trục phân tích cũng xoay theo, khiến delta position bị nhiễm bởi
            // rotation (thay đổi dù target không di chuyển thêm theo hướng đó).
            // Dùng rotation SNAPSHOT lúc capture cắt đứt sự phụ thuộc này: "Delta Position" giờ
            // đúng nghĩa "target đã di chuyển bao nhiêu theo hệ trục BAN ĐẦU của nó", độc lập với
            // việc target có xoay tiếp hay không — ổn định và dễ dự đoán hơn cho weapon attachment.
            Vector3 localAxisDelta = Quaternion.Inverse(targetInitialWorldRot) * targetDelta;

            // Dùng basis đã được snapshot từ đầu frame — KHÔNG đọc constraintObject.right/up/forward
            // trực tiếp ở đây. Nếu đọc live, Position sẽ phụ thuộc vào rotation HIỆN TẠI của
            // constraintObject, có thể đã bị Animator/IK thay đổi sau snapshot → order-dependency.
            Vector3 worldDeltaForB = Vector3.zero;
            if (copyX) worldDeltaForB += basisRight   * localAxisDelta.x;
            if (copyY) worldDeltaForB += basisUp       * localAxisDelta.y;
            if (copyZ) worldDeltaForB += basisForward  * localAxisDelta.z;

            // Chuyển worldDelta về local space của parent constraintObject.
            // Dùng InverseTransformVector thay vì InverseTransformDirection:
            //   • InverseTransformDirection: chỉ khử rotation, KHÔNG khử scale
            //     → code cũ phải chia scale thủ công sau đó → double-divide khi scale != 1
            //   • InverseTransformVector: khử cả rotation lẫn scale trong một bước, không cần chia thêm
            // Không dùng InverseTransformPoint vì worldDeltaForB là vector (không có translation).
            Transform parent = constraintObject.parent;
            Vector3 localDeltaForB = parent != null
                ? parent.InverseTransformVector(worldDeltaForB)
                : worldDeltaForB;

            return currentAnimationLocalPos + localDeltaForB;
        }

        public static Vector3 ComputeLocalAbsolutePosition(
            Transform target, Transform constraintObject, Vector3 currentBLocalPos, Vector3 currentBWorldPos,
            Vector3 basisRight, Vector3 basisUp, Vector3 basisForward,
            bool copyX, bool copyY, bool copyZ)
        {
            // Tính offset hoàn toàn trong world space (cùng hệ toạ độ), sau đó chiếu lên
            // từng trục local của constraintObject bằng dot product.
            // Dot product cho biết "target xa B bao nhiêu đơn vị theo trục right/up/forward của B".
            Vector3 worldOffset = target.position - currentBWorldPos;

            // Dùng basis đã snapshot — KHÔNG đọc constraintObject.right/up/forward trực tiếp.
            // Lý do giống ComputeLocalDeltaPosition: tránh order-dependency với UpdateRotation.
            Vector3 finalWorldPos = currentBWorldPos;
            if (copyX) finalWorldPos += basisRight   * Vector3.Dot(worldOffset, basisRight);
            if (copyY) finalWorldPos += basisUp       * Vector3.Dot(worldOffset, basisUp);
            if (copyZ) finalWorldPos += basisForward  * Vector3.Dot(worldOffset, basisForward);

            Transform parent = constraintObject.parent;
            return parent != null
                ? parent.InverseTransformPoint(finalWorldPos)
                : finalWorldPos;
        }

        #endregion

        #region Rotation Logic

        /// <summary>
        /// Copy rotation từ target sang constraintObject trong world space, lọc theo trục được chọn.
        ///
        /// ⚠ SEMANTIC QUAN TRỌNG — WORLD AXIS, KHÔNG PHẢI LOCAL AXIS:
        /// copyX/Y/Z ở đây có nghĩa là "copy twist quanh trục world X/Y/Z" (Vector3.right/up/forward),
        /// KHÔNG phải trục local của target hay constraintObject.
        ///
        /// Ví dụ: nếu target đang xoay 45° quanh local Y của nó nhưng local Y đó lệch so với world Y,
        /// thì "copyY = true" sẽ copy phần twist quanh world Y, không phải toàn bộ rotation đó.
        ///
        /// Nếu semantic mong muốn là "copy đúng local axis của target", cần refactor sang
        /// decompose dọc theo target.right/up/forward thay vì Vector3.right/up/forward.
        /// Trong thực tế FPS với weapon luôn align gần với world axis, sự khác biệt này thường nhỏ.
        /// </summary>
        public static Quaternion ComputeWorldAbsoluteRotation(
            Transform target, Transform constraintObject, Quaternion currentBRot,
            bool copyX, bool copyY, bool copyZ)
        {
            if (copyX && copyY && copyZ) return target.rotation;

            Quaternion targetRot = target.rotation;
            Quaternion result = currentBRot;

            // Swing-twist decomposition dọc theo trục world X/Y/Z.
            // Với mỗi trục được copy: lấy twist của target quanh trục đó,
            // bỏ twist của result trên cùng trục, ghép twist của target vào.
            // Thứ tự X → Y → Z: mỗi trục độc lập, không ảnh hưởng nhau.
            if (copyX)
            {
                Quaternion twistTarget  = GetTwist(targetRot, Vector3.right);
                Quaternion twistCurrent = GetTwist(result, Vector3.right);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyY)
            {
                Quaternion twistTarget  = GetTwist(targetRot, Vector3.up);
                Quaternion twistCurrent = GetTwist(result, Vector3.up);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyZ)
            {
                Quaternion twistTarget  = GetTwist(targetRot, Vector3.forward);
                Quaternion twistCurrent = GetTwist(result, Vector3.forward);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            return result;
        }

        // Tách phần xoay (twist) của quaternion q dọc theo twistAxis trong world space.
        // Phương pháp: chiếu phần vector (xyz) của q lên trục, normalize lại.
        // Trả về identity khi proj ≈ 0 (q gần identity quanh trục này) để tránh NaN.
        private static Quaternion GetTwist(Quaternion q, Vector3 twistAxis)
        {
            Vector3 qVec = new Vector3(q.x, q.y, q.z);
            Vector3 proj = Vector3.Project(qVec, twistAxis);
            Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, q.w);
            // Nếu gần identity (proj ≈ 0), trả về identity để tránh NaN khi normalize
            float sqrMag = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
            if (sqrMag < 1e-10f) return Quaternion.identity;
            float inv = 1f / Mathf.Sqrt(sqrMag);
            return new Quaternion(twist.x * inv, twist.y * inv, twist.z * inv, twist.w * inv);
        }

        public static Quaternion ComputeLocalDeltaRotation(
            Transform target, Transform constraintObject, Quaternion targetInitialLocalRot, Quaternion currentLocalRot,
            bool copyX, bool copyY, bool copyZ)
        {
            // Tính delta rotation trong local space của target.
            // BUG CŨ: convert sang Euler để lọc từng trục, rồi Euler → Quaternion lại.
            // Euler không ổn định — cùng hướng có thể có nhiều biểu diễn Euler khác nhau
            // (gimbal lock, flip 0°/360°) → delta bị sai khi rotation gần các vùng nhạy cảm.
            //
            // FIX: dùng swing-twist decomposition dọc theo từng trục local.
            // Với mỗi trục được copy: lấy twist của delta quanh trục đó, bỏ twist của currentLocalRot
            // trên cùng trục, ghép twist của delta vào. Kỹ thuật giống ComputeWorldAbsoluteRotation.
            Quaternion targetDeltaLocal = Quaternion.Inverse(targetInitialLocalRot) * target.localRotation;

            if (copyX && copyY && copyZ)
                return currentLocalRot * targetDeltaLocal;

            Quaternion result = currentLocalRot;

            if (copyX)
            {
                Quaternion twistDelta   = GetTwist(targetDeltaLocal, Vector3.right);
                Quaternion twistCurrent = GetTwist(result, Vector3.right);
                result = twistDelta * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyY)
            {
                Quaternion twistDelta   = GetTwist(targetDeltaLocal, Vector3.up);
                Quaternion twistCurrent = GetTwist(result, Vector3.up);
                result = twistDelta * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyZ)
            {
                Quaternion twistDelta   = GetTwist(targetDeltaLocal, Vector3.forward);
                Quaternion twistCurrent = GetTwist(result, Vector3.forward);
                result = twistDelta * Quaternion.Inverse(twistCurrent) * result;
            }

            return result;
        }

        public static Quaternion ComputeLocalAbsoluteRotation(
            Transform target, Transform constraintObject, Quaternion currentBLocalRot,
            bool copyX, bool copyY, bool copyZ)
        {
            // BUG CŨ: lấy eulerAngles của cả target lẫn currentBLocalRot, trộn theo trục, rồi
            // Quaternion.Euler() lại — không ổn định do Euler ambiguity (gimbal lock, flip).
            //
            // FIX: dùng swing-twist decomposition giống ComputeWorldAbsoluteRotation.
            // Với mỗi trục được copy: lấy twist của target.localRotation quanh trục đó,
            // bỏ twist tương ứng trong currentBLocalRot, ghép twist của target vào.
            // Kết quả: các trục được copy mang đúng orientation local của target,
            // các trục không copy giữ nguyên orientation của constraintObject — không qua Euler.
            if (copyX && copyY && copyZ) return target.localRotation;

            Quaternion targetLocalRot = target.localRotation;
            Quaternion result = currentBLocalRot;

            if (copyX)
            {
                Quaternion twistTarget  = GetTwist(targetLocalRot, Vector3.right);
                Quaternion twistCurrent = GetTwist(result, Vector3.right);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyY)
            {
                Quaternion twistTarget  = GetTwist(targetLocalRot, Vector3.up);
                Quaternion twistCurrent = GetTwist(result, Vector3.up);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            if (copyZ)
            {
                Quaternion twistTarget  = GetTwist(targetLocalRot, Vector3.forward);
                Quaternion twistCurrent = GetTwist(result, Vector3.forward);
                result = twistTarget * Quaternion.Inverse(twistCurrent) * result;
            }

            return result;
        }

        #endregion
    }
}