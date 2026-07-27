// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.GamepadUIButton.cs
//  Tab API Reference — module: TISGamepadUIButton
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_GamepadUIButton()
        {
            GUILayout.Space(8);
            // ── TISGamepadUIButton ─────────────────────────────────────────
            H2("TISGamepadUIButton — Properties & Methods");
            Note("Kế thừa từ UnityEngine.UI.Button. Dùng cho UI navigation gamepad nhiều lớp (Tab → SubTab → Content). Thêm component vào Button GameObject, cấu hình trong TIS_GamepadUIButtonEditor.");
            ApiHeader();
            ApiRow("navigationMode",            "NavMode",          "FourDirectional / Horizontal / Vertical — chế độ di chuyển D-pad / stick.", "purple");
            ApiRow("inputSource",               "InputSource",      "Default (DPad+Stick) / LB_RB / LT_RT — nguồn input cho layer này.", "purple");
            ApiRow("layer",                     "NavigationLayer",  "Tab / SubTab / Content — xác định button thuộc layer nào trong hệ thống phân cấp.", "purple");
            ApiRow("holdDelay",                 "float",            "Thời gian (giây) giữa các lần di chuyển khi giữ nút (mặc định 0.2f).", "amber");
            ApiRow("navUp / navDown / navLeft / navRight", "Button", "4 button láng giềng — gán trong Inspector để tạo luồng navigation.", "purple");
            ApiRow("alwaysActive",              "bool",             "Khi true: giữ visual Highlighted dù EventSystem focus ở nơi khác. Dùng cho Tab và SubTab layer.", "green");
            ApiRow("isFirstSelected",           "bool",             "Khi true: tự nhận EventSystem focus sau 1 frame khi GameObject được kích hoạt. Chỉ dùng với Content layer.", "green");
            ApiRow("SetAsActiveInLayer()",      "void",             "Đăng ký button này là 'active' trong layer — deselect button cũ, fire onDeselect / onSelect. Gọi khi khởi tạo scene.", "red");
            ApiRow("DisableNavigation()",       "void",             "Tắt navigation — button không nhận input, không di chuyển focus. Gọi khi mở panel đè lên.", "red");
            ApiRow("EnableNavigation()",        "void",             "Bật lại navigation sau khi panel đóng.", "red");
            ApiRow("GetActiveInLayer(layer)",   "TISGamepadUIButton","static — Lấy button đang 'active' trong layer chỉ định. null nếu chưa có.", "green");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("onSelect",              "SelectEvent (UnityEvent)",                        "Fire khi button được select (EventSystem focus hoặc SetAsActiveInLayer).", "purple");
            ApiRow("onDeselect",            "SelectEvent (UnityEvent)",                        "Fire khi button bị deselect.", "purple");
            ApiRow("onBecameActiveInLayer", "LayerChangedEvent (UnityEvent<TISGamepadUIButton>)", "Fire khi button trở thành active trong layer qua global input (LT/RT hoặc LB/RB). UINavigationContext lắng nghe để redirect Content focus.", "purple");

            GUILayout.Space(8);
            H2("TISGamepadUIButton — Enums");
            CodeBlock(
                "// NavMode — chế độ điều hướng:\n" +
                "FourDirectional  // D-pad 4 hướng (mặc định)\n" +
                "Horizontal       // Chỉ trái/phải\n" +
                "Vertical         // Chỉ lên/xuống\n\n" +
                "// InputSource — nguồn input:\n" +
                "Default          // DPad + LeftStick (Content layer)\n" +
                "LB_RB            // Left/Right Shoulder (SubTab layer)\n" +
                "LT_RT            // Left/Right Trigger (Tab layer)\n\n" +
                "// NavigationLayer — thứ bậc:\n" +
                "Tab              // Lớp ngoài cùng — LT/RT, alwaysActive\n" +
                "SubTab           // Lớp giữa — LB/RB, alwaysActive\n" +
                "Content          // Lớp trong — DPad/Stick, EventSystem focus thật");

        }

    }  // partial class TISDocWindow
}
#endif
