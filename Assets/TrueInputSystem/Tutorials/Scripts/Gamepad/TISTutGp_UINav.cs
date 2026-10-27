// ============================================================
//  TrueInputSystem Tutorials — Tay cầm · Điều hướng menu (TISGamepadUIButton + Context + Group)
// ============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/Lessons/Tay cầm · Điều hướng menu")]
    public class TISTutGp_UINav : TISTutLesson
    {
        public override string Title => "Điều hướng menu bằng tay cầm";

        public override string Summary =>
            "TISGamepadUIButton thay cho Button của Unity: D-pad / cần trái chuyển nút, A bấm, LT / RT đổi tab, LB / RB đổi tab con. TISUINavigationContext giữ trạng thái cả menu và tự lấy lại focus khi bị mất. TISGamepadUIButtonGroup tự nối điều hướng cho danh sách hay lưới.";

        public override string[] SetupSteps => new[]
        {
            "Thêm <b>TISUINavigationContext</b> vào gốc menu và khai báo Tabs (nút tab + nút đầu tiên của trang).",
            "Mọi nút dùng <b>TISGamepadUIButton</b>, chọn Layer: <b>Tab</b> (LT / RT), <b>SubTab</b> (LB / RB) hay <b>Content</b> (D-pad).",
            "Gom nút vào <b>TISGamepadUIButtonGroup</b> (Vertical / Horizontal / Grid) → <b>Rebuild Navigation</b>.",
            "Bàn phím: phím mũi tên chuyển nút, Q / E đổi tab — chạy song song với tay cầm.",
        };

        public override string Code =>
@"// Lưới 3 cột, tự nối lên/xuống/trái/phải
var g = grid.AddComponent<TISGamepadUIButtonGroup>();
g.layoutType = TISGamepadUIButtonGroup
               .GroupLayoutType.Grid;
g.gridColumns = 3;
g.RebuildNavigation();

// Tab đổi bằng LT / RT
tab.layer = TISGamepadUIButton
            .NavigationLayer.Tab;
tab.inputSource = TISGamepadUIButton
            .InputSource.LT_RT;
ctx.ScanChildButtons();";

        public override string[] Objectives => new[]
        {
            "Di chuyển focus qua 4 nút khác nhau",
            $"Nhấn {TISTutGlyphs.South} để bật / tắt một tuỳ chọn",
            $"Đổi sang tab Âm thanh bằng {TISTutGlyphs.RT} (bàn phím: E)",
        };

        public override string FooterHint => "D-pad / cần trái chuyển nút · A chọn · LT / RT đổi tab · bàn phím: mũi tên, Enter, Q / E";

        readonly HashSet<GameObject> _visited = new HashSet<GameObject>();
        readonly List<GameObject> _content = new List<GameObject>();
        TISGamepadUIButton _tabAudio;

        protected override void OnEnter()
        {
            _visited.Clear(); _content.Clear();
            Cam.LookEnabled = false;
            TIS.SetActionMapActive("Movement", false);     // cần trái dành cho menu

            var menu = TISTutUI.Card(Overlay, "Menu", new Vector2(0.5f, 0.5f), new Vector2(150f, -78f), new Vector2(620f, 380f));
            var ctx = menu.gameObject.AddComponent<TISUINavigationContext>();
            ctx.debugLog = false;

            var head = TISTutUI.Text(menu.transform, "Head", "CÀI ĐẶT", Theme.monoBold, 22, Theme.ink, TextAlignmentOptions.MidlineLeft);
            head.characterSpacing = 6f;
            TISTutUI.Place(head.rectTransform, 26f, 18f, -1f, -1f, width: 200f, height: 40f);

            var tabGfx  = TabButton(menu.transform, "ĐỒ HOẠ",   new Vector2(-170f, -38f));
            var tabSnd  = TabButton(menu.transform, "ÂM THANH", new Vector2(-24f, -38f));
            tabGfx.navRight = tabSnd; tabSnd.navLeft = tabGfx;
            _tabAudio = tabSnd;

            var pageGfx = Page(menu.transform, "Trang Đồ hoạ", new[] { "Bóng đổ: BẬT", "Khử răng cưa: BẬT", "V-Sync: TẮT", "Độ sáng: 70%", "Hiệu ứng: CAO", "FPS: 60" });
            var pageSnd = Page(menu.transform, "Trang Âm thanh", new[] { "Nhạc: BẬT", "Hiệu ứng: BẬT", "Giọng nói: BẬT", "Âm lượng: 80%", "Phụ đề: TẮT", "Rung: BẬT" });
            pageSnd.SetActive(false);

            tabGfx.onBecameActiveInLayer.AddListener(_ => { pageGfx.SetActive(true); pageSnd.SetActive(false); });
            tabSnd.onBecameActiveInLayer.AddListener(_ =>
            {
                pageGfx.SetActive(false); pageSnd.SetActive(true);
                Complete(2);
            });

            ctx.tabs.Add(new TISUINavigationContext.TabEntry { tabButton = tabGfx, firstContent = pageGfx.transform.GetChild(0).GetComponent<TISGamepadUIButton>() });
            ctx.tabs.Add(new TISUINavigationContext.TabEntry { tabButton = tabSnd, firstContent = pageSnd.transform.GetChild(0).GetComponent<TISGamepadUIButton>() });
            ctx.defaultTab = tabGfx;
            ctx.defaultContent = pageGfx.transform.GetChild(0).GetComponent<TISGamepadUIButton>();
            ctx.ScanChildButtons();

            GamepadWidget(new Vector2(0f, 0f), new Vector2(20f, 20f), 0.6f);

            Watch("currentSelectedGameObject", () => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                                                      ? EventSystem.current.currentSelectedGameObject.name : "—");
            Watch("Nút đã ghé", () => _visited.Count + " / 4", () => _visited.Count / 4f);
            Watch("Tab đang mở", () => pageSnd.activeSelf ? "Âm thanh" : "Đồ hoạ");
        }

        TISGamepadUIButton TabButton(Transform parent, string label, Vector2 pos)
        {
            var b = MakeButton(parent, label, new Vector2(1f, 1f), pos, new Vector2(136f, 44f));
            b.layer = TISGamepadUIButton.NavigationLayer.Tab;
            b.inputSource = TISGamepadUIButton.InputSource.LT_RT;
            b.navigationMode = TISGamepadUIButton.NavMode.Horizontal;
            b.alwaysActive = true;
            // Event không tự khởi tạo khi AddComponent lúc chạy (chỉ có khi tạo trong Editor)
            b.onBecameActiveInLayer ??= new TISGamepadUIButton.LayerChangedEvent();
            return b;
        }

        GameObject Page(Transform parent, string name, string[] items)
        {
            var page = TISTutUI.Rect(parent, name);
            TISTutUI.Place(page, 26f, 84f, 26f, 26f);
            var grid = page.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(176f, 120f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            foreach (var label in items)
            {
                var b = MakeButton(page, label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 120f));
                var text = b.GetComponentInChildren<TMP_Text>();
                b.onClick.AddListener(() =>
                {
                    text.text = text.text.Contains("BẬT") ? text.text.Replace("BẬT", "TẮT")
                              : text.text.Contains("TẮT") ? text.text.Replace("TẮT", "BẬT")
                              : text.text;
                    Complete(1);
                });
                _content.Add(b.gameObject);
            }
            var group = page.gameObject.AddComponent<TISGamepadUIButtonGroup>();
            group.layoutType = TISGamepadUIButtonGroup.GroupLayoutType.Grid;
            group.gridColumns = 3;
            group.RebuildNavigation();
            return page.gameObject;
        }

        TISGamepadUIButton MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var bg = TISTutUI.Shape(parent, label, Theme.paper, 14f, 2f, Theme.line);
            TISTutUI.At(bg.rectTransform, anchor, pos, size);
            var t = TISTutUI.Text(bg.transform, "Label", label, Theme.bodyBold, 17, Theme.ink, TextAlignmentOptions.Center, wrap: true);
            TISTutUI.Stretch(t.rectTransform);
            t.margin = new Vector4(10f, 6f, 10f, 6f);

            var b = bg.gameObject.AddComponent<TISGamepadUIButton>();
            b.onSelect   ??= new TISGamepadUIButton.SelectEvent();
            b.onDeselect ??= new TISGamepadUIButton.SelectEvent();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            Color accent = Theme.Accent();
            colors.normalColor      = Color.white;
            colors.highlightedColor = accent;
            colors.selectedColor    = accent;
            colors.pressedColor     = Theme.AccentStrong();
            colors.fadeDuration     = 0.08f;
            b.colors = colors;
            return b;
        }

        protected override void OnTick()
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && _content.Contains(sel))
            {
                _visited.Add(sel);
                if (_visited.Count >= 4) Complete(0);
            }
        }

        protected override void OnExit()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            TIS.EnableDefaultMaps();
        }
    }
}
