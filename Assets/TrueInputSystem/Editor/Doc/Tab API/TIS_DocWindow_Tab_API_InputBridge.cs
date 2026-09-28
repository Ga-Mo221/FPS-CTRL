// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.InputBridge.cs
//  Tab API Reference — module: TISInputBridge
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_InputBridge()
        {
            // ── TISInputBridge Properties ─────────────────────────────────
            H2("TISInputBridge — Properties");
            ApiHeader();
            ApiRow("Instance",              "static",   "Singleton access point toàn cục. Tự tìm — không cần assign.", "purple");
            ApiRow("MoveDirection",         "Vector2",  "Di chuyển từ WASD / LeftStick / Joystick ảo. Override nếu OverrideMoveInput active.", "blue");
            ApiRow("LookDelta",             "Vector2",  "Xoay camera frame này (chuột / right stick / kéo ngón). Override nếu OverrideLookInput active.", "blue");
            ApiRow("ZoomDelta",             "float",    "Pinch zoom / scroll — consume mỗi frame, reset về 0.", "amber");
            ApiRow("IsJumping",             "bool",     "Shortcut: GetSkillAction(\"Jump\") — đọc theo readMode trong profile.", "green");
            ApiRow("IsRunning",             "bool",     "Shortcut: GetSkillAction(\"Run\").", "green");
            ApiRow("IsAttacking",           "bool",     "Shortcut: GetSkillAction(\"Attack\").", "green");
            ApiRow("AimAssistTarget",       "Transform","Gán Transform target để AimAssistProcess() hút vào. null = tắt aim assist.", "purple");
            ApiRow("ActiveProfile",         "Config",   "TISInputProfileConfig đang active (runtime copy — safe đọc).", "purple");
            ApiRow("OriginalProfile",       "Config",   "ScriptableObject gốc từ Inspector — không bị mutate bởi rebinding.", "purple");
            ApiRow("TISCameraSettings",     "Settings", "Camera settings từ active profile. null nếu chưa load.", "purple");
            ApiRow("TISMobileSettings",     "Settings", "Mobile settings từ active profile.", "purple");
            ApiRow("TISGamepadSettings",    "Settings", "Gamepad settings từ active profile.", "purple");

            GUILayout.Space(8);
            // ── TISInputBridge Methods ────────────────────────────────────
            H2("TISInputBridge — Skill Actions");
            ApiHeader();
            ApiRow("GetSkillAction(name)",          "bool",    "Đọc button theo readMode cấu hình trong profile. Mobile mode đọc từ MobileButtonRegistry.", "green");
            ApiRow("GetSkillAction(name, mode)",    "bool",    "Đọc button với mode tường minh — bỏ qua profile.", "green");
            ApiRow("IsSkillDown(name)",             "bool",    "PressDown — true đúng 1 frame khi nhấn.", "green");
            ApiRow("IsSkillHeld(name)",             "bool",    "Hold — true suốt khi đang giữ.", "green");
            ApiRow("IsSkillReleased(name)",         "bool",    "PressUp — true đúng 1 frame khi nhả.", "green");
            ApiRow("GetVector2Action(name)",        "Vector2", "Đọc Vector2 action (navigate, joystick, RTS pan).", "blue");
            ApiRow("GetAxisAction(name)",           "float",   "Đọc float analog (trigger, throttle, zoom).", "amber");

            GUILayout.Space(8);
            H2("TISInputBridge — Override Channel (Mobile / Internal)");
            ApiHeader();
            ApiRow("OverrideMoveInput(v2)",         "void",    "Joystick ảo gọi — inject MoveDirection. Ưu tiên hơn InputSystem.", "red");
            ApiRow("ClearMoveOverride()",           "void",    "Xóa override, trả về InputSystem bình thường.", "red");
            ApiRow("OverrideLookInput(v2)",         "void",    "Camera swipe zone inject LookDelta.", "red");
            ApiRow("ClearLookOverride()",           "void",    "Xóa override look.", "red");
            ApiRow("OverrideZoomInput(f)",          "void",    "Pinch-to-zoom inject ZoomDelta.", "red");
            ApiRow("ClearZoomOverride()",           "void",    "Xóa override zoom.", "red");
            ApiRow("OverrideVector2Action(name,v2)","void",    "DirectionalSkillButton inject direction real-time vào action Vector2.", "red");
            ApiRow("ClearVector2Override(name)",    "void",    "Xóa override action Vector2.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — Action Map & Profile");
            ApiHeader();
            ApiRow("SetActionMapActive(map, bool)", "void",    "Bật/tắt Action Map theo tên (case-insensitive).", "red");
            ApiRow("DisableAllMaps()",              "void",    "Tắt hết action map (cutscene, death, loading).", "red");
            ApiRow("EnableDefaultMaps()",           "void",    "Bật theo enabledByDefault trong profile active.", "red");
            ApiRow("SwapProfile(config)",           "void",    "Đổi profile runtime — rebuild InputActionAsset, clear mobile registry.", "red");
            ApiRow("LogActiveProfile()",            "void",    "In toàn bộ cấu hình profile ra Console.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — AimAssist & Deadzone");
            ApiHeader();
            ApiRow("AimAssistProcess(settings,cam,target,look,intensity)", "Vector2", "Trả về tổng Delta góc xoay (độ) sau khi cộng lực hút aim assist.", "blue");
            ApiRow("ApplyDeadzone(rawInput, radius=0.2f)",                 "Vector2", "Scaled Radial Deadzone: input nhỏ hơn ngưỡng → zero; nội suy mượt.", "blue");

            GUILayout.Space(8);
            H2("TISInputBridge — Rumble (Gamepad)");
            ApiHeader();
            ApiRow("TryRumble(low, high, duration)", "void",   "Rung trong duration giây rồi tự dừng. Không làm gì nếu rumble bị tắt trong profile.", "red");
            ApiRow("TryRumble(low, high)",           "void",   "Rung liên tục cho đến khi gọi StopRumble().", "red");
            ApiRow("StopRumble()",                   "void",   "Dừng rung ngay lập tức, hủy coroutine nếu đang chạy.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — Gamepad Virtual Cursor");
            ApiHeader();
            ApiRow("RegisterGamepadCursor(cursor)",  "void",   "TISGamepadVirtualCursor tự gọi trong Awake().", "red");
            ApiRow("SetActiveGamepadCursor(active)", "void",   "Bật/tắt gamepad virtual cursor. Khi bật: reset vị trí về center màn hình.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — Gesture Proxy (Multi-finger)");
            ApiHeader();
            ApiRow("GestureActiveFingers",           "int",     "Số ngón đang chạm trong camera zone (đã loại joystick/button).", "amber");
            ApiRow("GestureCentroidPosition",        "Vector2", "Tâm (centroid) tất cả ngón trong zone — screen space.", "blue");
            ApiRow("GestureCentroidDelta",           "Vector2", "Delta centroid so frame trước (pixel). Zero khi số ngón thay đổi.", "blue");
            ApiRow("GestureIsTwoFingerPanning",      "bool",    "true khi đúng 2 ngón đang di chuyển cùng hướng.", "green");
            ApiRow("GestureTwoFingerPanDelta",       "Vector2", "Delta midpoint 2 ngón, đã nhân sensitivity.", "blue");
            ApiRow("GestureTwoFingerPanRaw",         "Vector2", "Delta midpoint 2 ngón, raw (chưa nhân sensitivity).", "blue");
            ApiRow("GestureFinger0Delta",            "Vector2", "Delta pixel ngón thứ nhất frame này.", "blue");
            ApiRow("GestureFinger1Delta",            "Vector2", "Delta pixel ngón thứ hai frame này.", "blue");
            ApiRow("GestureGetFingerPosition(i)",    "Vector2", "Vị trí screen của ngón thứ i trong zone.", "blue");
            ApiRow("GestureSyncZone(start, end)",    "void",    "Đồng bộ zone ratio cho gesture detector (0–1).", "red");
            ApiRow("GestureSyncZone()",              "void",    "Sync zone từ TISTouchCameraZone.", "red");
            ApiRow("RegisterGestureDetector(d)",     "void",    "Đăng ký TISMultiFingerGestureDetector (tự gọi trong Awake).", "red");
            ApiRow("UnregisterGestureDetector(d)",   "void",    "Hủy đăng ký gesture detector.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — Object Tracker");
            ApiHeader();
            ApiRow("ToucherObjectIDs",               "IReadOnly","Danh sách InstanceID tất cả GameObject đã touch trúng.", "purple");
            ApiRow("ToucherCount",                   "int",      "Số lượng object đang chạm. 0 nếu tracker tắt.", "amber");
            ApiRow("ToucherGetLastID()",             "int",      "InstanceID của object touch trúng cuối cùng.", "green");
            ApiRow("ToucherGetLastName()",           "string",   "Tên object touch trúng cuối cùng (debug).", "green");
            ApiRow("ToucherHasNoObjects()",          "bool",     "true nếu chưa touch trúng object nào.", "green");
            ApiRow("ToucherIsDoubleTapOnObject(id)", "bool",     "true đúng 1 frame khi double-tap vào object có InstanceID.", "green");
            ApiRow("ToucherIsDoubleTapOnScreen()",   "bool",     "true đúng 1 frame khi double-tap bất kỳ vị trí màn hình.", "green");
            ApiRow("ToucherGetDoubleTappedObject()", "GO",       "GameObject bị double-tap frame này. null nếu không có.", "green");
            ApiRow("ToucherGetDoubleTappedObject(id)","GO",      "Object bị double-tap khớp instanceID.", "green");
            ApiRow("ToucherGetSingleObject()",       "GO",       "Object đang touch nếu đúng 1 ngón active. null nếu 0 hoặc >1 ngón.", "green");
            ApiRow("ToucherWasTouched(id)",          "bool",     "true nếu object đó đã được touch trúng.", "green");
            ApiRow("ToucherIsSingleTouch()",         "bool",     "true khi đúng 1 touch đang active (chỉ đếm ngón, không raycast).", "green");
            ApiRow("ToucherWorldPosition()",         "Vector3",  "Vị trí World Space của touch hiện tại (Raycast). Vector3.zero nếu không có.", "blue");
            ApiRow("ToucherClearList()",             "void",     "Xóa danh sách touched objects (giữ trạng thái double-tap).", "red");
            ApiRow("ToucherClearAll()",              "void",     "Xóa toàn bộ trạng thái tracker.", "red");
            ApiRow("ToucherClearTouchCount()",       "void",     "Reset bộ đếm touch — phân biệt phiên chạm mới.", "red");
            ApiRow("ToucherResetDoubleTapForObject(id)","void",  "Reset trạng thái double-tap cho object chỉ định.", "red");
            ApiRow("ToucherResetDoubleTapScreen()",  "void",     "Reset trạng thái double-tap toàn màn hình.", "red");
            ApiRow("ToucherLogObjects()",            "void",     "Log danh sách touched objects ra Console.", "red");
            ApiRow("SetObjectTrackerEnabled(bool)",  "void",     "Bật/tắt Object Tracker lúc runtime.", "red");
            ApiRow("SetTrackerCamera(cam)",          "void",     "Đổi Camera raycast lúc runtime. null = dùng Camera.main.", "red");

            GUILayout.Space(8);
            H2("TISInputBridge — Pointer Position (Drag)");
            Note("4 API hỗ trợ drag-and-drop UI và World Space. Yêu cầu Object Tracker được bật (Enable Object Tracker trong Inspector).");
            ApiHeader();
            ApiRow("GetPointerScreenPosition()",   "Vector2", "[API 1 — UI Drag] Vị trí screen space (pixel) của pointer/touch đang giữ. Cập nhật mỗi frame. Dùng để di chuyển RectTransform trong Canvas. Vector2.zero nếu không có pointer active.", "blue");
            ApiRow("GetPointerScreenDelta()",      "Vector2", "Delta screen (pixel) của pointer so với frame trước. Dùng để tính offset kéo mà không cần lưu vị trí cũ. Vector2.zero nếu không có pointer active.", "blue");
            ApiRow("IsPointerHeld",                "bool",    "true khi pointer/touch đang được giữ (drag đang diễn ra). Dùng như guard trước khi đọc các Pointer API khác.", "green");
            ApiRow("GetPointerWorldPosition(depth)","Vector3","[API 2 — World Drag] Vị trí World Space tương ứng với pointer đang giữ. depth = khoảng cách từ camera đến mặt phẳng đặt object. 2D Ortho: depth = Mathf.Abs(Camera.main.transform.position.z). Vector3.zero nếu không có pointer hoặc không có Camera.", "blue");

            GUILayout.Space(8);
            H2("TISInputBridge — Events");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnProfileSwapped", "event Action<TISInputProfileConfig>", "Sau khi SwapProfile() hoàn tất. Param = profile mới đang active.", "purple");
        }

    }  // partial class TISDocWindow
}
#endif
