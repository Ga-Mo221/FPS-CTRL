// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_Overview.cs
//  Tab: Tổng Quan
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 1 — TỔNG QUAN
        // ══════════════════════════════════════════════════════

        private void DrawTab_Overview()
        {
            BeginContentScroll("overview");

            PageTitle("TrueInputSystem",
                "Hệ thống Input đa nền tảng — một API duy nhất cho PC, Gamepad và Mobile.");

            // ── 3 Feature Cards ─────────────────────────────────
            GUILayout.BeginHorizontal();
            FeatureCard("Đa Nền Tảng",    "PC · Gamepad · Mobile với API thống nhất — không cần if/else platform.", AccentBlue);
            GUILayout.Space(8);
            FeatureCard("Zero Config",    "Singleton tự tìm — không cần assign Inspector. Profile ScriptableObject.", AccentGreen);
            GUILayout.Space(8);
            FeatureCard("Visual Editor",  "Setup Tool kéo-thả, tạo profile trực quan, Validate & Generate.", AccentAmber);
            GUILayout.EndHorizontal();

            // ══════════════════════════════════════════════════
            //  LUỒNG DỮ LIỆU
            // ══════════════════════════════════════════════════
            GUILayout.Space(16);
            H2("Kiến Trúc — Luồng Dữ Liệu Từ Profile Đến Script");
            Body("Script của bạn chỉ cần nói chuyện với <b>TISInputBridge.Instance</b> — " +
                 "không bao giờ đọc UnityEngine.Input hay InputAction trực tiếp:");
            CodeBlock(
                "TISInputProfileConfig  (ScriptableObject — khai báo actions & bindings)\n" +
                "        ↓\n" +
                "TISInputBridge         (Singleton — build InputActionAsset, expose API)\n" +
                "        ↓                               ↓\n" +
                "  PC / Gamepad                    Mobile UI\n" +
                "  InputAction (built-in)           TISVirtualJoystick        → OverrideMoveInput()\n" +
                "                                   TISMobileSkillButton      → TISMobileButtonRegistry\n" +
                "                                   TISTouchCameraZone        → OverrideLookInput()\n" +
                "                                   TISDirectionalSkillButton → TISDirectionalRegistry\n" +
                "        ↓                               ↓\n" +
                "  Script của bạn — chỉ dùng TISInputBridge:\n" +
                "    _input.MoveDirection          // di chuyển\n" +
                "    _input.LookDelta              // xoay camera\n" +
                "    _input.GetSkillAction(\"name\") // button / skill");

            // ══════════════════════════════════════════════════
            //  QUICK START
            // ══════════════════════════════════════════════════
            Sep();
            H2("Quick Start — 3 Bước Để Chạy");
            Step(1, "Tạo Profile",
                "Tools → True Input System → Setup → + Profile Mới.\n" +
                "Thêm Action Map, khai báo actions, chọn binding cho PC / Gamepad / Mobile.");
            Step(2, "Gắn TISInputBridge",
                "Right-click Hierarchy → True Input System → TIS Setup.\n" +
                "Kéo file .asset vừa tạo vào field Profile trong Inspector.");
            Step(3, "Viết Script",
                "Cache TISInputBridge.Instance trong Start(). Đọc input trong Update().\n" +
                "Cùng code chạy được trên PC, Gamepad và Mobile — không cần if/else platform.");
            Tip("Mở: Tools → True Input System → Documentation để xem hướng dẫn chi tiết từng tab.");

            // ══════════════════════════════════════════════════
            //  RUNTIME CORE
            // ══════════════════════════════════════════════════
            Sep();
            H2("Runtime Core — Các Class Chạy Trong Game");
            ApiHeader("Class", "Loại", "Mô tả");
            ApiRow("TISInputBridge",            "Singleton", "Điểm duy nhất cần tham chiếu. Build InputActionAsset từ profile, expose toàn bộ API.", "blue");
            ApiRow("TISMobileButtonRegistry",   "static",    "Dictionary trạng thái on-screen buttons. GetSkillAction() đọc tại đây khi _isMobileMode = true.", "purple");
            ApiRow("TISDirectionalRegistry",    "static",    "Lưu direction vector sau khi cast từ TISDirectionalSkillButton. GetDirection(\"Skill_Dir\").", "purple");
            ApiRow("TISRebindingSystem",         "static",    "Interactive & programmatic key rebinding. JSON persistence. Không phụ thuộc MonoBehaviour.", "amber");

            // ══════════════════════════════════════════════════
            //  MOBILE UI
            // ══════════════════════════════════════════════════
            GUILayout.Space(8);
            H2("Mobile UI — Các Component On-Screen");
            ApiHeader("Class", "Loại", "Mô tả");
            ApiRow("TISMobileInputSetup",        "UI",     "MonoBehaviour tự tạo Canvas + Joystick + CameraZone lúc runtime.", "green");
            ApiRow("TISVirtualJoystick",         "UI",     "Joystick ảo. Dynamic mode (PUBG): hiện tại điểm chạm. Forward qua OverrideMoveInput().", "green");
            ApiRow("TISMobileSkillButton",       "UI",     "On-screen button. Inject state vào TISMobileButtonRegistry. Hỗ trợ cooldown, visual mode, events.", "green");
            ApiRow("TISDirectionalSkillButton",  "UI",     "Button kéo hướng MOBA style (Liên Quân). Nhả tay → cast. Hỗ trợ cancel zone, indicator.", "green");
            ApiRow("TISTouchCameraZone",         "UI",     "Vùng kéo camera + pinch zoom. Inject qua OverrideLookInput() và OverrideZoomInput().", "green");
            ApiRow("TISFullscreenTouchReceiver", "UI",     "Nhận touch toàn màn hình rồi phân loại về joystick hoặc camera zone.", "green");

            // ══════════════════════════════════════════════════
            //  GAMEPAD UI
            // ══════════════════════════════════════════════════
            GUILayout.Space(8);
            H2("Gamepad UI — Navigation 3 Lớp");
            Body("Hệ thống điều hướng UI bằng gamepad: Tab (LT/RT) → SubTab (LB/RB) → Content (DPad/Stick).");
            ApiHeader("Class", "Loại", "Mô tả");
            ApiRow("TISGamepadUIButton",         "UI",     "Gắn lên từng Button. Kế thừa UnityEngine.UI.Button. Xử lý navigation + input cho layer.", "blue");
            ApiRow("TISGamepadUIButtonGroup",    "UI",     "Gắn lên parent. RebuildNavigation() tự tính navUp/Down/Left/Right theo layout.", "blue");
            ApiRow("TISUINavigationContext",     "Singleton","Redirect Tab → SubTab → Content focus. Khai báo TabEntry/SubTabEntry trong Inspector.", "purple");
            ApiRow("TISGamepadVirtualCursor",    "UI",     "Con trỏ ảo điều khiển bằng left stick. Inject vào InputSystem như chuột thật.", "amber");

            // ══════════════════════════════════════════════════
            //  ADVANCED FEATURES
            // ══════════════════════════════════════════════════
            GUILayout.Space(8);
            H2("Tính Năng Nâng Cao");
            ApiHeader("Class / Feature", "Loại", "Mô tả");
            ApiRow("TISTargetScanner",           "Component","Auto lock target gần tâm màn hình. Kết hợp với AimAssistProcess() trên TISInputBridge.", "amber");
            ApiRow("AimAssistProcess()",         "Method",   "Hút camera về target theo AimAssistSettings. Chỉ hoạt động trên platform được chỉ định.", "amber");
            ApiRow("ApplyDeadzone()",            "Method",   "Scaled Radial Deadzone cho raw analog input. Input < ngưỡng → Vector2.zero.", "amber");
            ApiRow("TryRumble() / StopRumble()", "Method",   "Haptic gamepad có hẹn giờ hoặc liên tục. Tự bỏ qua nếu enableRumble = false.", "amber");
            ApiRow("TISMultiFingerGestureDetector","Component","Theo dõi centroid, two-finger pan, delta từng ngón trong camera zone.", "purple");
            ApiRow("TISTouchObjectTracker",       "Component","Raycast touch → tích lũy InstanceID. Double-tap detect trên 3D/2D object.", "purple");

            // ══════════════════════════════════════════════════
            //  EDITOR TOOLING
            // ══════════════════════════════════════════════════
            GUILayout.Space(8);
            H2("Editor Tooling");
            ApiHeader("Tool", "Loại", "Mô tả");
            ApiRow("Setup Tool",    "Editor Window", "Tạo / chỉnh sửa TISInputProfileConfig. 3-panel: Action Map · Binding Editor · Platform Settings.", "blue");
            ApiRow("✔ Validate",    "Toolbar",       "Kiểm tra tên trùng, binding conflict, thiếu action trước khi Generate.", "green");
            ApiRow("⬆ Generate",    "Toolbar",       "Lưu profile ra file .asset. Ctrl+S. Có thể gắn vào TISInputBridge trong scene.", "amber");
            ApiRow("Documentation", "Editor Window", "Cửa sổ này. Mở: Tools → True Input System → Documentation.", "purple");

            // ══════════════════════════════════════════════════
            //  NOTE
            // ══════════════════════════════════════════════════
            GUILayout.Space(4);
            Note("Script của bạn không bao giờ đọc UnityEngine.Input hay InputAction trực tiếp. " +
                 "Chỉ dùng TISInputBridge.Instance — cùng code hoạt động trên PC, Gamepad và Mobile.");

            EndContentScroll();
        }

        private void FeatureCard(string title, string desc, Color accentColor)
        {
            Rect cardRect = GUILayoutUtility.GetRect(0, 74, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(cardRect, BgSurface);
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3, cardRect.height), accentColor);
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, cardRect.width, 1), BorderSub);
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.yMax - 1, cardRect.width, 1), BorderSub);

            var tSt = new GUIStyle(EditorStyles.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TxtPrimary }, wordWrap = false };
            var dSt = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, normal = { textColor = TxtSecond } };
            GUI.Label(new Rect(cardRect.x + 12, cardRect.y + 10, cardRect.width - 16, 20), title, tSt);
            GUI.Label(new Rect(cardRect.x + 12, cardRect.y + 32, cardRect.width - 16, 36), desc, dSt);
        }


    }  // partial class TISDocWindow
}
#endif