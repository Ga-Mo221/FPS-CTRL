// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.MobileButtonRegistry.cs
//  Tab API Reference — module: TISMobileButtonRegistry
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_MobileButtonRegistry()
        {
            GUILayout.Space(8);
            // ── TISMobileButtonRegistry ────────────────────────────────────
            H2("TISMobileButtonRegistry — Static");
            ApiHeader();
            ApiRow("RegisteredKeys",             "IReadOnlyList", "Danh sách tên tất cả button đã đăng ký.", "purple");
            ApiRow("SetState(name, bool)",        "void",   "Inject trạng thái button. Tự đăng ký key lần đầu.", "red");
            ApiRow("GetState(name)",              "bool",   "Đọc trạng thái Hold hiện tại.", "green");
            ApiRow("ConsumeState(name)",          "bool",   "Đọc + reset (true 1 frame — GetKeyDown equivalent).", "green");
            ApiRow("WasPressedThisFrame(name)",   "bool",   "true đúng 1 frame khi nhấn. Không mutate _states.", "green");
            ApiRow("WasReleasedThisFrame(name)",  "bool",   "true đúng 1 frame khi nhả ngón tay.", "green");
            ApiRow("AnyPressed()",                "bool",   "true nếu có bất kỳ button nào đang pressed (debug).", "green");
            ApiRow("FlushFrame()",                "void",   "Snapshot _states→_prevStates cuối frame. Gọi bởi LateUpdate.", "red");
            ApiRow("ClearAll()",                  "void",   "Reset toàn bộ registry (SwapProfile, scene change).", "red");

        }

    }  // partial class TISDocWindow
}
#endif
