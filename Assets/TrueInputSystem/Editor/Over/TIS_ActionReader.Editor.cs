// ============================================================
//  TrueInputSystem — TIS_ActionReader.Editor.cs
//
//  Đặt file này trong thư mục Editor của package
//  (ví dụ: Assets/TrueInputSystem/Editor/)
//
//  Đăng ký Editor hooks vào TISActionReader qua [InitializeOnLoad].
//  Không ảnh hưởng đến DLL Runtime — delegates = null khi không
//  có Editor assembly.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrueInputSystem
{
    [InitializeOnLoad]
    internal static class TISActionReaderEditorHooks
    {
        static TISActionReaderEditorHooks() => RegisterHooks();

        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode() => RegisterHooks();

        private static void RegisterHooks()
        {
            // ── Hook 1: GetSkillCacheSnapshot ─────────────────────────────────
            // Gọi internal accessor trên reader — không dùng reflection,
            // không boxing thừa. Boxing xảy ra bên trong BuildSkillCacheSnapshot.
            TISActionReader.EditorHook_GetSkillCacheSnapshot = (reader) =>
                reader.BuildSkillCacheSnapshot();

            // ── Hook 2: LogSkillAction (mobile source) ────────────────────────
            // Log khi skill action kích hoạt từ TISMobileSkillButton.
            TISActionReader.EditorHook_LogSkillAction =
                (actionName, mode, sourceName, debugEnabled) =>
            {
                if (!debugEnabled) return;
                Debug.Log(
                    $"[TISActionReader] ✓ Action \"{actionName}\" [{mode}] — nguồn: {sourceName}");
            };

            // ── Hook 3: LogSkillActionWithSource (keyboard/gamepad source) ────
            // Log khi skill action kích hoạt từ keyboard hoặc gamepad.
            TISActionReader.EditorHook_LogSkillActionWithSource =
                (actionName, mode, inputAction, debugEnabled) =>
            {
                if (!debugEnabled) return;
                var    ctrl = inputAction.activeControl;
                string src  = ctrl?.device is Gamepad gp
                    ? $"Gamepad [{gp.displayName}]"
                    : $"Keyboard [{ctrl?.displayName ?? "?"}]";
                Debug.Log($"[TISActionReader] ✓ Action \"{actionName}\" [{mode}] — nguồn: {src}");
            };
        }
    }
}
#endif
