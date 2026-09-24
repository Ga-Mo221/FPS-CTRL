using UnityEngine;

// Static class: đây là tập hợp hàm thuần túy, không giữ state riêng (toàn bộ
// state đều nằm trong FlowerSetup / các tham số truyền vào), nên không cần
// instance hóa -> tránh hẳn lỗi quên "new" gây NullReferenceException.
public static class MoveAndRotate
{
    // Ngưỡng khoảng cách & góc để xem Owner đã "đến nơi" Target chưa.
    // Có thể điều chỉnh nếu cần độ nhạy khác.
    private const float POS_THRESHOLD = 0.01f;   // mét
    private const float ROT_THRESHOLD = 1f;       // độ

    /// <summary>
    /// Tính lại world-pose "ổn định" của Target dựa trên smoothedYawDeg (yaw đã
    /// qua SmoothDampAngle trong NetworkCharacterControllerCustom.Render(), KHÔNG
    /// đọc Target.transform hay root.rotation sống.
    ///
    /// Lý do: Target là con (gián tiếp) của root network. root.transform.rotation
    /// bị ghi 2 lần/frame với timing khác nhau — AccumulateYaw() snap thô ngay
    /// lập tức trong tick simulation (có thể chạy nhiều lần/frame khi
    /// resimulate), rồi Render() ghi đè lại bằng giá trị đã smooth. Vì vậy world
    /// pose của Target (và root.rotation.eulerAngles.y) có thể "rung" giữa 2 giá
    /// trị này tùy thời điểm đọc trong frame → chân IK giật rồi bị kéo ngược lại.
    ///
    /// Cách khắc phục: dùng offset cục bộ CỐ ĐỊNH (chụp 1 lần lúc Setup, xem
    /// TurnInPlaceSetup) + smoothedYawDeg (giá trị DUY NHẤT không bao giờ bị
    /// AccumulateYaw ghi thô) để tự dựng lại world pose ổn định.
    /// root.position và root.eulerAngles (X/Z) vẫn đọc trực tiếp vì không bị
    /// double-write theo kiểu trên.
    /// </summary>
    public static void GetStableTargetPose(
        FlowerSetup flowerSetup,
        Transform root,
        float smoothedYawDeg,
        float offset,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        Vector3 rootEuler = root.eulerAngles;
        Quaternion stableRootRot = Quaternion.Euler(rootEuler.x, smoothedYawDeg, rootEuler.z);

        worldPos = root.position + stableRootRot * flowerSetup.TargetLocalOffsetPos;
        worldRot = stableRootRot * flowerSetup.TargetLocalOffsetRot;

        worldPos += Vector3.up * offset;
    }

    /// <summary>
    /// Trả về true nếu Owner đã ở đúng vị trí/rotation của Target (tính cả offset).
    /// Dùng để bỏ qua lần kích hoạt dư thừa khi owner đã khớp target rồi.
    /// </summary>
    public static bool IsAtTarget(FlowerSetup flowerSetup, Transform root, float smoothedYawDeg, float offset)
    {
        if (flowerSetup.Target == null || flowerSetup.Owner == null) return true;

        GetStableTargetPose(flowerSetup, root, smoothedYawDeg, offset, out Vector3 targetPos, out Quaternion targetRot);

        bool posMatch = Vector3.Distance(flowerSetup.Owner.position, targetPos) < POS_THRESHOLD;
        bool rotMatch = Quaternion.Angle(flowerSetup.Owner.rotation, targetRot) < ROT_THRESHOLD;
        return posMatch && rotMatch;
    }

    public static void StartMoving(FlowerSetup flowerSetup, Transform root, float smoothedYawDeg, float offset = 0f)
    {
        if (flowerSetup.CanMove || flowerSetup.Target == null)
            return;

        // Nếu Owner đã ở đúng Target rồi → không cần animate, bỏ qua
        if (IsAtTarget(flowerSetup, root, smoothedYawDeg, offset))
            return;

        // Snap về freeze để StartPosition không bị IK làm lệch
        flowerSetup.Owner.position = flowerSetup.FreezePosition;
        flowerSetup.Owner.rotation = flowerSetup.FreezeRotation;

        flowerSetup.CanMove = true;
        flowerSetup.Timer = 0f;
        flowerSetup.StartPosition = flowerSetup.Owner.position;
        flowerSetup.StartRotation = flowerSetup.Owner.rotation;
    }

    /// <summary>
    /// Giống StartMoving nhưng KHÔNG check CanMove — dùng khi cần
    /// force reset về target (ví dụ khi TurnOn sau khi owner đã lệch).
    /// </summary>
    public static void ForceStartMoving(FlowerSetup flowerSetup, Transform root, float smoothedYawDeg)
    {
        if (flowerSetup.Target == null) return;

        // Snap owner về freeze trước, rồi mới lấy làm StartPosition
        flowerSetup.Owner.position = flowerSetup.FreezePosition;
        flowerSetup.Owner.rotation = flowerSetup.FreezeRotation;

        flowerSetup.CanMove = true;
        flowerSetup.IsMoving = false;
        flowerSetup.Timer = 0f;
        flowerSetup.StartPosition = flowerSetup.Owner.position;
        flowerSetup.StartRotation = flowerSetup.Owner.rotation;
    }

    // Chỉ lo việc khóa World Transform khi flower KHÔNG đang thực sự animate
    // (IsMoving == false). Chạy độc lập, không liên quan gì tới thứ tự trái/phải.
    public static void Lock(FlowerSetup flowerSetup)
    {
        if (flowerSetup.Target == null)
            return;
        
        if (!flowerSetup.IsMoving)
        {
            flowerSetup.Owner.position = flowerSetup.FreezePosition;
            flowerSetup.Owner.rotation = flowerSetup.FreezeRotation;
        }
    }

    public static void ToggleTurn(
        FlowerSetup flowerSetup_L,
        FlowerSetup flowerSetup_R,
        Transform root,
        ref float lateRootRotY,
        float angle,
        ref bool left,
        float smoothedYawDeg,
        float offset = 0f        // <-- thêm offset để IsAtTarget kiểm tra đúng
        )
    {
        // Nếu vẫn còn 1 bông hoa đang trong chu kỳ (CanMove == true, dù đang
        // active animate hay đang đợi tới lượt), TUYỆT ĐỐI không được lật
        // "left" hay bắt đầu chu kỳ mới. Phải đợi CẢ 2 bước (1 rồi 2) hoàn tất
        // hẳn (CanMove về false ở cả 2 bên) mới cho phép xét chu kỳ mới -
        // tránh trường hợp move thứ 2 đang chạy dở bị "nhảy cóc" về lại move 1
        // và lơ lửng giữa chừng.
        bool sequenceInProgress = flowerSetup_L.CanMove || flowerSetup_R.CanMove;
        if (sequenceInProgress)
            return;

        // Dùng smoothedYawDeg (yaw đã qua SmoothDampAngle trong Render(), KHÔNG
        // bao giờ bị AccumulateYaw() snap thô) thay vì root.rotation.eulerAngles.y
        // để tính delta. Đọc root.rotation trực tiếp ở đây từng gây delta giả:
        // root.rotation bị ghi thô trong tick simulation rồi Render() mới smooth
        // lại, nên Update() có thể bắt được giá trị "giữa chừng" chưa ổn định,
        // gây bắn StartMoving sai nhịp rồi bị kéo/lùi lại ngay sau đó (giật).
        // Dùng Mathf.DeltaAngle để tránh lỗi wrap-around khi giá trị nhảy qua
        // mốc 0/360 độ.
        float delta = Mathf.DeltaAngle(lateRootRotY, smoothedYawDeg);

        if (delta <= -angle)
        {
            lateRootRotY = smoothedYawDeg;
            left = false;
            StartMoving(flowerSetup_R, root, smoothedYawDeg, offset);
            StartMoving(flowerSetup_L, root, smoothedYawDeg, offset);
        }
        else if (delta >= angle)
        {
            lateRootRotY = smoothedYawDeg;
            left = true;
            StartMoving(flowerSetup_L, root, smoothedYawDeg, offset);
            StartMoving(flowerSetup_R, root, smoothedYawDeg, offset);
        }
        // Lưu ý: nếu root xoay quá nhanh trong 1 frame (delta có thể vượt nhiều
        // lần angle liên tiếp), logic này vẫn chỉ bắt được 1 lần "quẹo" mỗi
        // frame. Vì ToggleTurn bị chặn hoàn toàn trong lúc sequenceInProgress,
        // độ xoay tích lũy thêm trong thời gian đó sẽ được "gộp" lại và xét
        // ngay khi chu kỳ hiện tại vừa xong, nhưng vẫn chỉ bắt 1 lần quẹo cho
        // toàn bộ phần tích lũy đó. Nếu cần chính xác tuyệt đối ở tốc độ cao,
        // cân nhắc dùng vòng lặp while hoặc tăng tần suất kiểm tra (FixedUpdate).
    }

    // Chỉ lo phần animation (lerp/slerp tới Target). Việc khóa (freeze) khi
    // không di chuyển được xử lý riêng ở Lock(), chạy độc lập mỗi frame.
    public static bool Move(
        FlowerSetup flowerSetup,
        Transform root,
        float smoothedYawDeg,
        float jumpHeight,
        float duration,
        float offset
        )
    {
        if (flowerSetup.Target == null)
            return false;

        if (!flowerSetup.CanMove)
            return false;

        // Đánh dấu "đang thực sự animate frame này" để Lock() (chạy trong
        // Update, trước LateUpdate) biết mà KHÔNG ghi đè freeze lên bông hoa
        // này.
        flowerSetup.IsMoving = true;

        // Mỗi FlowerSetup có timer riêng, không dùng chung.
        flowerSetup.Timer += Time.deltaTime;

        float t = Mathf.Clamp01(flowerSetup.Timer / duration);

        // Đích của lerp/slerp là world-pose ỔN ĐỊNH (tính lại từ offset cố định
        // + smoothedYawDeg), không đọc Target.transform sống — nếu không, đích
        // sẽ "rung" theo rotation chưa ổn định của root ngay trong lúc đang lerp.
        GetStableTargetPose(flowerSetup, root, smoothedYawDeg, offset, out Vector3 targetPos, out Quaternion targetRot);

        Vector3 pos = Vector3.Lerp(flowerSetup.StartPosition, targetPos, t);
        
        pos.y += jumpHeight * 4f * t * (1f - t);
        
        flowerSetup.Owner.position = pos;
        flowerSetup.Owner.rotation = Quaternion.Slerp(flowerSetup.StartRotation, targetRot, t);

        if (t >= 1f)
        {
            flowerSetup.Owner.position = targetPos;
            flowerSetup.Owner.rotation = targetRot;

            flowerSetup.FreezePosition = targetPos;
            flowerSetup.FreezeRotation = targetRot;

            flowerSetup.IsMoving = false;
            flowerSetup.CanMove = false;
            flowerSetup.Timer = 0f;
            return false;
        }
        return true;
    }

    public static void resetFree(FlowerSetup flowerSetup)
    {
        if (flowerSetup.Target == null) return;
        // Chỉ snapshot vị trí HIỆN TẠI của owner làm freeze,
        // KHÔNG gọi StartMoving — ToggleTurn sẽ gọi khi delta đủ lớn
        flowerSetup.FreezePosition = flowerSetup.Owner.position;
        flowerSetup.FreezeRotation = flowerSetup.Owner.rotation;
        flowerSetup.CanMove = false;
        flowerSetup.IsMoving = false;
    }
}