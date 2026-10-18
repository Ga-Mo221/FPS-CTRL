// ============================================================
//  TrueInputSystem Tutorials — TISTutTheme.cs
//
//  Bảng màu + font dùng chung cho 3 scene hướng dẫn.
//  Nền kem · than chì · mỗi mode một màu nhấn:
//    Chuột + Bàn phím → xanh chanh · Tay cầm → xanh trời · Mobile → cam đào
//
//  Component UI đọc màu qua TISTutTheme.Current (không cần kéo reference).
//  TISTutController gán theme + mode của scene khi Awake.
// ============================================================

using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    public enum TISTutMode
    {
        KeyboardMouse = 0,
        Gamepad       = 1,
        Mobile        = 2,
    }

    [CreateAssetMenu(fileName = "TIS_Tut_Theme", menuName = "True Input System/Tutorials/Theme")]
    public class TISTutTheme : ScriptableObject
    {
        [Header("Nền & chữ")]
        public Color paper     = Hex("#F6F5EF");
        public Color paperDeep = Hex("#E9E8E1");
        public Color card      = Color.white;
        public Color ink       = Hex("#263133");
        public Color inkSoft   = Hex("#6B726F");
        public Color inkMuted  = Hex("#9A9F9C");
        public Color line      = Hex("#D6D5CD");
        public Color track     = Hex("#EEEDE6");
        public Color success   = Hex("#4E8A1A");
        public Color codeText  = Hex("#E9F3DF");

        [Header("Màu nhấn — Chuột + Bàn phím")]
        public Color kbmAccent       = Hex("#D7F59B");
        public Color kbmAccentStrong = Hex("#8FCC3A");

        [Header("Màu nhấn — Tay cầm")]
        public Color gamepadAccent       = Hex("#BFE1FF");
        public Color gamepadAccentStrong = Hex("#4E9BE6");

        [Header("Màu nhấn — Mobile")]
        public Color mobileAccent       = Hex("#FFD3B8");
        public Color mobileAccentStrong = Hex("#FF8A5B");

        [Header("Màu đồ vật trong sân chơi")]
        public Color toySky    = Hex("#BFE3F2");
        public Color toyGrass  = Hex("#A5D67E");
        public Color toyCoral  = Hex("#FF8A5B");
        public Color toySun    = Hex("#FFD45C");
        public Color toyLilac  = Hex("#B79CFF");
        public Color toyMint   = Hex("#7FDCC0");

        [Header("Font")]
        public TMP_FontAsset body;
        public TMP_FontAsset bodyBold;
        public TMP_FontAsset mono;
        public TMP_FontAsset monoBold;

        // ── Truy cập toàn cục ─────────────────────────────────

        static TISTutTheme _current;
        static TISTutTheme _fallback;

        /// <summary>Theme đang dùng. Chưa gán → theme mặc định (màu chuẩn, không font).</summary>
        public static TISTutTheme Current
        {
            get
            {
                if (_current != null) return _current;
                if (_fallback == null)
                {
                    _fallback = CreateInstance<TISTutTheme>();
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallback;
            }
            set => _current = value;
        }

        /// <summary>Mode của scene đang mở — quyết định màu nhấn.</summary>
        public static TISTutMode Mode { get; set; } = TISTutMode.KeyboardMouse;

        public Color Accent(TISTutMode mode) => mode switch
        {
            TISTutMode.Gamepad => gamepadAccent,
            TISTutMode.Mobile  => mobileAccent,
            _                  => kbmAccent,
        };

        public Color AccentStrong(TISTutMode mode) => mode switch
        {
            TISTutMode.Gamepad => gamepadAccentStrong,
            TISTutMode.Mobile  => mobileAccentStrong,
            _                  => kbmAccentStrong,
        };

        public Color Accent()       => Accent(Mode);
        public Color AccentStrong() => AccentStrong(Mode);

        /// <summary>"#RRGGBB" / "#RRGGBBAA" → Color. Thuần C# (gọi được trong field initializer).</summary>
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);
            uint v = System.Convert.ToUInt32(hex, 16);
            if (hex.Length == 6) v = (v << 8) | 0xFF;
            return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }
    }
}
