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
            "PLATFORM: PC · Gamepad · Mobile\n\n" +
            "SETUP\n" +
            "1. Đảm bảo scene có TISInputBridge với Profile đã assign.\n" +
            "2. Tạo 3 action Button trong Profile:\n" +
            "   • 'Jump'   – PC: <Keyboard>/space\n" +
            "   • 'Sprint' – PC: <Keyboard>/leftShift\n" +
            "   • 'Aim'    – PC: <Mouse>/rightButton\n" +
            "3. Gán script này lên Player.\n\n" +
            "API\n" +
            "  bridge.IsSkillDown(name)     → PressDown (1 frame khi nhấn)\n" +
            "  bridge.IsSkillHeld(name)     → Hold (suốt khi giữ)\n" +
            "  bridge.IsSkillReleased(name) → PressUp (1 frame khi nhả)\n\n" +
            "GHI CHÚ\n" +
            "  Đây là shortcut của GetSkillAction(name, mode).\n" +
            "  Không cần viết TISButtonReadMode.PressDown mỗi lần.",
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