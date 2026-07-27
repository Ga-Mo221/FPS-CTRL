// ============================================================
//  TrueInputSystem — TISCode_ProfileSwap.cs
//  Code Sub-Tab Module: Profile/Events
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_ProfileSwap : ICodeSubTabModule
    {
        public string Label => "Profile/Events";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Profile & Events",
                "SwapProfile runtime và lắng nghe OnProfileSwapped.");

            win.H2("SwapProfile — Đổi Toàn Bộ Cấu Hình Input");
            win.Body("Rebuild InputActionAsset, xóa mobile registry, fire OnProfileSwapped. Dùng khi game mode thay đổi hoàn toàn:");
            win.CodeBlock(
                "[SerializeField] TISInputProfileConfig _vehicleProfile;\n" +
                "[SerializeField] TISInputProfileConfig _characterProfile;\n\n" +
                "void OnEnterVehicle() => TISInputBridge.Instance.SwapProfile(_vehicleProfile);\n" +
                "void OnExitVehicle()  => TISInputBridge.Instance.SwapProfile(_characterProfile);");

            win.Sep();
            win.H2("SwapProfile vs SetActionMapActive");
            win.CodeBlock(
                "// SwapProfile        — thay TOÀN BỘ cấu hình (khác game mode hoàn toàn)\n" +
                "// SetActionMapActive — chỉ bật/tắt nhóm trong profile hiện tại\n\n" +
                "// ✓ Dùng SetActionMapActive khi: lên xe, cutscene, death\n" +
                "// ✓ Dùng SwapProfile khi:        chuyển adventure → vehicle racing mode");

            win.Warn("SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll() — button mobile bị reset. Dùng SetActionMapActive() nếu muốn giữ state.");

            win.Sep();
            win.H2("OnProfileSwapped — Lắng Nghe Sau Khi Swap");
            win.Body("Event fire sau khi SwapProfile() hoàn tất, truyền profile mới làm argument:");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _input.OnProfileSwapped += OnProfileChanged;\n" +
                "}\n\n" +
                "void OnProfileChanged(TISInputProfileConfig newProfile)\n" +
                "{\n" +
                "    Debug.Log(\"Profile đã chuyển sang: \" + newProfile.profileName);\n" +
                "    RefreshControlHints(newProfile);\n" +
                "}\n\n" +
                "void OnDestroy()\n" +
                "{\n" +
                "    if (_input != null)\n" +
                "        _input.OnProfileSwapped -= OnProfileChanged;\n" +
                "}");

            win.Note("SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll() — button mobile bị reset. Dùng SetActionMapActive() nếu muốn giữ state.");
        }
    }
}
#endif
