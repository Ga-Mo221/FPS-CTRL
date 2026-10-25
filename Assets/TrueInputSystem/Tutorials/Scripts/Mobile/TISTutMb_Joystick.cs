// ============================================================
//  TrueInputSystem Tutorials — Mobile · Joystick ảo (TISVirtualJoystick)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Joystick ảo")]
    public class TISTutMb_Joystick : TISTutLesson
    {
        public override string Title => "Joystick ảo";

        public override string Summary =>
            "Joystick ảo ghi thẳng vào MoveDirection — code di chuyển dùng chung với WASD và tay cầm. Chế độ động: joystick hiện ra đúng chỗ ngón cái chạm, hợp với mọi cỡ tay. Chế độ cố định: luôn nằm một chỗ, dễ nhớ cho người mới.";

        public override string[] SetupSteps => new[]
        {
            "Mobile Settings: <b>Joystick Radius</b>, <b>Joystick Deadzone</b>, <b>Dynamic Joystick</b>, <b>Idle Opacity</b>.",
            "Action <b>Move</b> · Binding Mobile = <b>TISVirtualJoystick</b>.",
            "Đổi lúc chạy (vd. trong menu Cài đặt): <b>Joystick.SetDynamicMode()</b>, <b>SetRadius()</b>, <b>SetActiveOpacity()</b>.",
        };

        public override string Code =>
@"Vector2 mv = tis.MoveDirection; // joystick

var joy = setup.Joystick;
joy.SetAnchoredOrigin(new Vector2(-420, -250));
joy.SetDynamicMode(false);  // cố định
joy.SetIdleOpacity(0.9f);   // luôn hiện rõ
joy.SetRadius(120f);

joy.OnInputChanged += v => Hint(v);
joy.OnReleased     += () => StopRun();";

        public override string[] Objectives => new[]
        {
            "Đi tới vòng tròn cam bằng joystick",
            "Đẩy joystick hết cỡ",
            "Bấm nút JOYSTICK để đổi giữa ĐỘNG và CỐ ĐỊNH",
        };

        public override string FooterHint => "Kéo ở nửa trái sân chơi để đi";

        TISVirtualJoystick _joy;
        TISTutZone _zone;
        bool _dynamic = true;
        TISTutButton _toggle;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            _zone = PG.Zone("Zone Cam", PG.coral, new Vector3(-5f, 0f, 5f), 1.4f, Stage);
            var setup = FindAnyObjectByType<TISMobileInputSetup>();
            _joy = setup != null ? setup.Joystick : null;
            _dynamic = true;

            _toggle = TISTutUI.Button(Overlay, "Joystick Mode", "JOYSTICK: ĐỘNG", TISTutButton.Style.Dark, 300f, 52f, null, false, 17f);
            TISTutUI.At((RectTransform)_toggle.transform, new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(300f, 52f));
            _toggle.onClick.AddListener(() =>
            {
                _dynamic = !_dynamic;
                if (_joy != null)
                {
                    // Cố định: đặt joystick ở góc dưới-trái sân chơi và luôn hiện rõ
                    if (!_dynamic) _joy.SetAnchoredOrigin(FixedOrigin);
                    _joy.SetIdleOpacity(_dynamic ? IdleDynamic() : 0.9f);
                    _joy.SetDynamicMode(_dynamic);
                }
                _toggle.Text = _dynamic ? "JOYSTICK: ĐỘNG" : "JOYSTICK: CỐ ĐỊNH";
                Complete(2);
            });

            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("Joystick.RawInput", () => _joy != null ? F(_joy.RawInput) : "—");
            Watch("Joystick.InputMagnitude", () => _joy != null ? F(_joy.InputMagnitude) : "—", () => _joy != null ? _joy.InputMagnitude : 0f);
            Watch("Joystick.IsDragging", () => _joy != null ? F(_joy.IsDragging) : "—");
        }

        // Toạ độ trên Canvas mobile (1920×1080, gốc giữa màn hình) — nửa trái, phía dưới sân chơi
        static readonly Vector2 FixedOrigin = new Vector2(-420f, -250f);

        float IdleDynamic()
        {
            var setup = FindAnyObjectByType<TISMobileInputSetup>();
            return setup != null ? setup._idleOpacityOverride : 0.6f;
        }

        protected override void OnTick()
        {
            if (_zone.Reached) Complete(0);
            if (TIS.MoveDirection.magnitude >= 0.97f) Complete(1);
        }

        protected override void OnExit()
        {
            var setup = FindAnyObjectByType<TISMobileInputSetup>();
            if (setup != null && setup.Joystick != null)
            {
                setup.Joystick.SetIdleOpacity(setup._idleOpacityOverride);
                setup.Joystick.SetDynamicMode(true);       // trả về joystick động cho các bài sau
            }
        }
    }
}
