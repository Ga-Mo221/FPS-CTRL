// ============================================================
//  TrueInputSystem — TISCode_Reference.cs
//  Code Sub-Tab Module: Lấy Reference
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Reference : ICodeSubTabModule
    {
        public string Label => "Lấy Reference";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Lấy Reference",
                "Bước đầu tiên: lấy singleton TISInputBridge trong script của bạn.");

            win.Body("Toàn bộ API input đều truy cập qua <b>TISInputBridge.Instance</b>. " +
                 "Singleton tự tìm — không cần kéo thả vào Inspector.");

            GUILayout.Space(8);
            win.H2("Namespace & Cache Instance — Khởi Tạo Trong Start()");
            win.CodeBlock(
                "using TrueInputSystem;\n\n" +
                "public class MyScript : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        // Singleton tự khởi tạo — không cần assign Inspector\n" +
                "        _input = TISInputBridge.Instance;\n" +
                "        if (_input == null)\n" +
                "            Debug.LogError(\"TISInputBridge không tìm thấy trong scene!\");\n" +
                "    }\n" +
                "}");

            win.Note("Luôn cache vào field _input thay vì gọi TISInputBridge.Instance mỗi frame để tránh overhead.");

            win.Sep();
            win.H2("Đọc Profile Đang Active — ActiveProfile.profileName");
            win.Body("ActiveProfile trả về TISInputProfileConfig hiện tại — dùng để log, debug hoặc hiển thị tên profile:");
            win.CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n" +
                "if (input != null)\n" +
                "    Debug.Log(\"Profile hiện tại: \" + input.ActiveProfile.profileName);");

            win.Sep();
            win.H2("Đọc Settings Từ Active Profile — Camera / Mobile / Gamepad");
            win.Body("Truy cập nhanh vào Camera, Mobile và Gamepad settings mà không cần đi qua ActiveProfile:");
            win.CodeBlock(
                "// Camera settings\n" +
                "var camSettings = TISInputBridge.Instance.TISCameraSettings;\n" +
                "Debug.Log(\"Sensitivity: \" + camSettings.mouseSensitivity);\n\n" +
                "// Gamepad settings\n" +
                "var gpSettings = TISInputBridge.Instance.TISGamepadSettings;\n" +
                "Debug.Log(\"Rumble enabled: \" + gpSettings.enableRumble);\n\n" +
                "// Mobile settings\n" +
                "var mobileSettings = TISInputBridge.Instance.TISMobileSettings;");
        }
    }
}
#endif
