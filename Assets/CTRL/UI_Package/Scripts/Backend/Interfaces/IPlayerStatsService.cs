using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>Thống kê chiến đấu (Player Statistics) + kill-theo-súng.</summary>
    public interface IPlayerStatsService
    {
        void GetMyStats(Action<CombatStats> onResult, Action<AuthError> onFailure);

        // Cộng dồn kết quả 1 trận vào thống kê tích luỵ + cập nhật stat dẫn xuất (KDA, headshot%).
        // [Aemy 2026-09-17] Them assists + expGained; tra ve CombatStats sau cong don (de UI hien Level/EXP moi).
        void ReportMatchResult(int kills, int deaths, int headshotKills, int assists, int expGained,
            Action<CombatStats> onSuccess, Action<AuthError> onFailure);

        // ↓ Lấy top N súng nhiều kill nhất (cho panel "SÚNG CHẦ LỰC").
        void GetTopWeapons(int count, Action<List<WeaponKillEntry>> onResult, Action<AuthError> onFailure);

        // ↑ Cộng dồn kill-theo-súng của 1 trận.
        void AddWeaponKills(Dictionary<string, int> killsThisMatch, Action onSuccess, Action<AuthError> onFailure);
    }
}
