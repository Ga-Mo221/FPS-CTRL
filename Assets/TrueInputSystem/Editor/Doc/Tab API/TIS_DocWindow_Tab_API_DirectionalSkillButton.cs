// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.DirectionalSkillButton.cs
//  Tab API Reference — module: TISDirectionalSkillButton
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_DirectionalSkillButton()
        {
            // ── TISDirectionalSkillButton ──────────────────────────────────
            H2("TISDirectionalSkillButton — Properties & Methods");
            ApiHeader();
            ApiRow("WasCastThisFrame",   "bool",    "true đúng 1 frame khi skill vừa được cast.", "green");
            ApiRow("CastDirection",      "Vector2", "Hướng cast (normalized). zero = quick tap / auto-target.", "blue");
            ApiRow("DirectionAngle",     "float",   "Góc hướng cast (0°=phải, 90°=trên).", "amber");
            ApiRow("Direction",          "Vector2", "Direction real-time khi đang kéo (normalized). zero khi nhả.", "blue");
            ApiRow("IsDragging",         "bool",    "true khi đang kéo ngón tay (đã vượt dragThreshold).", "green");
            ApiRow("IsInCancelZone",     "bool",    "true khi ngón tay đang hover trong vùng hủy.", "green");
            ApiRow("IsDisabled",         "bool",    "true nếu button bị disable (external hoặc cooldown).", "green");
            ApiRow("SetDisabled(bool)",  "void",    "Bật/tắt disable từ game code. Tự release nếu đang hold.", "red");
            ApiRow("SetCooldownFill(f)", "void",    "0=sẵn sàng, 1=full cooldown. Tự release nếu đang hold.", "red");
            ApiRow("ForceCast(dir)",     "void",    "Force cast bằng code không cần touch (tutorial, automation).", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnDirectionCast", "event Action<string, Vector2>", "Fire khi cast thành công. Params: (skillName, direction). zero = auto-target.", "purple");
            ApiRow("OnCancelled",     "UnityEvent",                    "Fire khi nhả trong Cancel Zone — kỹ năng bị hủy.", "purple");
            ApiRow("OnDragStarted",   "UnityEvent",                    "Fire khi bắt đầu kéo vượt dragThreshold.", "purple");

        }

    }  // partial class TISDocWindow
}
#endif
