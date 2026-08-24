using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherGetDoubleTappedObject()
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Lấy GameObject bị double-tap trong frame này.
/// Khác DoubleTapOnObject: script này đặt ở Manager, không cần gán lên từng object.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GetDoubleTapped))]
public class TIS_Demo_GetDoubleTapped_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35s\n" +
            "   ✓ Double-Tap Distance   = 60px\n" +
            "2. Gán script này lên SelectionManager / GameManager.\n" +
            "   KHÔNG cần gán lên từng object riêng lẻ.\n\n" +
            "API\n" +
            "  bridge.ToucherGetDoubleTappedObject()            →  GameObject\n" +
            "  bridge.ToucherGetDoubleTappedObject(instanceID)  →  GameObject\n" +
            "  Cả hai trả về null nếu không có double-tap trong frame này.\n\n" +
            "GHI CHÚ\n" +
            "  Dùng khi có nhiều loại object cần phản ứng khác nhau.\n" +
            "  Kiểm tra tag/layer/component của kết quả để phân loại hành động.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GetDoubleTapped")]
public class TIS_Demo_GetDoubleTapped : MonoBehaviour
{
    [Header("Tags phân loại hành động")]
    public string playerTag  = "Player";
    public string cubeTag   = "cube";
    public string groundTag = "ground";

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        // ← API chính: trả về object đầu tiên bị double-tap frame này
        GameObject tapped = _input.ToucherGetDoubleTappedObject();
        if (tapped == null) return;

        Debug.Log($"[GetDoubleTapped] '{tapped.name}' bị double-tap!");

        if (tapped.CompareTag(playerTag))
        {
            Log(playerTag);
        }
        else if (tapped.CompareTag(cubeTag))
        {
            Log(cubeTag);
        }
        else if (tapped.CompareTag(groundTag))
        {
            Log(groundTag);
        }
        else
        {
            Debug.Log("  → Hành động: khong trung gi");
        }
    }
    
    void Log(string txt) => Debug.Log($"  → Hành động: Bạn đã double tap trúng {txt}");
}
