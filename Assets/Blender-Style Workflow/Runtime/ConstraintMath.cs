using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public static class ConstraintMath
    {
        #region Position Logic

        // Ghi chú: influence tổng đã được áp dụng MỘT LẦN ở tầng ngoài (UpdatePosition, sau khi blend
        // xong tất cả target), nên các hàm bên dưới không cần Lerp lại nữa (trước đây luôn gọi với
        // influence = 1f => Lerp(a, b, 1) == b, tốn 1 phép Lerp thừa mỗi target mỗi frame).
        // currentBPos/currentBRot cũng được truyền vào từ ngoài (đã cache sẵn) thay vì tự đọc lại
        // constraintObject.position/rotation bên trong — tránh gọi native property lặp lại N lần/frame.
        public static Vector3 ComputeWorldAbsolutePosition(
            Transform target, Transform constraintObject, Vector3 currentBPos,
            bool copyX, bool copyY, bool copyZ)
        {
            Vector3 targetPos = target.position;

            float x = copyX ? targetPos.x : currentBPos.x;
            float y = copyY ? targetPos.y : currentBPos.y;
            float z = copyZ ? targetPos.z : currentBPos.z;

            return new Vector3(x, y, z);
        }

        public static Vector3 ComputeLocalDeltaPosition(
            Transform target, Transform constraintObject, Vector3 targetInitialLocalPos,
            Vector3 currentAnimationLocalPos,
            bool copyX, bool copyY, bool copyZ)
        {
            Vector3 targetDelta = target.localPosition - targetInitialLocalPos;

            // targetDelta đang nằm trong hệ trục của CHA (target.parent), chưa phải hệ trục gizmo
            // cục bộ của chính target. Cần "gỡ" xoay cục bộ của target ra để lấy đúng lượng di chuyển
            // theo từng trục X/Y/Z riêng của target (đúng như gizmo Move > Local hiển thị),
            // rồi mới map lượng đó sang các trục tương ứng (right/up/forward) của constraintObject.
            Vector3 localAxisDelta = Quaternion.Inverse(target.localRotation) * targetDelta;

            Vector3 worldDeltaForB = Vector3.zero;
            if (copyX) worldDeltaForB += constraintObject.right * localAxisDelta.x;
            if (copyY) worldDeltaForB += constraintObject.up * localAxisDelta.y;
            if (copyZ) worldDeltaForB += constraintObject.forward * localAxisDelta.z;

            // Cache parent một lần — tránh 2 lần gọi native property
            Transform parent = constraintObject.parent;
            Vector3 localDeltaForB = parent != null
                ? parent.InverseTransformDirection(worldDeltaForB)
                : worldDeltaForB;

            // Khử Scale của Cha
            if (parent != null)
            {
                Vector3 parentScale = parent.localScale;
                localDeltaForB.x /= (parentScale.x != 0) ? parentScale.x : 1f;
                localDeltaForB.y /= (parentScale.y != 0) ? parentScale.y : 1f;
                localDeltaForB.z /= (parentScale.z != 0) ? parentScale.z : 1f;
            }

            // influence luôn = 1 ở nơi gọi (blend tổng đã xử lý bên ngoài) nên bỏ phép nhân thừa,
            // trả thẳng kết quả cộng dồn.
            return currentAnimationLocalPos + localDeltaForB;
        }

        public static Vector3 ComputeLocalAbsolutePosition(
            Transform target, Transform constraintObject, Vector3 currentBLocalPos, Vector3 currentBWorldPos,
            bool copyX, bool copyY, bool copyZ)
        {
            Vector3 targetLocalPos = target.localPosition;

            // Hiệu số thô này đang nằm trong hệ trục CHA của mỗi bên, chưa phải hệ trục gizmo cục bộ
            // của target. Cần "gỡ" xoay cục bộ của target ra trước khi map sang trục của constraintObject,
            // giống hệt cách xử lý trong ComputeLocalDeltaPosition.
            Vector3 rawOffset = new Vector3(
                targetLocalPos.x - currentBLocalPos.x,
                targetLocalPos.y - currentBLocalPos.y,
                targetLocalPos.z - currentBLocalPos.z);
            Vector3 localAxisOffset = Quaternion.Inverse(target.localRotation) * rawOffset;

            Vector3 worldTargetPos = currentBWorldPos;

            // Chỉ tính phép nhân vector cho các trục thực sự được copy — bỏ qua nếu delta = 0
            if (copyX) worldTargetPos += constraintObject.right * localAxisOffset.x;
            if (copyY) worldTargetPos += constraintObject.up * localAxisOffset.y;
            if (copyZ) worldTargetPos += constraintObject.forward * localAxisOffset.z;

            // Cache parent một lần
            Transform parent = constraintObject.parent;
            return parent != null
                ? parent.InverseTransformPoint(worldTargetPos)
                : worldTargetPos;
        }

        #endregion

        #region Rotation Logic

        public static Quaternion ComputeWorldAbsoluteRotation(
            Transform target, Transform constraintObject, Quaternion currentBRot,
            bool copyX, bool copyY, bool copyZ)
        {
            Vector3 targetEuler = target.rotation.eulerAngles;
            Vector3 currentBEuler = currentBRot.eulerAngles;

            float x = copyX ? targetEuler.x : currentBEuler.x;
            float y = copyY ? targetEuler.y : currentBEuler.y;
            float z = copyZ ? targetEuler.z : currentBEuler.z;

            return Quaternion.Euler(x, y, z);
        }

        public static Quaternion ComputeLocalDeltaRotation(
            Transform target, Transform constraintObject, Quaternion targetInitialLocalRot, Quaternion currentLocalRot,
            bool copyX, bool copyY, bool copyZ)
        {
            // 1. Tính delta rotation trong không gian LOCAL của chính vật thể A (Target)
            Quaternion targetDeltaLocal = Quaternion.Inverse(targetInitialLocalRot) * target.localRotation;
            Vector3 localEulerDelta = targetDeltaLocal.eulerAngles;

            // 2. Lọc các trục theo mong muốn
            float x = copyX ? Mathf.DeltaAngle(0, localEulerDelta.x) : 0f;
            float y = copyY ? Mathf.DeltaAngle(0, localEulerDelta.y) : 0f;
            float z = copyZ ? Mathf.DeltaAngle(0, localEulerDelta.z) : 0f;

            Quaternion filteredDelta = Quaternion.Euler(x, y, z);
            return currentLocalRot * filteredDelta;
        }

        public static Quaternion ComputeLocalAbsoluteRotation(
            Transform target, Transform constraintObject, Quaternion currentBLocalRot,
            bool copyX, bool copyY, bool copyZ)
        {
            Vector3 targetLocalEuler = target.localRotation.eulerAngles;
            Vector3 currentBLocalEuler = currentBLocalRot.eulerAngles;

            float x = copyX ? Mathf.DeltaAngle(0, targetLocalEuler.x) : Mathf.DeltaAngle(0, currentBLocalEuler.x);
            float y = copyY ? Mathf.DeltaAngle(0, targetLocalEuler.y) : Mathf.DeltaAngle(0, currentBLocalEuler.y);
            float z = copyZ ? Mathf.DeltaAngle(0, targetLocalEuler.z) : Mathf.DeltaAngle(0, currentBLocalEuler.z);

            return Quaternion.Euler(x, y, z);
        }

        #endregion
    }
}