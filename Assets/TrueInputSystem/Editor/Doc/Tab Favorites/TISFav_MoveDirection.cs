// ============================================================
//  TrueInputSystem — TISFav_MoveDirection.cs
//  Favorites Sub-Tab Module: MoveDirection
// ============================================================
#if UNITY_EDITOR
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public class TISFav_MoveDirection : IFavSubTabModule
    {
        public string Label => "MoveDirection";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "MoveDirection",
                "Vector2",
                "Di chuyển từ WASD / LeftStick / Joystick ảo — chuẩn hóa trong deadzone.",
                win.C_AccentBlue);

            win.H2("Cú Pháp");
            win.CodeBlock("Vector2 dir = TISInputBridge.Instance.MoveDirection;");

            win.Sep();
            win.H2("Ví Dụ — Di Chuyển Nhân Vật Đơn Giản");
            win.CodeBlock(
                "private TISInputBridge _input;\n\n" +
                "void Start() => _input = TISInputBridge.Instance;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Di Chuyển Theo Hướng Camera (World Space)");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    Vector3 camForward = Camera.main.transform.forward;\n" +
                "    Vector3 camRight   = Camera.main.transform.right;\n" +
                "    camForward.y = 0; camRight.y = 0;\n" +
                "    camForward.Normalize(); camRight.Normalize();\n\n" +
                "    Vector3 dir = camForward * move.y + camRight * move.x;\n" +
                "    _rb.MovePosition(transform.position + dir * speed * Time.deltaTime);\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Tốc Độ Analog (Gamepad Stick Magnitude)");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 move  = _input.MoveDirection;\n" +
                "    float   speed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.Clamp01(move.magnitude));\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            win.Note("MoveDirection trả về Vector2 đã qua deadzone và normalization. Dùng .magnitude để đọc mức độ đẩy analog stick (0 → 1).");
            win.Tip("OverrideMoveInput(v2) để inject từ Joystick ảo hoặc AI. ClearMoveOverride() hoàn trả input thật.");
        }
    }
}
#endif