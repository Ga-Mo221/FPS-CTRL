// GaMoInspector.DrawHelpers.cs  -  part of CustomsEditor (GaMo Inspector)
// Ham ve nen bo tron, texture dung chung
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        // ---------- Vẽ nút nền đặc ----------
        // GUI.backgroundColor chỉ NHÂN màu lên texture của skin Unity nên có skin gần như không đổi màu.
        // Vẽ thẳng một khối màu đặc (kèm viền) để màu luôn đúng bất kể skin.
        const float CornerRadius = 3f; // bo góc nhẹ cho nút nền đặc (tab component, Lock): khớp độ bo của nút skin Unity

        static void DrawSolid(Rect r, Color c, bool hover, float radius = CornerRadius)
        {
            if (Event.current.type != EventType.Repaint) return;
            float a = GUI.enabled ? 1f : 0.4f;
            var fill = hover && GUI.enabled ? Color.Lerp(c, Color.white, 0.15f) : c;
            fill.a = a;
            var border = new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, a);
            DrawRounded(r, border, radius);
            DrawRounded(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), fill, Mathf.Max(0f, radius - 1f));
        }

        // Texture trắng bo góc (9-slice) tự tạo + GUIStyle thường. KHÔNG dùng GUI.DrawTexture(borderRadiuses): đường vẽ đó đi qua
        // material riêng của Unity, và nội dung (chữ/icon) vẽ ngay sau nó có thể biến mất (xem nút Locked).
        const int RoundTexSize = 14, RoundTexRadius = 4, RoundTexBorder = 6;
        static Texture2D _roundTex;
        static GUIStyle _roundStyle;

        static void DestroyRoundTex()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyRoundTex;
            if (_roundTex != null) UObject.DestroyImmediate(_roundTex); // HideAndDontSave KHÔNG tự bị hủy khi reload domain => mỗi lần recompile sót 1 texture
            _roundTex = null;
            _roundStyle = null;
        }

        static Texture2D RoundTex()
        {
            if (_roundTex != null) return _roundTex;
            var tex = new Texture2D(RoundTexSize, RoundTexSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            const int ss = 4; // lấy mẫu 4x4 mỗi pixel để mép bo mượt
            for (int y = 0; y < RoundTexSize; y++)
                for (int x = 0; x < RoundTexSize; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float px = x + (sx + 0.5f) / ss, py = y + (sy + 0.5f) / ss;
                            float cx = Mathf.Clamp(px, RoundTexRadius, RoundTexSize - RoundTexRadius);
                            float cy = Mathf.Clamp(py, RoundTexRadius, RoundTexSize - RoundTexRadius);
                            float dx = px - cx, dy = py - cy;
                            if (dx * dx + dy * dy <= RoundTexRadius * RoundTexRadius) hit++;
                        }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / (float)(ss * ss)));
                }
            tex.Apply(false, false);
            _roundStyle = null;
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyRoundTex;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyRoundTex;
            return _roundTex = tex;
        }

        // Gán texture bo góc làm nền cho MỌI trạng thái của style (để hover / on / active đều giữ nền cam)
        static void ApplyRoundBackground(GUIStyle st)
        {
            var t = RoundTex();
            st.normal.background = st.hover.background = st.active.background = st.focused.background = t;
            st.onNormal.background = st.onHover.background = st.onActive.background = st.onFocused.background = t;
            st.border = new RectOffset(RoundTexBorder, RoundTexBorder, RoundTexBorder, RoundTexBorder);
        }

        static void DrawRounded(Rect r, Color c, float radius)
        {
            if (r.width < 1f || r.height < 1f) return;
            if (_roundStyle == null || _roundTex == null)
            {
                RoundTex();
                _roundStyle = new GUIStyle();
                ApplyRoundBackground(_roundStyle);
            }
            var old = GUI.color;
            GUI.color = c;
            _roundStyle.Draw(r, false, false, false, false);
            GUI.color = old;
        }

        // Danh sách object hợp lệ, object chính đứng đầu.
        GameObject[] ValidGos()
        {
            if (_validCache != null) return _validCache; // xóa ở Layout / Bind
            var list = new List<GameObject>(_gos.Count + 1);
            if (_go != null) list.Add(_go);
            foreach (var g in _gos)
                if (g != null && g != _go) list.Add(g);
            return _validCache = list.ToArray();
        }

        // Danh sách flag Static: bỏ flag obsolete (Unity 6 còn giữ NavigationStatic, OffMeshLinkGeneration, LightmapStatic...),
        // bỏ Nothing/Everything và flag ghép.
    }
}
#endif
