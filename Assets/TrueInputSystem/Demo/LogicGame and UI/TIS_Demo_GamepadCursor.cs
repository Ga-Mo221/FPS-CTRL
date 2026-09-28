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
[UnityEditor.CustomEditor(typeof(TIS_Demo_GamepadCursor))]
public class TIS_Demo_GamepadCursor_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách bật/tắt Gamepad Cursor thông qua TIS API.\n" +
            "  Mỗi frame khi TISInputBridge đang chạy, script tự động toggle trạng thái\n" +
            "  cursor gamepad (enable → disable → enable...). Dùng làm ví dụ tham khảo\n" +
            "  cách gọi SetActiveGamepadCursor() trong logic game thực tế.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Scene cần có TISInputBridge + TISMobileInputSetup.\n" +
            "2. Gán script này lên bất kỳ GameObject trong scene.\n" +
            "3. Trong game thực tế, thay logic toggle bằng điều kiện cụ thể\n" +
            "   (vd: chỉ bật cursor khi mở menu, tắt khi vào gameplay).\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Kiểm tra TISInputBridge.IsRunning trước khi gọi API.\n" +
            "  2. Đọc trạng thái hiện tại qua GetGamepadCursorEnable().\n" +
            "  3. Đảo ngược trạng thái bằng SetActiveGamepadCursor(!current).\n\n" +

            "API\n" +
            "  bridge.IsRunning                        →  bool (TIS đã khởi động)\n" +
            "  bridge.GetGamepadCursorEnable()         →  bool (cursor đang bật?)\n" +
            "  bridge.SetActiveGamepadCursor(bool)     →  void (bật/tắt cursor)\n\n" +

            "GHI CHÚ\n" +
            "  – Script hiện toggle mỗi frame — trong thực tế nên gọi có điều kiện.\n" +
            "  – Luôn kiểm tra IsRunning trước khi gọi bất kỳ API nào của TIS.\n" +
            "  – Gamepad Cursor cần được cấu hình sẵn trong TISMobileInputSetup.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GamepadCursor")]
public class TIS_Demo_GamepadCursor : MonoBehaviour
{
    private TISInputBridge _input;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
    }

    private void Update()
    {
        if (_input.IsRunning)
        {
            _input.SetActiveGamepadCursor(!_input.GetGamepadCursorEnable());
        }
    }
}
