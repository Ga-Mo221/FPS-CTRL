using System;
using System.Collections.Generic;

namespace Backend.Models
{
    /// <summary>Hồ sơ hiển thị của người chơi (dùng cho roster, menu...).</summary>
    [Serializable]
    public class PlayerProfileInfo
    {
        public string PlayFabId;
        public string DisplayName;
        public string AvatarUrl;
        public int Level;
    }

    /// <summary>Cấu hình Loadout: 2 slot súng + 2 slot utility (theo GDD).</summary>
    [Serializable]
    public class LoadoutConfig
    {
        public string[] Weapons = new string[2];
        public string[] Utilities = new string[2];
        // Loai cua tung sung (song song voi Weapons) - de truyen vao loadout in-game.
        public string[] WeaponTypes = new string[2];
    }

    /// <summary>Thống kê chiến đấu tích luỵ.</summary>
    public class CombatStats
    {
        public int Kills;
        public int Deaths;
        public int HeadshotKills;
        public int Matches;
        // [Aemy 2026-09-17] Assist (ban lead co dung) + Experience (tich luy de len Level).
        public int Assist;
        public int Experience;

        public float KDA => Deaths <= 0 ? Kills : (float)Kills / Deaths;
        public int HeadshotPercent => Kills <= 0 ? 0 : (int)System.Math.Round(100.0 * HeadshotKills / Kills);
        // [Aemy] Level suy ra tu tong Experience (nguon su that duy nhat cho Level).
        public int Level => LevelCurve.LevelForXp(Experience);
    }

    /// <summary>Một dòng trên bảng xếp hạng.</summary>
    public class LeaderboardEntryInfo
    {
        public int Position;      // 0-based theo PlayFab
        public string PlayFabId;
        public string DisplayName;
        public int StatValue;
    }

    /// <summary>Một súng và số kill tích luỵ bằng súng đó.</summary>
    [Serializable]
    public class WeaponKillEntry
    {
        public string weapon;
        public int kills;
    }

    /// <summary>Bảng kill-theo-súng (lưu JSON trong User Data để hiện "SÚNG CHẦ LỰC").</summary>
    [Serializable]
    public class WeaponKillTable
    {
        public List<WeaponKillEntry> entries = new List<WeaponKillEntry>();
    }

        /// <summary>Một người bạn trong danh sách bạn bè (PlayFab Friends).</summary>
    /// <summary>[Aemy 2026-09-21] Trang thai hien dien.</summary>
    public enum FriendPresence { Offline = 0, Away = 1, Online = 2 }

    [Serializable]
    public class FriendInfo
    {
        public string PlayFabId;
        public string DisplayName;
        public string AvatarUrl;
        public int Level;                 // [Aemy]
        public long LastSeenMinutes;      // [Aemy] epoch phut lan cuoi online (0 = khong ro)
        public FriendPresence Presence;   // [Aemy] suy tu LastSeen
    }

    /// <summary>[Aemy 2026-09-24] Mot loi moi ket ban DEN nguoi choi (tu CloudScript FriendRequests).</summary>
    [Serializable]
    public class FriendRequestInfo
    {
        public string FromId;    // PlayFabId nguoi gui
        public string FromName;  // Ten hien thi nguoi gui
        public long Ts;          // epoch giay luc gui
    }

/// <summary>Tên các statistic dùng chung (khớp tên tạo trên PlayFab).</summary>
    public static class StatKeys
    {
        public const string Kills         = "Kills";
        public const string Deaths        = "Deaths";
        public const string HeadshotKills = "HeadshotKills";
        public const string Matches       = "Matches";
        public const string Level         = "Level";
        public const string Assist        = "Assist";        // [Aemy] tro lai theo ban lead
        public const string Experience    = "Experience";    // [Aemy] tong XP tich luy
        public const string LastSeen      = "LastSeen";      // [Aemy] epoch phut - presence ban be
        // Dẫn xuất cho leaderboard (int):
        public const string KDAx100       = "KDAx100";      // KDA * 100
        public const string HeadshotPct   = "HeadshotPct";  // 0..100
    }

    /// <summary>[Aemy 2026-09-17] Quy doi EXP <-> Level. Level L->L+1 can 1000xL XP.</summary>
    public static class LevelCurve
    {
        public static int XpForLevelUp(int level) { return 1000 * System.Math.Max(1, level); }
        // Tong XP de DAT level L (L>=1); Reach(1)=0.
        public static int XpToReach(int level)
        {
            if (level <= 1) return 0;
            int n = level - 1;
            return 1000 * n * (n + 1) / 2;
        }
        public static int LevelForXp(int totalXp, out int xpIntoLevel, out int xpForNext)
        {
            if (totalXp < 0) totalXp = 0;
            int level = 1;
            while (totalXp >= XpToReach(level + 1)) level++;
            xpIntoLevel = totalXp - XpToReach(level);
            xpForNext = XpForLevelUp(level);
            return level;
        }
        public static int LevelForXp(int totalXp) { int a, b; return LevelForXp(totalXp, out a, out b); }
    }
}
