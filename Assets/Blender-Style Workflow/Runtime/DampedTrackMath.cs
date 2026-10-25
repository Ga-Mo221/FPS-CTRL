using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Toán học riêng cho DampedTrackConstraint. Tách khỏi ConstraintMath.cs để không đụng vào
    /// file gốc — có thể gộp vào ConstraintMath.cs (thêm 1 #region "Damped Track Logic") nếu muốn
    /// gom chung, logic không phụ thuộc gì vào nhau.
    /// </summary>
    public static class DampedTrackMath
    {
        /// <summary>
        /// Chuyển enum TrackAxis sang vector trục local tương ứng.
        /// </summary>
        public static Vector3 GetTrackAxisVector(DampedTrackConstraint.TrackAxis axis)
        {
            switch (axis)
            {
                case DampedTrackConstraint.TrackAxis.X: return Vector3.right;
                case DampedTrackConstraint.TrackAxis.Y: return Vector3.up;
                case DampedTrackConstraint.TrackAxis.Z: return Vector3.forward;
                case DampedTrackConstraint.TrackAxis.NegX: return Vector3.left;
                case DampedTrackConstraint.TrackAxis.NegY: return Vector3.down;
                case DampedTrackConstraint.TrackAxis.NegZ: return Vector3.back;
                default: return Vector3.forward;
            }
        }

        /// <summary>
        /// Tính góc yaw/pitch (độ) của hướng output so với baseline, theo hệ trục (fwd, up) cục bộ.
        /// yaw > 0 = sang phải, pitch > 0 = ngẩng lên. Hướng ra sau lưng → |yaw| > 90.
        /// </summary>
        public static void GetYawPitch(Quaternion baseWorldRot, Quaternion outWorldRot,
            Vector3 fwd, Vector3 up, out float yaw, out float pitch)
        {
            Vector3 right = Vector3.Cross(up, fwd);
            if (right.sqrMagnitude < 1e-6f) { yaw = 0f; pitch = 0f; return; } // up trùng fwd → không xác định
            right.Normalize();
            up = Vector3.Cross(fwd, right);

            Vector3 d = Quaternion.Inverse(baseWorldRot) * (outWorldRot * fwd);
            float f = Vector3.Dot(d, fwd);
            float r = Vector3.Dot(d, right);
            float u = Vector3.Dot(d, up);

            yaw = Mathf.Atan2(r, f) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(u, Mathf.Sqrt(f * f + r * r)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Tính rotation world sao cho localTrackAxis (sau khi áp baseWorldRot) hướng thẳng về
        /// targetWorldPos, bằng SWING ROTATION NHỎ NHẤT (Quaternion.FromToRotation) — đúng cách
        /// Blender "Damped Track" hoạt động, khác với LookRotation/"Track To":
        ///
        /// • LookRotation/Track To cần thêm Up Axis để xác định roll → bị giật/flip khi hướng nhìn
        ///   đi ngang qua gần trục Up (gimbal-like flip).
        /// • Damped Track KHÔNG cần Up Axis: chỉ xoay đúng lượng tối thiểu để đưa trục cũ (đọc từ
        ///   baseWorldRot — rotation hiện tại, liên tục qua từng frame) về hướng mới. Roll quanh
        ///   track axis vì vậy được BẢO TOÀN liên tục, không có bước nhảy góc.
        /// </summary>
        public static Quaternion ComputeDampedTrackRotation(
            Vector3 selfWorldPos, Vector3 targetWorldPos, Quaternion baseWorldRot, Vector3 localTrackAxis)
        {
            Vector3 dir = targetWorldPos - selfWorldPos;
            float sqrLen = dir.sqrMagnitude;

            // Target trùng vị trí self → không có hướng xác định, giữ nguyên baseline tránh NaN.
            if (sqrLen < 1e-10f) return baseWorldRot;

            dir /= Mathf.Sqrt(sqrLen);

            Vector3 currentAxisWorld = baseWorldRot * localTrackAxis;
            float dot = Vector3.Dot(currentAxisWorld, dir);

            Quaternion swing;
            if (dot < -0.9999f)
            {
                // Trường hợp suy biến: hướng target gần như ĐỐI NGHỊCH trục tracking hiện tại.
                // FromToRotation không xác định được trục xoay duy nhất ở đây (vô số trục vuông góc
                // hợp lệ) → tự chọn 1 trục vuông góc cố định để xoay 180°, tránh NaN/giật khi target
                // đi ngang qua đúng điểm đối cực của trục tracking.
                Vector3 fallbackAxis = Vector3.Cross(Vector3.up, currentAxisWorld);
                if (fallbackAxis.sqrMagnitude < 1e-6f)
                    fallbackAxis = Vector3.Cross(Vector3.right, currentAxisWorld);
                fallbackAxis.Normalize();
                swing = Quaternion.AngleAxis(180f, fallbackAxis);
            }
            else
            {
                swing = Quaternion.FromToRotation(currentAxisWorld, dir);
            }

            // Nhân bên TRÁI: thêm swing vào rotation hiện có, không thay thế hoàn toàn → roll
            // quanh track axis không bị reset mỗi frame.
            return swing * baseWorldRot;
        }
    }
}