// ============================================================
//  TrueInputSystem — TISFav_ToucherClear.cs
//  Favorites Sub-Tab Module: ToucherClear
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_ToucherClear : IFavSubTabModule
    {
        public string Label => "ToucherClear";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherClearList / ToucherClearAll / ToucherClearTouchCount",
                "void",
                "Reset trạng thái Object Tracker — dùng khi chuyển scene hoặc sau khi xử lý gesture.",
                win.C_AccentRed);

            win.H2("Cú Pháp — 3 Method");
            win.CodeBlock(
                "// Xóa danh sách touched objects (giữ lại bộ đếm & double-tap):\n" +
                "_input.ToucherClearList();\n\n" +
                "// Xóa toàn bộ trạng thái tracker (list + double-tap state):\n" +
                "_input.ToucherClearAll();\n\n" +
                "// Reset bộ đếm touch nội bộ về 0 (dùng sau khi xử lý xong gesture):\n" +
                "_input.ToucherClearTouchCount();");

            win.Sep();
            win.H2("Ví Dụ — Chuyển Scene");
            win.CodeBlock(
                "void OnSceneUnload(Scene s, LoadSceneMode m)\n" +
                "{\n" +
                "    _input.ToucherClearAll();   // reset sạch trước khi load scene mới\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Sau Khi Xử Lý Gesture");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureIsTwoFingerPanning)\n" +
                "    {\n" +
                "        // ... xử lý pan ...\n" +
                "    }\n" +
                "    else if (_input.ToucherCount > 0)\n" +
                "    {\n" +
                "        ProcessTouchedObjects();\n" +
                "        _input.ToucherClearTouchCount(); // reset sau khi xử lý xong\n" +
                "    }\n" +
                "}");
        }
    }
}
#endif
