namespace Backend.Models
{
    /// <summary>Chế độ ghép trận. Tách khỏi GamePlayMode của game (map ở lớp cầu nối Fusion).</summary>
    public enum MatchMode
    {
        FFA, // Free-for-all (Deathmatch solo)
        TDM  // Team Deathmatch
    }

    public class MatchmakingRequest
    {
        public MatchMode Mode;
    }

    /// <summary>Tiến trình tìm trận, bắn định kỳ cho UI.</summary>
    public class MatchmakingProgress
    {
        public float ElapsedSeconds;
        public int MaxSeconds;      // = GiveUpAfterSeconds (mốc ép bắt đầu)
        public string Status;       // "WaitingForPlayers" / "WaitingForMatch" / "Matched" / "Canceled"...
    }

    /// <summary>Kết quả khi PlayFab ghép được trận. MatchId dùng làm SessionName của Fusion.</summary>
    public class MatchFoundResult
    {
        public string MatchId;
        public MatchMode Mode;
        public string QueueName;
    }

    /// <summary>[UI-INTEGRATION 2026-09-13] 1 nguoi choi trong lobby (lay tu PlayFab GetMatch member attributes).</summary>
    public class LobbyMember
    {
        public string Name;
        public string AvatarUrl;
        public string TeamId;   // "" neu FFA
        public bool IsLocal;
    }

    /// <summary>Cấu hình queue + nhịp poll. Đổi được trong Inspector qua BackendBootstrap.</summary>
    public class MatchmakingConfig
    {
        public string FfaQueueName = "ffa_quick";
        public string TdmQueueName = "tdm_quick";
        public int GiveUpAfterSeconds = 120;   // ép bắt đầu sau 2 phút (theo GDD)
        public float PollIntervalSeconds = 6f; // PlayFab giới hạn poll tối thiểu ~6s

        public string QueueFor(MatchMode mode)
            => mode == MatchMode.TDM ? TdmQueueName : FfaQueueName;
    }
}
