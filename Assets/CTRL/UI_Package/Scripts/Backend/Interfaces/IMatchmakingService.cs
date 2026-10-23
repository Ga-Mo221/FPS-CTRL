using System;
using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Ghép trận qua PlayFab Matchmaking (queue theo mode, ép bắt đầu sau giveUp).
    /// Thuần PlayFab — không phụ thuộc Fusion. Việc bàn giao vào phòng do IMatchHandoff lo.
    /// </summary>
    public interface IMatchmakingService
    {
        bool IsSearching { get; }

        void StartMatchmaking(
            MatchmakingRequest request,
            Action<MatchFoundResult> onMatchFound,   // PlayFab ghép được trận
            Action<MatchmakingProgress> onProgress,  // cập nhật định kỳ cho UI
            Action<AuthError> onError,               // lỗi cứng
            Action onTimedOut,                        // hết giveUp mà chưa đủ người -> caller quyết ép bắt đầu
            Action onCancelled);                      // người chơi tự hủy

        void CancelMatchmaking(Action onDone = null);

        // [UI-INTEGRATION 2026-09-13] Lay roster da ghep (ten/avatar/doi) de hien lobby truoc khi vao tran.
        void GetMatchMembers(string matchId, string queueName,
            Action<System.Collections.Generic.List<LobbyMember>> onDone,
            Action<AuthError> onError);
    }
}
