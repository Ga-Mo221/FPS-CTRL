// ============================================================
//  TrueInputSystem — TISCode_MobileButton.cs
//  Code Sub-Tab Module: Mobile Button
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_MobileButton : ICodeSubTabModule
    {
        public string Label => "Mobile Button";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Mobile Button",
                "TISMobileSkillButton — SkillName, HoldTime, Interactable, SetDisabled, Cooldown, VisualMode, Events.");

            win.H2("Lấy Reference & SkillName");
            win.CodeBlock(
                "TISMobileSkillButton btn = attackButton.GetComponent<TISMobileSkillButton>();\n\n" +
                "// Tên action được binding (get only) — dùng để hiển thị tooltip\n" +
                "Debug.Log(\"Skill: \" + btn.SkillName); // e.g. \"Attack\"");

            win.Sep();
            win.H2("HoldTime — Thời Gian Đang Giữ (giây)");
            win.Body("<b>HoldTime</b> là public float property — thời gian tính từ lúc nhấn xuống, reset về 0 khi nhả.\n" +
                     "Dùng <c>btn.HoldTime > 0f</c> để kiểm tra ngón đang giữ (không có property IsPressed):");
            win.CodeBlock(
                "TISMobileSkillButton btn = jumpButton.GetComponent<TISMobileSkillButton>();\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (btn.HoldTime > 0f)\n" +
                "        Debug.Log(\"Đang giữ: \" + btn.HoldTime.ToString(\"F2\") + \"s\");\n\n" +
                "    if (btn.HoldTime >= 1.5f)\n" +
                "        ShowFullChargeEffect();\n" +
                "}");

            win.Note("Không có property IsPressed. Dùng btn.HoldTime > 0f để kiểm tra ngón đang giữ, " +
                     "hoặc đăng ký OnHold / OnHoldTimed event (xem section bên dưới).");

            win.Sep();
            win.H2("Interactable vs SetDisabled — Thứ Tự Ưu Tiên");
            win.Body("<b>Interactable</b> (ưu tiên cao hơn) — khóa hoàn toàn, không nhận touch, hiển thị notInteractable visual.\n" +
                     "<b>SetDisabled</b> — tắt tạm thời, hiển thị disabled visual tối. Dùng cho stun / stance sai / không đủ mana:");
            win.CodeBlock(
                "// Interactable — khóa / mở hoàn toàn (nhân vật chết, stage lock)\n" +
                "void OnPlayerDead()    => btn.Interactable = false;\n" +
                "void OnPlayerRevived() => btn.Interactable = true;\n\n" +
                "// SetDisabled — tắt tạm thời khi bị stun\n" +
                "void OnStunned(float stunDuration)\n" +
                "{\n" +
                "    btn.SetDisabled(true);\n" +
                "    StartCoroutine(ReEnableAfter(stunDuration));\n" +
                "}\n\n" +
                "IEnumerator ReEnableAfter(float delay)\n" +
                "{\n" +
                "    yield return new WaitForSeconds(delay);\n" +
                "    btn.SetDisabled(false);\n" +
                "}");

            win.Sep();
            win.H2("SetCooldownFill — Thanh Cooldown");
            win.Body("<b>fillAmount = 1</b> → cooldown đầy (button disabled, Image fill = 1).\n" +
                     "<b>fillAmount = 0</b> → sẵn sàng (button mở khoá, Image fill = 0).\n" +
                     "Cooldown chạy từ 1 xuống 0 — ngược chiều với elapsed:");
            win.CodeBlock(
                "[SerializeField] TISMobileSkillButton skillBtn;\n\n" +
                "IEnumerator RunCooldown(float cooldownTime)\n" +
                "{\n" +
                "    float elapsed = 0f;\n" +
                "    while (elapsed < cooldownTime)\n" +
                "    {\n" +
                "        elapsed += Time.deltaTime;\n" +
                "        // 1 = vừa dùng (full cooldown) → 0 = hết cooldown (ready)\n" +
                "        skillBtn.SetCooldownFill(1f - elapsed / cooldownTime);\n" +
                "        yield return null;\n" +
                "    }\n" +
                "    skillBtn.SetCooldownFill(0f); // 0 = ready, button mở khoá\n" +
                "}");

            win.Note("Yêu cầu Image con Type=Filled được gán vào field Cooldown Fill trong Inspector. " +
                     "SetCooldownFill tự release button nếu ngón tay đang giữ khi cooldown bắt đầu.");

            win.Sep();
            win.H2("OnHold & OnHoldTimed — Callback Mỗi Frame Khi Giữ");
            win.Body("<b>OnHold</b> — UnityEvent, gọi mỗi frame khi ngón tay đang giữ.\n" +
                     "<b>OnHoldTimed</b> — UnityEvent<float>, truyền thời gian giữ (giây) làm tham số mỗi frame:");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    btn.OnHold.AddListener(() => AutoAttack());\n\n" +
                "    btn.OnHoldTimed.AddListener(holdTime =>\n" +
                "    {\n" +
                "        float charge = Mathf.Clamp01(holdTime / maxChargeTime);\n" +
                "        chargeBar.fillAmount = charge;\n" +
                "        if (charge >= 1f) ShowFullChargeEffect();\n" +
                "    });\n" +
                "}");

            win.Sep();
            win.H2("SetVisualMode — Đổi ColorTint / SpriteSwap Runtime");
            win.CodeBlock(
                "void OnSkinChanged(bool useSpriteSwap)\n" +
                "{\n" +
                "    btn.SetVisualMode(useSpriteSwap\n" +
                "        ? TISMobileSkillButton.VisualMode.SpriteSwap\n" +
                "        : TISMobileSkillButton.VisualMode.ColorTint);\n" +
                "}");

            win.Note("SpriteSwap cần _normalSprite và _pressedSprite được assign trong Inspector. " +
                     "Nếu _disabledSprite hoặc _notInteractableSprite để null, component tự fade alpha của Normal sprite.");

            win.Sep();
            win.H2("OnPress & OnRelease Events");
            win.Body("UnityEvent — có thể đăng ký qua code hoặc kéo thả trong Inspector:");
            win.CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    btn.OnPress.AddListener(() =>\n" +
                "    {\n" +
                "        Debug.Log(\"Nhấn Attack!\");\n" +
                "        PlayAttackSFX();\n" +
                "    });\n\n" +
                "    btn.OnRelease.AddListener(() =>\n" +
                "    {\n" +
                "        Debug.Log(\"Nhả Attack\");\n" +
                "        ReleaseChargeSkill();\n" +
                "    });\n" +
                "}");

            win.Tip("OnRelease cũng được gọi khi ngón tay trượt ra ngoài vùng button (PointerExit) để tránh stuck-pressed. " +
                    "Nếu cần đo holdTime chính xác, dùng OnHoldTimed cập nhật mỗi frame thay vì dùng Time.time - startTime trong OnRelease.");
        }
    }
}
#endif
