// [Aemy 2026-10-22] FILE MOI. Danh sach nguoi choi cua phong, gan vao thuoc tinh session Fusion.
using System.Collections.Generic;
using System.Text;
using Fusion;

/// <summary>
/// Nguoi vao tran DANG DIEN RA chi thay SessionInfo (so nguoi, che do, map) -> man ghep tran chi hien moi minh.
/// Host ghi ten + doi cua nguoi dang trong phong vao thuoc tinh "r"; nguoi vao sau doc ra de hien roster.
/// SRP: chi ma hoa / giai ma + doc tu runner. Host goi Publish (NetworkGameState), menu goi Read (MenuShellBinder).
/// Dinh dang: moi nguoi = 1 ky tu doi (B / R / -) + ten (toi da 16 ky tu), cach nhau boi \u001E.
/// </summary>
public static class SessionRoster
{
    public const string PROP = "r";
    private const char Sep = '\u001E';

    public struct Entry
    {
        public string Name;
        public PlayerTeam Team;
    }

    /// <summary>Roster hien tai cua phong (chi host / state authority goi).</summary>
    public static string Build(NetworkRunner runner)
    {
        if (runner == null) return "";
        var sb = new StringBuilder();
        foreach (var pr in runner.ActivePlayers)
        {
            NetworkObject po;
            if (!runner.TryGetPlayerObject(pr, out po) || po == null) continue;
            var np = po.GetComponent<NPlayer>();
            if (np == null) continue;
            string name = np.Nickname.ToString();
            if (string.IsNullOrEmpty(name)) continue; // chua nhan ten (vua vao) -> lan sau
            if (sb.Length > 0) sb.Append(Sep);
            sb.Append(TeamChar(np.NetworkedTeam)).Append(name.Replace(Sep, ' '));
        }
        return sb.ToString();
    }

    /// <summary>Host: cap nhat thuoc tinh "r" neu roster doi. Giu nguyen cac thuoc tinh khac (che do / map / phien ban).</summary>
    public static void Publish(NetworkRunner runner, ref string lastPublished)
    {
        if (runner == null || !runner.IsServer || runner.SessionInfo == null || !runner.SessionInfo.IsValid) return;
        string roster = Build(runner);
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
        if (s == null || s.Properties == null) return list;
        SessionProperty p;
        if (!s.Properties.TryGetValue(PROP, out p) || !p.IsString) return list;
        string raw = (string)p;
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var part in raw.Split(Sep))
        {
            if (part.Length < 2) continue;
            list.Add(new Entry { Team = TeamOf(part[0]), Name = part.Substring(1) });
        }
        return list;
    }

    private static char TeamChar(PlayerTeam t) { return t == PlayerTeam.Blue ? 'B' : t == PlayerTeam.Red ? 'R' : '-'; }
    private static PlayerTeam TeamOf(char c) { return c == 'B' ? PlayerTeam.Blue : c == 'R' ? PlayerTeam.Red : PlayerTeam.Zero; }
}
