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

    [Header("Fire Delay (seconds between shots)")]
    [Tooltip("Thời gian chờ (giây) giữa hai phát bắn.\n" +
             "Ví dụ: 0.5 → click nhanh cỡ nào cũng phải chờ 0.5s mới bắn được phát tiếp.")]
    [Min(0.01f)]
    public float FireDelay = 0.125f;
    
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
             "• Y = (chưa dùng, dự phòng)\n" +
             "• Z = nhân Yaw    (ví dụ: 7 → lắc ngang nhiều hơn khi auto)\n\n" +
             "Đặt = 1 để không thay đổi so với semi. Tăng Z tạo hiệu ứng 'spin-up' đặc trưng súng máy.")]
    public Vector3 extraRot = new Vector3(1.3f, 3f, 7f);

    [Tooltip("Hệ số nhân thêm lên giật tịnh tiến khi đang bắn chế độ Auto hoặc Burst.\n\n" +
             "• X = nhân KickRight  (lệch ngang thêm khi auto)\n" +
             "• Y = nhân KickUp     (nảy lên thêm khi auto)\n" +
             "• Z = nhân Kickback   (ví dụ: 1.3 → lùi thêm 30% khi auto)\n\n" +
             "Đặt = 1 để tịnh tiến không thay đổi giữa semi và auto.\n" +
             "Tăng Z để cảm giác súng máy 'đẩy mạnh hơn' khi bắn liên thanh.")]
    public Vector3 extraLoc = new Vector3(1f, 1f, 1.3f);

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
    /// Trả về true nếu asset hợp lệ.
    /// Gọi từ Editor hoặc runtime Awake.
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        // Allowed modes
        if (allowedModes == 0)
        { errorMessage = "AllowedModes: phải có ít nhất một mode được tick."; return false; }

        // Fire rates
        if (FireDelay  <= 0f) { errorMessage = "FireDelay phải > 0.";  return false; }

        // Pitch range
        if (pitch.x > pitch.y)
        { errorMessage = "Pitch: min (X) phải ≤ max (Y)."; return false; }

        // Roll: XY là cặp min, ZW là cặp max
        if (roll.x > roll.y)
        { errorMessage = "Roll: min range (X) phải ≤ (Y)."; return false; }
        if (roll.z > roll.w)
        { errorMessage = "Roll: max range (Z) phải ≤ (W)."; return false; }

        // Yaw: tương tự
        if (yaw.x > yaw.y)
        { errorMessage = "Yaw: min range (X) phải ≤ (Y)."; return false; }
        if (yaw.z > yaw.w)
        { errorMessage = "Yaw: max range (Z) phải ≤ (W)."; return false; }

        // Translation ranges
        if (kickback.x > kickback.y)
        { errorMessage = "Kickback: min (X) phải ≤ max (Y)."; return false; }
        if (kickUp.x > kickUp.y)
        { errorMessage = "KickUp: min (X) phải ≤ max (Y)."; return false; }
        if (kickRight.x > kickRight.y)
        { errorMessage = "KickRight: min (X) phải ≤ max (Y)."; return false; }

        // Smooth / damp phải dương
        if (smoothRot.x <= 0f || smoothRot.y <= 0f || smoothRot.z <= 0f)
        { errorMessage = "SmoothRot: tất cả components phải > 0."; return false; }
        if (smoothLoc.x <= 0f || smoothLoc.y <= 0f || smoothLoc.z <= 0f)
        { errorMessage = "SmoothLoc: tất cả components phải > 0."; return false; }
        if (pushAccel <= 0f || pushDamp <= 0f)
        { errorMessage = "PushAccel và PushDamp phải > 0."; return false; }
        if (idleNoiseDecayTime < 0f)
        { errorMessage = "IdleNoiseDecayTime phải ≥ 0."; return false; }

        // Camera shake
        if (camSnappiness <= 0f)
        { errorMessage = "CamSnappiness phải > 0."; return false; }
        if (camReturnSpeed <= 0f)
        { errorMessage = "CamReturnSpeed phải > 0."; return false; }

        // Curves: phải bắt đầu và kết thúc tại 0
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
            if (curve == null || curve.length == 0)
            { errorMessage = $"{curveNames[i]}: curve rỗng."; return false; }

            float startVal = curve.Evaluate(curve.keys[0].time);
            float endVal   = curve.Evaluate(curve.keys[curve.length - 1].time);

            if (Mathf.Abs(startVal) > 0.001f)
            { errorMessage = $"{curveNames[i]}: phải bắt đầu tại 0 (hiện tại: {startVal:F4})."; return false; }
            if (Mathf.Abs(endVal) > 0.001f)
            { errorMessage = $"{curveNames[i]}: phải kết thúc tại 0 (hiện tại: {endVal:F4})."; return false; }

            // FIX: curve là hệ số lerp nên PHẢI nằm trong [0,1] xuyên suốt, không chỉ đầu/cuối.
            // Mathf.Lerp tự clamp t vào [0,1], nên nếu curve đi ra ngoài khoảng này (ví dụ
            // overshoot âm ở giữa curve), phát bắn sẽ bị "đứng khựng" (t âm → clamp về 0,
            // không lerp) rồi giật bù đột ngột khi curve quay lại dương — đây chính là lỗi
            // đã gặp với M4_Recoil.asset (curve có key value: -0.6 và -0.1 ở giữa).
            float startTime = curve.keys[0].time;
            float endTime   = curve.keys[curve.length - 1].time;
            const int sampleCount = 20;
            for (int s = 0; s <= sampleCount; s++)
            {
                float t = Mathf.Lerp(startTime, endTime, s / (float)sampleCount);
                float v = curve.Evaluate(t);
                if (v < -0.01f || v > 1.01f)
                {
                    errorMessage = $"{curveNames[i]}: curve vượt ra ngoài [0,1] tại t={t:F3} " +
                                   $"(giá trị: {v:F3}). Curve là hệ số lerp nên phải luôn nằm " +
                                   $"trong [0,1], nếu không phát bắn sẽ bị đứng khựng rồi giật bù đột ngột.";
                    return false;
                }
            }
        }

        errorMessage = string.Empty;
        return true;
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
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GunRecoilData data = (GunRecoilData)target;

        EditorGUILayout.Space(8);
        if (GUILayout.Button("✔  Validate Asset", GUILayout.Height(28)))
        {
            if (data.Validate(out string error))
                EditorUtility.DisplayDialog("Validation Passed ✔", "Tất cả giá trị hợp lệ!", "OK");
            else
                EditorUtility.DisplayDialog("Validation Failed ✘", error, "OK");
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            $"Allowed modes: {data.allowedModes}\n" +
            $"Fire delay: {data.FireDelay:F3}s  " +
            $"Semi curve duration: {data.SemiCurveDuration:F2}s\n" +
            $"Auto curve duration: {data.AutoCurveDuration:F2}s",
            MessageType.Info);
    }
}
#endif