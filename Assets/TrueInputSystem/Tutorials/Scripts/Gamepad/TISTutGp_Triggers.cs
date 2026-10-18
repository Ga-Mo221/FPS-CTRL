// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Nút, cò (trục / nút) & tổ hợp (Gamepad Modifier)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Nút, cò & tổ hợp")]
    public class TISTutGp_Triggers : TISTutLesson
    {
        public override string Title => "Nút, cò & tổ hợp";

        public override string Summary =>
            "Cò (LT / RT) là nút analog từ 0 đến 1: đọc như trục bằng GetAxisAction (ga xe, kéo cung, độ mạnh cú ném) hoặc như nút bằng IsSkillDown (bắn). Gamepad Modifier tạo tổ hợp kiểu giữ LB rồi nhấn Y — thêm nút mà không cần thêm phím vật lý.";

        public override string[] SetupSteps => new[]
        {
            "<b>Throttle</b> (Axis) = <Gamepad>/rightTrigger · <b>Attack</b> (Button) = <Gamepad>/rightTrigger — một cò, hai cách đọc.",
            "<b>Special</b> (Button) = <Gamepad>/buttonNorth + Gamepad Modifier = <b><Gamepad>/leftShoulder</b>.",
            "TIS lấy mẫu cò một lần mỗi frame → IsSkillDown của cò không bị bắn đôi khi đọc ở nhiều script.",
        };

        public override string Code =>
@"float gas = tis.GetAxisAction(""Throttle"");
car.Accelerate(gas);          // 0..1

if (tis.IsSkillDown(""Attack"")) // RT như nút
    Fire();

if (tis.IsSkillDown(""Special"")) // LB + Y
    Ultimate();";

        public override string[] Objectives => new[]
        {
            $"Nhảy bằng {TISTutGlyphs.South}",
            $"Giữ {TISTutGlyphs.RT} ở khoảng nửa hành trình trong 1 giây",
            $"Giữ {TISTutGlyphs.LB} rồi nhấn {TISTutGlyphs.North} để tung chiêu đặc biệt",
        };

        public override string FooterHint => "Bóp cò từ từ để thấy giá trị trục · giữ LB + Y cho chiêu đặc biệt";

        readonly Pulse _attack = new Pulse(), _special = new Pulse(), _jump = new Pulse();
        float _halfTime;
        TISTutShape _fill;
        TMP_Text _value;
        Transform _ring;
        float _ringT = -1f;

        protected override void OnEnter()
        {
            Player.CanJump = true;
            GamepadCamera();

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.8f);

            // Đồng hồ ga (có vùng 40–70% tô sáng)
            var card = TISTutUI.Card(Overlay, "Throttle", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(340f, 120f));
            var t = TISTutUI.Text(card.transform, "Title", "GetAxisAction(\"Throttle\")", Theme.mono, 14, Theme.inkSoft, TextAlignmentOptions.TopLeft);
            TISTutUI.Place(t.rectTransform, 18f, 12f, 18f, -1f, height: 22f);
            _value = TISTutUI.Text(card.transform, "Value", "0.00", Theme.monoBold, 28, Theme.ink, TextAlignmentOptions.TopRight);
            TISTutUI.Place(_value.rectTransform, 18f, 34f, 18f, -1f, height: 34f);
            var track = TISTutUI.Shape(card.transform, "Track", Theme.track, 9f);
            TISTutUI.Place(track.rectTransform, 18f, -1f, 18f, 18f, height: 18f);
            var band = TISTutUI.Shape(track.transform, "Band", Theme.Accent(), 0f);
            band.rectTransform.anchorMin = new Vector2(0.4f, 0f); band.rectTransform.anchorMax = new Vector2(0.7f, 1f);
            band.rectTransform.offsetMin = band.rectTransform.offsetMax = Vector2.zero;
            _fill = TISTutUI.Shape(track.transform, "Fill", Theme.AccentStrong(), 9f);
            var frt = _fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.001f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            _ring = PG.Spawn(PrimitiveType.Cylinder, "Burst", PG.lilac, Vector3.zero, Vector3.zero, Stage, collider: false).transform;
            _ring.gameObject.SetActive(false);

            Watch("GetAxisAction(\"Throttle\")", () => F(TIS.GetAxisAction("Throttle")), () => TIS.GetAxisAction("Throttle"));
            Watch("IsSkillDown(\"Attack\")", () => _attack.Text);
            Watch("IsSkillDown(\"Special\")", () => _special.Text);
            Watch("IsSkillDown(\"Jump\")", () => _jump.Text);
        }

        protected override void OnTick()
        {
            var tis = TIS;
            float gas = tis.GetAxisAction("Throttle");
            _value.text = gas.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            _fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, gas), 1f);

            _halfTime = gas >= 0.4f && gas <= 0.7f ? _halfTime + Time.deltaTime : 0f;
            if (_halfTime >= 1f) Complete(1);

            if (tis.IsSkillDown("Jump"))   { _jump.Hit(); Complete(0); }
            if (tis.IsSkillDown("Attack")) { _attack.Hit(); Toast("Bắn!"); }
            if (tis.IsSkillDown("Special"))
            {
                _special.Hit();
                _ringT = 0f;
                _ring.gameObject.SetActive(true);
                Toast("Chiêu đặc biệt!");
                Complete(2);
            }

            if (_ringT >= 0f)
            {
                _ringT += Time.deltaTime * 1.6f;
                float s = Mathf.Lerp(1f, 9f, _ringT);
                _ring.position = Player.transform.position + Vector3.down * 1f;
                _ring.localScale = new Vector3(s, 0.05f, s);
                if (_ringT >= 1f) { _ringT = -1f; _ring.gameObject.SetActive(false); }
            }
        }
    }
}
