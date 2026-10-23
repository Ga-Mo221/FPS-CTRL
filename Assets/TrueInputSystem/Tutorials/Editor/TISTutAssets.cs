// ============================================================
//  TrueInputSystem Tutorials — TISTutAssets.cs (Editor)
//
//  Tạo asset dùng chung cho 3 scene hướng dẫn:
//    • TMP Font Asset (dynamic, nạp sẵn ký tự tiếng Việt) từ TTF trong Fonts/
//    • TIS_Tut_Theme.asset (màu + font)
//    • Vật liệu màu cho đồ vật sân chơi (theo shader mặc định của render pipeline)
// ============================================================

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

namespace GaMo.TrueInputSystem.Tutorials.Editor
{
    public static class TISTutAssets
    {
        public const string Root       = "Assets/TrueInputSystem/Tutorials/";
        public const string FontsDir   = Root + "Fonts/";
        public const string MatDir     = Root + "Materials/";
        public const string ThemePath  = Root + "TIS_Tut_Theme.asset";

        const string ViChars =
            "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ" +
            "ÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ";
        const string Symbols = "→←↑↓✓✕×●○◉▶■□◆⏎⇧⌫·…–—•";

        static string Ascii()
        {
            var sb = new System.Text.StringBuilder();
            for (char c = (char)32; c < 127; c++) sb.Append(c);
            return sb.ToString();
        }
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Tạo Font + Theme + Vật liệu", priority = 60)]
#endif
        public static TISTutTheme EnsureAll()
        {
            var theme = EnsureTheme();
            EnsureMaterials(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        // ── Font ──────────────────────────────────────────────

        public static TMP_FontAsset EnsureFont(string ttfPath, string assetName, bool mono)
        {
            string outPath = FontsDir + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError($"[TISTut] Không tìm thấy font {ttfPath}"); return null; }

            var fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                                                   AtlasPopulationMode.Dynamic, true);
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, outPath);

            fa.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            // Nạp sẵn ASCII + tiếng Việt (+ ký hiệu với font mono) để chữ không "nhảy" lần đầu hiện
            fa.TryAddCharacters(Ascii() + ViChars + (mono ? Symbols : ""), out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        public static TISTutTheme EnsureTheme()
        {
            var monoMed  = EnsureFont(FontsDir + "JetBrainsMono/JetBrainsMono-Medium.ttf", "TIS_Tut_JetBrainsMono_Medium SDF", true);
            var monoBold = EnsureFont(FontsDir + "JetBrainsMono/JetBrainsMono-Bold.ttf",   "TIS_Tut_JetBrainsMono_Bold SDF",   true);
            var body     = EnsureFont(FontsDir + "BeVietnamPro/BeVietnamPro-Regular.ttf",  "TIS_Tut_BeVietnamPro_Regular SDF", false);
            var bodyBold = EnsureFont(FontsDir + "BeVietnamPro/BeVietnamPro-Bold.ttf",     "TIS_Tut_BeVietnamPro_Bold SDF",    false);

            // Be Vietnam Pro thiếu mũi tên / ✓ / ● → mượn từ JetBrains Mono
            foreach (var f in new[] { body, bodyBold })
            {
                if (f == null || monoMed == null) continue;
                f.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                if (!f.fallbackFontAssetTable.Contains(monoMed))
                {
                    f.fallbackFontAssetTable.Add(monoMed);
                    EditorUtility.SetDirty(f);
                }
            }

            var theme = AssetDatabase.LoadAssetAtPath<TISTutTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<TISTutTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.body     = body;
            theme.bodyBold = bodyBold;
            theme.mono     = monoMed;
            theme.monoBold = monoBold;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        // ── Sprite tròn (joystick, nút cảm ứng) ───────────────

        public const string SpriteDir = Root + "Sprites/";

        public static Sprite Circle => EnsureSprite("TIS_Tut_Circle", 0f);
        public static Sprite Ring   => EnsureSprite("TIS_Tut_Ring", 0.14f);

        /// <summary>Sinh PNG tròn khử răng cưa (ring > 0 = vòng rỗng, độ dày theo bán kính).</summary>
        static Sprite EnsureSprite(string name, float ring)
        {
            string path = SpriteDir + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!Directory.Exists(SpriteDir)) Directory.CreateDirectory(SpriteDir);
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f - 2f, inner = r * (1f - ring);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ring > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── Vật liệu ──────────────────────────────────────────

        public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "TIS_Tut_" + name + ".mat");

        public static void EnsureMaterials(TISTutTheme th)
        {
            if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);
            MakeMat("Grass", th.toyGrass, 0.15f);
            MakeMat("Coral", th.toyCoral, 0.35f);
            MakeMat("Sun",   th.toySun,   0.35f);
            MakeMat("Lilac", th.toyLilac, 0.35f);
            MakeMat("Mint",  th.toyMint,  0.35f);
            MakeMat("Ink",   th.ink,      0.3f);
            MakeMat("Paper", th.paper,    0.2f);
        }

        static void MakeMat(string name, Color c, float smooth)
        {
            string path = MatDir + "TIS_Tut_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(DefaultLitShader());
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            EditorUtility.SetDirty(m);
        }

        static Shader DefaultLitShader()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) return rp.defaultMaterial.shader;
            return Shader.Find("Standard");
        }
    }
}
