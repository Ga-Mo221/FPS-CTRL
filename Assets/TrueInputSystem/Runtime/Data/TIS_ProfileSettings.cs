// ============================================================
//  TrueInputSystem — ProfileSettings.cs
//  Các struct cấu hình Mobile, Camera, và Gamepad.
// ============================================================

using System;
using UnityEngine;

namespace TrueInputSystem
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
        [Tooltip("Curve stick input. > 1 = precise aiming. = 1 = tuyến tính.")]
        public float stickCurve = 1.5f;

        [Header("Haptics")]

        [Tooltip("Rumble / haptic feedback khi nhận damage hoặc hành động đặc biệt.")]
        public bool enableRumble = true;
    }
}