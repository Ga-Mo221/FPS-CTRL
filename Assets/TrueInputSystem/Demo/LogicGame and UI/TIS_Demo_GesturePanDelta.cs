using TMPro;
using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: GestureTwoFingerPanDelta (GesturePanDelta)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Đọc delta pan 2 ngón tay (scroll map, di chuyển camera top-down).
/// Chỉ kích hoạt khi đúng 2 ngón di chuyển cùng hướng trong camera zone.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GesturePanDelta))]
public class TIS_Demo_GesturePanDelta_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Scene cần có TISInputBridge + TISMobileInputSetup.\n" +
            "2. TISMobileInputSetup sẽ tự tạo TISMultiFingerGestureDetector.\n" +
            "3. Gán script này lên Camera hoặc Map Pivot.\n\n" +
            "API\n" +
            "  bridge.GestureTwoFingerPanDelta  →  Vector2 (pixel/frame, đã nhân sensitivity)\n" +
            "  bridge.GestureTwoFingerPanRaw    →  Vector2 (pixel/frame, chưa nhân)\n" +
            "  bridge.GestureIsTwoFingerPanning →  bool\n\n" +
            "GHI CHÚ\n" +
            "  Delta bằng 0 nếu số ngón thay đổi giữa 2 frame (tránh giật).\n" +
            "  Nhân với panSpeed để điều chỉnh độ nhạy pan.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GesturePanDelta")]
public class TIS_Demo_GesturePanDelta : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 0.015f;

    [Header("References")]
    [Tooltip("Transform sẽ pan. Để trống = dùng transform của script này.")]
    public Transform targetTransform;
    public TextMeshProUGUI panText;

    private TISInputBridge _input;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        if (targetTransform == null) targetTransform = transform;
    }

    private void Update()
    {
        if (panText != null)
            panText.text = "No Pan";
        if ((_input.LookDelta != Vector2.zero) && (_input.ZoomDelta != 0))  return;
        if (!_input.GestureIsTwoFingerPanning) return;
        
        if (panText != null)
            panText.text = "Paning";

        Vector2 pan = _input.GestureTwoFingerPanDelta;

        Vector3 move =
            (-targetTransform.right * pan.x
             -targetTransform.forward * pan.y)
            * panSpeed;

        targetTransform.position += move;
    }
}