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
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách đọc trạng thái một Button action từ TIS Profile.\n" +
            "  GetSkillAction() tự động đọc readMode đã cấu hình trong Profile (PressDown /\n" +
            "  Hold / PressUp) — không cần hardcode chế độ đọc trong code.\n" +
            "  Dùng cho: Attack, Skill, Jump, Interact, hay bất kỳ button action nào.\n\n" +

            "PLATFORM: PC · Gamepad · Mobile\n\n" +

            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo action tên 'Attack' (type = Button) trong Profile.\n" +
            "   Binding ví dụ:\n" +
            "   • PC:     <Keyboard>/space\n" +
            "   • Gamepad: <Gamepad>/buttonSouth\n" +
            "   • Mobile:  Button_0\n" +
            "3. Chọn readMode trong Profile: PressDown / Hold / PressUp.\n" +
            "4. Điền 'Action Name' trong Inspector khớp tên action trong Profile.\n" +
            "5. Gán script này lên bất kỳ GameObject nào.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Mỗi frame gọi GetSkillAction(actionName).\n" +
            "  2. TIS đọc readMode từ Profile → trả về true/false tương ứng.\n" +
            "  3. Nếu true → kích hoạt logic (Attack, Skill, v.v.).\n\n" +

            "API\n" +
            "  bridge.GetSkillAction(\"Attack\")  →  bool\n" +
            "  Tự đọc readMode từ Profile:\n" +
            "  • PressDown  →  true đúng 1 frame khi nhấn xuống\n" +
            "  • Hold       →  true mỗi frame khi đang giữ\n" +
            "  • PressUp    →  true đúng 1 frame khi thả ra\n\n" +

            "GHI CHÚ\n" +
            "  – readMode thay đổi trong Profile, không cần sửa code.\n" +
            "  – actionName phải khớp chính xác (phân biệt hoa/thường) với tên trong Profile.\n" +
            "  – Cache TISInputBridge.Instance trong Start() để tránh overhead mỗi frame.",
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