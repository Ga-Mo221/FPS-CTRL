// ============================================================
//  TrueInputSystem — TISProfileBuilder.cs
//
//  P3 FIX #15: Tách ra từ TISInputBridge.
//  Trách nhiệm DUY NHẤT: Xây dựng UnityEngine.InputSystem.InputActionAsset
//  từ một TISInputProfileConfig.
//
//  TISInputBridge gọi TISProfileBuilder.Build() và nhận lại
//  BuildResult chứa asset + các action map đã cache.
//  Không có dependency ngược lại (không biết đến TISInputBridge).
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

using UISActionType = UnityEngine.InputSystem.InputActionType;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Kết quả trả về sau khi build xong.
    /// </summary>
    public sealed class TISProfileBuildResult
    {
        /// <summary>InputActionAsset đã được tạo và enable. Caller chịu trách nhiệm Destroy khi không dùng.</summary>
        public InputActionAsset Asset { get; }

        /// <summary>Cache tất cả InputActionMap theo tên (OrdinalIgnoreCase).</summary>
        public Dictionary<string, InputActionMap> MapCache { get; }

        public TISProfileBuildResult(InputActionAsset asset, Dictionary<string, InputActionMap> mapCache)
        {
            Asset    = asset;
            MapCache = mapCache;
        }
    }

    /// <summary>
    /// Builder thuần: nhận TISInputProfileConfig → trả TISProfileBuildResult.
    /// Không kế thừa MonoBehaviour, không giữ state giữa các lần Build().
    /// </summary>
    public static class TISProfileBuilder
    {
        // ══════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Xây dựng một InputActionAsset hoàn chỉnh từ profile đã cho.
        /// Asset được enable trước khi trả về.
        /// </summary>
        /// <param name="profile">Profile (thường là runtime copy đã clone).</param>
        /// <param name="verboseLog">Nếu true, log chi tiết từng action/binding ra Console.</param>
        public static TISProfileBuildResult Build(TISInputProfileConfig profile, bool verboseLog = false)
        {
            var asset     = BuildAsset(profile, verboseLog);
            var mapCache  = BuildMapCache(asset);
            return new TISProfileBuildResult(asset, mapCache);
        }

        // ══════════════════════════════════════════════════════
        //  BUILD ASSET
        // ══════════════════════════════════════════════════════

        private static InputActionAsset BuildAsset(TISInputProfileConfig profile, bool verboseLog)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = $"TIS_{profile.profileName}";

            foreach (var mapData in profile.actionMaps)
            {
                var map = asset.AddActionMap(mapData.mapName);

                foreach (var def in mapData.actions)
                {
                    if (string.IsNullOrWhiteSpace(def.actionName))
                    {
                        Debug.LogWarning(
                            $"[TISProfileBuilder] Map \"{mapData.mapName}\": bỏ qua action tên trống.");
                        continue;
                    }

                    UISActionType unityType = def.actionType switch
                    {
                        TISInputActionType.Vector2 => UISActionType.Value,
                        TISInputActionType.Axis    => UISActionType.Value,
                        _                          => UISActionType.Button,
                    };

                    var action = map.AddAction(def.actionName, unityType);
                    AddPlatformBindings(action, def, profile);

                    if (verboseLog)
                    {
                        bool analogBtn = IsTriggerAnalogPath(def.bindingGamepad)
                                         && def.actionType == TISInputActionType.Button;
                        Debug.Log(
                            $"[TISProfileBuilder] + [{mapData.mapName}] \"{def.actionName}\" " +
                            $"PC={def.bindingPC} GP={def.bindingGamepad}" +
                            (analogBtn ? " [trigger→Button]" : "") +
                            $" MB={def.bindingMobile} mode={def.readMode}");
                    }
                }
            }

            return asset;
        }

        // ══════════════════════════════════════════════════════
        //  PLATFORM BINDINGS
        // ══════════════════════════════════════════════════════

        private static void AddPlatformBindings(
            InputAction action,
            TISInputActionDefinition def,
            TISInputProfileConfig profile)
        {
            bool isMobile = Application.isMobilePlatform;
            bool isEditor = Application.isEditor;

            bool pcEnabled      = isEditor || (profile.enablePC      );
            bool gamepadEnabled = isEditor || (profile.enableGamepad  );

            // ── PC / Keyboard + Mouse ─────────────────────────
            // Mobile vẫn giữ binding PC: bàn phím / chuột Bluetooth, Samsung DeX, ChromeOS.
            // Riêng path <Pointer> / <Touchscreen> bị bỏ trên mobile vì chạm màn hình
            // cũng là Pointer → sẽ kích hoạt nhầm action (đã có nút ảo / camera zone lo).
            bool touchLikePath = def.bindingPC != null
                && (def.bindingPC.StartsWith("<Pointer>",     StringComparison.OrdinalIgnoreCase)
                 || def.bindingPC.StartsWith("<Touchscreen>", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(def.bindingPC) && pcEnabled && !(isMobile && !isEditor && touchLikePath))
            {
                if (def.bindingPC.IndexOf("wasd", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddWASDComposite(action);
                else if (!string.IsNullOrEmpty(def.bindingPCModifier))
                    action.AddCompositeBinding("OneModifier")
                          .With("Modifier", def.bindingPCModifier)
                          .With("Binding",  def.bindingPC);
                else
                    action.AddBinding(def.bindingPC);
            }

            // ── Gamepad ───────────────────────────────────────
            if (!string.IsNullOrEmpty(def.bindingGamepad) && gamepadEnabled)
            {
                bool isVector2Path = def.actionType == TISInputActionType.Vector2;
                bool hasModifier   = !string.IsNullOrEmpty(def.bindingGamepadModifier);

                if (hasModifier)
                {
                    if (isVector2Path || def.actionType == TISInputActionType.Axis)
                    {
                        Debug.LogWarning(
                            $"[TISProfileBuilder] \"{def.actionName}\": Gamepad modifier bị bỏ qua " +
                            $"vì OneModifier composite không hỗ trợ {def.actionType} path.");
                        hasModifier = false;
                    }
                    else
                    {
                        action.AddCompositeBinding("OneModifier")
                              .With("Modifier", def.bindingGamepadModifier)
                              .With("Binding",  def.bindingGamepad);
                    }
                }

                if (!hasModifier)
                {
                    bool isDpad = def.bindingGamepad.EndsWith(
                        "/dpad", StringComparison.OrdinalIgnoreCase);

                    if (isVector2Path && IsStickPath(def.bindingGamepad) && !isDpad)
                    {
                        // Analog stick: apply deadzone processor để tránh drift
                        bool isRightStick = def.bindingGamepad.EndsWith(
                            "/rightStick", StringComparison.OrdinalIgnoreCase);
                        float stickDeadzone = isRightStick
                            ? (profile.gamepadSettings.rightStickDeadzone)
                            : (profile.gamepadSettings.leftStickDeadzone);

                        // InvariantCulture BẮT BUỘC: locale vi-VN / de / fr... format 0.12 thành "0,120"
                        // → "stickDeadzone(min=0,120,max=1)" → Input System hiểu sai tham số,
                        // deadzone mất tác dụng (stick drift).
                        string dz = stickDeadzone.ToString("F3", CultureInfo.InvariantCulture);
                        string stickProcessors = $"stickDeadzone(min={dz},max=1)";

                        // stickCurve chỉ cho CẦN PHẢI (ngắm): nghiêng nhẹ chính xác hơn.
                        // Cần trái (di chuyển) giữ tuyến tính để tốc độ đi không bị giảm.
                        float curve = profile.gamepadSettings.stickCurve;
                        if (isRightStick && !Mathf.Approximately(curve, 1f))
                        {
                            TISStickCurveProcessor.EnsureRegistered();
                            stickProcessors += $",{TISStickCurveProcessor.Name}(exponent={F(curve)})";
                        }

                        // Luôn dựng part gyro / touchpad nếu action tick "+ Gyro / + Touchpad":
                        // bật / tắt lúc chạy (Enable Gyro của profile, SetActionMotion...) có hiệu lực
                        // ngay mà không cần rebuild — thiết bị ảo trả 0 hoặc part bị override tắt.
                        var motion   = profile.motionSettings ?? new TISMotionSettings();
                        bool mixGyro  = def.gamepadMixGyro;
                        bool mixTouch = def.gamepadMixTouchpad;

                        if (mixGyro || mixTouch)
                        {
                            // Cần + gyro + touchpad cộng lại trong 1 composite (mỗi part có processor riêng)
                            TISMotionDevice.EnsureRegistered();
                            Vector2 gs = MotionScale(def, profile, motion.gyroSensitivity);
                            Vector2 ts = MotionScale(def, profile, motion.touchpadLookSensitivity);
                            var comp = action.AddCompositeBinding(
                                    $"{TISStickMotionComposite.Name}(gyroScaleX={F(gs.x)},gyroScaleY={F(gs.y)}," +
                                    $"touchpadScaleX={F(ts.x)},touchpadScaleY={F(ts.y)})")
                                .With("Stick", def.bindingGamepad, processors: stickProcessors);
                            if (mixGyro)  comp.With("Gyro",     TISMotionPaths.Gyro);
                            if (mixTouch) comp.With("Touchpad", TISMotionPaths.Touchpad);
                        }
                        else
                        {
                            action.AddBinding(def.bindingGamepad).WithProcessors(stickProcessors);
                        }
                    }
                    else if (TISMotionPaths.IsMotionPath(def.bindingGamepad))
                    {
                        // Gyro / touchpad bind riêng. Với action "Look": áp độ nhạy Motion Settings
                        // để cùng thang với LookDelta; action khác nhận đơn vị thô (độ/giây, bề rộng/giây).
                        TISMotionDevice.EnsureRegistered();
                        var bind = action.AddBinding(def.bindingGamepad);
                        string scale = LookMotionScaleProcessor(def, profile);
                        if (scale != null) bind.WithProcessor(scale);
                    }
                    else
                    {
                        // D-Pad (digital, không drift) hoặc các path khác: bind thẳng
                        action.AddBinding(def.bindingGamepad);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  MAP CACHE
        // ══════════════════════════════════════════════════════

        private static Dictionary<string, InputActionMap> BuildMapCache(InputActionAsset asset)
        {
            var cache = new Dictionary<string, InputActionMap>(StringComparer.OrdinalIgnoreCase);
            foreach (var map in asset.actionMaps)
                cache[map.name] = map;
            return cache;
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS — path classification
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        //  HELPERS — gyro / touchpad (TISMotion)
        // ══════════════════════════════════════════════════════

        private const string LookActionName = "Look";

        private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

        private static bool IsLook(TISInputActionDefinition def)
            => string.Equals(def.actionName, LookActionName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Hệ số gyro / touchpad trong composite (đơn vị "cần analog").
        /// Look: LookDelta nhân gamepadSensitivity cho nguồn tay cầm → chia trước để kết quả cuối
        /// = tốc độ × sensitivity của Motion Settings (đúng nghĩa thông số).
        /// Action khác: sensitivity = số đơn vị cần trên mỗi đơn vị gyro / touchpad.
        /// </summary>
        private static Vector2 MotionScale(TISInputActionDefinition def, TISInputProfileConfig profile, Vector2 sensitivity)
        {
            if (!IsLook(def)) return sensitivity;
            Vector2 pad = profile.cameraSettings.gamepadSensitivity;
            return new Vector2(
                Mathf.Approximately(pad.x, 0f) ? 0f : sensitivity.x / pad.x,
                Mathf.Approximately(pad.y, 0f) ? 0f : sensitivity.y / pad.y);
        }

        /// <summary>Processor scale cho binding gyro / touchpad riêng của action Look (null = không cần).</summary>
        private static string LookMotionScaleProcessor(TISInputActionDefinition def, TISInputProfileConfig profile)
        {
            var motion = profile.motionSettings;
            if (!IsLook(def) || motion == null) return null;

            string path = def.bindingGamepad;
            Vector2 sens;
            if (TISMotionPaths.IsGyroPath(path))                  sens = motion.gyroSensitivity;
            else if (TISMotionPaths.IsTouchpadVelocityPath(path)) sens = motion.touchpadLookSensitivity;
            else return null;

            Vector2 s = MotionScale(def, profile, sens);
            if (path.EndsWith("/x", StringComparison.OrdinalIgnoreCase)) return $"scale(factor={F(s.x)})";
            if (path.EndsWith("/y", StringComparison.OrdinalIgnoreCase)) return $"scale(factor={F(s.y)})";
            return $"scaleVector2(x={F(s.x)},y={F(s.y)})";
        }

        /// <summary>
        /// Profile có binding nào dùng gyro / touchpad không (bind <TISMotion>/... hoặc "+ Gyro / + Touchpad").
        /// TISInputBridge chỉ khởi động provider (SDL3...) khi cần.
        /// </summary>
        public static bool UsesMotion(TISInputProfileConfig profile)
        {
            if (profile?.actionMaps == null) return false;
            foreach (var map in profile.actionMaps)
            foreach (var def in map.actions)
            {
                if (TISMotionPaths.IsMotionPath(def.bindingGamepad)) return true;
                if ((def.gamepadMixGyro || def.gamepadMixTouchpad) && IsStickPath(def.bindingGamepad)
                    && !def.bindingGamepad.EndsWith("/dpad", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static bool IsStickPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.EndsWith("/leftStick",  StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith("/rightStick", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith("/dpad",       StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsTriggerAnalogPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return (path.EndsWith("/leftTrigger",  StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/rightTrigger", StringComparison.OrdinalIgnoreCase))
                   && !path.EndsWith("Button", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddWASDComposite(InputAction action)
        {
            action.AddCompositeBinding("2DVector(mode=2)")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }
    }
}