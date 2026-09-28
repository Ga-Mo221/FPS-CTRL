// ============================================================
//  TrueInputSystem — TISFav_GetDoubleTapped.cs
//  Favorites Sub-Tab Module: GetDoubleTapped
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_GetDoubleTapped : IFavSubTabModule
    {
        public string Label => "GetDoubleTapped";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherGetDoubleTappedObject()",
                "GameObject",
                "GameObject bị double-tap frame này. null nếu không có hoặc không raycast trúng.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("GameObject obj = _input.ToucherGetDoubleTappedObject();");

            win.Sep();
            win.H2("Ví Dụ — Xử Lý Object Bị Double-Tap");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    GameObject tapped = _input.ToucherGetDoubleTappedObject();\n" +
                "    if (tapped == null) return;\n\n" +
                "    if (tapped.CompareTag(\"Enemy\"))\n" +
                "        LockOnTarget(tapped.transform);\n" +
                "    else if (tapped.CompareTag(\"Item\"))\n" +
                "        PickUpItem(tapped);\n" +
                "}");

            win.Note("Trả về null nếu frame này không có double-tap, hoặc raycast không trúng GameObject có Collider. Cần Object Tracker đang bật.");
        }
    }
}
#endif
