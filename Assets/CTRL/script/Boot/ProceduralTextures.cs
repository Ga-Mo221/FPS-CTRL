// [Aemy 2026-10-26] FILE MOI.
using UnityEngine;

namespace Game.Boot
{
    /// <summary>
    /// Ve texture don gian luc chay (khong can file anh): vanh nhat thuc, dia toi, vignette.
    /// Texture trang + alpha; mau that lay tu Graphic.color (tint).
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>
        /// Vanh nhat thuc: vien sang mong o ban kinh radius, quang toa ra ngoai, 1 "hat kim cuong" sang o goc beadDeg.
        /// Cac tham so tinh theo ti le kich thuoc texture (0..0.5).
        /// </summary>
        public static Texture2D Corona(int size, float radius, float width, float glow, float beadDeg)
        {
            var px = new Color32[size * size];
            float inv = 1f / size;
            float bead = beadDeg * Mathf.Deg2Rad;
            var beadPos = new Vector2(Mathf.Cos(bead), Mathf.Sin(bead)) * radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) * inv - 0.5f, (y + 0.5f) * inv - 0.5f);
                float r = p.magnitude;
                float d = r - radius;
                float ring = Mathf.Exp(-(d * d) / (width * width));
                float halo = d > 0f ? Mathf.Exp(-d / glow) * 0.55f : Mathf.Exp(d / (glow * 0.25f)) * 0.2f;
                // Sang hon ve phia hat kim cuong (nhu nhat thuc that).
                float ang = Mathf.Atan2(p.y, p.x);
                float side = 0.6f + 0.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(ang - bead)), 3f);
                float b = (p - beadPos).sqrMagnitude;
                float beadGlow = Mathf.Exp(-b / (0.0009f)) + Mathf.Exp(-b / 0.006f) * 0.35f;
                float a = Mathf.Clamp01((ring + halo) * side + beadGlow);
                a *= 1f - Smooth(0.40f, 0.5f, r); // tat dan truoc mep texture -> khong lo khung vuong
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Make(size, px, "Corona");
        }

        /// <summary>[Aemy 2026-10-27] Vien tam giac canh bao (bo goc tron), thickness tinh theo ti le texture.</summary>
        public static Texture2D TriangleOutline(int size, float thickness)
        {
            var px = new Color32[size * size];
            Vector2 a = new Vector2(0.5f, 0.93f) * size, b = new Vector2(0.05f, 0.1f) * size, c = new Vector2(0.95f, 0.1f) * size;
            float half = thickness * size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
                float al = Mathf.Clamp01(half + 0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(al * 255f));
            }
            return Make(size, px, "Triangle");
        }

        private static float Seg(Vector2 p, Vector2 s, Vector2 e)
        {
            var d = e - s;
            float k = Mathf.Clamp01(Vector2.Dot(p - s, d) / d.sqrMagnitude);
            return (p - (s + d * k)).magnitude;
        }

        /// <summary>Dia tron (mat trang che mat troi), mep mem.</summary>
        public static Texture2D Disc(int size, float radius, float feather)
        {
            var px = new Color32[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + 0.5f) * inv - 0.5f, (y + 0.5f) * inv - 0.5f).magnitude;
                float a = 1f - Smooth(radius - feather, radius, r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Make(size, px, "Disc");
        }

        /// <summary>Vignette: giua trong suot, toi dan ra mep (inner -> outer, tinh theo nua duong cheo).</summary>
        public static Texture2D Vignette(int size, float inner, float outer, float power)
        {
            var px = new Color32[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2((x + 0.5f) * inv - 0.5f, (y + 0.5f) * inv - 0.5f).magnitude / 0.7071f;
                float a = Mathf.Pow(Mathf.Clamp01((r - inner) / Mathf.Max(0.001f, outer - inner)), power);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Make(size, px, "Vignette");
        }

        // smoothstep chuan (Mathf.SmoothStep cua Unity la noi suy gia tri, khong phai ham bien).
        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-5f, e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D Make(int size, Color32[] px, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural " + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
