// ============================================================
//  TrueInputSystem — TISFav_TEMPLATE.cs
//  HƯỚNG DẪN TẠO MODULE FAVORITES MỚI
// ============================================================
//
//  BƯỚC 1: Copy file này, đổi tên thành  TISFav_<TenAPI>.cs
//  BƯỚC 2: Thay "TEMPLATE" → tên class thực, điền Label + Draw
//  BƯỚC 3: Đăng ký vào TIS_DocWindow_Tab_Favorites.cs:
//            _favModules.Add(new TISFav_<TenAPI>());
//
//  MÀU ACCENT GỢI Ý:
//    win.C_AccentBlue   — Vector / Direction / Axis input
//    win.C_AccentGreen  — Boolean / Button input
//    win.C_AccentAmber  — Float / Trigger / Timing
//    win.C_AccentRed    — Destructor / Clear / Disable
//
//  HELPERS CÓ SẴN (gọi qua win.XXX):
//    win.FavApiBox(class, member, returnType, desc, color)
//    win.H2(text)         — heading section
//    win.Body(text)       — paragraph text
//    win.CodeBlock(code)  — khung code mono
//    win.Sep()            — đường kẻ ngang
//    win.Tip(text)        — box xanh lá — best practice
//    win.Note(text)       — box xanh dương — thông tin thêm
//    win.Warn(text)       — box vàng — cảnh báo
//    win.PageTitle(t, s)  — tiêu đề trang + subtitle
// ============================================================

#if UNITY_EDITOR
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public class TISFav_TEMPLATE : IFavSubTabModule
    {
        // Hiển thị trong sidebar (tối đa ~14 ký tự)
        public string Label => "MyApiName";

        public void Draw(TISDocWindow win)
        {
            // ── Header: khung signature API ────────────────
            win.FavApiBox(
                "TISInputBridge",           // Tên class chứa API
                "MyApiName(param)",         // Tên member / signature
                "ReturnType",               // Kiểu trả về (void, bool, Vector2...)
                "Mô tả ngắn — một dòng.",   // Mô tả
                win.C_AccentBlue);            // Màu accent

            // ── Cú pháp cơ bản ─────────────────────────────
            win.H2("Cú Pháp");
            win.CodeBlock(
                "// Ví dụ cú pháp cơ bản\n" +
                "var result = _input.MyApiName();");

            win.Sep();

            // ── Ví dụ 1 ────────────────────────────────────
            win.H2("Ví Dụ — Tình Huống Thường Gặp");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    // code ví dụ ở đây\n" +
                "}");

            win.Sep();

            // ── Ví dụ 2 ────────────────────────────────────
            win.H2("Ví Dụ — Tình Huống Nâng Cao");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    // code ví dụ nâng cao\n" +
                "}");

            // ── Ghi chú cuối ───────────────────────────────
            win.Tip("Best practice hoặc lưu ý quan trọng nhất.");
            win.Note("Thông tin thêm về edge case hoặc dependencies.");
            // win.Warn("Cảnh báo nếu có gotcha cần chú ý.");
        }
    }
}
#endif
