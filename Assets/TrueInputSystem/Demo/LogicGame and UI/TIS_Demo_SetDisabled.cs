using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: TISMobileSkillButton.SetDisabled(bool)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Tắt/bật tạm thời on-screen skill button theo trạng thái gameplay.
/// Khác Interactable: disabled chỉ chặn input tạm thời, không ẩn hoàn toàn.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SetDisabled))]
public class TIS_Demo_SetDisabled_Editor : UnityEditor.Editor
{
        public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách tắt/bật tạm thời on-screen skill button\n" +
            "  theo điều kiện gameplay như mana, cooldown, hoặc trạng thái stunned.\n" +
            "  Khác với Interactable: SetDisabled chỉ khóa input tạm thời và tự\n" +
            "  ForceRelease nếu button đang được nhấn, không ẩn button khỏi UI.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Tạo Canvas → thêm TISMobileSkillButton lên Image.\n" +
            "2. Assign TISMobileSkillButton vào field 'skillButton'.\n" +
            "3. Gán script này lên AbilityManager / StanceController.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Đủ mana]\n" +
            "    Button enabled — nhận input bình thường, visual sáng.\n" +
            "    Khi GetSkillAction() trả về true → trừ mana, kiểm tra lại ngưỡng.\n\n" +
            "  [Không đủ mana]\n" +
            "    SetDisabled(true) → chặn input, hiện visual disabled.\n" +
            "    Mana tự regen theo thời gian; khi vượt manaCost → tự enable lại.\n" +
            "    SetDisabled chỉ được gọi khi trạng thái thay đổi, không gọi mỗi frame.\n\n" +

            "API  (gọi trên TISMobileSkillButton)\n" +
            "  button.SetDisabled(true)   → chặn input, hiện visual disabled\n" +
            "  button.SetDisabled(false)  → mở input, trở về normal visual\n\n" +

            "SO SÁNH\n" +
            "  SetDisabled(true)    = tạm khóa (cooldown, hết mana, đang stunned)\n" +
            "  Interactable = false = khóa vĩnh viễn (chưa unlock, không đủ level)\n" +
            "  SetCooldownFill()    = tạm khóa kèm fill bar đếm ngược trực quan\n\n" +

            "GHI CHÚ\n" +
            "  SetDisabled(true) tự ForceRelease nếu button đang được nhấn.\n" +
            "  Dùng SetCooldownFill() thay SetDisabled() nếu cần hiện fill bar.\n" +
            "  Chỉ gọi SetDisabled() khi trạng thái thay đổi để tránh overhead.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SetDisabled")]
public class TIS_Demo_SetDisabled : MonoBehaviour
{
    [Header("References")]
    public TISMobileSkillButton skillButton;

    [Header("Điều kiện disable (ví dụ: mana threshold)")]
    public float manaCost     = 30f;
    public float maxMana      = 100f;
    public float manaRegen    = 10f;   // mana/giây

    private float _currentMana;
    private TISInputBridge _input;
    private bool _wasDisabled;

    private void Start()
    {
        _input       = TISInputBridge.Instance;
        _currentMana = maxMana;
    }

    private void Update()
    {
        if (skillButton == null) return;

        // Regenerate mana
        _currentMana = Mathf.Min(_currentMana + manaRegen * Time.deltaTime, maxMana);

        bool shouldDisable = _currentMana < manaCost;

        // Chỉ gọi SetDisabled khi trạng thái thay đổi (tránh gọi mỗi frame)
        if (shouldDisable != _wasDisabled)
        {
            _wasDisabled = shouldDisable;
            skillButton.SetDisabled(shouldDisable);     // ← API chính
            Debug.Log($"[SetDisabled] mana={_currentMana:F0}  disabled={shouldDisable}");
        }

        // Kích hoạt skill → tiêu mana
        if (!shouldDisable && _input.GetSkillAction("Attack"))
        {
            _currentMana -= manaCost;
            Debug.Log($"[SetDisabled] Dùng skill! Còn {_currentMana:F0} mana.");
        }
    }
}
