// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Con trỏ ảo (TISGamepadVirtualCursor)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Con trỏ ảo")]
    public class TISTutGp_VirtualCursor : TISTutLesson
    {
        public override string Title => "Con trỏ ảo bằng cần analog";

        public override string Summary =>
            "Màn hình kiểu PC (bản đồ, kho đồ kéo thả) khó đi bằng nút. TISGamepadVirtualCursor biến cần trái thành con trỏ chuột: A = click, touchpad / gyro cũng di chuyển được. Nhấn D-pad / LB / RB / LT / RT hoặc chạm chuột thật để thoát và focus lại nút cũ.";

        public override string[] SetupSteps => new[]
        {
            "Thêm <b>TISGamepadVirtualCursor</b> vào Canvas; gán <b>Cursor Transform</b> (hình con trỏ) và <b>Parent Canvas</b>.",
            "Bật / tắt: <b>tis.SetActiveGamepadCursor(true / false)</b>. Kiểm tra: <b>TISGamepadVirtualCursor.IsCursorActive</b>.",
            "Motion Settings › Virtual Cursor: <b>Touchpad Moves Cursor</b>, <b>Touchpad Click</b>, <b>Gyro Moves Cursor</b>.",
        };

        public override string Code =>
@"// Mở bản đồ → bật con trỏ ảo
tis.SetActiveGamepadCursor(true);

// Con trỏ đang bật → cần trái dành cho nó
if (TISGamepadVirtualCursor.IsCursorActive)
    return;

cursor.OnExitByNavigationInput += () =>
    Debug.Log(""Thoát bằng D-pad / LB / RB"");
cursor.OnExitByMouse += () =>
    Debug.Log(""Người chơi cầm chuột"");";

        public override string[] Objectives => new[]
        {
            $"Nhấn {TISTutGlyphs.North} để bật con trỏ ảo",
            $"Dùng cần trái + {TISTutGlyphs.South} click vào 3 ô màu",
            "Nhấn D-pad để thoát con trỏ",
        };

        public override string FooterHint => "Y bật con trỏ · cần trái di chuyển · A click · D-pad thoát";

        TISGamepadVirtualCursor _cursor;
        int _clicked;
        string _lastExit = "—";

        protected override void OnEnter()
        {
            GamepadCamera(look: false);
            _clicked = 0; _lastExit = "—";

            _cursor = FindAnyObjectByType<TISGamepadVirtualCursor>(FindObjectsInactive.Include);
            if (_cursor != null)
            {
                _cursor.OnExitByNavigationInput += OnExitNav;
                _cursor.OnExitByMouse += OnExitMouse;
            }

            var board = TISTutUI.Card(Overlay, "Board", new Vector2(0.5f, 0.5f), new Vector2(120f, -30f), new Vector2(560f, 300f));
            var head = TISTutUI.Text(board.transform, "Head", "BẢN ĐỒ — CLICK CÁC Ô", Theme.monoBold, 18, Theme.ink, TextAlignmentOptions.MidlineLeft);
            head.characterSpacing = 4f;
            TISTutUI.Place(head.rectTransform, 24f, 16f, 24f, -1f, height: 32f);
            Color[] cols = { Theme.toyCoral, Theme.toySun, Theme.toyMint, Theme.toyLilac, Theme.toySky, Theme.Accent() };
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3, row = i / 3;
                var tile = TISTutUI.Button(board.transform, "Tile " + (i + 1), (i + 1).ToString(), TISTutButton.Style.Ghost, 150f, 96f, null, false, 26f, 8f);
                TISTutUI.At((RectTransform)tile.transform, new Vector2(0f, 1f), new Vector2(24f + col * 172f, -62f - row * 112f), new Vector2(150f, 96f));
                var tint = cols[i];
                tile.onClick.AddListener(() =>
                {
                    if (tile.IsOn) return;
                    tile.IsOn = true;
                    _clicked++;
                    Toast($"Ô {tile.Text} ✓");
                    if (_clicked >= 3) Complete(1);
                });
            }

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.6f);

            Watch("IsCursorActive", () => F(TISGamepadVirtualCursor.IsCursorActive));
            Watch("GetGamepadCursorEnable()", () => F(TIS.GetGamepadCursorEnable()));
            Watch("Ô đã click", () => _clicked + " / 3", () => _clicked / 3f);
            Watch("Sự kiện thoát", () => _lastExit);
        }

        void OnExitNav()   { _lastExit = "OnExitByNavigationInput"; Toast("Thoát con trỏ (D-pad / vai / cò)"); Complete(2); }
        void OnExitMouse() { _lastExit = "OnExitByMouse"; Toast("Thoát con trỏ (chuột thật)"); }

        protected override void OnTick()
        {
            var tis = TIS;
            Player.CanMove = !TISGamepadVirtualCursor.IsCursorActive;

            if (!TISGamepadVirtualCursor.IsCursorActive && tis.IsSkillDown("Toggle"))
            {
                if (_cursor == null) { Toast("Scene chưa có TISGamepadVirtualCursor"); return; }
                tis.SetActiveGamepadCursor(true);
                Toast("Con trỏ ảo: BẬT");
                Complete(0);
            }
        }

        protected override void OnExit()
        {
            if (_cursor != null)
            {
                _cursor.OnExitByNavigationInput -= OnExitNav;
                _cursor.OnExitByMouse -= OnExitMouse;
            }
            if (TISGamepadVirtualCursor.IsCursorActive) TIS.SetActiveGamepadCursor(false);
        }
    }
}
