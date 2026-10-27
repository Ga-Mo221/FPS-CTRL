// ============================================================
//  TrueInputSystem Tutorials — Mobile · Tay cầm Bluetooth trên điện thoại
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Tay cầm Bluetooth")]
    public class TISTutMb_Gamepad : TISTutLesson
    {
        public override string Title => "Tay cầm Bluetooth trên điện thoại";

        public override string Summary =>
            "Nhiều người chơi mobile cắm tay cầm Bluetooth. TIS tự chuyển ActiveDevice: bấm tay cầm → Gamepad, chạm màn hình → Mobile. Ẩn nút cảm ứng khi đang cầm tay cầm cho màn hình gọn — code nhân vật vẫn giữ nguyên.";

        public override string[] SetupSteps => new[]
        {
            "Mỗi action điền cả <b>Binding Mobile</b> lẫn <b>Binding Gamepad</b> trong profile.",
            "So <b>ActiveDevice</b> mỗi frame để ẩn / hiện HUD cảm ứng và đổi gợi ý nút.",
            "Tay cầm trên mobile vẫn điều hướng được menu <b>TISGamepadUIButton</b> và dùng được con trỏ ảo.",
        };

        public override string Code =>
@"bool pad = tis.ActiveDevice
           == TISActiveDevice.Gamepad;

touchHud.SetActive(!pad);
padHints.SetActive(pad);

// Nhân vật: không cần đổi gì
Vector2 mv = tis.MoveDirection;";

        public override string[] Objectives => new[]
        {
            "Bấm một nút tay cầm — nút cảm ứng ẩn đi",
            "Đi bằng cần trái của tay cầm",
            "Chạm màn hình — nút cảm ứng hiện lại",
        };

        public override string FooterHint => "Cần một tay cầm đang kết nối (Editor: cắm tay cầm vào PC)";

        bool _wasPad;

        protected override void OnEnter()
        {
            Hud.Show(showJump: true, showAttack: true);
            Player.CanJump = true;
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _wasPad = TIS.IsGamepad;

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.6f);

            Watch("ActiveDevice", () => TIS.ActiveDevice.ToString());
            Watch("IsMobile", () => F(TIS.IsMobile));
            Watch("IsGamepad", () => F(TIS.IsGamepad));
            Watch("HUD cảm ứng", () => TIS.IsGamepad ? "ẩn" : "hiện");
            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
        }

        protected override void OnTick()
        {
            bool pad = TIS.IsGamepad;
            Hud.SetVisible(!pad);

            if (pad) Complete(0);
            if (pad && TIS.MoveDirection.magnitude > 0.5f) Complete(1);
            if (_wasPad && !pad && IsObjectiveDone(0)) Complete(2);
            _wasPad = pad;
        }

        protected override void OnExit() => Hud?.SetVisible(true);
    }
}
