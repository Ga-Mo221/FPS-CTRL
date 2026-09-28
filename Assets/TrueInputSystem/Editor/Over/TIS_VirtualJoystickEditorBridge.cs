#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using GaMo.TrueInputSystem;

namespace GaMo.TrueInputSystem.Editor
{
    [InitializeOnLoad]
    public static class TISVirtualJoystickEditorBridge
    {
        static TISVirtualJoystickEditorBridge()
        {
            TISVirtualJoystick.EditorOnGUIOverride = DrawDebugPreview;
        }

        private static void DrawDebugPreview(TISVirtualJoystick joystick)
        {
            var accessor = new JoystickAccessor(joystick);

            const float phoneW  = 220f;
            const float phoneH  = 390f;
            const float marginX = 12f;
            const float marginY = 12f;

            float phoneX = marginX;
            float phoneY = marginY;

            float refW = accessor.CanvasWidth;
            float refH = accessor.CanvasHeight;

            float scaleX = phoneW / refW;
            float scaleY = phoneH / refH;
            float scale  = Mathf.Min(scaleX, scaleY);

            var oldColor = GUI.color;

            // ── Viền điện thoại ──
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.97f);
            GUI.DrawTexture(new Rect(phoneX - 6, phoneY - 6, phoneW + 12, phoneH + 12), GetSolidTex());

            // ── Màn hình ──
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.99f);
            GUI.DrawTexture(new Rect(phoneX, phoneY, phoneW, phoneH), GetSolidTex());

            // ── Phân vùng ──
            float activationRatio = joystick.ActivationZoneWidthRatio;
            float divX = phoneX + activationRatio * phoneW;

            GUI.color = new Color(0.3f, 0.6f, 1f, 0.07f);
            GUI.DrawTexture(new Rect(phoneX, phoneY, divX - phoneX, phoneH), GetSolidTex());

            GUI.color = new Color(0.2f, 0.9f, 0.5f, 0.05f);
            GUI.DrawTexture(new Rect(divX, phoneY, phoneX + phoneW - divX, phoneH), GetSolidTex());

            DrawGUILine(
                new Vector2(divX, phoneY),
                new Vector2(divX, phoneY + phoneH),
                new Color(0.45f, 0.68f, 1f, 0.55f), 1.5f);

            // ── Tâm joystick ──
            float r       = accessor.Radius;
            float joyCanX = accessor.AnchoredPositionX;
            float joyCanY = accessor.AnchoredPositionY;

            float joyGUIx = phoneX + (joyCanX + refW * 0.5f) * scaleX;
            float joyGUIy = phoneY + (refH * 0.5f - joyCanY) * scaleY;

            float bgRadGUI   = r * scale;
            float knobRatio  = accessor.KnobSizeRatioDebug;
            float knobRadGUI = bgRadGUI * knobRatio;

            DrawFilledCircle(new Vector2(joyGUIx, joyGUIy), bgRadGUI,   new Color(1f, 1f, 1f, 0.07f));
            DrawCircleOutline(new Vector2(joyGUIx, joyGUIy), bgRadGUI,  new Color(0.4f, 0.65f, 1f, 0.8f), 2f);
            DrawFilledCircle(new Vector2(joyGUIx, joyGUIy), knobRadGUI, new Color(1f, 1f, 1f, 0.2f));
            DrawCircleOutline(new Vector2(joyGUIx, joyGUIy), knobRadGUI,new Color(1f, 0.85f, 0.3f, 0.95f), 1.5f);

            // ── Labels ──
            var styleL = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.55f, 0.78f, 1f, 0.85f) } };
            GUI.color = Color.white;
            GUI.Label(new Rect(phoneX, phoneY + 3, divX - phoneX, 16), "Joystick ◀", styleL);
            var styleR = new GUIStyle(styleL) { normal = { textColor = new Color(0.3f, 0.92f, 0.62f, 0.85f) } };
            GUI.Label(new Rect(divX, phoneY + 3, phoneX + phoneW - divX, 16), "▶ Camera", styleR);

            var styleInfo = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.7f, 0.85f, 1f, 0.85f) } };
            string info = $"BG ⌀ {r * 2f:F0} px  |  Knob ⌀ {r * 2f * knobRatio:F0} px\n" +
                          $"Zone  {activationRatio * 100f:F0}% / {(1f - activationRatio) * 100f:F0}%";
            GUI.Label(new Rect(phoneX, phoneY + phoneH + 5, phoneW + 60, 36), info, styleInfo);

            GUI.color = oldColor;
        }

        // ── Draw helpers ──────────────────────────────────────────────────────

        private static Texture2D _solidTex;
        private static Texture2D GetSolidTex()
        {
            if (_solidTex != null) return _solidTex;
            _solidTex = new Texture2D(1, 1);
            _solidTex.SetPixel(0, 0, Color.white);
            _solidTex.Apply();
            return _solidTex;
        }

        private static void DrawFilledCircle(Vector2 center, float radius, Color col)
        {
            var old = GUI.color;
            GUI.color = col;
            for (int row = -(int)radius; row <= (int)radius; row++)
            {
                float halfW = Mathf.Sqrt(Mathf.Max(0f, radius * radius - row * row));
                GUI.DrawTexture(new Rect(center.x - halfW, center.y - row - 0.5f, halfW * 2f, 1f), GetSolidTex());
            }
            GUI.color = old;
        }

        private static void DrawCircleOutline(Vector2 center, float radius, Color col, float thickness = 2f, int segments = 60)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i       / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector2 p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                DrawGUILine(p0, p1, col, thickness);
            }
        }

        private static void DrawGUILine(Vector2 from, Vector2 to, Color col, float thickness = 2f)
        {
            var old = GUI.color;
            GUI.color = col;
            Vector2 dir = to - from;
            float   len = dir.magnitude;
            if (len < 0.001f) { GUI.color = old; return; }
            float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 mid   = (from + to) * 0.5f;
            var     mat   = GUI.matrix;
            GUIUtility.RotateAroundPivot(-angle, mid);
            GUI.DrawTexture(new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness), GetSolidTex());
            GUI.matrix = mat;
            GUI.color  = old;
        }

        // ── Accessor dùng Reflection để đọc field private của Runtime DLL ────

        private readonly struct JoystickAccessor
        {
            private const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            private static readonly System.Reflection.FieldInfo _fRadius           = typeof(TISVirtualJoystick).GetField("_radius",            Flags);
            private static readonly System.Reflection.FieldInfo _fKnobSizeRatio    = typeof(TISVirtualJoystick).GetField("_knobSizeRatioDebug", Flags);
            private static readonly System.Reflection.FieldInfo _fBgRect           = typeof(TISVirtualJoystick).GetField("_bgRect",             Flags);
            private static readonly System.Reflection.FieldInfo _fCanvasRect       = typeof(TISVirtualJoystick).GetField("_canvasRect",         Flags);
            private static readonly System.Reflection.FieldInfo _fReferenceWidth   = typeof(TISVirtualJoystick).GetField("_referenceWidth",     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            private static readonly System.Reflection.FieldInfo _fReferenceHeight  = typeof(TISVirtualJoystick).GetField("_referenceHeight",    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            private readonly TISVirtualJoystick _j;
            public JoystickAccessor(TISVirtualJoystick j) => _j = j;

            public float Radius            => (float)(_fRadius?.GetValue(_j) ?? 100f);
            public float KnobSizeRatioDebug => (float)(_fKnobSizeRatio?.GetValue(_j) ?? 0.5f);

            private RectTransform BgRect     => _fBgRect?.GetValue(_j) as RectTransform;
            private RectTransform CanvasRect => _fCanvasRect?.GetValue(_j) as RectTransform;

            private float ReferenceWidth  => (float)(_fReferenceWidth?.GetValue(null)  ?? 1920f);
            private float ReferenceHeight => (float)(_fReferenceHeight?.GetValue(null) ?? 1080f);

            public float CanvasWidth  => CanvasRect != null ? CanvasRect.rect.width  : ReferenceWidth;
            public float CanvasHeight => CanvasRect != null ? CanvasRect.rect.height : ReferenceHeight;

            public float AnchoredPositionX => BgRect != null ? BgRect.anchoredPosition.x : (-CanvasWidth  * 0.5f + Radius + 20f);
            public float AnchoredPositionY => BgRect != null ? BgRect.anchoredPosition.y : (-CanvasHeight * 0.5f + Radius + 20f);
        }
    }
}
#endif