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
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đọc delta pan 2 ngón tay để di chuyển\n" +
            "  camera hoặc map theo hướng ngón tay kéo (scroll map, pan camera top-down).\n" +
            "  Chỉ kích hoạt khi đúng 2 ngón di chuyển cùng hướng trong camera zone,\n" +
            "  và tự nhường ưu tiên cho LookDelta / ZoomDelta nếu đang dùng đồng thời.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Scene cần có TISInputBridge + TISMobileInputSetup.\n" +
            "2. TISMobileInputSetup sẽ tự tạo TISMultiFingerGestureDetector.\n" +
            "3. Gán script này lên Camera hoặc Map Pivot.\n" +
            "4. Assign 'Target Transform' — để trống = transform của GameObject này.\n" +
            "5. Chỉnh 'Pan Speed' để điều chỉnh độ nhạy di chuyển.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Kiểm tra LookDelta / ZoomDelta — nếu đang dùng thì bỏ qua pan.\n" +
            "  2. Kiểm tra GestureIsTwoFingerPanning — chỉ xử lý khi đúng 2 ngón pan.\n" +
            "  3. Đọc GestureTwoFingerPanDelta → tính vector di chuyển trên XZ.\n" +
            "  4. Cộng vào position của targetTransform.\n\n" +

            "API\n" +
            "  bridge.GestureIsTwoFingerPanning    →  bool (đang pan 2 ngón?)\n" +
            "  bridge.GestureTwoFingerPanDelta      →  Vector2 (pixel/frame, đã nhân sensitivity)\n" +
            "  bridge.GestureTwoFingerPanRaw        →  Vector2 (pixel/frame, chưa nhân)\n" +
            "  bridge.LookDelta                     →  Vector2 (để kiểm tra xung đột)\n" +
            "  bridge.ZoomDelta                     →  float   (để kiểm tra xung đột)\n\n" +

            "GHI CHÚ\n" +
            "  – Delta bằng 0 nếu số ngón thay đổi giữa 2 frame (tránh giật).\n" +
            "  – Pan bị bỏ qua khi LookDelta hoặc ZoomDelta đang hoạt động.\n" +
            "  – panSpeed nhân trực tiếp vào delta pixel — giá trị nhỏ (~0.01) là hợp lý.\n" +
            "  – panText hiển thị trạng thái pan realtime, có thể bỏ trống nếu không cần.",
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