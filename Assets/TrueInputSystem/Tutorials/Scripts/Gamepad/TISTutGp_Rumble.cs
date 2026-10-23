// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Rung (TryRumble / StopRumble)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Rung")]
    public class TISTutGp_Rumble : TISTutLesson
    {
        public override string Title => "Rung tay cầm";

        public override string Summary =>
            "TryRumble(motor trái, motor phải, thời gian) rung một lúc rồi tự dừng; bỏ tham số thời gian thì rung tới khi gọi StopRumble(). Motor trái (tần số thấp) rung nặng, motor phải (tần số cao) rung lăn tăn. Người chơi tắt rung trong profile thì mọi lệnh tự bị bỏ qua.";

        public override string[] SetupSteps => new[]
        {
            "Gamepad Settings › <b>Enable Rumble</b> — nối với tuỳ chọn \"Rung\" trong menu cài đặt của game.",
            "Gọi an toàn khi không có tay cầm hoặc tay cầm không hỗ trợ rung — TIS tự bỏ qua.",
            "DualShock 4 / DualSense kết nối Bluetooth: Unity không gửi được lệnh rung → TIS rung qua <b>SDL3</b> (Motion Settings › <b>Use SDL3 On PC</b>). Tay XInput (Xbox, Flydigi) rung qua Unity.",
            "Luôn gọi <b>StopRumble()</b> khi mở menu, tạm dừng hay thoát game để tay cầm không rung mãi.",
        };

        public override string Code =>
@"// Trúng đòn: rung nặng 0.3 giây
tis.TryRumble(0.8f, 0.3f, 0.3f);

// Động cơ xe: rung theo ga tới khi dừng
float gas = tis.GetAxisAction(""Throttle"");
tis.TryRumble(gas * 0.4f, gas);

// Mở menu / tạm dừng
tis.StopRumble();";

        public override string[] Objectives => new[]
        {
            $"Nhấn {TISTutGlyphs.West}: rung nhẹ",
            $"Nhấn {TISTutGlyphs.North}: rung mạnh",
            $"Giữ {TISTutGlyphs.RB} rồi bóp {TISTutGlyphs.RT} để rung theo cò, thả {TISTutGlyphs.RB} để dừng",
        };

        public override string FooterHint => "Không có cảm giác rung? Kiểm tra tay cầm có hỗ trợ rung và Enable Rumble trong profile";

        float _low, _high, _until;
        bool  _continuous, _didContinuous;
        TISTutShape _lowFill, _highFill;

        protected override void OnEnter()
        {
            GamepadCamera();
            _low = _high = 0f; _until = 0f; _continuous = _didContinuous = false;

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.8f);
            var card = TISTutUI.Card(Overlay, "Motors", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(340f, 150f));
            _lowFill  = Meter(card.transform, "MOTOR TRÁI · NẶNG",     40f);
            _highFill = Meter(card.transform, "MOTOR PHẢI · LĂN TĂN",  92f);

            Watch("enableRumble", () => TIS.TISGamepadSettings != null ? F(TIS.TISGamepadSettings.enableRumble) : "—");
            Watch("motor trái (low)",  () => F(_low),  () => _low);
            Watch("motor phải (high)", () => F(_high), () => _high);
            Watch("Rung liên tục", () => _continuous ? "đang rung" : "—");
        }

        TISTutShape Meter(Transform card, string label, float top)
        {
            var t = TISTutUI.Text(card, "Label", label, Theme.monoBold, 14, Theme.inkSoft, TextAlignmentOptions.TopLeft);
            TISTutUI.Place(t.rectTransform, 18f, top - 26f, 18f, -1f, height: 22f);
            var track = TISTutUI.Shape(card, "Track", Theme.track, 9f);
            TISTutUI.Place(track.rectTransform, 18f, top, 18f, -1f, height: 18f);
            var fill = TISTutUI.Shape(track.transform, "Fill", Theme.AccentStrong(), 9f);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.001f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            return fill;
        }

        protected override void OnTick()
        {
            var tis = TIS;

            if (tis.IsSkillDown("Interact")) RumbleOnce(0.15f, 0.35f, 0.2f, 0, "Rung nhẹ");
            if (tis.IsSkillDown("Toggle"))   RumbleOnce(1f, 0.6f, 0.45f, 1, "Rung mạnh!");

            if (tis.IsSkillHeld("Bumper"))
            {
                float gas = tis.GetAxisAction("Throttle");
                _low = gas * 0.5f; _high = gas;
                tis.TryRumble(_low, _high);
                _continuous = true;
                if (gas > 0.3f) _didContinuous = true;
            }
            else if (_continuous)
            {
                tis.StopRumble();
                _continuous = false;
                _low = _high = 0f;
                if (_didContinuous) Complete(2);
            }
            else if (Time.unscaledTime > _until)
            {
                _low = _high = 0f;
            }

            SetFill(_lowFill, _low);
            SetFill(_highFill, _high);
        }

        void RumbleOnce(float low, float high, float seconds, int objective, string toast)
        {
            TIS.TryRumble(low, high, seconds);
            _low = low; _high = high;
            _until = Time.unscaledTime + seconds;
            Toast(toast);
            Complete(objective);
        }

        static void SetFill(TISTutShape fill, float v)
            => fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, Mathf.Clamp01(v)), 1f);

        protected override void OnExit() => TIS.StopRumble();
    }
}
