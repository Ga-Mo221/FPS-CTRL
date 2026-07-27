// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.MobileSkillButton.cs
//  Tab API Reference — module: TISMobileSkillButton
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_MobileSkillButton()
        {
            // ── TISMobileSkillButton ───────────────────────────────────────
            H2("TISMobileSkillButton — Properties & Methods");
            ApiHeader();
            ApiRow("SkillName",              "string", "Tên action bind (get only). Khớp với actionName trong profile.", "purple");
            ApiRow("HoldTime",               "float",  "Thời gian đã giữ nút (giây). Reset về 0 khi nhả.", "amber");
            ApiRow("Interactable",           "bool",   "get/set — Ưu tiên cao nhất. false: không nhận touch, visual mờ.", "purple");
            ApiRow("SetDisabled(bool)",      "void",   "Block input tạm thời + visual tối (stun, skill locked). Tự release nếu đang nhấn.", "red");
            ApiRow("SetCooldownFill(f)",     "void",   "0=sẵn sàng, 1=full cooldown. Block input + cập nhật Image fill.", "red");
            ApiRow("SetVisualMode(mode)",    "void",   "Đổi ColorTint / SpriteSwap lúc runtime.", "red");
            ApiHeader("Event", "Loại", "Khi nào fire");
            ApiRow("OnPress",      "UnityEvent",       "Fired 1 lần khi PointerDown (bắt đầu nhấn).", "purple");
            ApiRow("OnRelease",    "UnityEvent",       "Fired khi PointerUp hoặc PointerExit (nhả / trượt ra ngoài).", "purple");
            ApiRow("OnHold",       "UnityEvent",       "Fired MỖI FRAME khi đang giữ nút. Dùng cho auto-attack, charge.", "purple");
            ApiRow("OnHoldTimed",  "UnityEvent<float>","Fired mỗi frame khi giữ, param = HoldTime (giây). Dùng cho charge bar.", "purple");

        }

    }  // partial class TISDocWindow
}
#endif
