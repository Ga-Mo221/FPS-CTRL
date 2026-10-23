// [Aemy 2026-09-27] Bang chon avatar (preset). Dung UI dung BANG CODE luc runtime -> khong can chinh scene.
// Cach dung: AvatarPickerController.Open(url => { ... refresh ... });
//   -> mo overlay luoi avatar; tap 1 cai -> SetAvatarUrl len PlayFab -> goi onPicked(url) + dong.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.Bootstrap;
using Backend.PlayfabImpl;

namespace Game.UI
{
    public class AvatarPickerController : MonoBehaviour
    {
        private Action<string> _onPicked;
        private RectTransform _grid;
        private GameObject _statusGo;
        private TMP_Text _statusText;
        private bool _busy;
        private bool _suppressed;
        // [Aemy 2026-10-10] Tay cam: focus o avatar, cuon theo focus, dong thi tra focus cu.
        private GameObject _prevFocus;
        private ScrollRect _scroll;
        private Button _firstCell;

        public static void Open(Action<string> onPicked)
        {
            var go = new GameObject("~AvatarPicker");
            var ctrl = go.AddComponent<AvatarPickerController>();
            ctrl._onPicked = onPicked;
            ctrl.Build();
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress++; _suppressed = true; // chan input menu phia sau
            var es0 = UnityEngine.EventSystems.EventSystem.current;
            _prevFocus = es0 != null ? es0.currentSelectedGameObject : null; // [Aemy 2026-10-10]

            var theme = PopupTheme.Default;   // [Aemy 2026-10-11] cung nen/khung/font voi cac popup khac

            // Nen mo (bam ra ngoai = dong)
            var dim = NewImage(transform, "Dim", theme != null ? theme.dim : new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None }; // [Aemy] nen mo khong nhan focus tay cam
            dimBtn.onClick.AddListener(Close);

            // Panel giua
            var panel = NewImage(transform, "Panel", new Color(0.10f, 0.12f, 0.16f, 1f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760, 560);
            prt.anchoredPosition = Vector2.zero;
            bool themed = theme != null && theme.DressPanel(panel);
            if (themed) panel.raycastTarget = true;   // chan click xuyen xuong nen mo (khong dong khi bam trong panel)

            var title = NewText(panel.transform, "Title", "CHỌN AVATAR", 34, TextAlignmentOptions.Left);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(28, -20); trt.sizeDelta = new Vector2(-120, 48);
            if (theme != null) theme.StyleTitle(title);
            if (themed)
            {
                // gach accent duoi tieu de
                var bar = NewImage(panel.transform, "TitleBar", theme.accent);
                var brt = bar.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, 1);
                brt.anchoredPosition = new Vector2(30, -68); brt.sizeDelta = new Vector2(64, 3);
                bar.raycastTarget = false;
            }

            var closeImg = NewImage(panel.transform, "Close", new Color(0.85f, 0.22f, 0.22f, 1f));
            var crt = closeImg.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1);
            crt.sizeDelta = new Vector2(44, 44); crt.anchoredPosition = new Vector2(-18, -18);
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.onClick.AddListener(Close);
            closeImg.gameObject.AddComponent<TISSelectHighlight>(); // [Aemy] vien focus khi tay cam chon nut X
            var cx = NewText(closeImg.transform, "X", "X", 26, TextAlignmentOptions.Center); Stretch(cx.rectTransform);
            if (theme != null) theme.StyleTitle(cx);

            var st = NewText(panel.transform, "Status", "Đang tải avatar...", 20, TextAlignmentOptions.Center);
            _statusGo = st.gameObject; _statusText = st;
            if (theme != null) theme.StyleBody(st);
            var srt = st.rectTransform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = new Vector2(0, 16); srt.sizeDelta = new Vector2(-40, 30);

            // Vung cuon + luoi
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrt = scrollGo.GetComponent<RectTransform>();
            scrt.anchorMin = new Vector2(0, 0); scrt.anchorMax = new Vector2(1, 1); scrt.pivot = new Vector2(0.5f, 1);
            scrt.offsetMin = new Vector2(20, 54); scrt.offsetMax = new Vector2(-20, -78);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24f;

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scrollGo.transform, false);
            _grid = content.GetComponent<RectTransform>();
            _grid.anchorMin = new Vector2(0, 1); _grid.anchorMax = new Vector2(1, 1); _grid.pivot = new Vector2(0.5f, 1);
            _grid.anchoredPosition = Vector2.zero; _grid.sizeDelta = new Vector2(0, 0);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150, 150); grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _grid;
            _scroll = scroll;

            AvatarService.LoadList(list =>
            {
                if (this == null || _grid == null) return;
                if (_statusGo != null) _statusGo.SetActive(false);
                if (list != null) foreach (var a in list) AddCell(a);
                FocusFirstForGamepad(); // [Aemy 2026-10-10]
            });
        }

        private void AddCell(AvatarInfo a)
        {
            if (a == null || string.IsNullOrEmpty(a.url) || _grid == null) return;
            // [Aemy 2026-10-11] O = nen toi (theme) + anh avatar ben trong (avatar PNG trong suot van co nen).
            var th = PopupTheme.Default;
            var tile = NewImage(_grid, string.IsNullOrEmpty(a.id) ? "cell" : a.id, th != null ? th.cellColor : new Color(0.18f, 0.20f, 0.26f, 1f));
            var cellBtn = tile.gameObject.AddComponent<Button>();
            cellBtn.onClick.AddListener(() => Pick(a));
            tile.gameObject.AddComponent<TISSelectHighlight>(); // [Aemy 2026-10-10] vien teal khi tay cam chon
            var cellImg = NewImage(tile.transform, "Avatar", new Color(1f, 1f, 1f, 0f));
            Stretch(cellImg.rectTransform);
            cellImg.rectTransform.offsetMin = new Vector2(8, 8); cellImg.rectTransform.offsetMax = new Vector2(-8, -8);
            cellImg.preserveAspect = true; cellImg.raycastTarget = false;
            if (_firstCell == null) _firstCell = cellBtn;
            Game.Shop.ShopSpriteLoader.Load(a.url, spr => { if (cellImg != null && spr != null) { cellImg.color = Color.white; cellImg.sprite = spr; } });
        }

        private void Pick(AvatarInfo a)
        {
            if (_busy || a == null || string.IsNullOrEmpty(a.url)) return;
            var svc = BackendBootstrap.PlayerProfileService;
            if (svc == null) { SetStatus("Backend chưa sẵn sàng."); return; }
            _busy = true;
            SetStatus("Đang lưu avatar...");
            svc.SetAvatarUrl(a.url,
                () => { _busy = false; ProfileCache.Invalidate(); if (_onPicked != null) _onPicked(a.url); Close(); },
                e => { _busy = false; SetStatus("Lỗi: " + (e != null ? e.ErrorMessage : "khong ro")); });
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
            if (_statusGo != null) _statusGo.SetActive(true);
        }

        private void Close() { if (this != null && gameObject != null) Destroy(gameObject); }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame)) { Close(); return; }

            // [Aemy 2026-10-10] Tay cam: neu chua co gi duoc chon ma nguoi choi bam dpad -> chon avatar dau; cuon theo focus.
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var sel = es.currentSelectedGameObject;
            if ((sel == null || !sel.transform.IsChildOf(transform)) && gp != null
                && (gp.dpad.ReadValue().sqrMagnitude > 0.25f || gp.leftStick.ReadValue().sqrMagnitude > 0.25f))
            { FocusFirst(); return; }
            if (sel != null && _grid != null && sel.transform.parent == _grid) KeepVisible((RectTransform)sel.transform);
        }

        private bool UsingGamepad()
        {
            var br = GaMo.TrueInputSystem.TISInputBridge.Instance;
            return br != null ? br.IsGamepad : UnityEngine.InputSystem.Gamepad.current != null;
        }

        private void FocusFirstForGamepad() { if (UsingGamepad()) FocusFirst(); }

        private void FocusFirst()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && _firstCell != null) es.SetSelectedGameObject(_firstCell.gameObject);
        }

        // Cuon ScrollRect de o dang chon nam trong vung nhin.
        private void KeepVisible(RectTransform cell)
        {
            if (_scroll == null || _scroll.viewport == null && _scroll.GetComponent<RectTransform>() == null) return;
            var view = _scroll.viewport != null ? _scroll.viewport : _scroll.GetComponent<RectTransform>();
            float contentH = _grid.rect.height, viewH = view.rect.height;
            if (contentH <= viewH + 1f) return;
            Vector3 cTop = view.InverseTransformPoint(cell.TransformPoint(new Vector3(0, cell.rect.yMax, 0)));
            Vector3 cBot = view.InverseTransformPoint(cell.TransformPoint(new Vector3(0, cell.rect.yMin, 0)));
            float vTop = view.rect.yMax, vBot = view.rect.yMin, shift = 0f;
            if (cTop.y > vTop) shift = cTop.y - vTop;          // o nam tren vung nhin -> keo noi dung xuong
            else if (cBot.y < vBot) shift = cBot.y - vBot;     // o nam duoi -> keo noi dung len
            if (Mathf.Abs(shift) < 0.5f) return;
            var p = _grid.anchoredPosition; p.y -= shift;
            p.y = Mathf.Clamp(p.y, 0f, contentH - viewH);
            _grid.anchoredPosition = p;
        }

        private void OnDestroy()
        {
            if (_suppressed) { GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress - 1); _suppressed = false; }
            // [Aemy 2026-10-10] tra focus ve nut da mo bang chon avatar
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && _prevFocus != null && _prevFocus.activeInHierarchy) es.SetSelectedGameObject(_prevFocus);
        }

        // ---- Helpers dung UI ----
        private static Image NewImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = c;
            return img;
        }

        private static TMP_Text NewText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
