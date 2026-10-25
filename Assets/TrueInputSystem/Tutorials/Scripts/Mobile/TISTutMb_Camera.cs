// ============================================================
//  TrueInputSystem Tutorials — Mobile · Vuốt xoay camera (TISTouchCameraZone)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Vuốt xoay camera")]
    public class TISTutMb_Camera : TISTutLesson
    {
        public override string Title => "Vuốt xoay camera";

        public override string Summary =>
            "Vuốt ở nửa phải màn hình → LookDelta (đã nhân Mobile Sensitivity). Chụm / mở 2 ngón → ZoomDelta — cùng API với chuột. Mỗi ngón tay chỉ thuộc một control: ngón đang giữ joystick không làm xoay camera và ngược lại.";

        public override string[] SetupSteps => new[]
        {
            "Action <b>Look</b> · Binding Mobile = <b>CameraZone</b>. Mobile Settings › <b>Camera Zone Width Ratio</b> = phần màn hình dành cho camera.",
            "Camera Settings › <b>Mobile Sensitivity</b>, <b>Invert Y</b>.",
            "Chụm 2 ngón cần màn hình cảm ứng thật (Unity Remote 5 hoặc bản build) — chuột trong Editor chỉ là 1 ngón.",
        };

        public override string Code =>
@"Vector2 look = tis.LookDelta;  // vuốt
float zoom   = tis.ZoomDelta;  // chụm 2 ngón

var zone = TISTouchCameraZone.Current;
if (zone != null && zone.IsPinching)
    ShowZoomHint();

// Ngón đang giữ camera zone
int fingers = zone.PointerCount;";

        public override string[] Objectives => new[]
        {
            "Vuốt ngang để xoay camera 180°",
            "Vuốt dọc để ngẩng hoặc cúi camera",
            "Chụm / mở 2 ngón để zoom (điện thoại)",
        };

        public override string FooterHint => "Kéo bất kỳ đâu trên sân chơi để xoay (joystick tắt trong bài này)";

        float _lastZoom, _lastZoomTime = -9f;

        protected override void OnEnter()
        {
            Cam.LookEnabled = true;
            Cam.ZoomEnabled = true;
            Cam.RequireRightMouse = false;
            SetMobileJoystick(false);      // bài này chỉ vuốt camera — cả sân chơi là vùng camera

            Watch("LookDelta", () => F(TIS.LookDelta), () => Mathf.Clamp01(TIS.LookDelta.magnitude / 3f));
            Watch("ZoomDelta", () => Time.unscaledTime - _lastZoomTime < 0.4f ? F(_lastZoom) : "0.00");
            Watch("zone.RawDelta", () => TISTouchCameraZone.Current != null ? F(TISTouchCameraZone.Current.RawDelta) : "—");
            Watch("zone.PointerCount", () => TISTouchCameraZone.Current != null ? TISTouchCameraZone.Current.PointerCount.ToString() : "—");
            Watch("zone.IsPinching", () => TISTouchCameraZone.Current != null ? F(TISTouchCameraZone.Current.IsPinching) : "—");
            Watch("Camera đã xoay", () => Mathf.Min(180f, Cam.TotalYaw).ToString("0") + " / 180°", () => Cam.TotalYaw / 180f);
        }

        protected override void OnTick()
        {
            float z = TIS.ZoomDelta;
            if (z != 0f) { _lastZoom = z; _lastZoomTime = Time.unscaledTime; }

            if (Cam.TotalYaw >= 180f) Complete(0);
            if (Cam.MinPitch <= -20f || Cam.MaxPitch >= 50f) Complete(1);

            var zone = TISTouchCameraZone.Current;
            if (zone != null && zone.IsPinching) Complete(2);
        }
    }
}
