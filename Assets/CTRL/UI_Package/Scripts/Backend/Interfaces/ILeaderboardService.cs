using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>Bảng xếp hạng theo một statistic (K/D/A, headshot%...).</summary>
    public interface ILeaderboardService
    {
        void GetLeaderboard(string statisticName, int maxCount,
            Action<List<LeaderboardEntryInfo>> onResult, Action<AuthError> onFailure);
    }
}
