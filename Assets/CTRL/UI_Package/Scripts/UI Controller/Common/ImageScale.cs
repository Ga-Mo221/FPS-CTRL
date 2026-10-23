using UnityEngine;

namespace Game.UI
{
    /// <summary>[#5 2026-09-29 Aemy] Thu nho texture tai ve xuong <= maxSize (giu ti le) -> nhe VRAM.
    /// Dung GPU blit nen chay duoc ca voi texture khong readable.</summary>
    public static class ImageScale
    {
        public static Texture2D Fit(Texture2D src, int maxSize)
        {
            if (src == null || maxSize <= 0) return src;
            int w = src.width, h = src.height;
            if (w <= maxSize && h <= maxSize) return src;
            float scale = Mathf.Min((float)maxSize / w, (float)maxSize / h);
            int nw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int nh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            dst.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }
    }
}
