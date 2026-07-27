// ============================================================
//  TrueInputSystem — TISFav_SetDisabled.cs
//  Favorites Sub-Tab Module: SetDisabled
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_SetDisabled : IFavSubTabModule
    {
        public string Label => "SetDisabled";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISMobileSkillButton",
                "SetDisabled(bool)",
                "void",
                "Tắt button tạm thời — stun, cooldown, charge. Ưu tiên THẤP hơn Interactable.",
                win.C_AccentRed);

            win.H2("Cú Pháp");
            win.CodeBlock(
                "btn.SetDisabled(bool disabled);\n\n" +
                "// Khóa hoàn toàn (ưu tiên cao hơn):\n" +
                "btn.Interactable = false;");

            win.Sep();
            win.H2("Interactable vs SetDisabled");
            win.CodeBlock(
                "// Interactable = false → khóa HOÀN TOÀN:\n" +
                "//   - Không nhận touch\n" +
                "//   - Visual mờ nhất (\"not interactable\" state)\n" +
                "//   - Dùng khi: nhân vật chết, skill chưa unlock, stage lock\n\n" +
                "// SetDisabled(true) → tắt TẠM THỜI:\n" +
                "//   - Chặn input tạm thời\n" +
                "//   - Visual \"disabled\" (mờ hơn normal, sáng hơn !Interactable)\n" +
                "//   - Dùng khi: cooldown, stun, charge, resource thiếu\n\n" +
                "// Thứ tự ưu tiên visual:\n" +
                "// !Interactable  >  Disabled  >  Pressed  >  Normal");

            win.Sep();
            win.H2("Ví Dụ — Stun");
            win.CodeBlock(
                "[SerializeField] private TISMobileSkillButton[] _skillButtons;\n\n" +
                "IEnumerator StunRoutine(float duration)\n" +
                "{\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.SetDisabled(true);\n\n" +
                "    yield return new WaitForSeconds(duration);\n\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.SetDisabled(false);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Disable Khi Thiếu Mana");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    bool hasEnoughMana = _mana >= _skillManaCost;\n" +
                "    _skillBtn.SetDisabled(!hasEnoughMana);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Khóa Khi Nhân Vật Chết");
            win.CodeBlock(
                "void OnDeath()\n" +
                "{\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.Interactable = false;   // khóa hoàn toàn\n" +
                "}\n\n" +
                "void OnRespawn()\n" +
                "{\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.Interactable = true;    // mở lại\n" +
                "}");

            win.Tip("Dùng btn.OnPress.AddListener(() => ...) và btn.OnRelease.AddListener(() => ...) để xử lý event nhấn/nhả trong Inspector hoặc code.");
        }
    }
}
#endif
