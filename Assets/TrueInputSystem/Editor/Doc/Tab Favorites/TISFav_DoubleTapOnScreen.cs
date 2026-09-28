// ============================================================
//  TrueInputSystem — TISFav_DoubleTapOnScreen.cs
//  Favorites Sub-Tab Module: DoubleTapOnScreen
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_DoubleTapOnScreen : IFavSubTabModule
    {
        public string Label => "DoubleTapOnScreen";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherIsDoubleTapOnScreen()",
                "bool",
                "true đúng 1 frame khi double-tap bất kỳ vị trí màn hình.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("bool doubleTap = _input.ToucherIsDoubleTapOnScreen();");

            win.Sep();
            win.H2("Ví Dụ — Double-Tap Để Dash");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        DoDash();\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Double-Tap Để Mở Map");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        mapPanel.SetActive(!mapPanel.activeSelf);\n" +
                "}");

            win.Tip("Khác với ToucherIsDoubleTapOnObject: method này không raycast, chỉ kiểm tra gesture trên toàn màn hình.");
        }
    }
}
#endif
