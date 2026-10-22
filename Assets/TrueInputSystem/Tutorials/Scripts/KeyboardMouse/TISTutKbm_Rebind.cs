// ============================================================
//  TrueInputSystem Tutorials — Chuột + Bàn phím · Đổi phím (TISRebindingSystem)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/KBM · Đổi phím")]
    public class TISTutKbm_Rebind : TISTutLesson
    {
        public override string Title => "Cho người chơi đổi phím";

        public override string Summary =>
            "TISRebindingSystem lắng nghe phím tiếp theo người chơi nhấn rồi gán cho action. Kết quả tự lưu theo profile và tự áp lại lần mở game sau. Có sẵn kiểm tra trùng phím và khôi phục mặc định — đủ để làm màn hình Cài đặt › Điều khiển.";

        public override string[] SetupSteps => new[]
        {
            "Nút \"Đổi\" gọi <b>TISRebindingSystem.StartRebinding(tis, tên, RebindTarget.PC, callback)</b>. Esc để huỷ.",
            "Hiển thị phím hiện tại: <b>GetActivePath</b> + <b>GetShortDisplayName</b>.",
            "<b>FindConflict</b> trả về tên action khác đang dùng cùng phím. <b>ResetBinding</b> / <b>ResetAllBindings</b> để khôi phục.",
            "Bridge tự áp phím đã lưu khi nạp profile — không cần gọi thêm.",
        };

        public override string Code =>
@"TISRebindingSystem.StartRebinding(
    tis, ""Jump"", RebindTarget.PC,
    r =>
    {
        if (!r.Success) return;
        label.text = TISRebindingSystem
            .GetShortDisplayName(r.NewPath);

        string other = TISRebindingSystem
            .FindConflict(tis, ""Jump"",
                RebindTarget.PC, r.NewPath);
        if (other != null) Warn(other);
    });

TISRebindingSystem.ResetBinding(
    tis, ""Jump"", RebindTarget.PC);";

        public override string[] Objectives => new[]
        {
            "Đổi phím Nhảy sang một phím khác",
            "Nhảy bằng phím mới",
            "Khôi phục phím mặc định",
        };

        public override string FooterHint => "Bấm ĐỔI rồi nhấn phím mới · Esc để huỷ";

        static readonly (string action, string label)[] Rows =
        {
            ("Jump",     "Nhảy"),
            ("Run",      "Chạy"),
            ("Interact", "Tương tác"),
        };

        readonly TMP_Text[] _keys = new TMP_Text[3];
        bool _rebound;

        protected override void OnEnter()
        {
            _rebound = false;
            Player.CanJump = true;
            Player.CanRun = true;

            var card = TISTutUI.Card(Overlay, "Rebind Panel", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(430f, 330f));
            var head = TISTutUI.Text(card.transform, "Head", "ĐIỀU KHIỂN", Theme.monoBold, 17, Theme.inkSoft, TextAlignmentOptions.MidlineLeft);
            head.characterSpacing = 4f;
            TISTutUI.Place(head.rectTransform, 22f, 16f, 22f, -1f, height: 30f);

            for (int i = 0; i < Rows.Length; i++)
            {
                int idx = i;
                float y = 58f + i * 66f;
                var name = TISTutUI.Text(card.transform, "Name", Rows[i].label, Theme.bodyBold, 19, Theme.ink, TextAlignmentOptions.MidlineLeft);
                TISTutUI.Place(name.rectTransform, 22f, y, -1f, -1f, width: 130f, height: 52f);
                var keyBg = TISTutUI.Shape(card.transform, "Key", Theme.paper, 10f, 2f, Theme.line);
                TISTutUI.Place(keyBg.rectTransform, 150f, y + 4f, -1f, -1f, width: 130f, height: 44f);
                _keys[i] = TISTutUI.Text(keyBg.transform, "Text", "—", Theme.monoBold, 17, Theme.ink, TextAlignmentOptions.Center);
                TISTutUI.Stretch(_keys[i].rectTransform);
                var btn = TISTutUI.Button(card.transform, "Change", "ĐỔI", TISTutButton.Style.Dark, 110f, 44f, null, false, 16f, 7f);
                TISTutUI.Place((RectTransform)btn.transform, -1f, y + 4f, 22f, -1f, width: 110f, height: 44f);
                btn.onClick.AddListener(() => Begin(idx));
            }

            var reset = TISTutUI.Button(card.transform, "Reset", "KHÔI PHỤC MẶC ĐỊNH", TISTutButton.Style.Accent, 386f, 46f, null, false, 15f, 8f);
            TISTutUI.Place((RectTransform)reset.transform, 22f, -1f, 22f, 18f, height: 46f);
            reset.onClick.AddListener(ResetAll);

            RefreshKeys();

            Watch("State", () => TISRebindingSystem.State.ToString());
            Watch("GetActivePath(\"Jump\")", () => TISRebindingSystem.GetActivePath(TIS, "Jump", RebindTarget.PC) ?? "—");
            Watch("IsSkillDown(\"Jump\")", () => F(TIS.IsSkillDown("Jump")));
        }

        void Begin(int i)
        {
            if (TISRebindingSystem.State == RebindState.Listening) TISRebindingSystem.CancelRebinding();
            string action = Rows[i].action;
            _keys[i].text = "Nhấn phím…";
            TISRebindingSystem.StartRebinding(TIS, action, RebindTarget.PC, r =>
            {
                RefreshKeys();
                if (!r.Success) { Toast("Đã huỷ đổi phím"); return; }

                string other = TISRebindingSystem.FindConflict(TIS, action, RebindTarget.PC, r.NewPath);
                Toast(other != null
                    ? $"Phím {TISRebindingSystem.GetShortDisplayName(r.NewPath)} đang trùng với \"{other}\""
                    : $"{Rows[i].label} → {TISRebindingSystem.GetShortDisplayName(r.NewPath)}");
                if (action == "Jump") { _rebound = true; Complete(0); }
            });
        }

        void ResetAll()
        {
            if (TISRebindingSystem.State == RebindState.Listening) TISRebindingSystem.CancelRebinding();
            foreach (var row in Rows) TISRebindingSystem.ResetBinding(TIS, row.action, RebindTarget.PC);
            RefreshKeys();
            Toast("Đã khôi phục phím mặc định");
            if (IsObjectiveDone(0)) Complete(2);
        }

        void RefreshKeys()
        {
            for (int i = 0; i < Rows.Length; i++)
            {
                string path = TISRebindingSystem.GetActivePath(TIS, Rows[i].action, RebindTarget.PC);
                _keys[i].text = string.IsNullOrEmpty(path) ? "—" : TISRebindingSystem.GetShortDisplayName(path);
            }
        }

        protected override void OnTick()
        {
            if (_rebound && TIS.IsSkillDown("Jump") && TISRebindingSystem.State != RebindState.Listening)
            {
                string path = TISRebindingSystem.GetActivePath(TIS, "Jump", RebindTarget.PC);
                if (path != "<Keyboard>/space") Complete(1);
            }
        }

        protected override void OnExit()
        {
            if (TISRebindingSystem.State == RebindState.Listening) TISRebindingSystem.CancelRebinding();
        }
    }
}
