// ============================================================
//  TrueInputSystem — TISCode_Vector2Axis.cs
//  Code Sub-Tab Module: Vector2/Axis
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_Vector2Axis : ICodeSubTabModule
    {
        public string Label => "Vector2/Axis";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Vector2 / Axis Action",
                "GetVector2Action và GetAxisAction — đọc các action trả về Vector2 hoặc float.");

            win.H2("GetVector2Action — Đọc Action Trả Về Vector2");
            win.Body("Dùng cho WASD composite, stick, menu navigation hoặc bất kỳ action nào có Action Type = Vector2:");
            win.CodeBlock(
                "// Đọc navigation trong UI menu\n" +
                "Vector2 menuNav = _input.GetVector2Action(\"Navigate\");\n" +
                "if (menuNav.y > 0.5f)  SelectPreviousItem();\n" +
                "if (menuNav.y < -0.5f) SelectNextItem();");

            win.Sep();
            win.H2("Ví Dụ: Di Chuyển RTS / TopDown Map");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 scroll = _input.GetVector2Action(\"MapScroll\");\n" +
                "    _mapCamera.transform.Translate(\n" +
                "        scroll.x * panSpeed * Time.deltaTime,\n" +
                "        0,\n" +
                "        scroll.y * panSpeed * Time.deltaTime);\n" +
                "}");

            win.Sep();
            win.H2("GetAxisAction — Đọc Action Trả Về float");
            win.Body("Dùng cho analog trigger (ga xe, zoom), có apply trigger response curve từ profile nếu là gamepad trigger:");
            win.CodeBlock(
                "// Đọc trigger để điều chỉnh ga xe\n" +
                "float gas = _input.GetAxisAction(\"Accelerate\");\n" +
                "_vehicle.SetThrottle(gas);\n\n" +
                "// Đọc trigger để zoom camera\n" +
                "float aimZoom = _input.GetAxisAction(\"AimZoom\");\n" +
                "_cam.fieldOfView = Mathf.Lerp(normalFOV, aimFOV, aimZoom);");

            win.Sep();
            win.H2("Ví Dụ: Điều Khiển Xe Đầy Đủ");
            win.CodeBlock(
                "void HandleVehicle()\n" +
                "{\n" +
                "    float gas   = _input.GetAxisAction(\"Accelerate\"); // 0 → 1\n" +
                "    float brake = _input.GetAxisAction(\"Brake\");      // 0 → 1\n" +
                "    bool  horn  = _input.GetSkillAction(\"Horn\");\n" +
                "    bool  drift = _input.IsSkillHeld(\"Drift\");\n\n" +
                "    _vehicle.SetThrottle(gas);\n" +
                "    _vehicle.SetBrake(brake);\n" +
                "    if (horn)  _vehicle.PlayHorn();\n" +
                "    if (drift) _vehicle.SetDrift(true);\n" +
                "}");

            win.Note("Action Type trong profile phải là <b>Axis</b> để GetAxisAction() trả về giá trị đúng. Button type sẽ chỉ trả về 0 hoặc 1.");
        }
    }
}
#endif
