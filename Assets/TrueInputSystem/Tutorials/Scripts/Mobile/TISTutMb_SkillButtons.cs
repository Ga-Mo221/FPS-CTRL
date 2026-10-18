// ============================================================
//  TrueInputSystem Tutorials — Mobile · Nút kỹ năng (TISMobileSkillButton)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Nút kỹ năng")]
    public class TISTutMb_SkillButtons : TISTutLesson
    {
        public override string Title => "Nút kỹ năng";

        public override string Summary =>
            "TISMobileSkillButton ghi trạng thái nút vào TIS theo tên action — IsSkillDown / IsSkillHeld / IsSkillReleased y hệt bàn phím và tay cầm, nên code nhảy, đánh không phải viết lại cho mobile. Ngón đè lên nút không bị tính là vuốt camera.";

        public override string[] SetupSteps => new[]
        {
            "Thêm <b>TISMobileSkillButton</b> lên một Image · <b>Skill Name</b> = tên action (vd. <b>Jump</b>).",
            "Action trong profile cần có <b>Binding Mobile</b> (vd. Button_0) để TIS đọc từ nút cảm ứng khi chạy trên mobile.",
            "Sự kiện Inspector: <b>OnPress</b>, <b>OnRelease</b>, <b>OnHold</b>, <b>OnHoldTimed</b> (số giây đã giữ).",
        };

        public override string Code =>
@"// Giống hệt bàn phím / tay cầm
if (tis.IsSkillDown(""Jump""))
    Jump();

if (tis.IsSkillHeld(""Attack""))
    _charge += Time.deltaTime;

if (tis.IsSkillReleased(""Attack""))
    Strike(_charge);

// Hoặc dùng sự kiện của nút
attackBtn.OnHoldTimed.AddListener(
    sec => chargeBar.value = sec);";

        public override string[] Objectives => new[]
        {
            "Bấm NHẢY 3 lần",
            "Giữ ĐÁNH ít nhất 1 giây rồi thả để tung đòn mạnh",
            "Đi vào vòng tròn tím rồi nhảy khi đang đứng trong vòng",
        };

        public override string FooterHint => "Nút NHẢY / ĐÁNH ở góc dưới-phải · kéo nửa trái để đi";

        readonly Pulse _jump = new Pulse(), _released = new Pulse();
        float _hold;
        TISTutZone _zone;

        protected override void OnEnter()
        {
            Hud.Show(showJump: true, showAttack: true);
            Player.CanJump = true;
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _hold = 0f;
            _zone = PG.Zone("Zone Tím", PG.lilac, new Vector3(-4f, 0f, 4f), 1.4f, Stage);

            Watch("IsSkillDown(\"Jump\")", () => _jump.Text);
            Watch("IsSkillHeld(\"Attack\")", () => F(TIS.IsSkillHeld("Attack")));
            Watch("IsSkillReleased(\"Attack\")", () => _released.Text);
            Watch("Thời gian giữ ĐÁNH", () => _hold.ToString("0.0") + " s", () => Mathf.Clamp01(_hold));
            Watch("Registry.GetState(\"Jump\")", () => F(TISMobileButtonRegistry.GetState("Jump")));
        }

        protected override void OnTick()
        {
            var tis = TIS;
            if (tis.IsSkillDown("Jump"))
            {
                _jump.Hit();
                if (_jump.Count >= 3) Complete(0);
                if (_zone.IsInside) Complete(2);
            }

            if (tis.IsSkillHeld("Attack")) _hold += Time.deltaTime;
            if (tis.IsSkillReleased("Attack"))
            {
                _released.Hit();
                if (_hold >= 1f)
                {
                    var t = Player.transform;
                    var ball = PG.Spawn(PrimitiveType.Sphere, "Strike", PG.coral, t.position + t.forward + Vector3.up * 0.4f,
                                        Vector3.one * 0.7f, Stage, collider: false);
                    ball.AddComponent<TISTutProjectile>().Launch(t.forward * 18f + Vector3.up * 3f);
                    Toast("Đòn mạnh!");
                    Complete(1);
                }
                else Toast($"Giữ lâu hơn ({_hold:0.0}s / 1s)");
                _hold = 0f;
            }
        }
    }
}
