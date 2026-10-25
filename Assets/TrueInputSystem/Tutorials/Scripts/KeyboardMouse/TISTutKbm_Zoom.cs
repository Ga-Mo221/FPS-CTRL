// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Zoom (ZoomDelta)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Zoom")]
    public class TISTutKbm_Zoom : TISTutLesson
    {
        public override string Title => "Zoom bằng con lăn chuột";

        public override string Summary =>
            "ZoomDelta là lượng cuộn của frame này (dương = cuộn lên). Trên mobile cùng API này trả về cử chỉ chụm 2 ngón — code zoom viết một lần dùng cho cả hai. Zoom tự tắt khi action map chứa Look bị tắt.";

        public override string[] SetupSteps => new[]
        {
            "Không cần tạo action — TIS đọc thẳng con lăn của chuột thật (bỏ qua chuột ảo của virtual cursor).",
            "Windows trả về ±120 cho mỗi nấc cuộn, một số nền tảng trả ±1 — chuẩn hoá như code mẫu.",
        };

        public override string Code =>
@"float z = tis.ZoomDelta;
if (z != 0)
{
    // Windows: ±120 mỗi nấc
    float notch = Mathf.Abs(z) > 10
        ? z / 120f : z;

    _distance = Mathf.Clamp(
        _distance - notch * step,
        minDistance, maxDistance);
}";

        public override string[] Objectives => new[]
        {
            "Cuộn lên để phóng to sát nhân vật",
            "Cuộn xuống để thu nhỏ ra thật xa",
        };

        public override string FooterHint => "Con lăn chuột để zoom · giữ chuột phải để xoay";

        float _last;
        float _lastTime = -9f;

        protected override void OnEnter()
        {
            Cam.LookEnabled = true;
            Cam.ZoomEnabled = true;

            KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("CON LĂN", "<Mouse>/scroll", 150f));

            Watch("ZoomDelta", () => Time.unscaledTime - _lastTime < 0.4f ? F(_last) : "0.00");
            Watch("Khoảng cách camera", () => Cam.distance.ToString("0.0") + " m",
                  () => Mathf.InverseLerp(Cam.minDistance, Cam.maxDistance, Cam.distance));
        }

        protected override void OnTick()
        {
            float z = TIS.ZoomDelta;
            if (z != 0f) { _last = z; _lastTime = Time.unscaledTime; }

            if (Cam.distance <= Cam.minDistance + 0.5f) Complete(0);
            if (Cam.distance >= Cam.maxDistance - 0.5f) Complete(1);
        }
    }
}
