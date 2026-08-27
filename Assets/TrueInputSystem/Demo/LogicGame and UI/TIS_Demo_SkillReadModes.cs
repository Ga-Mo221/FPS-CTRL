using System.ComponentModel;
using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: IsSkillDown / IsSkillHeld / IsSkillReleased
/// Platform: PC / Gamepad / Mobile
/// ──────────────────────────────────────────────
/// Ba shortcut cho GetSkillAction() với readMode cụ thể.
/// Giảm boilerplate khi không cần overload mode.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SkillReadModes))]
public class TIS_Demo_SkillReadModes_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa 3 shortcut đọc trạng thái skill button theo\n" +
            "  từng pha nhấn: nhấn xuống, đang giữ, và thả ra. Thay thế cho\n" +
            "  GetSkillAction(name, mode) để giảm boilerplate trong code gameplay.\n\n" +

            "PLATFORM: PC · Gamepad · Mobile\n\n" +

            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo action Button trong Profile và điền tên vào field bên dưới.\n" +
            "   Ví dụ mặc định: 'Attack' — PC: <Mouse>/rightButton.\n" +
            "3. Gán script này lên Player / AbilityController.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [IsSkillDown]\n" +
            "    Trả về true đúng 1 frame khi button vừa được nhấn xuống.\n" +
            "    Dùng cho: nhảy, tấn công, kích hoạt skill một lần.\n\n" +
            "  [IsSkillHeld]\n" +
            "    Trả về true suốt trong khi button đang được giữ.\n" +
            "    Dùng cho: sprint, charge, ngắm liên tục, giữ để sạc.\n\n" +
            "  [IsSkillReleased]\n" +
            "    Trả về true đúng 1 frame khi button vừa được thả ra.\n" +
            "    Dùng cho: release charge, kết thúc aim, drop item.\n\n" +

            "API\n" +
            "  bridge.IsSkillDown(name)     → PressDown (1 frame khi nhấn)\n" +
            "  bridge.IsSkillHeld(name)     → Hold (suốt khi giữ)\n" +
            "  bridge.IsSkillReleased(name) → PressUp (1 frame khi nhả)\n\n" +

            "GHI CHÚ\n" +
            "  Đây là shortcut của GetSkillAction(name, TISButtonReadMode.*).\n" +
            "  Tên action phải khớp chính xác với actionName trong Profile.\n" +
            "  IsSkillDown và IsSkillReleased chỉ true đúng 1 frame — không dùng\n" +
            "  trong coroutine hoặc callback bất đồng bộ.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/IsSkillDown-Held-Released")]
public class TIS_Demo_SkillReadModes : MonoBehaviour
{
    [Header("Action Names (phải khớp với Profile)")]
    [Header("Default Skill 1 = right mouse click")]    
    public string Skill_1   = "Attack";

    private TISInputBridge _input;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        if (_input.IsSkillDown(Skill_1))
            Debug.Log($"[IsSkillDown]     '{Skill_1}' — Nhấn xuống!");
        
        if (_input.IsSkillHeld(Skill_1))
            Debug.Log($"[IsSkillHeld]     '{Skill_1}' — Đang Nhấn...");
        
        if (_input.IsSkillReleased(Skill_1))
            Debug.Log($"[IsSkillReleased] '{Skill_1}' — Thả Ra.");
    }
}