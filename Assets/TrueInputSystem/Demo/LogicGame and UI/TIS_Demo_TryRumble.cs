using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: TryRumble(lowFreq, highFreq, duration)
/// Platform: Gamepad (Xbox / PS / Switch Pro)
/// ──────────────────────────────────────────────
/// Kích hoạt rumble/haptic feedback trên gamepad.
/// Tự động bỏ qua nếu enableRumble = false trong Profile hoặc không có Gamepad.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_TryRumble))]
public class TIS_Demo_TryRumble_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Gamepad (Xbox / PlayStation / Switch Pro)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputProfileConfig → GamepadSettings:\n" +
            "   ✓ Enable Rumble = true\n" +
            "2. Cắm Gamepad và chạy Play Mode.\n" +
            "3. Gán action tên 'Attack' (Button) trong Profile.\n\n" +
            "API\n" +
            "  bridge.TryRumble(lowFreq, highFreq, duration)\n" +
            "  bridge.TryRumble(lowFreq, highFreq)  ← liên tục, không tự dừng\n" +
            "  bridge.StopRumble()\n" +
            "  • lowFreq / highFreq: [0..1] — motor trái / phải\n" +
            "  • duration: giây (0 = không dùng overload này)\n\n" +
            "GHI CHÚ\n" +
            "  TryRumble tự bỏ qua nếu enableRumble=false hoặc không có Gamepad.\n" +
            "  Gọi StopRumble() khi scene unload để tránh motor chạy mãi.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/TryRumble")]
public class TIS_Demo_TryRumble : MonoBehaviour
{
    [Header("Action để kích hoạt rumble")]
    public string hitAction = "Attack";

    [Header("Rumble Settings")]
    [Range(0f, 1f)] public float lowFreq   = 0.3f;
    [Range(0f, 1f)] public float highFreq  = 0.8f;
    public float duration = 0.25f;

    [Header("Rumble khi giữ phím (liên tục)")]
    public string holdAction = "Run";
    [Range(0f, 1f)] public float holdLow  = 0.1f;
    [Range(0f, 1f)] public float holdHigh = 0.2f;

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        // Rumble có thời lượng
        if (_input.IsSkillDown(hitAction))
        {
            _input.TryRumble(lowFreq, highFreq, duration);
            Debug.Log($"[TryRumble] {hitAction} hit → rumble {duration}s");
        }

        // Rumble liên tục khi giữ; dừng khi nhả
        if (_input.IsSkillDown(holdAction))
            _input.TryRumble(holdLow, holdHigh);
        if (_input.IsSkillReleased(holdAction))
            _input.StopRumble();
    }

    private void OnDisable() => _input?.StopRumble();
}