// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Áp dụng: săn sao
// ============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Áp dụng vào game")]
    public class TISTutKbm_MiniGame : TISTutLesson
    {
        public override string Title => "Áp dụng: săn 10 ngôi sao";

        public override string Summary =>
            "Ghép mọi thứ đã học: WASD di chuyển, giữ chuột phải để xoay camera, con lăn để zoom, Space nhảy, Shift chạy. Nhặt 10 ngôi sao trong 60 giây. Script nhân vật và camera của sân chơi là ví dụ hoàn chỉnh để bạn chép vào game.";

        public override string[] SetupSteps => new[]
        {
            "Mở <b>Tutorials/Scripts/Core/TISTutPlayer.cs</b> và <b>TISTutOrbitCamera.cs</b> — mọi input đọc qua TISInputBridge, không dòng nào đụng tới Keyboard / Mouse trực tiếp.",
            "Chính code đó chạy với tay cầm và mobile mà không sửa gì — xem 2 scene hướng dẫn còn lại.",
            "Muốn đổi phím? Sửa profile hoặc cho người chơi tự đổi (bài Đổi phím).",
        };

        public override string Code =>
@"// Toàn bộ input của nhân vật
var tis = TISInputBridge.Instance;

Vector2 mv  = tis.MoveDirection;
bool run    = tis.IsSkillHeld(""Run"");
bool jump   = tis.IsSkillDown(""Jump"");

// Camera
Vector2 look = tis.LookDelta;
float zoom   = tis.ZoomDelta;";

        public override string[] Objectives => new[]
        {
            "Nhặt 5 ngôi sao",
            "Nhặt đủ 10 ngôi sao",
            "Hoàn thành trong 60 giây",
        };

        public override string FooterHint => "WASD · Shift · Space · giữ chuột phải xoay · con lăn zoom";

        readonly List<TISTutCollectible> _stars = new List<TISTutCollectible>();
        int   _got;
        float _time;
        bool  _running;
        TMP_Text _timer;

        protected override void OnEnter()
        {
            SetupControls();

            var card = TISTutUI.Card(Overlay, "Score", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(300f, 110f));
            _timer = TISTutUI.Text(card.transform, "Text", "", Theme.monoBold, 26, Theme.ink, TextAlignmentOptions.Center, wrap: true);
            TISTutUI.Place(_timer.rectTransform, 10f, 10f, 10f, 56f);
            var again = TISTutUI.Button(card.transform, "Again", "CHƠI LẠI", TISTutButton.Style.Accent, 200f, 40f, null, false, 15f, 7f);
            TISTutUI.At((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(200f, 40f));
            again.onClick.AddListener(Restart);

            Restart();

            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("LookDelta", () => F(TIS.LookDelta), () => Mathf.Clamp01(TIS.LookDelta.magnitude / 3f));
            Watch("IsSkillHeld(\"Run\")", () => F(TIS.IsSkillHeld("Run")));
            Watch("Sao", () => _got + " / 10", () => _got / 10f);
        }

        void Restart()
        {
            foreach (var s in _stars) if (s != null) Destroy(s.gameObject);
            _stars.Clear();
            _got = 0; _time = 0f; _running = true;
            Player.Teleport(TISTutPlayground.PlayerStart);

            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                float r = i % 2 == 0 ? 7f : 11f;
                float h = i % 3 == 0 ? 2.2f : 0.8f;          // vài ngôi sao phải nhảy mới tới
                var star = PG.Star(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), Stage);
                star.OnCollected += OnStar;
                _stars.Add(star);
            }
        }

        /// <summary>Gọi mỗi khi nhặt được sao (bản tay cầm thêm rung).</summary>
        protected virtual void OnStarCollected(int total) { }

        protected virtual void SetupControls()
        {
            Player.CanJump = true;
            Player.CanRun = true;
            Cam.LookEnabled = true;
            Cam.ZoomEnabled = true;
        }

        void OnStar(TISTutCollectible s)
        {
            _got++;
            OnStarCollected(_got);
            if (_got >= 5) Complete(0);
            if (_got >= 10)
            {
                Complete(1);
                _running = false;
                if (_time <= 60f) Complete(2);
                Toast(_time <= 60f ? $"Xuất sắc! {_time:0.0} giây" : $"Xong trong {_time:0.0} giây — thử lại dưới 60 giây nhé");
            }
        }

        protected override void OnTick()
        {
            if (_running) _time += Time.deltaTime;
            _timer.text = $"SAO {_got}/10 · {_time:0.0}s";
            _timer.color = _running && _time > 60f ? Theme.toyCoral : Theme.ink;
        }
    }
}
