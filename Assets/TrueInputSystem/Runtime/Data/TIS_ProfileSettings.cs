// ============================================================
//  TrueInputSystem — ProfileSettings.cs
//  Các struct cấu hình Mobile, Camera, và Gamepad.
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    // ══════════════════════════════════════════════════════════
    //  MOBILE SETTINGS
    // ══════════════════════════════════════════════════════════

    [Serializable]
    public class TISMobileSettings
    {
        [Header("Virtual Joystick")]

        [Range(50f, 250f)]
        [Tooltip("Bán kính (pixel) của vùng kéo joystick.")]
        public float joystickRadius = 100f;

        [Range(0f, 0.5f)]
        [Tooltip("Deadzone joystick — loại bỏ rung tay khi cố giữ yên.")]
        public float joystickDeadzone = 0.08f;

        [Tooltip("true = joystick xuất hiện tại điểm chạm (linh hoạt).\n" +
                 "false = cố định ở góc trái màn hình.")]
        public bool dynamicJoystick = true;

        [Range(0f, 1f)]
        [Tooltip("Opacity của joystick khi không được chạm.")]
        public float idleOpacity = 0.4f;

        [Header("On-Screen Buttons")]

        [Tooltip("Hiển thị các button skill trên màn hình mobile.")]
        public bool showSkillButtons = true;

        [Range(60f, 180f)]
        [Tooltip("Kích thước (pixel) của mỗi on-screen skill button.")]
        public float skillButtonSize = 100f;

        [Header("Touch Camera Zone")]

        [Range(0.2f, 0.6f)]
        [Tooltip("Tỉ lệ chiều rộng màn hình dành cho vùng kéo camera.")]
        public float cameraZoneWidthRatio = 0.4f;

        [Range(0f, 0.3f)]
        [Tooltip("Opacity vùng camera khi debug. Đặt = 0 trong production.")]
        public float cameraZoneDebugOpacity = 0f;
    }

    // ══════════════════════════════════════════════════════════
    //  CAMERA SETTINGS
    // ══════════════════════════════════════════════════════════

    [Serializable]
    public class TISCameraSettings
    {
        [Header("PC — Mouse Sensitivity")]
        [Tooltip("Độ nhạy chuột theo trục X (ngang) và Y (dọc).")]
        public Vector2 mouseSensitivity = new Vector2(1.5f, 1.5f);

        [Header("Gamepad — Right Stick")]
        [Tooltip("Độ nhạy right stick. Thường cần lớn hơn mouse nhiều.")]
        public Vector2 gamepadSensitivity = new Vector2(0.25f, 0.25f);

        [Header("Mobile — Touch Drag")]
        [Tooltip("Độ nhạy kéo ngón tay trên mobile.")]
        public Vector2 mobileSensitivity = new Vector2(0.12f, 0.12f);

        [Header("Behaviour")]

        [Tooltip("Đảo chiều trục Y khi nhìn lên/xuống (flight-sim convention).")]
        public bool invertY = false;

        [Range(1f, 30f)]
        [Tooltip("Tốc độ làm mượt camera. Cao = nhanh, thấp = cinematic lag.")]
        public float smoothing = 12f;

        [Header("Pitch Limits")]

        [Range(-90f, 0f)]
        [Tooltip("Giới hạn góc nhìn lên (âm = nhìn lên).")]
        public float pitchMin = -80f;

        [Range(0f, 90f)]
        [Tooltip("Giới hạn góc nhìn xuống (dương = nhìn xuống).")]
        public float pitchMax = 80f;
    }

    // ══════════════════════════════════════════════════════════
    //  GAMEPAD SETTINGS
    // ══════════════════════════════════════════════════════════

    [Serializable]
    public class TISGamepadSettings
    {
        [Header("Deadzone")]

        [Range(0f, 0.5f)]
        [Tooltip("Deadzone left stick (di chuyển). Tránh drift.")]
        public float leftStickDeadzone = 0.12f;

        [Range(0f, 0.5f)]
        [Tooltip("Deadzone right stick (camera).")]
        public float rightStickDeadzone = 0.1f;

        [Header("Response Curve")]

        [Range(1f, 3f)]
        [Tooltip("Đường cong phản hồi CẦN PHẢI (ngắm). 1 = tuyến tính, > 1 = chính xác hơn khi nghiêng nhẹ.\n" +
                 "Cần trái (di chuyển) luôn tuyến tính.")]
        public float stickCurve = 1.5f;

        [Header("Haptics")]

        [Tooltip("Rumble / haptic feedback khi nhận damage hoặc hành động đặc biệt.")]
        public bool enableRumble = true;
    }

    // ══════════════════════════════════════════════════════════
    //  MOTION SETTINGS (Gyro + Touchpad)
    // ══════════════════════════════════════════════════════════

    /// <summary>Khi nào gyro được cộng vào LookDelta.</summary>
    public enum TISGyroActivation
    {
        /// <summary>Luôn bật.</summary>
        Always,
        /// <summary>Chỉ bật khi đang giữ action <see cref="TISMotionSettings.gyroActivationAction"/> (ví dụ "Aim").</summary>
        WhileActionHeld,
        /// <summary>Tắt khi đang giữ action (dùng làm nút "ratchet" để chỉnh lại tay cầm).</summary>
        DisabledWhileActionHeld,
    }

    /// <summary>Cách quy đổi tốc độ góc 3D của tay cầm sang yaw/pitch 2D.</summary>
    public enum TISGyroSpace
    {
        /// <summary>Yaw = trục Y của tay cầm. Đơn giản, đúng khi cầm tay cầm nằm ngang.</summary>
        Local,
        /// <summary>Yaw tính theo trục trọng lực — người chơi xoay hay nghiêng tay cầm đều quay ngang được.</summary>
        Player,
    }

    [Serializable]
    public class TISMotionSettings
    {
        [Header("Chung")]

        [Tooltip("Chỉ đưa gyro / touchpad vào action (Look...) khi người chơi đang dùng tay cầm.\n" +
                 "Chuyển sang bàn phím-chuột / cảm ứng → tay cầm đặt trên bàn không làm xoay camera nữa;\n" +
                 "bấm lại tay cầm là gyro / touchpad chạy tiếp. Nguồn giả lập trong Editor không bị chặn.")]
        public bool onlyWhenGamepadActive = true;

        [Header("Gyro Aim")]

        [Tooltip("Công tắc tổng gyro. Gyro được đưa vào game qua binding <TISMotion>/gyro\n" +
                 "hoặc ô \"+ Gyro\" của binding Left / Right Stick trong Setup Tool.")]
        public bool enableGyro = false;

        public TISGyroActivation gyroActivation = TISGyroActivation.Always;

        [Tooltip("Tên action dùng cho WhileActionHeld / DisabledWhileActionHeld (ví dụ \"Aim\").")]
        public string gyroActivationAction = "Aim";

        public TISGyroSpace gyroSpace = TISGyroSpace.Player;

        [Tooltip("Độ nhạy gyro khi dùng cho action \"Look\" (độ/giây → LookDelta).\n" +
                 "Action khác bind gyro nhận đơn vị thô: độ/giây.")]
        public Vector2 gyroSensitivity = new Vector2(0.02f, 0.02f);

        public bool invertGyroX = false;
        public bool invertGyroY = false;

        [Header("Gyro Filtering")]

        [Range(0f, 10f)]
        [Tooltip("Tốc độ góc (độ/giây) dưới ngưỡng này bị loại bỏ hoàn toàn.")]
        public float gyroDeadzone = 0.5f;

        [Range(0f, 20f)]
        [Tooltip("Tightening: dưới ngưỡng này (độ/giây) input bị giảm tỉ lệ để chống rung tay. 0 = tắt.")]
        public float tighteningThreshold = 3f;

        [Range(0f, 30f)]
        [Tooltip("Soft-tier smoothing: dưới ngưỡng này (độ/giây) input được làm mượt, trên ngưỡng giữ nguyên độ trễ 0. 0 = tắt.")]
        public float smoothingThreshold = 6f;

        [Range(0.01f, 0.5f)]
        [Tooltip("Thời gian làm mượt (giây) cho vùng soft-tier.")]
        public float smoothingTime = 0.08f;

        [Header("Gyro Calibration")]

        [Tooltip("Tự hiệu chỉnh drift khi tay cầm được đặt yên.")]
        public bool autoCalibrate = true;

        [Range(0.2f, 5f)]
        [Tooltip("Tay cầm phải đứng yên bao lâu (giây) trước khi bắt đầu tự hiệu chỉnh.")]
        public float stillnessTime = 1.5f;

        [Range(0.5f, 10f)]
        [Tooltip("Ngưỡng tốc độ góc (độ/giây, đã trừ offset) để coi là đứng yên.")]
        public float stillnessGyroThreshold = 2f;

        [Range(0.005f, 0.2f)]
        [Tooltip("Ngưỡng thay đổi gia tốc (g) giữa 2 frame để coi là đứng yên.")]
        public float stillnessAccelThreshold = 0.03f;

        [Header("Virtual Cursor")]

        [Tooltip("Dùng touchpad để di chuyển con trỏ ảo (giống touchpad laptop).")]
        public bool touchpadMovesCursor = true;

        [Range(0.1f, 5f)]
        [Tooltip("Độ nhạy touchpad: 1 = kéo hết chiều ngang touchpad ↔ đi hết chiều ngang màn hình.")]
        public float touchpadCursorSensitivity = 1.2f;

        [Tooltip("Nhấn touchpad = click chuột trái.")]
        public bool touchpadClickIsLeftClick = true;

        [Tooltip("Dùng gyro để di chuyển con trỏ ảo.")]
        public bool gyroMovesCursor = false;

        [Range(1f, 100f)]
        [Tooltip("Pixel mỗi độ xoay tay cầm khi dùng gyro cursor.")]
        public float gyroCursorPixelsPerDegree = 20f;

        [Header("Touchpad Look")]

        [Tooltip("Công tắc tổng touchpad (binding <TISMotion>/touchpad*, \"+ Touchpad\", virtual cursor).")]
        public bool enableTouchpad = false;

        [Tooltip("Độ nhạy touchpad khi dùng cho action \"Look\" (tốc độ vuốt → LookDelta).\n" +
                 "Tốc độ vuốt tính theo bề rộng touchpad / giây.\n" +
                 "Action khác bind touchpad nhận đơn vị thô.")]
        public Vector2 touchpadLookSensitivity = new Vector2(0.1f, 0.1f);

        [Header("Providers (PC)")]

        [Tooltip("Đọc gyro / touchpad tay cầm thật trên PC (DualShock 4, DualSense, Switch Pro...) qua SDL3.dll.\n" +
                 "Chỉ khởi động khi profile có binding dùng gyro / touchpad hoặc virtual cursor cần.")]
        public bool useSdl3OnPC = true;

        public TISSdl3Settings sdl3 = new TISSdl3Settings();

        [Tooltip("Giả lập gyro (I/J/K/L/U/O, Right Shift = nhanh) và touchpad (giữ T + chuột, G = click) trong Editor.\n" +
                 "Tay cầm thật luôn được ưu tiên khi đang kết nối.")]
        public bool simulateInEditor = false;

        public TISSimulatedMotionSource simulated = new TISSimulatedMotionSource();

        [Tooltip("Hiện bảng debug gyro / touchpad góc trên-trái (Editor + Development Build).")]
        public bool showDebugGUI = false;
    }
}