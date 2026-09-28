using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Trục local dùng làm trục dài của Capsule — chọn được thay vì cố định local up như trước,
    /// khớp với property "Direction" của Dynamic Bone Collider quen thuộc.
    /// </summary>
    public enum DynamicBoneColliderDirection
    {
        [InspectorName("X-Axis")] XAxis,
        [InspectorName("Y-Axis")] YAxis,
        [InspectorName("Z-Axis")] ZAxis
    }

    /// <summary>
    /// Chế độ bán kính của Capsule Collider (cùng thiết kế với MiniColliderVisual bên BoneCloth):
    /// - Uniform: dùng chung 1 "Capsule Radius" cho cả 2 đầu → viên nang đều, trụ thẳng.
    /// - PerEnd: mỗi đầu 1 bán kính riêng ("Start Radius" / "End Radius") → capsule dạng côn
    ///   (to đầu này nhỏ đầu kia), hợp cho tay/chân/đuôi thon dần.
    /// </summary>
    public enum DynamicBoneCapsuleRadiusMode { Uniform, PerEnd }

    /// <summary>
    /// Chế độ kích thước của Box: Uniform = 1 Size (X,Y,Z) cho cả khối; PerFace = mặt trên và mặt dưới
    /// mỗi mặt có rộng/dài riêng (khối cụt), chiều cao lấy từ boxSize.y.
    /// </summary>
    public enum DynamicBoneBoxSizeMode { Uniform, PerFace }

    /// <summary>
    /// Component collider ĐỘC LẬP, gắn trực tiếp lên Transform (thường là 1 bone của rig) để dùng
    /// làm vật cản va chạm cho DynamicBone.
    ///
    /// TÁCH RIÊNG khỏi DynamicBone (trước đây collider được khai báo INLINE qua nested class
    /// DynamicBone.ColliderConfig, mỗi entry tự kéo 1 "Transform" + tự gõ size/offset trong list
    /// của TỪNG DynamicBone) vì cùng 3 lý do như MiniColliderVisual bên BoneCloth:
    /// 1) Gizmo trực quan: OnDrawGizmosSelected vẽ đúng hình dạng/kích thước collider ngay tại vị trí
    ///    bone, chỉnh xong thấy ngay trong Scene view (ColliderConfig cũ KHÔNG có gizmo).
    /// 2) Tái sử dụng: 1 collider (VD trên vai, hông...) có thể được kéo vào field "colliders" của
    ///    NHIỀU DynamicBone khác nhau (nhiều chain tóc/khăn/đuôi cùng va vào 1 collider) mà không
    ///    cần khai báo lại từng nơi, không sợ lệch config giữa các bản copy.
    /// 3) Gọn Inspector của DynamicBone: mỗi collider có cấu hình riêng ngay tại bone của nó, thay
    ///    vì 1 list dài các struct lồng nhau (Type/Transform/CenterOffset/Size/Reverse) khó phân biệt.
    ///
    /// CÁCH DÙNG: gắn component này lên Transform (bone), chọn Type + cấu hình kích thước, rồi kéo
    /// GameObject này vào field "colliders" (List&lt;DynamicBoneCollider&gt;) của DynamicBone.
    /// Component này TỰ NÓ không làm gì lúc runtime (không có Update) - nó chỉ là nguồn dữ liệu +
    /// gizmo. DynamicBone.InitializeTrees() lọc null/disabled còn DynamicBone.BuildColliderConfigData()
    /// đọc dữ liệu qua ToColliderConfigData() mỗi frame (qua DynamicBoneManager.RefreshPerFrameParams) -
    /// GatherCollidersJob (DynamicBoneJobs.cs) là nơi thực sự tính toán world-space collider mỗi
    /// frame - hoàn toàn không đổi so với trước, chỉ đổi NGUỒN đọc config.
    /// </summary>
    public class DynamicBoneCollider : MonoBehaviour
    {
        [Tooltip("Hình dạng collider:\n" +
                 "- Sphere: hình cầu, tâm bám theo Transform này + Center Offset, bán kính = Sphere Radius.\n" +
                 "- Capsule: hình viên nang (2 đầu cầu nối bằng trụ), trục dọc theo local Y (up) của " +
                 "Transform này. Dùng cho tay chân, cổ, thân người, đuôi...\n" +
                 "- Plane: mặt phẳng vô hạn, pháp tuyến = local up của Transform. Dùng cho sàn, tường, " +
                 "hoặc chặn 1 phía của 1 bộ phận rộng.")]
        public DynamicBone.ColliderShapeType type = DynamicBone.ColliderShapeType.Sphere;

        [Tooltip("Độ lệch tâm collider so với Transform này. Được xoay theo rotation của Transform này " +
                 "nhưng CHỈ nhân với scale của ROOT (transform.root.localScale), KHÔNG cộng dồn scale của " +
                 "các bone cha ở giữa hierarchy — cùng quy ước với Radius/Length/Width/Height.\n" +
                 "Dùng khi tâm collider mong muốn không trùng pivot của bone.")]
        public Vector3 centerOffset;

        [Header("Sphere")]
        [Tooltip("Bán kính hình cầu (chỉ dùng khi Type = Sphere). Bán kính thực tế còn nhân thêm với " +
                 "scale lớn nhất trong 3 trục của Transform.")]
        public float sphereRadius = 0.1f;

        [Header("Capsule")]
        [Tooltip("Capsule: trục local dùng làm trục dài. Plane: trục local dùng làm PHÁP TUYẾN (phía particle được đẩy ra).\n" +
                 "(X/Y/Z của Transform này). Mặc định các " +
                 "collider cũ dùng Y (up) — đổi sang X/Z nếu bone của bạn hướng trục khác.")]
        public DynamicBoneColliderDirection direction = DynamicBoneColliderDirection.YAxis;

        [Tooltip("Có tác dụng với Capsule, Box và Plane (Plane hữu hạn).\nBox: Bật = Center ở tâm hộp; Tắt = Center ở tâm MẶT TRÊN, hộp rủ xuống.\n" +
                 "Plane: Bật = Center nằm giữa hình chữ nhật; Tắt = Center nằm ở TRUNG ĐIỂM CẠNH TRÊN, mặt phẳng rủ xuống theo chiều Height.\n" +
                 "Capsule:\n" +
                 "- Bật (mặc định): 2 đầu capsule đối xứng qua Center (Center nằm CHÍNH GIỮA trục).\n" +
                 "- Tắt: 1 đầu neo tại Center, đầu còn lại kéo dài toàn bộ Length theo Direction " +
                 "(Center nằm Ở ĐẦU trục) — hợp khi bone đặt tại gốc/khớp thay vì giữa thân.")]
        public bool alignedOnCenter = true;

        [Tooltip("Chế độ bán kính 2 đầu capsule:\n" +
                 "- Uniform: 1 giá trị dùng chung, đơn giản, hợp cho tay/chân/thân dày đều.\n" +
                 "- Per End: 2 giá trị riêng biệt (Start/End), cho phép côn thon dần.")]
        public DynamicBoneCapsuleRadiusMode capsuleRadiusMode = DynamicBoneCapsuleRadiusMode.Uniform;

        [Tooltip("Bán kính dùng chung cho CẢ 2 đầu capsule - chỉ có tác dụng khi Radius Mode = Uniform.")]
        public float capsuleRadius = 0.1f;

        [Tooltip("Bán kính đầu START - chỉ có tác dụng khi Radius Mode = Per End.\n" +
                 "Đầu START là đầu +local Y (up) của Transform này TRƯỚC khi áp Reverse Direction; nếu " +
                 "Reverse Direction bật, đầu START sẽ đổi sang phía -Y (xem tooltip Reverse Direction).")]
        public float capsuleStartRadius = 0.1f;

        [Tooltip("Bán kính đầu END - chỉ có tác dụng khi Radius Mode = Per End.\n" +
                 "Đầu END là đầu -local Y (up) của Transform này TRƯỚC khi áp Reverse Direction; nếu " +
                 "Reverse Direction bật, đầu END sẽ đổi sang phía +Y (xem tooltip Reverse Direction).")]
        public float capsuleEndRadius = 0.05f;

        [Tooltip("Chiều dài trục capsule, đo dọc theo local up của Transform này (nhân thêm scale Y). " +
                 "Đây là khoảng cách GIỮA 2 TÂM đầu cầu, không tính thêm bán kính 2 đầu.")]
        public float capsuleLength = 0.2f;

        [Tooltip("Plane: đảo hướng pháp tuyến (đẩy particle sang phía ngược lại).\nCapsule: đảo ngược 2 đầu start/end của capsule (và theo " +
                 "đó đảo luôn bán kính giữa 2 đầu khi Radius Mode = Per End).\n" +
                 "Bật khi trục up của Transform bone đang ngược hướng mong muốn, thay vì phải xoay lại bone.")]
        public bool reverseDirection;

        [Header("Box")]
        [Tooltip("Uniform: 1 Size (X, Y, Z) cho cả khối.\nPer Face: mặt trên/mặt dưới mỗi mặt có Width (X) và Length (Z) riêng " +
                 "→ khối côn; chiều cao lấy từ Height.")]
        public DynamicBoneBoxSizeMode boxSizeMode = DynamicBoneBoxSizeMode.Uniform;

        [Tooltip("Kích thước hộp (X, Y, Z local), nhân theo scale của root. Chỉ dùng khi Type = Box.\n" +
                 "Per Face: chỉ dùng Y làm chiều cao.\n" +
                 "Hộp xoay theo rotation của Transform này. Aligned On Center tắt = Center nằm ở tâm MẶT TRÊN (+Y), hộp rủ xuống.")]
        public Vector3 boxSize = new Vector3(0.2f, 0.2f, 0.2f);

        [Tooltip("Per Face: kích thước MẶT TRÊN (+Y) — x = rộng theo local X, y = dài theo local Z.")]
        public Vector2 boxTopSize = new Vector2(0.2f, 0.2f);

        [Tooltip("Per Face: kích thước MẶT DƯỚI (-Y) — x = rộng theo local X, y = dài theo local Z.")]
        public Vector2 boxBottomSize = new Vector2(0.2f, 0.2f);

        [Header("Plane")]
        [Tooltip("CHỈ có tác dụng với Type = Plane. Bật = mặt phẳng vô hạn (hành vi cũ, Width/Height bị bỏ qua). " +
                 "Tắt = mặt phẳng hữu hạn hình chữ nhật kích thước Width x Height.")]
        public bool planeInfinite = true;

        [Tooltip("Chiều rộng mặt phẳng (nhân scale root). Trục: Direction X → local Z; Direction Y/Z → local X. Chỉ dùng khi Infinite tắt.")]
        public float planeWidth = 1f;

        [Tooltip("Chiều cao mặt phẳng (nhân scale root). Trục: Direction X/Z → local Y; Direction Y → local Z. Chỉ dùng khi Infinite tắt.")]
        public float planeHeight = 1f;

        [Header("Gizmo")]
        [Tooltip("Bật/tắt vẽ gizmo của collider này trong Scene view khi GameObject được chọn.")]
        public bool drawGizmo = true;

        [Tooltip("Màu gizmo, đổi khác nhau giữa các collider giúp phân biệt dễ hơn khi có nhiều " +
                 "collider chồng gần nhau (VD nách/vai/cổ).")]
        public Color gizmoColor = new Color(0.3f, 0.7f, 1f, 1f);

        /// <summary>Bán kính đầu START thực tế dùng để build ColliderConfigData (tuỳ theo Radius Mode).</summary>
        public float EffectiveStartRadius => capsuleRadiusMode == DynamicBoneCapsuleRadiusMode.Uniform ? capsuleRadius : capsuleStartRadius;

        /// <summary>Bán kính đầu END thực tế dùng để build ColliderConfigData (tuỳ theo Radius Mode).</summary>
        public float EffectiveEndRadius => capsuleRadiusMode == DynamicBoneCapsuleRadiusMode.Uniform ? capsuleRadius : capsuleEndRadius;

        /// <summary>Vector trục dài capsule trong WORLD SPACE, theo Direction đã chọn (X/Y/Z local).</summary>
        public Vector3 GetWorldAxisDirection()
        {
            switch (direction)
            {
                case DynamicBoneColliderDirection.XAxis: return transform.right;
                case DynamicBoneColliderDirection.ZAxis: return transform.forward;
                default: return transform.up;
            }
        }

        /// <summary>
        /// Scale dùng để nhân bán kính/chiều dài collider (Sphere Radius, Capsule Start/End Radius,
        /// Capsule Length) — CHỈ lấy từ Transform GỐC NGOÀI CÙNG của cả hierarchy (transform.root),
        /// KHÔNG dùng lossyScale cộng dồn qua từng Transform cha.
        ///
        /// LÝ DO: lossyScale nhân dồn TẤT CẢ scale từ root xuống tới transform này. Nếu 1 bone cha ở
        /// giữa chừng (VD bone kỹ thuật/twist bone/bone phụ trợ nào đó) bị đặt scale bất thường
        /// (VD 100,100,100) trong khi bản thân root và collider vẫn scale 1,1,1, thì lossyScale của
        /// collider vẫn ra 100,100,100 — kéo bán kính/chiều dài phóng to sai x100 dù không hề có chủ
        /// đích đó. Scale CÓ CHỦ ĐÍCH ảnh hưởng tới kích thước thật của cả nhân vật thường chỉ được
        /// đặt Ở ROOT (VD import scale của model), nên dùng root.localScale mới đúng ý đồ.
        /// </summary>
        public Vector3 GetSizeScale() => transform.root.localScale;

        /// <summary>
        /// Tâm collider world-space: position + rotation * (centerOffset * rootScale). Không dùng
        /// TransformPoint vì nó nhân dồn scale của mọi bone cha (VD parent 2 = 100 → offset x100).
        /// PHẢI khớp y hệt cách GatherCollidersJob tính center.
        /// </summary>
        public Vector3 GetWorldCenter()
            => transform.position + transform.rotation * Vector3.Scale(centerOffset, GetSizeScale());

        /// <summary>
        /// Build 1 ColliderConfigData (struct blittable, xem DynamicBoneJobTypes.cs) tương ứng để
        /// DynamicBone gửi vào NativeArray toàn cục của DynamicBoneManager mỗi frame. Đóng gói
        /// "size" đúng quy ước sẵn có (xem GatherCollidersJob trong DynamicBoneJobs.cs):
        /// Sphere → size.x = radius; Capsule → size.x/y/z = startRadius/endRadius/length;
        /// Plane → bỏ qua size.
        ///
        /// KHÔNG gán particleRadius ở đây: 1 collider có thể được dùng chung bởi nhiều DynamicBone
        /// khác nhau (mỗi cái có particleRadius riêng), nên DynamicBone.BuildColliderConfigData()
        /// là nơi gán particleRadius của CHÍNH NÓ vào struct trả về từ hàm này trước khi đưa vào
        /// buffer toàn cục — 1 collider component có thể sinh ra NHIỀU ColliderConfigData khác nhau
        /// (1 cho mỗi DynamicBone đang tham chiếu tới nó), y hệt cách MiniColliderVisual dùng chung
        /// giữa nhiều BoneClothComponent.
        /// </summary>
        public ColliderConfigData ToColliderConfigData()
        {
            Vector3 size;
            switch (type)
            {
                case DynamicBone.ColliderShapeType.Sphere:
                    size = new Vector3(sphereRadius, 0f, 0f);
                    break;
                case DynamicBone.ColliderShapeType.Capsule:
                    size = new Vector3(EffectiveStartRadius, EffectiveEndRadius, capsuleLength);
                    break;
                case DynamicBone.ColliderShapeType.Box:
                    size = boxSize;
                    break;
                default:
                    size = new Vector3(planeWidth, planeHeight, 0f);
                    break;
            }

            Vector2 boxTop = new Vector2(boxSize.x, boxSize.z);
            Vector2 boxBottom = boxTop;
            if (type == DynamicBone.ColliderShapeType.Box && boxSizeMode == DynamicBoneBoxSizeMode.PerFace)
            {
                boxTop = boxTopSize;
                boxBottom = boxBottomSize;
            }

            return new ColliderConfigData
            {
                boxTopSize = boxTop,
                boxBottomSize = boxBottom,
                type = (byte)type,
                centerOffset = centerOffset,
                size = size,
                reverseDirection = (byte)(reverseDirection ? 1 : 0),
                axis = (byte)direction,
                alignedOnCenter = (byte)(alignedOnCenter ? 1 : 0),
                planeInfinite = (byte)(planeInfinite ? 1 : 0),
                rootScale = GetSizeScale(),
                particleRadius = 0f // gán bởi DynamicBone.BuildColliderConfigData()
            };
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = gizmoColor;

            switch (type)
            {
                case DynamicBone.ColliderShapeType.Sphere:
                    DrawSphereGizmo();
                    break;
                case DynamicBone.ColliderShapeType.Capsule:
                    DrawCapsuleGizmo();
                    break;
                case DynamicBone.ColliderShapeType.Plane:
                    DrawPlaneGizmo();
                    break;
                case DynamicBone.ColliderShapeType.Box:
                    DrawBoxGizmo();
                    break;
            }
        }

        private void DrawSphereGizmo()
        {
            Vector3 scale = GetSizeScale();
            float r = sphereRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = GetWorldCenter();
            Gizmos.DrawWireSphere(center, r);
        }

        // Giữ NGUYÊN cách tính start/end point & gán radius y hệt GatherCollidersJob (case Capsule
        // trong DynamicBoneJobs.cs) để gizmo hiển thị ĐÚNG những gì sẽ chạy lúc runtime, tránh lệch
        // giữa "nhìn thấy lúc chỉnh" và "kết quả va chạm thật".
        private void DrawCapsuleGizmo()
        {
            Vector3 scale = GetSizeScale();
            Vector3 axis = GetWorldAxisDirection().normalized;

            float scaleAlongAxis = direction == DynamicBoneColliderDirection.XAxis ? scale.x
                : direction == DynamicBoneColliderDirection.YAxis ? scale.y
                : scale.z;
            float scalePerp = direction == DynamicBoneColliderDirection.XAxis ? (Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) * 0.5f
                : direction == DynamicBoneColliderDirection.YAxis ? (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f
                : (Mathf.Abs(scale.x) + Mathf.Abs(scale.y)) * 0.5f;

            float length = capsuleLength * Mathf.Abs(scaleAlongAxis);
            Vector3 center = GetWorldCenter();

            Vector3 start, end;
            if (alignedOnCenter)
            {
                start = center + axis * (length * 0.5f);
                end = center - axis * (length * 0.5f);
            }
            else
            {
                start = center;
                end = center + axis * length;
            }

            if (reverseDirection) { (start, end) = (end, start); }

            float startRadius = EffectiveStartRadius * scalePerp;
            float endRadius = EffectiveEndRadius * scalePerp;

            DrawWireCapsule(start, end, startRadius, endRadius, axis);

            // Chấm nhỏ đánh dấu Center (đặc biệt hữu ích khi Aligned On Center tắt — Center khi đó
            // trùng với 1 trong 2 đầu chứ không còn nằm giữa trục).
            Gizmos.DrawWireSphere(center, Mathf.Max(0.005f, (startRadius + endRadius) * 0.05f));
        }

        // Vẽ capsule bằng 2 wire sphere (2 đầu, đúng bán kính riêng từng đầu - kể cả khi côn) +
        // 4 đường nối quanh trục để gợi hình mặt trụ/côn giữa 2 đầu. KHÔNG dùng UnityEditor.Handles
        // để giữ code đơn giản, không phụ thuộc thêm - với mục đích debug/tuỳ chỉnh trong Scene
        // view, xấp xỉ này đã đủ trực quan.
        private static void DrawWireCapsule(Vector3 start, Vector3 end, float startRadius, float endRadius, Vector3 axis)
        {
            Gizmos.DrawWireSphere(start, startRadius);
            Gizmos.DrawWireSphere(end, endRadius);

            Vector3 upRef = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Vector3 side = Vector3.Cross(upRef, axis).normalized;
            Vector3 fwd = Vector3.Cross(axis, side).normalized;

            Gizmos.DrawLine(start + side * startRadius, end + side * endRadius);
            Gizmos.DrawLine(start - side * startRadius, end - side * endRadius);
            Gizmos.DrawLine(start + fwd * startRadius, end + fwd * endRadius);
            Gizmos.DrawLine(start - fwd * startRadius, end - fwd * endRadius);
        }

        private void DrawBoxGizmo()
        {
            Vector3 scale = GetSizeScale();
            Vector2 top = boxSizeMode == DynamicBoneBoxSizeMode.PerFace ? boxTopSize : new Vector2(boxSize.x, boxSize.z);
            Vector2 bot = boxSizeMode == DynamicBoneBoxSizeMode.PerFace ? boxBottomSize : top;
            float ax = Mathf.Abs(scale.x), az = Mathf.Abs(scale.z);
            Vector2 tH = new Vector2(top.x * ax, top.y * az) * 0.5f;
            Vector2 bH = new Vector2(bot.x * ax, bot.y * az) * 0.5f;
            float hy = boxSize.y * Mathf.Abs(scale.y) * 0.5f;

            // Aligned tắt: Center (gốc gizmo) = tâm mặt trên → cả khối lệch xuống -hy.
            float yShift = alignedOnCenter ? 0f : -hy;

            Vector3[] c = new Vector3[8];
            c[0] = new Vector3(-tH.x, hy + yShift, -tH.y);
            c[1] = new Vector3(tH.x, hy + yShift, -tH.y);
            c[2] = new Vector3(tH.x, hy + yShift, tH.y);
            c[3] = new Vector3(-tH.x, hy + yShift, tH.y);
            c[4] = new Vector3(-bH.x, -hy + yShift, -bH.y);
            c[5] = new Vector3(bH.x, -hy + yShift, -bH.y);
            c[6] = new Vector3(bH.x, -hy + yShift, bH.y);
            c[7] = new Vector3(-bH.x, -hy + yShift, bH.y);

            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(GetWorldCenter(), transform.rotation, Vector3.one);
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                Gizmos.DrawLine(c[i], c[n]);         // mặt trên
                Gizmos.DrawLine(c[4 + i], c[4 + n]); // mặt dưới
                Gizmos.DrawLine(c[i], c[4 + i]);     // cạnh bên
            }
            float maxDim = Mathf.Max(tH.x, tH.y, bH.x, bH.y, hy);
            Gizmos.DrawWireSphere(Vector3.zero, Mathf.Max(0.005f, maxDim * 0.1f));
            Gizmos.matrix = old;
        }

        private void DrawPlaneGizmo()
        {
            Vector3 scale = GetSizeScale();
            Vector3 center = GetWorldCenter();

            // Khớp GatherCollidersJob: Direction = pháp tuyến; width/height là 2 trục còn lại.
            Vector3 normal, wAxis, hAxis;
            float sw, sh;
            switch (direction)
            {
                case DynamicBoneColliderDirection.XAxis:
                    normal = transform.right; wAxis = transform.forward; hAxis = transform.up;
                    sw = scale.z; sh = scale.y; break;
                case DynamicBoneColliderDirection.ZAxis:
                    normal = transform.forward; wAxis = transform.right; hAxis = transform.up;
                    sw = scale.x; sh = scale.y; break;
                default:
                    normal = transform.up; wAxis = transform.right; hAxis = transform.forward;
                    sw = scale.x; sh = scale.z; break;
            }
            normal = normal.normalized; wAxis = wAxis.normalized; hAxis = hAxis.normalized;
            if (reverseDirection) normal = -normal;

            float w, h;
            Vector3 origin; // góc (-wAxis, -hAxis) của lưới
            if (planeInfinite)
            {
                // Vô hạn: chỉ vẽ lưới preview tham khảo quanh Center.
                w = h = Mathf.Max(Mathf.Abs(sw), Mathf.Abs(sh), 0.01f);
                origin = center - wAxis * (w * 0.5f) - hAxis * (h * 0.5f);
            }
            else
            {
                w = planeWidth * Mathf.Abs(sw);
                h = planeHeight * Mathf.Abs(sh);
                // Aligned tắt: Center = trung điểm cạnh trên, hình chữ nhật rủ xuống -hAxis.
                Vector3 rectCenter = alignedOnCenter ? center : center - hAxis * (h * 0.5f);
                origin = rectCenter - wAxis * (w * 0.5f) - hAxis * (h * 0.5f);
            }

            const int divisions = 3;
            for (int i = 0; i <= divisions; i++)
            {
                float t = i / (float)divisions;
                Gizmos.DrawLine(origin + wAxis * (w * t), origin + wAxis * (w * t) + hAxis * h);
                Gizmos.DrawLine(origin + hAxis * (h * t), origin + hAxis * (h * t) + wAxis * w);
            }

            // Mũi tên pháp tuyến từ Center (pivot), hướng phía particle được đẩy ra
            float arrow = 0.5f * Mathf.Max(w, h);
            Vector3 tip = center + normal * arrow;
            Gizmos.DrawLine(center, tip);
            Gizmos.DrawLine(tip, tip - normal * (arrow * 0.25f) + wAxis * (arrow * 0.08f));
            Gizmos.DrawLine(tip, tip - normal * (arrow * 0.25f) - wAxis * (arrow * 0.08f));
        }
#endif
    }
}