// ============================================================
//  TrueInputSystem — TISFav_SetCooldownFill.cs
//  Favorites Sub-Tab Module: SetCooldownFill
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_SetCooldownFill : IFavSubTabModule
    {
        public string Label => "SetCooldownFill";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISMobileSkillButton",
                "SetCooldownFill(float)",
                "void",
                "Cập nhật thanh cooldown. 0 = full cooldown (button bị disable). 1 = sẵn sàng.",
                win.C_AccentAmber);

            win.H2("Cú Pháp");
            win.CodeBlock(
                "btn.SetCooldownFill(float fill); // fill: 0 → 1\n\n" +
                "// 0 = full cooldown (button disable)\n" +
                "// 1 = ready (button enable)\n" +
                "// 0.5 = nửa cooldown");

            win.Sep();
            win.H2("Yêu Cầu Setup");
            win.CodeBlock(
                "// Inspector TISMobileSkillButton:\n" +
                "// 1. Tạo Image con (child) với Image Type = Filled\n" +
                "//    Fill Method = Radial360 hoặc Horizontal\n" +
                "//    Fill Origin = Top\n" +
                "// 2. Kéo Image đó vào field 'Cooldown Fill' trong Inspector");

            win.Sep();
            win.H2("Ví Dụ — Coroutine Cooldown Đơn Giản");
            win.CodeBlock(
                "[SerializeField] private TISMobileSkillButton _attackBtn;\n" +
                "private float _cooldown = 3f;\n\n" +
                "void OnAttack()\n" +
                "{\n" +
                "    StartCoroutine(CooldownRoutine());\n" +
                "}\n\n" +
                "IEnumerator CooldownRoutine()\n" +
                "{\n" +
                "    float elapsed = 0f;\n" +
                "    _attackBtn.SetCooldownFill(0f);     // bắt đầu cooldown\n\n" +
                "    while (elapsed < _cooldown)\n" +
                "    {\n" +
                "        elapsed += Time.deltaTime;\n" +
                "        _attackBtn.SetCooldownFill(elapsed / _cooldown);\n" +
                "        yield return null;\n" +
                "    }\n\n" +
                "    _attackBtn.SetCooldownFill(1f);     // sẵn sàng\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Cập Nhật Trong Update");
            win.CodeBlock(
                "private float _cooldownTimer;\n" +
                "private float _cooldown = 3f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_cooldownTimer < _cooldown)\n" +
                "    {\n" +
                "        _cooldownTimer += Time.deltaTime;\n" +
                "        _attackBtn?.SetCooldownFill(_cooldownTimer / _cooldown);\n" +
                "    }\n" +
                "}");

            win.Note("SetCooldownFill() tự gọi SetDisabled(fill < 1) nội bộ — không cần gọi SetDisabled() thêm khi dùng cooldown fill.");
        }
    }
}
#endif
