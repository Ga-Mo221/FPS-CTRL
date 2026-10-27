// ============================================================
//  TrueInputSystem — TIS_MotionDevice.cs
//
//  Đưa gyro + touchpad (đọc qua SDL3 / provider giả lập) vào Unity Input System
//  như một THIẾT BỊ ẢO — để bind trong Setup Tool như mọi nút tay cầm khác:
//
//    <TISMotion>/gyro              Vector2  tốc độ ngắm (độ/giây, x = phải, y = lên)
//    <TISMotion>/gyro/x | /gyro/y  Axis
//    <TISMotion>/touchpad          Vector2  tốc độ vuốt (bề rộng touchpad / giây)
//    <TISMotion>/touchpad/x | /y   Axis
//    <TISMotion>/touchpadPosition  Vector2  vị trí ngón (0..1, gốc dưới-trái)
//    <TISMotion>/touchpadTouch     Button   đang chạm touchpad
//    <TISMotion>/touchpadPress     Button   đang nhấn (click) touchpad
//
//  TISStickMotionComposite: CỘNG cần analog + gyro + touchpad thành một Vector2
//  (vd. Look = right stick + gyro + touchpad) — dùng khi bật "+ Gyro" / "+ Touchpad"
//  cho binding Left / Right Stick trong Setup Tool.
// ============================================================

using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace GaMo.TrueInputSystem
{
    // ══════════════════════════════════════════════════════════════════════
    //  STATE
    // ══════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct TISMotionState : IInputStateTypeInfo
    {
        public static FourCC Format => new FourCC('T', 'I', 'S', 'M');
        public FourCC format => Format;

        [InputControl(name = "gyro", layout = "Vector2", displayName = "Gyro")]
        [FieldOffset(0)]  public Vector2 gyro;

        [InputControl(name = "touchpad", layout = "Vector2", displayName = "Touchpad Swipe")]
        [FieldOffset(8)]  public Vector2 touchpad;

        [InputControl(name = "touchpadPosition", layout = "Vector2", displayName = "Touchpad Position")]
        [FieldOffset(16)] public Vector2 touchpadPosition;

        [InputControl(name = "touchpadTouch", layout = "Button", bit = 0, displayName = "Touchpad Touch")]
        [InputControl(name = "touchpadPress", layout = "Button", bit = 1, displayName = "Touchpad Click")]
        [FieldOffset(24)] public uint buttons;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DEVICE
    // ══════════════════════════════════════════════════════════════════════

    [InputControlLayout(stateType = typeof(TISMotionState), displayName = "TIS Motion (Gyro + Touchpad)")]
    public class TISMotionDevice : InputDevice
    {
        public const string LayoutName = "TISMotion";

        public Vector2Control gyro             { get; private set; }
        public Vector2Control touchpad         { get; private set; }
        public Vector2Control touchpadPosition { get; private set; }
        public ButtonControl  touchpadTouch    { get; private set; }
        public ButtonControl  touchpadPress    { get; private set; }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            gyro             = GetChildControl<Vector2Control>("gyro");
            touchpad         = GetChildControl<Vector2Control>("touchpad");
            touchpadPosition = GetChildControl<Vector2Control>("touchpadPosition");
            touchpadTouch    = GetChildControl<ButtonControl>("touchpadTouch");
            touchpadPress    = GetChildControl<ButtonControl>("touchpadPress");
        }

        // ── Registration ─────────────────────────────────────────────────

        private static bool s_Registered;

        /// <summary>Đăng ký layout + composite (an toàn khi gọi nhiều lần). Gọi trước khi build binding.</summary>
        public static void EnsureRegistered()
        {
            if (s_Registered) return;
            InputSystem.RegisterLayout<TISMotionDevice>(LayoutName);
            InputSystem.RegisterBindingComposite<TISStickMotionComposite>(TISStickMotionComposite.Name);
            s_Registered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistration() => s_Registered = false;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => EnsureRegistered();
#endif

        // ── Runtime instance (TISInputBridge quản lý) ─────────────────────

        private TISMotionState _last;
        private bool           _hasPushed;

        /// <summary>
        /// Ghi trạng thái mới — áp NGAY (InputState.Change) để action đọc được trong cùng frame.
        /// Bỏ qua nếu không đổi (gyro trong deadzone, không chạm touchpad...) → không sinh event thừa.
        /// </summary>
        public void Push(in TISMotionState s)
        {
            if (_hasPushed
                && s.gyro == _last.gyro && s.touchpad == _last.touchpad
                && s.touchpadPosition == _last.touchpadPosition && s.buttons == _last.buttons)
                return;

            _last      = s;
            _hasPushed = true;
            // Ghi rõ buffer của gameplay. Không truyền → dùng "update gần nhất", trong Editor khi
            // Game view mất focus đó là Editor update → action của game không thấy giá trị.
            InputState.Change(this, s, GameplayUpdateType());
        }

        private static InputUpdateType GameplayUpdateType()
        {
            switch (InputSystem.settings.updateMode)
            {
                case InputSettings.UpdateMode.ProcessEventsInFixedUpdate: return InputUpdateType.Fixed;
                case InputSettings.UpdateMode.ProcessEventsManually:      return InputUpdateType.Manual;
                default:                                                  return InputUpdateType.Dynamic;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  COMPOSITE: STICK + GYRO + TOUCHPAD
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cộng cần analog + gyro + touchpad. Các part không bind → 0.
    /// Hệ số gyro / touchpad quy đổi sang "đơn vị cần analog" (TISProfileBuilder tính từ Motion Settings).
    /// </summary>
    [DisplayStringFormat("{stick} + Motion")]
    public class TISStickMotionComposite : InputBindingComposite<Vector2>
    {
        public const string Name = "TISStickMotion";

        [InputControl(layout = "Vector2")] public int stick;
        [InputControl(layout = "Vector2")] public int gyro;
        [InputControl(layout = "Vector2")] public int touchpad;

        public float gyroScaleX     = 1f;
        public float gyroScaleY     = 1f;
        public float touchpadScaleX = 1f;
        public float touchpadScaleY = 1f;

        public override Vector2 ReadValue(ref InputBindingCompositeContext context)
        {
            var s = context.ReadValue<Vector2, Vector2MagnitudeComparer>(stick);
            var g = context.ReadValue<Vector2, Vector2MagnitudeComparer>(gyro);
            var t = context.ReadValue<Vector2, Vector2MagnitudeComparer>(touchpad);
            return new Vector2(
                s.x + g.x * gyroScaleX + t.x * touchpadScaleX,
                s.y + g.y * gyroScaleY + t.y * touchpadScaleY);
        }

        public override float EvaluateMagnitude(ref InputBindingCompositeContext context)
            => ReadValue(ref context).magnitude;
    }

    /// <summary>Path helper dùng chung cho runtime + Setup Tool.</summary>
    public static class TISMotionPaths
    {
        public const string Prefix           = "<" + TISMotionDevice.LayoutName + ">";
        public const string Gyro             = Prefix + "/gyro";
        public const string Touchpad         = Prefix + "/touchpad";
        public const string TouchpadPosition = Prefix + "/touchpadPosition";
        public const string TouchpadTouch    = Prefix + "/touchpadTouch";
        public const string TouchpadPress    = Prefix + "/touchpadPress";

        public static bool IsMotionPath(string path)
            => !string.IsNullOrEmpty(path)
               && path.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase);

        public static bool IsGyroPath(string path)
            => IsMotionPath(path) && path.IndexOf("/gyro", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsTouchpadVelocityPath(string path)
            => IsMotionPath(path)
               && path.IndexOf("/touchpad", System.StringComparison.OrdinalIgnoreCase) >= 0
               && path.IndexOf("/touchpadPosition", System.StringComparison.OrdinalIgnoreCase) < 0
               && path.IndexOf("/touchpadTouch",    System.StringComparison.OrdinalIgnoreCase) < 0
               && path.IndexOf("/touchpadPress",    System.StringComparison.OrdinalIgnoreCase) < 0;
    }
}
