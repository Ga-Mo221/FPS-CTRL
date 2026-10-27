// [Aemy 2026-10-22] FILE MOI. Danh sach nguoi choi cua phong, gan vao thuoc tinh session Fusion.
using System.Collections.Generic;
using System.Text;
using Fusion;

/// <summary>
/// Nguoi vao tran DANG DIEN RA chi thay SessionInfo (so nguoi, che do, map) -> man ghep tran chi hien moi minh.
/// Host ghi ten + doi cua nguoi dang trong phong vao thuoc tinh "r"; nguoi vao sau doc ra de hien roster.
/// SRP: chi ma hoa / giai ma + doc tu runner. Host goi Publish (NetworkGameState), menu goi Read (MenuShellBinder).
/// Dinh dang: moi nguoi = 1 ky tu doi (B / R / -) + ten (toi da 16 ky tu), cach nhau boi \u001E.
/// [Aemy 2026-10-24] + avatar sau \u001F (URL robohash rut gon "~...") de man ghep tran cua nguoi vao sau co avatar,
/// + phan tu dau "L" (chua bat dau tran: phong cho / dem nguoc) hoac "G" (dang danh).
/// </summary>
public static class SessionRoster
{
    public const string PROP = "r";
    private const char Sep = '\u001E';
    private const char AvatarSep = '\u001F';
    private const string RobohashPrefix = "https://robohash.org/";
    private const int MaxAvatarChars = 160; // URL dai hon -> bo (giu thuoc tinh phong nho)

    public struct Entry
    {
        public string Name;
        public PlayerTeam Team;
        public string AvatarUrl;
    }

    /// <summary>Roster hien tai cua phong (chi host / state authority goi).</summary>
    public static string Build(NetworkRunner runner, bool preGame)
    {
        if (runner == null) return "";
        var sb = new StringBuilder();
        sb.Append(preGame ? 'L' : 'G');
        foreach (var pr in runner.ActivePlayers)
        {
            NetworkObject po;
            if (!runner.TryGetPlayerObject(pr, out po) || po == null) continue;
            var np = po.GetComponent<NPlayer>();
            if (np == null) continue;
            string name = np.Nickname.ToString();
            if (string.IsNullOrEmpty(name)) continue; // chua nhan ten (vua vao) -> lan sau
            sb.Append(Sep);
            sb.Append(TeamChar(np.NetworkedTeam)).Append(name.Replace(Sep, ' ').Replace(AvatarSep, ' '));
            string avatar = PackAvatar(np.AvatarUrl.ToString());
            if (avatar.Length > 0) sb.Append(AvatarSep).Append(avatar);
        }
        return sb.ToString();
    }

    /// <summary>Host: cap nhat thuoc tinh "r" neu roster doi. Giu nguyen cac thuoc tinh khac (che do / map / phien ban).</summary>
    public static void Publish(NetworkRunner runner, ref string lastPublished, bool preGame)
    {
        if (runner == null || !runner.IsServer || runner.SessionInfo == null || !runner.SessionInfo.IsValid) return;
        string roster = Build(runner, preGame);
        if (roster == lastPublished) return;
        lastPublished = roster;
        var props = new Dictionary<string, SessionProperty>();
        if (runner.SessionInfo.Properties != null)
            foreach (var kv in runner.SessionInfo.Properties) props[kv.Key] = kv.Value;
        props[PROP] = roster;
        runner.SessionInfo.UpdateCustomProperties(props);
    }

    /// <summary>Doc roster tu phong tim thay trong lobby. Rong neu host ban cu chua ghi.</summary>
    public static List<Entry> Read(SessionInfo s)
    {
        var list = new List<Entry>();
        string raw = Raw(s);
        if (string.IsNullOrEmpty(raw)) return list;
        var parts = raw.Split(Sep);
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (i == 0 && part.Length == 1) continue; // co L / G
            if (part.Length < 2) continue;
            string body = part.Substring(1), avatar = "";
            int a = body.IndexOf(AvatarSep);
            if (a >= 0) { avatar = UnpackAvatar(body.Substring(a + 1)); body = body.Substring(0, a); }
            list.Add(new Entry { Team = TeamOf(part[0]), Name = body, AvatarUrl = avatar });
        }
        return list;
    }

    /// <summary>Phong chua bat dau tran (phong cho / dem nguoc) -> nguoi vao se thay phong cho trong tran, khong can xem truoc o menu.</summary>
    public static bool IsPreGame(SessionInfo s)
    {
        string raw = Raw(s);
        return !string.IsNullOrEmpty(raw) && raw[0] == 'L' && (raw.Length == 1 || raw[1] == Sep);
    }

    private static string Raw(SessionInfo s)
    {
        if (s == null || s.Properties == null) return null;
        SessionProperty p;
        if (!s.Properties.TryGetValue(PROP, out p) || !p.IsString) return null;
        return (string)p;
    }

    private static string PackAvatar(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (url.StartsWith(RobohashPrefix)) url = "~" + url.Substring(RobohashPrefix.Length);
        if (url.Length > MaxAvatarChars || url.IndexOf(Sep) >= 0 || url.IndexOf(AvatarSep) >= 0) return "";
        return url;
    }

    private static string UnpackAvatar(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return "";
        return packed[0] == '~' ? RobohashPrefix + packed.Substring(1) : packed;
    }

    private static char TeamChar(PlayerTeam t) { return t == PlayerTeam.Blue ? 'B' : t == PlayerTeam.Red ? 'R' : '-'; }
    private static PlayerTeam TeamOf(char c) { return c == 'B' ? PlayerTeam.Blue : c == 'R' ? PlayerTeam.Red : PlayerTeam.Zero; }
}
