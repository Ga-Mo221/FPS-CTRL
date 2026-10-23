using Backend.Models;

namespace Backend.Interfaces
{
    /// <summary>
    /// Cầu nối "PlayFab match -> phòng chơi thực tế (Fusion)".
    /// Trong sandbox UI: LoggingMatchHandoff (chỉ log).
    /// Trong FPS-CTRL sau này: FusionMatchHandoff gọi NetworkRunnerHandler với SessionName = MatchId.
    /// Nhờ interface này, ráp Fusion về sau chỉ là viết 1 class, không sửa service/UI.
    /// </summary>
    public interface IMatchHandoff
    {
        // Vào đúng phòng của match (SessionName = match.MatchId, lobby = mode).
        void HandOff(MatchFoundResult match);

        // Hết 2 phút không đủ người -> tạo phòng solo/warm-up cho mode đó.
        void HandOffSolo(MatchMode mode);
    }
}
