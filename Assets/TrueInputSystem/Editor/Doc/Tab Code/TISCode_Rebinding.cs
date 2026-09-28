// ============================================================
//  TrueInputSystem — TISCode_Rebinding.cs
//  Code Sub-Tab Module: Rebinding
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Rebinding : ICodeSubTabModule
    {
        public string Label => "Rebinding";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Key Rebinding",
                "TISRebindingSystem — Interactive, Programmatic, Persistence, Query.");

            win.H2("Interactive — Chờ Người Dùng Nhấn Phím");
            win.Body("Gọi StartRebinding() khi người dùng click nút 'Rebind' trong Settings UI:");
            win.CodeBlock(
                "void OnRebindJumpClicked()\n" +
                "{\n" +
                "    _pressKeyOverlay.SetActive(true);\n\n" +
                "    TISRebindingSystem.StartRebinding(\n" +
                "        TISInputBridge.Instance,\n" +
                "        \"Jump\",\n" +
                "        RebindTarget.PC,\n" +
                "        result =>\n" +
                "        {\n" +
                "            _pressKeyOverlay.SetActive(false);\n" +
                "            if (result.Success)\n" +
                "                _jumpLabel.text = TISRebindingSystem.GetDisplayName(result.NewPath);\n" +
                "        }\n" +
                "    );\n" +
                "}\n\n" +
                "// Hủy rebinding đang chạy\n" +
                "void OnCancelClicked() => TISRebindingSystem.CancelRebinding();");

            win.Sep();
            win.H2("Events Toàn Cục — OnRebindStarted & OnRebindCompleted");
            win.Body("Subscribe một lần trong Awake(), nhận event cho mọi nút rebind:");
            win.CodeBlock(
                "void Awake()\n" +
                "{\n" +
                "    TISRebindingSystem.OnRebindStarted   += (action, target) => ShowOverlay(action);\n" +
                "    TISRebindingSystem.OnRebindCompleted += result =>\n" +
                "    {\n" +
                "        HideOverlay();\n" +
                "        if (result.Success) RefreshLabel(result.ActionName, result.NewPath);\n" +
                "    };\n" +
                "}\n\n" +
                "void OnDestroy()\n" +
                "{\n" +
                "    TISRebindingSystem.OnRebindStarted   -= ...;\n" +
                "    TISRebindingSystem.OnRebindCompleted -= ...;\n" +
                "}");

            win.Sep();
            win.H2("Programmatic — ForceApply & ApplyPreset");
            win.Body("Apply binding bằng code không cần nhấn phím — dùng cho preset FPS/RPG:");
            win.CodeBlock(
                "TISRebindingSystem.ForceApply(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC, \"<Keyboard>/g\"\n" +
                ");\n\n" +
                "var fpsPreset = new Dictionary<string, string>\n" +
                "{\n" +
                "    { \"Jump\",   \"<Keyboard>/space\" },\n" +
                "    { \"Run\",    \"<Keyboard>/leftShift\" },\n" +
                "    { \"Attack\", \"<Mouse>/leftButton\" },\n" +
                "};\n" +
                "TISRebindingSystem.ApplyPreset(TISInputBridge.Instance, fpsPreset, RebindTarget.PC);");

            win.Sep();
            win.H2("Query — Hiển Thị Phím Lên UI");
            win.CodeBlock(
                "string path = TISRebindingSystem.GetActivePath(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC\n" +
                ");\n" +
                "_jumpLabel.text = TISRebindingSystem.GetDisplayName(path);\n\n" +
                "string conflict = TISRebindingSystem.FindConflict(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC, \"<Keyboard>/g\"\n" +
                ");\n" +
                "if (conflict != null) ShowWarning($\"{conflict} đang dùng phím này!\");");

            win.Sep();
            win.H2("Persistence — Load & Reset");
            win.CodeBlock(
                "void Awake()\n" +
                "{\n" +
                "    TISRebindingSystem.LoadAndApply(TISInputBridge.Instance);\n" +
                "}\n\n" +
                "void OnResetJumpClicked()\n" +
                "    => TISRebindingSystem.ResetBinding(TISInputBridge.Instance, \"Jump\", RebindTarget.PC);\n\n" +
                "void OnResetAllClicked()\n" +
                "    => TISRebindingSystem.ResetAllBindings(TISInputBridge.Instance);");

            win.Note("JSON file lưu tại Application.persistentDataPath/TIS_Bindings_{profileName}.json. Chỉ hỗ trợ Button action — Vector2/Axis/Mobile binding không rebind qua hệ thống này.");
        }
    }
}
#endif
