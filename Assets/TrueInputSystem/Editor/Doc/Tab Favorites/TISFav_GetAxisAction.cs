// ============================================================
//  TrueInputSystem — TISFav_GetAxisAction.cs
//  Favorites Sub-Tab Module: GetAxisAction
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_GetAxisAction : IFavSubTabModule
    {
        public string Label => "GetAxisAction";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "GetAxisAction(name)",
                "float",
                "Đọc float analog — trigger, throttle, brake, zoom. Có apply deadzone & trigger curve.",
                win.C_AccentAmber);

            win.H2("Cú Pháp");
            win.CodeBlock("float value = _input.GetAxisAction(\"ActionName\");");

            win.Sep();
            win.H2("Ví Dụ — Xe: Gas & Brake");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float gas   = _input.GetAxisAction(\"Accelerate\"); // <Gamepad>/rightTrigger\n" +
                "    float brake = _input.GetAxisAction(\"Brake\");      // <Gamepad>/leftTrigger\n" +
                "    float horn  = _input.GetSkillAction(\"Horn\") ? 1f : 0f;\n\n" +
                "    ApplyThrottle(gas);\n" +
                "    ApplyBrake(brake);\n" +
                "    if (horn > 0f) PlayHorn();\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Zoom Camera Qua Axis");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float zoom = _input.GetAxisAction(\"CameraZoom\"); // <Mouse>/scroll/y\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * 0.5f, minDist, maxDist);\n" +
                "}");

            win.Note("Profile: action type = Axis. Binding PC: <Mouse>/scroll/y hoặc <Keyboard>/e (sẽ return 0 hoặc 1). Gamepad: <Gamepad>/rightTrigger.");
        }
    }
}
#endif
