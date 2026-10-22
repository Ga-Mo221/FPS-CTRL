// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Xoay camera (LookDelta)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Xoay camera")]
    public class TISTutKbm_Look : TISTutLesson
    {
        public override string Title => "Xoay camera bằng chuột";

        public override string Summary =>
            "LookDelta là lượng xoay của frame này — với chuột là độ dịch chuyển đã nhân Mouse Sensitivity của profile, với tay cầm là cần phải. Cộng dồn vào yaw / pitch của camera. Ở đây giữ chuột phải để xoay; game FPS thường khoá con trỏ giữa màn hình.";

        public override string[] SetupSteps => new[]
        {
            "Action <b>Look</b> (Vector2) · Binding PC = <b><Mouse>/delta</b>.",
            "Độ nhạy: profile › Camera Settings › <b>Mouse Sensitivity</b>. Đảo trục dọc: <b>Invert Y</b>.",
            "Giới hạn góc ngẩng / cúi: <b>Pitch Min / Max</b> — đọc lúc chạy qua <b>TIS.TISCameraSettings</b>.",
        };

        public override string Code =>
@"Vector2 look = tis.LookDelta;
var cs = tis.TISCameraSettings;

_yaw   += look.x * speed;
_pitch -= look.y * speed;
_pitch  = Mathf.Clamp(_pitch,
    cs.pitchMin, cs.pitchMax);

cam.rotation = Quaternion.Euler(
    _pitch, _yaw, 0);

// FPS: khoá con trỏ giữa màn hình
Cursor.lockState = CursorLockMode.Locked;";

        public override string[] Objectives => new[]
        {
            "Giữ chuột phải và xoay camera trọn một vòng 360°",
            "Ngẩng camera nhìn lên trời",
            "Cúi camera nhìn xuống đất",
        };

        public override string FooterHint => "Giữ chuột phải + rê chuột để xoay · WASD vẫn đi được";

        protected override void OnEnter()
        {
            Cam.LookEnabled = true;

            var row = KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("CHUỘT PHẢI", "<Mouse>/rightButton", 170f));

            Watch("LookDelta", () => F(TIS.LookDelta), () => Mathf.Clamp01(TIS.LookDelta.magnitude / 3f));
            Watch("yaw (camera)", () => Cam.yaw.ToString("0") + "°");
            Watch("pitch (camera)", () => Cam.pitch.ToString("0") + "°");
            Watch("Đã xoay", () => Mathf.Min(360f, Cam.TotalYaw).ToString("0") + " / 360°", () => Cam.TotalYaw / 360f);
            Watch("mouseSensitivity", () => F(TIS.TISCameraSettings.mouseSensitivity));
        }

        protected override void OnTick()
        {
            if (Cam.TotalYaw >= 360f) Complete(0);
            if (Cam.MinPitch <= -25f) Complete(1);
            if (Cam.MaxPitch >= 55f)  Complete(2);
        }
    }
}
