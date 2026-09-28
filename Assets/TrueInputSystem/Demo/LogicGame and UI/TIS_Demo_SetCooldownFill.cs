using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: TISMobileSkillButton.SetCooldownFill(float)
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Hiển thị thanh cooldown trên on-screen skill button.
/// 0 = full cooldown (tối), 1 = sẵn sàng (sáng).
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_SetCooldownFill))]
public class TIS_Demo_SetCooldownFill_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách điều khiển hiệu ứng cooldown trực quan\n" +
            "  trên on-screen skill button của mobile. Khi skill được kích hoạt,\n" +
            "  button tự động bị disable và thanh fill đếm ngược đến khi sẵn sàng.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Tạo Canvas → Image (Button Background) → thêm TISMobileSkillButton.\n" +
            "2. Trong TISMobileSkillButton Inspector → field 'Cooldown Fill':\n" +
            "   Assign một Image con với Image Type = Filled.\n" +
            "3. Assign TISMobileSkillButton vào field 'skillButton' bên dưới.\n" +
            "4. Gán script này lên SkillController / AbilityManager.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Đang cooldown]\n" +
            "    fill giảm từ 1 → 0 theo thời gian, button bị disabled tự động.\n" +
            "    SetCooldownFill() được gọi mỗi frame để cập nhật thanh hiển thị.\n\n" +
            "  [Sẵn sàng]\n" +
            "    fill = 1 → button được enable, chờ player kích hoạt skill.\n" +
            "    Khi GetSkillAction() trả về true → bắt đầu lại cooldown.\n\n" +

            "API  (gọi trên TISMobileSkillButton)\n" +
            "  button.SetCooldownFill(fill)   fill: [0..1]\n" +
            "    0   = full cooldown  → button bị disabled tự động\n" +
            "    1   = ready          → button được enable tự động\n" +
            "    0.5 = nửa cooldown\n\n" +

            "GHI CHÚ\n" +
            "  SetCooldownFill tự gọi SetDisabled() bên trong.\n" +
            "  Không cần gọi SetDisabled() riêng khi dùng fill.\n" +
            "  triggerActionName tự lấy từ skillButton.SkillName — không cần set tay.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/SetCooldownFill")]
public class TIS_Demo_SetCooldownFill : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TISMobileSkillButton cần hiển thị cooldown.")]
    public TISMobileSkillButton skillButton;

    [Header("Cooldown Settings")]
    public float cooldownDuration = 3f;
    private string triggerActionName = "Skill1";

    private TISInputBridge _input;
    private float _cooldownRemaining = 0f;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        triggerActionName = skillButton.SkillName;
    }

    private void Update()
    {
        if (skillButton == null) return;

        // Tick cooldown
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
            float fill = Mathf.Clamp01(_cooldownRemaining / cooldownDuration);
            skillButton.SetCooldownFill(fill);      // ← API chính
        }
        else
        {
            skillButton.SetCooldownFill(0f);        // Đảm bảo luôn ready khi hết

            // Kích hoạt skill → bắt đầu cooldown
            if (_input.GetSkillAction(triggerActionName))
            {
                _cooldownRemaining = cooldownDuration;
                skillButton.SetCooldownFill(0f);    // Ngay lập tức hiện full cooldown
                Debug.Log($"[SetCooldownFill] '{triggerActionName}' dùng → cooldown {cooldownDuration}s");
            }
        }
    }
}
