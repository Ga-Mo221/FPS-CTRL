// ============================================================
//  TrueInputSystem — TISFav_GetVector2Action.cs
//  Favorites Sub-Tab Module: GetVector2Action
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_GetVector2Action : IFavSubTabModule
    {
        public string Label => "GetVector2Action";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "GetVector2Action(name)",
                "Vector2",
                "Đọc Vector2 action theo tên — dành cho UI navigate, map scroll, RTS pan.",
                win.C_AccentBlue);

            win.H2("Cú Pháp");
            win.CodeBlock("Vector2 v2 = _input.GetVector2Action(\"ActionName\");");

            win.Sep();
            win.H2("Ví Dụ — UI Menu Navigate");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 nav = _input.GetVector2Action(\"Navigate\");\n" +
                "    if (Mathf.Abs(nav.y) > 0.5f)\n" +
                "        SelectMenuItem(nav.y > 0 ? -1 : 1);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — RTS Map Scroll");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 pan = _input.GetVector2Action(\"MapScroll\");\n" +
                "    Vector3 move = new Vector3(pan.x, 0, pan.y) * panSpeed * Time.deltaTime;\n" +
                "    transform.position += move;\n" +
                "}");

            win.Note("Profile cần action type = Vector2. Binding PC: <Keyboard>/wasd composite hoặc <Mouse>/delta. Gamepad: <Gamepad>/leftStick.");
        }
    }
}
#endif
