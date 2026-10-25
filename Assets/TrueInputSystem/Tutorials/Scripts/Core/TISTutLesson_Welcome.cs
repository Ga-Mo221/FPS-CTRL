// ============================================================
//  TrueInputSystem Tutorials — TISTutLesson_Welcome.cs
//  Bài 0 (dùng chung 3 scene): làm quen màn hình hướng dẫn + ActiveDevice.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Bài mở đầu")]
    public class TISTutLesson_Welcome : TISTutLesson
    {
        float _clipTimer;

        TISTutMode Mode => Controller != null ? Controller.Mode : TISTutTheme.Mode;

        public override string Title => "Chào mừng";

        public override string Summary => Mode switch
        {
            TISTutMode.Gamepad => "Học cách dùng tay cầm với TIS: cần analog, nút, rung, điều hướng UI, con trỏ ảo, gyro và touchpad. Mỗi bài có phần setup, code mẫu và thử ngay ở sân chơi.",
            TISTutMode.Mobile  => "Học cách làm điều khiển cảm ứng với TIS: joystick ảo, vùng xoay camera, nút kỹ năng, kỹ năng định hướng kiểu MOBA và cử chỉ nhiều ngón. Mỗi bài có phần setup, code mẫu và thử ngay ở sân chơi.",
            _                  => "Học cách dùng chuột + bàn phím với TIS: di chuyển, xoay camera, nút bấm, con trỏ chuột, action map và đổi phím. Mỗi bài có phần setup, code mẫu và thử ngay ở sân chơi.",
        };

        public override string[] SetupSteps => new[]
        {
            "Kéo <b>TISInputBridge</b> vào scene (menu <b>Tools › True Input System › Setup Tool</b> làm việc này giúp bạn).",
            "Gán một <b>Input Profile</b> vào ô Profile của bridge — profile chứa mọi action và binding cho PC, tay cầm, mobile.",
            "Từ bất kỳ script nào, đọc input qua <b>TISInputBridge.Instance</b>. Không cần tự tạo InputAction.",
        };

        public override string Code =>
@"using GaMo.TrueInputSystem;
using UnityEngine;

public class Player : MonoBehaviour
{
    void Update()
    {
        var tis = TISInputBridge.Instance;
        if (tis == null) return;

        // Thiết bị người chơi đang dùng (tự đổi khi họ cầm thiết bị khác)
        if (tis.IsGamepad)        ShowGamepadIcons();
        else if (tis.IsMobile)    ShowTouchControls();
        else                      ShowKeyboardIcons();
    }
}";

        public override string[] Objectives => new[]
        {
            "Mở tab Code mẫu ở cột bên phải",
            "Bấm Sao chép để copy đoạn code",
            "Bấm một phím hoặc nút bất kỳ — xem ActiveDevice đổi",
        };

        public override string FooterHint => "Bấm Bài tiếp theo khi xong · Tiến độ được lưu tự động";

        TISActiveDevice _startDevice;
        bool _sawInput;

        protected override void OnEnter()
        {
            _startDevice = TIS != null ? TIS.ActiveDevice : TISActiveDevice.KeyboardMouse;
            _sawInput = false;

            Watch("ActiveDevice",    () => TIS != null ? TIS.ActiveDevice.ToString() : "—");
            Watch("IsKeyboardMouse", () => TIS != null ? F(TIS.IsKeyboardMouse) : "—");
            Watch("IsGamepad",       () => TIS != null ? F(TIS.IsGamepad) : "—");
            Watch("IsMobile",        () => TIS != null ? F(TIS.IsMobile) : "—");
            Watch("ActiveProfile",   () => TIS != null && TIS.OriginalProfile != null ? TIS.OriginalProfile.name : "—");
        }

        protected override void OnTick()
        {
            var view = Controller.View;
            if (view != null && view.codePage != null && view.codePage.activeInHierarchy) Complete(0);

            // Clipboard đọc ~2 lần/giây là đủ
            _clipTimer -= Time.unscaledDeltaTime;
            if (_clipTimer <= 0f)
            {
                _clipTimer = 0.5f;
                if (GUIUtility.systemCopyBuffer == Code) Complete(1);
            }

            if (TIS != null && !_sawInput)
            {
                var gp = UnityEngine.InputSystem.Gamepad.current;
                bool anyInput = UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true
                             || UnityEngine.InputSystem.Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true
                             || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame
                                                || gp.startButton.wasPressedThisFrame))
                             || TIS.ActiveDevice != _startDevice;
                if (anyInput) { _sawInput = true; Complete(2); }
            }
        }
    }
}
