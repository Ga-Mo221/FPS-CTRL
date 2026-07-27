// ============================================================
//  TrueInputSystem — TISFav_LookDelta.cs
//  Favorites Sub-Tab Module: LookDelta
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_LookDelta : IFavSubTabModule
    {
        public string Label => "LookDelta";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "LookDelta",
                "Vector2",
                "Delta xoay camera frame này — chuột / right stick / kéo ngón. Tiêu thụ mỗi frame.",
                win.C_AccentBlue);

            win.H2("Cú Pháp");
            win.CodeBlock("Vector2 look = TISInputBridge.Instance.LookDelta;");

            win.Sep();
            win.H2("Ví Dụ — Xoay Yaw & Pitch Thủ Công");
            win.CodeBlock(
                "private float _yaw, _pitch;\n" +
                "private float sensitivityH = 2f, sensitivityV = 1.5f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    Vector2 look = _input.LookDelta;\n" +
                "    _yaw   += look.x * sensitivityH;\n" +
                "    _pitch -= look.y * sensitivityV;\n" +
                "    _pitch  = Mathf.Clamp(_pitch, -80f, 80f);\n\n" +
                "    transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Override LookDelta Từ Swipe Zone");
            win.CodeBlock(
                "// TISTouchCameraZone tự gọi nội bộ:\n" +
                "// _input.OverrideLookInput(swipeDelta);\n" +
                "// _input.ClearLookOverride();\n\n" +
                "// Nếu bạn muốn inject thủ công:\n" +
                "_input.OverrideLookInput(new Vector2(dx, dy));\n" +
                "// Khi nhả ngón:\n" +
                "_input.ClearLookOverride();");
        }
    }
}
#endif
