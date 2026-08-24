using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherCount
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Đọc số lượng GameObject đã bị touch trúng (tích lũy từ lần reset cuối).
/// Dùng để biết người chơi đã chạm vào bao nhiêu object trên màn hình.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ToucherCount))]
public class TIS_Demo_ToucherCount_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector → bật 'Enable Object Tracker'.\n" +
            "2. Assign Camera vào 'Raycast Camera' (hoặc để trống → Camera.main).\n" +
            "3. Đảm bảo các GameObject cần track có Collider.\n" +
            "4. Gán script này lên bất kỳ GameObject nào (UI Manager, v.v.).\n\n" +
            "API\n" +
            "  bridge.ToucherCount        →  int  (số object đã touch)\n" +
            "  bridge.ToucherObjectIDs    →  IReadOnlyList<int> (tất cả InstanceID)\n\n" +
            "GHI CHÚ\n" +
            "  ToucherCount tích lũy — dùng ToucherClearList() để reset về 0.\n" +
            "  Dùng cho minigame 'chạm hết X vật thể' hoặc combo system.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ToucherCount")]
public class TIS_Demo_ToucherCount : MonoBehaviour
{
    [Header("Mục tiêu cần chạm đủ")]
    public int targetCount = 5;

    private TISInputBridge _input;
    private bool _goalReached;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        int count = _input.ToucherCount;   // ← API chính

        if (!_goalReached && count >= targetCount)
        {
            _goalReached = true;
            Debug.Log($"[ToucherCount] Đã chạm đủ {targetCount} object! 🎉");
            ResetCounter();
        }

        // Debug log mỗi khi count thay đổi
        if (count > 0)
            Debug.Log($"[ToucherCount] count={count} / {targetCount}");
    }

    [ContextMenu("Reset Counter")]
    private void ResetCounter()
    {
        _input.ToucherClearList();
        _goalReached = false;
        Debug.Log("[ToucherCount] Đã reset danh sách.");
    }
}