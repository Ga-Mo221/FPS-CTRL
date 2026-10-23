using Backend.Interfaces;
using Backend.Models;
using UnityEngine;

namespace Backend.Handoff
{
    /// <summary>
    /// Handoff mặc định cho sandbox UI (chưa có Fusion). Chỉ log để kiểm tra luồng.
    /// Khi gộp vào FPS-CTRL, thay bằng FusionMatchHandoff.
    /// </summary>
    public class LoggingMatchHandoff : IMatchHandoff
    {
        public void HandOff(MatchFoundResult match)
        {
            Debug.Log($"<color=#22d3ee>[Handoff]</color> VÀO PHÒNG Fusion (giả lập): SessionName='{match.MatchId}', lobby='{match.Mode}'. " +
                      $"(FPS-CTRL sẽ gọi NetworkRunnerHandler ở đây.)");
        }

        public void HandOffSolo(MatchMode mode)
        {
            Debug.Log($"<color=#22d3ee>[Handoff]</color> ÉP BẮT ĐẦU solo/warm-up (giả lập) cho mode '{mode}'. " +
                      $"(FPS-CTRL sẽ tạo session solo ở đây.)");
        }
    }
}
