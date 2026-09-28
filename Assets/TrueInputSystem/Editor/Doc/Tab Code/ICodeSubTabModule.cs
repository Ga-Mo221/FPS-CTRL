// ============================================================
//  TrueInputSystem — ICodeSubTabModule.cs
//  Interface chung cho mọi module sub-tab trong tab "Code"
// ============================================================
//
//  CÁCH THÊM SUB-TAB MỚI:
//  1. Tạo file mới:  TIS_DocWindow_Code_<TenModule>.cs
//  2. Khai báo class kế thừa ICodeSubTabModule, điền Label + Draw(win)
//  3. Đăng ký vào TIS_DocWindow_Tab_Code.cs:
//       _codeModules.Add(new TISCode_<TenModule>());
//  Không cần chỉnh switch/case hay mảng label — registry tự cập nhật.
// ============================================================

#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    /// <summary>
    /// Interface mỗi module sub-tab phải implement.
    /// </summary>
    public interface ICodeSubTabModule
    {
        /// <summary>Tên hiển thị trong sidebar (tối đa ~14 ký tự).</summary>
        string Label { get; }

        /// <summary>
        /// Vẽ toàn bộ nội dung của sub-tab này.
        /// Nhận instance <see cref="TISDocWindow"/> để gọi các draw helper
        /// (H2, Body, CodeBlock, Sep, Tip, v.v.) vốn là internal method.
        /// </summary>
        void Draw(TISDocWindow win);
    }
}
#endif