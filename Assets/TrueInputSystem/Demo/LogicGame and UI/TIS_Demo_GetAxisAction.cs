using System.ComponentModel;
using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: GetAxisAction(name)
/// Platform: PC / Gamepad
/// ──────────────────────────────────────────────
/// Đọc float từ action type Axis — ví dụ: Throttle, Brake, Trigger.
/// Tự động áp dụng stickCurve nếu là analog trigger của Gamepad.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GetAxisAction))]
public class TIS_Demo_GetAxisAction_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đọc giá trị float liên tục (Axis) từ TIS profile\n" +
            "  để mô phỏng hệ thống Throttle / Brake đơn giản cho xe hoặc phương tiện.\n" +
            "  Axis trả về float [0..1], tự động áp dụng stickCurve nếu là analog trigger.\n" +
            "  Dùng cho: Racing (Throttle/Brake), Flight Sim, hay bất kỳ giá trị float 1 chiều.\n\n" +

            "PLATFORM: PC · Gamepad\n\n" +

            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo 2 action type = Axis trong Profile:\n" +
            "   [Throttle]\n" +
            "   • Gamepad: <Gamepad>/rightTrigger  (0..1)\n" +
            "   • PC:      <Keyboard>/w             (0 hoặc 1)\n" +
            "   [Brake]\n" +
            "   • Gamepad: <Gamepad>/leftTrigger   (0..1)\n" +
            "   • PC:      <Keyboard>/s             (0 hoặc 1)\n" +
            "3. Điền 'Throttle Action' và 'Brake Action' trong Inspector khớp tên Profile.\n" +
            "4. Chỉnh 'Max Speed' và 'Brake Force' phù hợp với phương tiện.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Đọc throttle và brake mỗi frame qua GetAxisAction().\n" +
            "  2. Cộng throttle × maxSpeed vào tốc độ hiện tại.\n" +
            "  3. Trừ brake × brakeForce khỏi tốc độ hiện tại.\n" +
            "  4. Clamp tốc độ trong [0, maxSpeed].\n\n" +

            "API\n" +
            "  bridge.GetAxisAction(\"Throttle\")  →  float [0..1]\n" +
            "  bridge.GetAxisAction(\"Brake\")     →  float [0..1]\n" +
            "  Tự áp dụng stickCurve từ GamepadSettings nếu là analog trigger.\n\n" +

            "GHI CHÚ\n" +
            "  – _currentSpeed, throttle, brake là ReadOnly — chỉ để quan sát trong Inspector.\n" +
            "  – Axis khác Button: trả về float liên tục, không phải bool pressed/released.\n" +
            "  – stickCurve áp dụng tự động cho analog trigger gamepad, không cần cấu hình thêm.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GetAxisAction")]
public class TIS_Demo_GetAxisAction : MonoBehaviour
{
    [Header("Action Names (type = Axis trong Profile)")]
    [Header("Nhấn W (được set trong profile)")]
    public string throttleAction = "Throttle";
    [Header("Nhấn D (được set trong profile)")]
    public string brakeAction    = "Brake";

    [Header("Vehicle Settings")]
    public float maxSpeed = 20f;
    public float brakeForce = 30f;

    [Header("Debug")]
    [ReadOnly] public float _currentSpeed;
    [ReadOnly] public float throttle = 0;
    [ReadOnly] public float brake = 0;

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        throttle = _input.GetAxisAction(throttleAction);  // 0..1
        brake    = _input.GetAxisAction(brakeAction);     // 0..1

        _currentSpeed += throttle * maxSpeed * Time.deltaTime;
        _currentSpeed -= brake    * brakeForce * Time.deltaTime;
        _currentSpeed  = Mathf.Clamp(_currentSpeed, 0f, maxSpeed);

        if (throttle > 0.01f || brake > 0.01f)
            Debug.Log($"[GetAxisAction] throttle={throttle:F2}  brake={brake:F2}  speed={_currentSpeed:F1}");
    }
}

// không liên quan đến code logic game.
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false; // Vô hiệu hóa chỉnh sửa
        UnityEditor.EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;  // Kích hoạt lại cho các field sau
    }
}
#endif