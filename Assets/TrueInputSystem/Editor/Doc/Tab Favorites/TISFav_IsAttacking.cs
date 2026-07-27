// ============================================================
//  TrueInputSystem — TISFav_IsAttacking.cs
//  Favorites Sub-Tab Module: IsAttacking
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_IsAttacking : IFavSubTabModule
    {
        public string Label => "IsAttacking";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "IsAttacking",
                "bool",
                "Shortcut tấn công — đọc theo readMode trong profile.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("bool attacking = TISInputBridge.Instance.IsAttacking;");

            win.Sep();
            win.H2("Ví Dụ — Tấn Công Cơ Bản");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsAttacking)\n" +
                "        _animator.SetTrigger(\"Attack\");\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Tấn Công Có Cooldown");
            win.CodeBlock(
                "private float _attackCooldown = 0.5f;\n" +
                "private float _lastAttack;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsAttacking && Time.time - _lastAttack >= _attackCooldown)\n" +
                "    {\n" +
                "        _lastAttack = Time.time;\n" +
                "        DoAttack();\n" +
                "    }\n" +
                "}");
        }
    }
}
#endif
