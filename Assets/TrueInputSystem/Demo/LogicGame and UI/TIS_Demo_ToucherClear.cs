using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: ToucherClearList / ToucherClearAll / ToucherClearTouchCount
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Các API dọn dẹp trạng thái Object Tracker.
/// Gọi khi chuyển wave, reset puzzle, hoặc thay đổi scene.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ToucherClear))]
public class TIS_Demo_ToucherClear_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách dùng các API dọn dẹp trạng thái của\n" +
            "  Object Tracker trong TIS. Dùng để reset danh sách object đã chạm,\n" +
            "  bộ đếm touch, hoặc toàn bộ trạng thái tracker khi chuyển wave,\n" +
            "  reset puzzle, hay chuyển scene — tránh tình trạng ID cũ còn sót\n" +
            "  lại gây sai lệch logic gameplay.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector → bật 'Enable Object Tracker'.\n" +
            "2. Gán script này lên GameManager / WaveController.\n\n" +
            "API — 3 mức dọn dẹp:\n" +
            "  bridge.ToucherClearTouchCount()\n" +
            "    → Chỉ đặt bộ đếm về 0, giữ lại double-tap history.\n" +
            "  bridge.ToucherClearList()\n" +
            "    → Xoá danh sách touched objects (Count về 0).\n" +
            "  bridge.ToucherClearAll()\n" +
            "    → Xoá tất cả: danh sách + double-tap + trạng thái màn hình.\n\n" +
            "GHI CHÚ\n" +
            "  Gọi ClearAll() khi chuyển scene để tránh ID cũ còn sót.\n" +
            "  Gọi ClearList() giữa các wave để reset combo mà giữ lịch sử.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ToucherClear")]
public class TIS_Demo_ToucherClear : MonoBehaviour
{
    [Header("Số object cần chạm để kết thúc wave")]
    public int waveObjectTarget = 3;

    private TISInputBridge _input;
    private int _wave = 1;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        int count = _input.ToucherCount;

        if (count >= waveObjectTarget)
        {
            Debug.Log($"[ToucherClear] Wave {_wave} hoàn thành! Đã chạm {count} object.");
            StartNextWave();
        }
    }

    private void StartNextWave()
    {
        _wave++;
        // Chỉ reset bộ đếm, giữ lại history double-tap
        _input.ToucherClearList();                  // ← API: xoá danh sách touched
        Debug.Log($"[ToucherClear] Bắt đầu Wave {_wave} — đã ClearList().");
    }

    [ContextMenu("Full Reset (ClearAll)")]
    private void FullReset()
    {
        _input.ToucherClearAll();                   // ← API: xoá tất cả trạng thái
        _wave = 1;
        Debug.Log("[ToucherClear] ClearAll() — reset hoàn toàn tracker.");
    }

    [ContextMenu("Reset Bộ Đếm (ClearTouchCount)")]
    private void ResetCountOnly()
    {
        _input.ToucherClearTouchCount();            // ← API: chỉ đặt count về 0
        Debug.Log("[ToucherClear] ClearTouchCount() — count về 0.");
    }
}
