using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: SetObjectTrackerEnabled(bool)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Bật/tắt Object Tracker lúc runtime để tiết kiệm tài nguyên.
/// Tắt khi vào cutscene/menu, bật lại khi gameplay tiếp tục.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SetObjTracker))]
public class TIS_Demo_SetObjTracker_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách bật/tắt Object Tracker lúc runtime để\n" +
            "  tiết kiệm tài nguyên. Tracker thực hiện Raycast mỗi frame nên nên\n" +
            "  được tắt khi không cần thiết: vào cutscene, mở menu, hoặc pause.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true (bật mặc định)\n" +
            "2. Gán script này lên GameStateManager / UIController.\n" +
            "3. Gọi Enable/Disable qua UnityEvent hoặc từ code.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Gameplay]\n" +
            "    Tracker BẬT — Raycast mỗi frame, ToucherWorldPosition() hoạt động.\n\n" +
            "  [Cutscene / PauseMenu]\n" +
            "    Tracker TẮT — không Raycast, tiết kiệm CPU.\n" +
            "    ToucherCount, DoubleTap, GetSingleObject trả về giá trị mặc định\n" +
            "    (0 / false / null) — không gây lỗi.\n\n" +
            "  [UnityEvent / Timeline]\n" +
            "    Gọi EnableTracker() / DisableTracker() trực tiếp từ Button,\n" +
            "    Timeline Signal, hoặc AnimationEvent mà không cần reference code.\n\n" +

            "API\n" +
            "  bridge.SetObjectTrackerEnabled(true)   → bật tracker\n" +
            "  bridge.SetObjectTrackerEnabled(false)  → tắt tracker\n" +
            "  EnableTracker()   → public, gọi được từ UnityEvent\n" +
            "  DisableTracker()  → public, gọi được từ UnityEvent\n\n" +

            "GHI CHÚ\n" +
            "  Đổi 'currentState' trong Inspector lúc Play Mode để test nhanh.\n" +
            "  Tắt tracker khi không cần thiết để giảm Raycast overhead mỗi frame.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SetObjTracker")]
public class TIS_Demo_SetObjTracker : MonoBehaviour
{
    public enum GameState { Gameplay, Cutscene, PauseMenu }

    [Header("Trạng thái hiện tại")]
    public GameState currentState = GameState.Gameplay;

    private TISInputBridge _input;
    private GameState _lastState;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        _lastState = currentState;
        ApplyTrackerState(currentState);
    }

    private void Update()
    {
        if (currentState == _lastState) return;
        _lastState = currentState;
        ApplyTrackerState(currentState);
    }

    private void ApplyTrackerState(GameState state)
    {
        bool enable = state == GameState.Gameplay;
        _input.SetObjectTrackerEnabled(enable);     // ← API chính
        Debug.Log($"[SetObjTracker] State={state} → Tracker {(enable ? "BẬT" : "TẮT")}");
    }

    // Gọi từ UnityEvent (Button, Timeline Signal, v.v.)
    public void EnableTracker()  => _input.SetObjectTrackerEnabled(true);
    public void DisableTracker() => _input.SetObjectTrackerEnabled(false);
}
