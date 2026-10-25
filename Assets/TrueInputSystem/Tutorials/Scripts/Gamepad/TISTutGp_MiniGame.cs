// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Áp dụng: săn sao (rung khi nhặt)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Áp dụng vào game")]
    public class TISTutGp_MiniGame : TISTutKbm_MiniGame
    {
        public override string Summary =>
            "Cùng mini-game với scene Chuột + Bàn phím — script nhân vật và camera không đổi một dòng. Cần trái đi, cần phải (+ gyro, + touchpad) xoay, A nhảy, nhấn cần trái để chạy. Tay cầm rung nhẹ mỗi lần nhặt sao.";

        public override string[] SetupSteps => new[]
        {
            "Mở <b>TISTutPlayer.cs</b> / <b>TISTutOrbitCamera.cs</b>: chỉ đọc MoveDirection, LookDelta, IsSkillDown — không có dòng nào riêng cho tay cầm.",
            "Phản hồi xúc giác: <b>TryRumble</b> ngắn khi nhặt đồ, trúng đòn, tiếp đất mạnh.",
        };

        public override string Code =>
@"// Giống hệt bản bàn phím
Vector2 mv  = tis.MoveDirection;
Vector2 look = tis.LookDelta;
bool jump   = tis.IsSkillDown(""Jump"");
bool run    = tis.IsSkillHeld(""Run"");

// Thêm cảm giác khi nhặt sao
tis.TryRumble(0.2f, 0.5f, 0.12f);";

        public override string FooterHint => "Cần trái đi · nhấn cần trái chạy · A nhảy · cần phải / gyro xoay";

        protected override void SetupControls()
        {
            Player.CanJump = true;
            Player.CanRun = true;
            GamepadCamera();
        }

        protected override void OnStarCollected(int total)
            => TIS.TryRumble(0.2f, 0.5f, total >= 10 ? 0.5f : 0.12f);
    }
}
