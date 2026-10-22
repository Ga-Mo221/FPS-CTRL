// ============================================================
//  TrueInputSystem Tutorials — TISTutGamepadWidget.cs
//
//  Hình tay cầm minh hoạ trực tiếp trạng thái Gamepad.current:
//  nút sáng khi nhấn, chấm cần analog di chuyển, cò có thanh mức.
//  Chỉ để HIỂN THỊ phần cứng — logic game luôn đọc qua TISInputBridge.
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Gamepad Widget")]
    public class TISTutGamepadWidget : MonoBehaviour
    {
        struct Btn
        {
            public TISTutShape shape;
            public System.Func<Gamepad, bool> pressed;
        }

        readonly System.Collections.Generic.List<Btn> _buttons = new System.Collections.Generic.List<Btn>();
        RectTransform _lDot, _rDot;
        TISTutShape   _lt, _rt;
        TMP_Text      _name;
        TMP_Text      _south, _east, _west, _north, _lb, _rb, _ltLabel, _rtLabel;
        const float StickTravel = 18f;

        public static TISTutGamepadWidget Create(Transform parent, Vector2 anchor, Vector2 pos, float scale = 1f)
        {
            var th = TISTutTheme.Current;
            var root = TISTutUI.Rect(parent, "Gamepad Widget");
            TISTutUI.At(root, anchor, pos, new Vector2(420f, 270f));
            root.localScale = Vector3.one * scale;
            var w = root.gameObject.AddComponent<TISTutGamepadWidget>();

            // Cò + vai
            w._lt = w.Trigger(root, new Vector2(70f, 250f), out w._ltLabel);
            w._rt = w.Trigger(root, new Vector2(350f, 250f), out w._rtLabel);
            w.Add(root, new Vector2(70f, 214f), new Vector2(110f, 26f), 10f, gp => gp.leftShoulder.isPressed, TISTutGlyphs.LB, out w._lb);
            w.Add(root, new Vector2(350f, 214f), new Vector2(110f, 26f), 10f, gp => gp.rightShoulder.isPressed, TISTutGlyphs.RB, out w._rb);

            // Thân
            var body = TISTutUI.Shape(root, "Body", th.card, 46f, 2.5f, th.ink);
            TISTutUI.At(body.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420f, 200f));
            body.rectTransform.pivot = Vector2.zero;
            var b = body.transform;

            w._name = TISTutUI.Text(b, "Name", "", th.monoBold, 13, th.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.At(w._name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(240f, 20f));

            // Cần trái / phải
            w._lDot = w.Stick(b, new Vector2(95f, 120f), gp => gp.leftStickButton.isPressed);
            w._rDot = w.Stick(b, new Vector2(270f, 60f), gp => gp.rightStickButton.isPressed);

            // D-pad
            Vector2 dp = new Vector2(150f, 60f);
            w.Add(b, dp + new Vector2(0f, 24f),  new Vector2(22f, 22f), 5f, gp => gp.dpad.up.isPressed,    "", out _);
            w.Add(b, dp + new Vector2(0f, -24f), new Vector2(22f, 22f), 5f, gp => gp.dpad.down.isPressed,  "", out _);
            w.Add(b, dp + new Vector2(-24f, 0f), new Vector2(22f, 22f), 5f, gp => gp.dpad.left.isPressed,  "", out _);
            w.Add(b, dp + new Vector2(24f, 0f),  new Vector2(22f, 22f), 5f, gp => gp.dpad.right.isPressed, "", out _);

            // ABXY
            Vector2 fb = new Vector2(330f, 120f);
            w.Add(b, fb + new Vector2(0f, -28f), new Vector2(34f, 34f), 17f, gp => gp.buttonSouth.isPressed, TISTutGlyphs.South, out w._south);
            w.Add(b, fb + new Vector2(28f, 0f),  new Vector2(34f, 34f), 17f, gp => gp.buttonEast.isPressed,  TISTutGlyphs.East,  out w._east);
            w.Add(b, fb + new Vector2(-28f, 0f), new Vector2(34f, 34f), 17f, gp => gp.buttonWest.isPressed,  TISTutGlyphs.West,  out w._west);
            w.Add(b, fb + new Vector2(0f, 28f),  new Vector2(34f, 34f), 17f, gp => gp.buttonNorth.isPressed, TISTutGlyphs.North, out w._north);

            // View / Menu
            w.Add(b, new Vector2(180f, 140f), new Vector2(34f, 16f), 8f, gp => gp.selectButton.isPressed, "", out _);
            w.Add(b, new Vector2(240f, 140f), new Vector2(34f, 16f), 8f, gp => gp.startButton.isPressed,  "", out _);
            return w;
        }

        void Add(Transform parent, Vector2 center, Vector2 size, float radius, System.Func<Gamepad, bool> pressed,
                 string label, out TMP_Text text)
        {
            var th = TISTutTheme.Current;
            var s = TISTutUI.Shape(parent, "Btn " + label, th.card, radius, 2f, th.ink);
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            text = null;
            if (!string.IsNullOrEmpty(label))
            {
                text = TISTutUI.Text(s.transform, "Label", label, th.monoBold, 15, th.ink, TextAlignmentOptions.Center);
                TISTutUI.Stretch(text.rectTransform);
            }
            _buttons.Add(new Btn { shape = s, pressed = pressed });
        }

        RectTransform Stick(Transform parent, Vector2 center, System.Func<Gamepad, bool> click)
        {
            var th = TISTutTheme.Current;
            var ring = TISTutUI.Shape(parent, "Stick", th.paper, 34f, 2f, th.ink);
            var rt = ring.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(68f, 68f);
            var dot = TISTutUI.Shape(ring.transform, "Dot", th.ink, 15f);
            var drt = dot.rectTransform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(30f, 30f);
            _buttons.Add(new Btn { shape = dot, pressed = click });
            return drt;
        }

        TISTutShape Trigger(Transform parent, Vector2 center, out TMP_Text label)
        {
            var th = TISTutTheme.Current;
            var track = TISTutUI.Shape(parent, "Trigger", th.track, 7f, 2f, th.ink);
            var rt = track.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(110f, 16f);
            var fill = TISTutUI.Shape(track.transform, "Fill", th.AccentStrong(), 7f);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0.001f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            label = TISTutUI.Text(parent, "Trigger Label", "", th.monoBold, 13, th.inkSoft, TextAlignmentOptions.Center);
            TISTutUI.At(label.rectTransform, Vector2.zero, center + new Vector2(0f, 18f), new Vector2(110f, 18f));
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = center + new Vector2(0f, 18f);
            return fill;
        }

        void Update()
        {
            var th = TISTutTheme.Current;
            var gp = Gamepad.current;

            if (_name != null) _name.text = TISTutGlyphs.DeviceName.ToUpperInvariant();
            if (_south != null)
            {
                _south.text = TISTutGlyphs.South; _east.text = TISTutGlyphs.East;
                _west.text  = TISTutGlyphs.West;  _north.text = TISTutGlyphs.North;
                _lb.text = TISTutGlyphs.LB; _rb.text = TISTutGlyphs.RB;
                _ltLabel.text = TISTutGlyphs.LT; _rtLabel.text = TISTutGlyphs.RT;
            }

            Color on = th.AccentStrong();
            foreach (var b in _buttons)
            {
                bool p = gp != null && b.pressed(gp);
                bool isDot = b.shape.name == "Dot";
                b.shape.color = p ? on : (isDot ? th.ink : th.card);
            }

            Vector2 l = gp != null ? gp.leftStick.ReadValue() : Vector2.zero;
            Vector2 r = gp != null ? gp.rightStick.ReadValue() : Vector2.zero;
            _lDot.anchoredPosition = l * StickTravel;
            _rDot.anchoredPosition = r * StickTravel;
            SetFill(_lt, gp != null ? gp.leftTrigger.ReadValue() : 0f);
            SetFill(_rt, gp != null ? gp.rightTrigger.ReadValue() : 0f);
        }

        static void SetFill(TISTutShape fill, float v)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, Mathf.Clamp01(v)), 1f);
        }
    }
}
