using UnityEngine;
using GaMo.TrueInputSystem;

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
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách lấy GameObject bị double-tap trong frame hiện tại\n" +
            "  từ một Manager tập trung, thay vì gán script lên từng object riêng lẻ.\n" +
            "  Sau khi lấy được object, phân loại hành động theo tag/layer/component.\n" +
            "  Dùng cho: selection manager, interact system, phân loại nhiều loại object.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "KHÁC BIỆT VỚI DoubleTapOnObject\n" +
            "  DoubleTapOnObject  →  gán lên từng object, tự kiểm tra chính nó.\n" +
            "  GetDoubleTapped    →  gán lên Manager, quản lý tất cả object tập trung.\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35s\n" +
            "   ✓ Double-Tap Distance   = 60px\n" +
            "2. Gán script này lên SelectionManager / GameManager.\n" +
            "   KHÔNG cần gán lên từng object riêng lẻ.\n" +
            "3. Điền các tag vào Inspector ('Player Tag', 'Cube Tag', 'Ground Tag')\n" +
            "   khớp với tag của các object trong scene.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Mỗi frame gọi ToucherGetDoubleTappedObject() → nhận GameObject hoặc null.\n" +
            "  2. Nếu null → không có double-tap, bỏ qua.\n" +
            "  3. Kiểm tra tag của object nhận được → phân loại hành động tương ứng.\n\n" +

            "API\n" +
            "  bridge.ToucherGetDoubleTappedObject()            →  GameObject (bất kỳ)\n" +
            "  bridge.ToucherGetDoubleTappedObject(instanceID)  →  GameObject (theo ID)\n" +
            "  Cả hai trả về null nếu không có double-tap trong frame này.\n\n" +

            "GHI CHÚ\n" +
            "  – Kết quả chỉ hợp lệ đúng 1 frame — kiểm tra trong Update.\n" +
            "  – Object phải có Collider để Raycast trúng.\n" +
            "  – Dùng tag/layer/component của kết quả để phân loại hành động.\n" +
            "  – Có thể mở rộng thêm tag mới mà không cần thêm script lên object.",
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
