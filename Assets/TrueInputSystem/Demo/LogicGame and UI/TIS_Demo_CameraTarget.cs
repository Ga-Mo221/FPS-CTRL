using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: TISCameraTarget — GestureCentroidPosition / GestureGetFingerPosition
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Sử dụng vị trí centroid ngón tay trong camera zone để
/// xoay hoặc nhìn theo hướng ngón tay chỉ (look-at-touch).
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_CameraTarget))]
public class TIS_Demo_CameraTarget_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách dùng TIS API để điều khiển camera/pivot\n" +
            "  tự động xoay về điểm ngón tay chạm trên màn hình mobile (look-at-touch).\n" +
            "  Hỗ trợ 2 chế độ: theo dõi object được touch (ObjectTracker) hoặc\n" +
            "  theo tâm đa ngón tay kết hợp Raycast (MultiFingerGestureDetector).\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Scene cần có TISInputBridge + TISMobileInputSetup.\n" +
            "2. Gán script này lên Camera hoặc Camera Rig.\n" +
            "3. Assign 'targetCamera' — để trống = Camera.main.\n" +
            "4. Assign 'pivotTransform' — để trống = transform của GameObject này.\n" +
            "5. Chọn 'ModeTouch' phù hợp với use case.\n" +
            "6. Để script hoạt động song song TIS_Demo_LookDelta:\n" +
            "   chỉ bật 1 trong 2 script tại một thời điểm.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [ObjectTracker]\n" +
            "    Dùng ToucherWorldPosition() để lấy vị trí world 3D của điểm touch.\n" +
            "    Pivot xoay về object/điểm được chạm trực tiếp.\n\n" +
            "  [MultiFingerGestureDetector]\n" +
            "    Dùng GestureCentroidPosition (tâm các ngón) → Raycast xuống scene.\n" +
            "    Pivot xoay về điểm Raycast trúng trên LayerMask groundLayer.\n\n" +

            "API\n" +
            "  bridge.ToucherWorldPosition()        →  Vector3 (world pos của touch)\n" +
            "  bridge.GestureCentroidPosition       →  Vector2 (screen px, tâm các ngón)\n" +
            "  bridge.GestureGetFingerPosition(i)   →  Vector2 (ngón thứ i)\n" +
            "  bridge.GestureActiveFingers          →  int (số ngón đang chạm)\n\n" +

            "GHI CHÚ\n" +
            "  smoothSpeed   : tốc độ Lerp xoay pivot (càng cao càng nhanh).\n" +
            "  raycastDistance: độ dài tia Raycast (chỉ dùng ở MultiFingerGestureDetector).\n" +
            "  groundLayer   : layer mà Raycast sẽ kiểm tra va chạm.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/CameraTarget")]
public class TIS_Demo_CameraTarget : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera dùng để ScreenToWorldPoint. Trống = Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Transform sẽ LookAt điểm ngón tay chỉ.")]
    public Transform pivotTransform;

    [Header("Settings")]
    public float smoothSpeed = 8f;
    public float raycastDistance = 50f;
    public LayerMask groundLayer = ~0;
    
    public ModeTouch modeTouch = ModeTouch.ObjectTracker;

    private TISInputBridge _input;
    private Vector3 _targetPoint;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        if (targetCamera == null)  targetCamera   = Camera.main;
        if (pivotTransform == null) pivotTransform = transform;
        _targetPoint = pivotTransform.position + pivotTransform.forward * 5f;
    }
    
    private void Update()
    {
        if (modeTouch == ModeTouch.ObjectTracker)
        {
            Vector3 hitWorldPos = _input.ToucherWorldPosition();
    
            // Nếu frame này có click trúng vị trí mới, ta mới ghi đè lên _targetPoint
            if (hitWorldPos != Vector3.zero) 
            {
                _targetPoint = hitWorldPos;
                Debug.Log($"[CameraTarget] Đã cập nhật mục tiêu mới: {_targetPoint}");
            }
        }

        if (modeTouch == ModeTouch.MultiFingerGestureDetector)
        {
            // kiểm tra có bao nhiên ngón tay trên màn hình.
            if (_input.GestureActiveFingers < 1) return;
    
            // ← API chính: vị trí centroid tất cả ngón trong camera zone
            var screenPos = _input.GestureCentroidPosition;
    
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer))
            {
                _targetPoint = hit.point;
                Debug.Log($"[CameraTarget] Ngón tay → world point: {_targetPoint}");
            }
        }
        
        // Xoay pivot về hướng điểm ngón tay chỉ
        Vector3 dir = (_targetPoint - pivotTransform.position).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            pivotTransform.rotation = Quaternion.Lerp(
                pivotTransform.rotation, targetRot, Time.deltaTime * smoothSpeed);
        }
    }
}

public enum ModeTouch
{
    ObjectTracker,
    MultiFingerGestureDetector
}
