using System.ComponentModel;
using UnityEngine;
using TrueInputSystem;

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
            "PLATFORM: PC · Gamepad\n\n" +
            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo action tên 'Throttle' với type = Axis trong Profile.\n" +
            "   Binding ví dụ:\n" +
            "   • Gamepad: <Gamepad>/rightTrigger  (0..1)\n" +
            "   • PC:      <Keyboard>/w             (0 hoặc 1)\n" +
            "3. Điền 'actionName' trong Inspector.\n\n" +
            "API\n" +
            "  bridge.GetAxisAction(\"Throttle\")  →  float [0..1]\n" +
            "  Tự áp dụng stickCurve từ GamepadSettings nếu là analog trigger.\n\n" +
            "GHI CHÚ\n" +
            "  Dùng cho Racing (Throttle/Brake), Flight Sim, hay bất kỳ\n" +
            "  giá trị float liên tục 1 chiều.",
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