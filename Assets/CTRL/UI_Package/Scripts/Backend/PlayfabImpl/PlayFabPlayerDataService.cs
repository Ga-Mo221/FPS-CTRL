using System;
using System.Collections.Generic;
using Backend.Interfaces;
using Backend.Models;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

namespace Backend.PlayfabImpl
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  QUY ƯỚC CHIỀU DỮ LIỆU (đọc kỹ để hiểu nhanh cả file):
    //
    //   ↑ GỬI LÊN  = client GHI dữ liệu LÊN PlayFab (các API Update... / AddOrUpdate...)
    //                → dữ liệu được lưu trên cloud của PlayFab.
    //   ↓ LẤY VỀ   = client ĐỌC dữ liệu TỪ PlayFab XUỐNG (các API Get...)
    //                → dữ liệu trả về qua callback để đưa vào game/UI.
    //
    //  Mọi lời gọi PlayFabClientAPI.* đều là 1 request mạng BẤT ĐỒNG BỘ:
    //  kết quả về ở callback thành công (arg 2) hoặc callback lỗi (arg 3).
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dữ liệu người chơi trên PlayFab: profile (name/avatar/level), loadout, stats, leaderboard.
    /// Một instance implement 4 interface nhỏ (ISP), expose qua BackendBootstrap.
    /// </summary>
    public class PlayFabPlayerDataService :
        IPlayerProfileService, ILoadoutService, IPlayerStatsService, ILeaderboardService, IMenuDataService, IFriendsService, ISettingsService
    {
        // Khoá lưu Loadout trong User Data (kiểu key-value string trên PlayFab).
        private const string LoadoutKey = "Loadout";
        // So slot loadout + tien to key cho tung slot (Loadout_0/1/2).
        private const int LoadoutSlotCount = 3;
        private static string LoadoutSlotKey(int slot) { return "Loadout_" + slot; }
        // Khoa luu toan bo Settings (1 blob JSON) trong User Data.
        private const string SettingsKey = "Settings";

        // ============================ PROFILE ============================

        // ↓ LẤY VỀ: hồ sơ CỦA CHÍNH MÌNH (name + avatar) rồi lấy thêm Level (statistic).
        public void GetMyProfile(Action<PlayerProfileInfo> onResult, Action<AuthError> onError)
        {
            // PlayFabId của mình đã được SDK lưu sau khi đăng nhập.
            string myId = PlayFabSettings.staticPlayer.PlayFabId;

            // Bước 1 ↓ LẤY VỀ: name + avatar.
            GetProfileInternal(myId, profile =>
            {
                // Bước 2 ↓ LẤY VỀ: đọc Level từ Player Statistics (một request nữa),
                // rồi gộp vào profile trước khi trả cho UI.
                PlayFabClientAPI.GetPlayerStatistics(   // ↓ LẤY VỀ statistics
                    new GetPlayerStatisticsRequest { StatisticNames = new List<string> { StatKeys.Level } },
                    stat =>
                    {
                        if (stat.Statistics != null)
                            foreach (var v in stat.Statistics)
                                if (v.StatisticName == StatKeys.Level) profile.Level = v.Value;
                        onResult?.Invoke(profile);          // trả kết quả cuối cùng cho UI
                    },
                    _ => onResult?.Invoke(profile));        // Level lỗi -> vẫn trả profile (name+avatar)
            }, onError);
        }

        // ↓ LẤY VỀ: hồ sơ CỦA NGƯỜI KHÁC theo PlayFabId (dùng cho ROSTER: điền name+avatar mỗi slot).
        public void GetProfile(string playFabId, Action<PlayerProfileInfo> onResult, Action<AuthError> onError)
            => GetProfileInternal(playFabId, onResult, onError);

        // Hàm dùng chung: ↓ LẤY VỀ name + avatar của 1 player bất kỳ.
        private void GetProfileInternal(string playFabId, Action<PlayerProfileInfo> onResult, Action<AuthError> onError)
        {
            var req = new GetPlayerProfileRequest
            {
                PlayFabId = playFabId,
                // Xin PlayFab trả về đúng các field này (title phải BẬT cho phép xem — Client Profile Options).
                ProfileConstraints = new PlayerProfileViewConstraints
                {
                    ShowDisplayName = true,
                    ShowAvatarUrl = true
                }
            };

            PlayFabClientAPI.GetPlayerProfile(req,   // ↓ LẤY VỀ profile
                res =>
                {
                    var p = res.PlayerProfile;       // dữ liệu PlayFab trả xuống
                    onResult?.Invoke(new PlayerProfileInfo
                    {
                        PlayFabId = playFabId,
                        DisplayName = (p != null && !string.IsNullOrEmpty(p.DisplayName)) ? p.DisplayName : "PLAYER",   // [NEWPLAYER 2026-09-14 - Aemy] fallback PLAYER
                        AvatarUrl = p != null ? p.AvatarUrl : null,
                        Level = 0                    // Level lấy riêng ở GetMyProfile
                    });
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ↑ GỬI LÊN: đặt tên hiển thị lên PlayFab.
        public void SetDisplayName(string displayName, Action onSuccess, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(   // ↑ GỬI LÊN display name
                new UpdateUserTitleDisplayNameRequest { DisplayName = displayName },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // ↑ GỬI LÊN: đặt URL avatar lên PlayFab.
        public void SetAvatarUrl(string imageUrl, Action onSuccess, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdateAvatarUrl(   // ↑ GỬI LÊN avatar url
                new UpdateAvatarUrlRequest { ImageUrl = imageUrl },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // ↑ GỬI LÊN: ghi Level (dưới dạng 1 statistic) lên PlayFab.
        public void SetLevel(int level, Action onSuccess, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdatePlayerStatistics(   // ↑ GỬI LÊN statistic "Level"
                new UpdatePlayerStatisticsRequest
                {
                    Statistics = new List<StatisticUpdate>
                    {
                        new StatisticUpdate { StatisticName = StatKeys.Level, Value = level }
                    }
                },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // Xoá tài khoản: client KHÔNG tự xoá được -> gọi CloudScript 'DeleteMyAccount' (server.DeletePlayer).
        public void DeleteMyAccount(System.Action onSuccess, System.Action<AuthError> onError)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest { FunctionName = "DeleteMyAccount", GeneratePlayStreamEvent = true },
                res =>
                {
                    if (res.Error != null)
                    {
                        onError?.Invoke(new AuthError { ErrorCode = "CloudScriptError", ErrorMessage = res.Error.Message });
                        return;
                    }
                    onSuccess?.Invoke();
                },
                e => onError?.Invoke(MapError(e)));
        }


        // [NEWPLAYER 2026-09-14 - Aemy] Set mac dinh player moi: loadout 3 slot (Dao+Vesper, GE+Flash) + Level 1 + stats 0.
        public void InitializeNewPlayer(Action onSuccess, Action<AuthError> onError)
        {
            var def = new LoadoutConfig
            {
                Weapons = new string[] { "Knife", "Vesper" },   // [Aemy 2026-10-11] thong nhat ID
                WeaponTypes = new string[] { "", "Pistol" },
                Utilities = new string[] { "Lựu đạn", "Lựu đạn choáng" }
            };
            string json = JsonUtility.ToJson(def);
            var data = new Dictionary<string, string>
            {
                { LoadoutSlotKey(0), json },
                { LoadoutSlotKey(1), json },
                { LoadoutSlotKey(2), json }
            };
            PlayFabClientAPI.UpdateUserData(
                new UpdateUserDataRequest { Data = data },
                _ =>
                {
                    PlayFabClientAPI.UpdatePlayerStatistics(
                        new UpdatePlayerStatisticsRequest
                        {
                            Statistics = new List<StatisticUpdate>
                            {
                                new StatisticUpdate { StatisticName = StatKeys.Level, Value = 1 },
                                new StatisticUpdate { StatisticName = StatKeys.Kills, Value = 0 },
                                new StatisticUpdate { StatisticName = StatKeys.Deaths, Value = 0 },
                                new StatisticUpdate { StatisticName = StatKeys.HeadshotKills, Value = 0 },
                                new StatisticUpdate { StatisticName = StatKeys.Matches, Value = 0 },
                                new StatisticUpdate { StatisticName = StatKeys.Assist, Value = 0 },        // [Aemy]
                                new StatisticUpdate { StatisticName = StatKeys.Experience, Value = 0 },    // [Aemy]
                                new StatisticUpdate { StatisticName = StatKeys.KDAx100, Value = 0 },
                                new StatisticUpdate { StatisticName = StatKeys.HeadshotPct, Value = 0 }
                            }
                        },
                        __ => { if (onSuccess != null) onSuccess(); },
                        e => { if (onError != null) onError(MapError(e)); });
                },
                e => { if (onError != null) onError(MapError(e)); });
        }

        // ============================ LOADOUT ============================

        // ↑ GỬI LÊN: lưu Loadout (2 súng + 2 utility) lên PlayFab User Data.
        // LoadoutConfig -> chuỗi JSON -> lưu vào key "Loadout".
        public void SaveLoadout(LoadoutConfig loadout, Action onSuccess, Action<AuthError> onError)
        {
            string json = JsonUtility.ToJson(loadout);   // đối tượng -> JSON để lưu được dạng string
            PlayFabClientAPI.UpdateUserData(             // ↑ GỬI LÊN user data
                new UpdateUserDataRequest
                {
                    Data = new Dictionary<string, string> { { LoadoutKey, json } }
                },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // ↓ LẤY VỀ: đọc Loadout đã lưu từ PlayFab User Data, parse JSON -> LoadoutConfig.
        public void LoadLoadout(Action<LoadoutConfig> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserData(   // ↓ LẤY VỀ user data
                new GetUserDataRequest { Keys = new List<string> { LoadoutKey } },
                res =>
                {
                    if (res.Data != null && res.Data.ContainsKey(LoadoutKey))
                    {
                        // JSON (từ cloud) -> đối tượng LoadoutConfig để dùng trong game.
                        var cfg = JsonUtility.FromJson<LoadoutConfig>(res.Data[LoadoutKey].Value);
                        onResult?.Invoke(cfg ?? new LoadoutConfig());
                    }
                    else onResult?.Invoke(new LoadoutConfig()); // chưa từng lưu -> loadout rỗng
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ↑ GUI LEN: luu loadout cho 1 SLOT cu the (key "Loadout_{slot}").
        public void SaveLoadoutSlot(int slot, LoadoutConfig loadout, Action onSuccess, Action<AuthError> onError)
        {
            string json = JsonUtility.ToJson(loadout);
            PlayFabClientAPI.UpdateUserData(
                new UpdateUserDataRequest
                {
                    Data = new Dictionary<string, string> { { LoadoutSlotKey(slot), json } }
                },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // ↓ LAY VE: doc TAT CA slot trong 1 request. arr[i] = null neu slot i chua co du lieu.
        public void LoadAllLoadouts(Action<LoadoutConfig[]> onResult, Action<AuthError> onError)
        {
            var keys = new List<string>();
            for (int i = 0; i < LoadoutSlotCount; i++) keys.Add(LoadoutSlotKey(i));

            PlayFabClientAPI.GetUserData(
                new GetUserDataRequest { Keys = keys },
                res =>
                {
                    var arr = new LoadoutConfig[LoadoutSlotCount];
                    for (int i = 0; i < LoadoutSlotCount; i++)
                    {
                        string k = LoadoutSlotKey(i);
                        if (res.Data != null && res.Data.ContainsKey(k))
                            arr[i] = JsonUtility.FromJson<LoadoutConfig>(res.Data[k].Value);
                        else
                            arr[i] = null; // slot chua tung luu
                    }
                    onResult?.Invoke(arr);
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ============================ SETTINGS ============================

        // GUI LEN: luu toan bo settings (JSON) vao User Data key "Settings".
        public void SaveSettings(string settingsJson, Action onSuccess, Action<AuthError> onError)
        {
            PlayFabClientAPI.UpdateUserData(
                new UpdateUserDataRequest
                {
                    Data = new Dictionary<string, string> { { SettingsKey, settingsJson } }
                },
                _ => onSuccess?.Invoke(),
                e => onError?.Invoke(MapError(e)));
        }

        // LAY VE: doc settings JSON. Tra null neu chua tung luu.
        public void LoadSettings(Action<string> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserData(
                new GetUserDataRequest { Keys = new List<string> { SettingsKey } },
                res =>
                {
                    string json = (res.Data != null && res.Data.ContainsKey(SettingsKey)) ? res.Data[SettingsKey].Value : null;
                    onResult?.Invoke(json);
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ============================ STATS ============================

        // ↓ LẤY VỀ: đọc các chỉ số chiến đấu tích luỹ từ Player Statistics.
        public void GetMyStats(Action<CombatStats> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetPlayerStatistics(
                new GetPlayerStatisticsRequest
                {
                    StatisticNames = new List<string>
                    { StatKeys.Kills, StatKeys.Deaths, StatKeys.HeadshotKills, StatKeys.Matches, StatKeys.Assist, StatKeys.Experience }
                },
                res => onResult?.Invoke(BuildStats(res.Statistics)),
                e => onError?.Invoke(MapError(e)));
        }

        // Kết hợp ↓ LẤY VỀ rồi ↑ GỬI LÊN: cộng dồn kết quả 1 trận vào tổng tích luỹ.
        // [Aemy 2026-09-17] Them assists + expGained; Level suy tu Experience; tra CombatStats moi.
        public void ReportMatchResult(int kills, int deaths, int headshotKills, int assists, int expGained,
            Action<CombatStats> onSuccess, Action<AuthError> onError)
        {
            GetMyStats(cur =>
            {
                var total = new CombatStats
                {
                    Kills = cur.Kills + kills,
                    Deaths = cur.Deaths + deaths,
                    HeadshotKills = cur.HeadshotKills + headshotKills,
                    Assist = cur.Assist + assists,                                   // [Aemy]
                    Experience = cur.Experience + (expGained > 0 ? expGained : 0),   // [Aemy]
                    Matches = cur.Matches + 1
                };
                int kdax100 = (int)System.Math.Round(total.KDA * 100.0);

                PlayFabClientAPI.UpdatePlayerStatistics(
                    new UpdatePlayerStatisticsRequest
                    {
                        Statistics = new List<StatisticUpdate>
                        {
                            new StatisticUpdate { StatisticName = StatKeys.Kills,         Value = total.Kills },
                            new StatisticUpdate { StatisticName = StatKeys.Deaths,        Value = total.Deaths },
                            new StatisticUpdate { StatisticName = StatKeys.HeadshotKills, Value = total.HeadshotKills },
                            new StatisticUpdate { StatisticName = StatKeys.Matches,       Value = total.Matches },
                            new StatisticUpdate { StatisticName = StatKeys.KDAx100,       Value = kdax100 },
                            new StatisticUpdate { StatisticName = StatKeys.HeadshotPct,   Value = total.HeadshotPercent },
                            new StatisticUpdate { StatisticName = StatKeys.Assist,        Value = total.Assist },       // [Aemy]
                            new StatisticUpdate { StatisticName = StatKeys.Experience,    Value = total.Experience },   // [Aemy]
                            new StatisticUpdate { StatisticName = StatKeys.Level,         Value = total.Level },        // [Aemy] suy tu Experience
                        }
                    },
                    _ => { if (onSuccess != null) onSuccess(total); },
                    e => onError?.Invoke(MapError(e)));
            }, onError);
        }

        // (local) Chuyển list statistic PlayFab trả về -> đối tượng CombatStats cho gọn.
        private static CombatStats BuildStats(List<StatisticValue> list)
        {
            var s = new CombatStats();
            if (list == null) return s;
            foreach (var v in list)
            {
                switch (v.StatisticName)
                {
                    case StatKeys.Kills:         s.Kills = v.Value; break;
                    case StatKeys.Deaths:        s.Deaths = v.Value; break;
                    case StatKeys.HeadshotKills: s.HeadshotKills = v.Value; break;
                    case StatKeys.Matches:       s.Matches = v.Value; break;
                    case StatKeys.Assist:        s.Assist = v.Value; break;         // [Aemy]
                    case StatKeys.Experience:    s.Experience = v.Value; break;     // [Aemy]
                }
            }
            return s;
        }

        // ============================ LEADERBOARD ============================

        // ↓ LẤY VỀ: đọc bảng xếp hạng theo 1 statistic (vd KDAx100, HeadshotPct).
        public void GetLeaderboard(string statisticName, int maxCount,
            Action<List<LeaderboardEntryInfo>> onResult, Action<AuthError> onError)
        {
            var req = new GetLeaderboardRequest
            {
                StatisticName = statisticName,   // xếp hạng theo chỉ số này
                MaxResultsCount = maxCount,       // lấy tối đa bao nhiêu dòng
                StartPosition = 0,                // từ hạng 1
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true } // kèm tên hiển thị
            };

            PlayFabClientAPI.GetLeaderboard(req,   // ↓ LẤY VỀ leaderboard
                res =>
                {
                    // Chuyển dữ liệu PlayFab -> list gọn cho UI hiển thị.
                    var list = new List<LeaderboardEntryInfo>();
                    if (res.Leaderboard != null)
                        foreach (var e in res.Leaderboard)
                            list.Add(new LeaderboardEntryInfo
                            {
                                Position = e.Position,
                                PlayFabId = e.PlayFabId,
                                DisplayName = e.DisplayName,
                                StatValue = e.StatValue
                            });
                    onResult?.Invoke(list);
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ============================ MENU DATA ============================


        // ↓ LẤY VỀ: số dư các loại virtual currency (vd GC, CR) từ kho người chơi.
        public void GetVirtualCurrency(Action<Dictionary<string, int>> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserInventory(   // ↓ LẤY VỀ inventory (kèm VirtualCurrency)
                new GetUserInventoryRequest(),
                res => onResult?.Invoke(res.VirtualCurrency ?? new Dictionary<string, int>()),
                e => onError?.Invoke(MapError(e)));
        }

        // ↓ LẤY VỀ danh sách ItemId đang sở hữu (PlayFab inventory) — đánh dấu "đã mua".
        // Lay danh sach itemId da so huu tu UserReadOnlyData (CloudScript ghi khi mua). Nguon: Title Data.
        public void GetOwnedItems(Action<List<string>> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserReadOnlyData(
                new GetUserDataRequest(),
                res =>
                {
                    var ids = new List<string>();
                    if (res.Data != null && res.Data.ContainsKey("OwnedShopItems"))
                    {
                        string json = res.Data["OwnedShopItems"].Value;
                        if (!string.IsNullOrEmpty(json))
                        {
                            try
                            {
                                var arr = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<List<string>>(json);
                                if (arr != null) ids.AddRange(arr);
                            }
                            catch { }
                        }
                    }
                    onResult?.Invoke(ids);
                },
                e => onError?.Invoke(MapError(e)));
        }

        // MUA vật phẩm qua PlayFab Catalog/Store: server tự trừ VC + cấp item (chống gian lận).
        // MUA qua CloudScript 'PurchaseShopItem': server doc gia tu Title Data 'ShopCatalog',
        // tru tien va ghi so huu vao UserReadOnlyData. Title Data la NGUON DUY NHAT (khong can Catalog).
        // currencyCode/price o day chi de tuong thich interface; server tu doc gia that de chong hack.
        public void PurchaseItem(string itemId, string currencyCode, int price, Action onSuccess, Action<AuthError> onError)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest
                {
                    FunctionName = "PurchaseShopItem",
                    FunctionParameter = new { itemId = itemId },
                    GeneratePlayStreamEvent = true
                },
                res =>
                {
                    if (res.Error != null)
                    {
                        onError?.Invoke(new AuthError { ErrorCode = "CloudScriptError", ErrorMessage = res.Error.Message });
                        return;
                    }
                    bool ok = false;
                    string msg = "Mua that bai.";
                    var result = res.FunctionResult as PlayFab.Json.JsonObject;
                    if (result != null)
                    {
                        object v;
                        if (result.TryGetValue("ok", out v) && v != null) { bool b; if (bool.TryParse(v.ToString(), out b)) ok = b; }
                        if (result.TryGetValue("message", out v) && v != null && !string.IsNullOrEmpty(v.ToString())) msg = v.ToString();
                    }
                    if (ok) onSuccess?.Invoke();
                    else onError?.Invoke(new AuthError { ErrorCode = "PurchaseFailed", ErrorMessage = msg });
                },
                e => onError?.Invoke(MapError(e)));
        }




        // ============================ WEAPON KILLS ============================
        // Lưu số kill theo từng súng dưới dạng JSON trong User Data (key "WeaponKills").
        // Vì số loại súng nhiều/động, dùng 1 blob JSON linh hoạt hơn tạo statistic cho mỗi súng.
        private const string WeaponKillsKey = "WeaponKills";

        // ↓ LẤY VỀ: bảng kill-theo-súng, sắp giảm dần, lấy top N (cho panel "SÚNG CHỦ LỰC").
        public void GetTopWeapons(int count, Action<List<WeaponKillEntry>> onResult, Action<AuthError> onError)
        {
            GetWeaponTable(table =>
            {
                table.entries.Sort((a, b) => b.kills.CompareTo(a.kills));
                if (count > 0 && table.entries.Count > count)
                    table.entries = table.entries.GetRange(0, count);
                onResult?.Invoke(table.entries);
            }, onError);
        }

        // Kết hợp ↓ LẤY VỀ rồi ↑ GỬI LÊN: cộng dồn kill-theo-súng của 1 trận vào tổng.
        public void AddWeaponKills(Dictionary<string, int> killsThisMatch, Action onSuccess, Action<AuthError> onError)
        {
            if (killsThisMatch == null || killsThisMatch.Count == 0) { onSuccess?.Invoke(); return; }
            GetWeaponTable(table =>
            {
                foreach (var kv in killsThisMatch)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    var entry = table.entries.Find(x => x.weapon == kv.Key);
                    if (entry == null) { entry = new WeaponKillEntry { weapon = kv.Key, kills = 0 }; table.entries.Add(entry); }
                    entry.kills += kv.Value;
                }
                string json = JsonUtility.ToJson(table);
                PlayFabClientAPI.UpdateUserData(
                    new UpdateUserDataRequest { Data = new Dictionary<string, string> { { WeaponKillsKey, json } } },
                    _ => onSuccess?.Invoke(),
                    e => onError?.Invoke(MapError(e)));
            }, onError);
        }

        // ↓ LẤY VỀ + parse bảng kill-theo-súng (rỗng nếu chưa từng lưu).
        private void GetWeaponTable(Action<WeaponKillTable> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetUserData(
                new GetUserDataRequest { Keys = new List<string> { WeaponKillsKey } },
                res =>
                {
                    WeaponKillTable table = null;
                    if (res.Data != null && res.Data.ContainsKey(WeaponKillsKey))
                        table = JsonUtility.FromJson<WeaponKillTable>(res.Data[WeaponKillsKey].Value);
                    onResult?.Invoke(table ?? new WeaponKillTable());
                },
                e => onError?.Invoke(MapError(e)));
        }

        // ----------------- Helper -----------------
        // Chuyển lỗi PlayFab -> AuthError trung lập (để UI không phụ thuộc kiểu của PlayFab).
        private static AuthError MapError(PlayFabError error) => new AuthError
        {
            ErrorCode = error.Error.ToString(),
            ErrorMessage = error.ErrorMessage
        };
    

        // ====== BAN BE (PlayFab Friends) ======
        /// <summary>Lay danh sach ban be kem ten hien thi + avatar url.</summary>
        public void GetFriends(Action<List<Backend.Models.FriendInfo>> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.GetFriendsList(
                new GetFriendsListRequest
                {
                    ProfileConstraints = new PlayerProfileViewConstraints
                    {
                        ShowDisplayName = true,
                        ShowAvatarUrl = true
                    }
                },
                res =>
                {
                    var list = new List<Backend.Models.FriendInfo>();
                    if (res.Friends != null)
                    {
                        foreach (var f in res.Friends)
                        {
                            var name = (f.Profile != null && !string.IsNullOrEmpty(f.Profile.DisplayName))
                                ? f.Profile.DisplayName
                                : f.TitleDisplayName;
                            list.Add(new Backend.Models.FriendInfo
                            {
                                PlayFabId = f.FriendPlayFabId,
                                DisplayName = name,
                                AvatarUrl = f.Profile != null ? f.Profile.AvatarUrl : null
                            });
                        }
                    }
                    if (onResult != null) onResult(list);
                },
                e => { if (onError != null) onError(MapError(e)); });
        }

        // [Aemy 2026-09-21] Them ban theo Ten hien thi HOAC PlayFabId.
        public void AddFriend(string displayNameOrId, Action onSuccess, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(displayNameOrId)) return;
            var req = new AddFriendRequest();
            bool looksId = System.Text.RegularExpressions.Regex.IsMatch(displayNameOrId, "^[A-Fa-f0-9]{8,20}$");
            if (looksId) req.FriendPlayFabId = displayNameOrId.ToUpper();
            else req.FriendTitleDisplayName = displayNameOrId;
            PlayFabClientAPI.AddFriend(req,
                _ => { if (onSuccess != null) onSuccess(); },
                e => { if (onError != null) onError(MapError(e)); });
        }

        // [Aemy] Xoa ban theo PlayFabId.
        public void RemoveFriend(string playFabId, Action onSuccess, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(playFabId)) return;
            PlayFabClientAPI.RemoveFriend(new RemoveFriendRequest { FriendPlayFabId = playFabId },
                _ => { if (onSuccess != null) onSuccess(); },
                e => { if (onError != null) onError(MapError(e)); });
        }

        // [Aemy] Heartbeat: ghi LastSeen = epoch phut hien tai.
        public void Heartbeat(Action onSuccess = null, Action<AuthError> onError = null)
        {
            int nowMin = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60);
            PlayFabClientAPI.UpdatePlayerStatistics(
                new UpdatePlayerStatisticsRequest
                {
                    Statistics = new List<StatisticUpdate> { new StatisticUpdate { StatisticName = StatKeys.LastSeen, Value = nowMin } }
                },
                _ => { if (onSuccess != null) onSuccess(); },
                e => { if (onError != null) onError(MapError(e)); });
        }

        // [Aemy] Danh sach ban + presence: GetFriends roi phu LastSeen tu GetFriendLeaderboard.
        public void GetFriendsWithPresence(Action<List<Backend.Models.FriendInfo>> onResult, Action<AuthError> onError)
        {
            GetFriends(list =>
            {
                PlayFabClientAPI.GetFriendLeaderboard(
                    new GetFriendLeaderboardRequest { StatisticName = StatKeys.LastSeen, MaxResultsCount = 100, StartPosition = 0 },
                    lb =>
                    {
                        var seen = new Dictionary<string, int>();
                        if (lb.Leaderboard != null)
                            foreach (var e in lb.Leaderboard)
                                if (!string.IsNullOrEmpty(e.PlayFabId)) seen[e.PlayFabId] = e.StatValue;
                        int nowMin = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60);
                        foreach (var fr in list)
                        {
                            int last;
                            if (fr.PlayFabId != null && seen.TryGetValue(fr.PlayFabId, out last))
                            {
                                fr.LastSeenMinutes = last;
                                int diff = nowMin - last;
                                fr.Presence = diff <= 2 ? Backend.Models.FriendPresence.Online
                                            : diff <= 10 ? Backend.Models.FriendPresence.Away
                                            : Backend.Models.FriendPresence.Offline;
                            }
                            else { fr.LastSeenMinutes = 0; fr.Presence = Backend.Models.FriendPresence.Offline; }
                        }
                        if (onResult != null) onResult(list);
                    },
                    e =>
                    {
                        foreach (var fr in list) fr.Presence = Backend.Models.FriendPresence.Offline;
                        if (onResult != null) onResult(list);
                    });
            }, onError);
        }

        // ============ KET BAN 2 CHIEU qua CloudScript (request -> accept) [Aemy 2026-09-24] ============
        // Helper: goi 1 handler CloudScript tra { ok, message } (giong PurchaseItem).
        private void CallFriendCloud(string fn, object param, Action<string> onDone, Action<AuthError> onError)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest { FunctionName = fn, FunctionParameter = param, GeneratePlayStreamEvent = false },
                res =>
                {
                    if (res.Error != null) { if (onError != null) onError(new AuthError { ErrorCode = "CloudScriptError", ErrorMessage = res.Error.Message }); return; }
                    bool ok = false; string msg = "";
                    var r = res.FunctionResult as PlayFab.Json.JsonObject;
                    if (r != null)
                    {
                        object v;
                        if (r.TryGetValue("ok", out v) && v != null) { bool b; if (bool.TryParse(v.ToString(), out b)) ok = b; }
                        if (r.TryGetValue("message", out v) && v != null) msg = v.ToString();
                    }
                    if (ok) { if (onDone != null) onDone(msg); }
                    else { if (onError != null) onError(new AuthError { ErrorCode = "FriendRequestFailed", ErrorMessage = string.IsNullOrEmpty(msg) ? "Thao tac that bai." : msg }); }
                },
                e => { if (onError != null) onError(MapError(e)); });
        }

        public void IndexMyName(Action onDone = null, Action<AuthError> onError = null)
        {
            CallFriendCloud("IndexMyName", null, _ => { if (onDone != null) onDone(); }, onError);
        }

        public void SendFriendRequest(string query, Action<string> onDone, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(query)) { if (onError != null) onError(new AuthError { ErrorCode = "EmptyQuery", ErrorMessage = "Nhap ten hoac ID nguoi choi." }); return; }
            CallFriendCloud("SendFriendRequest", new { query = query }, onDone, onError);
        }

        public void AcceptFriendRequest(string fromId, Action onSuccess, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(fromId)) return;
            CallFriendCloud("AcceptFriendRequest", new { fromId = fromId }, _ => { if (onSuccess != null) onSuccess(); }, onError);
        }

        public void DeclineFriendRequest(string fromId, Action onSuccess, Action<AuthError> onError)
        {
            if (string.IsNullOrEmpty(fromId)) return;
            CallFriendCloud("DeclineFriendRequest", new { fromId = fromId }, _ => { if (onSuccess != null) onSuccess(); }, onError);
        }

        public void GetIncomingRequests(Action<List<Backend.Models.FriendRequestInfo>> onResult, Action<AuthError> onError)
        {
            PlayFabClientAPI.ExecuteCloudScript(
                new ExecuteCloudScriptRequest { FunctionName = "GetFriendRequests", GeneratePlayStreamEvent = false },
                res =>
                {
                    var list = new List<Backend.Models.FriendRequestInfo>();
                    if (res.Error != null) { if (onError != null) onError(new AuthError { ErrorCode = "CloudScriptError", ErrorMessage = res.Error.Message }); return; }
                    var r = res.FunctionResult as PlayFab.Json.JsonObject;
                    if (r != null)
                    {
                        object arrObj;
                        if (r.TryGetValue("requests", out arrObj) && arrObj is System.Collections.IList)
                        {
                            foreach (var item in (System.Collections.IList)arrObj)
                            {
                                var jo = item as PlayFab.Json.JsonObject;
                                if (jo == null) continue;
                                var info = new Backend.Models.FriendRequestInfo();
                                object v;
                                if (jo.TryGetValue("fromId", out v) && v != null) info.FromId = v.ToString();
                                if (jo.TryGetValue("fromName", out v) && v != null) info.FromName = v.ToString();
                                if (jo.TryGetValue("ts", out v) && v != null) { long ts; if (long.TryParse(v.ToString(), out ts)) info.Ts = ts; }
                                list.Add(info);
                            }
                        }
                    }
                    if (onResult != null) onResult(list);
                },
                e => { if (onError != null) onError(MapError(e)); });
        }
}
}
