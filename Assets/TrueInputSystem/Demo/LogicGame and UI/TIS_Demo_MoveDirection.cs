using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: MoveDirection
/// ──────────────────────────────────────────────
/// Đọc Vector2 di chuyển thống nhất từ WASD / Gamepad LeftStick / Virtual Joystick.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_MoveDirection))]
public class TIS_Demo_MoveDirection_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đọc Vector2 di chuyển thống nhất từ\n" +
            "  nhiều nguồn input khác nhau (WASD trên PC, Gamepad LeftStick,\n" +
            "  hoặc Virtual Joystick trên mobile) thông qua một API duy nhất\n" +
            "  MoveDirection — giúp code di chuyển nhân vật không cần quan tâm\n" +
            "  người chơi đang dùng thiết bị input nào.\n\n" +

            "PLATFORM: PC · Mobile · Gamepad\n\n" +
            "SETUP\n" +
            "1. Gán script này lên bất kỳ GameObject nào (thường là Player).\n" +
            "2. Đảm bảo scene có TISInputBridge (singleton).\n" +
            "3. Assign [CharacterController] vào field 'Controller' nếu muốn nhân vật thực sự di chuyển.\n\n" +
            "API\n" +
            "  TISInputBridge.Instance.MoveDirection  →  Vector2\n" +
            "  • x = trục ngang  (A/D | LeftStick.x)\n" +
            "  • y = trục dọc    (W/S | LeftStick.y)\n\n" +
            "GHI CHÚ\n" +
            "  – Giá trị đã normalize theo deadzone profile.\n" +
            "  – Cache Instance trong Start() để tránh overhead mỗi frame.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/MoveDirection")]
public class TIS_Demo_MoveDirection : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float gravity  = -9.81f;

    [Header("References")]
    public CharacterController controller;

    private TISInputBridge _input;
    private float _yVelocity;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        if (controller == null)
            controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 rawMove = _input.MoveDirection;           // ← API chính
        Vector2 move = _input.ApplyDeadzone(rawMove);

        Vector3 motion = new Vector3(move.x, 0f, move.y) * moveSpeed;

        _yVelocity += gravity * Time.deltaTime;
        motion.y    = _yVelocity;

        if (controller != null)
            controller.Move(motion * Time.deltaTime);

        // Debug log để xem giá trị thô
        Debug.Log($"[MoveDirection] {move}");
    }
}