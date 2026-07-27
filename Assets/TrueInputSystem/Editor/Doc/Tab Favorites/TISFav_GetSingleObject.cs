// ============================================================
//  TrueInputSystem — TISFav_GetSingleObject.cs
//  Favorites Sub-Tab Module: GetSingleObject
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_GetSingleObject : IFavSubTabModule
    {
        public string Label => "GetSingleObject";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "ToucherGetSingleObject()",
                "GameObject",
                "GameObject đang touch nếu đúng 1 touch active. null nếu 0 hoặc >1 ngón.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock(
                "GameObject obj = _input.ToucherGetSingleObject();\n\n" +
                "// Kiểm tra đếm không raycast:\n" +
                "bool isSingle = _input.ToucherIsSingleTouch();");

            win.Sep();
            win.H2("Ví Dụ — Single Touch Để Di Chuyển Object");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    GameObject obj = _input.ToucherGetSingleObject();\n" +
                "    if (obj == null) return;\n\n" +
                "    if (obj.CompareTag(\"Draggable\"))\n" +
                "    {\n" +
                "        // Đang touch đúng 1 ngón vào draggable object\n" +
                "        DragObject(obj);\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Phân Biệt 1 Ngón vs 2 Ngón");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureIsTwoFingerPanning)\n" +
                "    {\n" +
                "        PanCamera(_input.GestureTwoFingerPanDelta);\n" +
                "    }\n" +
                "    else\n" +
                "    {\n" +
                "        GameObject target = _input.ToucherGetSingleObject();\n" +
                "        if (target != null) SelectTarget(target);\n" +
                "    }\n" +
                "}");

            win.Note("ToucherIsSingleTouch() chỉ đếm ngón, không raycast. ToucherGetSingleObject() raycast để trả về object — nặng hơn, dùng khi thực sự cần object reference.");
        }
    }
}
#endif
