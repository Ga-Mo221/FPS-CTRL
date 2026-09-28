// ============================================================
//  TrueInputSystem — TISFav_IsRunning.cs
//  Favorites Sub-Tab Module: IsRunning
// ============================================================
#if UNITY_EDITOR
namespace GaMo.TrueInputSystem.Editor
{
    public class TISFav_IsRunning : IFavSubTabModule
    {
        public string Label => "IsRunning";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "IsRunning",
                "bool",
                "Shortcut sprint — đọc theo readMode trong profile. Patch mobile nếu cần.",
                win.C_AccentGreen);

            win.H2("Cú Pháp");
            win.CodeBlock("bool running = TISInputBridge.Instance.IsRunning;");

            win.Sep();
            win.H2("Ví Dụ — Tốc Độ Chạy / Đi");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float speed = _input.IsRunning ? runSpeed : walkSpeed;\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Animation Blend Tree");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float speed = _input.MoveDirection.magnitude;\n" +
                "    if (_input.IsRunning) speed *= 2f;   // blend tree 0=idle,1=walk,2=run\n" +
                "    _animator.SetFloat(\"Speed\", speed, 0.1f, Time.deltaTime);\n" +
                "}");

            win.Warn("IsRunning đọc InputAction trực tiếp, KHÔNG qua TISMobileButtonRegistry. Patch giống IsJumping nếu dùng Mobile button.");
        }
    }
}
#endif
