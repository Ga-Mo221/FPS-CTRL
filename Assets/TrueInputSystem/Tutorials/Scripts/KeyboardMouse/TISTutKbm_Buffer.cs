// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · TISInputBuffer
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Input buffer")]
    public class TISTutKbm_Buffer : TISTutLesson
    {
        public override string Title => "Input buffer: không trượt phím";

        public override string Summary =>
            "Người chơi hay bấm nhảy sớm vài khung hình trước khi chạm đất — không có buffer thì cú bấm đó mất, cảm giác \"game không ăn phím\". TISInputBuffer giữ lần bấm trong một cửa sổ thời gian rồi Consume() khi đủ điều kiện.";

        public override string[] SetupSteps => new[]
        {
            "Tạo <b>new TISInputBuffer(\"Jump\", 0.15f)</b> trong script nhân vật — 0.15 giây là cửa sổ giữ lần bấm.",
            "Mỗi frame gọi <b>Tick(tis)</b>; khi nhân vật chạm đất gọi <b>Consume()</b> — trả về true một lần rồi tự xoá.",
            "Dùng tương tự cho tấn công combo, lướt, tương tác. Nhấn <b>B</b> để bật / tắt buffer và so sánh.",
        };

        public override string Code =>
@"TISInputBuffer _jump =
    new TISInputBuffer(""Jump"", 0.15f);

void Update()
{
    _jump.Tick(tis);   // ghi nhận lần bấm

    if (grounded && _jump.Consume())
        Jump();
}

// Kiểm tra mà không tiêu thụ
bool waiting = _jump.IsBuffered;";

        public override string[] Objectives => new[]
        {
            "Nhảy liên tục — bấm Space ngay trước khi chạm đất 2 lần",
            "Nhấn B tắt buffer, thử lại và cảm nhận khác biệt",
            "Nhấn B bật lại buffer",
        };

        public override string FooterHint => "Space nhảy · B bật / tắt buffer";

        int  _saved;
        bool _turnedOff;

        protected override void OnEnter()
        {
            _saved = 0; _turnedOff = false;
            Player.CanJump = true;
            Player.UseJumpBuffer = true;
            Player.jumpHeight = 2.2f;
            Player.OnJumped += buffered =>
            {
                if (!buffered) return;
                _saved++;
                Toast($"Buffer cứu cú nhảy! ({_saved})");
                if (_saved >= 2) Complete(0);
            };

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));
            KeyRow(new Vector2(0f, 0f), new Vector2(230f, 20f), ("SPACE", "<Keyboard>/space", 220f), ("B", "<Keyboard>/b", 64f));

            Watch("Buffer", () => Player.UseJumpBuffer ? "BẬT" : "TẮT");
            Watch("IsBuffered", () => F(Player.JumpBuffer.IsBuffered), () => Player.JumpBuffer.IsBuffered ? 1f : 0f);
            Watch("WindowSeconds", () => F(Player.JumpBuffer.WindowSeconds) + " s");
            Watch("Đang chạm đất", () => F(Player.IsGrounded));
            Watch("Nhảy nhờ buffer", () => _saved + " lần");
        }

        protected override void OnTick()
        {
            if (TIS.IsSkillDown("BufferToggle"))
            {
                Player.UseJumpBuffer = !Player.UseJumpBuffer;
                Player.JumpBuffer.Clear();
                Toast(Player.UseJumpBuffer ? "Buffer: BẬT" : "Buffer: TẮT — bấm sớm sẽ bị mất");
                if (!Player.UseJumpBuffer) { _turnedOff = true; Complete(1); }
                else if (_turnedOff) Complete(2);
            }
        }

        protected override void OnExit() => Player.jumpHeight = 1.6f;
    }
}
