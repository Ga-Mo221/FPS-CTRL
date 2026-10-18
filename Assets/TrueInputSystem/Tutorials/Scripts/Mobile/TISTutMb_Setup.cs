// ============================================================
//  TrueInputSystem Tutorials — Mobile · Bật điều khiển cảm ứng (TISMobileInputSetup)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Bật điều khiển cảm ứng")]
    public class TISTutMb_Setup : TISTutLesson
    {
        public override string Title => "Bật điều khiển cảm ứng";

        public override string Summary =>
            "Thêm TISMobileInputSetup vào scene là có ngay joystick ảo (nửa trái), vùng vuốt xoay camera (nửa phải) và lớp nhận chạm toàn màn hình. Trên điện thoại tự bật; trong Editor bật Simulate Mobile In Editor trên bridge để chuột đóng vai ngón tay.";

        public override string[] SetupSteps => new[]
        {
            "Tools › True Input System › <b>Setup Tool</b>, hoặc thêm <b>TISMobileInputSetup</b> vào một GameObject trống.",
            "TISInputBridge › bật <b>Simulate Mobile In Editor</b>, rồi chạy trong cửa sổ <b>Device Simulator</b> — TIS chỉ giả lập mobile ở đó. (Scene hướng dẫn này có thêm <b>TISTutForceMobile</b> để chạy được cả trong Game view.)",
            "Profile › <b>Mobile Settings</b>: bán kính joystick, joystick động / cố định, tỉ lệ vùng camera.",
        };

        public override string Code =>
@"var tis = TISInputBridge.Instance;
if (tis.IsMobile) ShowTouchHints();

var setup =
    FindAnyObjectByType<TISMobileInputSetup>();
setup.SetupCompleted += s =>
    Debug.Log(""Joystick: "" + s.Joystick);

// Đang giả lập mobile trong Editor?
bool sim = tis.SimulateMobileInEditor();";

        public override string[] Objectives => new[]
        {
            "Chạm (click) vào sân chơi",
            "Kéo ở nửa trái — joystick hiện ra dưới ngón tay",
            "Kéo ở nửa phải — camera xoay theo",
        };

        public override string FooterHint => "Trong Editor: click + kéo chuột = một ngón tay";

        TISMobileInputSetup _setup;

        protected override void OnEnter()
        {
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _setup = FindAnyObjectByType<TISMobileInputSetup>();

            Watch("IsMobile", () => F(TIS.IsMobile));
            Watch("ActiveDevice", () => TIS.ActiveDevice.ToString());
            Watch("SimulateMobileInEditor()", () => F(TIS.SimulateMobileInEditor()));
            Watch("setup.IsReady", () => _setup != null ? F(_setup.IsReady) : "không có setup");
            Watch("Joystick.IsDragging", () => _setup != null && _setup.Joystick != null ? F(_setup.Joystick.IsDragging) : "—");
            Watch("CameraZone.PointerCount", () => TISTouchCameraZone.Current != null ? TISTouchCameraZone.Current.PointerCount.ToString() : "—");
        }

        protected override void OnTick()
        {
            var ptr = UnityEngine.InputSystem.Pointer.current;
            if (ptr != null && ptr.press.wasPressedThisFrame && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                Complete(0);
            if (UnityEngine.InputSystem.Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) Complete(0);

            if (_setup != null && _setup.Joystick != null && _setup.Joystick.IsDragging) { Complete(0); Complete(1); }
            if (Cam.TotalYaw > 30f) { Complete(0); Complete(2); }
        }
    }
}
