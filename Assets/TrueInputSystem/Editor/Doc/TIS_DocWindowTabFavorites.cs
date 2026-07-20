// ============================================================
//  TrueInputSystem — TIS_DocWindow_Tab_Favorites.cs
//  Tab: Favorites — Các API Hay Dùng + Code Ví Dụ
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 10 — FAVORITES (Subtab state)
        // ══════════════════════════════════════════════════════

        private int _favSubTab = 0;

        private readonly string[] _favSubLabels = {
            "MoveDirection",        // 0
            "LookDelta",            // 1
            "ZoomDelta",            // 2
            "IsJumping",            // 3
            "IsRunning",            // 4
            "IsAttacking",          // 5
            "GetSkillAction",       // 6
            "GetVector2Action",     // 7
            "GetAxisAction",        // 8
            "TryRumble",            // 9
            "GesturePanDelta",      // 10
            "ToucherCount",         // 11
            "ToucherGetLastID",     // 12
            "DoubleTapOnObject",    // 13
            "DoubleTapOnScreen",    // 14
            "GetDoubleTapped",      // 15
            "ToucherClear",         // 16
            "SetObjTracker",        // 17
            "SetTrackerCamera",     // 18
            "GetSingleObject",      // 19
            "SetCooldownFill",      // 20
            "SetDisabled",          // 21
        };

        private void DrawTab_Favorites()
        {
            // ── Single scroll wrapping sidebar + content (mirrors Tab Code pattern)
            var scroll = Scroll("fav_main");
            var newScroll = EditorGUILayout.BeginScrollView(scroll);
            SetScroll("fav_main", newScroll);

            GUILayout.BeginHorizontal();

            // ── Left sidebar ────────────────────────────────────
            DrawFavSubTabSidebar();

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
                switch (_favSubTab)
                {
                    case 0:  DrawFav_MoveDirection();       break;
                    case 1:  DrawFav_LookDelta();           break;
                    case 2:  DrawFav_ZoomDelta();           break;
                    case 3:  DrawFav_IsJumping();           break;
                    case 4:  DrawFav_IsRunning();           break;
                    case 5:  DrawFav_IsAttacking();         break;
                    case 6:  DrawFav_GetSkillAction();      break;
                    case 7:  DrawFav_GetVector2Action();    break;
                    case 8:  DrawFav_GetAxisAction();       break;
                    case 9:  DrawFav_TryRumble();           break;
                    case 10: DrawFav_GesturePanDelta();     break;
                    case 11: DrawFav_ToucherCount();        break;
                    case 12: DrawFav_ToucherGetLastID();    break;
                    case 13: DrawFav_DoubleTapOnObject();   break;
                    case 14: DrawFav_DoubleTapOnScreen();   break;
                    case 15: DrawFav_GetDoubleTapped();     break;
                    case 16: DrawFav_ToucherClear();        break;
                    case 17: DrawFav_SetObjTracker();       break;
                    case 18: DrawFav_SetTrackerCamera();    break;
                    case 19: DrawFav_GetSingleObject();     break;
                    case 20: DrawFav_SetCooldownFill();     break;
                    case 21: DrawFav_SetDisabled();         break;
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

        // ══════════════════════════════════════════════════════
        //  SUBTAB SIDEBAR (vertical) — mirrors DrawCodeSidebar
        // ══════════════════════════════════════════════════════

        private const float FavSidebarW = 130f;

        private void DrawFavSubTabSidebar()
        {
            GUILayout.BeginVertical(GUILayout.Width(FavSidebarW));

            // Section header
            var headerSt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = TxtMuted },
                padding   = new RectOffset(14, 14, 14, 6),
                margin    = new RectOffset(0, 0, 0, 0),
            };
            GUILayout.Label("API HAY DÙNG", headerSt);

            for (int i = 0; i < _favSubLabels.Length; i++)
            {
                bool isActive = _favSubTab == i;

                // Row rect
                Rect rowRect = GUILayoutUtility.GetRect(FavSidebarW, 30,
                    GUILayout.Width(FavSidebarW), GUILayout.Height(30));

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
                GUI.Label(rowRect, _favSubLabels[i], labelSt);

                // Click
                if (Event.current.type == EventType.MouseDown
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _favSubTab = i;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════
        //  HELPER — FavApiBox: khung signature màu accent
        // ══════════════════════════════════════════════════════

        private void FavApiBox(string className, string memberName, string returnType, string description, Color accent)
        {
            Rect boxRect = GUILayoutUtility.GetRect(0, 64, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(boxRect, BgSurface);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, 4, boxRect.height), accent);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - 1, boxRect.width, 1), BorderSub);

            // Return type badge
            var retBg = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f);
            var retSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = accent, background = Tex(retBg) },
                padding = new RectOffset(6, 6, 3, 3),
            };
            float retW = 70;
            GUI.Label(new Rect(boxRect.xMax - retW - 10, boxRect.y + 10, retW, 18), returnType, retSt);

            // Class · Member
            var classSt = new GUIStyle(EditorStyles.label) { fontSize = 10, normal = { textColor = TxtMuted }, padding = new RectOffset(14, 0, 8, 0) };
            var memberSt = new GUIStyle(EditorStyles.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = accent }, padding = new RectOffset(14, 0, 0, 0) };
            var descSt = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, normal = { textColor = TxtSecond }, padding = new RectOffset(14, 84, 0, 0) };

            GUI.Label(new Rect(boxRect.x, boxRect.y,     boxRect.width, 20), className, classSt);
            GUI.Label(new Rect(boxRect.x, boxRect.y + 18, boxRect.width, 22), memberName, memberSt);
            GUI.Label(new Rect(boxRect.x, boxRect.y + 40, boxRect.width, 20), description, descSt);

            GUILayout.Space(6);
        }

        // ══════════════════════════════════════════════════════
        //  0 — MoveDirection
        // ══════════════════════════════════════════════════════

        private void DrawFav_MoveDirection()
        {
            FavApiBox("TISInputBridge", "MoveDirection", "Vector2", "Di chuyển từ WASD / LeftStick / Joystick ảo — chuẩn hóa trong deadzone.", AccentBlue);
            H2("Cú Pháp");
            CodeBlock("Vector2 dir = TISInputBridge.Instance.MoveDirection;");

            Sep();
            H2("Ví Dụ — Di Chuyển Nhân Vật Đơn Giản");
            CodeBlock(
                "private TISInputBridge _input;\n\n" +
                "void Start() => _input = TISInputBridge.Instance;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Di Chuyển Theo Hướng Camera (World Space)");
            CodeBlock(
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

            Sep();
            H2("Ví Dụ — Tốc Độ Analog (Gamepad Stick Magnitude)");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 move  = _input.MoveDirection;\n" +
                "    float   speed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.Clamp01(move.magnitude));\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            Note("MoveDirection trả về Vector2 đã qua deadzone và normalization. Dùng .magnitude để đọc mức độ đẩy analog stick (0 → 1).");
            Tip("OverrideMoveInput(v2) để inject từ Joystick ảo hoặc AI. ClearMoveOverride() hoàn trả input thật.");
        }

        // ══════════════════════════════════════════════════════
        //  1 — LookDelta
        // ══════════════════════════════════════════════════════

        private void DrawFav_LookDelta()
        {
            FavApiBox("TISInputBridge", "LookDelta", "Vector2", "Delta xoay camera frame này — chuột / right stick / kéo ngón. Tiêu thụ mỗi frame.", AccentBlue);
            H2("Cú Pháp");
            CodeBlock("Vector2 look = TISInputBridge.Instance.LookDelta;");

            Sep();
            H2("Ví Dụ — Xoay Yaw & Pitch Thủ Công");
            CodeBlock(
                "private float _yaw, _pitch;\n" +
                "private float sensitivityH = 2f, sensitivityV = 1.5f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    Vector2 look = _input.LookDelta;\n" +
                "    _yaw   += look.x * sensitivityH;\n" +
                "    _pitch -= look.y * sensitivityV;\n" +
                "    _pitch  = Mathf.Clamp(_pitch, -80f, 80f);\n\n" +
                "    transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Override LookDelta Từ Swipe Zone");
            CodeBlock(
                "// TISTouchCameraZone tự gọi nội bộ:\n" +
                "// _input.OverrideLookInput(swipeDelta);\n" +
                "// _input.ClearLookOverride();\n\n" +
                "// Nếu bạn muốn inject thủ công:\n" +
                "_input.OverrideLookInput(new Vector2(dx, dy));\n" +
                "// Khi nhả ngón:\n" +
                "_input.ClearLookOverride();");
        }

        // ══════════════════════════════════════════════════════
        //  2 — ZoomDelta
        // ══════════════════════════════════════════════════════

        private void DrawFav_ZoomDelta()
        {
            FavApiBox("TISInputBridge", "ZoomDelta", "float", "Pinch zoom / scroll wheel frame này. Tự reset về 0 sau mỗi frame — chỉ đọc 1 lần.", AccentAmber);
            H2("Cú Pháp");
            CodeBlock("float zoom = TISInputBridge.Instance.ZoomDelta;");

            Sep();
            H2("Ví Dụ — Zoom Camera Thủ Công");
            CodeBlock(
                "private float _distance = 5f;\n" +
                "private float _minDist  = 1f, _maxDist = 15f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    float zoom = _input.ZoomDelta;          // đọc đúng 1 lần\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * zoomSpeed, _minDist, _maxDist);\n\n" +
                "    // Áp dụng khoảng cách:\n" +
                "    Vector3 dir = -transform.forward;\n" +
                "    cam.transform.position = transform.position + dir * _distance;\n" +
                "}");

            Sep();
            H2("Ví Dụ — Override ZoomDelta Từ Pinch Gesture");
            CodeBlock(
                "// Trong pinch recognizer:\n" +
                "_input.OverrideZoomInput(pinchDelta);\n\n" +
                "// Khi gesture kết thúc:\n" +
                "_input.ClearZoomOverride();");

            Warn("Chỉ đọc ZoomDelta đúng 1 lần mỗi frame. Giá trị reset về 0 sau lần đọc — đọc nhiều lần sẽ cho 0 ở các lần sau.");
        }

        // ══════════════════════════════════════════════════════
        //  3 — IsJumping
        // ══════════════════════════════════════════════════════

        private void DrawFav_IsJumping()
        {
            FavApiBox("TISInputBridge", "IsJumping", "bool", "Shortcut nhảy — đọc theo readMode được cấu hình trong profile.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("bool jumping = TISInputBridge.Instance.IsJumping;");

            Sep();
            H2("Ví Dụ — Nhảy Cơ Bản");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsJumping && _isGrounded)\n" +
                "    {\n" +
                "        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);\n" +
                "        _cam?.NotifyJump(0.4f);   // bypass pivot lag\n" +
                "    }\n" +
                "}");

            Sep();
            H2("Ví Dụ — Nhảy Với Coyote Time");
            CodeBlock(
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

            Warn("IsJumping đọc InputAction trực tiếp, KHÔNG qua TISMobileButtonRegistry. Trên Mobile, cần patch thêm check registry — xem tab Nâng Cao.");
        }

        // ══════════════════════════════════════════════════════
        //  4 — IsRunning
        // ══════════════════════════════════════════════════════

        private void DrawFav_IsRunning()
        {
            FavApiBox("TISInputBridge", "IsRunning", "bool", "Shortcut sprint — đọc theo readMode trong profile. Patch mobile nếu cần.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("bool running = TISInputBridge.Instance.IsRunning;");

            Sep();
            H2("Ví Dụ — Tốc Độ Chạy / Đi");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float speed = _input.IsRunning ? runSpeed : walkSpeed;\n" +
                "    Vector2 move = _input.MoveDirection;\n" +
                "    transform.Translate(new Vector3(move.x, 0, move.y) * speed * Time.deltaTime);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Animation Blend Tree");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float speed = _input.MoveDirection.magnitude;\n" +
                "    if (_input.IsRunning) speed *= 2f;   // blend tree 0=idle,1=walk,2=run\n" +
                "    _animator.SetFloat(\"Speed\", speed, 0.1f, Time.deltaTime);\n" +
                "}");

            Warn("IsRunning đọc InputAction trực tiếp, KHÔNG qua TISMobileButtonRegistry. Patch giống IsJumping nếu dùng Mobile button.");
        }

        // ══════════════════════════════════════════════════════
        //  5 — IsAttacking
        // ══════════════════════════════════════════════════════

        private void DrawFav_IsAttacking()
        {
            FavApiBox("TISInputBridge", "IsAttacking", "bool", "Shortcut tấn công — đọc theo readMode trong profile.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("bool attacking = TISInputBridge.Instance.IsAttacking;");

            Sep();
            H2("Ví Dụ — Tấn Công Cơ Bản");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsAttacking)\n" +
                "        _animator.SetTrigger(\"Attack\");\n" +
                "}");

            Sep();
            H2("Ví Dụ — Tấn Công Có Cooldown");
            CodeBlock(
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

        // ══════════════════════════════════════════════════════
        //  6 — GetSkillAction
        // ══════════════════════════════════════════════════════

        private void DrawFav_GetSkillAction()
        {
            FavApiBox("TISInputBridge", "GetSkillAction(name) / IsSkillDown / IsSkillHeld / IsSkillReleased", "bool", "Đọc button theo tên action — theo profile hoặc mode tường minh.", AccentGreen);
            H2("Cú Pháp — 5 Overload");
            CodeBlock(
                "// Đọc theo readMode đã config trong Profile:\n" +
                "bool result = _input.GetSkillAction(\"ActionName\");\n\n" +
                "// Đọc với mode tường minh (bỏ qua profile readMode):\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.PressDown);\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.Hold);\n" +
                "bool result = _input.GetSkillAction(\"ActionName\", TISReadMode.PressUp);\n\n" +
                "// Shortcut tường minh:\n" +
                "bool down     = _input.IsSkillDown(\"ActionName\");     // PressDown — 1 frame nhấn\n" +
                "bool held      = _input.IsSkillHeld(\"ActionName\");     // Hold      — suốt khi giữ\n" +
                "bool released  = _input.IsSkillReleased(\"ActionName\"); // PressUp   — 1 frame nhả");

            Sep();
            H2("Ví Dụ — GetSkillAction Cơ Bản");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GetSkillAction(\"Attack\"))  DoAttack();\n" +
                "    if (_input.GetSkillAction(\"Skill1\"))  UseDash();\n" +
                "    if (_input.GetSkillAction(\"Skill2\"))  UseShield();\n" +
                "}");

            Sep();
            H2("Ví Dụ — Charge Mechanic (3 Mode Cùng 1 Action)");
            CodeBlock(
                "private float _chargeTime;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.IsSkillDown(\"Fire\"))     BeginCharge();\n" +
                "    if (_input.IsSkillHeld(\"Fire\"))     ContinueCharge(ref _chargeTime);\n" +
                "    if (_input.IsSkillReleased(\"Fire\")) ReleaseCharge(_chargeTime);\n" +
                "}");

            Sep();
            H2("Ví Dụ — ADS / Aim Down Sight");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    bool aiming = _input.IsSkillHeld(\"Aim\");\n" +
                "    _cam?.SetZoomEnabled(!aiming);        // tắt scroll zoom khi ADS\n" +
                "    float fov = aiming ? adsFOV : normalFOV;\n" +
                "    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, adsLerpSpeed * Time.deltaTime);\n" +
                "}");

            Tip("readMode trong Profile: PressDown = 1 frame khi nhấn (default), Hold = true suốt khi giữ, PressUp = 1 frame khi nhả.");
        }

        // ══════════════════════════════════════════════════════
        //  7 — GetVector2Action
        // ══════════════════════════════════════════════════════

        private void DrawFav_GetVector2Action()
        {
            FavApiBox("TISInputBridge", "GetVector2Action(name)", "Vector2", "Đọc Vector2 action theo tên — dành cho UI navigate, map scroll, RTS pan.", AccentBlue);
            H2("Cú Pháp");
            CodeBlock("Vector2 v2 = _input.GetVector2Action(\"ActionName\");");

            Sep();
            H2("Ví Dụ — UI Menu Navigate");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 nav = _input.GetVector2Action(\"Navigate\");\n" +
                "    if (Mathf.Abs(nav.y) > 0.5f)\n" +
                "        SelectMenuItem(nav.y > 0 ? -1 : 1);\n" +
                "}");

            Sep();
            H2("Ví Dụ — RTS Map Scroll");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    Vector2 pan = _input.GetVector2Action(\"MapScroll\");\n" +
                "    Vector3 move = new Vector3(pan.x, 0, pan.y) * panSpeed * Time.deltaTime;\n" +
                "    transform.position += move;\n" +
                "}");

            Note("Profile cần action type = Vector2. Binding PC: <Keyboard>/wasd composite hoặc <Mouse>/delta. Gamepad: <Gamepad>/leftStick.");
        }

        // ══════════════════════════════════════════════════════
        //  8 — GetAxisAction
        // ══════════════════════════════════════════════════════

        private void DrawFav_GetAxisAction()
        {
            FavApiBox("TISInputBridge", "GetAxisAction(name)", "float", "Đọc float analog — trigger, throttle, brake, zoom. Có apply deadzone & trigger curve.", AccentAmber);
            H2("Cú Pháp");
            CodeBlock("float value = _input.GetAxisAction(\"ActionName\");");

            Sep();
            H2("Ví Dụ — Xe: Gas & Brake");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float gas   = _input.GetAxisAction(\"Accelerate\"); // <Gamepad>/rightTrigger\n" +
                "    float brake = _input.GetAxisAction(\"Brake\");      // <Gamepad>/leftTrigger\n" +
                "    float horn  = _input.GetSkillAction(\"Horn\") ? 1f : 0f;\n\n" +
                "    ApplyThrottle(gas);\n" +
                "    ApplyBrake(brake);\n" +
                "    if (horn > 0f) PlayHorn();\n" +
                "}");

            Sep();
            H2("Ví Dụ — Zoom Camera Qua Axis");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    float zoom = _input.GetAxisAction(\"CameraZoom\"); // <Mouse>/scroll/y\n" +
                "    _distance = Mathf.Clamp(_distance - zoom * 0.5f, minDist, maxDist);\n" +
                "}");

            Note("Profile: action type = Axis. Binding PC: <Mouse>/scroll/y hoặc <Keyboard>/e (sẽ return 0 hoặc 1). Gamepad: <Gamepad>/rightTrigger.");
        }

        // ══════════════════════════════════════════════════════
        //  9 — TryRumble
        // ══════════════════════════════════════════════════════

        private void DrawFav_TryRumble()
        {
            FavApiBox("TISInputBridge", "TryRumble / StopRumble", "void", "Rung tay cầm gamepad — ngắn có thời gian hoặc liên tục cho đến khi StopRumble().", AccentAmber);
            H2("Cú Pháp — 3 Overload");
            CodeBlock(
                "// Rung trong dur giây rồi tự dừng:\n" +
                "_input.TryRumble(float lowFreq, float highFreq, float duration);\n\n" +
                "// Rung liên tục — phải gọi StopRumble() thủ công:\n" +
                "_input.TryRumble(float lowFreq, float highFreq);\n\n" +
                "// Dừng rung ngay lập tức:\n" +
                "_input.StopRumble();");

            Sep();
            H2("Ví Dụ — Impact / Hit");
            CodeBlock(
                "void OnHit()\n" +
                "{\n" +
                "    _input.TryRumble(0.6f, 0.3f, 0.15f);  // mạnh - nhẹ - 150ms\n" +
                "}");

            Sep();
            H2("Ví Dụ — Charge (Rung Liên Tục)");
            CodeBlock(
                "void OnBeginCharge()  => _input.TryRumble(0.2f, 0.1f);  // bắt đầu charge\n" +
                "void OnReleaseCharge() => _input.StopRumble();            // thả ra");

            Sep();
            H2("Ví Dụ — Theo Sức Mạnh Đòn");
            CodeBlock(
                "void OnAttack(float power) // power: 0 → 1\n" +
                "{\n" +
                "    _input.TryRumble(\n" +
                "        Mathf.Lerp(0.1f, 0.8f, power),   // lowFreq\n" +
                "        Mathf.Lerp(0.05f, 0.5f, power),  // highFreq\n" +
                "        Mathf.Lerp(0.05f, 0.3f, power)   // duration\n" +
                "    );\n" +
                "}");

            Tip("lowFreq = rung nặng (HD Rumble trái). highFreq = rung nhỏ/nhanh (HD Rumble phải). Cả hai trong dải 0–1.");
            Note("TryRumble chỉ hoạt động khi enableRumble = true trong TISGamepadSettings và có gamepad kết nối.");
        }

        // ══════════════════════════════════════════════════════
        //  10 — GestureTwoFingerPanDelta
        // ══════════════════════════════════════════════════════

        private void DrawFav_GesturePanDelta()
        {
            FavApiBox("TISInputBridge", "GestureTwoFingerPanDelta", "Vector2", "Delta midpoint 2 ngón frame này, đã nhân sensitivity — dùng cho pan 2 ngón.", AccentBlue);
            H2("Cú Pháp");
            CodeBlock(
                "// Delta đã nhân sensitivity:\n" +
                "Vector2 pan = _input.GestureTwoFingerPanDelta;\n\n" +
                "// Delta chưa nhân sensitivity (raw pixel):\n" +
                "Vector2 panRaw = _input.GestureTwoFingerPanRaw;\n\n" +
                "// Kiểm tra điều kiện trước:\n" +
                "bool isPanning = _input.GestureIsTwoFingerPanning;");

            Sep();
            H2("Ví Dụ — Pan Map / Top-Down Camera");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.GestureIsTwoFingerPanning) return;\n\n" +
                "    Vector2 pan = _input.GestureTwoFingerPanDelta;\n" +
                "    Vector3 move = new Vector3(-pan.x, 0, -pan.y) * panSensitivity * Time.deltaTime;\n" +
                "    transform.position += move;\n" +
                "}");

            Note("GestureIsTwoFingerPanning = true khi đúng 2 ngón trong camera zone đang di chuyển cùng hướng. Kết hợp với ZoomDelta để làm pinch-to-zoom đồng thời.");
        }

        // ══════════════════════════════════════════════════════
        //  11 — ToucherCount
        // ══════════════════════════════════════════════════════

        private void DrawFav_ToucherCount()
        {
            FavApiBox("TISInputBridge", "ToucherCount", "int", "Số lượng touch đã ghi nhận từ Object Tracker. 0 nếu tracker bị tắt.", AccentAmber);
            H2("Cú Pháp");
            CodeBlock("int count = _input.ToucherCount;");

            Sep();
            H2("Ví Dụ — Hiển Thị Số Lần Chạm");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    touchCountLabel.text = $\"Đã chạm: {_input.ToucherCount} object(s)\";\n" +
                "}");

            Sep();
            H2("Ví Dụ — Kích Hoạt Khi Đã Chạm Đủ Object");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherCount >= requiredTouchCount)\n" +
                "        UnlockDoor();\n" +
                "}");

            Note("ToucherCount đếm tất cả touch ghi nhận từ đầu session, không reset tự động. Gọi ToucherClearAll() để reset về 0.");
        }

        // ══════════════════════════════════════════════════════
        //  12 — ToucherGetLastID
        // ══════════════════════════════════════════════════════

        private void DrawFav_ToucherGetLastID()
        {
            FavApiBox("TISInputBridge", "ToucherGetLastID()", "int", "InstanceID của GameObject được touch trúng lần cuối cùng. 0 nếu chưa có.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock(
                "int lastID = _input.ToucherGetLastID();\n\n" +
                "// Tên để debug:\n" +
                "string name = _input.ToucherGetLastName();");

            Sep();
            H2("Ví Dụ — Xử Lý Object Được Touch Cuối");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    int id = _input.ToucherGetLastID();\n" +
                "    if (id == 0) return;   // chưa touch trúng gì\n\n" +
                "    var go = EditorUtility.InstanceIDToObject(id) as GameObject;\n" +
                "    // hoặc dùng ObjectRegistry của bạn để tra cứu:\n" +
                "    if (_registry.TryGetValue(id, out var enemy))\n" +
                "        HighlightEnemy(enemy);\n" +
                "}");
        }

        // ══════════════════════════════════════════════════════
        //  13 — ToucherIsDoubleTapOnObject
        // ══════════════════════════════════════════════════════

        private void DrawFav_DoubleTapOnObject()
        {
            FavApiBox("TISInputBridge", "ToucherIsDoubleTapOnObject(id)", "bool", "true đúng 1 frame khi double-tap vào object có InstanceID cho trước.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("bool hit = _input.ToucherIsDoubleTapOnObject(gameObject.GetInstanceID());");

            Sep();
            H2("Ví Dụ — Double-Tap Để Chọn Đơn Vị");
            CodeBlock(
                "// Gắn script này lên mỗi unit:\n" +
                "private TISInputBridge _input;\n" +
                "private int _myID;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _input = TISInputBridge.Instance;\n" +
                "    _myID  = gameObject.GetInstanceID();\n" +
                "}\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnObject(_myID))\n" +
                "        SelectThisUnit();\n" +
                "}");

            Note("Chỉ true đúng 1 frame. Cần SetObjectTrackerEnabled(true) và SetTrackerCamera(cam) trước khi dùng.");
        }

        // ══════════════════════════════════════════════════════
        //  14 — ToucherIsDoubleTapOnScreen
        // ══════════════════════════════════════════════════════

        private void DrawFav_DoubleTapOnScreen()
        {
            FavApiBox("TISInputBridge", "ToucherIsDoubleTapOnScreen()", "bool", "true đúng 1 frame khi double-tap bất kỳ vị trí màn hình.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("bool doubleTap = _input.ToucherIsDoubleTapOnScreen();");

            Sep();
            H2("Ví Dụ — Double-Tap Để Dash");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        DoDash();\n" +
                "}");

            Sep();
            H2("Ví Dụ — Double-Tap Để Mở Map");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        mapPanel.SetActive(!mapPanel.activeSelf);\n" +
                "}");

            Tip("Khác với ToucherIsDoubleTapOnObject: method này không raycast, chỉ kiểm tra gesture trên toàn màn hình.");
        }

        // ══════════════════════════════════════════════════════
        //  15 — ToucherGetDoubleTappedObject
        // ══════════════════════════════════════════════════════

        private void DrawFav_GetDoubleTapped()
        {
            FavApiBox("TISInputBridge", "ToucherGetDoubleTappedObject()", "GameObject", "GameObject bị double-tap frame này. null nếu không có hoặc không raycast trúng.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock("GameObject obj = _input.ToucherGetDoubleTappedObject();");

            Sep();
            H2("Ví Dụ — Xử Lý Object Bị Double-Tap");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    GameObject tapped = _input.ToucherGetDoubleTappedObject();\n" +
                "    if (tapped == null) return;\n\n" +
                "    if (tapped.CompareTag(\"Enemy\"))\n" +
                "        LockOnTarget(tapped.transform);\n" +
                "    else if (tapped.CompareTag(\"Item\"))\n" +
                "        PickUpItem(tapped);\n" +
                "}");

            Note("Trả về null nếu frame này không có double-tap, hoặc raycast không trúng GameObject có Collider. Cần Object Tracker đang bật.");
        }

        // ══════════════════════════════════════════════════════
        //  16 — ToucherClear
        // ══════════════════════════════════════════════════════

        private void DrawFav_ToucherClear()
        {
            FavApiBox("TISInputBridge", "ToucherClearList / ToucherClearAll / ToucherClearTouchCount", "void", "Reset trạng thái Object Tracker — dùng khi chuyển scene hoặc sau khi xử lý gesture.", AccentRed);
            H2("Cú Pháp — 3 Method");
            CodeBlock(
                "// Xóa danh sách touched objects (giữ lại bộ đếm & double-tap):\n" +
                "_input.ToucherClearList();\n\n" +
                "// Xóa toàn bộ trạng thái tracker (list + double-tap state):\n" +
                "_input.ToucherClearAll();\n\n" +
                "// Reset bộ đếm touch nội bộ về 0 (dùng sau khi xử lý xong gesture):\n" +
                "_input.ToucherClearTouchCount();");

            Sep();
            H2("Ví Dụ — Chuyển Scene");
            CodeBlock(
                "void OnSceneUnload(Scene s, LoadSceneMode m)\n" +
                "{\n" +
                "    _input.ToucherClearAll();   // reset sạch trước khi load scene mới\n" +
                "}");

            Sep();
            H2("Ví Dụ — Sau Khi Xử Lý Gesture");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureIsTwoFingerPanning)\n" +
                "    {\n" +
                "        // ... xử lý pan ...\n" +
                "    }\n" +
                "    else if (_input.ToucherCount > 0)\n" +
                "    {\n" +
                "        ProcessTouchedObjects();\n" +
                "        _input.ToucherClearTouchCount(); // reset sau khi xử lý xong\n" +
                "    }\n" +
                "}");
        }

        // ══════════════════════════════════════════════════════
        //  17 — SetObjectTrackerEnabled
        // ══════════════════════════════════════════════════════

        private void DrawFav_SetObjTracker()
        {
            FavApiBox("TISInputBridge", "SetObjectTrackerEnabled(bool)", "void", "Bật/tắt Object Tracker lúc runtime. Tắt để tiết kiệm tài nguyên raycast mỗi frame.", AccentRed);
            H2("Cú Pháp");
            CodeBlock("_input.SetObjectTrackerEnabled(bool enabled);");

            Sep();
            H2("Ví Dụ — Tắt Khi Không Cần");
            CodeBlock(
                "// Bật khi mở puzzle / selection mode:\n" +
                "void EnterSelectionMode()\n" +
                "{\n" +
                "    _input.SetObjectTrackerEnabled(true);\n" +
                "}\n\n" +
                "// Tắt khi thoát:\n" +
                "void ExitSelectionMode()\n" +
                "{\n" +
                "    _input.ToucherClearAll();\n" +
                "    _input.SetObjectTrackerEnabled(false);\n" +
                "}");

            Tip("Object Tracker thực hiện Raycast mỗi frame khi bật. Chỉ bật khi cần touch picking để tối ưu performance.");
        }

        // ══════════════════════════════════════════════════════
        //  18 — SetTrackerCamera
        // ══════════════════════════════════════════════════════

        private void DrawFav_SetTrackerCamera()
        {
            FavApiBox("TISInputBridge", "SetTrackerCamera(Camera)", "void", "Đổi Camera dùng để raycast cho Object Tracker lúc runtime.", AccentRed);
            H2("Cú Pháp");
            CodeBlock("_input.SetTrackerCamera(Camera cam);");

            Sep();
            H2("Ví Dụ — Đổi Camera Khi Spectate");
            CodeBlock(
                "void OnEnterSpectate(Camera spectatorCam)\n" +
                "{\n" +
                "    _input.SetTrackerCamera(spectatorCam);\n" +
                "}\n\n" +
                "void OnExitSpectate(Camera playerCam)\n" +
                "{\n" +
                "    _input.SetTrackerCamera(playerCam);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Setup Lúc Start");
            CodeBlock(
                "void Start()\n" +
                "{\n" +
                "    _input = TISInputBridge.Instance;\n" +
                "    _input.SetTrackerCamera(Camera.main);\n" +
                "    _input.SetObjectTrackerEnabled(true);\n" +
                "}");
        }

        // ══════════════════════════════════════════════════════
        //  19 — ToucherGetSingleObject
        // ══════════════════════════════════════════════════════

        private void DrawFav_GetSingleObject()
        {
            FavApiBox("TISInputBridge", "ToucherGetSingleObject()", "GameObject", "GameObject đang touch nếu đúng 1 touch active. null nếu 0 hoặc >1 ngón.", AccentGreen);
            H2("Cú Pháp");
            CodeBlock(
                "GameObject obj = _input.ToucherGetSingleObject();\n\n" +
                "// Kiểm tra đếm không raycast:\n" +
                "bool isSingle = _input.ToucherIsSingleTouch();");

            Sep();
            H2("Ví Dụ — Single Touch Để Di Chuyển Object");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    GameObject obj = _input.ToucherGetSingleObject();\n" +
                "    if (obj == null) return;\n\n" +
                "    if (obj.CompareTag(\"Draggable\"))\n" +
                "    {\n" +
                "        // Đang touch đúng 1 ngón vào draggable object\n" +
                "        DragObject(obj);\n" +
                "    }\n" +
                "}");

            Sep();
            H2("Ví Dụ — Phân Biệt 1 Ngón vs 2 Ngón");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureIsTwoFingerPanning)\n" +
                "    {\n" +
                "        PanCamera(_input.GestureTwoFingerPanDelta);\n" +
                "    }\n" +
                "    else\n" +
                "    {\n" +
                "        GameObject target = _input.ToucherGetSingleObject();\n" +
                "        if (target != null) SelectTarget(target);\n" +
                "    }\n" +
                "}");

            Note("ToucherIsSingleTouch() chỉ đếm ngón, không raycast. ToucherGetSingleObject() raycast để trả về object — nặng hơn, dùng khi thực sự cần object reference.");
        }


        // ══════════════════════════════════════════════════════
        //  20 — TISMobileSkillButton.SetCooldownFill
        // ══════════════════════════════════════════════════════

        private void DrawFav_SetCooldownFill()
        {
            FavApiBox("TISMobileSkillButton", "SetCooldownFill(float)", "void", "Cập nhật thanh cooldown. 0 = full cooldown (button bị disable). 1 = sẵn sàng.", AccentAmber);
            H2("Cú Pháp");
            CodeBlock(
                "btn.SetCooldownFill(float fill); // fill: 0 → 1\n\n" +
                "// 0 = full cooldown (button disable)\n" +
                "// 1 = ready (button enable)\n" +
                "// 0.5 = nửa cooldown");

            Sep();
            H2("Yêu Cầu Setup");
            CodeBlock(
                "// Inspector TISMobileSkillButton:\n" +
                "// 1. Tạo Image con (child) với Image Type = Filled\n" +
                "//    Fill Method = Radial360 hoặc Horizontal\n" +
                "//    Fill Origin = Top\n" +
                "// 2. Kéo Image đó vào field 'Cooldown Fill' trong Inspector");

            Sep();
            H2("Ví Dụ — Coroutine Cooldown Đơn Giản");
            CodeBlock(
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

            Sep();
            H2("Ví Dụ — Cập Nhật Trong Update");
            CodeBlock(
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

            Note("SetCooldownFill() tự gọi SetDisabled(fill < 1) nội bộ — không cần gọi SetDisabled() thêm khi dùng cooldown fill.");
        }

        // ══════════════════════════════════════════════════════
        //  21 — TISMobileSkillButton.SetDisabled
        // ══════════════════════════════════════════════════════

        private void DrawFav_SetDisabled()
        {
            FavApiBox("TISMobileSkillButton", "SetDisabled(bool)", "void", "Tắt button tạm thời — stun, cooldown, charge. Ưu tiên THẤP hơn Interactable.", AccentRed);
            H2("Cú Pháp");
            CodeBlock(
                "btn.SetDisabled(bool disabled);\n\n" +
                "// Khóa hoàn toàn (ưu tiên cao hơn):\n" +
                "btn.Interactable = false;");

            Sep();
            H2("Interactable vs SetDisabled");
            CodeBlock(
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

            Sep();
            H2("Ví Dụ — Stun");
            CodeBlock(
                "[SerializeField] private TISMobileSkillButton[] _skillButtons;\n\n" +
                "IEnumerator StunRoutine(float duration)\n" +
                "{\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.SetDisabled(true);\n\n" +
                "    yield return new WaitForSeconds(duration);\n\n" +
                "    foreach (var btn in _skillButtons)\n" +
                "        btn.SetDisabled(false);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Disable Khi Thiếu Mana");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    bool hasEnoughMana = _mana >= _skillManaCost;\n" +
                "    _skillBtn.SetDisabled(!hasEnoughMana);\n" +
                "}");

            Sep();
            H2("Ví Dụ — Khóa Khi Nhân Vật Chết");
            CodeBlock(
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

            Tip("Dùng btn.OnPress.AddListener(() => ...) và btn.OnRelease.AddListener(() => ...) để xử lý event nhấn/nhả trong Inspector hoặc code.");
        }

    }  // partial class TISDocWindow
}
#endif