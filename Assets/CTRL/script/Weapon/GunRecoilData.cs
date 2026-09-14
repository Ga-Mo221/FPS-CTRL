using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject chứa toàn bộ tham số recoil.
/// Create → Right-click Project → Weapons → Gun Recoil Data
/// </summary>
[CreateAssetMenu(fileName = "NewGunRecoilData", menuName = "Weapons/Gun Recoil Data", order = 0)]
public class GunRecoilData : ScriptableObject
{
    // ─────────────────────────────────────────────
    //  FIRING MODE PERMISSIONS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Bitmask — tick các chế độ bắn mà khẩu súng này được phép dùng.
    /// Có thể tick 1, 2 hoặc cả 3 mode.
    /// </summary>
    [Flags]
    public enum AllowedModes
    {
        Semi  = 1 << 0,   // 1
        Auto  = 1 << 1,   // 2
        Burst = 1 << 2,   // 4
        Melee = 1 << 3,
    }

    [Header("Firing Mode Permissions")]
    [Tooltip("Các chế độ bắn mà khẩu súng này được phép sử dụng.\n" +
             "Tick 1, 2 hoặc cả 3 mode tùy loại súng:\n\n" +
             "• Semi only        → súng bắn đơn (pistol, sniper)\n" +
             "• Auto only        → súng máy (LMG)\n" +
             "• Semi + Burst     → súng trường chiến thuật\n" +
             "• Semi + Auto      → AR thông thường\n" +
             "• Semi + Auto + Burst → AR full-featured\n\n" +
             "GunRecoilController sẽ từ chối SetFiringMode() nếu mode không được phép.")]
    public AllowedModes allowedModes = AllowedModes.Semi | AllowedModes.Auto | AllowedModes.Burst;
    
    [Header("Brust Count")]
    [Tooltip("Số phát bắn của mỗi lần bắn ở chế độ Burst")]
    [Range(0, 5)]
    public int BurstCount = 3;

    // ─────────────────────────────────────────────
    //  ROTATION TARGETS
    // ─────────────────────────────────────────────
    [Header("Rotation Targets")]

    [Tooltip("Góc giật theo trục X (pitch = ngóc lên/cúi xuống) mỗi phát bắn.\n\n" +
             "• X = giá trị nhỏ nhất (thường âm = nòng súng ngóc lên)\n" +
             "• Y = giá trị lớn nhất\n\n" +
             "Ví dụ: (-3, -2) → mỗi phát giật ngóc lên ngẫu nhiên từ 2° đến 3°.\n" +
             "Để pitch nhất quán, đặt X = Y (ví dụ: -2, -2).")]
    public Vector2 pitch = new Vector2(-2f, -2f);

    [Tooltip("Góc giật theo trục Z (roll = nghiêng trái/phải) mỗi phát bắn.\n" +
             "Dùng 2 cặp giá trị để tạo ngưỡng min/max ngẫu nhiên:\n\n" +
             "• X = cận dưới của ngưỡng min  (ví dụ: -4)\n" +
             "• Y = cận trên của ngưỡng min  (ví dụ: -3)\n" +
             "  → randomMin = Random(X, Y)\n\n" +
             "• Z = cận dưới của ngưỡng max  (ví dụ:  3)\n" +
             "• W = cận trên của ngưỡng max  (ví dụ:  4)\n" +
             "  → randomMax = Random(Z, W)\n\n" +
             "Roll cuối cùng = Random(randomMin, randomMax).\n" +
             "Cách này giúp súng nghiêng sang hai phía đối xứng một cách tự nhiên.")]
    public Vector4 roll = new Vector4(-4f, -3f, 3f, 4f);

    [Tooltip("Góc giật theo trục Y (yaw = xoay ngang trái/phải) mỗi phát bắn.\n" +
             "Dùng 2 cặp giá trị để tạo ngưỡng min/max ngẫu nhiên:\n\n" +
             "• X = cận dưới của ngưỡng min  (ví dụ: -1)\n" +
             "• Y = cận trên của ngưỡng min  (ví dụ: -0.5)\n" +
             "  → randomMin = Random(X, Y)\n\n" +
             "• Z = cận dưới của ngưỡng max  (ví dụ:  0.5)\n" +
             "• W = cận trên của ngưỡng max  (ví dụ:  1)\n" +
             "  → randomMax = Random(Z, W)\n\n" +
             "Yaw cuối cùng = Random(randomMin, randomMax).\n" +
             "Giá trị nhỏ (±0.5–1°) tạo cảm giác súng lắc nhẹ ngang; tăng lên nếu muốn kick mạnh hơn.")]
    public Vector4 yaw = new Vector4(-1f, -0.5f, 0.5f, 1f);

    // ─────────────────────────────────────────────
    //  TRANSLATION TARGETS
    // ─────────────────────────────────────────────
    [Header("Translation Targets")]

    [Tooltip("Độ giật lùi theo trục Z (súng đẩy về phía sau camera) mỗi phát bắn.\n\n" +
             "• X = khoảng lùi nhỏ nhất (giá trị âm, ví dụ: -0.03)\n" +
             "• Y = khoảng lùi lớn nhất (ít âm hơn, ví dụ: -0.022)\n\n" +
             "Mỗi phát bắn lùi ngẫu nhiên trong khoảng [X, Y].\n" +
             "Giá trị điển hình: -0.01 → -0.05. Không đặt dương (súng sẽ chạy về phía trước).")]
    public Vector2 kickback = new Vector2(-0.03f, -0.022f);

    [Tooltip("Độ nảy lên theo trục Y (súng nhấc lên) mỗi phát bắn.\n\n" +
             "• X = khoảng nảy nhỏ nhất (ví dụ: 0.005)\n" +
             "• Y = khoảng nảy lớn nhất (ví dụ: 0.007)\n\n" +
             "Kết hợp với Pitch tạo cảm giác giật đầu nòng vừa xoay vừa nhấc lên.\n" +
             "Giữ nhỏ (< 0.01) để trông tự nhiên; tăng nếu muốn súng nảy mạnh.")]
    public Vector2 kickUp = new Vector2(0.005f, 0.007f);

    [Tooltip("Độ lệch ngang theo trục X (súng dịch sang phải) mỗi phát bắn.\n\n" +
             "• X = khoảng lệch nhỏ nhất (âm = lệch trái)\n" +
             "• Y = khoảng lệch lớn nhất (dương = lệch phải)\n\n" +
             "Đặt (0, 0) nếu không muốn súng lắc ngang.\n" +
             "Ví dụ (-0.005, 0.005) tạo rung nhẹ ngẫu nhiên sang hai phía.")]
    public Vector2 kickRight = new Vector2(0f, 0f);

    // ─────────────────────────────────────────────
    //  AIMING MULTIPLIERS
    // ─────────────────────────────────────────────
    [Header("Aiming Multipliers")]

    [Tooltip("Hệ số nhân giật xoay (rotation) khi đang ngắm ADS.\n" +
             "Áp dụng lên từng trục ngay trước khi cộng vào target:\n\n" +
             "• X = nhân Pitch  (ví dụ: 0.5 → giật lên chỉ bằng nửa)\n" +
             "• Y = nhân Roll   (ví dụ: 0.3 → nghiêng ít hơn nhiều khi ADS)\n" +
             "• Z = nhân Yaw    (ví dụ: 0.5 → lắc ngang ít hơn)\n\n" +
             "Đặt = 1 để giữ nguyên như khi bắn từ hông.\n" +
             "Thường đặt < 1 (ví dụ 0.3–0.6) vì khi ADS súng cần ổn định hơn.")]
    public Vector3 aimRotMultiplier = Vector3.one;

    [Tooltip("Hệ số nhân giật tịnh tiến (translation/location) khi đang ngắm ADS.\n" +
             "Áp dụng lên từng trục tịnh tiến:\n\n" +
             "• X = nhân KickRight (lệch ngang)\n" +
             "• Y = nhân KickUp    (nảy lên)\n" +
             "• Z = nhân Kickback  (lùi ra sau)\n\n" +
             "Đặt = 1 để giữ nguyên.\n" +
             "Thường đặt < 1 khi ADS, ví dụ (0.5, 0.5, 0.7) giảm dịch chuyển súng khi ngắm.")]
    public Vector3 aimLocMultiplier = Vector3.one;

    // ─────────────────────────────────────────────
    //  AUTO / BURST SETTINGS
    // ─────────────────────────────────────────────
    [Header("Auto / Burst Settings")]

    [Tooltip("Tốc độ lerp (decay) của rotation target về 0 sau mỗi phát bắn.\n" +
             "Giá trị cao = phục hồi nhanh hơn, giật cảm giác 'snap'; thấp = phục hồi chậm, mượt hơn.\n\n" +
             "• X = tốc độ phục hồi Pitch/Yaw khi bắn Semi (ví dụ: 15 → nhanh)\n" +
             "• Y = tốc độ phục hồi Pitch/Yaw khi bắn Auto (ví dụ: 9 → chậm hơn để giật chồng nhau)\n" +
             "• Z = tốc độ smooth riêng cho Roll (thường thấp hơn, ví dụ: 5, để roll trông mượt mà)\n\n" +
             "Tăng X nếu semi bắn nhanh mà cần reset về giữa 2 phát; giảm Y nếu muốn auto leo dần.")]
    public Vector3 smoothRot = new Vector3(15f, 9f, 5f);

    [Tooltip("Tốc độ lerp (decay) của location target về 0 sau mỗi phát bắn.\n\n" +
             "• X = tốc độ phục hồi vị trí khi bắn Semi (ví dụ: 1.1 → rất chậm, kick lùi cảm giác nặng)\n" +
             "• Y = tốc độ phục hồi vị trí khi bắn Auto (ví dụ: 25 → nhanh để kick không bị cộng dồn quá)\n" +
             "• Z = (dự phòng, chưa dùng) có thể mở rộng sau\n\n" +
             "Semi thường dùng X thấp để cảm giác súng 'ngồi lại' sau phát bắn.")]
    public Vector3 smoothLoc = new Vector3(1.1f, 25f, 55f);

    [Tooltip("Hệ số nhân thêm lên giật xoay khi đang bắn chế độ Auto hoặc Burst.\n" +
             "Mô phỏng hiện tượng súng leo dần (climb) khi bắn liên tục.\n\n" +
             "• X = nhân Roll   (ví dụ: 1.3 → nghiêng mạnh hơn 30% khi auto)\n" +
             "• Y = (chưa dùng — giữ = 1, không ảnh hưởng đến gameplay)\n" +
             "• Z = nhân Yaw    (ví dụ: 7 → lắc ngang nhiều hơn khi auto)\n\n" +
             "Đặt = 1 để không thay đổi so với semi. Tăng Z tạo hiệu ứng 'spin-up' đặc trưng súng máy.")]
    public Vector3 extraRot = new Vector3(1.3f, 1f, 7f);

    [Tooltip("Hệ số nhân thêm lên giật tịnh tiến khi đang bắn chế độ Auto hoặc Burst.\n\n" +
             "• X = nhân KickRight  (lệch ngang thêm khi auto)\n" +
             "• Y = nhân KickUp     (nảy lên thêm khi auto)\n" +
             "• Z = nhân Kickback   (ví dụ: 1.3 → lùi thêm 30% khi auto)\n\n" +
             "Đặt = 1 để tịnh tiến không thay đổi giữa semi và auto.\n" +
             "Tăng Z để cảm giác súng máy 'đẩy mạnh hơn' khi bắn liên thanh.")]
    public Vector3 extraLoc = new Vector3(1f, 1f, 1.3f);

    [Header("Rotation Clamp — Target Accumulation")]
    [Tooltip("Hệ số nhân lên giá trị kick tối đa để tính ngưỡng clamp tự động.\n\n" +
             "Clamp được tính từ chính data hiện tại:\n" +
             "  maxPitch = pitch.x (worst case) × multiplier\n" +
             "  maxRoll  = roll.w  × extraRot.x × multiplier\n" +
             "  maxYaw   = yaw.w   × extraRot.z × multiplier\n\n" +
             "• 1.0 = chặn ngay tại mức kick tối đa lý thuyết (khá chặt)\n" +
             "• 3–5 = cho phép cộng dồn vài phát trước khi chặn (khuyến nghị)\n" +
             "• 0   = tắt clamp hoàn toàn\n\n" +
             "Tăng lên nếu cảm thấy bị 'cứng' khi bắn; giảm xuống nếu vẫn bị xoay tròn.")]
    [Range(0f, 20f)]
    public float clampMultiplier = 5f;

    [Header("Translation Clamp — Target Accumulation")]
    [Tooltip("Hệ số nhân lên giá trị kick tịnh tiến tối đa để tính ngưỡng clamp.\n\n" +
             "Clamp được tính từ data hiện tại:\n" +
             "  maxKickback  = kickback.x  × extraLoc.z × locClampMultiplier\n" +
             "  maxKickUp    = kickUp.y    × extraLoc.y × locClampMultiplier\n" +
             "  maxKickRight = kickRight.y × extraLoc.x × locClampMultiplier\n\n" +
             "• 1–3 = giới hạn chặt, vũ khí không bị bay xa khỏi origin\n" +
             "• 0   = tắt clamp tịnh tiến (nguy hiểm — vũ khí có thể bay xuyên map)\n\n" +
             "ĐÂY LÀ THAM SỐ QUAN TRỌNG ĐỂ TRÁNH VŨ KHÍ BAY KHỎI MAP.")]
    [Range(0f, 20f)]
    public float locClampMultiplier = 3f;

    // ── Computed clamp limits (tự suy từ data, không cần nhập tay) ──────────
    /// <summary>Giới hạn Pitch (X) của _targetRot. Tự tính từ pitch + clampMultiplier.</summary>
    public Vector2 MaxPitch
    {
        get
        {
            if (clampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            // Pitch thường âm (nòng ngóc lên). Lấy worst = giá trị tuyệt đối lớn nhất.
            // FIX: áp clampMultiplier đối xứng cả hai phía để clamp thực sự hoạt động
            // khi _targetRot.x tích lũy nhiều phát liên tiếp (đặc biệt chế độ Auto).
            float worstKick = Mathf.Max(Mathf.Abs(pitch.x), Mathf.Abs(pitch.y));
            float lo = -worstKick * clampMultiplier;
            float hi =  worstKick * clampMultiplier * 0.25f; // phía dương cho phép nhỏ hơn
            return new Vector2(lo, hi);
        }
    }

    /// <summary>Giới hạn Roll (Z) của _targetRot. Tự tính từ roll + extraRot.x + clampMultiplier.</summary>
    public Vector2 MaxRoll
    {
        get
        {
            if (clampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            float worstRoll = Mathf.Max(Mathf.Abs(roll.x), Mathf.Abs(roll.w)) * extraRot.x;
            return new Vector2(-worstRoll * clampMultiplier, worstRoll * clampMultiplier);
        }
    }

    /// <summary>Giới hạn Yaw (Y) của _targetRot. Tự tính từ yaw + extraRot.z + clampMultiplier.</summary>
    public Vector2 MaxYaw
    {
        get
        {
            if (clampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            float worstYaw = Mathf.Max(Mathf.Abs(yaw.x), Mathf.Abs(yaw.w)) * extraRot.z;
            return new Vector2(-worstYaw * clampMultiplier, worstYaw * clampMultiplier);
        }
    }

    // ── Computed translation clamp limits ────────────────────────────────────

    /// <summary>Giới hạn Kickback (Z) của _targetLoc. Tự tính từ kickback + extraLoc.z + locClampMultiplier.</summary>
    public Vector2 MaxKickback
    {
        get
        {
            if (locClampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            // kickback thường âm (lùi ra sau), worst case = kickback.x (cực âm nhất)
            float worstKb = Mathf.Min(kickback.x, kickback.y) * extraLoc.z;
            float lo = worstKb * locClampMultiplier;
            float hi = Mathf.Abs(worstKb) * 0.5f; // phía dương hiếm xảy ra
            return new Vector2(lo, hi);
        }
    }

    /// <summary>Giới hạn KickUp (Y) của _targetLoc. Tự tính từ kickUp + extraLoc.y + locClampMultiplier.</summary>
    public Vector2 MaxKickUp
    {
        get
        {
            if (locClampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            float worstKu = Mathf.Max(Mathf.Abs(kickUp.x), Mathf.Abs(kickUp.y)) * extraLoc.y;
            return new Vector2(-worstKu * locClampMultiplier, worstKu * locClampMultiplier);
        }
    }

    /// <summary>Giới hạn KickRight (X) của _targetLoc. Tự tính từ kickRight + extraLoc.x + locClampMultiplier.</summary>
    public Vector2 MaxKickRight
    {
        get
        {
            if (locClampMultiplier <= 0f) return new Vector2(float.MinValue, float.MaxValue);
            float worstKr = Mathf.Max(Mathf.Abs(kickRight.x), Mathf.Abs(kickRight.y)) * extraLoc.x;
            return new Vector2(-worstKr * locClampMultiplier, worstKr * locClampMultiplier);
        }
    }

    // ─────────────────────────────────────────────
    //  NOISE LAYER
    // ─────────────────────────────────────────────
    [Header("Noise Layer")]

    [Tooltip("Biên độ rung ngẫu nhiên theo chiều ngang (trục X) của lớp noise.\n" +
             "Lớp noise chạy bằng spring vật lý độc lập với recoil chính.\n\n" +
             "• X = giá trị nhỏ nhất (âm = rung sang trái)\n" +
             "• Y = giá trị lớn nhất (dương = rung sang phải)\n\n" +
             "Ví dụ: (-0.01, 0.01) tạo rung nhẹ đều hai phía.\n" +
             "Nhân thêm noiseScalar trước khi áp dụng. Đặt (0, 0) để tắt noise ngang.")]
    public Vector2 noiseX = new Vector2(-0.01f, 0.01f);

    [Tooltip("Biên độ rung ngẫu nhiên theo chiều dọc (trục Y) của lớp noise.\n\n" +
             "• X = giá trị nhỏ nhất (âm = rung xuống)\n" +
             "• Y = giá trị lớn nhất (dương = rung lên)\n\n" +
             "Ví dụ: (-0.01, 0.01) tạo rung nhẹ đều lên xuống.\n" +
             "Thường đặt bằng noiseX để noise trông tròn đều. Đặt (0, 0) để tắt noise dọc.")]
    public Vector2 noiseY = new Vector2(-0.01f, 0.01f);

    [Tooltip("Độ 'cứng' (acceleration) của spring vật lý điều khiển noise.\n" +
             "Giá trị cao → spring kéo về nhanh → noise rung tần số cao, co cụm.\n" +
             "Giá trị thấp → spring mềm → noise trôi chậm, lan rộng.\n\n" +
             "• X = acceleration theo chiều ngang (ví dụ: 8)\n" +
             "• Y = acceleration theo chiều dọc   (ví dụ: 12 → rung dọc nhanh hơn ngang)\n\n" +
             "Thường kết hợp với noiseDamp để đặt tần số và độ tắt dần của rung.")]
    public Vector2 noiseAccel = new Vector2(8f, 12f);

    [Tooltip("Độ giảm chấn (damping) của spring vật lý điều khiển noise.\n" +
             "Giá trị cao → rung tắt nhanh, ít dao động thừa → trông 'chắc'.\n" +
             "Giá trị thấp → rung dao động qua lại nhiều lần → trông 'lỏng lẻo'.\n\n" +
             "• X = damping theo chiều ngang (ví dụ: 9)\n" +
             "• Y = damping theo chiều dọc   (ví dụ: 9)\n\n" +
             "Critical damping ≈ 2 * sqrt(noiseAccel). Để rung tắt đẹp không dao động thừa,\n" +
             "thường đặt damping ≥ 2*sqrt(accel) (ví dụ: accel=9 → damp≥6).")]
    public Vector2 noiseDamp = new Vector2(9f, 9f);

    [Tooltip("Hệ số nhân cường độ toàn bộ lớp noise.\n\n" +
             "• 0   = tắt hoàn toàn lớp noise (súng hoàn toàn không rung)\n" +
             "• 1   = cường độ mặc định theo noiseX / noiseY\n" +
             "• > 1 = khuếch đại noise (ví dụ: 2 = rung mạnh gấp đôi)\n\n" +
             "Dùng slider này để nhanh chóng tinh chỉnh cảm giác rung mà không cần\n" +
             "chỉnh từng noiseX / noiseY riêng lẻ.")]
    [Range(0f, 5f)]
    public float noiseScalar = 1f;

    [Tooltip("Thời gian (giây) noise vẫn còn active sau phát bắn cuối.\n\n" +
             "Trong khoảng thời gian này, lớp noise tiếp tục chạy và tắt dần.\n" +
             "Sau khi hết thời gian, noise dừng hoàn toàn (tiết kiệm CPU khi đứng yên).\n\n" +
             "• 0   = noise tắt ngay lập tức sau khi ngừng bắn\n" +
             "• 0.5 = noise tắt sau 0.5 giây (mặc định, tạo cảm giác súng 'nguội' dần)\n" +
             "• > 1 = noise kéo dài lâu sau khi ngừng bắn\n\n" +
             "Không ảnh hưởng đến idle animation; chỉ kiểm soát noise recoil.")]
    public float idleNoiseDecayTime = 0.5f;

    // ─────────────────────────────────────────────
    //  PUSHBACK LAYER
    // ─────────────────────────────────────────────
    [Header("Pushback Layer")]

    [Tooltip("Lực đẩy camera (hoặc rig) ra sau mỗi phát bắn, mô phỏng cú giật vật lý.\n" +
             "Khác với Kickback (dịch chuyển súng): pushback tác dụng lên camera/góc nhìn.\n\n" +
             "• Âm = đẩy camera ra sau (cảm giác bị 'đá' ngược)\n" +
             "• Dương = kéo camera về phía trước (hiếm dùng)\n\n" +
             "Ví dụ: -0.4 tạo cú giật camera nhẹ mỗi phát.\n" +
             "Tăng độ âm (ví dụ -0.8) cho súng nặng, giảm (ví dụ -0.1) cho súng nhỏ.")]
    public float pushAmount = -0.4f;

    [Tooltip("Độ cứng spring phục hồi của lớp pushback (acceleration).\n" +
             "Quyết định camera bật trở lại nhanh hay chậm sau cú đẩy.\n\n" +
             "• Thấp (3–5)  = phục hồi chậm, cảm giác nặng nề\n" +
             "• Vừa (7–10)  = phục hồi nhanh vừa phải (mặc định)\n" +
             "• Cao (15+)   = phục hồi rất nhanh, gần như instant\n\n" +
             "Kết hợp với pushDamp để kiểm soát dao động sau khi phục hồi.\n" +
             "Phải > 0 (validation sẽ báo lỗi nếu ≤ 0).")]
    public float pushAccel = 7f;

    [Tooltip("Độ giảm chấn spring phục hồi của lớp pushback (damping).\n" +
             "Kiểm soát camera có rung dao động qua lại sau phục hồi hay không.\n\n" +
             "• Thấp (< 2*sqrt(pushAccel)) = underdamped → camera dao động qua lại\n" +
             "• = 2*sqrt(pushAccel)        = critically damped → phục hồi nhanh không dao động\n" +
             "• Cao (> 2*sqrt(pushAccel))  = overdamped → phục hồi chậm không dao động\n\n" +
             "Với pushAccel = 7, critical damp ≈ 5.3. Mặc định 7 cho phục hồi mượt.\n" +
             "Phải > 0 (validation sẽ báo lỗi nếu ≤ 0).")]
    public float pushDamp = 7f;

    // ─────────────────────────────────────────────
    //  MISC
    // ─────────────────────────────────────────────
    [Header("Misc")]

    [Tooltip("Bật/tắt smooth blending cho góc Roll giữa các frame.\n\n" +
             "• true  = roll được interpolate mượt theo smoothRot.Z, tránh nhảy đột ngột\n" +
             "• false = roll áp dụng trực tiếp không qua blend, sắc nét hơn nhưng có thể giật cục\n\n" +
             "Nên bật (true) cho hầu hết súng. Tắt nếu muốn roll snap ngay lập tức\n" +
             "hoặc đang debug giá trị roll thô.")]
    public bool smoothRollEnabled = true;

    [Tooltip("Hệ số tăng tốc độ phát toàn bộ animation curves recoil.\n\n" +
             "• 1.0 = tốc độ mặc định theo thời gian thực của curve\n" +
             "• > 1 = curve chạy nhanh hơn (ví dụ: 1.15 = nhanh hơn 15%) → giật sắc, phục hồi nhanh\n" +
             "• < 1 = curve chạy chậm hơn → giật mềm, phục hồi chậm\n\n" +
             "Dùng để tinh chỉnh nhịp điệu tổng thể của recoil mà không cần\n" +
             "chỉnh lại từng key trong từng curve. Phạm vi hợp lệ: 0.1 – 3.0.")]
    [Range(0.1f, 3f)]
    public float playRate = 1.15f;

    // ─────────────────────────────────────────────
    //  CAMERA SHAKE (dùng bởi CameraRecoil, tách biệt với GunRecoilController)
    // ─────────────────────────────────────────────
    [Header("Camera Shake — CameraRecoil")]

    [Tooltip("Độ giật camera theo trục X (pitch) mỗi phát bắn — dùng bởi CameraRecoil.\n\n" +
             "Khác với 'pitch' ở trên (áp cho weapon model qua GunRecoilController),\n" +
             "giá trị này áp trực tiếp lên camera chính, thường nhỏ hơn nhiều.\n" +
             "Không random — cộng dồn cố định mỗi phát (giống recoilX gốc của CameraRecoil).")]
    public float camRecoilX = 2f;

    [Tooltip("Biên độ giật camera ngẫu nhiên theo trục Y (yaw) mỗi phát bắn.\n" +
             "CameraRecoil random trong khoảng [-camRecoilY, camRecoilY].")]
    public float camRecoilY = 1f;

    [Tooltip("Biên độ giật camera ngẫu nhiên theo trục Z (roll) mỗi phát bắn.\n" +
             "CameraRecoil random trong khoảng [-camRecoilZ, camRecoilZ].")]
    public float camRecoilZ = 1f;

    [Tooltip("Tốc độ camera 'bắt kịp' targetRotation (Slerp) — giá trị càng cao càng snap nhanh.\n" +
             "Tương đương field 'snappiness' gốc của CameraRecoil.")]
    public float camSnappiness = 10f;

    [Tooltip("Tốc độ targetRotation quay về 0 (Lerp) — giá trị càng cao càng hồi nhanh.\n" +
             "Tương đương field 'returnSpeed' gốc của CameraRecoil.")]
    public float camReturnSpeed = 8f;

    [Tooltip("Hệ số giảm giật camera khi đang ADS (aim), 0–1.\n" +
             "0 = không giảm gì khi ADS, 1 = triệt tiêu hoàn toàn giật camera khi ADS.\n" +
             "Tương đương field 'weightAirm' gốc của CameraRecoil.")]
    [Range(0f, 1f)]
    public float camAimWeight = 0.6f;

    // ─────────────────────────────────────────────
    //  RECOIL CURVES
    // ─────────────────────────────────────────────
    [Header("Recoil Curves — Semi Rotation")]

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Pitch (X) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây, từ 0 đến SemiCurveDuration).\n" +
             "Trục dọc   = hệ số lerp (0 = không di chuyển, 1 = snap ngay đến target).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại giá trị 0.\n" +
             "Đỉnh curve (peak) xác định thời điểm và tốc độ kick lớn nhất.\n" +
             "Ví dụ: peak sớm (t=0.1) → kick nhanh; peak muộn (t=0.4) → kick từ từ.")]
    public AnimationCurve semiRotX = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Roll (Y được map sang Z) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Thường để curve này giống semiRotX hoặc hơi chậm hơn để roll lag sau pitch.")]
    public AnimationCurve semiRotY = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Yaw (Z) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Yaw nhỏ → curve thấp hơn Pitch là bình thường.")]
    public AnimationCurve semiRotZ = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Header("Recoil Curves — Semi Location")]

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo X (KickRight) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Nếu kickRight = (0,0), curve này không có tác dụng thực tế.")]
    public AnimationCurve semiLocX = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo Y (KickUp) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Peak sớm → súng nảy lên nhanh rồi rơi xuống; peak muộn → nảy chậm.")]
    public AnimationCurve semiLocY = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo Z (Kickback) cho chế độ Semi.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Thường có peak ngắn hơn semiLocY để kickback cảm giác đột ngột hơn nảy lên.")]
    public AnimationCurve semiLocZ = AnimationCurve.EaseInOut(0f, 0f, 0.65f, 0f);

    [Header("Recoil Curves — Auto Rotation")]

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Pitch (X) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây, từ 0 đến AutoCurveDuration = thường 1.0s).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.\n" +
             "Auto curve dài hơn semi để giật chồng lên nhau qua nhiều phát liên tiếp.\n" +
             "Peak thấp hơn semi (ít 'snap') để recoil tích lũy trông mượt hơn.")]
    public AnimationCurve autoRotX = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Roll (Y) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.")]
    public AnimationCurve autoRotY = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp xoay theo Yaw (Z) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.")]
    public AnimationCurve autoRotZ = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Header("Recoil Curves — Auto Location")]

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo X (KickRight) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.")]
    public AnimationCurve autoLocX = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo Y (KickUp) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.")]
    public AnimationCurve autoLocY = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    [Tooltip("Curve điều khiển tốc độ lerp tịnh tiến theo Z (Kickback) cho chế độ Auto/Burst.\n\n" +
             "Trục ngang = thời gian (giây).\n" +
             "Trục dọc   = hệ số lerp (0–1).\n\n" +
             "QUY TẮC BẮT BUỘC: curve PHẢI bắt đầu VÀ kết thúc tại 0.")]
    public AnimationCurve autoLocZ = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    // ─────────────────────────────────────────────
    //  MODE HELPERS
    // ─────────────────────────────────────────────

    /// <summary>Kiểm tra mode có được phép trên khẩu súng này không.</summary>
    public bool IsModeAllowed(AllowedModes mode) => (allowedModes & mode) != 0;

    /// <summary>
    /// Lấy mode đầu tiên được phép theo thứ tự Semi → Auto → Burst.
    /// Dùng để chọn mode mặc định khi khởi tạo.
    /// </summary>
    public AllowedModes FirstAllowedMode()
    {
        if (IsModeAllowed(AllowedModes.Semi))  return AllowedModes.Semi;
        if (IsModeAllowed(AllowedModes.Auto))  return AllowedModes.Auto;
        if (IsModeAllowed(AllowedModes.Melee)) return AllowedModes.Melee;
        return AllowedModes.Burst;
    }

    /// <summary>Lấy end time của semi curves (key cuối cùng).</summary>
    public float SemiCurveDuration =>
        Mathf.Max(
            semiRotX.keys[semiRotX.length - 1].time,
            semiLocX.keys[semiLocX.length - 1].time);

    /// <summary>Lấy end time của auto curves.</summary>
    public float AutoCurveDuration =>
        Mathf.Max(
            autoRotX.keys[autoRotX.length - 1].time,
            autoLocX.keys[autoLocX.length - 1].time);

    // ─────────────────────────────────────────────
    //  VALIDATION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Collect toàn bộ vấn đề của asset vào một list.
    /// Trả về true nếu không có lỗi nào (issues rỗng).
    /// Gọi từ Editor button hoặc runtime Awake.
    /// </summary>
    public bool ValidateAll(out System.Collections.Generic.List<string> issues, float semiFireDelay = 0.25f, float autoFireDelay = 0.1f)
    {
        issues = new System.Collections.Generic.List<string>();

        // ── Allowed modes ──────────────────────────────────────────────
        if (allowedModes == 0)
            issues.Add("[AllowedModes] Phải có ít nhất một mode được tick.");

        if (IsModeAllowed(AllowedModes.Burst) && BurstCount < 2)
            issues.Add($"[BurstCount] Burst mode được bật nhưng BurstCount = {BurstCount} (khuyến nghị ≥ 2).");

        // ── Rotation targets ───────────────────────────────────────────
        if (pitch.x > pitch.y)
            issues.Add($"[Pitch] min (X={pitch.x}) phải ≤ max (Y={pitch.y}).");
        if (pitch.x >= 0f && pitch.y >= 0f)
            issues.Add($"[Pitch] Cả hai giá trị đều dương ({pitch.x}, {pitch.y}). " +
                       "Pitch thường âm để nòng súng ngóc lên. Kiểm tra lại. " +
                       "Khuyến nghị: Pistol = (-8, -3), Rifle = (-4, -2), Shotgun = (-12, -5), Sniper = (-15, -6).");

        if (roll.x > roll.y)
            issues.Add($"[Roll] min range: X ({roll.x}) phải ≤ Y ({roll.y}).");
        if (roll.z > roll.w)
            issues.Add($"[Roll] max range: Z ({roll.z}) phải ≤ W ({roll.w}).");

        if (yaw.x > yaw.y)
            issues.Add($"[Yaw] min range: X ({yaw.x}) phải ≤ Y ({yaw.y}).");
        if (yaw.z > yaw.w)
            issues.Add($"[Yaw] max range: Z ({yaw.z}) phải ≤ W ({yaw.w}).");

        // ── Translation targets ────────────────────────────────────────
        if (kickback.x > kickback.y)
            issues.Add($"[Kickback] min (X={kickback.x}) phải ≤ max (Y={kickback.y}).");
        if (kickback.x >= 0f && kickback.y >= 0f)
            issues.Add($"[Kickback] Cả hai giá trị đều dương ({kickback.x}, {kickback.y}). " +
                       "Kickback thường âm (súng lùi ra sau). Kiểm tra lại. " +
                       "Khuyến nghị: Pistol = (-0.05, -0.02), Rifle = (-0.03, -0.015), " +
                       "Shotgun = (-0.10, -0.05), Sniper = (-0.06, -0.03).");

        if (kickUp.x > kickUp.y)
            issues.Add($"[KickUp] min (X={kickUp.x}) phải ≤ max (Y={kickUp.y}).");

        if (kickRight.x > kickRight.y)
            issues.Add($"[KickRight] min (X={kickRight.x}) phải ≤ max (Y={kickRight.y}).");

        // ── Aim multipliers ────────────────────────────────────────────
        if (aimRotMultiplier.x < 0f || aimRotMultiplier.y < 0f || aimRotMultiplier.z < 0f)
            issues.Add($"[AimRotMultiplier] Không nên âm ({aimRotMultiplier}). " +
                       "Giá trị âm sẽ đảo chiều giật khi ADS. " +
                       "Khuyến nghị: X (Pitch) = 0.3–0.6, Y (Roll) = 0.2–0.5, Z (Yaw) = 0.3–0.6. " +
                       "Sniper: X = 0.1–0.3. Đặt = 1.0 nếu muốn giữ nguyên như bắn từ hông.");
        if (aimLocMultiplier.x < 0f || aimLocMultiplier.y < 0f || aimLocMultiplier.z < 0f)
            issues.Add($"[AimLocMultiplier] Không nên âm ({aimLocMultiplier}). " +
                       "Giá trị âm sẽ đảo chiều tịnh tiến khi ADS. " +
                       "Khuyến nghị: X (KickRight) = 0.3–0.7, Y (KickUp) = 0.4–0.7, Z (Kickback) = 0.5–0.8. " +
                       "Đặt = 1.0 để giữ nguyên không giảm khi ADS.");

        // ── Smooth / damp ──────────────────────────────────────────────
        if (smoothRot.x <= 0f)
            issues.Add($"[SmoothRot.X] Phải > 0 (hiện tại: {smoothRot.x}). Dùng cho Semi decay. " +
                       "Khuyến nghị: Pistol/Sniper = 12–20, Rifle/SMG = 10–15, Shotgun = 6–12.");
        if (smoothRot.y <= 0f)
            issues.Add($"[SmoothRot.Y] Phải > 0 (hiện tại: {smoothRot.y}). Dùng cho Auto decay. " +
                       "Khuyến nghị: 9–25 — thấp hơn SmoothRot.X để giật Auto cộng dồn tự nhiên.");
        if (smoothRot.z <= 0f)
            issues.Add($"[SmoothRot.Z] Phải > 0 (hiện tại: {smoothRot.z}). Dùng cho Roll smooth. " +
                       "Khuyến nghị: 3–8 (thường thấp hơn SmoothRot.X để roll trôi mượt hơn pitch).");

        if (smoothLoc.x <= 0f)
            issues.Add($"[SmoothLoc.X] Phải > 0 (hiện tại: {smoothLoc.x}). Dùng cho Semi loc decay. " +
                       "Khuyến nghị: 1–5 nếu muốn cảm giác nặng; >= 10 nếu cần tránh tích lũy kickback.");
        if (smoothLoc.y <= 0f)
            issues.Add($"[SmoothLoc.Y] Phải > 0 (hiện tại: {smoothLoc.y}). Dùng cho Auto loc decay. " +
                       "Khuyến nghị: 18–30 để kickback Auto không tích lũy ở 600 RPM (0.1s/phát).");
        if (smoothLoc.z <= 0f)
            issues.Add($"[SmoothLoc.Z] Phải > 0 (hiện tại: {smoothLoc.z}). " +
                       "Khuyến nghị: 30–60 (dự phòng, ít ảnh hưởng gameplay).");

        if (pushAccel <= 0f)
            issues.Add($"[PushAccel] Phải > 0 (hiện tại: {pushAccel}). " +
                       "Khuyến nghị: 3–5 (phục hồi chậm/nặng nề), 7–10 (vừa phải), 15+ (rất nhanh).");
        if (pushDamp <= 0f)
            issues.Add($"[PushDamp] Phải > 0 (hiện tại: {pushDamp}). " +
                       $"Khuyến nghị: >= 2×√pushAccel để tránh dao động. " +
                       $"Với pushAccel={pushAccel:F1}: PushDamp >= {2f * Mathf.Sqrt(Mathf.Max(pushAccel,0.01f)):F1}.");

        // ── Pushback underdamped warning ───────────────────────────────
        if (pushAccel > 0f && pushDamp > 0f)
        {
            float critDamp = 2f * Mathf.Sqrt(pushAccel);
            if (pushDamp < critDamp * 0.5f)
                issues.Add($"[PushDamp] Có thể underdamped nặng (pushDamp={pushDamp:F1}, " +
                           $"critical≈{critDamp:F1}). Camera có thể dao động qua lại nhiều lần. " +
                           $"Khuyến nghị: PushDamp >= {critDamp * 0.5f:F1} (tối thiểu, ít dao động), " +
                           $">= {critDamp:F1} (critically damped, không dao động), " +
                           $">= {critDamp * 1.5f:F1} (overdamped, phục hồi mượt chậm hơn).");
        }

        // ── Noise ──────────────────────────────────────────────────────
        if (idleNoiseDecayTime < 0f)
            issues.Add($"[IdleNoiseDecayTime] Phải ≥ 0 (hiện tại: {idleNoiseDecayTime}). " +
                       "Khuyến nghị: 0 (noise tắt ngay), 0.3–0.5 (fade-out nhẹ, mặc định), 1.0 (kéo dài). " +
                       "Sửa: đặt giá trị >= 0.");
        if (noiseScalar > 0f)
        {
            if (noiseAccel.x <= 0f || noiseAccel.y <= 0f)
                issues.Add($"[NoiseAccel] Phải > 0 khi noiseScalar > 0 (hiện tại: {noiseAccel}). " +
                           "Khuyến nghị: X (ngang) = 6–10, Y (dọc) = 10–15. " +
                           "Tăng để noise rung nhanh/co cụm; giảm để noise trôi chậm/lan rộng.");
            if (noiseDamp.x <= 0f || noiseDamp.y <= 0f)
                issues.Add($"[NoiseDamp] Phải > 0 khi noiseScalar > 0 (hiện tại: {noiseDamp}). " +
                           $"Khuyến nghị: X >= {2f * Mathf.Sqrt(Mathf.Max(noiseAccel.x, 0.01f)):F1} " +
                           $"(với noiseAccel.X={noiseAccel.x:F1}), " +
                           $"Y >= {2f * Mathf.Sqrt(Mathf.Max(noiseAccel.y, 0.01f)):F1} " +
                           $"(với noiseAccel.Y={noiseAccel.y:F1}). " +
                           "Đặt >= 2×√noiseAccel để tắt dao động thừa.");
        }

        // ── Camera shake ───────────────────────────────────────────────
        if (camSnappiness <= 0f)
            issues.Add($"[CamSnappiness] Phải > 0 (hiện tại: {camSnappiness}). " +
                       "Khuyến nghị: 6–10 (mượt) đến 15–20 (nhanh/snap). Giá trị thấp = camera bắt kịp chậm hơn.");
        if (camReturnSpeed <= 0f)
            issues.Add($"[CamReturnSpeed] Phải > 0 (hiện tại: {camReturnSpeed}). " +
                       "Khuyến nghị: 5–10 (hồi chậm, cảm giác nặng) đến 15–25 (hồi nhanh, cảm giác nhẹ).");
        if (camAimWeight < 0f || camAimWeight > 1f)
            issues.Add($"[CamAimWeight] Phải nằm trong [0, 1] (hiện tại: {camAimWeight:F2}). " +
                       "Khuyến nghị: 0.4–0.7 — giảm giật camera khi ADS nhưng không triệt tiêu hoàn toàn.");

        // ── PlayRate ───────────────────────────────────────────────────
        if (playRate <= 0f)
            issues.Add($"[PlayRate] Phải > 0 (hiện tại: {playRate}). " +
                       "Khuyến nghị: 0.8–1.0 (mềm/chậm), 1.0–1.3 (mặc định), 1.5–2.0 (sắc/nhanh). " +
                       "Giá trị quá nhỏ (< 0.1) sẽ làm animation recoil rất chậm và không phản hồi.");

        // ── Clamp multipliers ──────────────────────────────────────────
        if (clampMultiplier < 0f)
            issues.Add($"[ClampMultiplier] Không nên âm (hiện tại: {clampMultiplier}). " +
                       "Khuyến nghị: 0 (tắt clamp), 1–2 (chặt — chặn ngay sau 1–2 phát), " +
                       "3–5 (vừa phải, mặc định — cho phép tích lũy vài phát trước khi chặn), " +
                       "10+ (rất rộng — gần như không giới hạn). Đặt = 0 để âm.");
        if (locClampMultiplier <= 0f)
            issues.Add($"[LocClampMultiplier] Nên > 0 để tránh vũ khí bay khỏi map (hiện tại: {locClampMultiplier}). " +
                       "Khuyến nghị: 1–3 (chặt, an toàn — vũ khí không bị đẩy quá xa), " +
                       "3–5 (vừa phải — có khoảng cộng dồn nhỏ), " +
                       "0 = TẮT HOÀN TOÀN (nguy hiểm, vũ khí có thể xuyên qua map/camera).");

        // ── Curves ────────────────────────────────────────────────────
        AnimationCurve[] allCurves =
        {
            semiRotX, semiRotY, semiRotZ,
            semiLocX, semiLocY, semiLocZ,
            autoRotX, autoRotY, autoRotZ,
            autoLocX, autoLocY, autoLocZ
        };
        string[] curveNames =
        {
            "SemiRotX","SemiRotY","SemiRotZ",
            "SemiLocX","SemiLocY","SemiLocZ",
            "AutoRotX","AutoRotY","AutoRotZ",
            "AutoLocX","AutoLocY","AutoLocZ"
        };

        for (int i = 0; i < allCurves.Length; i++)
        {
            var curve = allCurves[i];
            string cName = curveNames[i];

            if (curve == null || curve.length == 0)
            {
                issues.Add($"[{cName}] Curve rỗng — không có key nào.");
                continue;
            }

            float startVal = curve.Evaluate(curve.keys[0].time);
            float endVal   = curve.Evaluate(curve.keys[curve.length - 1].time);

            if (Mathf.Abs(startVal) > 0.001f)
                issues.Add($"[{cName}] Phải bắt đầu tại 0 (hiện tại: {startVal:F4}). " +
                           "Curve bắt đầu ≠ 0 sẽ gây giật tức thời khi phát đầu tiên. " +
                           "Sửa: chọn key đầu tiên trong Curve Editor, đặt Value = 0.");
            if (Mathf.Abs(endVal) > 0.001f)
                issues.Add($"[{cName}] Phải kết thúc tại 0 (hiện tại: {endVal:F4}). " +
                           "Curve kết thúc ≠ 0 sẽ giữ súng ở vị trí lệch sau khi hết animation. " +
                           "Sửa: chọn key cuối cùng trong Curve Editor, đặt Value = 0.");

            // Curve là hệ số lerp → PHẢI nằm trong [0,1] xuyên suốt.
            // Nếu âm: Lerp clamp về 0 → súng đứng khựng. Nếu > 1: Lerp clamp về 1 → snap cứng.
            float startTime  = curve.keys[0].time;
            float endTime    = curve.keys[curve.length - 1].time;
            const int sampleCount = 30;
            float firstOOB = float.NaN;
            float firstOOBVal = 0f;
            for (int s = 0; s <= sampleCount; s++)
            {
                float t = Mathf.Lerp(startTime, endTime, s / (float)sampleCount);
                float v = curve.Evaluate(t);
                if (v < -0.01f || v > 1.01f)
                {
                    if (float.IsNaN(firstOOB)) { firstOOB = t; firstOOBVal = v; }
                }
            }
            if (!float.IsNaN(firstOOB))
                issues.Add($"[{cName}] Curve vượt ngoài [0,1] — lần đầu tại t={firstOOB:F3} " +
                           $"(giá trị: {firstOOBVal:F3}). Curve là hệ số lerp; nếu âm → súng đứng " +
                           $"khựng rồi giật bù đột ngột. Nếu > 1 → snap cứng vượt quá target. " +
                           "Sửa: trong Curve Editor chọn tất cả key → Right-Click → 'Clamp' hoặc " +
                           "điều chỉnh tay để toàn bộ đường cong nằm trong dải [0, 1].");
        }

        // ── Accumulation simulation ────────────────────────────────────────
        // Simulate N phát bắn liên tiếp với fireDelay điển hình để kiểm tra
        // xem smooth decay có kịp kéo _targetRot/_targetLoc về trước phát tiếp theo không.
        // Nếu không → tích lũy vô hạn → súng xoay vòng / bay ra ngoài map.
        //
        // Mô hình toán: mỗi bước dt, decay chạy Lerp(target, 0, smoothX * dt).
        // Tương đương: target *= (1 - smoothX * dt) per frame (xấp xỉ tuyến tính).
        // Sau fireDelay giây: remainFactor ≈ exp(-smooth * fireDelay) [continuous].
        // Ta simulate discrete theo frameRate để sát với runtime thực tế hơn.
        CheckAccumulationRot(issues, semiFireDelay, autoFireDelay);
        CheckAccumulationLoc(issues, semiFireDelay, autoFireDelay);

        return issues.Count == 0;
    }

    // ─────────────────────────────────────────────
    //  ACCUMULATION SIMULATION HELPERS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra xem rotation (_targetRot) có bị cộng dồn không kiểm soát không.
    /// Simulate vòng bắn Auto/Semi điển hình ở 60 fps.
    /// </summary>
    private void CheckAccumulationRot(System.Collections.Generic.List<string> issues, float semiFireDelay, float autoFireDelay)
    {
        // ── Thông số simulate ─────────────────────────────────────────────────
        // fireDelay: khoảng cách giữa 2 phát — dùng giá trị thực tế phổ biến.
        // Semi: ~0.25s (4 shots/s bắn nhanh), Auto: ~0.1s (600 RPM).
        // Clamp multiplier tự tính từ data → kết quả giữa clamp và không clamp.

        const int   simShots    = 20;   // số phát simulate
        const float fps         = 60f;
        const float dt          = 1f / fps;

        // ── SEMI (smoothRot.x, fireDelay = 0.25s) ─────────────────────────
        {
            float fireDelay = semiFireDelay;
            float smooth = smoothRot.x;
            float pitchKick = pitch.x; // worst case: giá trị cực đại

            float target   = 0f;
            float peakSeen = 0f;
            for (int shot = 0; shot < simShots; shot++)
            {
                // Kick
                target += pitchKick;

                // Decay trong fireDelay giây @ fps
                int frames = Mathf.RoundToInt(fireDelay / dt);
                for (int f = 0; f < frames; f++)
                    target = Mathf.Lerp(target, 0f, smooth * dt);

                peakSeen = Mathf.Max(peakSeen, Mathf.Abs(target));
            }

            // Tính steady-state thực: sau N phát nếu |target| vẫn tăng → không ổn định
            // Ngưỡng: nếu sau 20 phát tích lũy > clampMultiplier * |pitchKick| * 2 thì cảnh báo
            float safeThreshold = Mathf.Abs(pitchKick) * Mathf.Max(clampMultiplier, 1f);
            float decayRatio    = DecayRatioPerShot(smooth, fireDelay, fps);

            if (decayRatio > 0.85f) // chỉ decay được < 15% mỗi phát thì tích lũy rất nhanh
            {
                // Tính smooth tối thiểu để decayRatio <= 0.85
                float minSmoothForSemiPitch = 0f;
                for (float s = 0.1f; s <= 50f; s += 0.1f)
                {
                    if (DecayRatioPerShot(s, fireDelay, fps) <= 0.85f)
                    {
                        minSmoothForSemiPitch = s;
                        break;
                    }
                }
                // Tính pitch max an toàn với smooth hiện tại (decayRatio <= 0.85 → không apply nếu smooth đủ)
                issues.Add(
                    $"[Accumulation | Semi | Pitch] SmoothRot.X ({smooth:F1}) quá thấp so với " +
                    $"fireDelay {fireDelay:F2}s — chỉ decay được {(1f-decayRatio)*100f:F0}% " +
                    $"giữa 2 phát. Sau {simShots} phát _targetRot.X tích lũy ~{peakSeen:F1}° " +
                    $"(ngưỡng an toàn ≈ {safeThreshold:F1}°). " +
                    "Tăng SmoothRot.X hoặc giảm Pitch để tránh súng xoay vòng. " +
                    $"Khuyến nghị: SmoothRot.X >= {minSmoothForSemiPitch:F1} " +
                    $"(để decay >= 15%/phát tại fireDelay={fireDelay:F2}s). " +
                    $"Hoặc giảm |Pitch.X| <= {Mathf.Abs(pitchKick) * (1f - decayRatio) / (decayRatio > 0f ? decayRatio : 0.01f):F2}° " +
                    $"(nếu giữ SmoothRot.X={smooth:F1}).");
            }
        }

        // ── AUTO (smoothRot.y, fireDelay = 0.1s = 600 RPM) ──────────────
        if (IsModeAllowed(AllowedModes.Auto))
        {
            float fireDelay = autoFireDelay;
            float smooth = smoothRot.y;
            // Auto dùng extraRot nhân thêm
            float pitchKick = pitch.x; // pitch không nhân extraRot theo trục X trong code

            float target   = 0f;
            float peakSeen = 0f;
            bool  growing  = false;
            float prevAbs  = 0f;

            for (int shot = 0; shot < simShots; shot++)
            {
                target += pitchKick;

                int frames = Mathf.RoundToInt(fireDelay / dt);
                for (int f = 0; f < frames; f++)
                    target = Mathf.Lerp(target, 0f, smooth * dt);

                float curAbs = Mathf.Abs(target);
                if (shot > 0 && curAbs > prevAbs + 0.01f) growing = true;
                peakSeen = Mathf.Max(peakSeen, curAbs);
                prevAbs  = curAbs;
            }

            float decayRatio = DecayRatioPerShot(smooth, fireDelay, fps);

            if (growing)
            {
                // Tính SmoothRot.Y tối thiểu để Auto Pitch không growing
                float minSmoothAutoP = 0f;
                for (float s = 0.5f; s <= 100f; s += 0.5f)
                {
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest += pitchKick;
                        int fr = Mathf.RoundToInt(fireDelay / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, s * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.01f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { minSmoothAutoP = s; break; }
                }
                // Tính pitch max an toàn với SmoothRot.Y hiện tại
                float maxSafePitchAuto = 0f;
                for (float pm = 0.1f; pm <= 20f; pm += 0.1f)
                {
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest -= pm;
                        int fr = Mathf.RoundToInt(fireDelay / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, smooth * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.01f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { maxSafePitchAuto = pm; break; }
                }
                issues.Add(
                    $"[Accumulation | Auto | Pitch] _targetRot.X vẫn tăng liên tục sau {simShots} " +
                    $"phát Auto (fireDelay={fireDelay:F2}s, SmoothRot.Y={smooth:F1}). " +
                    $"Decay chỉ xử lý được {(1f-decayRatio)*100f:F0}% per phát — " +
                    $"tích lũy đạt ~{peakSeen:F1}° và sẽ tiếp tục leo. " +
                    $"Clamp MaxPitch={MaxPitch.x:F1}° sẽ chặn nhưng súng sẽ BỊ KHOÁ ở góc đó. " +
                    "Tăng SmoothRot.Y để decay kịp giữa các phát. " +
                    $"Khuyến nghị: SmoothRot.Y >= {minSmoothAutoP:F1} " +
                    $"(với pitch={pitchKick:F2}°, fireDelay={fireDelay:F2}s). " +
                    $"Hoặc |Pitch.X| <= {maxSafePitchAuto:F2}° (nếu giữ SmoothRot.Y={smooth:F1}).");
            }

            // Yaw tích lũy khi Auto (nhân extraRot.z)
            float yawKick = Mathf.Max(Mathf.Abs(yaw.x), Mathf.Abs(yaw.w)) * extraRot.z;
            if (yawKick > 0f)
            {
                float yTarget   = 0f;
                float yPeakSeen = 0f;
                bool  yGrowing  = false;
                float yPrevAbs  = 0f;

                for (int shot = 0; shot < simShots; shot++)
                {
                    yTarget += yawKick;
                    int frames = Mathf.RoundToInt(fireDelay / dt);
                    for (int f = 0; f < frames; f++)
                        yTarget = Mathf.Lerp(yTarget, 0f, smooth * dt);

                    float yAbs = Mathf.Abs(yTarget);
                    if (shot > 0 && yAbs > yPrevAbs + 0.01f) yGrowing = true;
                    yPeakSeen = Mathf.Max(yPeakSeen, yAbs);
                    yPrevAbs  = yAbs;
                }

                if (yGrowing)
                {
                    // Tính SmoothRot.Y min để Yaw không growing
                    float minSmoothYaw = 0f;
                    for (float s = 0.5f; s <= 100f; s += 0.5f)
                    {
                        float yT = 0f, yP = 0f; bool yG = false;
                        for (int sh = 0; sh < simShots; sh++)
                        {
                            yT += yawKick;
                            int fr = Mathf.RoundToInt(fireDelay / dt);
                            for (int ff = 0; ff < fr; ff++) yT = Mathf.Lerp(yT, 0f, s * dt);
                            float ya = Mathf.Abs(yT);
                            if (sh > 0 && ya > yP + 0.01f) { yG = true; break; }
                            yP = ya;
                        }
                        if (!yG) { minSmoothYaw = s; break; }
                    }
                    // Tính ExtraRot.Z max an toàn (giữ smooth và yaw.w hiện tại)
                    float maxSafeExtraRotZ = 0f;
                    float yawWMax = Mathf.Max(Mathf.Abs(yaw.x), Mathf.Abs(yaw.w));
                    for (float erz = 0.5f; erz <= 20f; erz += 0.5f)
                    {
                        float kickT = yawWMax * erz;
                        float yT = 0f, yP = 0f; bool yG = false;
                        for (int sh = 0; sh < simShots; sh++)
                        {
                            yT += kickT;
                            int fr = Mathf.RoundToInt(fireDelay / dt);
                            for (int ff = 0; ff < fr; ff++) yT = Mathf.Lerp(yT, 0f, smooth * dt);
                            float ya = Mathf.Abs(yT);
                            if (sh > 0 && ya > yP + 0.01f) { yG = true; break; }
                            yP = ya;
                        }
                        if (!yG) { maxSafeExtraRotZ = erz; break; }
                    }
                    // Tính Yaw.W max an toàn (giữ smooth và extraRot.z hiện tại)
                    float maxSafeYawW = 0f;
                    for (float yw = 0.1f; yw <= 10f; yw += 0.1f)
                    {
                        float kickT = yw * extraRot.z;
                        float yT = 0f, yP = 0f; bool yG = false;
                        for (int sh = 0; sh < simShots; sh++)
                        {
                            yT += kickT;
                            int fr = Mathf.RoundToInt(fireDelay / dt);
                            for (int ff = 0; ff < fr; ff++) yT = Mathf.Lerp(yT, 0f, smooth * dt);
                            float ya = Mathf.Abs(yT);
                            if (sh > 0 && ya > yP + 0.01f) { yG = true; break; }
                            yP = ya;
                        }
                        if (!yG) { maxSafeYawW = yw; break; }
                    }
                    issues.Add(
                        $"[Accumulation | Auto | Yaw] _targetRot.Y (Yaw × ExtraRot.Z = {yawKick:F2}°/phát) " +
                        $"tích lũy liên tục sau {simShots} phát. Đạt ~{yPeakSeen:F1}° — " +
                        $"súng xoay ngang không kiểm soát. " +
                        $"Giảm Yaw ({yaw.w:F2}), ExtraRot.Z ({extraRot.z:F1}), hoặc tăng SmoothRot.Y ({smooth:F1}). " +
                        $"Khuyến nghị: " +
                        $"(A) SmoothRot.Y >= {minSmoothYaw:F0} " +
                        $"(với YawKick={yawKick:F2}°/phát, fireDelay={fireDelay:F2}s). " +
                        (maxSafeExtraRotZ > 0f ? $"(B) ExtraRot.Z <= {maxSafeExtraRotZ:F1} (giữ SmoothRot.Y={smooth:F1}, Yaw.W={yaw.w:F2}). " : "") +
                        (maxSafeYawW > 0f ? $"(C) Yaw.W <= {maxSafeYawW:F2}° (giữ SmoothRot.Y={smooth:F1}, ExtraRot.Z={extraRot.z:F1})." : ""));
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra location (_targetLoc) có tích lũy không kiểm soát không.
    /// </summary>
    private void CheckAccumulationLoc(System.Collections.Generic.List<string> issues, float semiFireDelay, float autoFireDelay)
    {
        const int   simShots    = 20;
        const float fps         = 60f;
        const float dt          = 1f / fps;

        // ── SEMI kickback (smoothLoc.x, fireDelay = 0.25s) ────────────────
        {
            float fireDelay = semiFireDelay;
            float smooth    = smoothLoc.x;
            float kbKick    = kickback.x; // worst case (cực âm nhất, = xa nhất)

            float target   = 0f;
            float peakSeen = 0f;
            bool  growing  = false;
            float prevAbs  = 0f;

            for (int shot = 0; shot < simShots; shot++)
            {
                target += kbKick;
                int frames = Mathf.RoundToInt(fireDelay / dt);
                for (int f = 0; f < frames; f++)
                    target = Mathf.Lerp(target, 0f, smooth * dt);

                float curAbs = Mathf.Abs(target);
                if (shot > 0 && curAbs > prevAbs + 0.0001f) growing = true;
                peakSeen = Mathf.Max(peakSeen, curAbs);
                prevAbs  = curAbs;
            }

            if (growing)
            {
                // Tính SmoothLoc.X tối thiểu để không growing
                float minSmoothLocX = 0f;
                for (float s = 0.1f; s <= 100f; s += 0.1f)
                {
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest += kbKick;
                        int fr = Mathf.RoundToInt(fireDelay / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, s * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.0001f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { minSmoothLocX = s; break; }
                }
                // Tính fireDelay tối thiểu để không growing (nếu muốn giữ smooth thấp)
                float minFireDelayForSafe = 0f;
                for (float fd = 0.1f; fd <= 10f; fd += 0.1f)
                {
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest += kbKick;
                        int fr = Mathf.RoundToInt(fd / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, smooth * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.0001f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { minFireDelayForSafe = fd; break; }
                }
                issues.Add(
                    $"[Accumulation | Semi | Kickback] _targetLoc.Z tích lũy liên tục " +
                    $"(kickback={kbKick:F4}m/phát, SmoothLoc.X={smooth:F1}, fireDelay={semiFireDelay:F3}s). " +
                    $"Sau {simShots} phát: ~{peakSeen:F4}m — vũ khí bị đẩy ra xa khỏi tay. " +
                    $"LocClampMultiplier={locClampMultiplier:F1} sẽ chặn tại {MaxKickback.x:F4}m nhưng " +
                    "súng sẽ bị khoá ở vị trí đó. Tăng SmoothLoc.X hoặc giảm Kickback. " +
                    $"Khuyến nghị: " +
                    $"(A) SmoothLoc.X >= {minSmoothLocX:F1} " +
                    $"(với kickback={kbKick:F4}m, fireDelay={semiFireDelay:F3}s). " +
                    (minFireDelayForSafe > 0f && minFireDelayForSafe < 5f
                        ? $"(B) Giảm tốc độ bắn: Semi fireDelay >= {minFireDelayForSafe:F1}s " +
                          $"(≈{60f/minFireDelayForSafe:F0} RPM) nếu giữ SmoothLoc.X={smooth:F1}. "
                        : "(B) SmoothLoc.X=1.1 quá thấp — bất kỳ kickback nào cũng growing ở fireDelay này. ") +
                    $"(C) Compromise: SmoothLoc.X = {minSmoothLocX * 0.5f:F1}, " +
                    $"Kickback.X = {kbKick * 0.5f:F4}m (giảm cả hai để giữ feel nặng).");
            }
        }

        // ── AUTO kickback (smoothLoc.y, fireDelay = 0.1s) ─────────────────
        if (IsModeAllowed(AllowedModes.Auto))
        {
            float fireDelay = autoFireDelay;
            float smooth = smoothLoc.y;
            float kbKick = kickback.x * extraLoc.z; // Auto nhân thêm extraLoc.z

            float target   = 0f;
            float peakSeen = 0f;
            bool  growing  = false;
            float prevAbs  = 0f;

            for (int shot = 0; shot < simShots; shot++)
            {
                target += kbKick;
                int frames = Mathf.RoundToInt(fireDelay / dt);
                for (int f = 0; f < frames; f++)
                    target = Mathf.Lerp(target, 0f, smooth * dt);

                float curAbs = Mathf.Abs(target);
                if (shot > 0 && curAbs > prevAbs + 0.0001f) growing = true;
                peakSeen = Mathf.Max(peakSeen, curAbs);
                prevAbs  = curAbs;
            }

            // Auto thường decay nhanh hơn (smoothLoc.y = 25 default) → ít cộng dồn hơn,
            // nhưng nếu ai đó set thấp thì vẫn nguy hiểm
            if (growing)
            {
                float minSmoothLocY = 0f;
                for (float s = 0.5f; s <= 100f; s += 0.5f)
                {
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest += kbKick;
                        int fr = Mathf.RoundToInt(fireDelay / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, s * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.0001f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { minSmoothLocY = s; break; }
                }
                // Tính ExtraLoc.Z max an toàn
                float maxSafeExtraLocZ = 0f;
                float baseKb = kickback.x;
                for (float elz = 0.1f; elz <= 5f; elz += 0.1f)
                {
                    float kickT = baseKb * elz;
                    float tTest = 0f, prevA = 0f; bool g = false;
                    for (int sh = 0; sh < simShots; sh++)
                    {
                        tTest += kickT;
                        int fr = Mathf.RoundToInt(fireDelay / dt);
                        for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, smooth * dt);
                        float ca = Mathf.Abs(tTest);
                        if (sh > 0 && ca > prevA + 0.0001f) { g = true; break; }
                        prevA = ca;
                    }
                    if (!g) { maxSafeExtraLocZ = elz; break; }
                }
                issues.Add(
                    $"[Accumulation | Auto | Kickback] _targetLoc.Z tích lũy liên tục khi Auto " +
                    $"(kickback×extraLoc.Z={kbKick:F4}m/phát, SmoothLoc.Y={smooth:F1}, fireDelay={autoFireDelay:F3}s). " +
                    $"Sau {simShots} phát: ~{peakSeen:F4}m. " +
                    $"Ngưỡng clamp MaxKickback={MaxKickback.x:F4}m. " +
                    "Tăng SmoothLoc.Y hoặc giảm Kickback / ExtraLoc.Z. " +
                    $"Khuyến nghị: " +
                    $"(A) SmoothLoc.Y >= {minSmoothLocY:F1} " +
                    $"(với kbKick={kbKick:F4}m, fireDelay={autoFireDelay:F3}s). " +
                    (maxSafeExtraLocZ > 0f
                        ? $"(B) ExtraLoc.Z <= {maxSafeExtraLocZ:F2} (giữ SmoothLoc.Y={smooth:F1}, Kickback.X={kickback.x:F4}). "
                        : "") +
                    $"(C) Kickback.X >= {kbKick / (maxSafeExtraLocZ > 0f ? maxSafeExtraLocZ : extraLoc.z):F4}m " +
                    $"(nếu giữ SmoothLoc.Y={smooth:F1}, ExtraLoc.Z={extraLoc.z:F2}).");
            }

            // KickUp Auto — ít nguy hiểm hơn nhưng vẫn kiểm tra
            float kuKick = kickUp.y * extraLoc.y;
            if (kuKick > 0f)
            {
                float kuTarget = 0f;
                float kuPeak   = 0f;
                bool  kuGrow   = false;
                float kuPrev   = 0f;

                for (int shot = 0; shot < simShots; shot++)
                {
                    kuTarget += kuKick;
                    int frames = Mathf.RoundToInt(fireDelay / dt);
                    for (int f = 0; f < frames; f++)
                        kuTarget = Mathf.Lerp(kuTarget, 0f, smooth * dt);

                    float ka = Mathf.Abs(kuTarget);
                    if (shot > 0 && ka > kuPrev + 0.0001f) kuGrow = true;
                    kuPeak = Mathf.Max(kuPeak, ka);
                    kuPrev = ka;
                }

                if (kuGrow)
                {
                    float minSmoothForKU = 0f;
                    for (float s = 0.5f; s <= 100f; s += 0.5f)
                    {
                        float tTest = 0f, prevA = 0f; bool g = false;
                        for (int sh = 0; sh < simShots; sh++)
                        {
                            tTest += kuKick;
                            int fr = Mathf.RoundToInt(fireDelay / dt);
                            for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, s * dt);
                            float ca = Mathf.Abs(tTest);
                            if (sh > 0 && ca > kuPrev + 0.0001f) { g = true; break; }
                            prevA = ca;
                        }
                        if (!g) { minSmoothForKU = s; break; }
                    }
                    float maxSafeKickUpY = 0f;
                    for (float ku = 0.001f; ku <= 0.1f; ku += 0.001f)
                    {
                        float kickT = ku * extraLoc.y;
                        float tTest = 0f, prevA = 0f; bool g = false;
                        for (int sh = 0; sh < simShots; sh++)
                        {
                            tTest += kickT;
                            int fr = Mathf.RoundToInt(fireDelay / dt);
                            for (int ff = 0; ff < fr; ff++) tTest = Mathf.Lerp(tTest, 0f, smooth * dt);
                            float ca = Mathf.Abs(tTest);
                            if (sh > 0 && ca > prevA + 0.0001f) { g = true; break; }
                            prevA = ca;
                        }
                        if (!g) { maxSafeKickUpY = ku; break; }
                    }
                    issues.Add(
                        $"[Accumulation | Auto | KickUp] _targetLoc.Y tích lũy khi Auto " +
                        $"(kickUp×extraLoc.Y={kuKick:F4}m/phát, SmoothLoc.Y={smooth:F1}). " +
                        $"Sau {simShots} phát: ~{kuPeak:F4}m — súng trôi lên trên. " +
                        "Tăng SmoothLoc.Y hoặc giảm KickUp / ExtraLoc.Y. " +
                        $"Khuyến nghị: " +
                        $"(A) SmoothLoc.Y >= {minSmoothForKU:F1} " +
                        $"(với kickUp={kuKick:F4}m, fireDelay={fireDelay:F3}s). " +
                        (maxSafeKickUpY > 0f
                            ? $"(B) KickUp.Y <= {maxSafeKickUpY:F3}m (giữ SmoothLoc.Y={smooth:F1}, ExtraLoc.Y={extraLoc.y:F2}). "
                            : "") +
                        $"Lưu ý: SmoothLoc.Y ảnh hưởng cả KickUp lẫn Kickback — tăng SmoothLoc.Y " +
                        "đồng thời giải quyết cả hai nếu cũng bị lỗi Auto Kickback.");
                }
            }
        }
    }

    /// <summary>
    /// Tính tỉ lệ còn lại của target sau fireDelay giây decay rời rạc ở fps Hz.
    /// remainRatio = 1.0 → không decay gì. 0.0 → decay hoàn toàn.
    /// </summary>
    private static float DecayRatioPerShot(float smooth, float fireDelay, float fps)
    {
        float dt     = 1f / fps;
        int   frames = Mathf.RoundToInt(fireDelay / dt);
        float remain = 1f;
        for (int f = 0; f < frames; f++)
            remain *= (1f - smooth * dt);
        return Mathf.Clamp01(remain);
    }

    /// <summary>
    /// Compat wrapper — trả về lỗi đầu tiên như cũ, dùng bởi runtime Awake.
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        bool ok = ValidateAll(out var issues);
        errorMessage = ok ? string.Empty : issues[0];
        return ok;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Validate(out string error))
            Debug.LogWarning($"[GunRecoilData] '{name}': {error}", this);
#endif
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  CUSTOM EDITOR
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(GunRecoilData))]
public class GunRecoilDataEditor : Editor
{
    // ── Loại súng để validate thêm các quy tắc đặc thù ──────────────────────
    public enum GunType { None, Rifle, Pistol, Shotgun, Sniper }

    private GunType _gunType       = GunType.None;
    private float   _semiFireDelay = 0.25f;   // giây/phát — điền từ WeaponData thực tế
    private float   _autoFireDelay = 0.1f;    // giây/phát — 0.1s ≈ 600 RPM

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GunRecoilData data = (GunRecoilData)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── Validation Settings ──────────────────", EditorStyles.boldLabel);

        // Gun type
        _gunType = (GunType)EditorGUILayout.EnumPopup(
            new GUIContent("Gun Type", "Chọn loại súng để nhận cảnh báo đặc thù."),
            _gunType);

        EditorGUILayout.Space(4);

        // Fire delay dùng cho accumulation simulation
        EditorGUILayout.LabelField("Accumulation Simulation", EditorStyles.boldLabel);

        _semiFireDelay = EditorGUILayout.FloatField(
            new GUIContent("Semi Fire Delay (s)",
                "Khoảng thời gian giữa 2 phát Semi (giây).\n" +
                "Lấy từ WeaponData.fireRate thực tế của khẩu súng này.\n" +
                "Ví dụ: 0.25s = 240 RPM, 0.15s = 400 RPM."),
            _semiFireDelay);
        _semiFireDelay = Mathf.Max(0.05f, _semiFireDelay);

        _autoFireDelay = EditorGUILayout.FloatField(
            new GUIContent("Auto Fire Delay (s)",
                "Khoảng thời gian giữa 2 phát Auto (giây).\n" +
                "Lấy từ WeaponData.fireRate thực tế của khẩu súng này.\n" +
                "Ví dụ: 0.1s = 600 RPM, 0.067s = 900 RPM."),
            _autoFireDelay);
        _autoFireDelay = Mathf.Max(0.016f, _autoFireDelay);

        // RPM display
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(" ");
        EditorGUILayout.LabelField(
            $"→ Semi ≈ {60f / _semiFireDelay:F0} RPM    Auto ≈ {60f / _autoFireDelay:F0} RPM",
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Validate button
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("✔  Validate Asset", GUILayout.Height(32)))
        {
            RunValidation(data, _gunType, _semiFireDelay, _autoFireDelay);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            $"Allowed modes : {data.allowedModes}\n" +
            $"Semi  curve   : {data.SemiCurveDuration:F2}s\n" +
            $"Auto  curve   : {data.AutoCurveDuration:F2}s\n" +
            $"Burst count   : {data.BurstCount}\n" +
            $"Sim fire delay: Semi {_semiFireDelay:F3}s | Auto {_autoFireDelay:F3}s",
            MessageType.Info);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CORE VALIDATION RUNNER
    // ─────────────────────────────────────────────────────────────────────────

    private static void RunValidation(GunRecoilData data, GunType gunType, float semiFireDelay = 0.25f, float autoFireDelay = 0.1f)
    {
        string assetName = data.name;
        string tag       = $"[GunRecoilData | {assetName}]";

        // 1. Thu thập lỗi chung
        bool ok = data.ValidateAll(out var issues, semiFireDelay, autoFireDelay);

        // 2. Thu thập cảnh báo đặc thù theo loại súng
        var hints = new System.Collections.Generic.List<string>();
        if (gunType != GunType.None)
            CollectGunTypeHints(data, gunType, hints);

        // 3. Log kết quả
        if (ok && hints.Count == 0)
        {
            Debug.Log($"<color=#44ff88>{tag} ✔ PASSED — không có vấn đề nào được tìm thấy.</color>",
                      data);
        }
        else
        {
            // Tiêu đề tổng quan
            int errorCount = issues.Count;
            int hintCount  = hints.Count;
            Debug.LogWarning(
                $"{tag} Validation kết thúc — " +
                $"{errorCount} lỗi, {hintCount} cảnh báo loại súng ({gunType}).\n" +
                "Chi tiết xem các dòng bên dưới ↓", data);

            // Log từng lỗi
            for (int i = 0; i < issues.Count; i++)
                Debug.LogError($"{tag} [{i + 1}/{errorCount}] {issues[i]}", data);

            // Log từng hint theo gun type
            for (int i = 0; i < hints.Count; i++)
                Debug.LogWarning($"{tag} [Hint {i + 1}/{hintCount} | {gunType}] {hints[i]}", data);
        }

        // 4. Ping asset trong Project window để dễ tìm
        EditorGUIUtility.PingObject(data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GUN-TYPE SPECIFIC HINTS
    //  Mỗi loại súng có dải giá trị "lành mạnh" khác nhau.
    //  Các con số dưới đây là khuyến nghị thực tế — không phải giới hạn cứng.
    // ─────────────────────────────────────────────────────────────────────────

    private static void CollectGunTypeHints(GunRecoilData d, GunType type,
        System.Collections.Generic.List<string> hints)
    {
        switch (type)
        {
            // ── RIFLE (AR / SMG) ─────────────────────────────────────────
            // Semi + Auto, pitch vừa phải, yaw nhỏ, noise thấp
            case GunType.Rifle:
            {
                CheckModeExpected(d, GunRecoilData.AllowedModes.Auto,
                    "Rifle thường có chế độ Auto. Nếu chỉ Semi thì đây là súng chiến thuật.", hints);

                if (Mathf.Abs(d.pitch.x) < 1f)
                    hints.Add($"Pitch rất nhỏ (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Rifle thường pitch 1.5–4° mỗi phát.");
                if (Mathf.Abs(d.pitch.x) > 8f)
                    hints.Add($"Pitch quá lớn (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Rifle hiếm khi vượt 6°/phát — kiểm tra lại.");

                if (Mathf.Abs(d.yaw.w) > 3f)
                    hints.Add($"Yaw max ({d.yaw.w:F2}) khá cao cho Rifle. " +
                              "AR thường yaw ±0.5–2°.");

                if (d.extraRot.z < 1f)
                    hints.Add($"ExtraRot.Z ({d.extraRot.z:F2}) < 1. Rifle Auto sẽ không leo ngang — " +
                              "thêm climb nếu muốn mô phỏng full-auto.");

                if (d.smoothRot.y > 20f)
                    hints.Add($"SmoothRot.Y (Auto decay = {d.smoothRot.y:F1}) rất cao. " +
                              "Rifle auto giật sẽ reset quá nhanh giữa các phát.");

                if (d.noiseScalar > 2f)
                    hints.Add($"NoiseScalar ({d.noiseScalar:F2}) cao cho Rifle. " +
                              "Thường 0.5–1.5 để không che khuất crosshair.");
                break;
            }

            // ── PISTOL ───────────────────────────────────────────────────
            // Semi only, pitch mạnh, kickback rõ, noise thấp, no auto
            case GunType.Pistol:
            {
                if (d.IsModeAllowed(GunRecoilData.AllowedModes.Auto))
                    hints.Add("Pistol thường chỉ có Semi. Auto trên pistol rất hiếm " +
                              "(trừ Glock 18 / Beretta 93R).");

                if (Mathf.Abs(d.pitch.x) < 2f)
                    hints.Add($"Pitch nhỏ (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Pistol thường pitch 3–8° — giật cảm giác nhẹ.");
                if (Mathf.Abs(d.pitch.x) > 12f)
                    hints.Add($"Pitch rất lớn (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Pistol hiếm khi vượt 10°/phát.");

                if (d.kickback.x > -0.01f)
                    hints.Add($"Kickback quá nhỏ (X={d.kickback.x:F4}). " +
                              "Pistol thường kickback -0.02 đến -0.05 để cảm giác giật tay.");

                if (d.camRecoilX < 1.5f)
                    hints.Add($"CamRecoilX ({d.camRecoilX:F2}) thấp. " +
                              "Pistol nên có camera shake rõ hơn (2–5) vì không có báng tì vai.");

                if (d.smoothRot.x < 10f)
                    hints.Add($"SmoothRot.X (Semi decay = {d.smoothRot.x:F1}) thấp. " +
                              "Pistol thường hồi nhanh (12–20) giữa các phát semi.");
                break;
            }

            // ── SHOTGUN ──────────────────────────────────────────────────
            // Semi / Pump (Semi only), pitch rất mạnh, kickback lớn, yaw rộng, noise cao
            case GunType.Shotgun:
            {
                if (d.IsModeAllowed(GunRecoilData.AllowedModes.Auto))
                    hints.Add("Shotgun thường chỉ Semi (pump/semi-auto). " +
                              "Auto shotgun rất hiếm — xác nhận lại nếu chủ ý.");

                if (Mathf.Abs(d.pitch.x) < 4f)
                    hints.Add($"Pitch nhỏ (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Shotgun thường pitch mạnh 5–12° mỗi phát.");

                if (d.kickback.x > -0.03f)
                    hints.Add($"Kickback nhỏ (X={d.kickback.x:F4}). " +
                              "Shotgun nên có kickback lớn (-0.05 đến -0.12) để cảm giác 'đá' mạnh.");

                if (d.pushAmount > -0.5f)
                    hints.Add($"PushAmount ({d.pushAmount:F2}) nhỏ. " +
                              "Shotgun thường pushAmount -0.6 đến -1.2 để camera jolt mạnh.");

                if (Mathf.Abs(d.yaw.w) < 1f && Mathf.Abs(d.roll.w) < 2f)
                    hints.Add("Yaw và Roll đều nhỏ. Shotgun thường có yaw/roll rộng hơn " +
                              "(yaw ±1–3°, roll ±3–6°) để cảm giác súng không kiểm soát được.");

                if (d.smoothRot.x > 20f)
                    hints.Add($"SmoothRot.X (Semi decay = {d.smoothRot.x:F1}) rất cao. " +
                              "Shotgun pump-action nên phục hồi chậm (6–12) — mỗi phát nặng nề.");
                break;
            }

            // ── SNIPER ───────────────────────────────────────────────────
            // Semi only, pitch rất lớn, yaw nhỏ, noise tối thiểu khi ADS, hồi chậm
            case GunType.Sniper:
            {
                if (d.IsModeAllowed(GunRecoilData.AllowedModes.Auto))
                    hints.Add("Sniper không nên có Auto mode. " +
                              "Nếu là DMR bán tự động thì cần cân nhắc kỹ tốc độ bắn.");

                if (d.IsModeAllowed(GunRecoilData.AllowedModes.Burst))
                    hints.Add("Sniper thường không có Burst mode.");

                if (Mathf.Abs(d.pitch.x) < 5f)
                    hints.Add($"Pitch nhỏ (|X|={Mathf.Abs(d.pitch.x):F2}). " +
                              "Sniper thường pitch 6–15° mỗi phát — giật mạnh để phạt bắn nhanh.");

                if (d.aimRotMultiplier.x > 0.4f)
                    hints.Add($"AimRotMultiplier.X ({d.aimRotMultiplier.x:F2}) cao khi ADS. " +
                              "Sniper khi scope-in nên giảm pitch rõ rệt (0.1–0.3) để cảm giác 'kiểm soát được'.");

                if (d.camRecoilX < 3f)
                    hints.Add($"CamRecoilX ({d.camRecoilX:F2}) thấp. " +
                              "Sniper nên shake camera mạnh (4–10) mỗi phát.");

                if (d.smoothRot.x > 15f)
                    hints.Add($"SmoothRot.X (Semi decay = {d.smoothRot.x:F1}) cao. " +
                              "Sniper nên hồi chậm (5–10) để phạt follow-up shot quá nhanh.");

                if (d.noiseScalar > 1f && d.aimLocMultiplier.x > 0.5f)
                    hints.Add($"NoiseScalar ({d.noiseScalar:F2}) cao và AimLocMultiplier chưa giảm noise khi ADS. " +
                              "Sniper khi scope-in cần noise gần như bằng 0.");

                if (d.extraRot.z > 3f)
                    hints.Add($"ExtraRot.Z ({d.extraRot.z:F2}) lớn nhưng Sniper không bắn Auto. " +
                              "ExtraRot chỉ ảnh hưởng Auto/Burst — giá trị này không có tác dụng.");
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static void CheckModeExpected(GunRecoilData d, GunRecoilData.AllowedModes mode,
        string message, System.Collections.Generic.List<string> hints)
    {
        if (!d.IsModeAllowed(mode))
            hints.Add(message);
    }
}
#endif