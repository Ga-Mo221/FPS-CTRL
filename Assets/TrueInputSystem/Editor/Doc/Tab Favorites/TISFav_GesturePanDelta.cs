// ============================================================
//  TrueInputSystem — TISFav_GesturePanDelta.cs
//  Favorites Sub-Tab Module: GesturePanDelta
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_GesturePanDelta : IFavSubTabModule
    {
        public string Label => "GesturePanDelta";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "GestureTwoFingerPanDelta",
                "Vector2",
                "Delta midpoint 2 ngón frame này, đã nhân sensitivity — dùng cho pan 2 ngón.",
                win.C_AccentBlue);

            win.H2("Cú Pháp");
            win.CodeBlock(
                "// Delta đã nhân sensitivity:\n" +
                "Vector2 pan = _input.GestureTwoFingerPanDelta;\n\n" +
                "// Delta chưa nhân sensitivity (raw pixel):\n" +
                "Vector2 panRaw = _input.GestureTwoFingerPanRaw;\n\n" +
                "// Kiểm tra điều kiện trước:\n" +
                "bool isPanning = _input.GestureIsTwoFingerPanning;");

            win.Sep();
            win.H2("Ví Dụ — Pan Map / Top-Down Camera");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.GestureIsTwoFingerPanning) return;\n\n" +
                "    Vector2 pan = _input.GestureTwoFingerPanDelta;\n" +
                "    Vector3 move = new Vector3(-pan.x, 0, -pan.y) * panSensitivity * Time.deltaTime;\n" +
                "    transform.position += move;\n" +
                "}");

            win.Note("GestureIsTwoFingerPanning = true khi đúng 2 ngón trong camera zone đang di chuyển cùng hướng. Kết hợp với ZoomDelta để làm pinch-to-zoom đồng thời.");
        }
    }
}
#endif
