// ============================================================
//  TrueInputSystem — IFavSubTabModule.cs
//  Interface chung cho mọi module sub-tab trong tab "Favorites"
// ============================================================
//
//  CÁCH THÊM API MỚI VÀO TAB FAVORITES:
//  1. Tạo file mới:  TISFav_<TenAPI>.cs
//  2. Khai báo class kế thừa IFavSubTabModule, điền Label + Draw(win)
//  3. Đăng ký vào TIS_DocWindow_Tab_Favorites.cs:
//       _favModules.Add(new TISFav_<TenAPI>());
//  Không cần chỉnh switch/case hay mảng label — registry tự cập nhật.
// ============================================================

#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    /// <summary>
    /// Interface mỗi module Favorites sub-tab phải implement.
    /// </summary>
    public interface IFavSubTabModule
    {
        /// <summary>Tên hiển thị trong sidebar (tối đa ~14 ký tự).</summary>
        string Label { get; }

        /// <summary>
        /// Vẽ toàn bộ nội dung của sub-tab này.
        /// Nhận instance <see cref="TISDocWindow"/> để gọi các draw helper
        /// (H2, Body, CodeBlock, Sep, Tip, Warn, Note, PageTitle, v.v.)
        /// vốn là internal method của TISDocWindow.
        /// Dùng win.FavApiBox(...) cho khung header API signature.
        /// </summary>
        void Draw(TISDocWindow win);
    }
}
#endif
