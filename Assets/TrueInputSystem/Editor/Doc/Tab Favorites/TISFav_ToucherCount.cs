// ============================================================
//  TrueInputSystem — TISFav_ToucherCount.cs
//  Favorites Sub-Tab Module: ToucherCount
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_ToucherCount : IFavSubTabModule
    {
        public string Label => "ToucherCount";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherCount",
                "int",
                "Số lượng touch đã ghi nhận từ Object Tracker. 0 nếu tracker bị tắt.",
                win.C_AccentAmber);

            win.H2("Cú Pháp");
            win.CodeBlock("int count = _input.ToucherCount;");

            win.Sep();
            win.H2("Ví Dụ — Hiển Thị Số Lần Chạm");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    touchCountLabel.text = $\"Đã chạm: {_input.ToucherCount} object(s)\";\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Kích Hoạt Khi Đã Chạm Đủ Object");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherCount >= requiredTouchCount)\n" +
                "        UnlockDoor();\n" +
                "}");

            win.Note("ToucherCount đếm tất cả touch ghi nhận từ đầu session, không reset tự động. Gọi ToucherClearAll() để reset về 0.");
        }
    }
}
#endif
