using UnityEngine;

namespace TrueInputSystem
{
    /// <summary>
    /// Nền tảng được phép áp dụng Aim Assist.
    /// Dùng [Flags] để có thể chọn nhiều nền tảng cùng lúc.
    /// Ví dụ: AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile
    /// </summary>
    [System.Flags]
    public enum AimAssistPlatform
    {
        None      = 0,
        PC        = 1 << 0,   // Keyboard + Mouse
        Gamepad   = 1 << 1,   // Controller (Xbox, PS, v.v.)
        Mobile    = 1 << 2,   // Touchscreen
        All       = PC | Gamepad | Mobile
    }
    
    public class TISAimAssist
    {
        private AimAssistPlatform _currentPlatform = AimAssistPlatform.PC;
        // -------------------------------------------------------
        // API CHÍNH
        // -------------------------------------------------------

        /// <summary>
        /// Áp dụng aim assist, trả về lượng góc xoay hoàn chỉnh (đơn vị: Độ) sau khi đã cộng lực hút.
        /// </summary>
        /// <param name="cameraTransform">Transform của camera</param>
        /// <param name="target">Vị trí mục tiêu trong World Space</param>
        /// <param name="playerLookDelta">Góc xoay dự kiến của người chơi trong frame này (đơn vị: Độ)</param>
        /// <param name="intensity">Mức độ hỗ trợ (0 = tắt, 1 = mạnh nhất)</param>
        /// <returns>Tổng góc xoay Delta (Độ) cuối cùng để áp dụng vào Camera</returns>
        public Vector2 AimAssistProcess(AimAssistSettings settings, Transform cameraTransform, Vector3 target, Vector2 playerLookDelta, float intensity = 0.3f)
        {
            if (!IsPlatformEnabled(settings.EnabledPlatforms))
                return playerLookDelta;

            if (target == Vector3.zero) return playerLookDelta;
            
            intensity = Mathf.Clamp01(intensity);

            if (intensity <= 0f)
                return playerLookDelta;

            // --- 1. Chống lỗi Aimbot: Không có input thì không tự hút (nếu cấu hình tắt) ---
            if (!settings.AssistWithoutInput && playerLookDelta.sqrMagnitude < 0.0001f)
                return playerLookDelta;

            // --- 2. Hướng tới target trong world space ---
            Vector3 directionToTarget = target - cameraTransform.position;
            if (directionToTarget.sqrMagnitude < 0.0001f)
                return playerLookDelta;

            directionToTarget.Normalize();

            // Bỏ qua nếu target nằm phía sau camera
            if (Vector3.Dot(cameraTransform.forward, directionToTarget) <= 0f)
                return playerLookDelta;

            // --- 3. Chuyển sang camera-local space ---
            Vector3 localDir = cameraTransform.InverseTransformDirection(directionToTarget);

            // --- 4. Tính góc lệch yaw/pitch (đơn vị: Độ) giữa tâm màn hình và target ---
            float yawError   =  Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float pitchError = -Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg; 

            float angleToTarget = Mathf.Sqrt(yawError * yawError + pitchError * pitchError);

            // --- 5. Chỉ hỗ trợ khi target nằm trong snapRadius ---
            if (angleToTarget > settings.SnapRadius)
                return playerLookDelta;

            // --- 6. Falloff: mạnh nhất ở tâm, giảm dần khi ra rìa vòng tròn hỗ trợ ---
            float t = 1f - (angleToTarget / settings.SnapRadius);
            float falloff = Mathf.SmoothStep(0f, 1f, t);

            // --- 7. Tính toán lượng góc bổ sung (Độ) độc lập với FPS nhờ Time.deltaTime ---
            Vector2 angleCorrection = new Vector2(yawError, pitchError);
            Vector2 assistDelta = angleCorrection * falloff * intensity * settings.AssistSpeed * Time.deltaTime;

            // --- 8. Kết hợp góc xoay của người chơi và lực hút hỗ trợ ---
            return playerLookDelta + assistDelta;
        }
        
        /// <summary>Gọi mỗi frame trong Update() để cập nhật thiết bị đang dùng.</summary>
        public void UpdateActivePlatform()
        {
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            bool gamepadHasInput = false;
            if (gamepad != null)
            {
                gamepadHasInput = gamepad.leftStick.ReadValue().sqrMagnitude > 0.01f
                                  || gamepad.rightStick.ReadValue().sqrMagnitude > 0.01f
                                  || gamepad.buttonSouth.wasPressedThisFrame
                                  || gamepad.buttonNorth.wasPressedThisFrame
                                  || gamepad.buttonEast.wasPressedThisFrame
                                  || gamepad.buttonWest.wasPressedThisFrame
                                  || gamepad.leftTrigger.ReadValue() > 0.01f
                                  || gamepad.rightTrigger.ReadValue() > 0.01f
                                  || gamepad.leftShoulder.wasPressedThisFrame
                                  || gamepad.rightShoulder.wasPressedThisFrame
                                  || gamepad.dpad.ReadValue().sqrMagnitude > 0.01f;
            }

            bool pcHasInput = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                              || (mouse != null && (mouse.delta.ReadValue() != Vector2.zero
                                                    || mouse.leftButton.wasPressedThisFrame
                                                    || mouse.rightButton.wasPressedThisFrame));

            if (pcHasInput)
                _currentPlatform = AimAssistPlatform.PC;
            else if (gamepadHasInput)
                _currentPlatform = AimAssistPlatform.Gamepad;

            //Debug.Log($"Gamepad detected: {gamepad?.name ?? "null"} | HasInput: {gamepadHasInput} | Platform: {_currentPlatform}");
        }
        
        /// <summary>Kiểm tra nền tảng runtime hiện tại có nằm trong EnabledPlatforms không.</summary>
        private bool IsPlatformEnabled(AimAssistPlatform enabledPlatforms)
        {
            if (enabledPlatforms == AimAssistPlatform.None) return false;
            if (enabledPlatforms == AimAssistPlatform.All)  return true;

            if (Application.isMobilePlatform)
                return (enabledPlatforms & AimAssistPlatform.Mobile) != 0;

            return (enabledPlatforms & _currentPlatform) != 0;
        }
    }
    
    public class AimAssistSettings
    {
        private float _snapRadius = 5f;
        private float _assistSpeed = 8f;
        private bool _assistWithoutInput = false;
        private AimAssistPlatform _enabledPlatforms = AimAssistPlatform.All;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="snapRadius">Bán kính vùng hỗ trợ tính bằng độ (góc từ tâm màn hình rọi ra)</param>
        /// <param name="assistSpeed">Tốc độ kéo/hút tâm ngắm về phía mục tiêu (giá trị càng cao hút càng nhanh)</param>
        /// <param name="assistWithoutInput">Nếu TRUE: Tâm tự động hút kể cả khi đứng yên (Aimbot). Nếu FALSE: Chỉ hỗ trợ khi người chơi đang di chuyển tâm ngắm.</param>
        /// <param name="enabledPlatforms"></param>
        public AimAssistSettings(float snapRadius = 5f, float assistSpeed = 8f, bool assistWithoutInput = false,
            AimAssistPlatform enabledPlatforms = AimAssistPlatform.All)
        {
            _snapRadius = snapRadius;
            _assistSpeed = assistSpeed;
            _assistWithoutInput = assistWithoutInput;
            _enabledPlatforms = enabledPlatforms;
        }

        public AimAssistPlatform EnabledPlatforms
        {
            get => _enabledPlatforms;
            set => _enabledPlatforms = value;
        }
        
        [Tooltip("Bán kính vùng hỗ trợ tính bằng độ (góc từ tâm màn hình rọi ra)")]
        public float SnapRadius
        {
            get => _snapRadius;
            set => _snapRadius = value;
        }

        [Tooltip("Tốc độ kéo/hút tâm ngắm về phía mục tiêu (giá trị càng cao hút càng nhanh)")]
        public float AssistSpeed
        {
            get => _assistSpeed;
            set => _assistSpeed = value;
        }

        [Tooltip("Nếu TRUE: Tâm tự động hút kể cả khi đứng yên (Aimbot). Nếu FALSE: Chỉ hỗ trợ khi người chơi đang di chuyển tâm ngắm.")]
        public bool AssistWithoutInput
        {
            get => _assistWithoutInput;
            set => _assistWithoutInput = value;
        }
    }
}
