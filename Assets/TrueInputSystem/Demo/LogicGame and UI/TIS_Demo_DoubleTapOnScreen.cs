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
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Double-Tap Interval   = 0.35s\n" +
            "   ✓ Double-Tap Distance   = 60px\n" +
            "2. Gán script này lên Player hoặc GameManager.\n\n" +
            "API\n" +
            "  bridge.ToucherIsDoubleTapOnScreen()  →  bool\n" +
            "  true đúng 1 frame khi double-tap bất kỳ vị trí màn hình.\n\n" +
            "GHI CHÚ\n" +
            "  Phát hiện ngay cả khi không trúng Collider nào.\n" +
            "  Để phân biệt vùng (vd: nửa phải vs nửa trái), tự kiểm tra\n" +
            "  ToucherGetLastScreenPos() sau khi IsDoubleTapOnScreen() = true.",
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