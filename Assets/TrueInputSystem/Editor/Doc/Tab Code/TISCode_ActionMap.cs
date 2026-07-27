// ============================================================
//  TrueInputSystem — TISCode_ActionMap.cs
//  Code Sub-Tab Module: Action Map
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_ActionMap : ICodeSubTabModule
    {
        public string Label => "Action Map";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Action Map",
                "Bật / tắt nhóm input theo context: lên xe, cutscene, death...");

            win.H2("SetActionMapActive");
            win.CodeBlock(
                "_input.SetActionMapActive(\"Movement\", true);\n" +
                "_input.SetActionMapActive(\"Movement\", false);");

            win.Sep();
            win.H2("Ví Dụ: Lên / Xuống Xe");
            win.CodeBlock(
                "void OnEnterVehicle()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Movement\", false);\n" +
                "    _input.SetActionMapActive(\"Skill\",    false);\n" +
                "    _input.SetActionMapActive(\"Vehicle\",  true);\n" +
                "}\n\n" +
                "void OnExitVehicle()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Vehicle\",  false);\n" +
                "    _input.SetActionMapActive(\"Movement\", true);\n" +
                "    _input.SetActionMapActive(\"Skill\",    true);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ: Mở / Đóng Inventory UI");
            win.CodeBlock(
                "void OnOpenInventory()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Combat\", false);\n" +
                "    _input.SetActionMapActive(\"UI\",     true);\n" +
                "}\n\n" +
                "void OnCloseInventory()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Combat\", true);\n" +
                "    _input.SetActionMapActive(\"UI\",     false);\n" +
                "}");

            win.Sep();
            win.H2("Tắt / Bật Toàn Bộ — DisableAllMaps & EnableDefaultMaps");
            win.Body("Dùng khi cutscene, loading, nhân vật chết — không muốn bất kỳ input nào hoạt động:");
            win.CodeBlock(
                "// Tắt hết\n" +
                "void OnCutsceneStart() => _input.DisableAllMaps();\n\n" +
                "// Bật lại theo enabledByDefault trong profile\n" +
                "void OnCutsceneEnd()   => _input.EnableDefaultMaps();");

            win.Sep();
            win.H2("enabledByDefault Trong Profile — Map Nào Bật / Tắt Khi Load");
            win.Body("Maps nên tắt mặc định (vehicle, ui-only) sẽ không active trước khi bạn gọi SetActionMapActive():");
            win.CodeBlock(
                "// TISActionMapData trong Profile\n" +
                "mapName          = \"Vehicle\"\n" +
                "enabledByDefault = false   // tắt lúc game bắt đầu\n\n" +
                "// Chỉ bật khi người chơi lên xe:\n" +
                "_input.SetActionMapActive(\"Vehicle\", true);");

            win.Tip("Dùng EnableDefaultMaps() sau cutscene hoặc respawn thay vì bật từng map một.");

            win.Sep();
            win.H2("Debug — In Profile Hiện Tại Ra Console Với LogActiveProfile()");
            win.CodeBlock(
                "// Gọi từ Debug menu trong game\n" +
                "debugButton.onClick.AddListener(() => _input.LogActiveProfile());");
        }
    }
}
#endif
