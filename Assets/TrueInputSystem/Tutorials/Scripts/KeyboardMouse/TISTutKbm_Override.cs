// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Ghi đè input (OverrideMoveInput)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Ghi đè input")]
    public class TISTutKbm_Override : TISTutLesson
    {
        public override string Title => "Ghi đè input: tự chạy";

        public override string Summary =>
            "OverrideMoveInput / OverrideLookInput thay giá trị MoveDirection / LookDelta bằng giá trị bạn đưa vào. Mọi script đang đọc input nhận giá trị mới mà không phải sửa gì — dùng cho cutscene, dẫn đường, AI điều khiển hộ, hay nút ảo tự viết.";

        public override string[] SetupSteps => new[]
        {
            "Gọi <b>OverrideMoveInput(v)</b> — giá trị được giữ cho tới khi gọi <b>ClearMoveOverride()</b>.",
            "Tương tự: <b>OverrideLookInput</b>, <b>OverrideZoomInput</b>, <b>OverrideVector2Action(tên, v)</b> cho action bất kỳ.",
            "Map chứa Move đang tắt thì override cũng bị chặn — menu tạm dừng vẫn an toàn.",
        };

        public override string Code =>
@"// Tự chạy tới cờ (theo hướng camera)
Vector3 to = flag.position - player.position;
Vector3 f = cam.forward; f.y = 0;
Vector3 r = cam.right;   r.y = 0;
Vector2 dir = new Vector2(
    Vector3.Dot(to, r.normalized),
    Vector3.Dot(to, f.normalized));
tis.OverrideMoveInput(dir.normalized);

// Trả quyền điều khiển
tis.ClearMoveOverride();";

        public override string[] Objectives => new[]
        {
            "Nhấn T để bật tự chạy — nhân vật tự tới cờ",
            "Nhấn T lần nữa (hoặc tới nơi) để lấy lại quyền điều khiển",
            "Tự chạy tới cờ thứ hai",
        };

        public override string FooterHint => "T bật / tắt tự chạy · WASD bị thay thế khi đang tự chạy";

        TISTutZone[] _flags;
        int  _target;
        bool _auto;
        int  _arrived;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            _auto = false; _target = 0; _arrived = 0;
            _flags = new[]
            {
                Flag(new Vector3(-6f, 0f, 6f), PG.sun),
                Flag(new Vector3(6f, 0f, 5f),  PG.lilac),
            };

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));
            KeyRow(new Vector2(0f, 0f), new Vector2(230f, 20f), ("T", "<Keyboard>/t", 64f));

            Watch("Tự chạy (override)", () => _auto ? "BẬT" : "TẮT");
            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("Cờ đích", () => _target == 0 ? "Vàng" : "Tím");
        }

        TISTutZone Flag(Vector3 at, Material mat)
        {
            PG.Spawn(PrimitiveType.Cylinder, "Pole", PG.ink, at + new Vector3(0f, 1.5f, 0f), new Vector3(0.15f, 1.5f, 0.15f), Stage);
            PG.Spawn(PrimitiveType.Cube, "Flag", mat, at + new Vector3(0.55f, 2.6f, 0f), new Vector3(1f, 0.7f, 0.08f), Stage);
            return PG.Zone("Zone", mat, at, 1.4f, Stage);
        }

        protected override void OnTick()
        {
            var tis = TIS;
            if (tis.IsSkillDown("Autopilot")) SetAuto(!_auto);

            if (!_auto) return;
            var flag = _flags[_target];
            if (flag.IsInside)
            {
                _arrived++;
                Toast("Đã tới cờ!");
                if (_arrived >= 2) Complete(2);
                _target = (_target + 1) % _flags.Length;
                SetAuto(false);
                return;
            }

            Transform cam = Cam.transform;
            Vector3 to = flag.transform.position - Player.transform.position;
            Vector3 f = cam.forward; f.y = 0f;
            Vector3 r = cam.right;   r.y = 0f;
            var dir = new Vector2(Vector3.Dot(to, r.normalized), Vector3.Dot(to, f.normalized));
            tis.OverrideMoveInput(dir.normalized);
        }

        void SetAuto(bool on)
        {
            _auto = on;
            if (on) { Complete(0); Toast("Tự chạy: BẬT"); }
            else
            {
                TIS.ClearMoveOverride();
                if (IsObjectiveDone(0)) Complete(1);
            }
        }

        protected override void OnExit() => TIS.ClearMoveOverride();
    }
}
