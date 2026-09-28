// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_Mobile.cs
//  Tab: Mobile UI
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 3 — MOBILE UI
        // ══════════════════════════════════════════════════════

        private void DrawTab_Mobile()
        {
            BeginContentScroll("mobile");
            PageTitle("Mobile UI", "On-screen controls tự động — joystick, skill buttons, camera zone, pinch zoom.");

            H2("Luồng Dữ Liệu Mobile");
            CodeBlock(
                "// Ngón tay chạm màn hình\n" +
                "TISFullscreenTouchReceiver  →  TISVirtualJoystick\n" +
                "                           →  TISInputBridge.OverrideMoveInput()\n" +
                "TISTouchCameraZone          →  TISInputBridge.OverrideLookInput()\n" +
                "                           →  TISInputBridge.OverrideZoomInput()\n" +
                "TISMobileSkillButton        →  TISMobileButtonRegistry[skillName] = true\n" +
                "                           →  TISInputBridge.GetSkillAction() reads here\n" +
                "TISDirectionalSkillButton   →  TISMobileButtonRegistry[skillName] (1 frame)\n" +
                "                           →  TISDirectionalRegistry[skillName + \"_Dir\"]", "Flow");

            Sep();
            H2("Setup TISMobileSkillButton — Button Skill Cơ Bản");
            Note("Component yêu cầu Image trên cùng GameObject (RequireComponent). AddComponentMenu: TIS/Demo/TIS Mobile Button.");
            Step(1, "Thêm action vào Profile", "bindingMobile = Button_N (chọn từ dropdown trong Setup Tool).");
            Step(2, "Tạo UI Image trong Canvas", "GameObject → UI → Image → đặt vị trí (góc phải màn hình cho skills).");
            Step(3, "Add Component → TISMobileSkillButton", "Skill Name = tên action trong profile. Phân biệt hoa/thường.");
            Step(4, "Cooldown Fill (tùy chọn)", "Thêm Image con, Type = Filled → kéo vào field Cooldown Fill trong Inspector → gọi btn.SetCooldownFill(0→1) từ code.");
            Step(5, "Visual Mode", "ColorTint (mặc định): đổi màu theo trạng thái.\nSpriteSwap: assign Normal/Pressed/Disabled Sprite trong Inspector.");

            Sep();
            H2("TISMobileSkillButton — Inspector Fields Quan Trọng");
            ApiHeader("Field", "Type", "Mô tả");
            ApiRow("Skill Name",         "string",  "Tên action trong profile — khớp với GetSkillAction(\"name\").", "purple");
            ApiRow("Interactable",       "bool",    "false = khóa hoàn toàn, không nhận touch, visual mờ nhất.", "green");
            ApiRow("On Touch Camera",    "bool",    "true = touch xuyên qua button xuống camera zone bên dưới.", "green");
            ApiRow("Visual Mode",        "enum",    "ColorTint hoặc SpriteSwap — đổi được lúc runtime qua SetVisualMode().", "purple");
            ApiRow("Flash Duration",     "float",   "Thời gian flash màu khi nhấn (giây). Mặc định 0.1f.", "amber");
            ApiRow("Cooldown Fill",      "Image",   "Image Type=Filled hiển thị tiến độ cooldown. Để null nếu không cần.", "purple");

            Sep();
            H2("TISMobileSkillButton — Thứ Tự Ưu Tiên Visual");
            CodeBlock(
                "// Thứ tự ưu tiên (cao → thấp):\n" +
                "// 1. !Interactable      → notInteractableColor / notInteractableSprite\n" +
                "// 2. _disabledByExternal (SetDisabled) → disabledColor / disabledSprite\n" +
                "// 3. _disabledByCooldown (SetCooldownFill) → giữ normalColor (không đổi màu)\n" +
                "// 4. isPressed          → pressedColor / pressedSprite\n" +
                "// 5. normal             → normalColor / normalSprite\n\n" +
                "btn.Interactable = false;                              // khóa hoàn toàn\n" +
                "btn.SetDisabled(true);                                 // tắt tạm thời + visual tối\n" +
                "btn.SetCooldownFill(0.5f);                             // 50% cooldown, block input\n" +
                "btn.SetCooldownFill(0f);                               // sẵn sàng\n" +
                "btn.SetVisualMode(TISMobileSkillButton.VisualMode.SpriteSwap);");

            Sep();
            H2("TISMobileSkillButton — Events");
            CodeBlock(
                "TISMobileSkillButton btn = GetComponent<TISMobileSkillButton>();\n\n" +
                "// OnPress  — 1 lần khi PointerDown\n" +
                "btn.OnPress.AddListener(() => Debug.Log(\"Nhấn!\"));\n\n" +
                "// OnRelease — khi PointerUp hoặc PointerExit (thoát vùng button)\n" +
                "btn.OnRelease.AddListener(() => Debug.Log(\"Nhả!\"));\n\n" +
                "// OnHold   — mỗi frame khi đang giữ (auto-attack, charge)\n" +
                "btn.OnHold.AddListener(() => ContinueCharge());\n\n" +
                "// OnHoldTimed — mỗi frame, param = HoldTime (giây)\n" +
                "btn.OnHoldTimed.AddListener(t => chargeBar.fillAmount = t / maxCharge);");

            Sep();
            H2("Setup TISDirectionalSkillButton — Button Kéo Hướng (MOBA Style)");
            Note("Kiểu Liên Quân / Tốc Chiến: nhấn giữ + kéo để chọn hướng, nhả tay để cast. AddComponentMenu: TIS/Demo/TIS Mobile Button Direction.");
            Step(1, "Tạo UI Image trong Canvas", "Đặt vị trí button skill. Component yêu cầu Image.");
            Step(2, "Add Component → TISDirectionalSkillButton", "Skill Name = tên action. Direction Action Name để trống → tự dùng \"SkillName_Dir\".");
            Step(3, "Cấu hình Behaviour", "Cast Mode: DragRelease (chuẩn Liên Quân) hoặc Always.\nDrag Threshold Px: pixel tối thiểu phải kéo (mặc định 20px).\nAllow Quick Tap: tap nhanh cast với direction = zero (auto-target).");
            Step(4, "Cancel Zone (tùy chọn)", "Tạo UI GameObject icon hủy → SetActive(false) → kéo RectTransform vào Cancel Zone trong Inspector.\nScript tự bật/tắt Cancel Zone khi đang kéo.");
            Step(5, "Indicator (tùy chọn)", "Gán Indicator Root (RectTransform) và Indicator Image để hiện mũi tên / dot theo hướng kéo.\nIndicator Style: Arrow / Line / Knob / None.");

            Sep();
            H2("TISDirectionalSkillButton — Đọc Kết Quả");
            CodeBlock(
                "// Option A: đọc từ component trực tiếp\n" +
                "if (btn.WasCastThisFrame)\n" +
                "{\n" +
                "    Vector2 dir   = btn.CastDirection;  // normalized, zero = quick tap / auto-target\n" +
                "    float   angle = btn.DirectionAngle; // 0° = phải, 90° = trên\n" +
                "    SpawnProjectile(dir);\n" +
                "}\n\n" +
                "// Option B: registry (không cần giữ reference component)\n" +
                "if (TISMobileButtonRegistry.WasPressedThisFrame(\"Skill1\"))\n" +
                "{\n" +
                "    Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill1_Dir\");\n" +
                "    SpawnProjectile(dir);\n" +
                "}\n\n" +
                "// Option C: event\n" +
                "btn.OnDirectionCast += (skillName, dir) => SpawnProjectile(dir);\n\n" +
                "// Real-time direction khi đang kéo (chưa cast)\n" +
                "Vector2 aim = btn.Direction;   // normalized, zero khi không kéo\n" +
                "bool dragging = btn.IsDragging;");

            Sep();
            H2("TISDirectionalSkillButton — Gọi Qua TISInputBridge");
            Body("Nếu game script không giữ reference đến component, có thể đọc toàn bộ kết quả qua <b>TISInputBridge.Instance</b> — không cần biết button nằm ở đâu trong hierarchy.");
            CodeBlock(
                "var bridge = TISInputBridge.Instance;\n\n" +
                "// Bước 1 — kiểm tra cast (tương đương WasPressedThisFrame)\n" +
                "// GetSkillAction đọc TISMobileButtonRegistry — true đúng 1 frame khi cast\n" +
                "if (bridge.GetSkillAction(\"Skill1\"))\n" +
                "{\n" +
                "    // Bước 2 — lấy hướng sau cast từ TISDirectionalRegistry\n" +
                "    // Mặc định key = skillName + \"_Dir\"  →  \"Skill1_Dir\"\n" +
                "    Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill1_Dir\");\n" +
                "    // dir = Vector2.zero nếu quick tap (auto-target)\n" +
                "    SpawnProjectile(dir);\n" +
                "}\n\n" +
                "// Bước 3 (tùy chọn) — lấy direction real-time khi đang kéo\n" +
                "// Yêu cầu: Direction Action Name đã được điền trong Inspector của button\n" +
                "// (ví dụ: \"AimDir\") để button gọi OverrideVector2Action mỗi frame\n" +
                "Vector2 aimRealTime = bridge.GetVector2Action(\"AimDir\");  // zero khi không kéo");

            GUILayout.Space(4);
            Body("Sơ đồ luồng dữ liệu đầy đủ khi dùng qua Bridge:");
            CodeBlock(
                "// [Ngón tay kéo]\n" +
                "//   TISDirectionalSkillButton.OnDrag()\n" +
                "//     → TISDirectionalRegistry.SetVector2Override(\"AimDir\", direction)\n" +
                "//     → bridge.GetVector2Action(\"AimDir\")  ← đọc real-time\n\n" +
                "// [Ngón tay nhả — cast]\n" +
                "//   TISDirectionalSkillButton.ExecuteCast(dir)\n" +
                "//     → TISMobileButtonRegistry.SetState(\"Skill1\", true)  (1 frame)\n" +
                "//     → TISDirectionalRegistry.SetDirection(\"Skill1_Dir\", dir)\n" +
                "//     → TISDirectionalRegistry.ClearVector2Override(\"AimDir\")\n" +
                "//     → bridge.GetSkillAction(\"Skill1\")  ← true 1 frame\n" +
                "//     → TISDirectionalRegistry.GetDirection(\"Skill1_Dir\")  ← hướng cast");

            Note("Direction Action Name trong Inspector phải khớp với tên action Vector2 đã khai báo trong profile (dùng Setup Tool). Để trống nếu không cần đọc real-time direction qua bridge.");

            Sep();
            H2("Simulate Mobile In Editor");
            CodeBlock(
                "// Bật 'Simulate Mobile In Editor' trong TISInputBridge Inspector\n" +
                "// Khi bật:\n" +
                "//   _isMobileMode = true → GetSkillAction() đọc TISMobileButtonRegistry\n" +
                "//   Keyboard/mouse KHÔNG trigger GetSkillAction() nữa\n" +
                "//   TISSimulatorChecker tự bật khi dùng Unity Device Simulator\n" +
                "// Khi tắt:\n" +
                "//   Keyboard/mouse hoạt động bình thường để test PC");

            Warn("Jump và Run (IsJumping/IsRunning) đọc InputAction trực tiếp — KHÔNG qua TISMobileButtonRegistry. Cần patch riêng trong TISInputBridge nếu muốn mobile trigger các shortcut này.");

            EndContentScroll();
        }

    }  // partial class TISDocWindow
}
#endif