using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: SetTrackerCamera(Camera)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Thay đổi Camera dùng để Raycast của Object Tracker lúc runtime.
/// Dùng khi game chuyển sang camera khác (split-screen, security cam, aim cam...).
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SetTrackerCamera))]
public class TIS_Demo_SetTrackerCamera_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách thay đổi camera dùng để Raycast của\n" +
            "  Object Tracker lúc runtime. Khi game chuyển sang camera có FOV\n" +
            "  hoặc góc nhìn khác, tracker phải được cập nhật theo để Raycast\n" +
            "  bắn đúng hướng — tránh lệch điểm touch trên màn hình mobile.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector → bật 'Enable Object Tracker'.\n" +
            "2. Assign các Camera vào 'mainCamera', 'aimCamera', 'securityCamera'.\n" +
            "3. Gán script này lên CameraManager.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Main Camera]\n" +
            "    Camera mặc định — FOV rộng, góc nhìn bình thường.\n\n" +
            "  [Aim Camera]\n" +
            "    Camera phóng to khi aim-down-sights — FOV hẹp hơn, Raycast\n" +
            "    phải dùng đúng camera này để điểm chạm khớp với góc nhìn.\n\n" +
            "  [Security Camera]\n" +
            "    Camera cố định ở góc khác (ví dụ: camera an ninh, split-screen).\n" +
            "    Chuyển về null → tracker tự fallback về Camera.main.\n\n" +

            "API\n" +
            "  bridge.SetTrackerCamera(cam)\n" +
            "  → Đổi camera Raycast ngay lập tức, hiệu lực từ frame tiếp theo.\n" +
            "  → Truyền null → tracker tự fallback về Camera.main.\n" +
            "  UseMainCamera()     → public, gọi được từ UnityEvent\n" +
            "  UseAimCamera()      → public, gọi được từ UnityEvent\n" +
            "  UseSecurityCamera() → public, gọi được từ UnityEvent\n\n" +

            "GHI CHÚ\n" +
            "  Quan trọng khi dùng aim-down-sights cam có FOV khác main cam.\n" +
            "  Nếu không đổi camera, Raycast sẽ bắn từ sai góc nhìn.\n" +
            "  Có thể đổi camera qua Inspector (activeCameraIndex) lúc Play Mode\n" +
            "  để test nhanh mà không cần gọi code.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SetTrackerCamera")]
public class TIS_Demo_SetTrackerCamera : MonoBehaviour
{
    [Header("Danh sách camera có thể chuyển")]
    public Camera mainCamera;
    public Camera aimCamera;
    public Camera securityCamera;

    [Header("Index camera hiện tại (0=main, 1=aim, 2=security)")]
    [Range(0, 2)] public int activeCameraIndex = 0;

    private TISInputBridge _input;
    private int _lastIndex = -1;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        SwitchCamera(activeCameraIndex);
    }

    private void Update()
    {
        if (activeCameraIndex == _lastIndex) return;
        SwitchCamera(activeCameraIndex);
    }

    private void SwitchCamera(int index)
    {
        _lastIndex = index;
        Camera[] cams = { mainCamera, aimCamera, securityCamera };

        Camera target = (index >= 0 && index < cams.Length) ? cams[index] : null;
        _input.SetTrackerCamera(target);            // ← API chính

        string name = target != null ? target.name : "null (→ Camera.main)";
        Debug.Log($"[SetTrackerCamera] Chuyển sang camera: {name}");
    }

    // Gọi từ UnityEvent
    public void UseMainCamera()     => SwitchCamera(0);
    public void UseAimCamera()      => SwitchCamera(1);
    public void UseSecurityCamera() => SwitchCamera(2);
}
