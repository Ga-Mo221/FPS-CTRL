// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.cs
//  Tab: API Reference + FAQ
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 8 — API REFERENCE
        // ══════════════════════════════════════════════════════

        private void DrawTab_API()
        {
            BeginContentScroll("api");
            PageTitle("API Reference", "Toàn bộ public API — Properties, Methods, Events, Enums.");

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

            GUILayout.Space(8);
            // ── TISRebindingSystem ─────────────────────────────────────────
            H2("TISRebindingSystem — Static (JSON Persistence)");
            ApiHeader();
            ApiRow("State",                          "RebindState", "Trạng thái phiên rebinding: Idle / Listening / Done.", "purple");
            ApiRow("StartRebinding(bridge,name,tgt,cb,cancelPath)", "void", "Bắt đầu lắng nghe phím mới từ người dùng. cancelPath default = <Keyboard>/escape.", "red");
            ApiRow("CancelRebinding()",              "void",    "Huỷ phiên rebinding đang chạy. Không lưu thay đổi.", "red");
            ApiRow("ForceApply(bridge,name,tgt,path)","bool",   "Apply binding bằng code, không cần nhấn phím. true nếu thành công.", "red");
            ApiRow("ApplyPreset(bridge,dict,tgt,cb)", "int",    "Apply nhiều binding cùng lúc từ Dictionary. Chỉ rebuild một lần. Trả về số applied.", "red");
            ApiRow("GetActivePath(bridge,name,tgt)",  "string", "Lấy path đang active (override nếu có, default nếu không). Dùng hiện lên UI.", "green");
            ApiRow("GetDisplayName(path)",            "string", "Chuyển path sang tên thân thiện. VD: <Keyboard>/space → Space.", "green");
            ApiRow("FindConflict(bridge,skip,tgt,path)","string","Kiểm tra path có bị action khác dùng không. null nếu không conflict.", "green");
            ApiRow("LoadAndApply(bridge)",            "void",   "Load JSON và apply lên bridge. Gọi trong Awake() sau khi bridge khởi tạo.", "red");
            ApiRow("ResetBinding(bridge,name,tgt)",   "void",   "Reset 1 binding về default. Bridge tự rebuild.", "red");
            ApiRow("ResetAllBindings(bridge)",         "void",   "Reset toàn bộ override của profile hiện tại.", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnRebindStarted",   "event Action<string,RebindTarget>", "Fire khi StartRebinding() bắt đầu lắng nghe. Dùng hiện overlay 'Press any key...'.", "purple");
            ApiRow("OnRebindCompleted", "event Action<RebindResult>",        "Fire khi rebind kết thúc (success / cancel / fail). RebindResult: ActionName, NewPath, OldPath, Success, Cancelled.", "purple");

            GUILayout.Space(8);
            // ── TISMobileSkillButton ───────────────────────────────────────
            H2("TISMobileSkillButton — Properties & Methods");
            ApiHeader();
            ApiRow("SkillName",              "string", "Tên action bind (get only). Khớp với actionName trong profile.", "purple");
            ApiRow("HoldTime",               "float",  "Thời gian đã giữ nút (giây). Reset về 0 khi nhả.", "amber");
            ApiRow("Interactable",           "bool",   "get/set — Ưu tiên cao nhất. false: không nhận touch, visual mờ.", "purple");
            ApiRow("SetDisabled(bool)",      "void",   "Block input tạm thời + visual tối (stun, skill locked). Tự release nếu đang nhấn.", "red");
            ApiRow("SetCooldownFill(f)",     "void",   "0=sẵn sàng, 1=full cooldown. Block input + cập nhật Image fill.", "red");
            ApiRow("SetVisualMode(mode)",    "void",   "Đổi ColorTint / SpriteSwap lúc runtime.", "red");
            ApiHeader("Event", "Loại", "Khi nào fire");
            ApiRow("OnPress",      "UnityEvent",       "Fired 1 lần khi PointerDown (bắt đầu nhấn).", "purple");
            ApiRow("OnRelease",    "UnityEvent",       "Fired khi PointerUp hoặc PointerExit (nhả / trượt ra ngoài).", "purple");
            ApiRow("OnHold",       "UnityEvent",       "Fired MỖI FRAME khi đang giữ nút. Dùng cho auto-attack, charge.", "purple");
            ApiRow("OnHoldTimed",  "UnityEvent<float>","Fired mỗi frame khi giữ, param = HoldTime (giây). Dùng cho charge bar.", "purple");

            GUILayout.Space(8);
            // ── TISDirectionalSkillButton ──────────────────────────────────
            H2("TISDirectionalSkillButton — Properties & Methods");
            ApiHeader();
            ApiRow("WasCastThisFrame",   "bool",    "true đúng 1 frame khi skill vừa được cast.", "green");
            ApiRow("CastDirection",      "Vector2", "Hướng cast (normalized). zero = quick tap / auto-target.", "blue");
            ApiRow("DirectionAngle",     "float",   "Góc hướng cast (0°=phải, 90°=trên).", "amber");
            ApiRow("Direction",          "Vector2", "Direction real-time khi đang kéo (normalized). zero khi nhả.", "blue");
            ApiRow("IsDragging",         "bool",    "true khi đang kéo ngón tay (đã vượt dragThreshold).", "green");
            ApiRow("IsInCancelZone",     "bool",    "true khi ngón tay đang hover trong vùng hủy.", "green");
            ApiRow("IsDisabled",         "bool",    "true nếu button bị disable (external hoặc cooldown).", "green");
            ApiRow("SetDisabled(bool)",  "void",    "Bật/tắt disable từ game code. Tự release nếu đang hold.", "red");
            ApiRow("SetCooldownFill(f)", "void",    "0=sẵn sàng, 1=full cooldown. Tự release nếu đang hold.", "red");
            ApiRow("ForceCast(dir)",     "void",    "Force cast bằng code không cần touch (tutorial, automation).", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnDirectionCast", "event Action<string, Vector2>", "Fire khi cast thành công. Params: (skillName, direction). zero = auto-target.", "purple");
            ApiRow("OnCancelled",     "UnityEvent",                    "Fire khi nhả trong Cancel Zone — kỹ năng bị hủy.", "purple");
            ApiRow("OnDragStarted",   "UnityEvent",                    "Fire khi bắt đầu kéo vượt dragThreshold.", "purple");

            GUILayout.Space(8);
            H2("TISDirectionalRegistry — Static");
            ApiHeader();
            ApiRow("GetDirection(skillName)",           "Vector2", "Lấy direction sau cast (key=skillName+\"_Dir\"). zero nếu quick tap.", "green");
            ApiRow("SetVector2Override(name, v2)",      "void",    "Ghi đè action Vector2 real-time — bridge.GetVector2Action() đọc được.", "red");
            ApiRow("ClearVector2Override(name)",        "void",    "Xóa override action Vector2.", "red");
            ApiRow("GetVector2Override(name, out val)", "bool",    "Lấy override nếu có. true nếu tồn tại.", "green");

            GUILayout.Space(8);
            // ── TISTargetScanner ───────────────────────────────────────────
            H2("TISTargetScanner — Properties & Methods");
            Note("Gắn lên Camera GameObject. Tự động scan target gần tâm màn hình nhất theo interval. Kết hợp với AimAssistProcess() để có aim assist tự động.");
            ApiHeader();
            ApiRow("CurrentTarget",    "Transform", "Target đang được lock. null nếu không có target trong phạm vi.", "green");
            ApiRow("CurrentAimPoint",  "Vector3",   "Vị trí world-space điểm ngắm trên CurrentTarget (theo AimPointMode).", "blue");
            ApiRow("CurrentAngle",     "float",     "Góc lệch (độ) giữa tâm màn hình và target. -1 nếu không có target.", "amber");
            ApiRow("HasTarget",        "bool",      "Shortcut: CurrentTarget != null.", "green");
            ApiRow("ForceScan()",      "void",      "Buộc quét lại ngay lập tức. Dùng sau khi spawn enemy.", "red");
            ApiRow("ClearTarget()",    "void",      "Xóa CurrentTarget về null (target chết, bị destroy).", "red");

            GUILayout.Space(8);
            // ── TISMobileInputSetup ────────────────────────────────────────
            H2("TISMobileInputSetup — Properties, Methods & Events");
            ApiHeader();
            ApiRow("IsReady",              "bool",  "true sau khi toàn bộ mobile UI khởi tạo xong.", "green");
            ApiRow("BatTatXoayCamera()",   "void",  "Toggle camera rotation (batXoayCamera = !batXoayCamera).", "red");
            ApiRow("BatTatZoom()",         "void",  "Toggle pinch-to-zoom.", "red");
            ApiRow("BatTatJoystick()",     "void",  "Toggle joystick hiển thị.", "red");
            ApiRow("BatTatCameraZone()",   "void",  "Toggle camera zone.", "red");
            ApiRow("DatXoayCamera(bool)",  "void",  "Gán trực tiếp, áp dụng ngay.", "red");
            ApiRow("DatZoom(bool)",        "void",  "Gán batZoom.", "red");
            ApiRow("DatJoystick(bool)",    "void",  "Gán batJoystick.", "red");
            ApiRow("DatCameraZone(bool)",  "void",  "Gán batCamera.", "red");
            ApiRow("DatTyLeVung(float)",   "void",  "Gán tỉ lệ joystick/camera (clamp 0.25–0.75). Sync ngay.", "red");
            ApiRow("DongBoTyLe()",         "void",  "Sync tỉ lệ zone lên joystick, camera zone, gesture detector.", "red");
            ApiRow("ApDungCaiDatCamera()", "void",  "Áp dụng tất cả cài đặt camera zone từ Inspector.", "red");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("SetupCompleted", "event Action<TISMobileInputSetup>", "Fire sau khi toàn bộ mobile UI khởi tạo thành công. Param = instance.", "purple");

            GUILayout.Space(8);
            // ── TISGamepadVirtualCursor ────────────────────────────────────
            H2("TISGamepadVirtualCursor — Methods");
            Note("Gắn lên bất kỳ GameObject nào. Assign cursorTransform (RectTransform UI cursor) và parentCanvas trong Inspector. Tự đăng ký với bridge qua RegisterGamepadCursor(this) trong Awake().");
            ApiHeader();
            ApiRow("SetActiveCursor(bool)", "void", "Bật/tắt cursor. Khi bật: reset vị trí về center, sync vị trí với InputSystem. Dùng left stick gamepad để di chuyển, buttonSouth để click.", "red");

            GUILayout.Space(8);
            // ── TISGamepadUIButton ─────────────────────────────────────────
            H2("TISGamepadUIButton — Properties & Methods");
            Note("Kế thừa từ UnityEngine.UI.Button. Dùng cho UI navigation gamepad nhiều lớp (Tab → SubTab → Content). Thêm component vào Button GameObject, cấu hình trong TIS_GamepadUIButtonEditor.");
            ApiHeader();
            ApiRow("navigationMode",            "NavMode",          "FourDirectional / Horizontal / Vertical — chế độ di chuyển D-pad / stick.", "purple");
            ApiRow("inputSource",               "InputSource",      "Default (DPad+Stick) / LB_RB / LT_RT — nguồn input cho layer này.", "purple");
            ApiRow("layer",                     "NavigationLayer",  "Tab / SubTab / Content — xác định button thuộc layer nào trong hệ thống phân cấp.", "purple");
            ApiRow("holdDelay",                 "float",            "Thời gian (giây) giữa các lần di chuyển khi giữ nút (mặc định 0.2f).", "amber");
            ApiRow("navUp / navDown / navLeft / navRight", "Button", "4 button láng giềng — gán trong Inspector để tạo luồng navigation.", "purple");
            ApiRow("alwaysActive",              "bool",             "Khi true: giữ visual Highlighted dù EventSystem focus ở nơi khác. Dùng cho Tab và SubTab layer.", "green");
            ApiRow("isFirstSelected",           "bool",             "Khi true: tự nhận EventSystem focus sau 1 frame khi GameObject được kích hoạt. Chỉ dùng với Content layer.", "green");
            ApiRow("SetAsActiveInLayer()",      "void",             "Đăng ký button này là 'active' trong layer — deselect button cũ, fire onDeselect / onSelect. Gọi khi khởi tạo scene.", "red");
            ApiRow("DisableNavigation()",       "void",             "Tắt navigation — button không nhận input, không di chuyển focus. Gọi khi mở panel đè lên.", "red");
            ApiRow("EnableNavigation()",        "void",             "Bật lại navigation sau khi panel đóng.", "red");
            ApiRow("GetActiveInLayer(layer)",   "TISGamepadUIButton","static — Lấy button đang 'active' trong layer chỉ định. null nếu chưa có.", "green");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("onSelect",              "SelectEvent (UnityEvent)",                        "Fire khi button được select (EventSystem focus hoặc SetAsActiveInLayer).", "purple");
            ApiRow("onDeselect",            "SelectEvent (UnityEvent)",                        "Fire khi button bị deselect.", "purple");
            ApiRow("onBecameActiveInLayer", "LayerChangedEvent (UnityEvent<TISGamepadUIButton>)", "Fire khi button trở thành active trong layer qua global input (LT/RT hoặc LB/RB). UINavigationContext lắng nghe để redirect Content focus.", "purple");

            GUILayout.Space(8);
            H2("TISGamepadUIButton — Enums");
            CodeBlock(
                "// NavMode — chế độ điều hướng:\n" +
                "FourDirectional  // D-pad 4 hướng (mặc định)\n" +
                "Horizontal       // Chỉ trái/phải\n" +
                "Vertical         // Chỉ lên/xuống\n\n" +
                "// InputSource — nguồn input:\n" +
                "Default          // DPad + LeftStick (Content layer)\n" +
                "LB_RB            // Left/Right Shoulder (SubTab layer)\n" +
                "LT_RT            // Left/Right Trigger (Tab layer)\n\n" +
                "// NavigationLayer — thứ bậc:\n" +
                "Tab              // Lớp ngoài cùng — LT/RT, alwaysActive\n" +
                "SubTab           // Lớp giữa — LB/RB, alwaysActive\n" +
                "Content          // Lớp trong — DPad/Stick, EventSystem focus thật");

            GUILayout.Space(8);
            // ── TISMobileButtonRegistry ────────────────────────────────────
            H2("TISMobileButtonRegistry — Static");
            ApiHeader();
            ApiRow("RegisteredKeys",             "IReadOnlyList", "Danh sách tên tất cả button đã đăng ký.", "purple");
            ApiRow("SetState(name, bool)",        "void",   "Inject trạng thái button. Tự đăng ký key lần đầu.", "red");
            ApiRow("GetState(name)",              "bool",   "Đọc trạng thái Hold hiện tại.", "green");
            ApiRow("ConsumeState(name)",          "bool",   "Đọc + reset (true 1 frame — GetKeyDown equivalent).", "green");
            ApiRow("WasPressedThisFrame(name)",   "bool",   "true đúng 1 frame khi nhấn. Không mutate _states.", "green");
            ApiRow("WasReleasedThisFrame(name)",  "bool",   "true đúng 1 frame khi nhả ngón tay.", "green");
            ApiRow("AnyPressed()",                "bool",   "true nếu có bất kỳ button nào đang pressed (debug).", "green");
            ApiRow("FlushFrame()",                "void",   "Snapshot _states→_prevStates cuối frame. Gọi bởi LateUpdate.", "red");
            ApiRow("ClearAll()",                  "void",   "Reset toàn bộ registry (SwapProfile, scene change).", "red");

            GUILayout.Space(8);
            // ── Enums ──────────────────────────────────────────────────────
            H2("Enums");
            CodeBlock(
                "// TISButtonReadMode:\n" +
                "PressDown   // true đúng 1 frame khi nhấn (GetKeyDown) — mặc định cho button\n" +
                "Hold        // true suốt khi đang giữ (GetKey)\n" +
                "PressUp     // true đúng 1 frame khi nhả (GetKeyUp)\n\n" +
                "// RebindTarget:\n" +
                "PC          // Keyboard + Mouse\n" +
                "Gamepad     // Controller\n\n" +
                "// AimAssistPlatform:\n" +
                "None        // Tắt hoàn toàn\n" +
                "PC          // Keyboard + Mouse\n" +
                "Gamepad     // Controller\n" +
                "Mobile      // Touchscreen\n" +
                "All         // PC | Gamepad | Mobile\n\n" +
                "// TISTargetScanner.AimPointMode:\n" +
                "Center          // Gốc Transform của target\n" +
                "ColliderCenter  // Tâm Bounds của Collider (default)\n" +
                "Custom          // Offset tùy chỉnh (_aimPointOffset)\n\n" +
                "// TISMobileSkillButton.VisualMode:\n" +
                "ColorTint   // Tint màu Image theo trạng thái\n" +
                "SpriteSwap  // Đổi Sprite theo trạng thái");

            GUILayout.Space(8);
            H2("Code Ví Dụ Nhanh — Đọc Input Cơ Bản");
            CodeBlock(
                "var bridge = TISInputBridge.Instance;\n\n" +
                "// Movement\n" +
                "Vector2 move = bridge.MoveDirection;\n" +
                "Vector2 look = bridge.LookDelta;\n" +
                "float   zoom = bridge.ZoomDelta;\n\n" +
                "// Shortcuts\n" +
                "if (bridge.IsJumping)   DoJump();\n" +
                "if (bridge.IsRunning)   SetSpeed(runSpeed);\n" +
                "if (bridge.IsAttacking) DoAttack();\n\n" +
                "// Custom actions\n" +
                "if (bridge.GetSkillAction(\"Skill1\"))          CastSkill1();\n" +
                "if (bridge.IsSkillDown(\"Dodge\"))              DodgeRoll();\n" +
                "if (bridge.IsSkillHeld(\"Charge\"))             ContinueCharge();\n" +
                "if (bridge.IsSkillReleased(\"Charge\"))         ReleaseCharge();\n\n" +
                "// Rumble\n" +
                "bridge.TryRumble(0.6f, 0.3f, 0.2f); // duration\n" +
                "bridge.StopRumble();\n\n" +
                "// Rebinding\n" +
                "TISRebindingSystem.LoadAndApply(bridge);         // Awake()\n" +
                "TISRebindingSystem.StartRebinding(bridge, \"Jump\", RebindTarget.PC, result => {});\n" +
                "string key = TISRebindingSystem.GetDisplayName(TISRebindingSystem.GetActivePath(bridge, \"Jump\", RebindTarget.PC));");

            EndContentScroll();
        }

        // ══════════════════════════════════════════════════════
        //  TAB 10 — FAQ
        // ══════════════════════════════════════════════════════

        private void DrawTab_FAQ()
        {
            BeginContentScroll("faq");
            PageTitle("FAQ & Xử Lý Lỗi", "Các lỗi thường gặp và cách khắc phục nhanh.");

            FaqItem(0, "GetSkillAction() luôn trả về false dù đã nhấn phím?",
                "Kiểm tra lần lượt:\n" +
                "1. actionName trong code có khớp với profile không? (case-insensitive nhưng phải đúng tên)\n" +
                "2. Mở tab Debug của TISInputBridge → nhấn phím → cột Active có chuyển YES không?\n" +
                "3. Action Map có enabledByDefault = true không? Nếu false → gọi SetActionMapActive() để bật.\n" +
                "4. bindingPC có đúng path không? Dùng dropdown trong Setup Tool thay vì gõ tay.\n" +
                "5. Simulate Mobile In Editor có đang bật không? Nếu bật, keyboard không trigger nữa.");

            FaqItem(1, "Joystick mobile xuất hiện sai vị trí / bay ra ngoài màn hình?",
                "Nguyên nhân thường là anchor mismatch.\n" +
                "TISVirtualJoystick v6 đã fix bằng cách đặt anchor CENTER trong Awake().\n" +
                "Nếu dùng prefab joystick tự tạo: đảm bảo RectTransform anchor = center, pivot = center.");

            FaqItem(2, "TISMobileSkillButton inject nhưng GetSkillAction() không đọc được?",
                "GetSkillAction() chỉ check TISMobileButtonRegistry khi ĐỒNG THỜI:\n" +
                "(1) Platform là mobile HOẶC _simulateMobileInEditor = true trong Inspector, VÀ\n" +
                "(2) action đó có bindingMobile không trống trong profile.\n" +
                "Nếu bindingMobile trống → hàm đọc InputAction thật thay vì registry.");

            FaqItem(3, "IsJumping không nhận từ TISMobileSkillButton?",
                "IsJumping đọc InputAction trực tiếp, không qua registry.\n" +
                "Cần patch thêm trong TISInputBridge.cs — xem tab Nâng Cao.");

            FaqItem(4, "MaxMobileButtons không đủ — cần nhiều hơn 16 button?",
                "Tăng MaxMobileButtons trong TISBindingPathOptions.cs lên 24, 32, 48...\n" +
                "Runtime không bị giới hạn — TISMobileButtonRegistry dùng Dictionary<string, bool>.\n" +
                "ButtonByIndex(20) → \"Button_20\" hoàn toàn hợp lệ.");

            FaqItem(5, "SwapProfile bị mất trạng thái button mobile?",
                "Đây là hành vi đúng — SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll()\n" +
                "để tránh button bị stuck pressed sau khi đổi profile.\n" +
                "Nếu muốn giữ state, chỉ dùng SetActionMapActive() thay vì SwapProfile().");

            FaqItem(6, "Rumble tay cầm không hoạt động?",
                "Kiểm tra lần lượt:\n" +
                "1. enableRumble = true trong TISInputProfileConfig → tab Gamepad\n" +
                "2. Gamepad.current != null (người chơi chưa kết nối tay cầm)\n" +
                "3. Gọi đúng TryRumble() qua TISInputBridge.Instance\n" +
                "4. Gọi StopRumble() trong OnDisable() / OnApplicationQuit()\n" +
                "Rumble không hoạt động trên mobile — enableRumble = false trong profile mobile.");

            FaqItem(7, "Pinch zoom và kéo camera bị conflict?",
                "TISTouchCameraZone v3+ đã xử lý: pinch chỉ nhận khi CẢ 2 ngón ở trong camera zone.\n" +
                "Ngón joystick (nửa trái) bị loại trừ hoàn toàn.\n" +
                "Khi đang pinch, camera rotation tự tạm dừng.");

            FaqItem(8, "Rebinding không lưu sau khi thoát game?",
                "Phải gọi TISRebindingSystem.LoadAndApply(bridge) trong Awake() sau khi TISInputBridge khởi tạo.\n" +
                "Rebinding tự lưu vào JSON sau mỗi lần rebind thành công — không cần gọi Save() thủ công.\n" +
                "File JSON ở: Application.persistentDataPath/TIS_Bindings_{profileName}.json");

            FaqItem(9, "TISDirectionalSkillButton không fire OnDirectionCast?",
                "Kiểm tra:\n" +
                "1. CastMode = DragRelease: cần kéo đủ dragThresholdPx (mặc định 40px) rồi nhả.\n" +
                "   Quick tap dưới quickTapMaxSeconds → cast với direction = Vector2.zero.\n" +
                "2. Ngón tay không vào Cancel Zone khi nhả (Cancel Zone hủy cast).\n" +
                "3. IsDisabled = false và SetCooldownFill = 0 (sẵn sàng).\n" +
                "4. Script subscribe đúng event: btn.OnDirectionCast += (name, dir) => {}");

            FaqItem(10, "TISTargetScanner không tìm được target?",
                "Kiểm tra lần lượt:\n" +
                "1. Component gắn lên Camera GameObject? (yêu cầu RequireComponent(typeof(Camera)))\n" +
                "2. targetTags khớp với tag của enemy? Hoặc bật useLayerInsteadOfTag và chọn đúng Layer.\n" +
                "3. requireLineOfSight = true? Kiểm tra obstacleLayers không chặn tầm nhìn.\n" +
                "4. scanAngle đủ rộng chưa? Thử tăng lên 15–20° để test.\n" +
                "5. maxDistance đủ lớn? 0 = không giới hạn.\n" +
                "6. Gọi ForceScan() sau khi spawn enemy để quét ngay không chờ interval.");

            FaqItem(11, "TISMobileInputSetup không khởi tạo trên PC build?",
                "Đây là hành vi đúng — TISMobileInputSetup tự SetActive(false) trên non-mobile.\n" +
                "Trong Editor nó luôn active để test.\n" +
                "Nếu muốn test trên PC: bật Simulate Mobile In Editor trong TISInputBridge Inspector.");

            FaqItem(12, "Gamepad Virtual Cursor không di chuyển?",
                "Kiểm tra:\n" +
                "1. cursorTransform và parentCanvas đã assign trong Inspector.\n" +
                "2. Đã gọi SetActiveGamepadCursor(true).\n" +
                "3. Gamepad.current != null (tay cầm đang kết nối).\n" +
                "4. EventSystem trong scene có InputSystemUIInputModule không?\n" +
                "   Cursor dùng QueueDeltaStateEvent → cần InputSystem UI Module.\n" +
                "5. cursorTransform.gameObject.SetActive(true) — cursor image có đang visible không?");

            FaqItem(13, "Interactable và SetDisabled khác nhau như thế nào?",
                "Interactable (property get/set) — ưu tiên CAO NHẤT:\n" +
                "  false = button hoàn toàn bị khóa, không nhận touch, visual mờ nhất.\n" +
                "  Dùng khi: nhân vật chết, stage lock, chưa unlock skill.\n\n" +
                "SetDisabled(bool) — ưu tiên THẤP HƠN Interactable:\n" +
                "  Chặn input tạm thời, có thể bật lại sau (cooldown, stun, charge).\n" +
                "  SetCooldownFill() tự gọi SetDisabled(true) khi fill < 1.\n\n" +
                "Thứ tự visual: !Interactable > Disabled > Pressed > Normal.\n" +
                "Nếu Interactable=false thì Disabled bị bỏ qua hoàn toàn.");

            FaqItem(14, "IsSkillDown / IsSkillHeld / IsSkillReleased dùng khi nào?",
                "Đây là shortcut methods, tương đương GetSkillAction với mode tường minh:\n" +
                "  IsSkillDown(name)     → GetSkillAction(name, PressDown) — 1 frame khi nhấn\n" +
                "  IsSkillHeld(name)     → GetSkillAction(name, Hold)      — true suốt khi giữ\n" +
                "  IsSkillReleased(name) → GetSkillAction(name, PressUp)   — 1 frame khi nhả\n\n" +
                "Dùng khi cần mode khác với readMode đã config trong profile,\n" +
                "hoặc khi cần cả 3 event trên cùng 1 action (charge mechanic).");

            FaqItem(15, "Làm sao đổi giao diện button mobile lúc runtime?",
                "Dùng SetVisualMode(VisualMode mode) để chuyển giữa ColorTint và SpriteSwap:\n" +
                "    btn.SetVisualMode(TISMobileSkillButton.VisualMode.SpriteSwap);\n" +
                "    btn.SetVisualMode(TISMobileSkillButton.VisualMode.ColorTint);\n\n" +
                "Dùng khi: đổi skin nhân vật, chuyển theme UI, test A/B.\n" +
                "Hàm tự gọi UpdateVisual() ngay sau khi đổi mode.");

            FaqItem(16, "TISGamepadUIButton không di chuyển focus sang button khác?",
                "Kiểm tra lần lượt:\n" +
                "1. navUp / navDown / navLeft / navRight đã assign trong Inspector chưa?\n" +
                "2. isNavigationEnabled = true? Nếu DisableNavigation() đã gọi → gọi EnableNavigation() để bật lại.\n" +
                "3. layer = Content và button đang có EventSystem focus không? Dùng Debug → currentSelectedGameObject.\n" +
                "4. Gamepad.current != null (tay cầm đang kết nối).\n" +
                "5. holdDelay quá cao? Thử giảm xuống 0.15f.\n" +
                "6. Button đích có activeInHierarchy = true không? Inactive button bị bỏ qua.");

            FaqItem(17, "Tab / SubTab không chuyển khi nhấn LT/RT hoặc LB/RB?",
                "Tab và SubTab layer dùng alwaysActive = true và xử lý input trong HandleGlobalLayerInput().\n" +
                "Kiểm tra:\n" +
                "1. alwaysActive = true trong button Tab/SubTab.\n" +
                "2. SetAsActiveInLayer() đã được gọi khi khởi tạo scene (thường từ UINavigationContext).\n" +
                "3. inputSource đúng: Tab dùng LT_RT, SubTab dùng LB_RB.\n" +
                "4. navigationMode phù hợp: Horizontal nếu Tab nằm ngang, Vertical nếu dọc.\n" +
                "5. Button tab đích (navLeft/navRight) có activeInHierarchy = true không?");

            GUILayout.Space(8);
            Note("Mở cửa sổ Documentation từ trong Unity: Tools → True Input System → Documentation. Hoặc bấm nút ? trong Setup Tool.");

            EndContentScroll();
        }

        private void FaqItem(int id, string question, string answer)
        {
            bool isOpen = _faqOpen.Contains(id);

            // Question bar
            Rect qRect = GUILayoutUtility.GetRect(0, 36);
            EditorGUI.DrawRect(qRect, isOpen ? BgElevated : BgSurface);
            EditorGUI.DrawRect(new Rect(qRect.x, qRect.y, isOpen ? 3 : 1, qRect.height),
                               isOpen ? AccentBlue : BorderSub);
            EditorGUI.DrawRect(new Rect(qRect.x, qRect.yMax - 1, qRect.width, 1), BorderSub);

            // Toggle arrow
            var arrSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = isOpen ? AccentBlue : TxtMuted },
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 12, 0, 0),
            };
            GUI.Label(new Rect(qRect.xMax - 30, qRect.y, 30, qRect.height), isOpen ? "▲" : "▼", arrSt);

            // Question text
            var qSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, fontStyle = isOpen ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = isOpen ? TxtPrimary : TxtSecond },
                padding = new RectOffset(12, 36, 0, 0),
            };
            GUI.Label(new Rect(qRect.x, qRect.y, qRect.width - 30, qRect.height), "Q: " + question, qSt);

            // Click handler
            if (Event.current.type == EventType.MouseDown && qRect.Contains(Event.current.mousePosition))
            {
                if (isOpen) _faqOpen.Remove(id);
                else _faqOpen.Add(id);
                Event.current.Use();
                Repaint();
            }

            // Answer
            if (isOpen)
            {
                Rect aRect = GUILayoutUtility.GetRect(0, 0);
                var aSt = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12, richText = true, wordWrap = true,
                    normal = { textColor = TxtSecond, background = Tex(FaqAnswerBg) },
                    padding = new RectOffset(18, 14, 10, 12),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                var content = new GUIContent("A: " + answer);
                float h = aSt.CalcHeight(content, aRect.width);
                Rect answerRect = GUILayoutUtility.GetRect(aRect.width, h);
                EditorGUI.DrawRect(new Rect(answerRect.x, answerRect.y, 3, answerRect.height), AccentBlue);
                EditorGUI.DrawRect(new Rect(answerRect.x, answerRect.yMax - 1, answerRect.width, 1), BorderSub);
                GUI.Label(answerRect, content, aSt);
            }

            GUILayout.Space(2);
        }  // FaqItem

    }  // partial class TISDocWindow
}
#endif