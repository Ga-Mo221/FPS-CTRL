// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Di chuyển (MoveDirection)
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Di chuyển")]
    public class TISTutKbm_Move : TISTutLesson
    {
        public override string Title => "Di chuyển với WASD";

        public override string Summary =>
            "MoveDirection trả về Vector2 từ -1 đến 1: x là trái / phải, y là tiến / lùi. Giá trị đã chuẩn hoá — đi chéo không nhanh hơn đi thẳng. Dùng được cho game góc 3, nhìn từ trên xuống lẫn 2D.";

        public override string[] SetupSteps => new[]
        {
            "Trong profile, tạo action <b>Move</b> (Vector2) với Binding PC = <b><Keyboard>/wasd</b> — TIS tự tạo composite W / A / S / D.",
            "Đặt đúng tên <b>Move</b> để đọc qua thuộc tính <b>MoveDirection</b>. Action tên khác thì đọc bằng <b>GetVector2Action</b>.",
            "Muốn đi theo hướng camera: nhân trục tới / phải của camera với y / x như code mẫu.",
        };

        public override string Code =>
@"void Update()
{
    var tis = TISInputBridge.Instance;
    Vector2 mv = tis.MoveDirection;

    // Đi theo hướng camera
    Vector3 fwd = cam.forward;
    fwd.y = 0;
    Vector3 dir = fwd.normalized * mv.y
                + cam.right * mv.x;

    controller.Move(dir * speed
                    * Time.deltaTime);
}";

        public override string[] Objectives => new[]
        {
            "Đi tới vòng tròn vàng",
            "Đi tới vòng tròn tím",
            "Đi chéo — giữ 2 phím cùng lúc (vd. W + D)",
        };

        public override string FooterHint => "W A S D để di chuyển · camera đứng yên trong bài này";

        TISTutZone _a, _b;

        protected override void OnEnter()
        {
            PG.Toys.gameObject.SetActive(false);
            _a = PG.Zone("Zone Vàng", PG.sun,   new Vector3(-5f, 0f, 4f), 1.3f, Stage);
            _b = PG.Zone("Zone Tím",  PG.lilac, new Vector3(5f, 0f, 3.5f), 1.3f, Stage);

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));

            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("MoveDirection.x", () => F(TIS.MoveDirection.x), () => Mathf.Abs(TIS.MoveDirection.x));
            Watch("MoveDirection.y", () => F(TIS.MoveDirection.y), () => Mathf.Abs(TIS.MoveDirection.y));
            Watch("magnitude", () => F(TIS.MoveDirection.magnitude));
        }

        protected override void OnTick()
        {
            if (_a.Reached) Complete(0);
            if (_b.Reached) Complete(1);
            Vector2 mv = TIS.MoveDirection;
            if (Mathf.Abs(mv.x) > 0.5f && Mathf.Abs(mv.y) > 0.5f) Complete(2);
        }
    }
}
