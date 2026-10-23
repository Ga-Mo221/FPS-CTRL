// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · GetSkillAction & ReadMode
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · 3 chế độ đọc nút")]
    public class TISTutKbm_ReadMode : TISTutLesson
    {
        public override string Title => "3 chế độ đọc nút (Read Mode)";

        public override string Summary =>
            "GetSkillAction(tên) đọc nút theo Read Mode đặt sẵn trong profile — đổi cảm giác bấm mà không sửa code. Cần kiểu khác ngay trong code thì truyền thêm TISButtonReadMode. Ba đèn bên dưới cùng đọc action Interact (phím F) theo 3 kiểu.";

        public override string[] SetupSteps => new[]
        {
            "Profile › action <b>Interact</b> › <b>Read Mode</b>: PressDown (1 frame khi nhấn) · Hold (suốt lúc giữ) · PressUp (1 frame khi thả).",
            "<b>GetSkillAction(tên)</b> dùng Read Mode đó; <b>GetSkillAction(tên, mode)</b> bỏ qua profile.",
            "Thuộc tính tắt: <b>IsJumping</b> / <b>IsRunning</b> / <b>IsAttacking</b> = GetSkillAction(\"Jump\" / \"Run\" / \"Attack\").",
        };

        public override string Code =>
@"// Theo Read Mode trong profile
bool a = tis.GetSkillAction(""Interact"");

// Ép chế độ đọc trong code
bool down = tis.GetSkillAction(""Interact"",
    TISButtonReadMode.PressDown);
bool hold = tis.GetSkillAction(""Interact"",
    TISButtonReadMode.Hold);
bool up   = tis.GetSkillAction(""Interact"",
    TISButtonReadMode.PressUp);

// Thuộc tính tắt
if (tis.IsJumping) Jump();";

        public override string[] Objectives => new[]
        {
            "Làm sáng đèn PressDown (nhấn F)",
            "Giữ đèn Hold sáng liên tục 1 giây",
            "Làm sáng đèn PressUp (thả F)",
        };

        public override string FooterHint => "Nhấn, giữ rồi thả phím F — để ý đèn nào sáng, sáng bao lâu";

        readonly Pulse _down = new Pulse(), _up = new Pulse();
        TISTutShape _lampDown, _lampHold, _lampUp;
        float _holdTime;

        protected override void OnEnter()
        {
            _holdTime = 0f;
            var group = TISTutUI.Rect(Overlay, "Lamps");
            TISTutUI.At(group, new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(3 * 210f + 2 * 16f, 150f));
            _lampDown = Lamp(group, 0, "PressDown", "1 frame khi nhấn");
            _lampHold = Lamp(group, 1, "Hold",      "suốt lúc giữ");
            _lampUp   = Lamp(group, 2, "PressUp",   "1 frame khi thả");

            KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("F", "<Keyboard>/f", 64f));

            Watch("GetSkillAction(\"Interact\")", () => _down.Text);
            Watch("… PressDown", () => _down.Text);
            Watch("… Hold",      () => F(TIS.GetSkillAction("Interact", TISButtonReadMode.Hold)));
            Watch("… PressUp",   () => _up.Text);
            Watch("IsJumping (Space)", () => F(TIS.IsJumping));
        }

        TISTutShape Lamp(RectTransform parent, int i, string mode, string note)
        {
            var card = TISTutUI.Card(parent, mode, new Vector2(0f, 0f), new Vector2(i * 226f, 0f), new Vector2(210f, 150f));
            var lamp = TISTutUI.Shape(card.transform, "Lamp", Theme.track, 22f);
            TISTutUI.At(lamp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(44f, 44f));
            var t = TISTutUI.Text(card.transform, "Mode", mode, Theme.monoBold, 19, Theme.ink, TextAlignmentOptions.Center);
            TISTutUI.Place(t.rectTransform, 8f, 72f, 8f, -1f, height: 28f);
            var n = TISTutUI.Text(card.transform, "Note", note, Theme.body, 15, Theme.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.Place(n.rectTransform, 8f, 104f, 8f, -1f, height: 24f);
            return lamp;
        }

        protected override void OnTick()
        {
            var tis = TIS;
            bool down = tis.GetSkillAction("Interact", TISButtonReadMode.PressDown);
            bool hold = tis.GetSkillAction("Interact", TISButtonReadMode.Hold);
            bool up   = tis.GetSkillAction("Interact", TISButtonReadMode.PressUp);

            if (down) { _down.Hit(); Complete(0); }
            if (up)   { _up.Hit();   Complete(2); }
            _holdTime = hold ? _holdTime + Time.deltaTime : 0f;
            if (_holdTime >= 1f) Complete(1);

            Color off = Theme.track, on = Theme.AccentStrong();
            _lampDown.color = _down.On ? on : off;
            _lampHold.color = hold ? on : off;
            _lampUp.color   = _up.On ? on : off;
        }
    }
}
