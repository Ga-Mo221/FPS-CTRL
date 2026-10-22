// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Nút: Down / Held / Released
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Nhảy, chạy, tích lực")]
    public class TISTutKbm_Buttons : TISTutLesson
    {
        public override string Title => "Nhảy, chạy & tích lực";

        public override string Summary =>
            "Ba cách đọc một nút: IsSkillDown đúng 1 frame khi vừa nhấn (nhảy, bắn phát một) · IsSkillHeld suốt lúc giữ (chạy, ngắm) · IsSkillReleased đúng 1 frame khi thả (tung chiêu tích lực).";

        public override string[] SetupSteps => new[]
        {
            "Tạo các action kiểu <b>Button</b>: <b>Jump</b> = <Keyboard>/space · <b>Run</b> = <Keyboard>/leftShift · <b>Attack</b> = <Mouse>/leftButton.",
            "Đọc theo tên action — không phân biệt hoa thường. Tên sai → TIS cảnh báo một lần trong Console.",
        };

        public override string Code =>
@"if (tis.IsSkillDown(""Jump""))
    Jump();

speed = tis.IsSkillHeld(""Run"")
    ? runSpeed : walkSpeed;

if (tis.IsSkillHeld(""Attack""))
    _charge += Time.deltaTime;

if (tis.IsSkillReleased(""Attack""))
{
    Shoot(_charge);
    _charge = 0;
}";

        public override string[] Objectives => new[]
        {
            "Nhảy 3 lần (Space)",
            "Giữ Shift chạy liên tục 2 giây",
            "Giữ chuột trái tích lực rồi thả để bắn",
        };

        public override string FooterHint => "Space nhảy · Shift chạy · giữ rồi thả chuột trái để bắn";

        readonly Pulse _down = new Pulse();
        readonly Pulse _released = new Pulse();
        float _charge;
        bool  _charging;
        float _runTime;
        TISTutShape _chargeFill;

        protected override void OnEnter()
        {
            Player.CanJump = true;
            Player.CanRun  = true;
            _charge = 0f; _charging = false; _runTime = 0f;

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));
            KeyRow(new Vector2(0f, 0f), new Vector2(230f, 20f),
                   ("SHIFT", "<Keyboard>/leftShift", 110f), ("SPACE", "<Keyboard>/space", 220f),
                   ("CHUỘT TRÁI", "<Mouse>/leftButton", 170f));

            // Thanh lực (góc trên-phải)
            var card = TISTutUI.Card(Overlay, "Charge", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(320f, 74f));
            var lbl = TISTutUI.Text(card.transform, "Label", "LỰC TÍCH", Theme.monoBold, 15, Theme.inkSoft, TMPro.TextAlignmentOptions.TopLeft);
            TISTutUI.Place(lbl.rectTransform, 18f, 12f, 18f, -1f, height: 22f);
            var bar = TISTutUI.Shape(card.transform, "Track", Theme.track, 8f);
            TISTutUI.Place(bar.rectTransform, 18f, -1f, 18f, 16f, height: 16f);
            _chargeFill = TISTutUI.Shape(bar.transform, "Fill", Theme.AccentStrong(), 8f);
            var frt = _chargeFill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.001f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            Watch("IsSkillDown(\"Jump\")",        () => _down.Text);
            Watch("IsSkillHeld(\"Run\")",         () => F(TIS.IsSkillHeld("Run")));
            Watch("IsSkillHeld(\"Attack\")",      () => F(_charging));
            Watch("IsSkillReleased(\"Attack\")",  () => _released.Text);
            Watch("Lực tích", () => (_charge * 100f).ToString("0") + "%", () => _charge);
        }

        protected override void OnTick()
        {
            var tis = TIS;
            if (tis.IsSkillDown("Jump")) { _down.Hit(); if (_down.Count >= 3) Complete(0); }

            if (Player.IsRunning) { _runTime += Time.deltaTime; if (_runTime >= 2f) Complete(1); }
            else _runTime = 0f;

            // Bắt đầu tích lực chỉ khi bấm trong sân chơi (không phải bấm nút UI)
            if (tis.IsSkillDown("Attack"))
                _charging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            if (_charging && tis.IsSkillHeld("Attack"))
                _charge = Mathf.Min(1f, _charge + Time.deltaTime * 0.8f);
            if (tis.IsSkillReleased("Attack"))
            {
                _released.Hit();
                if (_charging && _charge > 0.05f)
                {
                    Shoot(_charge);
                    Complete(2);
                }
                _charging = false;
                _charge = 0f;
            }

            var frt = _chargeFill.rectTransform;
            frt.anchorMax = new Vector2(Mathf.Max(0.001f, _charge), 1f);
        }

        void Shoot(float power)
        {
            var t = Player.transform;
            var ball = PG.Spawn(PrimitiveType.Sphere, "Shot", PG.coral, t.position + t.forward * 0.9f + Vector3.up * 0.4f,
                                Vector3.one * Mathf.Lerp(0.3f, 0.8f, power), Stage, collider: false);
            ball.AddComponent<TISTutProjectile>().Launch(t.forward * Mathf.Lerp(6f, 22f, power) + Vector3.up * 3f);
            Toast(power > 0.9f ? "Bắn hết lực!" : $"Bắn với {power * 100f:0}% lực");
        }
    }
}
