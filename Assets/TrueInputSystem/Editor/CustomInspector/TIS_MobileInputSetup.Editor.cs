// ============================================================
//  TrueInputSystem — TIS_MobileInputSetup.Editor.cs
//
//  Đặt file này trong thư mục Editor của package
//  (ví dụ: Assets/TrueInputSystem/Editor/)
//
//  File này đăng ký các Editor hook vào static delegates của
//  TISMobileInputSetup. Vì dùng [InitializeOnLoadMethod], hook
//  được gắn ngay khi Unity load — không cần MonoBehaviour.
//
//  Khi build DLL Runtime: file này không được biên dịch vào.
//  Delegates trong Runtime = null → các lệnh gọi là no-op.
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem
{
    [InitializeOnLoad]
    internal static class TISMobileInputSetupEditorHooks
    {
        // Constructor tĩnh chạy mỗi khi Unity load hoặc recompile.
        static TISMobileInputSetupEditorHooks()
        {
            RegisterHooks();
        }

        // Cũng đăng ký lại sau khi Enter Play Mode (domain reload tắt).
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode() => RegisterHooks();

        private static void RegisterHooks()
        {
            // ── Hook 1: CheckSimulateMobile ───────────────────────────────────
            // Ghi đè isMobile dựa vào SimulateMobileInEditor() của InputBridge.
            TISMobileInputSetup.EditorHook_CheckSimulateMobile = (setup, currentValue) =>
            {
                if (setup._inputBridge != null && !setup._inputBridge.SimulateMobileInEditor())
                    return false;
                return true;
            };

            // ── Hook 2: TimBridgeTuDong ───────────────────────────────────────
            // Tự tìm TISInputBridge trong Scene; hiện dialog nếu không thấy.
            TISMobileInputSetup.EditorHook_TimBridgeTuDong = (setup) =>
            {
                setup._inputBridge = Object.FindAnyObjectByType<TISInputBridge>();
                if (setup._inputBridge == null)
                    EditorUtility.DisplayDialog(
                        "True Input System",
                        "Không tìm thấy TISInputBridge trong Scene.\n" +
                        "Hãy đảm bảo có một GameObject với TISInputBridge component.",
                        "OK");
            };

            // ── Hook 3: SetKnobSizeRatioDebug ─────────────────────────────────
            // Truyền knob ratio debug xuống joystick để Inspector preview khớp.
            TISMobileInputSetup.EditorHook_SetKnobSizeRatioDebug = (joystick, knobSizeRatio) =>
            {
                joystick.SetKnobSizeRatioDebug(knobSizeRatio);
            };
        }
    }
}
#endif