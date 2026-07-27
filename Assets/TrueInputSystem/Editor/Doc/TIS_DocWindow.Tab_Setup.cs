// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_Setup.cs
//  Tab: Setup Scene
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 2 — SETUP SCENE
        // ══════════════════════════════════════════════════════

        private void DrawTab_Setup()
        {
            BeginContentScroll("setup");
            PageTitle("Setup Scene", "Thực hiện đúng thứ tự bên dưới. Mỗi bước phụ thuộc bước trước.");

            Tip("Mở: Tools → True Input System → Setup để sử dụng giao diện trực quan.");
            GUILayout.Space(12);

            H2("Cách 1 — Hierarchy Context Menu (Nhanh Nhất)");
            Body("Right-click vào bất kỳ vị trí nào trong cửa sổ <b>Hierarchy</b> → chọn <b>True Input System</b>:");
            GUILayout.Space(4);
            Step(1, "True Input System → TIS Setup",
                "Tạo ngay GameObject \"TIS Setup\" với component TISInputBridge đã gắn sẵn.\n" +
                "Sau đó kéo file Profile (.asset) vào field Profile trong Inspector.\n" +
                "Menu chỉ hiện khi không ở Play Mode.");
            Step(2, "True Input System → Mobile Input Setup",
                "Tạo GameObject \"Mobile Input Setup\" với component TISMobileInputSetup.\n" +
                "Dùng cho dự án có on-screen controls (joystick, skill button, camera zone).\n" +
                "Menu chỉ hiện khi không ở Play Mode.");
            Note("Nếu right-click vào một GameObject trong Hierarchy, object mới sẽ tự gắn làm con của object đó.");

            Sep();
            H2("Cách 2 — Thủ Công Từng Bước");
            Step(1, "Tạo Default Profile",
                "Dùng menu: Tools → True Input System → Setup → + Profile Mới.\n" +
                "File tạo tại: Assets/TrueInputSystem/Profiles/DefaultProfile.asset\n" +
                "Có sẵn: Move, Jump, Run · Look · Attack, Skill1, Skill2");

            Step(2, "Tạo TISInputBridge GameObject",
                "Tạo Empty GameObject → Add Component → TISInputBridge → kéo DefaultProfile.asset vào field Profile.");

            Step(3, "Kết Nối Player Script",
                "Lấy reference qua TISInputBridge.Instance trong Start(). Camera tự đăng ký vào Bridge.");

            Step(4, "Kiểm Tra Trong Play Mode",
                "Chọn TISInputBridge → tab Debug → xem bảng Actions live → nhấn phím → cột Active chuyển YES.");

            Warn("Chỉ có đúng MỘT TISInputBridge trong scene. Nếu có 2 instance, cái sau tự hủy trong Awake() với cảnh báo.");

            Sep();
            H2("Kết Nối Player Script — Lấy TISInputBridge Trong Start()");
            CodeBlock(
                "private TISInputBridge _input;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _input = TISInputBridge.Instance;\n" +
                "}\n\n" +
                "void Update()\n" +
                "{\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    if (_input.IsJumping) Jump();\n" +
                "    if (_input.GetSkillAction(\"Attack\")) Attack();\n" +
                "}");

            EndContentScroll();
        }

    }  // partial class TISDocWindow
}
#endif