// ============================================================
//  TrueInputSystem Tutorials — TISTutProfiles.cs (Editor)
//
//  Tạo các Input Profile phục vụ scene hướng dẫn (Tutorials/Profiles/):
//    TIS_Tut_KeyboardMouse      — đi bộ: di chuyển, camera, nút, tổ hợp phím, action map...
//    TIS_Tut_KeyboardMouse_Car  — lái xe: dùng cho bài SwapProfile
//    TIS_Tut_Gamepad            — tay cầm: cần analog, cò, rung, gyro + touchpad
//    TIS_Tut_Mobile             — cảm ứng: joystick ảo, vùng camera, nút kỹ năng, kỹ năng định hướng
//
//  Mỗi lần chạy builder, nội dung action map được ghi lại cho khớp với bài học.
// ============================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials.Editor
{
    public static class TISTutProfiles
    {
        public const string Dir         = TISTutAssets.Root + "Profiles/";
        public const string KbmPath     = Dir + "TIS_Tut_KeyboardMouse.asset";
        public const string KbmCarPath  = Dir + "TIS_Tut_KeyboardMouse_Car.asset";
        public const string GamepadPath = Dir + "TIS_Tut_Gamepad.asset";
        public const string MobilePath  = Dir + "TIS_Tut_Mobile.asset";

        const TISButtonReadMode Down = TISButtonReadMode.PressDown;
        const TISButtonReadMode Hold = TISButtonReadMode.Hold;
#if TIS_TUTORIAL_DEV   // menu chỉ dành cho người bảo trì bộ hướng dẫn

        [MenuItem("Tools/True Input System/Tutorials/Tạo profile hướng dẫn", priority = 65)]
#endif
        public static void EnsureAll()
        {
            EnsureKeyboardMouse();
            EnsureKeyboardMouseCar();
            EnsureGamepad();
            EnsureMobile();
            AssetDatabase.SaveAssets();
        }

        // ══════════════════════════════════════════════════════
        //  CHUỘT + BÀN PHÍM
        // ══════════════════════════════════════════════════════

        public static TISInputProfileConfig EnsureKeyboardMouse()
        {
            var p = LoadOrCreate(KbmPath);
            p.profileName = "TIS Tut · Chuột + Phím";
            p.description = "Profile cho scene hướng dẫn Chuột + Bàn phím (đi bộ).";
            p.enablePC = true; p.enableGamepad = true; p.enableMobile = false;
            p.cameraSettings.mouseSensitivity = new Vector2(1.5f, 1.5f);
            p.cameraSettings.pitchMin = -60f;
            p.cameraSettings.pitchMax = 35f;

            // Không dùng gyro / touchpad → tắt (giống mặc định của profile mới)
            p.motionSettings.enableGyro = false;
            p.motionSettings.enableTouchpad = false;

            p.actionMaps = new List<TISActionMapData>
            {
                Map("Movement",
                    Vec("Move", "<Keyboard>/wasd", "<Gamepad>/leftStick"),
                    Btn("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth", Down),
                    Btn("Run",  "<Keyboard>/leftShift", "<Gamepad>/leftStickPress", Hold)),
                Map("Camera",
                    Vec("Look", "<Mouse>/delta", "<Gamepad>/rightStick")),
                Map("Combat",
                    Btn("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger", Down)),
                Map("Actions",
                    Btn("Interact",  "<Keyboard>/f", "<Gamepad>/buttonWest", Down),
                    Btn("QuickSave", "<Keyboard>/s", "", Down, pcModifier: "<Keyboard>/leftCtrl"),
                    Btn("Autopilot", "<Keyboard>/t", "<Gamepad>/buttonNorth", Down),
                    Btn("BufferToggle", "<Keyboard>/b", "", Down),
                    Axis("Scroll", "<Mouse>/scroll/y", "")),
                Map("UI",
                    Btn("Pause", "<Keyboard>/escape", "<Gamepad>/start", Down)),
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        public static TISInputProfileConfig EnsureKeyboardMouseCar()
        {
            var p = LoadOrCreate(KbmCarPath);
            p.profileName = "TIS Tut · Lái xe";
            p.description = "Profile lái xe — bài hướng dẫn SwapProfile (Chuột + Bàn phím).";
            p.enablePC = true; p.enableGamepad = true; p.enableMobile = false;
            p.cameraSettings.mouseSensitivity = new Vector2(1.2f, 1.2f);
            p.cameraSettings.pitchMin = -40f;
            p.cameraSettings.pitchMax = 20f;

            // Không dùng gyro / touchpad → tắt (giống mặc định của profile mới)
            p.motionSettings.enableGyro = false;
            p.motionSettings.enableTouchpad = false;

            p.actionMaps = new List<TISActionMapData>
            {
                Map("Driving",
                    Vec("Move", "<Keyboard>/wasd", "<Gamepad>/leftStick"),
                    Btn("Boost",   "<Keyboard>/leftShift", "<Gamepad>/rightTrigger", Hold),
                    Btn("Horn",    "<Keyboard>/h", "<Gamepad>/buttonNorth", Down),
                    Btn("ExitCar", "<Keyboard>/f", "<Gamepad>/buttonWest", Down)),
                Map("Camera",
                    Vec("Look", "<Mouse>/delta", "<Gamepad>/rightStick")),
                Map("UI",
                    Btn("Pause", "<Keyboard>/escape", "<Gamepad>/start", Down)),
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        // ══════════════════════════════════════════════════════
        //  TAY CẦM
        // ══════════════════════════════════════════════════════

        public static TISInputProfileConfig EnsureGamepad()
        {
            var p = LoadOrCreate(GamepadPath);
            p.profileName = "TIS Tut · Tay cầm";
            p.description = "Profile cho scene hướng dẫn Tay cầm: cần analog, cò, rung, gyro, touchpad.";
            p.enablePC = true; p.enableGamepad = true; p.enableMobile = false;

            p.cameraSettings.gamepadSensitivity = new Vector2(0.25f, 0.25f);
            p.cameraSettings.pitchMin = -60f;
            p.cameraSettings.pitchMax = 35f;

            var gs = p.gamepadSettings;
            gs.leftStickDeadzone  = 0.15f;
            gs.rightStickDeadzone = 0.12f;
            gs.stickCurve         = 1.5f;
            gs.enableRumble       = true;

            var ms = p.motionSettings;
            ms.enableGyro              = true;
            ms.gyroActivation          = TISGyroActivation.Always;
            // LookDelta tay cầm = giá trị × gamepadSensitivity mỗi frame; camera mẫu đổi 1 đơn vị = 720°/s
            // → gyro 0.002 ≈ xoay camera 1.5 lần góc xoay tay cầm.
            ms.gyroSensitivity         = new Vector2(0.002f, 0.002f);
            ms.enableTouchpad          = true;
            ms.touchpadLookSensitivity = new Vector2(0.25f, 0.25f);
            ms.useSdl3OnPC             = true;
            ms.simulateInEditor        = true;    // không có tay cầm gyro: I J K L = gyro, giữ T + chuột = touchpad
            ms.touchpadMovesCursor     = true;
            ms.touchpadClickIsLeftClick = true;

            p.actionMaps = new List<TISActionMapData>
            {
                Map("Movement",
                    Vec("Move", "<Keyboard>/wasd", "<Gamepad>/leftStick"),
                    Btn("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth", Down),
                    Btn("Run",  "<Keyboard>/leftShift", "<Gamepad>/leftStickPress", Hold)),
                Map("Camera",
                    // Không bind chuột: scene Tay cầm chỉ xoay camera bằng cần phải / gyro / touchpad
                    Vec("Look", "", "<Gamepad>/rightStick", mixGyro: true, mixTouchpad: true)),
                Map("Combat",
                    Btn("Attack",   "<Mouse>/leftButton", "<Gamepad>/rightTrigger", Down),
                    Btn("Special",  "", "<Gamepad>/buttonNorth", Down, gamepadModifier: "<Gamepad>/leftShoulder"),
                    Btn("Bumper",   "", "<Gamepad>/rightShoulder", Hold)),
                Map("Actions",
                    // Cùng cò RT với Attack — đặt ở map khác (trùng binding trong một map bị Validate báo lỗi)
                    Axis("Throttle", "", "<Gamepad>/rightTrigger"),
                    Btn("Interact", "<Keyboard>/f", "<Gamepad>/buttonWest",  Down),
                    Btn("Toggle",   "<Keyboard>/y", "<Gamepad>/buttonNorth", Down),
                    Btn("Back",     "<Keyboard>/backspace", "<Gamepad>/buttonEast", Down),
                    Vec("Tilt",     "", "<TISMotion>/gyro"),
                    Btn("PadClick", "", "<TISMotion>/touchpadPress", Down)),
                Map("UI",
                    Btn("Pause", "<Keyboard>/escape", "<Gamepad>/start", Down)),
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        // ══════════════════════════════════════════════════════
        //  MOBILE
        // ══════════════════════════════════════════════════════

        public static TISInputProfileConfig EnsureMobile()
        {
            var p = LoadOrCreate(MobilePath);
            p.profileName = "TIS Tut · Mobile";
            p.description = "Profile cho scene hướng dẫn Mobile: joystick ảo, vùng xoay camera, nút kỹ năng, kỹ năng định hướng.";
            p.enablePC = true; p.enableGamepad = true; p.enableMobile = true;

            p.cameraSettings.mobileSensitivity = new Vector2(0.24f, 0.24f);   // gấp đôi mặc định — vuốt nhẹ đã xoay đủ
            p.cameraSettings.pitchMin = -60f;
            p.cameraSettings.pitchMax = 35f;

            var m = p.mobileSettings;
            m.joystickRadius       = 110f;
            m.joystickDeadzone     = 0.08f;
            m.dynamicJoystick      = true;
            m.idleOpacity          = 0.45f;
            m.cameraZoneWidthRatio = 0.5f;

            // Look không bind chuột: Editor giả lập mobile dùng chuột làm ngón tay,
            // nếu giữ <Mouse>/delta thì rê chuột cũng xoay camera.
            // Không dùng gyro / touchpad → tắt (giống mặc định của profile mới)
            p.motionSettings.enableGyro = false;
            p.motionSettings.enableTouchpad = false;

            p.actionMaps = new List<TISActionMapData>
            {
                Map("Movement",
                    Vec("Move", "<Keyboard>/wasd", "<Gamepad>/leftStick", TISVirtualControlID.TISVirtualJoystick),
                    Btn("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth", Down, mobile: TISVirtualControlID.ButtonByIndex(0))),
                Map("Camera",
                    Vec("Look", "", "<Gamepad>/rightStick", TISVirtualControlID.CameraZone)),
                Map("Combat",
                    Btn("Attack",   "", "<Gamepad>/rightTrigger", Down, mobile: TISVirtualControlID.ButtonByIndex(1)),
                    Btn("Dash",     "", "<Gamepad>/buttonEast",   Down, mobile: TISVirtualControlID.ButtonByIndex(2)),
                    Btn("Fireball", "", "<Gamepad>/buttonNorth",  Down, mobile: TISVirtualControlID.ButtonByIndex(3)),
                    Vec("Fireball_Dir", "", "")),   // hướng do TISDirectionalSkillButton ghi vào
                Map("UI",
                    Btn("Pause", "<Keyboard>/escape", "<Gamepad>/start", Down, mobile: TISVirtualControlID.ButtonByIndex(9))),
            };
            EditorUtility.SetDirty(p);
            return p;
        }

        // ══════════════════════════════════════════════════════
        //  TIỆN ÍCH
        // ══════════════════════════════════════════════════════

        static TISInputProfileConfig LoadOrCreate(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(path);
            if (p != null) return p;
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            p = ScriptableObject.CreateInstance<TISInputProfileConfig>();
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static TISActionMapData Map(string name, params TISInputActionDefinition[] actions)
            => new TISActionMapData { mapName = name, enabledByDefault = true, actions = new List<TISInputActionDefinition>(actions) };

        static TISInputActionDefinition Btn(string name, string pc, string gamepad, TISButtonReadMode mode,
                                            string mobile = "", string pcModifier = "", string gamepadModifier = "")
            => new TISInputActionDefinition
            {
                actionName = name, actionType = TISInputActionType.Button,
                bindingPC = pc, bindingPCModifier = pcModifier, bindingGamepad = gamepad, bindingMobile = mobile,
                bindingGamepadModifier = gamepadModifier,
                readMode = mode, isSinglePress = mode == Down, _readModeMigrated = true,
            };

        static TISInputActionDefinition Vec(string name, string pc, string gamepad, string mobile = "",
                                            bool mixGyro = false, bool mixTouchpad = false)
            => new TISInputActionDefinition
            {
                actionName = name, actionType = TISInputActionType.Vector2,
                bindingPC = pc, bindingGamepad = gamepad, bindingMobile = mobile,
                gamepadMixGyro = mixGyro, gamepadMixTouchpad = mixTouchpad, _readModeMigrated = true,
            };

        static TISInputActionDefinition Axis(string name, string pc, string gamepad, string mobile = "")
            => new TISInputActionDefinition
            {
                actionName = name, actionType = TISInputActionType.Axis,
                bindingPC = pc, bindingGamepad = gamepad, bindingMobile = mobile, _readModeMigrated = true,
            };
    }
}
