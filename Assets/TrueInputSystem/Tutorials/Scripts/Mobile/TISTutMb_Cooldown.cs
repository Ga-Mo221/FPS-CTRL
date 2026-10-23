// ============================================================
//  TrueInputSystem Tutorials — Mobile · Hồi chiêu & khoá nút (SetCooldownFill / SetDisabled)
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Mobile · Hồi chiêu")]
    public class TISTutMb_Cooldown : TISTutLesson
    {
        public override string Title => "Hồi chiêu & khoá nút";

        public override string Summary =>
            "SetCooldownFill vẽ vòng hồi chiêu lên nút, SetDisabled khoá nút (bấm không phát lệnh). Logic hồi chiêu vẫn nằm trong game — nút chỉ hiển thị trạng thái, nên dễ đồng bộ với server hay buff giảm hồi chiêu.";

        public override string[] SetupSteps => new[]
        {
            "Gán <b>Cooldown Fill</b> = một Image kiểu <b>Filled · Radial 360</b> phủ lên nút.",
            "<b>SetCooldownFill(0..1)</b> mỗi frame khi đang hồi · <b>SetDisabled(true / false)</b> để khoá / mở.",
            "<b>Interactable</b> = false: nút mờ hẳn (khoá theo cốt truyện, chưa mở khoá kỹ năng).",
        };

        public override string Code =>
@"if (tis.IsSkillDown(""Dash"") && _cd <= 0)
{
    Dash();
    _cd = 3f;
    dashBtn.SetDisabled(true);
}

if (_cd > 0)
{
    _cd -= Time.deltaTime;
    dashBtn.SetCooldownFill(_cd / 3f);
    if (_cd <= 0) dashBtn.SetDisabled(false);
}";

        public override string[] Objectives => new[]
        {
            "Bấm LƯỚT",
            "Bấm LƯỚT khi đang hồi chiêu — nút không phản hồi",
            "Đợi hồi chiêu xong rồi lướt lần nữa",
        };

        public override string FooterHint => "LƯỚT hồi chiêu 3 giây · kéo nửa trái để đi";

        const float Cooldown = 3f;
        float _cd;
        int _dashes;

        protected override void OnEnter()
        {
            Hud.Show(showDash: true, showJump: true);
            Player.CanJump = true;
            Cam.LookEnabled = true;
            Cam.RequireRightMouse = false;
            _cd = 0f; _dashes = 0;
            Hud.dash.SetDisabled(false);
            Hud.dash.SetCooldownFill(0f);

            Watch("IsSkillDown(\"Dash\")", () => F(TIS.IsSkillDown("Dash")));
            Watch("Hồi chiêu còn", () => Mathf.Max(0f, _cd).ToString("0.0") + " s", () => _cd / Cooldown);
            Watch("Nút đang khoá", () => F(_cd > 0f));
            Watch("Số lần lướt", () => _dashes.ToString());
        }

        protected override void OnTick()
        {
            var tis = TIS;
            if (tis.IsSkillDown("Dash") && _cd <= 0f)
            {
                var t = Player.transform;
                Player.Teleport(t.position + t.forward * 3.5f, t.eulerAngles.y);
                _dashes++;
                Toast("Lướt!");
                Complete(0);
                if (_dashes >= 2) Complete(2);
                _cd = Cooldown;
                Hud.dash.SetDisabled(true);
            }

            if (_cd > 0f)
            {
                // Bấm vào nút khi đang khoá → nhiệm vụ 2
                var ptr = Pointer.current;
                if (ptr != null && ptr.press.wasPressedThisFrame)
                {
                    var rt = (RectTransform)Hud.dash.transform;
                    if (RectTransformUtility.RectangleContainsScreenPoint(rt, ptr.position.ReadValue(), null))
                    {
                        Toast("Đang hồi chiêu…");
                        Complete(1);
                    }
                }

                _cd -= Time.deltaTime;
                Hud.dash.SetCooldownFill(Mathf.Clamp01(_cd / Cooldown));
                if (_cd <= 0f) Hud.dash.SetDisabled(false);
            }
        }

        protected override void OnExit()
        {
            if (Hud == null || Hud.dash == null) return;
            Hud.dash.SetDisabled(false);
            Hud.dash.SetCooldownFill(0f);
        }
    }
}
