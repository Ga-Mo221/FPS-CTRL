// ============================================================
//  TrueInputSystem — TISFav_ToucherGetLastID.cs
//  Favorites Sub-Tab Module: ToucherGetLastID
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_ToucherGetLastID : IFavSubTabModule
    {
        public string Label => "ToucherGetLastID";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherGetLastID()",
                "int",
                "InstanceID của GameObject được touch trúng lần cuối cùng. 0 nếu chưa có.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock(
                "int lastID = _input.ToucherGetLastID();\n\n" +
                "// Tên để debug:\n" +
                "string name = _input.ToucherGetLastName();");

            win.Sep();
            win.H2("Ví Dụ — Xử Lý Object Được Touch Cuối");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    int id = _input.ToucherGetLastID();\n" +
                "    if (id == 0) return;   // chưa touch trúng gì\n\n" +
                "    var go = EditorUtility.InstanceIDToObject(id) as GameObject;\n" +
                "    // hoặc dùng ObjectRegistry của bạn để tra cứu:\n" +
                "    if (_registry.TryGetValue(id, out var enemy))\n" +
                "        HighlightEnemy(enemy);\n" +
                "}");
        }
    }
}
#endif
