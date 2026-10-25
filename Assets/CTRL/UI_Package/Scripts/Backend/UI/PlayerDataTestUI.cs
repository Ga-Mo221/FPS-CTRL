using UnityEngine;
using Backend.Models;
using Backend.Bootstrap;

namespace Backend.UI
{
    /// <summary>
    /// Test Phase 3 (Player Data) bằng IMGUI — dùng ĐĂNG NHẬP THẬT (không guest).
    /// Gắn cạnh BackendBootstrap, bấm Play, đăng nhập rồi thao tác ở panel góc phải.
    /// </summary>
    public class PlayerDataTestUI : MonoBehaviour
    {
        // Login
        private string _loginUser = "";
        private string _loginPass = "";

        // Profile / loadout / stats
        private string _displayName = "NyxPlayer";
        private string _avatarUrl = "https://picsum.photos/128";
        private string _level = "42";
        private string _weapon1 = "Umbra", _weapon2 = "Vesper";
        private string _util1 = "Flash", _util2 = "Smoke";

        private string _status = "Hay dang nhap de bat dau.";
        private Vector2 _scroll;

        private bool LoggedIn => BackendBootstrap.AuthService.IsLoggedIn;

        // Yêu cầu đã đăng nhập; nếu chưa thì báo (KHÔNG tự login khách nữa).
        private bool Require()
        {
            if (LoggedIn) return true;
            _status = "Chua dang nhap! Nhap user/email + password roi bam DANG NHAP.";
            return false;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 390, 10, 380, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("<b>PHASE 3 — PLAYER DATA TEST</b>");
            GUILayout.Label(LoggedIn
                ? "<color=#4ade80>Da dang nhap: " + BackendBootstrap.AuthService.CurrentUserId + "</color>"
                : "<color=#fbbf24>Chua dang nhap</color>");

            _scroll = GUILayout.BeginScrollView(_scroll);

            // ---------- LOGIN ----------
            GUILayout.Label("— Dang nhap —");
            _loginUser = Field("User/Email", _loginUser);
            _loginPass = FieldPass("Password", _loginPass);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("DANG NHAP"))
            {
                var input = _loginUser.Trim();
                var req = new LoginRequest
                {
                    UsernameorEmail = input,
                    Password = _loginPass,
                    UseEmail = input.Contains("@")
                };
                _status = "Dang dang nhap...";
                BackendBootstrap.AuthService.Login(req,
                    r => _status = "OK: dang nhap thanh cong\nPlayerId: " + r.PlayerId,
                    e => Err(e));
            }
            if (GUILayout.Button("Logout"))
            {
                BackendBootstrap.AuthService.Logout();
                _status = "Da logout.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("— Profile —");
            _displayName = Field("DisplayName", _displayName);
            if (GUILayout.Button("Set DisplayName") && Require())
                BackendBootstrap.PlayerProfileService.SetDisplayName(_displayName,
                    () => _status = "OK: da doi displayName", e => Err(e));

            _avatarUrl = Field("AvatarUrl", _avatarUrl);
            if (GUILayout.Button("Set AvatarUrl") && Require())
                BackendBootstrap.PlayerProfileService.SetAvatarUrl(_avatarUrl,
                    () => _status = "OK: da doi avatar", e => Err(e));

            _level = Field("Level", _level);
            if (GUILayout.Button("Set Level") && Require())
                BackendBootstrap.PlayerProfileService.SetLevel(ParseInt(_level),
                    () => _status = "OK: da set level", e => Err(e));

            if (GUILayout.Button("Get My Profile") && Require())
                BackendBootstrap.PlayerProfileService.GetMyProfile(
                    p => _status = $"Profile:\n name={p.DisplayName}\n avatar={p.AvatarUrl}\n level={p.Level}",
                    e => Err(e));

            GUILayout.Space(6);
            GUILayout.Label("— Loadout —");
            _weapon1 = Field("Sung 1", _weapon1);
            _weapon2 = Field("Sung 2", _weapon2);
            _util1 = Field("Utility 1", _util1);
            _util2 = Field("Utility 2", _util2);
            if (GUILayout.Button("Save Loadout") && Require())
            {
                var cfg = new LoadoutConfig
                {
                    Weapons = new[] { _weapon1, _weapon2 },
                    Utilities = new[] { _util1, _util2 }
                };
                BackendBootstrap.LoadoutService.SaveLoadout(cfg,
                    () => _status = "OK: da luu loadout", e => Err(e));
            }
            if (GUILayout.Button("Load Loadout") && Require())
                BackendBootstrap.LoadoutService.LoadLoadout(
                    c => _status = $"Loadout:\n sung: {Join(c.Weapons)}\n utility: {Join(c.Utilities)}",
                    e => Err(e));

            GUILayout.Space(6);
            GUILayout.Label("— Stats / Leaderboard —");
            if (GUILayout.Button("Report Match (5k/2d/3hs/1a/+250xp)") && Require())
                BackendBootstrap.PlayerStatsService.ReportMatchResult(5, 2, 3, 1, 250,
                    total => _status = $"OK: cong don 1 tran (Lv {total.Level}, XP {total.Experience})", e => Err(e));
            if (GUILayout.Button("Get My Stats") && Require())
                BackendBootstrap.PlayerStatsService.GetMyStats(
                    s => _status = $"Stats:\n K/D={s.Kills}/{s.Deaths}\n Assist={s.Assist}\n KDA={s.KDA:0.00}\n HS%={s.HeadshotPercent}\n EXP={s.Experience} (Lv {s.Level})\n Matches={s.Matches}",
                    e => Err(e));
            if (GUILayout.Button("Leaderboard KDA") && Require())
                BackendBootstrap.LeaderboardService.GetLeaderboard(StatKeys.KDAx100, 10,
                    list =>
                    {
                        var sb = new System.Text.StringBuilder("Leaderboard KDA:\n");
                        foreach (var e in list) sb.AppendLine($" #{e.Position + 1} {e.DisplayName} = {e.StatValue}");
                        _status = sb.ToString();
                    },
                    e => Err(e));

            GUILayout.EndScrollView();
            GUILayout.Space(6);
            GUILayout.Label("<b>Ket qua:</b>\n" + _status);
            GUILayout.EndArea();
        }

        private static string Field(string label, string val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90));
            val = GUILayout.TextField(val);
            GUILayout.EndHorizontal();
            return val;
        }

        private static string FieldPass(string label, string val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90));
            val = GUILayout.PasswordField(val, '*');
            GUILayout.EndHorizontal();
            return val;
        }

        private void Err(AuthError e) { _status = $"LOI [{e.ErrorCode}] {e.ErrorMessage}"; }
        private static int ParseInt(string s) { int v; return int.TryParse(s, out v) ? v : 0; }
        private static string Join(string[] a) { return a == null ? "" : string.Join(", ", a); }
    }
}
