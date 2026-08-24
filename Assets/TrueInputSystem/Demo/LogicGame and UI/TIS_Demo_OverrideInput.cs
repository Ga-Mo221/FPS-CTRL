using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: OverrideMoveInput / OverrideLookInput / OverrideZoomInput
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Inject giá trị input giả vào bridge — dùng cho AI, cutscene,
/// replay system, hoặc auto-pilot tạm thời.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_OverrideInput))]
public class TIS_Demo_OverrideInput_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: PC · Gamepad · Mobile\n\n" +
            "SETUP\n" +
            "1. Scene cần có TISInputBridge.\n" +
            "2. Gán script này lên CutsceneController / AIController.\n" +
            "3. Bật 'enableAutoPilot' trong Inspector để test.\n\n" +
            "API\n" +
            "  bridge.OverrideMoveInput(Vector2)   → MoveDirection trả về value này\n" +
            "  bridge.ClearMoveOverride()          → trở về đọc input thật\n" +
            "  bridge.OverrideLookInput(Vector2)   → LookDelta trả về value này\n" +
            "  bridge.ClearLookOverride()\n" +
            "  bridge.OverrideZoomInput(float)     → ZoomDelta trả về value này\n" +
            "  bridge.ClearZoomOverride()\n\n" +
            "GHI CHÚ\n" +
            "  Override ưu tiên hơn input thật — gọi Clear*() để phục hồi.\n" +
            "  Luôn Clear trong OnDisable() để tránh stuck override.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/OverrideInput")]
public class TIS_Demo_OverrideInput : MonoBehaviour
{
    [Header("Auto-pilot Settings")]
    public bool  enableAutoPilot = false;
    public Vector2 autoPilotMove = new Vector2(0f, 1f);   // luôn tiến thẳng
    public Vector2 autoPilotLook = new Vector2(0.5f, 0f); // xoay trái nhẹ

    [Header("Cutscene Zoom")]
    public bool  autoZoomIn   = false;
    public float autoZoomDelta = 0.1f;

    private TISInputBridge _input;
    private bool _wasOverriding;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        bool shouldOverride = enableAutoPilot || autoZoomIn;

        if (enableAutoPilot)
        {
            _input.OverrideMoveInput(autoPilotMove);    // ← API: override move
            _input.OverrideLookInput(autoPilotLook);    // ← API: override look
        }
        else if (_wasOverriding)
        {
            _input.ClearMoveOverride();                 // ← API: clear move
            _input.ClearLookOverride();                 // ← API: clear look
        }

        if (autoZoomIn)
            _input.OverrideZoomInput(autoZoomDelta);    // ← API: override zoom
        else if (_wasOverriding)
            _input.ClearZoomOverride();                 // ← API: clear zoom

        _wasOverriding = shouldOverride;

        if (shouldOverride)
            Debug.Log($"[OverrideInput] move={_input.MoveDirection}  look={_input.LookDelta}");
    }

    private void OnDisable()
    {
        if (_input == null) return;
        _input.ClearMoveOverride();
        _input.ClearLookOverride();
        _input.ClearZoomOverride();
    }
}
