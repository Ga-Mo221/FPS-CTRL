using UnityEngine;

// Static class: đây là tập hợp hàm thuần túy, không giữ state riêng (toàn bộ
// state đều nằm trong FlowerSetup / các tham số truyền vào), nên không cần
// instance hóa -> tránh hẳn lỗi quên "new" gây NullReferenceException.
public static class MoveAndRotate
{
    public static void StartMoving(FlowerSetup flowerSetup)
    {
        if (flowerSetup.CanMove || flowerSetup.Target == null)
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
    public static void ForceStartMoving(FlowerSetup flowerSetup)
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
        ref bool left
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

        // Dùng Mathf.DeltaAngle để tránh lỗi wrap-around khi eulerAngles.y nhảy
        // qua mốc 0/360 độ.
        float delta = Mathf.DeltaAngle(lateRootRotY, root.rotation.eulerAngles.y);

        if (delta <= -angle)
        {
            lateRootRotY = root.rotation.eulerAngles.y;
            left = false;
            StartMoving(flowerSetup_R);
            StartMoving(flowerSetup_L);
        }
        else if (delta >= angle)
        {
            lateRootRotY = root.rotation.eulerAngles.y;
            left = true;
            StartMoving(flowerSetup_L);
            StartMoving(flowerSetup_R);
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
        float jumpHeight,
        float duration,
        float offset
        )
    {
        if (flowerSetup.Target == null)
            return false;

        if (!flowerSetup.CanMove)
            return false;
        
        //Debug.Log($"{flowerSetup.Owner.position} and {flowerSetup.Target.position}");

        // Đánh dấu "đang thực sự animate frame này" để Lock() (chạy trong
        // Update, trước LateUpdate) biết mà KHÔNG ghi đè freeze lên bông hoa
        // này.
        flowerSetup.IsMoving = true;

        // Mỗi FlowerSetup có timer riêng, không dùng chung.
        flowerSetup.Timer += Time.deltaTime;

        float t = Mathf.Clamp01(flowerSetup.Timer / duration);

        Vector3 targetPos = flowerSetup.Target.position + Vector3.up * offset;
        Quaternion targetRot = flowerSetup.Target.rotation;
        
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