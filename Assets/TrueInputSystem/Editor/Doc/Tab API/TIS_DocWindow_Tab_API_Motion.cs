// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_API.Motion.cs
//  Tab API Reference — module: Gyro & Touchpad (TIS Motion)
//    TISInputBridge (motion API), binding <TISMotion>, TISMotionSystem,
//    TISGyroProcessor, ITISMotionSource, enum, TISPhysicalMouse,
//    TISTouchCameraZone (pointer ownership), TISUINavigationContext.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        private void Draw_API_Motion()
        {
            GUILayout.Space(8);
            // ── Bật / tắt ────────────────────────────────────────────────
            H2("TISInputBridge — Gyro & Touchpad: Bật / Tắt");
            Note("Gyro / touchpad của một action chạy khi CẢ 3 mức đều bật: toàn game (GyroEnabled) → profile (SetProfileMotion) → action (SetActionMotion). " +
                 "Hiệu lực ngay, không rebuild profile. save: true = lưu lựa chọn người chơi vào persistentDataPath/TIS_Motion_{profile}.json, tự áp lại khi load / swap profile.");
            ApiHeader();
            ApiRow("GyroEnabled",                                   "bool",  "Công tắc gyro TOÀN GAME lúc chạy (get / set). Mặc định true. Không lưu.", "green");
            ApiRow("TouchpadEnabled",                               "bool",  "Công tắc touchpad TOÀN GAME lúc chạy (get / set). Mặc định true. Không lưu.", "green");
            ApiRow("ProfileGyroEnabled",                            "bool",  "Gyro của profile đang chạy (Motion Settings › Enable Gyro, sau override).", "green");
            ApiRow("ProfileTouchpadEnabled",                        "bool",  "Touchpad của profile đang chạy (Motion Settings › Enable Touchpad, sau override).", "green");
            ApiRow("SetProfileMotion(gyro, touchpad, save=false)",  "void",  "Bật / tắt gyro + touchpad cho mọi action của profile đang chạy.", "red");
            ApiRow("SetProfileGyroEnabled(on, save=false)",         "void",  "Chỉ đổi gyro của profile.", "red");
            ApiRow("SetProfileTouchpadEnabled(on, save=false)",     "void",  "Chỉ đổi touchpad của profile.", "red");
            ApiRow("SetActionMotion(action, gyro, touchpad, save=false)", "void", "Bật / tắt gyro + touchpad của MỘT action (vd. tắt gyro của \"Look\"). Áp dụng cho cả \"+ Gyro / + Touchpad\" lẫn binding <TISMotion>/... riêng.", "red");
            ApiRow("SetActionGyroEnabled(action, on, save=false)",  "void",  "Chỉ đổi gyro của một action.", "red");
            ApiRow("SetActionTouchpadEnabled(action, on, save=false)", "void", "Chỉ đổi touchpad của một action.", "red");
            ApiRow("IsActionGyroEnabled(action)",                   "bool",  "Gyro của action có THỰC SỰ chạy: có binding gyro và cả 3 mức đều bật.", "green");
            ApiRow("IsActionTouchpadEnabled(action)",               "bool",  "Như trên, cho touchpad.", "green");
            ApiRow("ActionHasGyro(action)",                         "bool",  "Action có binding gyro không (<TISMotion>/gyro hoặc \"+ Gyro\").", "green");
            ApiRow("ActionHasTouchpad(action)",                     "bool",  "Action có binding touchpad không (<TISMotion>/touchpad* hoặc \"+ Touchpad\").", "green");
            ApiRow("ResetMotionSettings(deleteSaved=true)",         "void",  "Trả lựa chọn theo profile / action về cấu hình gốc của profile asset; xoá file đã lưu.", "red");
            ApiRow("ShowMotionDebugGUI",                            "bool",  "Bật bảng debug gyro / touchpad lúc chạy (OR với Motion Settings › Debug GUI). Editor + Development Build.", "green");

            GUILayout.Space(8);
            H2("TISInputBridge — Gyro & Touchpad: Truy Cập");
            ApiHeader();
            ApiRow("Motion",                         "TISMotionSystem", "Hệ thống gyro / touchpad: giá trị đã xử lý, calibration, source đang dùng.", "purple");
            ApiRow("MotionDevice",                   "TISMotionDevice", "Thiết bị ảo <TISMotion> trong Input System. null khi profile không bind gyro / touchpad.", "purple");
            ApiRow("RegisterMotionSource(source)",   "void",            "Đăng ký provider tự viết (vd. console). Source CONNECTED có Priority cao nhất được dùng.", "red");
            ApiRow("UnregisterMotionSource(source)", "void",            "Gỡ provider.", "red");
            ApiRow("GetGamepadCursorEnable()",       "bool",            "Gamepad Virtual Cursor có đang bật không.", "green");
            ApiRow("AimAssistProcess(settings,cam,Transform target,look,intensity)", "Vector2", "Overload nhận Transform: null = không có mục tiêu (overload Vector3 coi Vector3.zero là không có mục tiêu).", "blue");

            GUILayout.Space(8);
            H2("TISInputBridge — Events (Motion)");
            ApiHeader("Event", "Signature", "Khi nào fire");
            ApiRow("OnMotionSettingsChanged", "event Action", "Sau mọi lời gọi SetProfile… / SetAction… / ResetMotionSettings — cập nhật UI Settings.", "purple");

            GUILayout.Space(8);
            // ── Binding paths ────────────────────────────────────────────
            H2("Binding Paths — <TISMotion> (Setup Tool › Binding Gamepad)");
            Note("Gyro / touchpad (đọc qua SDL3 trên PC) được đưa vào Input System bằng thiết bị ảo <TISMotion>. " +
                 "Action \"Look\": TIS tự áp Gyro Sensitivity / Touchpad Look Sensitivity. Action khác nhận đơn vị thô.");
            ApiHeader("Path", "Kiểu", "Giá trị");
            ApiRow("<TISMotion>/gyro",             "Vector2", "Tốc độ ngắm, độ/giây (x = quay phải, y = nhìn lên). Đã lọc, calibration, invert, activation.", "blue");
            ApiRow("<TISMotion>/gyro/x  ·  /gyro/y","Axis",   "Từng trục của gyro.", "amber");
            ApiRow("<TISMotion>/touchpad",         "Vector2", "Tốc độ vuốt touchpad (bề rộng touchpad / giây). 0 khi không chạm.", "blue");
            ApiRow("<TISMotion>/touchpad/x  ·  /y","Axis",    "Từng trục tốc độ vuốt.", "amber");
            ApiRow("<TISMotion>/touchpadPosition", "Vector2", "Vị trí ngón trên touchpad (0..1, gốc dưới-trái).", "blue");
            ApiRow("<TISMotion>/touchpadTouch",    "Button",  "Đang chạm touchpad.", "green");
            ApiRow("<TISMotion>/touchpadPress",    "Button",  "Đang nhấn (click) touchpad.", "green");
            ApiRow("\"+ Gyro\" / \"+ Touchpad\"",  "Vector2", "Ô dưới binding Left / Right Stick: action = cần + gyro + touchpad (composite TISStickMotion).", "blue");

            GUILayout.Space(8);
            // ── TISMotionSystem ──────────────────────────────────────────
            H2("TISMotionSystem — Properties  (TISInputBridge.Instance.Motion)");
            ApiHeader();
            ApiRow("Source",                     "ITISMotionSource", "Provider đang dùng (SDL3 / Simulated / tự viết). null nếu không có.", "purple");
            ApiRow("Sources",                    "IReadOnlyList",    "Mọi provider đã đăng ký.", "purple");
            ApiRow("GyroProcessor",              "TISGyroProcessor", "Bộ xử lý gyro — calibration, offset, debug.", "purple");
            ApiRow("HasGyro / HasTouchpad",      "bool",    "Source hiện tại có gyro / touchpad và công tắc tương ứng đang bật.", "green");
            ApiRow("GyroRate",                   "Vector2", "Tốc độ gyro đã xử lý (độ/giây), KHÔNG phụ thuộc activation.", "blue");
            ApiRow("GyroAim",                    "Vector2", "Tốc độ dùng để ngắm (= giá trị <TISMotion>/gyro): đã invert + activation, 0 khi virtual cursor bật.", "blue");
            ApiRow("IsGyroAimActive",            "bool",    "Gyro đang được dùng để ngắm frame này.", "green");
            ApiRow("IsTouching",                 "bool",    "Có ngón trên touchpad.", "green");
            ApiRow("TouchpadPosition",           "Vector2", "Vị trí ngón (0..1).", "blue");
            ApiRow("TouchpadDelta",              "Vector2", "Delta vị trí so với frame trước (chuẩn hoá).", "blue");
            ApiRow("TouchpadVelocity",           "Vector2", "Tốc độ vuốt (/giây) — giá trị <TISMotion>/touchpad.", "blue");
            ApiRow("TouchpadPressed",            "bool",    "Đang nhấn touchpad.", "green");
            ApiRow("TouchpadPressedThisFrame",   "bool",    "Vừa nhấn touchpad frame này.", "green");
            ApiRow("TouchpadReleasedThisFrame",  "bool",    "Vừa nhả touchpad frame này.", "green");
            ApiRow("BeginGyroCalibration()",     "void",    "Bắt đầu calibration thủ công — người chơi đặt tay cầm yên.", "red");
            ApiRow("EndGyroCalibration()",       "bool",    "Kết thúc, áp offset trung bình. false nếu chưa có mẫu.", "red");
            ApiRow("OnActiveSourceChanged",      "event",   "Action<ITISMotionSource> — source đang dùng thay đổi (cắm / rút tay cầm).", "purple");

            GUILayout.Space(8);
            H2("TISGyroProcessor  (Motion.GyroProcessor)");
            ApiHeader();
            ApiRow("Offset",                   "Vector3", "Drift offset hiện tại (độ/giây).", "blue");
            ApiRow("IsCalibrated",             "bool",    "Đã có ít nhất một lần calibration (tự động hoặc thủ công).", "green");
            ApiRow("IsStill",                  "bool",    "Tay cầm đang đứng yên đủ lâu để tự hiệu chỉnh.", "green");
            ApiRow("IsManualCalibrating",      "bool",    "Đang calibration thủ công.", "green");
            ApiRow("SetOffset(v3)",            "void",    "Đặt offset trực tiếp (vd. khôi phục từ save data).", "red");

            GUILayout.Space(8);
            H2("ITISMotionSource — Provider Tự Viết (vd. PS4 / PS5 / Switch SDK)");
            Note("Implement interface rồi gọi TISInputBridge.Instance.RegisterMotionSource(this). Quy ước trục: X phải, Y lên, Z về phía người chơi; gyro độ/giây (quy tắc bàn tay phải); accel đơn vị g; touchpad 0..1 gốc dưới-trái.");
            ApiHeader();
            ApiRow("Name",                   "string",  "Tên hiển thị (debug).", "purple");
            ApiRow("Priority",               "int",     "Simulated = 0, SDL3 = 100, Console = 200. Cao nhất & đang kết nối được dùng.", "amber");
            ApiRow("IsConnected",            "bool",    "Có thiết bị hợp lệ để đọc.", "green");
            ApiRow("HasMotion / HasTouchpad","bool",    "Thiết bị có gyro + accel / touchpad.", "green");
            ApiRow("MaxTouchPoints",         "int",     "Số điểm chạm tối đa (DS4 / DualSense = 2).", "amber");
            ApiRow("Tick(dt)",               "void",    "Poll dữ liệu — TIS gọi 1 lần mỗi frame.", "red");
            ApiRow("Gyro / Accel",           "Vector3", "Gyro thô (độ/giây, chưa trừ offset) / gia tốc (g).", "blue");
            ApiRow("TryGetTouch(i, out p)",  "bool",    "Đọc điểm chạm thứ i (TISTouchPoint: id, active, position).", "green");
            ApiRow("TouchpadPressed",        "bool",    "Touchpad đang được nhấn.", "green");

            GUILayout.Space(8);
            H2("Enums — Motion Settings");
            ApiHeader("Enum", "Giá trị", "Ý nghĩa");
            ApiRow("TISGyroActivation", "Always",                  "Gyro luôn dùng để ngắm.", "amber");
            ApiRow("",                  "WhileActionHeld",         "Chỉ khi giữ action (vd. \"ADS\").", "amber");
            ApiRow("",                  "DisabledWhileActionHeld", "Tắt khi giữ action (nút \"nhấc tay\" chỉnh lại tư thế cầm).", "amber");
            ApiRow("TISGyroSpace",      "Local",                   "Yaw theo trục Y tay cầm (cầm nằm ngang).", "amber");
            ApiRow("",                  "Player",                  "Yaw theo trục trọng lực — xoay hay nghiêng tay cầm đều quay ngang được.", "amber");

            GUILayout.Space(8);
            // ── Khác ────────────────────────────────────────────────────
            H2("TISPhysicalMouse (static)");
            Note("Dùng thay Mouse.current khi cần CHUỘT THẬT — Gamepad Virtual Cursor tạo chuột ảo nên Mouse.current có thể là chuột ảo.");
            ApiHeader();
            ApiRow("Current",                "Mouse", "Chuột thật đang dùng (không bao giờ là chuột ảo). null nếu không có.", "purple");
            ApiRow("IsVirtual(device)",      "bool",  "Thiết bị có phải chuột ảo đã đăng ký.", "green");
            ApiRow("RegisterVirtual(device)","void",  "Đánh dấu thiết bị ảo (Virtual Cursor tự gọi).", "red");

            GUILayout.Space(8);
            H2("TISTouchCameraZone — Pointer Ownership (Mobile)");
            Note("Mỗi ngón một chủ: EventSystem giao ngón lúc chạm xuống; ngón thuộc camera zone tới khi nhấc tay, kéo đi đâu cũng vậy. Vị trí đọc trực tiếp từ Touchscreen mỗi frame (không trễ).");
            ApiHeader();
            ApiRow("Current",                      "static",  "Camera zone đang bật gần nhất.", "purple");
            ApiRow("PointerCount",                 "int",     "Số ngón đang thuộc camera zone.", "amber");
            ApiRow("TryGetPointer(i, out id, out pos, out prev)", "bool", "Đọc ngón thứ i (0 = ngón chạm sớm nhất, dùng để xoay).", "green");
            ApiRow("AddPointer(eventData)",        "bool",    "Giao ngón cho camera (OnPointerDown). Nút skill bật On Touch Camera tự gọi.", "red");
            ApiRow("MovePointer(id, pos)",         "void",    "Cập nhật vị trí từ OnDrag (chỉ dùng với EventSystem legacy).", "red");
            ApiRow("RemovePointer(id)",            "void",    "Trả ngón (OnPointerUp).", "red");
            ApiRow("OwnsPointer(id)",              "bool",    "Ngón có đang thuộc camera zone.", "green");

            GUILayout.Space(8);
            H2("TISUINavigationContext");
            ApiHeader();
            ApiRow("RestoreFocusIfLost()", "void", "Focus lại Content button (last → defaultContent) nếu selection hiện tại không còn là button của context (vd. sau khi tắt virtual cursor).", "red");
        }

    }  // partial class TISDocWindow
}
#endif
