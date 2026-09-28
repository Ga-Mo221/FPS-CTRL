using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    [CreateAssetMenu(
        menuName = "TrueInputSystem/Input Profile",
        fileName = "New_InputProfile",
        order = 0)]
    public class TISInputProfileConfig : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Profile Info")]
        [Tooltip("Tên hiển thị của profile này. Dùng để log và debug.")]
        public string profileName = "Default Profile";

        [Tooltip("Mô tả ngắn về profile. Chỉ dùng trong Editor.")]
        [TextArea(3, 5)]
        public string description = "";

        // ── Platform Enable Flags ─────────────────────────────

        [Header("Platform Support")]
        [Tooltip("Bật binding và logic cho nền tảng PC (Keyboard / Mouse).")]
        public bool enablePC = true;

        [Tooltip("Bật binding và logic cho Gamepad (Xbox, PlayStation, Switch Pro).")]
        public bool enableGamepad = false;

        [Tooltip("Bật binding và logic cho Mobile (Virtual Joystick, On-Screen Buttons).")]
        public bool enableMobile = false;

        // ── Action Maps ───────────────────────────────────────

        [Header("Action Maps")]
        [Tooltip("Danh sách Action Map. Mỗi map là một nhóm action.")]
        public List<TISActionMapData> actionMaps = new();

        // ── Platform Settings ─────────────────────────────────

        [Header("Mobile Settings")]
        public TISMobileSettings mobileSettings = new();

        [Header("Camera Settings")]
        public TISCameraSettings cameraSettings = new();

        [Header("Gamepad Settings")]
        public TISGamepadSettings gamepadSettings = new();

        // ── Validation (runtime, không serialize) ─────────────

        //[System.NonSerialized]
        //private List<string> _validationErrors = null;

        // ── Lookup Cache (Runtime) ────────────────────────────

        private Dictionary<string, TISInputActionDefinition> _actionLookup;

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — Lookup
        // ══════════════════════════════════════════════════════

        public TISInputActionDefinition FindAction(string actionName)
        {
            BuildLookupIfNeeded();
            _actionLookup.TryGetValue(
                actionName.ToLowerInvariant(), out var def);
            return def;
        }

        public TISActionMapData FindMap(string mapName)
        {
            return actionMaps.FirstOrDefault(
                m => string.Equals(m.mapName, mapName,
                                   System.StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<TISInputActionDefinition> GetAllActions()
        {
            return actionMaps.SelectMany(m => m.actions);
        }

        // ══════════════════════════════════════════════════════
        //  VALIDATION
        // ══════════════════════════════════════════════════════

        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(profileName))
                errors.Add("Profile name không được để trống.");

            if (actionMaps == null || actionMaps.Count == 0)
                errors.Add("Profile phải có ít nhất một Action Map.");

            var mapNames = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

            foreach (var map in actionMaps)
            {
                if (string.IsNullOrWhiteSpace(map.mapName))
                {
                    errors.Add("Có Action Map tên trống.");
                    continue;
                }
                if (!mapNames.Add(map.mapName))
                    errors.Add($"Tên Action Map bị trùng: \"{map.mapName}\"");
            }

            foreach (var map in actionMaps)
            {
                var actionNames    = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                var pcBindings     = new Dictionary<string, string>();
                var gamepadBindings = new Dictionary<string, string>();

                foreach (var action in map.actions)
                {
                    if (string.IsNullOrWhiteSpace(action.actionName))
                    {
                        errors.Add($"[{map.mapName}] Có action tên trống.");
                        continue;
                    }
                    if (!actionNames.Add(action.actionName))
                        errors.Add(
                            $"[{map.mapName}] Tên action bị trùng: \"{action.actionName}\"");

                    if (!string.IsNullOrEmpty(action.bindingPC))
                    {
                        if (pcBindings.TryGetValue(action.bindingPC, out var conflict))
                            errors.Add(
                                $"[{map.mapName}] PC binding \"{action.bindingPC}\" " +
                                $"bị dùng bởi cả \"{conflict}\" và \"{action.actionName}\".");
                        else
                            pcBindings[action.bindingPC] = action.actionName;
                    }

                    if (!string.IsNullOrEmpty(action.bindingGamepad))
                    {
                        if (gamepadBindings.TryGetValue(action.bindingGamepad, out var conflict))
                            errors.Add(
                                $"[{map.mapName}] Gamepad binding \"{action.bindingGamepad}\" " +
                                $"bị dùng bởi cả \"{conflict}\" và \"{action.actionName}\".");
                        else
                            gamepadBindings[action.bindingGamepad] = action.actionName;
                    }

                    if (!string.IsNullOrEmpty(action.bindingGamepadModifier) &&
                        (action.actionType == TISInputActionType.Vector2 ||
                         action.actionType == TISInputActionType.Axis))
                    {
                        errors.Add(
                            $"[{map.mapName}] \"{action.actionName}\": Gamepad Modifier " +
                            $"không hỗ trợ action type {action.actionType}. " +
                            $"OneModifier composite chỉ trả về Button (bool). " +
                            $"Xoá Gamepad Modifier hoặc đổi Action Type sang Button.");
                    }
                }
            }

            return errors;
        }

        // ══════════════════════════════════════════════════════
        //  CLONE
        // ══════════════════════════════════════════════════════

        public TISInputProfileConfig CreateRuntimeCopy()
        {
            var copy = CreateInstance<TISInputProfileConfig>();
            copy.profileName   = this.profileName + " (Runtime)";
            copy.description   = this.description;
            copy.enablePC      = this.enablePC;
            copy.enableGamepad = this.enableGamepad;
            copy.enableMobile  = this.enableMobile;
            copy.mobileSettings  = JsonUtility.FromJson<TISMobileSettings>(JsonUtility.ToJson(this.mobileSettings));
            copy.cameraSettings  = JsonUtility.FromJson<TISCameraSettings>(JsonUtility.ToJson(this.cameraSettings));
            copy.gamepadSettings = JsonUtility.FromJson<TISGamepadSettings>(JsonUtility.ToJson(this.gamepadSettings));
            copy.actionMaps      = this.actionMaps.Select(m => m.Clone()).ToList();
            return copy;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════

        private void BuildLookupIfNeeded()
        {
            if (_actionLookup != null) return;

            _actionLookup = new Dictionary<string, TISInputActionDefinition>(
                System.StringComparer.OrdinalIgnoreCase);

            foreach (var map in actionMaps)
            foreach (var action in map.actions)
            {
                var key = action.actionName.ToLowerInvariant();
                if (!_actionLookup.ContainsKey(key))
                    _actionLookup[key] = action;
            }
        }

        private void OnValidate()
        {
            _actionLookup = null;
        }

        // ══════════════════════════════════════════════════════
        //  EDITOR HELPERS
        // ══════════════════════════════════════════════════════
        
        [ContextMenu("Validate Profile")]
        private void LogValidationContext()
        {
            var errors = Validate();
            if (errors.Count == 0)
                Debug.Log($"[TrueInputSystem] Profile \"{profileName}\" hợp lệ ✓");
            else
                foreach (var e in errors)
                    Debug.LogError($"[TrueInputSystem] {e}");
        }

        [ContextMenu("Log All Actions")]
        private void LogAllActions()
        {
            Debug.Log($"[TrueInputSystem] Profile: {profileName}");
            foreach (var map in actionMaps)
            {
                Debug.Log($"  Map: {map.mapName} (enabled={map.enabledByDefault})");
                foreach (var action in map.actions)
                    Debug.Log($"    [{action.actionType}] {action.actionName} " +
                              $"| PC: {action.bindingPC} " +
                              $"| Gamepad: {action.bindingGamepad} " +
                              $"| Mobile: {action.bindingMobile}");
            }
        }

        // ══════════════════════════════════════════════════════
        //  DEFAULT PROFILE FACTORY
        // ══════════════════════════════════════════════════════

        public static TISInputProfileConfig CreateDefault()
        {
            var profile = CreateInstance<TISInputProfileConfig>();
            profile.profileName = "Default (Auto-generated)";

            var moveMap = new TISActionMapData { mapName = "Movement" };
            moveMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Move",
                actionType        = TISInputActionType.Vector2,
                bindingPC         = "<Keyboard>/wasd",
                bindingGamepad    = "<Gamepad>/leftStick",
                bindingMobile     = TISVirtualControlID.TISVirtualJoystick,
                readMode          = TISButtonReadMode.Hold,
                isSinglePress     = false,
                _readModeMigrated = true,
            });
            moveMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Jump",
                actionType        = TISInputActionType.Button,
                bindingPC         = "<Keyboard>/space",
                bindingGamepad    = "<Gamepad>/buttonSouth",
                bindingMobile     = TISVirtualControlID.ButtonByIndex(1),
                readMode          = TISButtonReadMode.PressDown,
                isSinglePress     = true,
                _readModeMigrated = true,
            });
            moveMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Run",
                actionType        = TISInputActionType.Button,
                bindingPC         = "<Keyboard>/leftShift",
                bindingGamepad    = "<Gamepad>/leftStickPress",
                bindingMobile     = TISVirtualControlID.ButtonByIndex(0),
                readMode          = TISButtonReadMode.Hold,
                isSinglePress     = false,
                _readModeMigrated = true,
            });
            profile.actionMaps.Add(moveMap);

            var camMap = new TISActionMapData { mapName = "Camera" };
            camMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Look",
                actionType        = TISInputActionType.Vector2,
                bindingPC         = "<Mouse>/delta",
                bindingGamepad    = "<Gamepad>/rightStick",
                bindingMobile     = TISVirtualControlID.CameraZone,
                readMode          = TISButtonReadMode.Hold,
                isSinglePress     = false,
                _readModeMigrated = true,
            });
            profile.actionMaps.Add(camMap);

            var skillMap = new TISActionMapData { mapName = "Skill" };
            skillMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Attack",
                actionType        = TISInputActionType.Button,
                bindingPC         = "<Mouse>/leftButton",
                bindingGamepad    = "<Gamepad>/buttonWest",
                bindingMobile     = TISVirtualControlID.ButtonByIndex(2),
                readMode          = TISButtonReadMode.PressDown,
                isSinglePress     = true,
                _readModeMigrated = true,
            });
            skillMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Skill1",
                actionType        = TISInputActionType.Button,
                bindingPC         = "<Keyboard>/q",
                bindingGamepad    = "<Gamepad>/leftShoulder",
                bindingMobile     = TISVirtualControlID.ButtonByIndex(3),
                readMode          = TISButtonReadMode.PressDown,
                isSinglePress     = true,
                _readModeMigrated = true,
            });
            skillMap.actions.Add(new TISInputActionDefinition
            {
                actionName        = "Skill2",
                actionType        = TISInputActionType.Button,
                bindingPC         = "<Keyboard>/e",
                bindingGamepad    = "<Gamepad>/rightShoulder",
                bindingMobile     = TISVirtualControlID.ButtonByIndex(4),
                readMode          = TISButtonReadMode.PressDown,
                isSinglePress     = true,
                _readModeMigrated = true,
            });
            profile.actionMaps.Add(skillMap);

            return profile;
        }
    }
}