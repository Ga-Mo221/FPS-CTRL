// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_SetupTool.cs
//  Tab: Editor Setup Tool
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 6 — SETUP TOOL
        // ══════════════════════════════════════════════════════

        private void DrawTab_SetupTool()
        {
            BeginContentScroll("setuptool");
            PageTitle("Editor Setup Tool", "Giao diện trực quan để tạo và chỉnh sửa TISInputProfileConfig.\nMở bằng: Tools → True Input System → Setup");

            H2("Bố Cục Cửa Sổ");
            // CodeBlock(
            //     "┌─ Toolbar ──────────────────────────────────────────────────────────┐\n" +
            //     "│ Mới | Mở .asset | ✔ Validate | ⬆ Generate | ▶ Gán | Name | ? │\n" +
            //     "├─ Panel Trái ───┬─── Panel Giữa ─────────────────────┬─ Panel Phải ─┤\n" +
            //     "│ Action Maps    │ Binding Editor                      │ Settings     │\n" +
            //     "│                │ (Name | PC | +Mod | GP | MB | ⋮)  │ Mobile       │\n" +
            //     "│ + Thêm Map    │                                     │ Camera       │\n" +
            //     "│ - Xoá         │ + Thêm Action                      │ Gamepad      │\n" +
            //     "├────────────────┴─────────────────────────────────────┴──────────────┤\n" +
            //     "│ Status Bar: X map · Y action · ● Chưa lưu / ✔ Đã lưu              │\n" +
            //     "└────────────────────────────────────────────────────────────────────┘", "Layout");

            Sep();
            H2("Toolbar — Chức Năng");
            ApiHeader("Nút", "Phím tắt", "Chức năng");
            ApiRow("Mới",         "—",       "Tạo working copy từ đầu (hỏi nếu có thay đổi chưa lưu)", "blue");
            ApiRow("Mở .asset",  "—",       "Load file .asset có sẵn vào tool", "blue");
            ApiRow("✔ Validate",  "—",       "Kiểm tra lỗi: tên trùng, binding conflict...", "green");
            ApiRow("⬆ Generate",  "Ctrl+S",  "Lưu vào file .asset (tạo mới hoặc ghi đè)", "amber");
            ApiRow("▶ Gán Scene", "—",       "Tự tìm TISInputBridge trong scene và assign profile", "purple");
            ApiRow("?",           "—",       "Mở cửa sổ Documentation này", "red");

            Sep();
            H2("Quy Trình Làm Việc");
            Step(1, "Mở Setup Tool",    "Tools → True Input System → Setup");
            Step(2, "Cấu hình Action Map", "Thêm map, thêm action, chọn binding cho 3 platform.");
            Step(3, "Chỉnh Settings",   "Panel phải → Mobile / Camera / Gamepad theo nhu cầu.");
            Step(4, "Validate",         "✔ Validate — sửa hết lỗi đỏ ở cuối panel giữa.");
            Step(5, "Generate",         "⬆ Generate → chọn nơi lưu → file .asset tạo ra.");
            Step(6, "Gán vào Scene",    "▶ Gán vào Scene → tự tìm TISInputBridge và assign.");

            Tip("Lưu thường xuyên. Dấu ● màu cam trên Status Bar nhắc khi có thay đổi chưa lưu. Ctrl+Z để hoàn tác.");

            Sep();
            H2("Panel Trái — Action Map List");
            CodeBlock(
                "// Click         — chọn map, panel giữa cập nhật\n" +
                "// Double-click  — đổi tên map inline\n" +
                "// Right-click   — context menu: Đổi tên / Nhân đôi / Bật-Tắt / Xoá\n" +
                "// + Thêm Map   — thêm map mới với tên tự động không trùng\n" +
                "// - (nút)      — xoá map đang chọn (yêu cầu xác nhận)", "Controls");

            Note("Profile phải có ít nhất 1 map — nút Xoá từ chối nếu chỉ còn 1 map.");

            EndContentScroll();
        }

    }  // partial class TISDocWindow
}
#endif
