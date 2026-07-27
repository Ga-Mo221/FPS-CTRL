// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.RebindingSystem.cs
//  Tab API Reference — module: TISRebindingSystem
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_RebindingSystem()
        {
            // ── TISRebindingSystem ─────────────────────────────────────────
            H2("TISRebindingSystem — Static (JSON Persistence)");
            ApiHeader();
            ApiRow("State",                          "RebindState", "Trạng thái phiên rebinding: Idle / Listening / Done.", "purple");
            ApiRow("StartRebinding(bridge,name,tgt,cb,cancelPath)", "void", "Bắt đầu lắng nghe phím mới từ người dùng. cancelPath default = <Keyboard>/escape.", "red");
            ApiRow("CancelRebinding()",              "void",    "Huỷ phiên rebinding đang chạy. Không lưu thay đổi.", "red");
            ApiRow("ForceApply(bridge,name,tgt,path)","bool",   "Apply binding bằng code, không cần nhấn phím. true nếu thành công.", "red");
            ApiRow("ApplyPreset(bridge,dict,tgt,cb)", "int",    "Apply nhiều binding cùng lúc từ Dictionary. Chỉ rebuild một lần. Trả về số applied.", "red");
            ApiRow("GetActivePath(bridge,name,tgt)",  "string", "Lấy path đang active (override nếu có, default nếu không). Dùng hiện lên UI.", "green");
            ApiRow("GetDisplayName(path)",            "string", "Chuyển path sang tên thân thiện. VD: <Keyboard>/space → Space.", "green");
            ApiRow("FindConflict(bridge,skip,tgt,path)","string","Kiểm tra path có bị action khác dùng không. null nếu không conflict.", "green");
            ApiRow("LoadAndApply(bridge)",            "void",   "Load JSON và apply lên bridge. Gọi trong Awake() sau khi bridge khởi tạo.", "red");
            ApiRow("ResetBinding(bridge,name,tgt)",   "void",   "Reset 1 binding về default. Bridge tự rebuild.", "red");
            ApiRow("ResetAllBindings(bridge)",         "void",   "Reset toàn bộ override của profile hiện tại.", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnRebindStarted",   "event Action<string,RebindTarget>", "Fire khi StartRebinding() bắt đầu lắng nghe. Dùng hiện overlay 'Press any key...'.", "purple");
            ApiRow("OnRebindCompleted", "event Action<RebindResult>",        "Fire khi rebind kết thúc (success / cancel / fail). RebindResult: ActionName, NewPath, OldPath, Success, Cancelled.", "purple");

        }

    }  // partial class TISDocWindow
}
#endif
