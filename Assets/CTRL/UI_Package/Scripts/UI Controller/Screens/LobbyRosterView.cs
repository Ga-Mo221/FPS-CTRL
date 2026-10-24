// [Aemy 2026-10-24] FILE MOI. Do danh sach nguoi choi vao man ghep tran (MatchmakingController).
// Tach tu MenuShellBinder.FillLobbyRoster de dung chung cho menu VA phong cho trong tran (MatchLobbyOverlay).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Xep roster len 2 cot cua man ghep tran:
    ///  - FFA: xen ke trai / phai (nguoi 1 trai, nguoi 2 phai...), minh luon o dau.
    ///  - TDM: theo doi - doi cua minh ben trai, doi dich ben phai (TeamId). Chua biet doi thi xen ke va ghi
    ///    "chia doi khi vao tran".
    /// Avatar tai 1 lan roi cache theo URL (phong cho lam moi roster lien tuc -> khong tai lai, khong nhay hinh).
    /// SRP: chi trinh bay; lay du lieu o dau (PlayFab / to doi / Fusion) la viec cua nguoi goi.
    /// </summary>
    public static class LobbyRosterView
    {
        public const int SlotsPerColumn = 5;

        private static readonly Dictionary<string, Sprite> _avatarCache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> _loading = new HashSet<string>();
        // Moi lan Fill tang phien ban -> avatar tai xong cua lan cu khong ghi de len roster moi.
        private static readonly Dictionary<MatchmakingController, int> _version = new Dictionary<MatchmakingController, int>();

        public static void Fill(MatchmakingController screen, List<LobbyMember> members, bool team, MonoBehaviour coroutineHost)
        {
            if (screen == null || members == null) return;
            int ver; _version.TryGetValue(screen, out ver); ver++; _version[screen] = ver;

            var ordered = new List<LobbyMember>();
            LobbyMember me = null;
            foreach (var m in members) if (m != null && m.IsLocal) { ordered.Add(m); me = m; break; }
            foreach (var m in members) if (m != null && m != me) ordered.Add(m);

            var left = new List<LobbyMember>();
            var right = new List<LobbyMember>();
            bool hasTeams = false;
            if (team) foreach (var m in ordered) if (!string.IsNullOrEmpty(m.TeamId)) { hasTeams = true; break; }

            if (hasTeams)
            {
                string myTeam = me != null && !string.IsNullOrEmpty(me.TeamId) ? me.TeamId : null;
                if (myTeam == null) foreach (var m in ordered) if (!string.IsNullOrEmpty(m.TeamId)) { myTeam = m.TeamId; break; }
                foreach (var m in ordered) (m.TeamId == myTeam ? left : right).Add(m);
            }
            else
            {
                for (int i = 0; i < ordered.Count; i++) (i % 2 == 0 ? left : right).Add(ordered[i]);
            }

            for (int i = 0; i < SlotsPerColumn; i++)
            {
                SetSlot(screen, 1, i, i < left.Count ? left[i] : null, ver, coroutineHost);
                SetSlot(screen, 2, i, i < right.Count ? right[i] : null, ver, coroutineHost);
            }

            int total = Mathf.Min(ordered.Count, SlotsPerColumn * 2);
            if (hasTeams) screen.SetTeamHeaders("ĐỘI CỦA BẠN (" + left.Count + ")", "ĐỘI ĐỊCH (" + right.Count + ")",
                new Color(0.31f, 0.84f, 0.79f, 1f), new Color(0.92f, 0.30f, 0.27f, 1f));
            else if (team) screen.SetTeamHeaders("NGƯỜI CHƠI (" + total + ")", "CHIA ĐỘI KHI VÀO TRẬN");
            else screen.SetTeamHeaders("NGƯỜI CHƠI (" + total + ")", "");
        }

        private static void SetSlot(MatchmakingController screen, int column, int index, LobbyMember m, int ver, MonoBehaviour host)
        {
            if (m == null) { Apply(screen, column, index, "", null, ""); return; }
            string status = m.IsLocal ? "BẠN" : "";
            Sprite cached = null;
            if (!string.IsNullOrEmpty(m.AvatarUrl)) _avatarCache.TryGetValue(m.AvatarUrl, out cached);
            Apply(screen, column, index, m.Name, cached, status);
            if (cached == null && !string.IsNullOrEmpty(m.AvatarUrl) && host != null && host.isActiveAndEnabled && !_loading.Contains(m.AvatarUrl))
                host.StartCoroutine(Load(m.AvatarUrl, sp =>
                {
                    int now; _version.TryGetValue(screen, out now);
                    if (screen != null && now == ver && sp != null) Apply(screen, column, index, m.Name, sp, status);
                }));
        }

        private static void Apply(MatchmakingController screen, int column, int index, string name, Sprite avatar, string status)
        {
            if (column == 1) screen.SetTeam1Member(index, name, avatar, status);
            else screen.SetTeam2Member(index, name, avatar, status);
        }

        private static IEnumerator Load(string url, System.Action<Sprite> onDone)
        {
            _loading.Add(url);
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                _loading.Remove(url);
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var tex = DownloadHandlerTexture.GetContent(req); if (tex != null) tex = ImageScale.Fit(tex, 64); // thu nho 64px
                if (tex == null) yield break;
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _avatarCache[url] = sp;
                if (onDone != null) onDone(sp);
            }
        }
    }
}
