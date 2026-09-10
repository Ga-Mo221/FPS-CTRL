using UnityEditor;
using UnityEngine;
using TrueInputSystem.Readme;

namespace TrueInputSystem.Readme.Editor
{
    /// <summary>
    /// Custom inspector for TISReadme.
    /// Draws a bold "TIS" hero banner (big centered wordmark, dual-color glow,
    /// drop shadow, accent bars) followed by a styled list of sections with
    /// clickable links.
    /// </summary>
    [CustomEditor(typeof(TISReadme))]
    public class TISReadmeEditor : UnityEditor.Editor
    {
        // Brand palette — cyan / violet duotone.
        private static readonly Color kAccentCyan = new Color(0.30f, 0.90f, 0.98f);
        private static readonly Color kAccentViolet = new Color(0.62f, 0.40f, 1.00f);
        private static readonly Color kBannerBg = new Color(0.08f, 0.09f, 0.12f);

        private GUIStyle _titleStyle;
        private GUIStyle _titleShadowStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _linkStyle;
        private bool _stylesReady;

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 58,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = kAccentCyan }
            };

            _titleShadowStyle = new GUIStyle(_titleStyle)
            {
                normal = { textColor = new Color(0f, 0f, 0f, 0.45f) }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.90f) }
            };

            _headingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = kAccentViolet }
            };

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = 12,
                padding = new RectOffset(0, 4, 2, 6)
            };

            _linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                fontSize = 12
            };
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            var readme = (TISReadme)target;

            DrawHeroBanner(readme);
            GUILayout.Space(14);

            if (readme.sections != null)
            {
                foreach (var section in readme.sections)
                {
                    DrawSection(section);
                    GUILayout.Space(4);
                }
            }
        }

        private void DrawHeroBanner(TISReadme readme)
        {
            const float bannerHeight = 128f;
            Rect banner = GUILayoutUtility.GetRect(0, bannerHeight, GUILayout.ExpandWidth(true));

            // Base background.
            EditorGUI.DrawRect(banner, kBannerBg);

            // Two soft color "glows" on opposite corners to fake a gradient
            // without needing a generated texture asset.
            DrawGlow(new Rect(banner.x - banner.width * 0.15f, banner.y - 30, banner.width * 0.65f, banner.height + 60), kAccentCyan, 0.14f);
            DrawGlow(new Rect(banner.x + banner.width * 0.5f, banner.y - 30, banner.width * 0.65f, banner.height + 60), kAccentViolet, 0.14f);

            // Top accent line.
            EditorGUI.DrawRect(new Rect(banner.x, banner.y, banner.width, 3), kAccentCyan);

            // Big centered "TIS" wordmark with a subtle drop shadow.
            Rect titleRect = new Rect(banner.x, banner.y + 16, banner.width, 68);
            Rect shadowRect = new Rect(titleRect.x + 2, titleRect.y + 3, titleRect.width, titleRect.height);
            GUI.Label(shadowRect, "TIS", _titleShadowStyle);
            GUI.Label(titleRect, "TIS", _titleStyle);

            // Subtitle underneath.
            Rect subtitleRect = new Rect(banner.x, banner.y + 88, banner.width, 20);
            string subtitle = string.IsNullOrEmpty(readme.title) ? "TrueInputSystem" : readme.title;
            GUI.Label(subtitleRect, subtitle, _subtitleStyle);

            // Bottom accent line, opposite color for a duotone bracket effect.
            EditorGUI.DrawRect(new Rect(banner.x, banner.yMax - 3, banner.width, 3), kAccentViolet);
        }

        private static void DrawGlow(Rect rect, Color color, float alpha)
        {
            var prev = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        private void DrawSection(TISReadme.Section section)
        {
            EditorGUILayout.BeginHorizontal();

            // Left accent bar for a "card" feel.
            Rect barRect = GUILayoutUtility.GetRect(4, 0, GUILayout.Width(4), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(barRect, kAccentCyan);

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();

            if (!string.IsNullOrEmpty(section.heading))
                EditorGUILayout.LabelField(section.heading, _headingStyle);

            if (!string.IsNullOrEmpty(section.text))
                EditorGUILayout.LabelField(section.text, _bodyStyle);

            if (!string.IsNullOrEmpty(section.linkText) && !string.IsNullOrEmpty(section.url))
            {
                if (GUILayout.Button(section.linkText, _linkStyle, GUILayout.ExpandWidth(false)))
                {
                    Application.OpenURL(section.url);
                }
                Rect lastRect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(lastRect.x, lastRect.yMax - 1, lastRect.width, 1), kAccentViolet);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}