// [Aemy 2026-10-17] FILE MOI. Menu ngu canh (chuot phai) dung chung: dung UI luc chay, theo PopupTheme.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// UIContextMenu.Show(viTriManHinh, items, tieuDe): hien danh sach lua chon tai con tro.
    /// Bam ra ngoai / Esc / nut B tay cam -> dong. Dieu huong bang mui ten / D-pad, Enter / A de chon.
    /// Chi lo hien thi + goi Action cua item (SRP); ai mo menu tu quyet dinh co nhung lua chon gi.
    /// </summary>
    public class UIContextMenu : MonoBehaviour
    {
        public struct Item
        {
            public string Label;
            public Action Action;
            public bool Danger;
            public Item(string label, Action action, bool danger = false) { Label = label; Action = action; Danger = danger; }
        }

        private static UIContextMenu s_open;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Image> _bgs = new List<Image>();
        private readonly List<GameObject> _rings = new List<GameObject>(); // [Aemy 2026-10-19] vien chon tay cam
        private int _focus = -1;
        private bool _suppressing;
        private float _openedAt;

        private static readonly Color Hover = new Color(0.31f, 0.84f, 0.79f, 0.22f);
        private static readonly Color DangerText = new Color(0.92f, 0.30f, 0.27f, 1f);

        public static bool IsOpen { get { return s_open != null; } }

        public static void Close()
        {
            if (s_open != null) Destroy(s_open.gameObject);
            s_open = null;
        }

        public static void Show(Vector2 screenPos, IList<Item> items, string title = null)
        {
            Close();
            if (items == null || items.Count == 0) return;
            var theme = PopupTheme.Default;

            var root = new GameObject("UIContextMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var menu = root.AddComponent<UIContextMenu>();
            s_open = menu;

            // Lop chan: bam ra ngoai (trai hoac phai) -> dong.
            var blocker = NewChild("Blocker", root.transform);
            Stretch(blocker);
            var bimg = blocker.gameObject.AddComponent<Image>(); bimg.color = new Color(0, 0, 0, 0.001f);
            var bbtn = blocker.gameObject.AddComponent<Button>(); bbtn.transition = Selectable.Transition.None; bbtn.onClick.AddListener(Close);
            RightClickRelay.On(blocker.gameObject).RightClicked += _ => Close();

            var panel = NewChild("Panel", root.transform);
            panel.pivot = new Vector2(0f, 1f); panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.color = theme != null ? new Color(theme.cellColor.r, theme.cellColor.g, theme.cellColor.b, 0.97f) : new Color(0.05f, 0.09f, 0.10f, 0.97f);
            var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.31f, 0.84f, 0.79f, 0.55f); outline.effectDistance = new Vector2(1, -1);
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6); vlg.spacing = 2; vlg.childControlHeight = true; vlg.childControlWidth = true; vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            panel.sizeDelta = new Vector2(250, 0);

            if (!string.IsNullOrEmpty(title))
            {
                var tt = NewText("Title", panel, title, 15, theme);
                tt.color = theme != null ? theme.accent : new Color(0.31f, 0.84f, 0.79f, 1f);
                tt.fontStyle = FontStyles.Bold;
                var le = tt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 30;
                tt.margin = new Vector4(10, 0, 10, 0);
            }

            foreach (var it in items)
            {
                var row = NewChild("Item_" + it.Label, panel);
                var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 40;
                var img = row.gameObject.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var act = it.Action;
                btn.onClick.AddListener(() => { Close(); if (act != null) act(); });
                var txt = NewText("Label", row, it.Label, 18, theme);
                Stretch(txt.rectTransform); txt.margin = new Vector4(14, 0, 10, 0);
                if (it.Danger) txt.color = DangerText;
                int idx = menu._buttons.Count;
                var trig = row.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => menu.SetFocus(idx));
                trig.triggers.Add(enter);
                menu._buttons.Add(btn); menu._bgs.Add(img);
                menu._rings.Add(TISSelectHighlight.CreateRing(row, new Color(0.31f, 0.86f, 0.80f, 1f), 2f, 0f));
            }

            // Dat tai con tro, kep trong man hinh.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            var rootRt = (RectTransform)root.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screenPos, null, out local);
            Vector2 half = rootRt.rect.size * 0.5f;
            Vector2 size = panel.rect.size;
            local.x = Mathf.Clamp(local.x, -half.x + 4, half.x - size.x - 4);
            local.y = Mathf.Clamp(local.y, -half.y + size.y + 4, half.y - 4);
            panel.anchoredPosition = local;

            menu._openedAt = Time.unscaledTime;
            GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress++; menu._suppressing = true; // chan dieu huong menu phia sau
            // [Aemy 2026-10-19] Mo bang tay cam -> chon san dong dau (co vien) de biet dang o dau.
            var br = GaMo.TrueInputSystem.TISInputBridge.Instance;
            if (br != null && br.IsGamepad) menu.SetFocus(0);
        }

        private void SetFocus(int i)
        {
            _focus = i;
            for (int k = 0; k < _bgs.Count; k++) if (_bgs[k] != null) _bgs[k].color = k == i ? Hover : new Color(1, 1, 1, 0f);
            for (int k = 0; k < _rings.Count; k++) if (_rings[k] != null) _rings[k].SetActive(k == i);
        }

        private void Update()
        {
            if (Time.unscaledTime - _openedAt < 0.1f) return; // bo qua phim/nut cua chinh lan mo menu
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (back) { Close(); return; }
            bool down = (kb != null && kb.downArrowKey.wasPressedThisFrame) || (gp != null && gp.dpad.down.wasPressedThisFrame);
            bool up = (kb != null && kb.upArrowKey.wasPressedThisFrame) || (gp != null && gp.dpad.up.wasPressedThisFrame);
            if (_buttons.Count > 0 && (down || up))
                SetFocus(_focus < 0 ? 0 : (_focus + (down ? 1 : -1) + _buttons.Count) % _buttons.Count);
            bool ok = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            if (ok && _focus >= 0 && _focus < _buttons.Count && _buttons[_focus] != null) _buttons[_focus].onClick.Invoke();
        }

        private void OnDestroy()
        {
            if (_suppressing) { GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress - 1); _suppressing = false; }
            if (s_open == this) s_open = null;
        }

        // ---- helpers ----
        private static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, PopupTheme theme)
        {
            var rt = NewChild(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
            if (theme != null) theme.StyleBody(t);
            t.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            return t;
        }
    }
}
