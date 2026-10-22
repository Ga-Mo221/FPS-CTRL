// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Action Map (tạm dừng game)
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Action Map")]
    public class TISTutKbm_ActionMaps : TISTutLesson
    {
        public override string Title => "Action Map: tạm dừng game";

        public override string Summary =>
            "Action map gom các action theo nhóm (Movement, Camera, Combat, UI...). Tắt một map là tắt mọi action trong đó — cách gọn nhất để chặn điều khiển khi mở menu, xem cutscene hay hội thoại, mà không phải thêm if ở từng script.";

        public override string[] SetupSteps => new[]
        {
            "Mỗi map trong profile có cờ <b>Enabled By Default</b>.",
            "Tắt map chứa <b>Move</b> → MoveDirection = 0 (kể cả override). Tắt map chứa <b>Look</b> → LookDelta và ZoomDelta = 0.",
            "<b>EnableDefaultMaps()</b> trả mọi map về mặc định; <b>DisableAllMaps()</b> tắt tất cả (kể cả UI).",
        };

        public override string Code =>
@"void OpenPause()
{
    tis.SetActionMapActive(""Movement"", false);
    tis.SetActionMapActive(""Camera"", false);
    tis.SetActionMapActive(""Combat"", false);
    pauseMenu.SetActive(true);
}

void ClosePause()
{
    tis.EnableDefaultMaps();
    pauseMenu.SetActive(false);
}

// Map UI vẫn bật → Esc vẫn đọc được
if (tis.IsSkillDown(""Pause"")) Toggle();";

        public override string[] Objectives => new[]
        {
            "Nhấn Esc để mở menu tạm dừng",
            "Thử đi bằng WASD khi đang dừng — nhân vật đứng yên",
            "Nhấn Esc hoặc nút Tiếp tục để chơi tiếp",
        };

        public override string FooterHint => "Esc mở / đóng menu tạm dừng · trong Editor, Esc có thể nhả con trỏ chuột";

        bool _paused;
        GameObject _menu;

        protected override void OnEnter()
        {
            _paused = false;
            Player.CanJump = true;
            Cam.LookEnabled = true;

            WasdCluster(new Vector2(0f, 0f), new Vector2(20f, 20f));
            KeyRow(new Vector2(0f, 0f), new Vector2(230f, 20f), ("ESC", "<Keyboard>/escape", 90f));

            // Menu tạm dừng
            var dim = TISTutUI.Shape(Overlay, "Pause Dim", new Color(0.15f, 0.19f, 0.2f, 0.45f), 0f);
            TISTutUI.Stretch(dim.rectTransform);
            _menu = dim.gameObject;
            var card = TISTutUI.Card(dim.transform, "Pause Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 280f));
            var title = TISTutUI.Text(card.transform, "Title", "TẠM DỪNG", Theme.monoBold, 34, Theme.ink, TextAlignmentOptions.Center);
            title.characterSpacing = 8f;
            TISTutUI.Place(title.rectTransform, 20f, 30f, 20f, -1f, height: 50f);
            var note = TISTutUI.Text(card.transform, "Note", "Map <b>Movement · Camera · Combat</b> đang tắt.\nMap <b>UI</b> vẫn bật để đọc phím Esc.",
                                     Theme.body, 18, Theme.inkSoft, TextAlignmentOptions.Center, wrap: true);
            TISTutUI.Place(note.rectTransform, 30f, 92f, 30f, -1f, height: 70f);
            var resume = TISTutUI.Button(card.transform, "Resume", "TIẾP TỤC", TISTutButton.Style.Dark, 300f, 58f, "→");
            TISTutUI.At((RectTransform)resume.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 58f));
            resume.onClick.AddListener(() => SetPaused(false));
            _menu.SetActive(false);

            Watch("Map Movement", () => _paused ? "TẮT" : "BẬT");
            Watch("Map Camera",   () => _paused ? "TẮT" : "BẬT");
            Watch("Map UI",       () => "BẬT");
            Watch("MoveDirection", () => F(TIS.MoveDirection), () => Mag(TIS.MoveDirection));
            Watch("IsSkillDown(\"Pause\")", () => F(TIS.IsSkillDown("Pause")));
        }

        void SetPaused(bool on)
        {
            if (_paused == on) return;
            _paused = on;
            var tis = TIS;
            if (on)
            {
                tis.SetActionMapActive("Movement", false);
                tis.SetActionMapActive("Camera", false);
                tis.SetActionMapActive("Combat", false);
                Complete(0);
            }
            else
            {
                tis.EnableDefaultMaps();
                if (IsObjectiveDone(0)) Complete(2);
            }
            _menu.SetActive(on);
        }

        protected override void OnTick()
        {
            if (TIS.IsSkillDown("Pause")) SetPaused(!_paused);

            if (_paused)
            {
                var kb = Keyboard.current;
                bool trying = kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed);
                if (trying && TIS.MoveDirection == Vector2.zero) Complete(1);
            }
        }

        protected override void OnExit()
        {
            if (_paused) TIS.EnableDefaultMaps();
            _paused = false;
        }
    }
}
