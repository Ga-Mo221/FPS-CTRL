using System;
using UnityEngine;

namespace TrueInputSystem
{
    [Serializable]
    public class TISInputActionDefinition : ISerializationCallbackReceiver
    {
        // ── Metadata ──────────────────────────────────────────

        [Tooltip("Tên duy nhất của action này.\n" +
                 "Dùng để tra cứu qua TISInputBridge.GetSkillAction(name).")]
        public string actionName = "NewAction";

        [Tooltip("Loại giá trị action trả về: Button, Vector2, hoặc Axis.")]
        public TISInputActionType actionType = TISInputActionType.Button;

        // ── PC Bindings ───────────────────────────────────────

        [Tooltip("Unity Input System binding path cho PC.\n" +
                 "Chọn từ dropdown hoặc tự gõ path tuỳ chỉnh.")]
        public string bindingPC = "";

        [Tooltip("Modifier key kết hợp với Binding.\n" +
                 "Ví dụ: Left Shift -> Shift + <Binding>.\n" +
                 "Chọn từ dropdown hoặc tự gõ path tuỳ chỉnh.")]
        public string bindingPCModifier = "";

        // ── Gamepad Binding ───────────────────────────────────

        [Tooltip("Unity Input System binding path cho Gamepad.\n" +
                 "Chọn từ dropdown hoặc tự gõ path tuỳ chỉnh.")]
        public string bindingGamepad = "";

        [Tooltip("Modifier button kết hợp với Gamepad Binding.\n" +
                 "Ví dụ: Left Shoulder → LB + <Binding>.\n" +
                 "Chọn từ dropdown hoặc tự gõ path tuỳ chỉnh.")]
        public string bindingGamepadModifier = "";

        // ── Mobile Binding ────────────────────────────────────

        [Tooltip("ID của virtual control trên mobile.\n" +
                 "Dùng hằng số trong TISVirtualControlID.")]
        public string bindingMobile = "";

        // ── Tuning ────────────────────────────────────────────

        [Tooltip("PressDown = true 1 frame khi nhấn xuống (GetKeyDown).\n" +
                 "Hold      = true suốt khi giữ phím (GetKey).\n" +
                 "PressUp   = true 1 frame khi nhả ra (GetKeyUp).")]
        public TISButtonReadMode readMode = TISButtonReadMode.PressDown;

        // ── isSinglePress — GIỮ LẠI ĐỂ MIGRATE DỮ LIỆU CŨ ──
        [HideInInspector]
        public bool isSinglePress = true;

        [HideInInspector]
        public bool _readModeMigrated = false;

        // ── ISerializationCallbackReceiver ────────────────────
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!_readModeMigrated)
            {
                readMode          = isSinglePress ? TISButtonReadMode.PressDown : TISButtonReadMode.Hold;
                _readModeMigrated = true;
            }
        }

        // ── Deadzone ──────────────────────────────────────────

        [Range(0f, 0.99f)]
        [Tooltip("Deadzone riêng cho action này.\n" +
                 "0 = dùng deadzone mặc định từ ProfileConfig.\n" +
                 "Chỉ ảnh hưởng khi type là Vector2 hoặc Axis.")]
        public float overrideDeadzone = 0f;

        // ── Runtime Helpers ───────────────────────────────────

        public bool HasBindingForPlatform(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    return !string.IsNullOrEmpty(bindingMobile);

                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    return !string.IsNullOrEmpty(bindingPC);

                default:
                    return !string.IsNullOrEmpty(bindingGamepad)
                        || !string.IsNullOrEmpty(bindingPC);
            }
        }

        public TISInputActionDefinition Clone()
        {
            return new TISInputActionDefinition
            {
                actionName             = this.actionName,
                actionType             = this.actionType,
                bindingPC              = this.bindingPC,
                bindingPCModifier      = this.bindingPCModifier,
                bindingGamepad         = this.bindingGamepad,
                bindingGamepadModifier = this.bindingGamepadModifier,
                bindingMobile          = this.bindingMobile,
                readMode               = this.readMode,
                isSinglePress          = this.isSinglePress,
                _readModeMigrated      = this._readModeMigrated,
                overrideDeadzone       = this.overrideDeadzone,
            };
        }
    }
}