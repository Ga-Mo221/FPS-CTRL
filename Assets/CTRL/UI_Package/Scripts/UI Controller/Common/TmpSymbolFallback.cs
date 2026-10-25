// [Aemy 2026-10-24] FILE MOI. Ky hieu ✓ cho chu TMP khi font khong co glyph (hien o vuong trong).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Game.UI
{
    /// <summary>
    /// ChakraPetch (va cac font fallback) khong co ky tu ✓ (U+2713) -> "✓ ĐÃ MUA", "✓ ĐANG DÙNG", "ĐÃ HIỆU CHỈNH ✓"
    /// hien o vuong trong. Khong sua font asset: tu ve dau tick vao 1 TMP_SpriteAsset luc chay, gan lam sprite asset
    /// cua chu nao co ✓. TMP tim ky tu thieu trong sprite asset cua chu theo unicode -> ✓ tu hien thanh dau tick,
    /// cung mau chu (tintAllSprites), cao bang chu hoa. Text / LocTable giu nguyen.
    /// SRP: chi lo ky hieu thieu glyph; khong biet gi ve Shop / Settings.
    /// </summary>
    public static class TmpSymbolFallback
    {
        private const int Size = 64;
        private static readonly uint[] CheckUnicodes = { 0x2713, 0x2714 }; // ✓ ✔
        private static TMP_SpriteAsset _asset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _asset = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        // Moi lan 1 chu TMP dung lai mesh. Chi gan 1 lan (spriteAsset dang trong) -> khong lap vo han.
        private static void OnTextChanged(Object obj)
        {
            var t = obj as TMP_Text;
            if (t == null || t.spriteAsset != null) return;
            string s = t.text;
            if (string.IsNullOrEmpty(s) || !HasCheck(s)) return;
            if (FontHas(t.font, 0x2713)) return; // font co san ✓ -> de nguyen
            var asset = Asset();
            if (asset == null) return;
            t.spriteAsset = asset;
            t.tintAllSprites = true;
        }

        private static bool HasCheck(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] == '✓' || s[i] == '✔') return true;
            return false;
        }

        private static bool FontHas(TMP_FontAsset font, uint unicode)
        {
            if (font == null) return false;
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(unicode)) return true;
            var fb = font.fallbackFontAssetTable;
            if (fb != null)
                foreach (var f in fb)
                    if (f != null && f.characterLookupTable != null && f.characterLookupTable.ContainsKey(unicode)) return true;
            return false;
        }

        private static TMP_SpriteAsset Asset()
        {
            if (_asset != null) return _asset;
            var tex = DrawCheck();
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "Runtime Symbols";
            asset.hideFlags = HideFlags.DontSave;
            MarkCurrentVersion(asset); // asset tao tay chua co version -> TMP se "nang cap" va xoa bang glyph
            asset.spriteSheet = tex;

            var def = TMP_Settings.defaultSpriteAsset;
            Material mat = null;
            if (def != null && def.material != null) mat = new Material(def.material);
            else { var sh = Shader.Find("TextMeshPro/Sprite"); if (sh != null) mat = new Material(sh); }
            if (mat == null) return null;
            mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
            mat.hideFlags = HideFlags.DontSave;
            asset.material = mat;

            // Khung glyph = 1 o vuong; faceInfo.pointSize = 0 -> TMP co theo chieu cao chu hoa cua font dang dung.
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(Size, Size, 0f, Size * 0.9f, Size * 1.05f),
                new GlyphRect(0, 0, Size, Size), 1f, 0);
            asset.spriteGlyphTable.Add(glyph);
            foreach (var u in CheckUnicodes)
            {
                var ch = new TMP_SpriteCharacter(u, glyph) { name = u == 0x2713 ? "check" : "check_heavy" };
                asset.spriteCharacterTable.Add(ch);
            }
            asset.UpdateLookupTables();
            _asset = asset;
            return _asset;
        }

        private static void MarkCurrentVersion(TMP_SpriteAsset asset)
        {
            for (var ty = asset.GetType(); ty != null; ty = ty.BaseType)
            {
                var f = ty.GetField("m_Version", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (f != null) { f.SetValue(asset, "1.1.0"); return; }
            }
        }

        // Dau tick trang, khu rang cua; mau that lay tu mau chu (tint). Nam sat chan chu, dinh ngang chu hoa.
        private static Texture2D DrawCheck()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Runtime Check", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            Vector2 a = new Vector2(9f, 28f), b = new Vector2(24f, 12f), c = new Vector2(55f, 47f);
            const float half = 4.6f; // nua do day net
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c));
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half + 0.5f - d) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static float DistToSegment(Vector2 p, Vector2 s, Vector2 e)
        {
            var d = e - s;
            float k = Mathf.Clamp01(Vector2.Dot(p - s, d) / d.sqrMagnitude);
            return (p - (s + d * k)).magnitude;
        }
    }
}
