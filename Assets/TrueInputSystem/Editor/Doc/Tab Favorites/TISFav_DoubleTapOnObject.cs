// ============================================================
//  TrueInputSystem — TISFav_DoubleTapOnObject.cs
//  Favorites Sub-Tab Module: DoubleTapOnObject
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_DoubleTapOnObject : IFavSubTabModule
    {
        public string Label => "DoubleTapOnObject";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherIsDoubleTapOnObject(id)",
                "bool",
                "true đúng 1 frame khi double-tap vào object có InstanceID cho trước.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("bool hit = _input.ToucherIsDoubleTapOnObject(gameObject.GetInstanceID());");

            win.Sep();
            win.H2("Ví Dụ — Double-Tap Để Chọn Đơn Vị");
            win.CodeBlock(
                "// Gắn script này lên mỗi unit:\n" +
                "private TISInputBridge _input;\n" +
                "private int _myID;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _input = TISInputBridge.Instance;\n" +
                "    _myID  = gameObject.GetInstanceID();\n" +
                "}\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnObject(_myID))\n" +
                "        SelectThisUnit();\n" +
                "}");

            win.Note("Chỉ true đúng 1 frame. Cần SetObjectTrackerEnabled(true) và SetTrackerCamera(cam) trước khi dùng.");
        }
    }
}
#endif
