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
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35 (giây)\n" +
            "   ✓ Double-Tap Distance   = 60 (pixel)\n" +
            "2. Gán script này lên GameObject CÓ COLLIDER cần theo dõi.\n" +
            "   Script tự lấy InstanceID của gameObject mình đang gắn.\n\n" +
            "API\n" +
            "  bridge.ToucherIsDoubleTapOnObject(instanceID)  →  bool\n" +
            "  Trả về true đúng 1 frame khi double-tap vào object này.\n\n" +
            "GHI CHÚ\n" +
            "  Object phải có Collider để Raycast trúng.\n" +
            "  Dùng cho: chọn unit, inspect item, activate portal...",
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