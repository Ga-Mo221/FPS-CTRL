// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Đổi nút (TISRebindingSystem, RebindTarget.Gamepad)
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Đổi nút")]
    public class TISTutGp_Rebind : TISTutLesson
    {
        public override string Title => "Cho người chơi đổi nút tay cầm";

        public override string Summary =>
            "Cùng TISRebindingSystem như bàn phím, chỉ đổi đích sang RebindTarget.Gamepad. Binding tay cầm và bàn phím lưu riêng — người chơi đổi nút tay cầm không ảnh hưởng phím. Bắt đầu nghe khi THẢ nút để chính nút vừa bấm không bị gán nhầm.";

        public override string[] SetupSteps => new[]
        {
            "<b>StartRebinding(tis, \"Jump\", RebindTarget.Gamepad, callback, cancelPath: \"<Gamepad>/start\")</b>.",
            "Tên ngắn để hiển thị: <b>GetShortDisplayName(GetActivePath(...))</b> → \"Button South\", \"Right Trigger\"...",
            "<b>FindConflict</b> kiểm tra trùng nút · <b>ResetBinding</b> khôi phục mặc định.",
        };

        public override string Code =>
@"// Gọi khi người chơi THẢ nút ""Đổi""
TISRebindingSystem.StartRebinding(
    tis, ""Jump"", RebindTarget.Gamepad,
    r => label.text = r.Success
        ? TISRebindingSystem
            .GetShortDisplayName(r.NewPath)
        : label.text,
    cancelPath: ""<Gamepad>/start"");

TISRebindingSystem.ResetBinding(
    tis, ""Jump"", RebindTarget.Gamepad);";

        public override string[] Objectives => new[]
        {
            $"Thả {TISTutGlyphs.West} để đổi nút Nhảy — rồi bấm nút mới",
            "Nhảy bằng nút mới",
            $"Thả {TISTutGlyphs.North} để khôi phục mặc định",
        };

        public override string FooterHint => "X: đổi nút Nhảy · Y: khôi phục · Start: huỷ khi đang nghe";

        TMP_Text _jumpKey, _state;
        bool _rebound;

        protected override void OnEnter()
        {
            _rebound = false;
            Player.CanJump = true;
            GamepadCamera();

            var card = TISTutUI.Card(Overlay, "Rebind", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(380f, 180f));
            var head = TISTutUI.Text(card.transform, "Head", "NÚT TAY CẦM", Theme.monoBold, 16, Theme.inkSoft, TextAlignmentOptions.MidlineLeft);
            head.characterSpacing = 4f;
            TISTutUI.Place(head.rectTransform, 22f, 14f, 22f, -1f, height: 28f);
            var name = TISTutUI.Text(card.transform, "Name", "Nhảy", Theme.bodyBold, 20, Theme.ink, TextAlignmentOptions.MidlineLeft);
            TISTutUI.Place(name.rectTransform, 22f, 56f, -1f, -1f, width: 120f, height: 48f);
            var keyBg = TISTutUI.Shape(card.transform, "Key", Theme.paper, 10f, 2f, Theme.line);
            TISTutUI.Place(keyBg.rectTransform, 130f, 58f, 22f, -1f, height: 44f);
            _jumpKey = TISTutUI.Text(keyBg.transform, "Text", "—", Theme.monoBold, 17, Theme.ink, TextAlignmentOptions.Center);
            TISTutUI.Stretch(_jumpKey.rectTransform);
            _state = TISTutUI.Text(card.transform, "State", "", Theme.body, 15, Theme.inkSoft, TextAlignmentOptions.MidlineLeft, wrap: true);
            TISTutUI.Place(_state.rectTransform, 22f, 116f, 22f, 12f);
            Refresh();

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.75f);

            Watch("State", () => TISRebindingSystem.State.ToString());
            Watch("GetActivePath(\"Jump\")", () => TISRebindingSystem.GetActivePath(TIS, "Jump", RebindTarget.Gamepad) ?? "—");
            Watch("IsSkillDown(\"Jump\")", () => F(TIS.IsSkillDown("Jump")));
        }

        void Refresh()
        {
            string path = TISRebindingSystem.GetActivePath(TIS, "Jump", RebindTarget.Gamepad);
            _jumpKey.text = string.IsNullOrEmpty(path) ? "—" : TISRebindingSystem.GetShortDisplayName(path);
            _state.text = TISRebindingSystem.State == RebindState.Listening ? "Đang nghe… bấm nút mới (Start để huỷ)" : "Thả X để đổi · thả Y để khôi phục";
        }

        protected override void OnTick()
        {
            var tis = TIS;
            bool listening = TISRebindingSystem.State == RebindState.Listening;

            if (!listening && tis.IsSkillReleased("Interact"))
            {
                TISRebindingSystem.StartRebinding(tis, "Jump", RebindTarget.Gamepad, r =>
                {
                    Refresh();
                    if (!r.Success) { Toast("Đã huỷ"); return; }
                    string other = TISRebindingSystem.FindConflict(tis, "Jump", RebindTarget.Gamepad, r.NewPath);
                    Toast(other != null ? $"Trùng với \"{other}\" — vẫn dùng được, nhưng nên đổi" : "Nhảy → " + TISRebindingSystem.GetShortDisplayName(r.NewPath));
                    _rebound = true;
                    Complete(0);
                }, "<Gamepad>/start");
                Refresh();
            }

            if (!listening && tis.IsSkillReleased("Toggle"))
            {
                TISRebindingSystem.ResetBinding(tis, "Jump", RebindTarget.Gamepad);
                Refresh();
                Toast("Đã khôi phục nút mặc định");
                if (IsObjectiveDone(0)) Complete(2);
            }

            if (_rebound && !listening && tis.IsSkillDown("Jump"))
            {
                string path = TISRebindingSystem.GetActivePath(tis, "Jump", RebindTarget.Gamepad);
                if (path != "<Gamepad>/buttonSouth") Complete(1);
            }
        }

        protected override void OnExit()
        {
            if (TISRebindingSystem.State == RebindState.Listening) TISRebindingSystem.CancelRebinding();
        }
    }
}
