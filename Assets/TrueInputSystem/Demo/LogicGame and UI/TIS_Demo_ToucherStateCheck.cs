using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherWasTouched / ToucherHasNoObjects / ToucherIsSingleTouch
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Các API kiểm tra trạng thái touch helper — dùng cho logic game
/// không cần biết object cụ thể, chỉ cần biết "đã chạm chưa".
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ToucherStateCheck))]
public class TIS_Demo_ToucherStateCheck_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector → bật 'Enable Object Tracker'.\n" +
            "2. Các object cần track phải có Collider.\n" +
            "3. Gán script này lên GameManager.\n\n" +
            "API\n" +
            "  bridge.ToucherWasTouched(instanceID)  →  bool\n" +
            "    Kiểm tra object có từng được touch chưa (tích lũy).\n" +
            "  bridge.ToucherHasNoObjects()           →  bool\n" +
            "    true nếu chưa touch trúng object nào từ lần reset cuối.\n" +
            "  bridge.ToucherIsSingleTouch()          →  bool\n" +
            "    true frame có bất kỳ touch nào (kể cả không trúng object).\n\n" +
            "GHI CHÚ\n" +
            "  ToucherWasTouched lưu lịch sử — reset bằng ToucherClearAll().\n" +
            "  Dùng ToucherHasNoObjects() để kiểm tra 'người chơi chưa chọn gì'.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ToucherStateCheck")]
public class TIS_Demo_ToucherStateCheck : MonoBehaviour
{
    [Header("Object cần kiểm tra đã touch chưa")]
    [Tooltip("Assign GameObject cần theo dõi lịch sử touch.")]
    public GameObject trackedObject;

    private TISInputBridge _input;
    private bool _notifiedWasTouched;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        // 1. Kiểm tra có touch vào màn hình frame này không
        if (_input.ToucherIsSingleTouch())
            Debug.Log("[ToucherStateCheck] Có touch frame này.");

        // 2. Kiểm tra chưa chạm object nào
        if (_input.ToucherHasNoObjects())
        {
            Debug.Log("[ToucherStateCheck] Chưa chạm trúng object nào (hint: chạm vào vật thể!)");
        }

        // 3. Kiểm tra một object cụ thể đã từng được touch chưa
        if (trackedObject != null && !_notifiedWasTouched)
        {
            int id = trackedObject.GetInstanceID();
            if (_input.ToucherWasTouched(id))   // ← API: lịch sử touch
            {
                _notifiedWasTouched = true;
                Debug.Log($"[ToucherStateCheck] '{trackedObject.name}' đã từng bị touch! lượt touch tiếp theo sẽ được reset");
                return;
            }
        }

        if (_input.ToucherIsSingleTouch()&&_notifiedWasTouched)
            ResetTracking();
    }

    [ContextMenu("Reset Tracking")]
    private void ResetTracking()
    {
        _input.ToucherClearAll();
        _notifiedWasTouched = false;
        Debug.Log("[ToucherStateCheck] Reset tracking.");
    }
}
