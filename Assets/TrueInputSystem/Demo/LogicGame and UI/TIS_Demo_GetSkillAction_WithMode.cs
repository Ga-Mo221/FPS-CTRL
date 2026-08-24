using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: GetSkillAction(name, mode)
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Đọc trạng thái button với readMode chỉ định thủ công, bỏ qua cấu hình Profile.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GetSkillAction_WithMode))]
public class TIS_Demo_GetSkillAction_WithMode_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: PC · Gamepad · Mobile\n\n" +
            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo action tên 'Dash' (Button) trong Profile.\n" +
            "3. Chọn 'Read Mode' trong Inspector:\n" +
            "   • PressDown = 1 frame khi nhấn (nhảy, tấn công)\n" +
            "   • Hold      = suốt khi giữ (chạy, ngắm)\n" +
            "   • PressUp   = 1 frame khi nhả (release charge)\n\n" +
            "API\n" +
            "  bridge.GetSkillAction(\"Dash\", TISButtonReadMode.Hold)\n" +
            "  → ghi đè readMode trong Profile cho action này.\n\n" +
            "GHI CHÚ\n" +
            "  Dùng overload này khi một action cần nhiều hành vi khác nhau\n" +
            "  ở các script khác nhau mà không muốn thêm action mới trong Profile.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GetSkillAction (With Mode)")]
public class TIS_Demo_GetSkillAction_WithMode : MonoBehaviour
{
    [Header("Action Name (phải khớp với Profile)")]
    public string actionName = "Attack";

    [Header("Read Mode Override")]
    public TISButtonReadMode readMode = TISButtonReadMode.Hold;

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        if (_input.GetSkillAction(actionName, readMode))
            Debug.Log($"[GetSkillAction+Mode] '{actionName}' | mode={readMode}");
    }
}