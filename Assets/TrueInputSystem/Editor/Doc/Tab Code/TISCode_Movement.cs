// ============================================================
//  TrueInputSystem — TISCode_Movement.cs
//  Code Sub-Tab Module: Di Chuyển
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Movement : ICodeSubTabModule
    {
        public string Label => "Di Chuyển";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Di Chuyển — MoveDirection",
                "Vector2 thống nhất từ WASD, Gamepad LeftStick và Mobile Joystick.");

            win.Body("MoveDirection tự động chọn đúng nguồn input theo platform — không cần if/else.");
            GUILayout.Space(8);

            win.H2("Cơ Bản — Đọc MoveDirection Và Di Chuyển Đơn Giản");
            win.CodeBlock(
                "// Vector2 (-1 đến 1) — đồng nhất trên mọi platform\n" +
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// Di chuyển đơn giản\n" +
                "transform.Translate(move.x * speed * Time.deltaTime,\n" +
                "                    0,\n" +
                "                    move.y * speed * Time.deltaTime);");

            win.Sep();
            win.H2("Kết Hợp Với Camera (World Space)");
            win.Body("Chuyển MoveDirection sang world space theo hướng camera để nhân vật di chuyển đúng chiều nhìn:");
            win.CodeBlock(
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// Chuyển sang world space theo hướng camera\n" +
                "Vector3 dir = cam.TransformDirection(new Vector3(move.x, 0, move.y));\n" +
                "dir.y = 0;  // loại bỏ pitch\n" +
                "dir.Normalize();\n\n" +
                "controller.Move(dir * speed * Time.deltaTime);");

            win.Sep();
            win.H2("Tốc Độ Analog (Stick Gamepad)");
            win.Body("Dùng magnitude để đọc mức độ đẩy stick — 0 khi đứng yên, 1 khi đẩy hết:");
            win.CodeBlock(
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// magnitude = 0 khi đứng yên, 1 khi đẩy hết\n" +
                "float speed = walkSpeed * Mathf.Clamp01(move.magnitude);\n\n" +
                "// Kết hợp với run\n" +
                "if (_input.IsRunning)\n" +
                "    speed = runSpeed * Mathf.Clamp01(move.magnitude);");

            win.Note("MoveDirection tự động dùng override từ TISVirtualJoystick trên mobile — không cần if(mobile) else(pc).");

            win.Sep();
            win.H2("Override MoveDirection (Virtual Joystick / AI / Replay)");
            win.Body("Dùng OverrideMoveInput() để inject giá trị tùy ý vào MoveDirection, thay thế input thật từ InputSystem:");
            win.CodeBlock(
                "// Gọi khi joystick di chuyển\n" +
                "public void OnJoystickDrag(Vector2 direction)\n" +
                "{\n" +
                "    TISInputBridge.Instance.OverrideMoveInput(direction);\n" +
                "}\n\n" +
                "// Gọi khi nhả joystick — hoàn trả lại input thật\n" +
                "public void OnJoystickRelease()\n" +
                "{\n" +
                "    TISInputBridge.Instance.ClearMoveOverride();\n" +
                "}");
        }
    }
}
#endif
