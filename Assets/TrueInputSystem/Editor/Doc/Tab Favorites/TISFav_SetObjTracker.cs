// ============================================================
//  TrueInputSystem — TISFav_SetObjTracker.cs
//  Favorites Sub-Tab Module: SetObjTracker
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_SetObjTracker : IFavSubTabModule
    {
        public string Label => "SetObjTracker";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "SetObjectTrackerEnabled(bool)",
                "void",
                "Bật/tắt Object Tracker lúc runtime. Tắt để tiết kiệm tài nguyên raycast mỗi frame.",
                win.C_AccentRed);

            win.H2("Cú Pháp");
            win.CodeBlock("_input.SetObjectTrackerEnabled(bool enabled);");

            win.Sep();
            win.H2("Ví Dụ — Tắt Khi Không Cần");
            win.CodeBlock(
                "// Bật khi mở puzzle / selection mode:\n" +
                "void EnterSelectionMode()\n" +
                "{\n" +
                "    _input.SetObjectTrackerEnabled(true);\n" +
                "}\n\n" +
                "// Tắt khi thoát:\n" +
                "void ExitSelectionMode()\n" +
                "{\n" +
                "    _input.ToucherClearAll();\n" +
                "    _input.SetObjectTrackerEnabled(false);\n" +
                "}");

            win.Tip("Object Tracker thực hiện Raycast mỗi frame khi bật. Chỉ bật khi cần touch picking để tối ưu performance.");
        }
    }
}
#endif
