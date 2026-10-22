// ============================================================
//  TrueInputSystem Tutorials — Mobile · Áp dụng: săn sao bằng cảm ứng
// ============================================================

namespace GaMo.TrueInputSystem.Tutorials
{
    [UnityEngine.AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Áp dụng vào game")]
    public class TISTutMb_MiniGame : TISTutKbm_MiniGame
    {
        public override string Summary =>
            "Cùng mini-game với 2 scene kia — script nhân vật và camera không đổi một dòng. Kéo nửa trái để đi, vuốt nửa phải để xoay, nút NHẢY để nhảy. Nhặt 10 ngôi sao trong 60 giây.";

        public override string[] SetupSteps => new[]
        {
            "TISTutPlayer / TISTutOrbitCamera chỉ đọc MoveDirection, LookDelta, IsSkillDown — joystick ảo, camera zone và nút kỹ năng đều ghi vào cùng các API đó.",
            "Xây bản mobile = thêm <b>TISMobileInputSetup</b> + vài <b>TISMobileSkillButton</b> và điền <b>Binding Mobile</b> trong profile.",
        };

        public override string Code =>
@"// Giống hệt bản bàn phím / tay cầm
Vector2 mv   = tis.MoveDirection;  // joystick
Vector2 look = tis.LookDelta;      // vuốt
bool jump    = tis.IsSkillDown(""Jump""); // nút";

        public override string FooterHint => "Kéo nửa trái đi · vuốt nửa phải xoay · NHẢY ở góc phải";

        protected override void SetupControls()
        {
            Hud.Show(showJump: true);
            Player.CanJump = true;
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
        }
    }
}
