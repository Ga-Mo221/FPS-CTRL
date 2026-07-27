// ============================================================
//  TrueInputSystem — TISFav_SetTrackerCamera.cs
//  Favorites Sub-Tab Module: SetTrackerCamera
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_SetTrackerCamera : IFavSubTabModule
    {
        public string Label => "SetTrackerCamera";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "SetTrackerCamera(Camera)",
                "void",
                "Đổi Camera dùng để raycast cho Object Tracker lúc runtime.",
                win.C_AccentRed);

            win.H2("Cú Pháp");
            win.CodeBlock("_input.SetTrackerCamera(Camera cam);");

            win.Sep();
            win.H2("Ví Dụ — Đổi Camera Khi Spectate");
            win.CodeBlock(
                "void OnEnterSpectate(Camera spectatorCam)\n" +
                "{\n" +
                "    _input.SetTrackerCamera(spectatorCam);\n" +
                "}\n\n" +
                "void OnExitSpectate(Camera playerCam)\n" +
                "{\n" +
                "    _input.SetTrackerCamera(playerCam);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Setup Lúc Start");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _input = TISInputBridge.Instance;\n" +
                "    _input.SetTrackerCamera(Camera.main);\n" +
                "    _input.SetObjectTrackerEnabled(true);\n" +
                "}");
        }
    }
}
#endif
