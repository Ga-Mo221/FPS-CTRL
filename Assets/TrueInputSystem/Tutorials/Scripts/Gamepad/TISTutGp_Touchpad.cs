// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Touchpad (Look + Touchpad, PadClick, Motion)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Touchpad")]
    public class TISTutGp_Touchpad : TISTutLesson
    {
        public override string Title => "Touchpad: vuốt & bấm";

        public override string Summary =>
            "Touchpad của DualShock 4 / DualSense vừa là bàn rê (vuốt để xoay camera, kéo con trỏ) vừa là một nút bấm lớn. Bật \"+ Touchpad\" cho Look để vuốt xoay camera; action <TISMotion>/touchpadPress dùng như nút thường để mở bản đồ, chụp ảnh...";

        public override string[] SetupSteps => new[]
        {
            "Action <b>Look</b> › bật <b>+ Touchpad</b>. Độ nhạy: Motion Settings › <b>Touchpad Look Sensitivity</b>.",
            "Action <b>PadClick</b> (Button) = <b><TISMotion>/touchpadPress</b>.",
            "Vị trí ngón tay: <b>Motion.TouchpadPosition</b> (0..1, gốc dưới-trái), đang chạm: <b>Motion.IsTouching</b>.",
            "Editor không có tay cầm: giữ <b>T</b> + rê chuột = vuốt, <b>G</b> = bấm touchpad.",
        };

        public override string Code =>
@"if (tis.IsSkillDown(""PadClick""))
    ToggleMap();

var m = tis.Motion;
if (m.HasTouchpad && m.IsTouching)
    finger.anchoredPosition =
        m.TouchpadPosition * padSize;

// Tắt vuốt touchpad cho camera
tis.SetActionTouchpadEnabled(""Look"", false);";

        public override string[] Objectives => new[]
        {
            "Vuốt touchpad để xoay camera",
            "Bấm touchpad để mở bản đồ",
            $"Nhấn {TISTutGlyphs.North} tắt touchpad cho Look, rồi bật lại",
        };

        public override string FooterHint => "Vuốt để xoay · bấm touchpad mở bản đồ · Editor: giữ T + rê chuột, G = bấm";

        RectTransform _pad, _finger;
        GameObject _map;
        float _swipe;
        bool _wasOff;

        protected override void OnEnter()
        {
            GamepadCamera();
            _swipe = 0f; _wasOff = false;
            TIS.SetActionTouchpadEnabled("Look", true);

            // Hình touchpad
            var card = TISTutUI.Card(Overlay, "Pad", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(330f, 200f));
            var t = TISTutUI.Text(card.transform, "Title", "TOUCHPAD", Theme.monoBold, 14, Theme.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.Place(t.rectTransform, 8f, 10f, 8f, -1f, height: 22f);
            var pad = TISTutUI.Shape(card.transform, "Surface", Theme.paper, 16f, 2f, Theme.ink);
            TISTutUI.Place(pad.rectTransform, 18f, 40f, 18f, 18f);
            _pad = pad.rectTransform;
            var f = TISTutUI.Shape(pad.transform, "Finger", Theme.AccentStrong(), 16f);
            _finger = f.rectTransform;
            _finger.anchorMin = _finger.anchorMax = Vector2.zero;
            _finger.pivot = new Vector2(0.5f, 0.5f);
            _finger.sizeDelta = new Vector2(32f, 32f);

            // Bản đồ (mở bằng PadClick)
            var map = TISTutUI.Card(Overlay, "Map", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 260f), Theme.ink, 20f);
            var mt = TISTutUI.Text(map.transform, "Text", "BẢN ĐỒ\n<size=60%>bấm touchpad lần nữa để đóng</size>", Theme.monoBold, 34, Theme.Accent(), TextAlignmentOptions.Center);
            TISTutUI.Stretch(mt.rectTransform);
            _map = map.gameObject;
            _map.SetActive(false);

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.7f);

            var m = TIS.Motion;
            Watch("Motion.HasTouchpad", () => F(m.HasTouchpad));
            Watch("Motion.IsTouching", () => F(m.IsTouching));
            Watch("Motion.TouchpadPosition", () => F(m.TouchpadPosition));
            Watch("Motion.TouchpadVelocity", () => F(m.TouchpadVelocity), () => Mathf.Clamp01(m.TouchpadVelocity.magnitude / 3f));
            Watch("IsActionTouchpadEnabled(\"Look\")", () => F(TIS.IsActionTouchpadEnabled("Look")));
        }

        protected override void OnTick()
        {
            var tis = TIS;
            var m = tis.Motion;

            _finger.gameObject.SetActive(m.IsTouching);
            if (m.IsTouching)
            {
                Vector2 size = _pad.rect.size;
                _finger.anchoredPosition = new Vector2(m.TouchpadPosition.x * size.x, m.TouchpadPosition.y * size.y);
                if (tis.IsActionTouchpadEnabled("Look")) _swipe += m.TouchpadDelta.magnitude;
                if (_swipe >= 1f) Complete(0);
            }

            if (tis.IsSkillDown("PadClick"))
            {
                _map.SetActive(!_map.activeSelf);
                Complete(1);
            }

            if (tis.IsSkillDown("Toggle"))
            {
                bool on = !tis.IsActionTouchpadEnabled("Look");
                tis.SetActionTouchpadEnabled("Look", on);
                Toast(on ? "Touchpad cho camera: BẬT" : "Touchpad cho camera: TẮT");
                if (!on) _wasOff = true;
                else if (_wasOff) Complete(2);
            }
        }

        protected override void OnExit() => TIS.SetActionTouchpadEnabled("Look", true);
    }
}
