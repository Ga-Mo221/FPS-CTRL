// ============================================================
//  TrueInputSystem Tutorials — TISTutUI.cs
//
//  Factory dựng UI theo phong cách hướng dẫn (dùng cả lúc chạy lẫn Editor builder):
//    Rect · Shape · Text · Button (kiểu nút chéo + ô mũi tên) · KeyCap · Card · Badge
//  Màu + font lấy từ TISTutTheme.Current.
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials
{
    public static class TISTutUI
    {
        static TISTutTheme Th => TISTutTheme.Current;

        // ── Khối cơ bản ───────────────────────────────────────

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static TISTutShape Shape(Transform parent, string name, Color color, float radius,
                                        float border = 0f, Color? borderColor = null)
        {
            var rt = Rect(parent, name);
            var s = rt.gameObject.AddComponent<TISTutShape>();
            s.color = color;
            s.Radius = radius;
            if (border > 0f)
            {
                s.BorderWidth = border;
                s.BorderColor = borderColor ?? Th.line;
            }
            return s;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size,
                                           Color color, TextAlignmentOptions align, bool wrap = false)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        /// <summary>Card trắng bo góc, kích thước cố định, neo theo anchor/pivot cho trước.</summary>
        public static TISTutShape Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
                                       Color? color = null, float radius = 18f)
        {
            var c = Shape(parent, name, color ?? Th.card, radius);
            var rt = c.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return c;
        }

        /// <summary>Nhãn dạng viên (chip) có nền màu.</summary>
        public static TextMeshProUGUI Badge(Transform parent, string name, string text, Color bg, Color fg,
                                            float w, float h, float fontSize = 16f)
        {
            var s = Shape(parent, name, bg, h * 0.3f);
            s.rectTransform.sizeDelta = new Vector2(w, h);
            var t = Text(s.transform, "Text", text, Th.monoBold, fontSize, fg, TextAlignmentOptions.Center);
            t.characterSpacing = 4f;
            Stretch(t.rectTransform);
            return t;
        }

        // ── Nút ───────────────────────────────────────────────

        /// <summary>
        /// Nút kiểu ảnh mẫu: thân nghiêng + (tuỳ chọn) ô biểu tượng tách rời.
        /// capIcon = null → nút không có ô mũi tên (tab, nút nhỏ).
        /// </summary>
        public static TISTutButton Button(Transform parent, string name, string label, TISTutButton.Style style,
                                          float w, float h, string capIcon = null, bool capLeft = false,
                                          float fontSize = 18f, float slant = 10f, float labelLeft = -1f,
                                          bool mono = true)
        {
            var root = Rect(parent, name);
            root.sizeDelta = new Vector2(w, h);
            var btn = root.gameObject.AddComponent<TISTutButton>();

            const float gap = 7f;
            bool hasCap = !string.IsNullOrEmpty(capIcon);
            float capW = hasCap ? h * 1.08f : 0f;

            var body = Shape(root, "Body", Th.ink, h * 0.28f);
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(hasCap && capLeft ? capW + gap : 0f, 0f);
            brt.offsetMax = new Vector2(hasCap && !capLeft ? -(capW + gap) : 0f, 0f);
            if (hasCap && capLeft) body.SlantLeft = slant;   // song song cạnh phải của ô mũi tên
            else                   body.SlantRight = slant;

            var text = Text(body.transform, "Label", label, mono ? Th.mono : Th.body, fontSize, Th.paper,
                            labelLeft >= 0f ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            if (mono) text.characterSpacing = 5f;
            Stretch(text.rectTransform);
            if (labelLeft >= 0f) text.rectTransform.offsetMin = new Vector2(labelLeft, 0f);

            TISTutShape cap = null;
            TMP_Text icon = null;
            if (hasCap)
            {
                cap = Shape(root, "Cap", Th.Accent(), h * 0.28f);
                var crt = cap.rectTransform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(capLeft ? 0f : 1f, 0.5f);
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = new Vector2(capW, h);
                if (capLeft) cap.SlantRight = slant; else cap.SlantLeft = slant;
                icon = Text(cap.transform, "Icon", capIcon, Th.monoBold, fontSize + 6f, Th.ink, TextAlignmentOptions.Center);
                Stretch(icon.rectTransform);
            }

            btn.Setup(body, cap, text, icon, style, new Vector2(capLeft ? -6f : 6f, 0f));
            return btn;
        }

        // ── Phím minh hoạ ─────────────────────────────────────

        /// <summary>Phím sáng lên khi control thật (controlPath) được nhấn.</summary>
        public static TISTutKeyCap KeyCap(Transform parent, string label, string controlPath,
                                          float w = 56f, float h = 56f, float fontSize = 20f)
        {
            var root = Rect(parent, "Key " + label);
            root.sizeDelta = new Vector2(w, h);
            var shape = Shape(root, "Cap", Th.card, 12f, 2.5f, Th.ink);
            Stretch(shape.rectTransform);
            var t = Text(shape.transform, "Label", label, Th.monoBold, fontSize, Th.ink, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            var kc = root.gameObject.AddComponent<TISTutKeyCap>();
            kc.Setup(shape, t, controlPath);
            return kc;
        }

        // ── Bố cục ────────────────────────────────────────────

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void AnchorLeft(RectTransform rt, float x)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        /// <summary>Đặt tại anchor/pivot + toạ độ, kích thước cố định.</summary>
        public static void At(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>
        /// Đặt theo lề (left, top, right, bottom) tính từ cạnh cha. Lề âm = "không neo cạnh đó"
        /// — khi đó dùng width / height.
        /// </summary>
        public static void Place(RectTransform rt, float left, float top, float right, float bottom,
                                 float width = 0f, float height = 0f)
        {
            bool l = left >= 0f, r = right >= 0f, t = top >= 0f, b = bottom >= 0f;

            float axMin = l ? 0f : 1f, axMax = r ? 1f : 0f;
            if (!l && !r) { axMin = axMax = 0.5f; }
            float ayMin = b ? 0f : 1f, ayMax = t ? 1f : 0f;
            if (!t && !b) { ayMin = ayMax = 0.5f; }

            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.pivot = new Vector2(l ? 0f : r ? 1f : 0.5f, t ? 1f : b ? 0f : 0.5f);

            float ox0, ox1, oy0, oy1;
            if (l && r)      { ox0 = left;           ox1 = -right; }
            else if (l)      { ox0 = left;           ox1 = left + width; }
            else if (r)      { ox0 = -right - width; ox1 = -right; }
            else             { ox0 = -width * 0.5f;  ox1 = width * 0.5f; }

            if (t && b)      { oy0 = bottom;         oy1 = -top; }
            else if (t)      { oy0 = -top - height;  oy1 = -top; }
            else if (b)      { oy0 = bottom;         oy1 = bottom + height; }
            else             { oy0 = -height * 0.5f; oy1 = height * 0.5f; }

            rt.offsetMin = new Vector2(ox0, oy0);
            rt.offsetMax = new Vector2(ox1, oy1);
        }

        /// <summary>Hàng ngang tự xếp con (cho cụm phím).</summary>
        public static RectTransform Row(Transform parent, string name, float spacing = 8f)
        {
            var rt = Rect(parent, name);
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }
    }
}
