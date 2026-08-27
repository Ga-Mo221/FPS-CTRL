using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherIsDoubleTapOnObject(instanceID)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Phát hiện double-tap vào một GameObject cụ thể.
/// true đúng 1 frame ngay khi double-tap xảy ra.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_DoubleTapOnObject))]
public class TIS_Demo_DoubleTapOnObject_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách phát hiện double-tap vào một GameObject cụ thể.\n" +
            "  Gắn script này lên object cần theo dõi — khi người chơi double-tap\n" +
            "  đúng vào object đó, sự kiện sẽ được kích hoạt đúng 1 frame duy nhất.\n" +
            "  Dùng cho: chọn unit, inspect item, activate portal, mở rương...\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35 (giây)\n" +
            "   ✓ Double-Tap Distance   = 60 (pixel)\n" +
            "2. Gán script này lên GameObject CÓ COLLIDER cần theo dõi.\n" +
            "   Script tự lấy InstanceID của gameObject mình đang gắn.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Người chơi double-tap lên màn hình.\n" +
            "  2. TIS Raycast xác định object bị chạm → trả về InstanceID.\n" +
            "  3. Script so sánh ID đó với ID của object mình đang gắn.\n" +
            "  4. Nếu khớp → kích hoạt logic (true đúng 1 frame).\n\n" +

            "API\n" +
            "  bridge.ToucherGetLastID()                      →  int (ID object bị chạm)\n" +
            "  bridge.ToucherIsDoubleTapOnObject(instanceID)  →  bool (true 1 frame)\n\n" +

            "GHI CHÚ\n" +
            "  – Object phải có Collider để Raycast trúng.\n" +
            "  – Double-Tap Interval/Distance chỉnh trong TISInputBridge Inspector.\n" +
            "  – Kết quả chỉ true đúng 1 frame — kiểm tra trong Update, không dùng coroutine chờ.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/DoubleTapOnObject")]
public class TIS_Demo_DoubleTapOnObject : MonoBehaviour
{
    private TISInputBridge _input;
    private int _myInstanceID;

    private void Start()
    {
        _input = TISInputBridge.Instance;
    }

    private void Update()
    {
        _myInstanceID = _input.ToucherGetLastID();
        // ← API chính: double-tap vào chính object này
        if (_input.ToucherIsDoubleTapOnObject(_myInstanceID))
        {
            Debug.Log($"[DoubleTapOnObject] Double-tap! (ID={_myInstanceID})");
        }
    }
}