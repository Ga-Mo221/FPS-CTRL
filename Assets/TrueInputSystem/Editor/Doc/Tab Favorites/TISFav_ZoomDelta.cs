// ============================================================
//  TrueInputSystem — TISFav_ZoomDelta.cs
//  Favorites Sub-Tab Module: ZoomDelta
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_ZoomDelta : IFavSubTabModule
    {
        public string Label => "ZoomDelta";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ZoomDelta",
                "float",
                "Pinch zoom / scroll wheel frame này. Tự reset về 0 sau mỗi frame — chỉ đọc 1 lần.",
                win.C_AccentAmber);

            win.H2("Cú Pháp");
            win.CodeBlock("float zoom = TISInputBridge.Instance.ZoomDelta;");

            win.Sep();
            win.H2("Ví Dụ — Zoom Camera Thủ Công");
            win.CodeBlock(
                "private float _distance = 5f;\n" +
                "private float _minDist  = 1f, _maxDist = 15f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    float zoom = _input.ZoomDelta;          // đọc đúng 1 lần\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * zoomSpeed, _minDist, _maxDist);\n\n" +
                "    // Áp dụng khoảng cách:\n" +
                "    Vector3 dir = -transform.forward;\n" +
                "    cam.transform.position = transform.position + dir * _distance;\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Override ZoomDelta Từ Pinch Gesture");
            win.CodeBlock(
                "// Trong pinch recognizer:\n" +
                "_input.OverrideZoomInput(pinchDelta);\n\n" +
                "// Khi gesture kết thúc:\n" +
                "_input.ClearZoomOverride();");

            win.Warn("Chỉ đọc ZoomDelta đúng 1 lần mỗi frame. Giá trị reset về 0 sau lần đọc — đọc nhiều lần sẽ cho 0 ở các lần sau.");
        }
    }
}
#endif
