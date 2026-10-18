// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Nhận diện thiết bị (ActiveDevice)
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Nhận diện")]
    public class TISTutGp_Detect : TISTutLesson
    {
        public override string Title => "Nhận diện tay cầm";

        public override string Summary =>
            "TIS tự biết người chơi đang cầm thiết bị nào: chạm tay cầm → ActiveDevice = Gamepad, chạm bàn phím / chuột → KeyboardMouse. Dùng để đổi biểu tượng nút trên UI, gợi ý đúng phím, ẩn / hiện con trỏ chuột — ngay khi người chơi đổi, không cần vào menu cài đặt.";

        public override string[] SetupSteps => new[]
        {
            "Profile › bật <b>Enable Gamepad</b> và điền <b>Binding Gamepad</b> cho từng action.",
            "Đọc <b>ActiveDevice</b> / <b>IsGamepad</b> khi cập nhật UI — rất rẻ, gọi mỗi frame cũng được.",
            "Cắm / rút tay cầm lúc đang chơi: Input System tự nhận, TIS đổi thiết bị theo lần bấm gần nhất.",
        };

        public override string Code =>
@"var tis = TISInputBridge.Instance;

hint.text = tis.IsGamepad
    ? ""Nhấn A để nhảy""
    : ""Nhấn Space để nhảy"";

switch (tis.ActiveDevice)
{
    case TISActiveDevice.Gamepad:
        ShowPadIcons(); break;
    case TISActiveDevice.KeyboardMouse:
        ShowKeyIcons(); break;
}";

        public override string[] Objectives => new[]
        {
            "Bấm một nút trên tay cầm — ActiveDevice = Gamepad",
            "Bấm một phím trên bàn phím — đổi sang KeyboardMouse",
            "Bấm tay cầm lần nữa để đổi lại",
        };

        public override string FooterHint => "Thay phiên bấm tay cầm và bàn phím — xem gợi ý và chip \"Đang dùng\" đổi theo";

        TMP_Text _hint;
        TISActiveDevice _last;

        protected override void OnEnter()
        {
            Player.CanJump = true;
            GamepadCamera();
            _last = TIS.ActiveDevice;

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f));

            var card = TISTutUI.Card(Overlay, "Hint", new Vector2(1f, 0f), new Vector2(-240f, 24f), new Vector2(380f, 80f), Theme.ink, 16f);
            _hint = TISTutUI.Text(card.transform, "Text", "", Theme.bodyBold, 22, Theme.Accent(), TextAlignmentOptions.Center);
            TISTutUI.Stretch(_hint.rectTransform);

            Watch("ActiveDevice", () => TIS.ActiveDevice.ToString());
            Watch("IsGamepad", () => F(TIS.IsGamepad));
            Watch("IsKeyboardMouse", () => F(TIS.IsKeyboardMouse));
            Watch("Tay cầm (Input System)", () => TISTutGlyphs.DeviceName);
            Watch("Số tay cầm đang cắm", () => Gamepad.all.Count.ToString());
        }

        protected override void OnTick()
        {
            var dev = TIS.ActiveDevice;
            _hint.text = TIS.IsGamepad ? $"Nhấn {TISTutGlyphs.South} để nhảy" : "Nhấn Space để nhảy";

            if (dev != _last)
            {
                if (dev == TISActiveDevice.Gamepad && !IsObjectiveDone(0)) Complete(0);
                else if (dev == TISActiveDevice.KeyboardMouse && IsObjectiveDone(0)) Complete(1);
                else if (dev == TISActiveDevice.Gamepad && IsObjectiveDone(1)) Complete(2);
                _last = dev;
            }
            else if (dev == TISActiveDevice.Gamepad && Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
            {
                // Đang là tay cầm từ trước khi vào bài → vẫn tính nhiệm vụ đầu
                if (!IsObjectiveDone(0) && AnyPadButton()) Complete(0);
                if (IsObjectiveDone(1) && AnyPadButton()) Complete(2);
            }
        }

        static bool AnyPadButton()
        {
            var gp = Gamepad.current;
            return gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame
                               || gp.buttonWest.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame);
        }
    }
}
