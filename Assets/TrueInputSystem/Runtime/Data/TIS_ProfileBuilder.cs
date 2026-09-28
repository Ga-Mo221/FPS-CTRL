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
            if (!string.IsNullOrEmpty(def.bindingPC) && pcEnabled && (!isMobile || isEditor))
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
                        float stickDeadzone = def.bindingGamepad.EndsWith(
                            "/rightStick", StringComparison.OrdinalIgnoreCase)
                            ? (profile.gamepadSettings.rightStickDeadzone)
                            : (profile.gamepadSettings.leftStickDeadzone);

                        action.AddBinding(def.bindingGamepad)
                              .WithProcessor($"stickDeadzone(min={stickDeadzone:F3},max=1)");
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