using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: GetSkillAction(name)
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Đọc trạng thái button theo readMode đã cấu hình trong Profile.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GetSkillAction))]
public class TIS_Demo_GetSkillAction_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: PC · Gamepad · Mobile\n\n" +
            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo action tên 'Attack' (Button) trong Profile.\n" +
            "   PC binding: <Keyboard>/space | Mobile: Button_0\n" +
            "3. Gán script này lên bất kỳ GameObject nào.\n\n" +
            "API\n" +
            "  bridge.GetSkillAction(\"Attack\")\n" +
            "  → đọc readMode từ Profile (PressDown / Hold / PressUp).\n" +
            "  → trả về true tương ứng với chế độ đó.\n\n" +
            "GHI CHÚ\n" +
            "  Cache TISInputBridge.Instance trong Start() để tránh overhead.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GetSkillAction")]
public class TIS_Demo_GetSkillAction : MonoBehaviour
{
    [Header("Action Name (phải khớp với Profile)")]
    public string actionName = "Attack";

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        if (_input.GetSkillAction(actionName))
            Debug.Log($"[GetSkillAction] '{actionName}' triggered (readMode từ Profile)");
    }
}