// ============================================================
//  TrueInputSystem — TISFav_IsJumping.cs
//  Favorites Sub-Tab Module: IsJumping
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_IsJumping : IFavSubTabModule
    {
        public string Label => "IsJumping";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "IsJumping",
                "bool",
                "Shortcut nhảy — đọc theo readMode được cấu hình trong profile.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("bool jumping = TISInputBridge.Instance.IsJumping;");

            win.Sep();
            win.H2("Ví Dụ — Nhảy Cơ Bản");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsJumping && _isGrounded)\n" +
                "    {\n" +
                "        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);\n" +
                "        _cam?.NotifyJump(0.4f);   // bypass pivot lag\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Nhảy Với Coyote Time");
            win.CodeBlock(
                "private float _coyoteTimer;\n" +
                "private const float CoyoteTime = 0.12f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_isGrounded) _coyoteTimer = CoyoteTime;\n" +
                "    else             _coyoteTimer -= Time.deltaTime;\n\n" +
                "    if (_input.IsJumping && _coyoteTimer > 0f)\n" +
                "    {\n" +
                "        _coyoteTimer = 0f;\n" +
                "        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);\n" +
                "    }\n" +
                "}");

            win.Warn("IsJumping đọc InputAction trực tiếp, KHÔNG qua TISMobileButtonRegistry. Trên Mobile, cần patch thêm check registry — xem tab Nâng Cao.");
        }
    }
}
#endif
