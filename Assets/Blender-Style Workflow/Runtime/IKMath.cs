using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public static class IKMath
    {
        /// <summary>
        /// Overload tương thích ngược cho code cũ đang gọi SolveFABRIK với chữ ký gốc (6 tham số).
        /// Sẽ tự tính totalLength từ bonesLength (giống hành vi bản gốc trước khi tối ưu).
        /// Nơi nào gọi được nên chuyển sang overload 7 tham số (truyền sẵn totalLength) để có lợi ích hiệu năng.
        /// </summary>
        public static void SolveFABRIK(Vector3[] positions, float[] bonesLength, Vector3 targetPos, Vector3 rootPos,
            int iterations, float delta)
        {
            float totalLength = 0f;
            for (int i = 0; i < bonesLength.Length; i++) totalLength += bonesLength[i];
            SolveFABRIK(positions, bonesLength, targetPos, rootPos, iterations, delta, totalLength);
        }

        /// <summary>
        /// Giải thuật FABRIK cập nhật mảng vị trí các khớp xương hướng về Target Pos
        /// </summary>
        /// <param name="totalLength">
        /// Tổng độ dài toàn bộ chuỗi xương (tổng bonesLength), truyền sẵn từ bên ngoài
        /// (tính 1 lần lúc Init) để tránh phải cộng dồn lại mảng bonesLength mỗi Frame.
        /// </param>
        public static void SolveFABRIK(Vector3[] positions, float[] bonesLength, Vector3 targetPos, Vector3 rootPos,
            int iterations, float delta, float totalLength)
        {
            int originLength = positions.Length;

            // Trường hợp Target nằm quá xa ngoài tầm với của chuỗi xương
            if ((targetPos - rootPos).sqrMagnitude >= totalLength * totalLength)
            {
                Vector3 direction = (targetPos - rootPos).normalized;
                for (int i = 1; i < originLength; i++)
                {
                    positions[i] = positions[i - 1] + direction * bonesLength[i - 1];
                }

                return;
            }

            // Trường hợp Target nằm trong tầm với -> Lặp để tối ưu khoảng cách
            float deltaSqr = delta * delta; // Tính sẵn 1 lần, tránh nhân lại mỗi vòng lặp
            for (int iter = 0; iter < iterations; iter++)
            {
                // STAGE 1: Backward Update (Đi ngược từ Target về Root)
                positions[originLength - 1] = targetPos;
                for (int i = originLength - 2; i >= 0; i--)
                {
                    Vector3 dir = (positions[i] - positions[i + 1]).normalized;
                    positions[i] = positions[i + 1] + dir * bonesLength[i];
                }

                // STAGE 2: Forward Update (Đi xuôi từ Root trở lại vị trí gốc cố định)
                positions[0] = rootPos;
                for (int i = 1; i < originLength; i++)
                {
                    Vector3 dir = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + dir * bonesLength[i - 1];
                }

                // Kiểm tra nếu đã đủ gần target thì dừng sớm
                if ((positions[originLength - 1] - targetPos).sqrMagnitude < deltaSqr)
                    break;
            }
        }
    }
}