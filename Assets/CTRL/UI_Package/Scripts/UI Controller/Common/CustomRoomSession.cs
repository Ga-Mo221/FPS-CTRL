// [Aemy 2026-10-17] FILE MOI. Trang thai "phong tuy chinh" (phong rieng co ma) dung chung menu <-> tran.
using UnityEngine;
using TMPro;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// - Ma phong: 6 ky tu, ky tu dau = luat choi (T = Team Deathmatch, F = Free-for-all) -> nguoi vao bang ma
    ///   biet luat ma khong can hoi chu phong. Ten session Fusion = "custom_" + ma (an khoi danh sach phong).
    /// - Moc ha guc: chu phong dat vao GameManager truoc khi tao phong; ve menu thi tra lai mac dinh.
    /// </summary>
    public static class CustomRoomSession
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bo O/0/I/1 de khong nham
        public const string SessionPrefix = "custom_";
        public const string PartyExtra = "custom";

        public static string Code { get; private set; }
        private static int _defaultKill = -1;

        public static string NewCode(MatchMode rule)
        {
            var sb = new System.Text.StringBuilder(rule == MatchMode.TDM ? "T" : "F");
            var rng = new System.Random();
            for (int i = 0; i < 5; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>Chuan hoa ma nguoi choi go (bo khoang trang, viet hoa). null neu sai dinh dang.</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var s = raw.Trim().Replace(" ", "").ToUpperInvariant();
            if (s.StartsWith(SessionPrefix.ToUpperInvariant())) s = s.Substring(SessionPrefix.Length);
            if (s.Length != 6) return null;
            if (s[0] != 'T' && s[0] != 'F') return null;
            foreach (var c in s) if (Alphabet.IndexOf(c) < 0) return null;
            return s;
        }

        public static MatchMode RuleOf(string code) { return !string.IsNullOrEmpty(code) && code[0] == 'T' ? MatchMode.TDM : MatchMode.FFA; }
        public static string SessionOf(string code) { return SessionPrefix + code; }
        public static bool IsCustomSession(string session) { return !string.IsNullOrEmpty(session) && session.StartsWith(SessionPrefix); }
        public static string CodeOfSession(string session) { return IsCustomSession(session) ? session.Substring(SessionPrefix.Length) : null; }

        public static void Begin(string code, int killTarget, bool asHost) { Begin(code, killTarget, 0, asHost); }

        /// <summary>[Aemy 2026-10-19] + thoi gian tran (phut, 0 = mac dinh). Chi chu phong (host) dat vao GameManager.</summary>
        public static void Begin(string code, int killTarget, int matchMinutes, bool asHost)
        {
            Code = code;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (_defaultKill < 0) _defaultKill = gm.KillTarget;
            if (asHost && killTarget > 0) gm.SetKillTarget(killTarget);
            gm.SetRoundMinutes(asHost ? matchMinutes : 0);
        }

        /// <summary>Extra cua to doi: "custom:&lt;kill&gt;:&lt;phut&gt;" (ban cu chi co kill).</summary>
        public static string MakePartyExtra(int killTarget, int matchMinutes) { return PartyExtra + ":" + killTarget + ":" + matchMinutes; }
        public static void ParsePartyExtra(string extra, out int killTarget, out int matchMinutes)
        {
            killTarget = 0; matchMinutes = 0;
            if (string.IsNullOrEmpty(extra)) return;
            var parts = extra.Split(':');
            if (parts.Length > 1) int.TryParse(parts[1], out killTarget);
            if (parts.Length > 2) int.TryParse(parts[2], out matchMinutes);
        }

        /// <summary>Ve menu / vao tran thuong: xoa ma + tra moc ha guc mac dinh.</summary>
        public static void Clear()
        {
            Code = null;
            var gm = GameManager.Instance;
            if (gm != null && _defaultKill >= 0) gm.SetKillTarget(_defaultKill);
            if (gm != null) gm.SetRoundMinutes(0); // [Aemy 2026-10-19] tran thuong: thoi gian mac dinh
        }

        /// <summary>HUD trong tran: nhan nho "PHONG: T7KQ2D" goc tren trai (chi khi dang o phong tuy chinh).</summary>
        public static void AttachHudLabel(Transform hudRoot)
        {
            if (string.IsNullOrEmpty(Code) || hudRoot == null) return;
            var canvas = hudRoot.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform : hudRoot;
            if (parent.Find("CustomRoomCode") != null) return;
            var go = new GameObject("CustomRoomCode", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -18f); rt.sizeDelta = new Vector2(420f, 32f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = "PHÒNG TÙY CHỈNH · MÃ <b>" + Code + "</b>";
            t.fontSize = 18; t.color = new Color(0.31f, 0.84f, 0.79f, 0.9f); t.raycastTarget = false; t.enableWordWrapping = false;
            var theme = PopupTheme.Default; if (theme != null && theme.bodyFont != null) t.font = theme.bodyFont;
        }
    }
}
