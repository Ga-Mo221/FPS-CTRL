using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ZoomDelta
/// ──────────────────────────────────────────────
/// Đọc giá trị zoom từ Mouse ScrollWheel / Pinch gesture mobile.
/// ZoomDelta tự reset về 0 sau mỗi frame — chỉ đọc 1 lần mỗi Update().
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ZoomDelta))]
public class TIS_Demo_ZoomDelta_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đọc giá trị zoom từ Mouse ScrollWheel\n" +
            "  (PC) hoặc Pinch gesture (mobile) thông qua API ZoomDelta, dùng\n" +
            "  để zoom camera vào/ra dựa trên khoảng cách clamp trong khoảng\n" +
            "  [minDistance, maxDistance]. Tự động bỏ qua zoom khi đang thực\n" +
            "  hiện two-finger pan để tránh xung đột gesture.\n\n" +

            "PLATFORM: PC · Mobile\n\n" +
            "SETUP\n" +
            "1. Gán script này lên Camera hoặc Camera Rig.\n" +
            "2. Đảm bảo scene có TISInputBridge.\n" +
            "3. Điền 'Min Distance' và 'Max Distance' phù hợp với scene.\n\n" +
            "API\n" +
            "  TISInputBridge.Instance.ZoomDelta  →  float\n" +
            "  • Dương = zoom in  (cuộn lên / pinch mở)\n" +
            "  • Âm   = zoom out (cuộn xuống / pinch đóng)\n" +
            "  • Tự reset về 0 sau mỗi frame — CHỈ đọc 1 lần trong Update().\n\n" +
            "GHI CHÚ\n" +
            "  – Nhân với 'zoomSpeed' để điều chỉnh tốc độ.\n" +
            "  – Clamp khoảng cách trong [minDistance, maxDistance].",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ZoomDelta")]
public class TIS_Demo_ZoomDelta : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed   = 2f;
    public float minDistance = 2f;
    public float maxDistance = 15f;

    [Header("References")]
    [Tooltip("Transform của camera. Để trống = dùng chính transform này.")]
    public Transform cameraTransform;

    private TISInputBridge _input;
    private float _currentDistance = 8f;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        if (cameraTransform == null) cameraTransform = transform;
        _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
    }

    private void Update()
    {
        if (_input.GestureIsTwoFingerPanning) return; // đang pan thì không xoay
        
        float zoom = _input.ZoomDelta;                 // ← API chính (consume mỗi frame)

        _currentDistance -= zoom * zoomSpeed;
        _currentDistance  = Mathf.Clamp(_currentDistance, minDistance, maxDistance);

        // Đẩy camera ra sau theo trục local Z
        Vector3 targetPos =
            cameraTransform.forward * -_currentDistance;

        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            targetPos,
            Time.deltaTime * 8f);

        if (Mathf.Abs(zoom) > 0.001f)
            Debug.Log($"[ZoomDelta] delta={zoom:F3}  distance={_currentDistance:F2}");
    }
}