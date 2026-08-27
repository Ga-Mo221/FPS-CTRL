using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherIsDoubleTapOnScreen()
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Phát hiện double-tap bất kỳ vị trí trên màn hình (không cần trúng object).
/// Dùng cho dodge-roll, dash, confirm action bằng double-tap vùng trống.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_DoubleTapOnScreen))]
public class TIS_Demo_DoubleTapOnScreen_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách phát hiện double-tap bất kỳ vị trí trên màn hình,\n" +
            "  không cần chạm trúng Collider nào. Khi phát hiện, kích hoạt hành động\n" +
            "  tức thì (dodge-roll, dash, confirm...) và có cooldown để tránh trigger liên tục.\n" +
            "  Dùng cho: dodge-roll, dash, confirm action bằng double-tap vùng trống.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35s\n" +
            "   ✓ Double-Tap Distance   = 60px\n" +
            "2. Gán script này lên Player hoặc GameManager.\n" +
            "3. Chỉnh 'Action Cooldown' để tránh dodge liên tục không mong muốn.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Người chơi double-tap bất kỳ vị trí trên màn hình.\n" +
            "  2. IsDoubleTapOnScreen() trả về true đúng 1 frame.\n" +
            "  3. Kiểm tra cooldown → nếu hợp lệ, kích hoạt PerformDodge().\n" +
            "  4. Gọi ToucherResetDoubleTapScreen() để tránh trigger lại ngay.\n\n" +

            "API\n" +
            "  bridge.ToucherIsDoubleTapOnScreen()      →  bool (true 1 frame)\n" +
            "  bridge.ToucherResetDoubleTapScreen()     →  void (reset trạng thái)\n" +
            "  bridge.ToucherGetLastScreenPos()         →  Vector2 (vị trí tap cuối)\n\n" +

            "GHI CHÚ\n" +
            "  – Phát hiện ngay cả khi không trúng Collider nào.\n" +
            "  – Để phân biệt vùng (vd: nửa phải vs nửa trái), kiểm tra\n" +
            "    ToucherGetLastScreenPos() sau khi IsDoubleTapOnScreen() = true.\n" +
            "  – actionCooldown nên >= Double-Tap Interval để tránh xung đột.\n" +
            "  – Kết quả chỉ true đúng 1 frame — kiểm tra trong Update, không dùng coroutine chờ.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/DoubleTapOnScreen")]
public class TIS_Demo_DoubleTapOnScreen : MonoBehaviour
{
    [Header("Cooldown sau double-tap (giây)")]
    public float actionCooldown = 0.5f;

    private TISInputBridge _input;
    private float _nextActionTime;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        // ← API chính
        if (_input.ToucherIsDoubleTapOnScreen())
        {
            if (Time.time >= _nextActionTime)
            {
                _nextActionTime = Time.time + actionCooldown;
                Debug.Log("[DoubleTapOnScreen] Double-tap màn hình → Dodge Roll!");
                PerformDodge();
            }
        }
    }

    private void PerformDodge()
    {
        // Ví dụ: trigger animation, i-frame, particle...
        Debug.Log("[DoubleTapOnScreen] Dodge executed!");

        // Reset double-tap để tránh trigger liên tục
        _input.ToucherResetDoubleTapScreen();
    }
}