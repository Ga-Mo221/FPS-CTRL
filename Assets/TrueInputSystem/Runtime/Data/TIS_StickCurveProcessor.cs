// ============================================================
//  TrueInputSystem — TIS_StickCurveProcessor.cs
//
//  Processor đường cong phản hồi cho cần analog (TISGamepadSettings.stickCurve).
//  Áp theo BÁN KÍNH (giữ nguyên hướng): độ lệch m → m^exponent.
//    exponent = 1   → tuyến tính (không đổi)
//    exponent > 1   → nghiêng nhẹ chậm hơn, ngắm tinh hơn; nghiêng hết vẫn = 1
//  Chạy SAU stickDeadzone (deadzone đã chuẩn hoá về 0..1).
//
//  Dùng trong binding: "stickDeadzone(min=0.1,max=1),tisStickCurve(exponent=1.5)"
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    public class TISStickCurveProcessor : InputProcessor<Vector2>
    {
        public const string Name = "tisStickCurve";

        [Tooltip("Số mũ đường cong. 1 = tuyến tính, > 1 = chính xác hơn khi nghiêng nhẹ.")]
        public float exponent = 1f;

        public override Vector2 Process(Vector2 value, InputControl control)
        {
            float m = value.magnitude;
            if (m < 1e-5f || Mathf.Approximately(exponent, 1f)) return value;

            float curved = Mathf.Pow(Mathf.Min(m, 1f), exponent);
            return value * (curved / m);
        }

        private static bool s_Registered;

        /// <summary>Đăng ký với Input System (an toàn khi gọi nhiều lần). Gọi trước khi build binding.</summary>
        public static void EnsureRegistered()
        {
            if (s_Registered) return;
            InputSystem.RegisterProcessor<TISStickCurveProcessor>(Name);
            s_Registered = true;
        }

        // Domain reload tắt → static giữ giá trị cũ nhưng registry của Input System đã reset
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistration() => s_Registered = false;

#if UNITY_EDITOR
        // Đăng ký cả ở Edit Mode để Input Debugger / binding preview không báo processor lạ
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => EnsureRegistered();
#endif
    }
}
