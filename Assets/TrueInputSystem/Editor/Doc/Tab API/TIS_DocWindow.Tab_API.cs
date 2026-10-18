// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.cs
//  Tab: API Reference — Orchestrator
//  Chỉ điều phối thứ tự. Nội dung từng class nằm ở các file
//  TIS_DocWindow_Tab_API_<TenClass>.cs riêng biệt (partial class).
//  Thêm class mới: tạo file mới theo mẫu rồi gọi thêm 1 dòng
//  Draw_API_XXX(); bên dưới.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 8 — API REFERENCE
        // ══════════════════════════════════════════════════════

        private void DrawTab_API()
        {
            BeginContentScroll("api");
            PageTitle("API Reference", "Toàn bộ public API — Properties, Methods, Events, Enums.");

            Draw_API_InputBridge();
            Draw_API_Motion();
            Draw_API_RebindingSystem();
            Draw_API_MobileSkillButton();
            Draw_API_DirectionalSkillButton();
            Draw_API_DirectionalRegistry();
            Draw_API_TargetScanner();
            Draw_API_MobileInputSetup();
            Draw_API_GamepadVirtualCursor();
            Draw_API_GamepadUIButton();
            Draw_API_MobileButtonRegistry();
            Draw_API_Enums();
            Draw_API_QuickExample();

            EndContentScroll();
        }

    }  // partial class TISDocWindow
}
#endif