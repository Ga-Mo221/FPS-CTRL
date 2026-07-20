// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_Code.cs
//  Tab: Code (all sub-tabs)
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 5 — DÙNG TRONG CODE
        // ══════════════════════════════════════════════════════

        private void DrawTab_Code()
        {
            // ── Single scroll wrapping sidebar + content ────────
            var scroll = Scroll("code_main");
            var newScroll = EditorGUILayout.BeginScrollView(scroll);
            SetScroll("code_main", newScroll);

            GUILayout.BeginHorizontal();

            // ── Left sidebar ────────────────────────────────────
            DrawCodeSidebar();

            // ── Vertical divider ────────────────────────────────
            Rect divRect = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(divRect, BorderSub);

            // ── Right content ───────────────────────────────────
            GUILayout.BeginVertical();
            GUILayout.Space(24);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.BeginVertical();

            try
            {
                switch (_codeSubTab)
                {
                    case 0:  DrawCode_Reference();        break;
                    case 1:  DrawCode_Movement();         break;
                    case 2:  DrawCode_Camera();           break;
                    case 3:  DrawCode_Shortcuts();        break;
                    case 4:  DrawCode_SkillAction();      break;
                    case 5:  DrawCode_Vector2Axis();      break;
                    case 6:  DrawCode_MobileButton();     break;
                    case 7:  DrawCode_ActionMap();        break;
                    case 8:  DrawCode_ProfileSwap();      break;
                    case 9:  DrawCode_Rumble();           break;
                    case 10: DrawCode_Rebinding();        break;
                    case 11: DrawCode_DirectionalButton();break;
                    case 12: DrawCode_TargetScanner();    break;
                    case 13: DrawCode_MobileInputSetup(); break;
                    case 14: DrawCode_AimAssistDeadzone();break;
                    case 15: DrawCode_GamepadCursor();    break;
                    case 16: DrawCode_FullExample();      break;
                    case 17: DrawCode_GestureDetector();  break;
                    case 18: DrawCode_ObjectTracker();    break;
                    case 19: DrawCode_GamepadUIButton();  break;
                    case 20: DrawCode_PointerPosition();  break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            GUILayout.Space(40);
            GUILayout.EndVertical();
            GUILayout.Space(24);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private const float CodeSidebarW = 130f;

        private void DrawCodeSidebar()
        {
            GUILayout.BeginVertical(GUILayout.Width(CodeSidebarW));

            // Section header
            var headerSt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = TxtMuted },
                padding   = new RectOffset(14, 14, 14, 6),
                margin    = new RectOffset(0, 0, 0, 0),
            };
            GUILayout.Label("VÍ DỤ CODE", headerSt);

            for (int i = 0; i < _codeSubLabels.Length; i++)
            {
                bool isActive = _codeSubTab == i;

                // Row rect
                Rect rowRect = GUILayoutUtility.GetRect(CodeSidebarW, 30,
                    GUILayout.Width(CodeSidebarW), GUILayout.Height(30));

                // Background
                if (isActive)
                {
                    EditorGUI.DrawRect(rowRect, SidebarActiveBg);
                    // Left accent bar
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height), AccentBlue);
                }
                else if (rowRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(rowRect, SidebarHoverBg);
                }

                // Bottom separator
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1),
                    SidebarRowSep);

                // Label
                var labelSt = new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 12,
                    fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap  = false,
                    alignment = TextAnchor.MiddleLeft,
                    normal    = { textColor = isActive ? AccentBlue : TxtSecond },
                    padding   = new RectOffset(isActive ? 14 : 12, 8, 0, 0),
                };
                GUI.Label(rowRect, _codeSubLabels[i], labelSt);

                // Click
                if (Event.current.type == EventType.MouseDown
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _codeSubTab = i;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
        }

        // ── Sub-tab pages ──────────────────────────────────────

        private void DrawCode_Reference()
        {
            PageTitle("Lấy Reference", "Bước đầu tiên: lấy singleton TISInputBridge trong script của bạn.");

            Body("Toàn bộ API input đều truy cập qua <b>TISInputBridge.Instance</b>. " +
                 "Singleton tự tìm — không cần kéo thả vào Inspector.");

            GUILayout.Space(8);
            H2("Namespace & Cache Instance — Khởi Tạo Trong Start()");
            CodeBlock(
                "using TrueInputSystem;\n\n" +
                "public class MyScript : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        // Singleton tự khởi tạo — không cần assign Inspector\n" +
                "        _input = TISInputBridge.Instance;\n" +
                "        if (_input == null)\n" +
                "            Debug.LogError(\"TISInputBridge không tìm thấy trong scene!\");\n" +
                "    }\n" +
                "}");

            Note("Luôn cache vào field _input thay vì gọi TISInputBridge.Instance mỗi frame để tránh overhead.");

            Sep();
            H2("Đọc Profile Đang Active — ActiveProfile.profileName");
            Body("ActiveProfile trả về TISInputProfileConfig hiện tại — dùng để log, debug hoặc hiển thị tên profile:");
            CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n" +
                "if (input != null)\n" +
                "    Debug.Log(\"Profile hiện tại: \" + input.ActiveProfile.profileName);");

            Sep();
            H2("Đọc Settings Từ Active Profile — Camera / Mobile / Gamepad");
            Body("Truy cập nhanh vào Camera, Mobile và Gamepad settings mà không cần đi qua ActiveProfile:");
            CodeBlock(
                "// Camera settings\n" +
                "var camSettings = TISInputBridge.Instance.TISCameraSettings;\n" +
                "Debug.Log(\"Sensitivity: \" + camSettings.mouseSensitivity);\n\n" +
                "// Gamepad settings\n" +
                "var gpSettings = TISInputBridge.Instance.TISGamepadSettings;\n" +
                "Debug.Log(\"Rumble enabled: \" + gpSettings.enableRumble);\n\n" +
                "// Mobile settings\n" +
                "var mobileSettings = TISInputBridge.Instance.TISMobileSettings;");
        }

        private void DrawCode_Movement()
        {
            PageTitle("Di Chuyển — MoveDirection", "Vector2 thống nhất từ WASD, Gamepad LeftStick và Mobile Joystick.");

            Body("MoveDirection tự động chọn đúng nguồn input theo platform — không cần if/else.");
            GUILayout.Space(8);

            H2("Cơ Bản — Đọc MoveDirection Và Di Chuyển Đơn Giản");
            CodeBlock(
                "// Vector2 (-1 đến 1) — đồng nhất trên mọi platform\n" +
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// Di chuyển đơn giản\n" +
                "transform.Translate(move.x * speed * Time.deltaTime,\n" +
                "                    0,\n" +
                "                    move.y * speed * Time.deltaTime);");

            Sep();
            H2("Kết Hợp Với Camera (World Space)");
            Body("Chuyển MoveDirection sang world space theo hướng camera để nhân vật di chuyển đúng chiều nhìn:");
            CodeBlock(
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// Chuyển sang world space theo hướng camera\n" +
                "Vector3 dir = cam.TransformDirection(new Vector3(move.x, 0, move.y));\n" +
                "dir.y = 0;  // loại bỏ pitch\n" +
                "dir.Normalize();\n\n" +
                "controller.Move(dir * speed * Time.deltaTime);");

            Sep();
            H2("Tốc Độ Analog (Stick Gamepad)");
            Body("Dùng magnitude để đọc mức độ đẩy stick — 0 khi đứng yên, 1 khi đẩy hết:");
            CodeBlock(
                "Vector2 move = _input.MoveDirection;\n\n" +
                "// magnitude = 0 khi đứng yên, 1 khi đẩy hết\n" +
                "float speed = walkSpeed * Mathf.Clamp01(move.magnitude);\n\n" +
                "// Kết hợp với run\n" +
                "if (_input.IsRunning)\n" +
                "    speed = runSpeed * Mathf.Clamp01(move.magnitude);");

            Note("MoveDirection tự động dùng override từ TISVirtualJoystick trên mobile — không cần if(mobile) else(pc).");

            Sep();
            H2("Override MoveDirection (Virtual Joystick / AI / Replay)");
            Body("Dùng OverrideMoveInput() để inject giá trị tùy ý vào MoveDirection, thay thế input thật từ InputSystem:");
            CodeBlock(
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

        private void DrawCode_Camera()
        {
            PageTitle("Camera Input — LookDelta & ZoomDelta", "Xoay camera và zoom thống nhất: chuột, gamepad right stick và kéo ngón tay.");

            H2("LookDelta — Xoay Camera");
            Body("PC: delta chuột | Gamepad: right stick | Mobile: kéo ngón trên CameraZone. Tự chuẩn hoá mouse delta (chia 100):");
            CodeBlock(
                "Vector2 look = _input.LookDelta;\n\n" +
                "// Áp dụng vào yaw / pitch\n" +
                "yaw   += look.x * sensitivity;\n" +
                "pitch -= look.y * sensitivity;\n" +
                "pitch  = Mathf.Clamp(pitch, -80f, 80f);\n\n" +
                "transform.rotation = Quaternion.Euler(pitch, yaw, 0f);");

            Sep();
            H2("ZoomDelta — Pinch Zoom / Scroll Wheel");
            Body("Consume pattern — tự reset về 0 sau mỗi lần đọc. Dương = zoom in, âm = zoom out:");
            CodeBlock(
                "float zoom = _input.ZoomDelta;\n\n" +
                "if (Mathf.Abs(zoom) > 0.001f)\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * zoomSpeed,\n" +
                "                            minDistance, maxDistance);");

            Note("ZoomDelta được consume mỗi frame — chỉ đọc 1 lần trong Update(). Đọc nhiều lần sẽ luôn nhận 0 ở lần thứ 2.");

            Sep();
            H2("Override LookDelta (Camera Swipe Zone / Replay)");
            Body("Inject delta xoay từ vùng kéo ngón tay, AI director, hoặc replay system:");
            CodeBlock(
                "// Trong TISTouchCameraZone — inject delta từ ngón tay\n" +
                "public void OnCameraZoneDrag(Vector2 delta)\n" +
                "{\n" +
                "    TISInputBridge.Instance.OverrideLookInput(delta * sensitivity);\n" +
                "}\n\n" +
                "// Gọi khi nhả ngón — hoàn trả lại input thật\n" +
                "public void OnCameraZoneRelease()\n" +
                "{\n" +
                "    TISInputBridge.Instance.ClearLookOverride();\n" +
                "}");

            Sep();
            H2("Override ZoomDelta (Pinch Gesture)");
            Body("TISTouchCameraZone gọi method này nội bộ. Bạn cũng có thể gọi từ gesture recognizer tự viết:");
            CodeBlock(
                "// Inject pinch zoom từ gesture recognizer\n" +
                "float pinchDelta = GetPinchDelta();\n" +
                "TISInputBridge.Instance.OverrideZoomInput(pinchDelta);\n\n" +
                "// Khi kết thúc gesture\n" +
                "TISInputBridge.Instance.ClearZoomOverride();");
        }

        private void DrawCode_Shortcuts()
        {
            PageTitle("Button Shortcut", "Properties tiện lợi cho Jump, Run, Attack và IsSkillDown/Held/Released.");

            H2("3 Shortcut Properties Thường Dùng");
            Body("Đọc trực tiếp, không cần biết tên action trong profile:");
            CodeBlock(
                "bool jump   = _input.IsJumping;    // Space / buttonSouth / on-screen Jump\n" +
                "bool run    = _input.IsRunning;     // LeftShift / leftStickPress\n" +
                "bool attack = _input.IsAttacking;   // Mouse0 / buttonSouth / Attack btn");

            Sep();
            H2("Dùng Trong Update — IsJumping / IsRunning / IsAttacking");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsJumping)   DoJump();\n" +
                "    if (_input.IsRunning)   SetSprint(true);\n" +
                "    if (_input.IsAttacking) Attack();\n" +
                "}");

            Sep();
            H2("ReadMode — Khi Nào Trigger?");
            Body("Mỗi action có thể config <b>readMode</b> trong TISInputActionDefinition:");
            CodeBlock(
                "PressDown  // true đúng 1 frame khi nhấn — mặc định cho button\n" +
                "Hold       // true suốt khi đang giữ phím\n" +
                "PressUp    // true đúng 1 frame khi nhả phím");

            Sep();
            H2("IsSkillDown / IsSkillHeld / IsSkillReleased");
            Body("Shortcut cho GetSkillAction với mode tường minh — bất kể readMode trong profile:");
            CodeBlock(
                "// PressDown — true đúng 1 frame khi nhấn\n" +
                "if (_input.IsSkillDown(\"Dodge\"))     DoDodge();\n\n" +
                "// Hold — true suốt khi đang giữ\n" +
                "if (_input.IsSkillHeld(\"Shield\"))    BlockDamage();\n\n" +
                "// PressUp — true đúng 1 frame khi nhả\n" +
                "if (_input.IsSkillReleased(\"Aim\"))   ExitAimMode();");

            Sep();
            H2("Charge Mechanic — Dùng Cả 3 Mode Trên 1 Action");
            Body("Khi cần đọc nhấn + giữ + nhả trên cùng 1 action, dùng IsSkillDown/Held/Released:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillDown(\"Charge\"))     BeginCharge();\n" +
                "    if (_input.IsSkillHeld(\"Charge\"))     ContinueCharge();\n" +
                "    if (_input.IsSkillReleased(\"Charge\")) ReleaseCharge();\n" +
                "}");

            Tip("IsJumping và IsRunning không check TISMobileButtonRegistry mặc định. Xem tab Nâng Cao để patch thêm mobile support.");
        }

        private void DrawCode_SkillAction()
        {
            PageTitle("GetSkillAction", "API tổng quát cho mọi button action — tìm xuyên suốt tất cả Action Map.");

            H2("Cú Pháp — 2 Overload");
            CodeBlock(
                "// Overload 1: dùng readMode được cấu hình trong profile\n" +
                "bool result = _input.GetSkillAction(\"actionName\");\n\n" +
                "// Overload 2: truyền mode tường minh — bỏ qua readMode trong profile\n" +
                "bool result = _input.GetSkillAction(\"actionName\", TISButtonReadMode.Hold);");

            Sep();
            H2("Ví Dụ Thực Tế — Nhiều Skill");
            CodeBlock(
                "void HandleSkills()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Attack\"))  DoAttack();\n" +
                "    if (_input.GetSkillAction(\"Skill_1\")) CastFireball();\n" +
                "    if (_input.GetSkillAction(\"Skill_2\")) Dash();\n" +
                "    if (_input.GetSkillAction(\"Skill_3\")) Shield();\n" +
                "    if (_input.GetSkillAction(\"Brake\"))   ApplyBrake();\n" +
                "    if (_input.GetSkillAction(\"Horn\"))    PlayHorn();\n" +
                "}");

            Sep();
            H2("Charge Mechanic — Overload 2 Với 3 Mode");
            Body("Dùng overload 2 khi cần đọc cả 3 event (nhấn, giữ, nhả) trên cùng 1 action:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.PressDown))\n" +
                "        BeginCharge();\n\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.Hold))\n" +
                "        ContinueCharge();\n\n" +
                "    if (_input.GetSkillAction(\"Charge\", TISButtonReadMode.PressUp))\n" +
                "        ReleaseCharge();\n" +
                "}");

            Sep();
            H2("Một Action — Nhiều Platform Binding");
            Body("GetSkillAction trả về true nếu <b>bất kỳ</b> binding nào active (PC, Gamepad, hoặc Mobile):");
            CodeBlock(
                "// Profile: Action \"Jump\"\n" +
                "//   bindingPC     = <Keyboard>/space\n" +
                "//   bindingGP     = <Gamepad>/buttonSouth\n" +
                "//   bindingMobile = Button_0\n\n" +
                "// Code — không đổi dù platform nào:\n" +
                "if (_input.GetSkillAction(\"Jump\")) DoJump();");

            Sep();
            H2("2 Phím Cho Cùng 1 Action");
            Body("Thêm action phụ với binding thứ hai, rồi OR cả hai trong code:");
            CodeBlock(
                "// Profile: thêm action \"Jump2\" với bindingPC = <Keyboard>/numpad0\n" +
                "if (_input.GetSkillAction(\"Jump\") || _input.GetSkillAction(\"Jump2\"))\n" +
                "    DoJump();");

            Note("Tên trong GetSkillAction(\"tên\") phải khớp actionName trong profile. Case-insensitive nhưng nên viết nhất quán.");
        }

        private void DrawCode_ActionMap()
        {
            PageTitle("Action Map", "Bật / tắt nhóm input theo context: lên xe, cutscene, death...");

            H2("SetActionMapActive");
            CodeBlock(
                "_input.SetActionMapActive(\"Movement\", true);\n" +
                "_input.SetActionMapActive(\"Movement\", false);");

            Sep();
            H2("Ví Dụ: Lên / Xuống Xe");
            CodeBlock(
                "void OnEnterVehicle()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Movement\", false);\n" +
                "    _input.SetActionMapActive(\"Skill\",    false);\n" +
                "    _input.SetActionMapActive(\"Vehicle\",  true);\n" +
                "}\n\n" +
                "void OnExitVehicle()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Vehicle\",  false);\n" +
                "    _input.SetActionMapActive(\"Movement\", true);\n" +
                "    _input.SetActionMapActive(\"Skill\",    true);\n" +
                "}");

            Sep();
            H2("Ví Dụ: Mở / Đóng Inventory UI");
            CodeBlock(
                "void OnOpenInventory()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Combat\", false);\n" +
                "    _input.SetActionMapActive(\"UI\",     true);\n" +
                "}\n\n" +
                "void OnCloseInventory()\n" +
                "{\n" +
                "    _input.SetActionMapActive(\"Combat\", true);\n" +
                "    _input.SetActionMapActive(\"UI\",     false);\n" +
                "}");

            Sep();
            H2("Tắt / Bật Toàn Bộ — DisableAllMaps & EnableDefaultMaps");
            Body("Dùng khi cutscene, loading, nhân vật chết — không muốn bất kỳ input nào hoạt động:");
            CodeBlock(
                "// Tắt hết\n" +
                "void OnCutsceneStart() => _input.DisableAllMaps();\n\n" +
                "// Bật lại theo enabledByDefault trong profile\n" +
                "void OnCutsceneEnd()   => _input.EnableDefaultMaps();");

            Sep();
            H2("enabledByDefault Trong Profile — Map Nào Bật / Tắt Khi Load");
            Body("Maps nên tắt mặc định (vehicle, ui-only) sẽ không active trước khi bạn gọi SetActionMapActive():");
            CodeBlock(
                "// TISActionMapData trong Profile\n" +
                "mapName          = \"Vehicle\"\n" +
                "enabledByDefault = false   // tắt lúc game bắt đầu\n\n" +
                "// Chỉ bật khi người chơi lên xe:\n" +
                "_input.SetActionMapActive(\"Vehicle\", true);");

            Tip("Dùng EnableDefaultMaps() sau cutscene hoặc respawn thay vì bật từng map một.");

            Sep();
            H2("Debug — In Profile Hiện Tại Ra Console Với LogActiveProfile()");
            CodeBlock(
                "// Gọi từ Debug menu trong game\n" +
                "debugButton.onClick.AddListener(() => _input.LogActiveProfile());");
        }

        private void DrawCode_ProfileSwap()
        {
            PageTitle("Profile & Events", "SwapProfile runtime và lắng nghe OnProfileSwapped.");

            H2("SwapProfile — Đổi Toàn Bộ Cấu Hình Input");
            Body("Rebuild InputActionAsset, xóa mobile registry, fire OnProfileSwapped. Dùng khi game mode thay đổi hoàn toàn:");
            CodeBlock(
                "[SerializeField] TISInputProfileConfig _vehicleProfile;\n" +
                "[SerializeField] TISInputProfileConfig _characterProfile;\n\n" +
                "void OnEnterVehicle() => TISInputBridge.Instance.SwapProfile(_vehicleProfile);\n" +
                "void OnExitVehicle()  => TISInputBridge.Instance.SwapProfile(_characterProfile);");

            Sep();
            H2("SwapProfile vs SetActionMapActive");
            CodeBlock(
                "// SwapProfile        — thay TOÀN BỘ cấu hình (khác game mode hoàn toàn)\n" +
                "// SetActionMapActive — chỉ bật/tắt nhóm trong profile hiện tại\n\n" +
                "// ✓ Dùng SetActionMapActive khi: lên xe, cutscene, death\n" +
                "// ✓ Dùng SwapProfile khi:        chuyển adventure → vehicle racing mode");

            Warn("SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll() — button mobile bị reset. Dùng SetActionMapActive() nếu muốn giữ state.");

            Sep();
            H2("OnProfileSwapped — Lắng Nghe Sau Khi Swap");
            Body("Event fire sau khi SwapProfile() hoàn tất, truyền profile mới làm argument:");
            CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _input.OnProfileSwapped += OnProfileChanged;\n" +
                "}\n\n" +
                "void OnProfileChanged(TISInputProfileConfig newProfile)\n" +
                "{\n" +
                "    Debug.Log(\"Profile đã chuyển sang: \" + newProfile.profileName);\n" +
                "    RefreshControlHints(newProfile);\n" +
                "}\n\n" +
                "void OnDestroy()\n" +
                "{\n" +
                "    if (_input != null)\n" +
                "        _input.OnProfileSwapped -= OnProfileChanged;\n" +
                "}");

            Note("SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll() — button mobile bị reset. Dùng SetActionMapActive() nếu muốn giữ state.");
        }

        private void DrawCode_FullExample()
        {
            PageTitle("Full Example — Controller Hoàn Chỉnh", "Script tổng hợp: move, jump, run, skills, aim assist, rumble — hoạt động trên PC, Gamepad và Mobile.");

            H2("MyPlayerController.cs — Script Hoàn Chỉnh");
            CodeBlock(
                "using UnityEngine;\n" +
                "using TrueInputSystem;\n\n" +
                "public class MyPlayerController : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] private float walkSpeed = 5f;\n" +
                "    [SerializeField] private float runSpeed  = 9f;\n" +
                "    [SerializeField] private TISTargetScanner _scanner; // gắn lên Camera\n\n" +
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
                "        // Load rebinding đã lưu\n" +
                "        TISRebindingSystem.LoadAndApply(_input);\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        HandleMovement();\n" +
                "        HandleSkills();\n" +
                "    }\n\n" +
                "    void LateUpdate()\n" +
                "    {\n" +
                "        HandleCamera();\n" +
                "    }\n\n" +
                "    void HandleMovement()\n" +
                "    {\n" +
                "        Vector2 move  = _input.MoveDirection;\n" +
                "        float   speed = _input.IsRunning ? runSpeed : walkSpeed;\n" +
                "        speed *= Mathf.Clamp01(move.magnitude);\n\n" +
                "        Vector3 dir = new Vector3(move.x, 0, move.y);\n" +
                "        _cc.Move(dir.normalized * speed * Time.deltaTime);\n\n" +
                "        if (_input.IsJumping) DoJump();\n" +
                "    }\n\n" +
                "    void HandleSkills()\n" +
                "    {\n" +
                "        if (_input.IsAttacking)              Attack();\n" +
                "        if (_input.GetSkillAction(\"Skill_1\")) CastFireball();\n" +
                "        if (_input.GetSkillAction(\"Skill_2\")) Dash();\n\n" +
                "        // Directional skill — đọc từ registry\n" +
                "        if (TISMobileButtonRegistry.WasPressedThisFrame(\"Skill_3\"))\n" +
                "        {\n" +
                "            Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill_3_Dir\");\n" +
                "            CastDirectionalSkill(dir);\n" +
                "        }\n" +
                "    }\n\n" +
                "    void HandleCamera()\n" +
                "    {\n" +
                "        Vector2 look = _input.LookDelta;\n\n" +
                "        // Aim assist nếu có target\n" +
                "        if (_scanner != null && _scanner.HasTarget)\n" +
                "            look = _input.AimAssistProcess(\n" +
                "                _aimSettings, Camera.main.transform,\n" +
                "                _scanner.CurrentAimPoint, look, 0.45f\n" +
                "            );\n\n" +
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
                "    void DoJump()               { /* ... */ }\n" +
                "    void Attack()               { /* ... */ }\n" +
                "    void CastFireball()         { /* ... */ }\n" +
                "    void Dash()                 { /* ... */ }\n" +
                "    void CastDirectionalSkill(Vector2 dir) { /* ... */ }\n" +
                "}");

            GUILayout.Space(8);
            Tip("Script này hoạt động trên PC, Gamepad và Mobile mà không cần thay đổi bất kỳ dòng nào.");
        }

        private void DrawCode_Vector2Axis()
        {
            PageTitle("Vector2 / Axis Action", "GetVector2Action và GetAxisAction — đọc các action trả về Vector2 hoặc float.");

            H2("GetVector2Action — Đọc Action Trả Về Vector2");
            Body("Dùng cho WASD composite, stick, menu navigation hoặc bất kỳ action nào có Action Type = Vector2:");
            CodeBlock(
                "// Đọc navigation trong UI menu\n" +
                "Vector2 menuNav = _input.GetVector2Action(\"Navigate\");\n" +
                "if (menuNav.y > 0.5f)  SelectPreviousItem();\n" +
                "if (menuNav.y < -0.5f) SelectNextItem();");

            Sep();
            H2("Ví Dụ: Di Chuyển RTS / TopDown Map");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 scroll = _input.GetVector2Action(\"MapScroll\");\n" +
                "    _mapCamera.transform.Translate(\n" +
                "        scroll.x * panSpeed * Time.deltaTime,\n" +
                "        0,\n" +
                "        scroll.y * panSpeed * Time.deltaTime);\n" +
                "}");

            Sep();
            H2("GetAxisAction — Đọc Action Trả Về float");
            Body("Dùng cho analog trigger (ga xe, zoom), có apply trigger response curve từ profile nếu là gamepad trigger:");
            CodeBlock(
                "// Đọc trigger để điều chỉnh ga xe\n" +
                "float gas = _input.GetAxisAction(\"Accelerate\");\n" +
                "_vehicle.SetThrottle(gas);\n\n" +
                "// Đọc trigger để zoom camera\n" +
                "float aimZoom = _input.GetAxisAction(\"AimZoom\");\n" +
                "_cam.fieldOfView = Mathf.Lerp(normalFOV, aimFOV, aimZoom);");

            Sep();
            H2("Ví Dụ: Điều Khiển Xe Đầy Đủ");
            CodeBlock(
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

            Note("Action Type trong profile phải là <b>Axis</b> để GetAxisAction() trả về giá trị đúng. Button type sẽ chỉ trả về 0 hoặc 1.");
        }

        private void DrawCode_MobileButton()
        {
            PageTitle("Mobile Button", "TISMobileSkillButton — SkillName, HoldTime, Interactable, SetDisabled, Cooldown, VisualMode, Events.");

            H2("Lấy Reference & SkillName");
            CodeBlock(
                "TISMobileSkillButton btn = attackButton.GetComponent<TISMobileSkillButton>();\n\n" +
                "// Tên action được binding (get only) — dùng để hiển thị tooltip\n" +
                "Debug.Log(\"Skill: \" + btn.SkillName); // e.g. \"Attack\"");

            Sep();
            H2("HoldTime — Thời Gian Đang Giữ (giây)");
            Body("<b>HoldTime</b> là public float property — thời gian tính từ lúc nhấn xuống, reset về 0 khi nhả.\n" +
                 "Dùng <c>btn.HoldTime > 0f</c> để kiểm tra ngón đang giữ (không có property IsPressed):");
            CodeBlock(
                "TISMobileSkillButton btn = jumpButton.GetComponent<TISMobileSkillButton>();\n\n" +
                "void Update()\n" +
                "{\n" +
                "    // Kiểm tra đang giữ: HoldTime > 0 khi ngón tay đang chạm\n" +
                "    if (btn.HoldTime > 0f)\n" +
                "        Debug.Log(\"Đang giữ: \" + btn.HoldTime.ToString(\"F2\") + \"s\");\n\n" +
                "    // Charge mechanic — đạt ngưỡng 1.5s\n" +
                "    if (btn.HoldTime >= 1.5f)\n" +
                "        ShowFullChargeEffect();\n" +
                "}");

            Note("Không có property IsPressed. Dùng btn.HoldTime > 0f để kiểm tra ngón đang giữ, " +
                 "hoặc đăng ký OnHold / OnHoldTimed event (xem section bên dưới).");

            Sep();
            H2("Interactable vs SetDisabled — Thứ Tự Ưu Tiên");
            Body("<b>Interactable</b> (ưu tiên cao hơn) — khóa hoàn toàn, không nhận touch, hiển thị notInteractable visual.\n" +
                 "<b>SetDisabled</b> — tắt tạm thời, hiển thị disabled visual tối. Dùng cho stun / stance sai / không đủ mana:");
            CodeBlock(
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

            Sep();
            H2("SetCooldownFill — Thanh Cooldown");
            Body("<b>fillAmount = 1</b> → cooldown đầy (button disabled, Image fill = 1).\n" +
                 "<b>fillAmount = 0</b> → sẵn sàng (button mở khoá, Image fill = 0).\n" +
                 "Cooldown chạy từ 1 xuống 0 — ngược chiều với elapsed:");
            CodeBlock(
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

            Note("Yêu cầu Image con Type=Filled được gán vào field Cooldown Fill trong Inspector. " +
                 "SetCooldownFill tự release button nếu ngón tay đang giữ khi cooldown bắt đầu.");

            Sep();
            H2("OnHold & OnHoldTimed — Callback Mỗi Frame Khi Giữ");
            Body("<b>OnHold</b> — UnityEvent, gọi mỗi frame khi ngón tay đang giữ.\n" +
                 "<b>OnHoldTimed</b> — UnityEvent<float>, truyền thời gian giữ (giây) làm tham số mỗi frame:");
            CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    // OnHold — auto-attack, charge\n" +
                "    btn.OnHold.AddListener(() => AutoAttack());\n\n" +
                "    // OnHoldTimed — cập nhật charge bar theo thời gian giữ\n" +
                "    btn.OnHoldTimed.AddListener(holdTime =>\n" +
                "    {\n" +
                "        float charge = Mathf.Clamp01(holdTime / maxChargeTime);\n" +
                "        chargeBar.fillAmount = charge;\n" +
                "        if (charge >= 1f) ShowFullChargeEffect();\n" +
                "    });\n" +
                "}");

            Sep();
            H2("SetVisualMode — Đổi ColorTint / SpriteSwap Runtime");
            Body("Dùng khi đổi skin nhân vật, chuyển theme UI, hoặc test A/B:");
            CodeBlock(
                "void OnSkinChanged(bool useSpriteSwap)\n" +
                "{\n" +
                "    btn.SetVisualMode(useSpriteSwap\n" +
                "        ? TISMobileSkillButton.VisualMode.SpriteSwap\n" +
                "        : TISMobileSkillButton.VisualMode.ColorTint);\n" +
                "}");

            Note("SpriteSwap cần _normalSprite và _pressedSprite được assign trong Inspector. " +
                 "Nếu _disabledSprite hoặc _notInteractableSprite để null, component tự fade alpha của Normal sprite.");

            Sep();
            H2("OnPress & OnRelease Events");
            Body("UnityEvent — có thể đăng ký qua code hoặc kéo thả trong Inspector:");
            CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    // OnPress — fired 1 lần khi ngón tay chạm xuống\n" +
                "    btn.OnPress.AddListener(() =>\n" +
                "    {\n" +
                "        Debug.Log(\"Nhấn Attack!\");\n" +
                "        PlayAttackSFX();\n" +
                "    });\n\n" +
                "    // OnRelease — fired 1 lần khi nhả / pointer thoát vùng button\n" +
                "    btn.OnRelease.AddListener(() =>\n" +
                "    {\n" +
                "        Debug.Log(\"Nhả Attack\");\n" +
                "        ReleaseChargeSkill();\n" +
                "    });\n" +
                "}");

            Tip("OnRelease cũng được gọi khi ngón tay trượt ra ngoài vùng button (PointerExit) để tránh stuck-pressed. " +
                "Nếu cần đo holdTime chính xác, dùng OnHoldTimed cập nhật mỗi frame thay vì dùng Time.time - startTime trong OnRelease.");
        }

        private void DrawCode_Rumble()
        {
            PageTitle("Gamepad Rumble", "TryRumble và StopRumble — haptic feedback trên gamepad.");

            H2("TryRumble — Rung Có Hẹn Giờ");
            Body("Rung trong duration giây rồi tự dừng. Tham số: lowFrequency (motor to), highFrequency (motor nhỏ):");
            CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n\n" +
                "// Trúng đạn — rung ngắn\n" +
                "void OnHit()     => input.TryRumble(0.6f, 0.3f, 0.15f);\n\n" +
                "// Nổ lớn gần\n" +
                "void OnExplode() => input.TryRumble(1.0f, 0.8f, 0.4f);\n\n" +
                "// Bắn súng ngắn\n" +
                "void OnShoot()   => input.TryRumble(0.0f, 0.35f, 0.05f);\n\n" +
                "// Rung scale theo damage\n" +
                "void OnHit(float damage)\n" +
                "{\n" +
                "    float intensity = Mathf.Clamp(damage / 100f, 0.3f, 2f);\n" +
                "    input.TryRumble(intensity * 0.6f, intensity * 0.3f, 0.2f);\n" +
                "}");

            Sep();
            H2("TryRumble — Rung Liên Tục");
            Body("Không hẹn giờ — gọi StopRumble() để dừng. Dùng cho: đứng gần vực, drift xe, debuff rung:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillHeld(\"Drift\"))\n" +
                "        _input.TryRumble(0.7f, 0.2f);\n" +
                "    else\n" +
                "        _input.StopRumble();\n" +
                "}\n\n" +
                "// Rung khi trong vùng nguy hiểm\n" +
                "void Update2()\n" +
                "{\n" +
                "    if (IsInDangerZone())\n" +
                "        _input.TryRumble(0.4f, 0.1f);\n" +
                "    else\n" +
                "        _input.StopRumble();\n" +
                "}");

            Sep();
            H2("StopRumble — Dừng Ngay Lập Tức");
            Body("Luôn gọi trong OnDisable() hoặc OnDestroy() để tránh controller rung vô hạn khi scene unload:");
            CodeBlock(
                "void OnDisable()\n" +
                "{\n" +
                "    TISInputBridge.Instance?.StopRumble();\n" +
                "}");

            Sep();
            H2("Điều Kiện Hoạt Động");
            Body("Rumble chỉ hoạt động khi tất cả điều kiện sau đều đúng:");
            CodeBlock(
                "// 1. enableRumble = true trong TISInputProfileConfig → tab Gamepad\n" +
                "// 2. Gamepad.current != null (tay cầm đang kết nối)\n" +
                "// 3. Gọi qua TISInputBridge (không gọi Gamepad.current.SetMotorSpeeds trực tiếp)\n\n" +
                "// Kiểm tra nhanh:\n" +
                "if (TISInputBridge.Instance.TISGamepadSettings.enableRumble)\n" +
                "    Debug.Log(\"Rumble enabled\");");

            Warn("Rumble không hoạt động trên mobile. enableRumble = false trong profile mobile để tránh lỗi.");
        }


        private void DrawCode_Rebinding()
        {
            PageTitle("Key Rebinding", "TISRebindingSystem — Interactive, Programmatic, Persistence, Query.");

            H2("Interactive — Chờ Người Dùng Nhấn Phím");
            Body("Gọi StartRebinding() khi người dùng click nút 'Rebind' trong Settings UI:");
            CodeBlock(
                "void OnRebindJumpClicked()\n" +
                "{\n" +
                "    _pressKeyOverlay.SetActive(true); // Hiện 'Press any key...'\n\n" +
                "    TISRebindingSystem.StartRebinding(\n" +
                "        TISInputBridge.Instance,\n" +
                "        \"Jump\",\n" +
                "        RebindTarget.PC,\n" +
                "        result =>\n" +
                "        {\n" +
                "            _pressKeyOverlay.SetActive(false);\n" +
                "            if (result.Success)\n" +
                "                _jumpLabel.text = TISRebindingSystem.GetDisplayName(result.NewPath);\n" +
                "        }\n" +
                "    );\n" +
                "}\n\n" +
                "// Hủy rebinding đang chạy\n" +
                "void OnCancelClicked() => TISRebindingSystem.CancelRebinding();");

            Sep();
            H2("Events Toàn Cục — OnRebindStarted & OnRebindCompleted");
            Body("Subscribe một lần trong Awake(), nhận event cho mọi nút rebind:");
            CodeBlock(
                "void Awake()\n" +
                "{\n" +
                "    TISRebindingSystem.OnRebindStarted   += (action, target) => ShowOverlay(action);\n" +
                "    TISRebindingSystem.OnRebindCompleted += result =>\n" +
                "    {\n" +
                "        HideOverlay();\n" +
                "        if (result.Success) RefreshLabel(result.ActionName, result.NewPath);\n" +
                "    };\n" +
                "}\n\n" +
                "// Đừng quên unsubscribe khi destroy\n" +
                "void OnDestroy()\n" +
                "{\n" +
                "    TISRebindingSystem.OnRebindStarted   -= ...;\n" +
                "    TISRebindingSystem.OnRebindCompleted -= ...;\n" +
                "}");

            Sep();
            H2("Programmatic — ForceApply & ApplyPreset");
            Body("Apply binding bằng code không cần nhấn phím — dùng cho preset FPS/RPG:");
            CodeBlock(
                "// Apply một binding\n" +
                "TISRebindingSystem.ForceApply(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC, \"<Keyboard>/g\"\n" +
                ");\n\n" +
                "// Apply preset toàn bộ\n" +
                "var fpsPreset = new Dictionary<string, string>\n" +
                "{\n" +
                "    { \"Jump\",   \"<Keyboard>/space\" },\n" +
                "    { \"Run\",    \"<Keyboard>/leftShift\" },\n" +
                "    { \"Attack\", \"<Mouse>/leftButton\" },\n" +
                "};\n" +
                "TISRebindingSystem.ApplyPreset(TISInputBridge.Instance, fpsPreset, RebindTarget.PC);");

            Sep();
            H2("Query — Hiển Thị Phím Lên UI");
            CodeBlock(
                "// Lấy path đang active (override nếu có, default nếu không)\n" +
                "string path = TISRebindingSystem.GetActivePath(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC\n" +
                ");\n" +
                "// Chuyển sang tên thân thiện: \"<Keyboard>/space\" → \"Space\"\n" +
                "_jumpLabel.text = TISRebindingSystem.GetDisplayName(path);\n\n" +
                "// Kiểm tra conflict trước khi apply\n" +
                "string conflict = TISRebindingSystem.FindConflict(\n" +
                "    TISInputBridge.Instance, \"Jump\", RebindTarget.PC, \"<Keyboard>/g\"\n" +
                ");\n" +
                "if (conflict != null) ShowWarning($\"{conflict} đang dùng phím này!\");");

            Sep();
            H2("Persistence — Load & Reset");
            CodeBlock(
                "// Gọi trong Awake() — load override đã lưu từ JSON\n" +
                "void Awake()\n" +
                "{\n" +
                "    TISRebindingSystem.LoadAndApply(TISInputBridge.Instance);\n" +
                "}\n\n" +
                "// Reset một binding về default\n" +
                "void OnResetJumpClicked()\n" +
                "    => TISRebindingSystem.ResetBinding(TISInputBridge.Instance, \"Jump\", RebindTarget.PC);\n\n" +
                "// Reset toàn bộ\n" +
                "void OnResetAllClicked()\n" +
                "    => TISRebindingSystem.ResetAllBindings(TISInputBridge.Instance);");

            Note("JSON file lưu tại Application.persistentDataPath/TIS_Bindings_{profileName}.json. Chỉ hỗ trợ Button action — Vector2/Axis/Mobile binding không rebind qua hệ thống này.");
        }

        private void DrawCode_DirectionalButton()
        {
            PageTitle("Directional Skill Button", "TISDirectionalSkillButton — kéo để chọn hướng, nhả để cast (Liên Quân style).");

            H2("Option A — Đọc Trực Tiếp Từ Component");
            CodeBlock(
                "[SerializeField] TISDirectionalSkillButton _skill1Btn;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_skill1Btn.WasCastThisFrame)\n" +
                "    {\n" +
                "        Vector2 dir   = _skill1Btn.CastDirection; // normalized, zero = auto-target\n" +
                "        float   angle = _skill1Btn.DirectionAngle; // 0°=phải, 90°=trên\n" +
                "        SpawnSkill(dir, angle);\n" +
                "    }\n\n" +
                "    // Hiện preview hướng real-time khi đang kéo\n" +
                "    if (_skill1Btn.IsDragging)\n" +
                "        ShowDirectionArrow(_skill1Btn.Direction);\n" +
                "}");

            Sep();
            H2("Option B — Dùng Event (Không Cần Reference)");
            CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _skill1Btn.OnDirectionCast += (skillName, dir) =>\n" +
                "    {\n" +
                "        if (dir == Vector2.zero) AutoTargetCast();\n" +
                "        else                    DirectionalCast(dir);\n" +
                "    };\n\n" +
                "    _skill1Btn.OnCancelled.AddListener(() => CancelSkillPreview());\n" +
                "    _skill1Btn.OnDragStarted.AddListener(() => ShowRangeCircle());\n" +
                "}");

            Sep();
            H2("Option C — TISDirectionalRegistry Pattern");
            Body("Dùng registry giống GetSkillAction — không cần reference component:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    // Kiểm tra cast qua registry (cùng frame)\n" +
                "    if (TISMobileButtonRegistry.WasPressedThisFrame(\"Skill1\"))\n" +
                "    {\n" +
                "        Vector2 dir = TISDirectionalRegistry.GetDirection(\"Skill1_Dir\");\n" +
                "        SpawnSkill1(dir);\n" +
                "    }\n\n" +
                "    // Đọc direction real-time khi đang kéo (qua override action Vector2)\n" +
                "    Vector2 aimDir = TISInputBridge.Instance.GetVector2Action(\"AimDirection\");\n" +
                "    UpdateAimIndicator(aimDir);\n" +
                "}");

            Sep();
            H2("ForceCast — Cast Bằng Code (Tutorial / Automation)");
            CodeBlock(
                "// Cast theo hướng đông-bắc\n" +
                "_skill1Btn.ForceCast(new Vector2(1f, 1f).normalized);\n\n" +
                "// Cast auto-target (zero direction)\n" +
                "_skill1Btn.ForceCast(Vector2.zero);");

            Sep();
            H2("Cooldown Control");
            CodeBlock(
                "IEnumerator CooldownRoutine(TISDirectionalSkillButton btn, float duration)\n" +
                "{\n" +
                "    float elapsed = 0f;\n" +
                "    while (elapsed < duration)\n" +
                "    {\n" +
                "        elapsed += Time.deltaTime;\n" +
                "        btn.SetCooldownFill(1f - elapsed / duration); // 1=full cooldown → 0=ready\n" +
                "        yield return null;\n" +
                "    }\n" +
                "    btn.SetCooldownFill(0f);\n" +
                "}");
        }

        private void DrawCode_TargetScanner()
        {
            PageTitle("TISTargetScanner", "Tự động lock target gần tâm màn hình nhất — kết hợp với AimAssistProcess().");

            H2("Setup");
            Body("Gắn TISTargetScanner lên Camera GameObject. Cấu hình trong Inspector: targetTags, scanAngle, maxDistance, scanInterval.");
            CodeBlock(
                "// Inspector setup:\n" +
                "// ▸ Target Filter: Tags = [\"Enemy\"], hoặc bật useLayerInsteadOfTag\n" +
                "// ▸ Scan Settings: scanAngle = 10°, maxDistance = 50m, scanInterval = 0.1s\n" +
                "// ▸ AimPointMode: ColliderCenter (khuyến nghị)");

            Sep();
            H2("Cơ Bản — HasTarget + AimAssistProcess");
            CodeBlock(
                "[SerializeField] TISTargetScanner _scanner;\n\n" +
                "readonly AimAssistSettings _aimSettings = new AimAssistSettings(\n" +
                "    snapRadius: 8f, assistSpeed: 6f, assistWithoutInput: false,\n" +
                "    enabledPlatforms: AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile\n" +
                ");\n\n" +
                "void LateUpdate()\n" +
                "{\n" +
                "    var bridge = TISInputBridge.Instance;\n" +
                "    Vector2 look = bridge.LookDelta;\n\n" +
                "    if (_scanner.HasTarget)\n" +
                "        look = bridge.AimAssistProcess(\n" +
                "            _aimSettings, Camera.main.transform,\n" +
                "            _scanner.CurrentAimPoint, look, intensity: 0.45f\n" +
                "        );\n\n" +
                "    _yaw   += look.x * sensitivity;\n" +
                "    _pitch  = Mathf.Clamp(_pitch - look.y * sensitivity, -80f, 80f);\n" +
                "    Camera.main.transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);\n" +
                "}");

            Sep();
            H2("ForceScan & ClearTarget");
            CodeBlock(
                "// Quét lại ngay sau khi spawn wave mới\n" +
                "void OnWaveSpawned() => _scanner.ForceScan();\n\n" +
                "// Khi enemy bị destroy\n" +
                "void OnEnemyDied(GameObject enemy)\n" +
                "{\n" +
                "    if (_scanner.CurrentTarget == enemy.transform)\n" +
                "        _scanner.ClearTarget();\n" +
                "}\n\n" +
                "// Debug thông tin target\n" +
                "void LogTarget()\n" +
                "{\n" +
                "    if (_scanner.HasTarget)\n" +
                "        Debug.Log($\"Target: {_scanner.CurrentTarget.name}, Angle: {_scanner.CurrentAngle:F1}°\");\n" +
                "}");
        }

        private void DrawCode_MobileInputSetup()
        {
            PageTitle("TISMobileInputSetup", "Khởi tạo mobile UI, toggle features, chia vùng joystick/camera.");

            H2("Subscribe SetupCompleted & Check IsReady");
            CodeBlock(
                "[SerializeField] TISMobileInputSetup _mobileSetup;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    if (_mobileSetup.IsReady)\n" +
                "        OnMobileReady(_mobileSetup);\n" +
                "    else\n" +
                "        _mobileSetup.SetupCompleted += OnMobileReady;\n" +
                "}\n\n" +
                "void OnMobileReady(TISMobileInputSetup setup)\n" +
                "{\n" +
                "    // Di chuyển phân chia: 40% joystick / 60% camera\n" +
                "    setup.DatTyLeVung(0.4f);\n" +
                "}");

            Sep();
            H2("Settings UI — Toggle & Setter Methods");
            CodeBlock(
                "// Toggle (bật/tắt)\n" +
                "rotateToggle.onValueChanged.AddListener(val => _mobileSetup.DatXoayCamera(val));\n" +
                "zoomToggle.onValueChanged.AddListener(val   => _mobileSetup.DatZoom(val));\n" +
                "joystickToggle.onValueChanged.AddListener(val => _mobileSetup.DatJoystick(val));\n\n" +
                "// Slider điều chỉnh tỉ lệ joystick (0.25 - 0.75)\n" +
                "joystickSizeSlider.onValueChanged.AddListener(val => _mobileSetup.DatTyLeVung(val));\n\n" +
                "// Hoặc dùng Toggle methods (flip giá trị hiện tại)\n" +
                "void OnCameraLockButton() => _mobileSetup.BatTatXoayCamera();");

            Sep();
            H2("Sync Zone Sau Khi Thay Đổi Layout");
            CodeBlock(
                "// Sau khi thay đổi tỉ lệ, sync lại\n" +
                "_mobileSetup.DatTyLeVung(0.35f);\n" +
                "_mobileSetup.DongBoTyLe(); // Đồng bộ joystick + camera zone\n\n" +
                "// Apply cài đặt camera zone\n" +
                "_mobileSetup.ApDungCaiDatCamera();");

            Note("TISMobileInputSetup chỉ active trên mobile platform. Trong Editor nó luôn active để test. Trên PC build component tự SetActive(false).");
        }

        private void DrawCode_AimAssistDeadzone()
        {
            PageTitle("AimAssist & Deadzone", "AimAssistProcess + ApplyDeadzone — hỗ trợ ngắm và xử lý vùng chết analog.");

            H2("AimAssistSettings — Cấu Hình");
            CodeBlock(
                "// Tạo settings (tạo 1 lần, cache lại)\n" +
                "readonly AimAssistSettings _aimSettings = new AimAssistSettings(\n" +
                "    snapRadius:         8f,   // góc độ từ tâm màn hình để kích hoạt\n" +
                "    assistSpeed:        6f,   // tốc độ hút camera về target\n" +
                "    assistWithoutInput: false, // false = chỉ hỗ trợ khi có input (chống aimbot)\n" +
                "    enabledPlatforms:   AimAssistPlatform.Gamepad | AimAssistPlatform.Mobile\n" +
                ");");

            Sep();
            H2("AimAssistProcess — Tích Hợp Với Camera");
            CodeBlock(
                "void LateUpdate()\n" +
                "{\n" +
                "    var bridge = TISInputBridge.Instance;\n" +
                "    Vector2 look = bridge.LookDelta;\n\n" +
                "    // Chỉ áp dụng khi có target\n" +
                "    if (bridge.AimAssistTarget != null)\n" +
                "    {\n" +
                "        look = bridge.AimAssistProcess(\n" +
                "            _aimSettings,\n" +
                "            Camera.main.transform,\n" +
                "            bridge.AimAssistTarget.position,\n" +
                "            look,\n" +
                "            intensity: 0.5f  // 0=tắt, 1=mạnh nhất\n" +
                "        );\n" +
                "    }\n\n" +
                "    _yaw   += look.x;\n" +
                "    _pitch -= look.y;\n" +
                "    transform.eulerAngles = new Vector3(_pitch, _yaw, 0);\n" +
                "}\n\n" +
                "// Gán target từ game script\n" +
                "void OnLockOn(Transform enemy)\n" +
                "    => TISInputBridge.Instance.AimAssistTarget = enemy;\n\n" +
                "void OnLockOff()\n" +
                "    => TISInputBridge.Instance.AimAssistTarget = null;");

            Sep();
            H2("ApplyDeadzone — Scaled Radial Deadzone");
            Body("Dùng khi muốn tự xử lý deadzone cho raw input từ thiết bị:");
            CodeBlock(
                "Vector2 rawStick = new Vector2(\n" +
                "    Input.GetAxis(\"Horizontal\"),\n" +
                "    Input.GetAxis(\"Vertical\")\n" +
                ");\n\n" +
                "// Scaled Radial: input < 0.2f → zero, input = 1f → 1f\n" +
                "// Nội suy mượt mà giữa deadzone và 1 — không bị nhảy đột ngột\n" +
                "Vector2 processed = TISInputBridge.Instance.ApplyDeadzone(rawStick, 0.2f);\n\n" +
                "// Dùng kết quả để di chuyển\n" +
                "transform.Translate(processed.x * speed * Time.deltaTime, 0, processed.y * speed * Time.deltaTime);");

            Note("ApplyDeadzone dùng Scaled Radial formula: (magnitude - deadzone) / (1 - deadzone). Input nhỏ hơn deadzone → Vector2.zero. Input > 1 bị clamp về 1.");
        }

        private void DrawCode_GamepadCursor()
        {
            PageTitle("Gamepad Virtual Cursor", "TISGamepadVirtualCursor — chuột ảo điều khiển bằng left stick gamepad.");

            H2("Setup");
            Step(1, "Add Component", "Gắn TISGamepadVirtualCursor vào bất kỳ GameObject nào trong scene.");
            Step(2, "Assign Inspector", "Kéo RectTransform (UI Image con trỏ) → cursorTransform. Kéo Canvas chứa cursor → parentCanvas.");
            Step(3, "Tự đăng ký", "Component tự gọi bridge.RegisterGamepadCursor(this) trong Awake() — không cần làm gì thêm.");

            Sep();
            H2("Bật / Tắt Cursor");
            CodeBlock(
                "// Khi mở menu: bật cursor, người chơi dùng left stick để di chuyển\n" +
                "void OnOpenMenu()\n" +
                "{\n" +
                "    TISInputBridge.Instance.SetActiveGamepadCursor(true);\n" +
                "    // Cursor tự reset về center màn hình khi bật\n" +
                "}\n\n" +
                "// Khi đóng menu: tắt cursor\n" +
                "void OnCloseMenu()\n" +
                "    => TISInputBridge.Instance.SetActiveGamepadCursor(false);");

            Sep();
            H2("Điều Kiện Hoạt Động");
            CodeBlock(
                "// 1. TISGamepadVirtualCursor component tồn tại trong scene\n" +
                "// 2. Gamepad.current != null\n" +
                "// 3. SetActiveGamepadCursor(true) đã được gọi\n\n" +
                "// Cursor dùng InputSystem.QueueDeltaStateEvent để inject chuột ảo\n" +
                "// → UI EventSystem nhận event click khi nhấn buttonSouth (A/Cross)\n" +
                "// → Tương thích với mọi Unity UI Button, Toggle, Slider");

            Note("TISGamepadVirtualCursor tự tìm TISInputBridge.Instance trong Awake. Assign cursorTransform và parentCanvas trong Inspector là đủ.");
        }

        private void DrawCode_GamepadUIButton()
        {
            PageTitle("TISGamepadUIButton", "UI navigation gamepad nhiều lớp: Tab → SubTab → Content. Kế thừa Unity Button.");

            Body("Hệ thống phân cấp 3 lớp:\n" +
                 "  <b>Tab</b> (LT/RT) → <b>SubTab</b> (LB/RB) → <b>Content</b> (DPad/Stick).\n" +
                 "Tab và SubTab dùng <i>virtual selection</i> (alwaysActive), Content dùng EventSystem thật.");

            GUILayout.Space(8);
            H2("Setup — Khởi Tạo Layer Active");
            CodeBlock(
                "using TrueInputSystem;\n\n" +
                "public class UINavigationContext : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] TISGamepadUIButton _defaultTab;\n" +
                "    [SerializeField] TISGamepadUIButton _defaultSubTab;\n" +
                "    [SerializeField] TISGamepadUIButton _defaultContent;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        // Đặt button đầu tiên là active trong mỗi layer\n" +
                "        _defaultTab.SetAsActiveInLayer();\n" +
                "        _defaultSubTab.SetAsActiveInLayer();\n\n" +
                "        // Content layer dùng isFirstSelected = true trong Inspector\n" +
                "        // hoặc set thủ công:\n" +
                "        EventSystem.current.SetSelectedGameObject(_defaultContent.gameObject);\n" +
                "    }\n" +
                "}");

            Sep();
            H2("Lắng Nghe onBecameActiveInLayer — Redirect Content Focus");
            CodeBlock(
                "// Khi Tab đổi → redirect focus về content button đầu của tab mới\n" +
                "[SerializeField] TISGamepadUIButton _tabA;\n" +
                "[SerializeField] TISGamepadUIButton _firstContentOfTabA;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _tabA.onBecameActiveInLayer.AddListener(OnTabBecameActive);\n" +
                "}\n\n" +
                "void OnTabBecameActive(TISGamepadUIButton activeTab)\n" +
                "{\n" +
                "    // Redirect EventSystem focus về content đầu tiên của tab này\n" +
                "    if (activeTab == _tabA)\n" +
                "        EventSystem.current.SetSelectedGameObject(_firstContentOfTabA.gameObject);\n" +
                "}");

            Sep();
            H2("Bật / Tắt Navigation — Panel Overlay");
            CodeBlock(
                "// Khi mở popup / dialog đè lên → khóa toàn bộ button bên dưới\n" +
                "[SerializeField] TISGamepadUIButton[] _mainMenuButtons;\n\n" +
                "void OnOpenPopup()\n" +
                "{\n" +
                "    foreach (var btn in _mainMenuButtons)\n" +
                "        btn.DisableNavigation();\n" +
                "}\n\n" +
                "void OnClosePopup()\n" +
                "{\n" +
                "    foreach (var btn in _mainMenuButtons)\n" +
                "        btn.EnableNavigation();\n" +
                "}");

            Sep();
            H2("Subscribe onSelect / onDeselect");
            CodeBlock(
                "[SerializeField] TISGamepadUIButton _inventoryTab;\n" +
                "[SerializeField] GameObject _inventoryPanel;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _inventoryTab.onSelect.AddListener(()  => _inventoryPanel.SetActive(true));\n" +
                "    _inventoryTab.onDeselect.AddListener(() => _inventoryPanel.SetActive(false));\n" +
                "}\n\n" +
                "// Hoặc dùng Inspector: kéo thả trong UnityEvent onSelect / onDeselect");

            Sep();
            H2("GetActiveInLayer — Query Từ Bất Kỳ Script Nào");
            CodeBlock(
                "// Lấy Tab đang active\n" +
                "var activeTab = TISGamepadUIButton.GetActiveInLayer(\n" +
                "    TISGamepadUIButton.NavigationLayer.Tab);\n\n" +
                "if (activeTab != null)\n" +
                "    Debug.Log(\"Tab active: \" + activeTab.name);\n\n" +
                "// Lấy SubTab đang active\n" +
                "var activeSub = TISGamepadUIButton.GetActiveInLayer(\n" +
                "    TISGamepadUIButton.NavigationLayer.SubTab);");

            Sep();
            H2("NavMode & InputSource — Cấu Hình Điển Hình");
            CodeBlock(
                "// Tab nằm ngang (LT/RT)\n" +
                "tabButton.navigationMode = TISGamepadUIButton.NavMode.Horizontal;\n" +
                "tabButton.inputSource    = TISGamepadUIButton.InputSource.LT_RT;\n" +
                "tabButton.layer          = TISGamepadUIButton.NavigationLayer.Tab;\n" +
                "tabButton.alwaysActive   = true;\n\n" +
                "// SubTab nằm ngang (LB/RB)\n" +
                "subTabButton.navigationMode = TISGamepadUIButton.NavMode.Horizontal;\n" +
                "subTabButton.inputSource    = TISGamepadUIButton.InputSource.LB_RB;\n" +
                "subTabButton.layer          = TISGamepadUIButton.NavigationLayer.SubTab;\n" +
                "subTabButton.alwaysActive   = true;\n\n" +
                "// Content button (DPad/Stick)\n" +
                "contentButton.navigationMode  = TISGamepadUIButton.NavMode.FourDirectional;\n" +
                "contentButton.inputSource     = TISGamepadUIButton.InputSource.Default;\n" +
                "contentButton.layer           = TISGamepadUIButton.NavigationLayer.Content;\n" +
                "contentButton.isFirstSelected = true;  // tự focus khi panel bật");

            Note("_codeSubLabels array trong TISDocWindow main file cần thêm entry \"Gamepad UI Btn\" ở index 19 và \"Pointer Pos\" ở index 20 để sidebar hiển thị đúng.");
        }

        private void DrawCode_PointerPosition()
        {
            PageTitle("Pointer Position (Drag)", "GetPointerScreenPosition, GetPointerScreenDelta, IsPointerHeld, GetPointerWorldPosition — drag & drop UI và World Space.");

            Body("4 API này hoạt động khi <b>Enable Object Tracker</b> được bật trong Inspector của TISInputBridge. " +
                 "Dùng <b>IsPointerHeld</b> làm guard trước khi đọc vị trí để tránh overhead khi không drag.");

            GUILayout.Space(8);
            H2("IsPointerHeld — Guard Trước Khi Drag");
            Body("Kiểm tra pointer/touch đang được giữ trước khi đọc các API vị trí:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    // Guard: chỉ xử lý khi đang giữ pointer\n" +
                "    if (!TISInputBridge.Instance.IsPointerHeld) return;\n\n" +
                "    // Đọc vị trí screen\n" +
                "    Vector2 screenPos = TISInputBridge.Instance.GetPointerScreenPosition();\n" +
                "    Debug.Log($\"Pointer tại: {screenPos}\");\n" +
                "}");

            Sep();
            H2("API 1 — GetPointerScreenPosition (UI Drag)");
            Body("Vị trí screen space (pixel) của pointer — dùng để kéo RectTransform trong Canvas UI:");
            CodeBlock(
                "using TrueInputSystem;\n\n" +
                "public class DragItem : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n" +
                "    private RectTransform  _rt;\n" +
                "    private Canvas         _canvas;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _input  = TISInputBridge.Instance;\n" +
                "        _rt     = GetComponent<RectTransform>();\n" +
                "        _canvas = GetComponentInParent<Canvas>();\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        if (!_input.IsPointerHeld) return;\n\n" +
                "        // Chuyển screen position sang local Canvas space\n" +
                "        RectTransformUtility.ScreenPointToLocalPointInRectangle(\n" +
                "            _canvas.transform as RectTransform,\n" +
                "            _input.GetPointerScreenPosition(),\n" +
                "            _canvas.worldCamera,\n" +
                "            out Vector2 localPos\n" +
                "        );\n" +
                "        _rt.localPosition = localPos;\n" +
                "    }\n" +
                "}");

            Sep();
            H2("GetPointerScreenDelta — Kéo Bằng Delta (Không Cần Lưu Vị Trí Cũ)");
            Body("Delta pixel so với frame trước — lý tưởng cho drag offset mà không cần cache vị trí:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.IsPointerHeld) return;\n\n" +
                "    Vector2 delta = _input.GetPointerScreenDelta();\n\n" +
                "    // Dịch chuyển RectTransform theo delta\n" +
                "    _rt.anchoredPosition += delta;\n\n" +
                "    // Hoặc scroll một panel\n" +
                "    _scrollRect.content.anchoredPosition += delta;\n" +
                "}");

            Note("GetPointerScreenDelta tự reset về Vector2.zero khi không có pointer active — không cần if check thêm sau IsPointerHeld.");

            Sep();
            H2("API 2 — GetPointerWorldPosition (World Space Drag)");
            Body("Kéo thả GameObject trong không gian 3D. <b>depth</b> = khoảng cách từ Camera đến mặt phẳng đặt object:");
            CodeBlock(
                "public class DragObject3D : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n" +
                "    private float          _depth;\n\n" +
                "    void Start() => _input = TISInputBridge.Instance;\n\n" +
                "    // Khi bắt đầu drag: ghi lại khoảng cách camera→object\n" +
                "    void OnMouseDown()\n" +
                "    {\n" +
                "        _depth = Vector3.Distance(\n" +
                "            Camera.main.transform.position,\n" +
                "            transform.position\n" +
                "        );\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        if (!_input.IsPointerHeld) return;\n\n" +
                "        // Di chuyển object đến vị trí world tương ứng pointer\n" +
                "        transform.position = _input.GetPointerWorldPosition(_depth);\n" +
                "    }\n" +
                "}");

            Sep();
            H2("GetPointerWorldPosition — Game 2D (Orthographic)");
            Body("Với camera Orthographic, depth = khoảng cách tuyệt đối từ camera đến mặt phẳng game:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.IsPointerHeld) return;\n\n" +
                "    // 2D Ortho: depth = |camera.z| (camera thường ở z = -10)\n" +
                "    float depth = Mathf.Abs(Camera.main.transform.position.z);\n" +
                "    Vector3 worldPos = _input.GetPointerWorldPosition(depth);\n\n" +
                "    // Giữ z của object không đổi\n" +
                "    worldPos.z = transform.position.z;\n" +
                "    transform.position = worldPos;\n" +
                "}");

            Sep();
            H2("Kết Hợp ScreenPosition + WorldPosition — Drag & Drop Lên Slot UI");
            Body("Dùng ScreenPosition để di chuyển icon UI, WorldPosition để xác định slot đặt object:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.IsPointerHeld) return;\n\n" +
                "    // Di chuyển icon theo con trỏ\n" +
                "    RectTransformUtility.ScreenPointToLocalPointInRectangle(\n" +
                "        _canvas.transform as RectTransform,\n" +
                "        _input.GetPointerScreenPosition(),\n" +
                "        _canvas.worldCamera,\n" +
                "        out Vector2 localPos\n" +
                "    );\n" +
                "    _dragIcon.localPosition = localPos;\n\n" +
                "    // Highlight slot gần nhất trong world\n" +
                "    Vector3 worldPos = _input.GetPointerWorldPosition(10f);\n" +
                "    HighlightNearestSlot(worldPos);\n" +
                "}");

            Note("Toàn bộ Pointer API trả về zero (Vector2.zero / Vector3.zero) khi không có pointer active. " +
                 "Luôn kiểm tra IsPointerHeld trước để tránh snap đột ngột về origin.");
        }

    }  // partial class TISDocWindow
}
#endif