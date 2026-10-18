// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Tổ hợp phím (PC Modifier)
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Tổ hợp phím")]
    public class TISTutKbm_Modifier : TISTutLesson
    {
        public override string Title => "Tổ hợp phím: Ctrl + S";

        public override string Summary =>
            "PC Modifier cho phép một action cần 2 phím (Ctrl + S, Shift + 1, Alt + Enter...). Nhấn S một mình vẫn là đi lùi — chỉ khi giữ Ctrl rồi nhấn S, action QuickSave mới kích hoạt.";

        public override string[] SetupSteps => new[]
        {
            "Action <b>QuickSave</b> (Button) · Binding PC = <b><Keyboard>/s</b> · PC Modifier = <b><Keyboard>/leftCtrl</b>.",
            "TIS tự tạo composite <b>OneModifier</b> — code đọc như một nút bình thường.",
            "Tay cầm cũng có Gamepad Modifier (vd. giữ LB + A).",
        };

        public override string Code =>
@"if (tis.IsSkillDown(""QuickSave""))
{
    SaveGame();
    ShowToast(""Đã lưu"");
}";

        public override string[] Objectives => new[]
        {
            "Nhấn S một mình — nhân vật lùi, game không lưu",
            "Giữ Ctrl rồi nhấn S để lưu nhanh",
            "Lưu nhanh 3 lần",
        };

        public override string FooterHint => "Ctrl + S để lưu nhanh · WASD vẫn di chuyển bình thường";

        readonly Pulse _save = new Pulse();

        protected override void OnEnter()
        {
            KeyRow(new Vector2(0f, 0f), new Vector2(20f, 20f), ("CTRL", "<Keyboard>/leftCtrl", 110f), ("S", "<Keyboard>/s", 64f));

            Watch("IsSkillDown(\"QuickSave\")", () => _save.Text);
            Watch("Số lần lưu", () => _save.Count.ToString(), () => _save.Count / 3f);
            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
        }

        protected override void OnTick()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.sKey.wasPressedThisFrame && !kb.ctrlKey.isPressed) Complete(0);

            if (TIS.IsSkillDown("QuickSave"))
            {
                _save.Hit();
                Toast($"Đã lưu nhanh ✓ ({_save.Count})");
                Complete(1);
                if (_save.Count >= 3) Complete(2);
            }
        }
    }
}
