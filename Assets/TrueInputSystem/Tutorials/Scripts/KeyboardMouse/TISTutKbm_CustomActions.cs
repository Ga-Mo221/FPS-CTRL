// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · GetVector2Action / GetAxisAction / Handle
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Đọc action bất kỳ")]
    public class TISTutKbm_CustomActions : TISTutLesson
    {
        public override string Title => "Đọc action bất kỳ + Handle";

        public override string Summary =>
            "Ngoài Move / Look / Zoom, mọi action đọc được theo tên: GetVector2Action trả Vector2, GetAxisAction trả float. Đọc nhiều lần mỗi frame? Lấy TISActionHandle một lần rồi đọc bằng handle — nhanh hơn tra theo tên, tự cập nhật khi đổi profile.";

        public override string[] SetupSteps => new[]
        {
            "Action <b>Scroll</b> (Axis) · Binding PC = <b><Mouse>/scroll/y</b> — quay chong chóng bằng con lăn.",
            "<b>GetActionHandle(\"Jump\")</b> gọi một lần (Start / OnEnable), lưu vào biến, rồi truyền handle cho IsSkillDown / GetAxisAction...",
            "Tên action sai → TIS cảnh báo một lần trong Console và trả về 0 / false, game không crash.",
        };

        public override string Code =>
@"Vector2 mv = tis.GetVector2Action(""Move"");
float scroll = tis.GetAxisAction(""Scroll"");

// Lấy handle 1 lần, đọc mỗi frame
TISActionHandle _jump;
void Start() =>
    _jump = tis.GetActionHandle(""Jump"");

void Update()
{
    if (tis.IsSkillDown(_jump)) Jump();
}";

        public override string[] Objectives => new[]
        {
            "Cuộn chuột quay chong chóng trọn 1 vòng (GetAxisAction)",
            "Nhảy — đọc bằng TISActionHandle",
            "Đi tới vòng tròn xanh — đọc GetVector2Action(\"Move\")",
        };

        public override string FooterHint => "Con lăn quay chong chóng · Space nhảy · WASD đi";

        TISActionHandle _jump, _scroll;
        Transform _spinner;
        float _spun, _lastAxis;
        TISTutZone _zone;
        readonly Pulse _jumpPulse = new Pulse();

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            _jump   = TIS.GetActionHandle("Jump");
            _scroll = TIS.GetActionHandle("Scroll");
            _spun = 0f;
            Player.CanJump = true;

            // Chong chóng: trụ + 2 cánh
            var pole = PG.Spawn(PrimitiveType.Cylinder, "Pole", PG.ink, new Vector3(-3.5f, 1.5f, 3f), new Vector3(0.3f, 1.5f, 0.3f), Stage);
            _spinner = new GameObject("Spinner").transform;
            _spinner.SetParent(Stage);
            _spinner.position = new Vector3(-3.5f, 3.2f, 2.7f);
            PG.Spawn(PrimitiveType.Cube, "Blade A", PG.coral, _spinner.position, new Vector3(3.2f, 0.45f, 0.12f), _spinner);
            PG.Spawn(PrimitiveType.Cube, "Blade B", PG.sun,   _spinner.position, new Vector3(0.45f, 3.2f, 0.12f), _spinner);
            _zone = PG.Zone("Zone Xanh", PG.mint, new Vector3(4.5f, 0f, 3.5f), 1.3f, Stage);

            KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("CON LĂN", "<Mouse>/scroll", 150f), ("SPACE", "<Keyboard>/space", 200f));

            Watch("GetAxisAction(\"Scroll\")", () => F(_lastAxis), () => Mathf.Clamp01(Mathf.Abs(_lastAxis) / 120f));
            Watch("GetVector2Action(\"Move\")", () => F(TIS.GetVector2Action("Move")), () => Mag(TIS.GetVector2Action("Move")));
            Watch("IsSkillDown(_jump)", () => _jumpPulse.Text);
            Watch("handle", () => _jump.ToString() + (_jump.IsValid ? " ✓" : ""));
            Watch("Chong chóng đã quay", () => Mathf.Min(360f, _spun).ToString("0") + "°", () => _spun / 360f);
        }

        protected override void OnTick()
        {
            float axis = TIS.GetAxisAction(in _scroll);
            if (axis != 0f) _lastAxis = axis;
            float notch = Mathf.Abs(axis) > 10f ? axis / 120f : axis;
            float deg = notch * 30f;
            _spinner.Rotate(0f, 0f, deg, Space.Self);
            _spun += Mathf.Abs(deg);
            if (_spun >= 360f) Complete(0);

            if (TIS.IsSkillDown(in _jump))
            {
                _jumpPulse.Hit();
                Complete(1);
            }

            if (_zone.Reached) Complete(2);
        }
    }
}
