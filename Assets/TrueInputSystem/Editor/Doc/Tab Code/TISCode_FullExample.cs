// ============================================================
//  TrueInputSystem — TISCode_FullExample.cs
//  Code Sub-Tab Module: Full Example
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_FullExample : ICodeSubTabModule
    {
        public string Label => "Full Example";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Full Example — Controller Hoàn Chỉnh",
                "Script tổng hợp: move, jump, run, skills, aim assist, rumble — hoạt động trên PC, Gamepad và Mobile.");

            win.H2("MyPlayerController.cs — Script Hoàn Chỉnh");
            win.CodeBlock(
                "using UnityEngine;\n" +
                "using TrueInputSystem;\n\n" +
                "public class MyPlayerController : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] private float walkSpeed = 5f;\n" +
                "    [SerializeField] private float runSpeed  = 9f;\n" +
                "    [SerializeField] private TISTargetScanner _scanner;\n\n" +
                "    private TISInputBridge      _input;\n" +
                "    private CharacterController _cc;\n\n" +
                "    readonly AimAssistSettings _aimSettings = new AimAssistSettings(\n" +
                "        snapRadius: 8f, assistSpeed: 6f, assistWithoutInput: false,\n" +
                "        enabledPlatforms: AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile\n" +
                "    );\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _input = TISInputBridge.Instance;\n" +
                "        _cc    = GetComponent<CharacterController>();\n" +
                "        TISRebindingSystem.LoadAndApply(_input);\n" +
                "    }\n\n" +
                "    void Update()      { HandleMovement(); HandleSkills(); }\n" +
                "    void LateUpdate()  { HandleCamera(); }\n\n" +
                "    void HandleMovement()\n" +
                "    {\n" +
                "        Vector2 move  = _input.MoveDirection;\n" +
                "        float   speed = _input.IsRunning ? runSpeed : walkSpeed;\n" +
                "        speed *= Mathf.Clamp01(move.magnitude);\n\n" +
                "        _cc.Move(new Vector3(move.x, 0, move.y).normalized * speed * Time.deltaTime);\n" +
                "        if (_input.IsJumping) DoJump();\n" +
                "    }\n\n" +
                "    void HandleSkills()\n" +
                "    {\n" +
                "        if (_input.IsAttacking)              Attack();\n" +
                "        if (_input.GetSkillAction(\"Skill_1\")) CastFireball();\n" +
                "        if (_input.GetSkillAction(\"Skill_2\")) Dash();\n\n" +
                "        if (TISMobileButtonRegistry.WasPressedThisFrame(\"Skill_3\"))\n" +
                "        {\n" +
                "            Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill_3_Dir\");\n" +
                "            CastDirectionalSkill(dir);\n" +
                "        }\n" +
                "    }\n\n" +
                "    void HandleCamera()\n" +
                "    {\n" +
                "        Vector2 look = _input.LookDelta;\n" +
                "        if (_scanner != null && _scanner.HasTarget)\n" +
                "            look = _input.AimAssistProcess(_aimSettings, Camera.main.transform,\n" +
                "                _scanner.CurrentAimPoint, look, 0.45f);\n" +
                "        _yaw   += look.x;\n" +
                "        _pitch  = Mathf.Clamp(_pitch - look.y, -80f, 80f);\n" +
                "        Camera.main.transform.eulerAngles = new Vector3(_pitch, _yaw, 0);\n" +
                "    }\n\n" +
                "    public void OnHit(float damage)\n" +
                "    {\n" +
                "        float intensity = Mathf.Clamp01(damage / 100f);\n" +
                "        _input.TryRumble(intensity * 0.6f, intensity * 0.3f, 0.15f);\n" +
                "    }\n\n" +
                "    void OnDestroy() => _input?.StopRumble();\n\n" +
                "    float _yaw, _pitch;\n" +
                "    void DoJump()                           { /* ... */ }\n" +
                "    void Attack()                           { /* ... */ }\n" +
                "    void CastFireball()                     { /* ... */ }\n" +
                "    void Dash()                             { /* ... */ }\n" +
                "    void CastDirectionalSkill(Vector2 dir)  { /* ... */ }\n" +
                "}");

            GUILayout.Space(8);
            win.Tip("Script này hoạt động trên PC, Gamepad và Mobile mà không cần thay đổi bất kỳ dòng nào.");
        }
    }
}
#endif
