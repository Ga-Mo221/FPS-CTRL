// ============================================================
//  TrueInputSystem — TIS_DocWindow_Search.cs
//  Search system: state, database, sidebar input, dropdown.
//
//  ĐỂ CẬP NHẬT DATABASE:
//    • Chỉ cần sửa file này — không đụng vào TIS_DocWindow.cs.
//    • Mỗi entry là 1 dòng new SearchEntry { ... }.
//    • TabIndex  → index trong _tabIds / _tabLabels (xem bên dưới).
//    • CodeSubTab → index sub-tab trong Code tab (-1 nếu không phải Code tab).
//    • Keywords  → chuỗi tìm kiếm, cách nhau bởi dấu cách, không phân biệt hoa/thường.
//    • Snippet   → mô tả ngắn hiện trong dropdown.
//
//  TAB INDEX REFERENCE (khớp với _tabIds trong TIS_DocWindow.cs):
//    0 = Favorites
//    1 = Tổng Quan        2 = Setup Scene     3 = Mobile UI
//    4 = Code             5 = API Reference   6 = Setup Tool
// ============================================================

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  SEARCH STATE
        // ══════════════════════════════════════════════════════

        private string  _searchQuery      = "";
        private bool    _searchFocused;
        private bool    _showSearchResults;
        private Vector2 _searchScroll;

        /// <summary>Window-space Y saved each Repaint — used outside BeginArea.</summary>
        private float _searchBoxBottomY;

        // ══════════════════════════════════════════════════════
        //  SEARCH ENTRY
        // ══════════════════════════════════════════════════════

        private struct SearchEntry
        {
            public int    TabIndex;
            public int    CodeSubTab; // -1 = không phải code sub-tab
            public string Title;
            public string Keywords;
            public string Snippet;
        }

        // ══════════════════════════════════════════════════════
        //  SEARCH DATABASE
        //  ► Để thêm/xoá entry, chỉ sửa mảng này.
        //  ► Giữ mỗi entry trên 1 dòng để dễ diff / review.
        // ══════════════════════════════════════════════════════

        private static readonly SearchEntry[] SearchDB =
        {
            // ── Tổng Quan (tab 1) ──────────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=1, CodeSubTab=-1, Title="Tổng Quan — Kiến trúc",                                       Keywords="TISInputBridge Singleton kiến trúc thành phần overview",                                     Snippet="TISInputBridge là điểm duy nhất script game cần giao tiếp." },
            new SearchEntry { TabIndex=1, CodeSubTab=-1, Title="Tổng Quan — Thành Phần",                                      Keywords="TISActionMapData TISVirtualJoystick TISTouchCameraZone component",                            Snippet="Các thành phần chính: Data, Runtime Core, Mobile UI, Camera, Editor." },
            new SearchEntry { TabIndex=1, CodeSubTab=-1, Title="Tổng Quan — Kiến Trúc: Luồng Từ Profile → Script",            Keywords="kiến trúc luồng dữ liệu profile script TISInputBridge singleton flow architecture",           Snippet="Profile → TISInputBridge → Script của bạn. Không cần đọc InputAction trực tiếp." },
            new SearchEntry { TabIndex=1, CodeSubTab=-1, Title="Tổng Quan — Runtime Core: TISInputBridge & Registry",         Keywords="runtime core TISInputBridge singleton TISMobileButtonRegistry dictionary static",               Snippet="TISInputBridge là singleton duy nhất. TISMobileButtonRegistry lưu trạng thái button mobile." },
            new SearchEntry { TabIndex=1, CodeSubTab=-1, Title="Tổng Quan — Mobile UI: Các Component On-Screen",              Keywords="mobile ui TISVirtualJoystick TISMobileSkillButton TISTouchCameraZone TISMobileInputSetup",      Snippet="4 component mobile: VirtualJoystick, SkillButton, TouchCameraZone, MobileInputSetup." },

            // ── Setup Scene (tab 2) ────────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=2, CodeSubTab=-1, Title="Setup — Tạo Profile",                                         Keywords="DefaultProfile asset tạo profile setup",                                                      Snippet="Tạo file .asset cấu hình mặc định qua Tools → True Input System." },
            new SearchEntry { TabIndex=2, CodeSubTab=-1, Title="Setup — TISInputBridge",                                      Keywords="GameObject singleton Awake duplicate profile assign",                                          Snippet="Chỉ có đúng MỘT TISInputBridge trong scene." },
            new SearchEntry { TabIndex=2, CodeSubTab=-1, Title="Setup — Kết Nối Player",                                      Keywords="Start Instance cache _input kết nối player script",                                           Snippet="Lấy reference qua TISInputBridge.Instance trong Start()." },
            new SearchEntry { TabIndex=2, CodeSubTab=-1, Title="Setup — Các Bước Thiết Lập: Thứ Tự Bắt Buộc",                Keywords="bước thiết lập thứ tự profile bridge player script play mode debug",                           Snippet="4 bước theo thứ tự: Tạo Profile → TISInputBridge → Kết nối Player → Kiểm tra Play Mode." },
            new SearchEntry { TabIndex=2, CodeSubTab=-1, Title="Setup — Kết Nối Player: Lấy Instance Trong Start()",          Keywords="kết nối player TISInputBridge Instance Start cache _input GetSkillAction MoveDirection",        Snippet="_input = TISInputBridge.Instance; gọi trong Start(), cache vào field." },

            // ── Mobile UI (tab 3) ──────────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — TISMobileSkillButton",                               Keywords="skillName SetCooldownFill bindingMobile inject registry on-screen button",                     Snippet="On-screen button inject state vào TISMobileButtonRegistry." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Simulate Mobile",                                    Keywords="simulateMobileInEditor simulate mobile editor test keyboard",                                   Snippet="Bật Simulate Mobile In Editor để test UI mobile trong Editor." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Dynamic Joystick",                                   Keywords="Dynamic Joystick PUBG Fixed opacity radius knob",                                             Snippet="Dynamic Mode = true: joystick hiện tại điểm ngón chạm (PUBG style)." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Luồng Dữ Liệu",                                     Keywords="FullscreenTouchReceiver OverrideMoveInput OverrideLookInput flow",                              Snippet="Ngón tay → TISVirtualJoystick → OverrideMoveInput → TISInputBridge." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Luồng Dữ Liệu: Touch → TISInputBridge",             Keywords="luồng dữ liệu mobile FullscreenTouchReceiver OverrideMoveInput OverrideLookInput OverrideZoomInput registry flow", Snippet="Ngón tay → TISVirtualJoystick → OverrideMoveInput → TISInputBridge → script." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Setup TISMobileSkillButton: 4 Bước",                 Keywords="setup TISMobileSkillButton bước canvas UI image cooldown fill skill name binding",              Snippet="4 bước: action trong Profile → UI Button → Add Component → Cooldown Fill (tùy chọn)." },
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — Thứ Tự Ưu Tiên Visual: Interactable > Disabled > Pressed", Keywords="visual priority thứ tự ưu tiên Interactable disabled pressed normal SetDisabled SetCooldownFill SetVisualMode", Snippet="Thứ tự: !Interactable > Disabled > Pressed > Normal." },

            // ── Code — sub-tabs (tab 4) ────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=0,  Title="Code — Lấy Reference",                                       Keywords="Instance TISInputBridge cache _input Start namespace using singleton",                          Snippet="_input = TISInputBridge.Instance; — cache trong Start()." },
            new SearchEntry { TabIndex=4, CodeSubTab=1,  Title="Code — Di Chuyển (MoveDirection)",                           Keywords="MoveDirection WASD LeftStick Vector2 walkSpeed magnitude world space",                          Snippet="Vector2 MoveDirection thống nhất từ WASD / LeftStick / Joystick." },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code — Camera (LookDelta & ZoomDelta)",                      Keywords="LookDelta ZoomDelta chuột right stick pinch zoom camera delta sensitivity",                     Snippet="LookDelta: delta xoay camera. ZoomDelta: pinch zoom — reset mỗi frame." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code — Button Shortcuts",                                    Keywords="IsJumping IsRunning IsAttacking shortcut property bool IsSkillDown IsSkillHeld IsSkillReleased PressDown Hold PressUp", Snippet="Shortcut properties: IsJumping, IsRunning, IsAttacking + IsSkillDown/Held/Released." },
            new SearchEntry { TabIndex=4, CodeSubTab=4,  Title="Code — GetSkillAction & Shortcuts",                          Keywords="GetSkillAction IsSkillDown IsSkillHeld IsSkillReleased Attack Skill_1 actionName tên action PressDown Hold PressUp mode overload charge", Snippet="GetSkillAction 2 overload + IsSkillDown/Held/Released shortcut methods." },
            new SearchEntry { TabIndex=4, CodeSubTab=5,  Title="Code — Bật / Tắt Action Map",                                Keywords="SetActionMapActive DisableAllMaps EnableDefaultMaps cutscene vehicle death",                   Snippet="Bật/tắt Action Map: lên xe, cutscene, death..." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code — Profile Swap Runtime",                                Keywords="SwapProfile runtime load Resources OnProfileSwapped event rebuild",                             Snippet="Swap profile runtime: rebuild toàn bộ InputActionAsset, clear registry." },
            new SearchEntry { TabIndex=4, CodeSubTab=7,  Title="Code — Full Example Controller",                             Keywords="MyPlayerController CharacterController HandleMovement HandleSkills complete",                   Snippet="Script controller hoàn chỉnh: move, jump, run, attack, skill trên mọi platform." },

            // ── Code — H2-level entries ────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=0,  Title="Code/Ref — Namespace & Cache Instance: using TrueInputSystem", Keywords="namespace using TrueInputSystem cache _input field Start MonoBehaviour overhead",             Snippet="Cache _input = TISInputBridge.Instance trong Start() — tránh gọi .Instance mỗi frame." },
            new SearchEntry { TabIndex=4, CodeSubTab=0,  Title="Code/Ref — Đọc ActiveProfile.profileName",                   Keywords="ActiveProfile profileName log debug hiển thị tên profile",                                    Snippet="input.ActiveProfile.profileName — dùng để log hoặc hiển thị tên profile." },
            new SearchEntry { TabIndex=4, CodeSubTab=0,  Title="Code/Ref — Đọc Settings: TISCameraSettings, TISGamepadSettings, TISMobileSettings", Keywords="settings TISCameraSettings TISGamepadSettings TISMobileSettings mouseSensitivity enableRumble", Snippet="Truy cập nhanh settings qua TISInputBridge.Instance.TISCameraSettings / TISGamepadSettings." },
            new SearchEntry { TabIndex=4, CodeSubTab=1,  Title="Code/Move — Cơ Bản: Đọc MoveDirection & Di Chuyển",          Keywords="MoveDirection cơ bản di chuyển đơn giản Translate speed deltaTime",                          Snippet="Vector2 move = _input.MoveDirection; — đồng nhất trên PC, Gamepad, Mobile." },
            new SearchEntry { TabIndex=4, CodeSubTab=1,  Title="Code/Move — Kết Hợp Với Camera World Space",                 Keywords="world space camera TransformDirection dir normalize move pitch controller",                    Snippet="TransformDirection(move) rồi dir.y=0 để di chuyển theo hướng camera nhìn." },
            new SearchEntry { TabIndex=4, CodeSubTab=1,  Title="Code/Move — Tốc Độ Analog: Gamepad Stick Magnitude",         Keywords="analog tốc độ magnitude stick gamepad clamp walkSpeed runSpeed",                              Snippet="Mathf.Clamp01(move.magnitude) để đọc mức độ đẩy stick từ 0 đến 1." },
            new SearchEntry { TabIndex=4, CodeSubTab=1,  Title="Code/Move — Override MoveDirection: Joystick / AI / Replay", Keywords="OverrideMoveInput ClearMoveOverride virtual joystick AI replay inject override",              Snippet="OverrideMoveInput(v2) inject giá trị tùy ý. ClearMoveOverride() hoàn trả input thật." },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code/Cam — LookDelta: Xoay Camera Yaw & Pitch",              Keywords="LookDelta xoay camera yaw pitch sensitivity chuột right stick kéo ngón",                     Snippet="yaw += look.x * sensitivity; pitch -= look.y * sensitivity;" },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code/Cam — ZoomDelta: Pinch Zoom & Scroll Wheel",            Keywords="ZoomDelta pinch zoom scroll wheel consume reset mỗi frame khoảng cách",                       Snippet="ZoomDelta consume mỗi frame — chỉ đọc 1 lần trong Update()." },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code/Cam — Override LookDelta: Swipe Zone / Replay",         Keywords="OverrideLookInput ClearLookOverride swipe zone camera touch override delta",                  Snippet="OverrideLookInput(delta) cho camera swipe zone. ClearLookOverride() khi nhả." },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code/Cam — Override ZoomDelta: Pinch Gesture",               Keywords="OverrideZoomInput ClearZoomOverride pinch gesture recognizer zoom",                           Snippet="OverrideZoomInput(pinchDelta). ClearZoomOverride() khi kết thúc gesture." },
            new SearchEntry { TabIndex=4, CodeSubTab=2,  Title="Code/Cam — Bật Tắt Xoay & Zoom: SetRotationEnabled / SetZoomEnabled", Keywords="SetRotationEnabled SetZoomEnabled ToggleRotation ToggleZoom cutscene ADS aim down sight lock", Snippet="cam.SetRotationEnabled(false) khi cutscene. cam.SetZoomEnabled(false) khi ADS." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code/Shortcut — IsJumping / IsRunning / IsAttacking: 3 Properties", Keywords="IsJumping IsRunning IsAttacking shortcut property bool direct không cần tên action",      Snippet="Đọc trực tiếp: _input.IsJumping, IsRunning, IsAttacking — không cần tên action." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code/Shortcut — Dùng Trong Update: Jump Run Attack",          Keywords="Update IsJumping IsRunning IsAttacking DoJump SetSprint Attack",                              Snippet="if (_input.IsJumping) DoJump(); nằm trong Update()." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code/Shortcut — ReadMode: PressDown / Hold / PressUp",       Keywords="ReadMode PressDown Hold PressUp trigger 1 frame nhấn giữ nhả",                              Snippet="PressDown=1 frame nhấn, Hold=suốt khi giữ, PressUp=1 frame nhả." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code/Shortcut — IsSkillDown / IsSkillHeld / IsSkillReleased", Keywords="IsSkillDown IsSkillHeld IsSkillReleased Dodge Shield Aim shortcut tường minh mode",        Snippet="IsSkillDown(name)=PressDown, IsSkillHeld=Hold, IsSkillReleased=PressUp — bất kể profile." },
            new SearchEntry { TabIndex=4, CodeSubTab=3,  Title="Code/Shortcut — Charge Mechanic: 3 Mode Trên 1 Action",      Keywords="charge mechanic BeginCharge ContinueCharge ReleaseCharge IsSkillDown Held Released",         Snippet="Dùng IsSkillDown + IsSkillHeld + IsSkillReleased cùng 1 action cho charge." },
            new SearchEntry { TabIndex=4, CodeSubTab=4,  Title="Code/Skill — GetSkillAction Cơ Bản",                         Keywords="GetSkillAction cơ bản button action attack skill false không trigger",                        Snippet="GetSkillAction(\"Attack\") đọc theo readMode trong profile." },
            new SearchEntry { TabIndex=4, CodeSubTab=5,  Title="Code/Vector2 — GetVector2Action: Navigate, MapScroll",       Keywords="GetVector2Action Navigate Vector2 UI menu RTS map scroll pan action type",                  Snippet="GetVector2Action(\"Navigate\") dùng cho menu nav, map pan, RTS movement." },
            new SearchEntry { TabIndex=4, CodeSubTab=5,  Title="Code/Vector2 — GetAxisAction: Trigger, Throttle, Zoom",      Keywords="GetAxisAction float trigger analog throttle brake zoom aim FOV axis type",                   Snippet="GetAxisAction(\"Accelerate\") trả về float 0→1 từ trigger có apply deadzone." },
            new SearchEntry { TabIndex=4, CodeSubTab=5,  Title="Code/Vector2 — Ví Dụ Điều Khiển Xe Đầy Đủ",                 Keywords="vehicle điều khiển xe gas brake horn drift GetAxisAction GetSkillAction IsSkillHeld",        Snippet="Gas/Brake qua GetAxisAction, Horn qua GetSkillAction, Drift qua IsSkillHeld." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code/Mobile — SkillName & IsPressed",                        Keywords="SkillName IsPressed TISMobileSkillButton tooltip get-only ngón đang giữ",                   Snippet="btn.SkillName (get only), btn.IsPressed để kiểm tra ngón đang giữ." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code/Mobile — Interactable vs SetDisabled: Ưu Tiên",         Keywords="Interactable SetDisabled ưu tiên khóa tạm thời chết stun stage lock coroutine",             Snippet="Interactable=false khóa hoàn toàn. SetDisabled(true) tắt tạm thời (stun, cooldown)." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code/Mobile — SetCooldownFill: Thanh Cooldown 0→1",          Keywords="SetCooldownFill cooldown fill 0 1 ready coroutine IEnumerator Image Filled",                Snippet="SetCooldownFill(0→1): 0=full cooldown (disable), 1=sẵn sàng. Yêu cầu Image Type=Filled." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code/Mobile — SetVisualMode: ColorTint / SpriteSwap",        Keywords="SetVisualMode ColorTint SpriteSwap runtime đổi skin theme AB test visual mode",             Snippet="btn.SetVisualMode(VisualMode.SpriteSwap) — tự gọi UpdateVisual() ngay." },
            new SearchEntry { TabIndex=4, CodeSubTab=6,  Title="Code/Mobile — OnPress & OnRelease: UnityEvent",              Keywords="OnPress OnRelease UnityEvent AddListener hold time charge sfx feedback Inspector",          Snippet="btn.OnPress.AddListener(() => ...) — fired 1 lần khi ngón chạm." },
            new SearchEntry { TabIndex=4, CodeSubTab=7,  Title="Code/ActionMap — SetActionMapActive: Bật Tắt Nhóm",          Keywords="SetActionMapActive bật tắt action map Combat UI Vehicle cutscene death inventory",          Snippet="SetActionMapActive(\"Combat\", false) — bật/tắt nhóm action trong profile hiện tại." },
            new SearchEntry { TabIndex=4, CodeSubTab=7,  Title="Code/ActionMap — DisableAllMaps & EnableDefaultMaps",        Keywords="DisableAllMaps EnableDefaultMaps tắt hết bật lại cutscene death loading respawn",           Snippet="DisableAllMaps() tắt hết. EnableDefaultMaps() bật theo enabledByDefault." },
            new SearchEntry { TabIndex=4, CodeSubTab=7,  Title="Code/ActionMap — enabledByDefault: Map Tắt Mặc Định",        Keywords="enabledByDefault false vehicle map bật tắt mặc định SetActionMapActive",                    Snippet="enabledByDefault=false → map tắt khi load. Bật bằng SetActionMapActive()." },

            // ── Code — Pointer Position (sub-tab 20) ──────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — IsPointerHeld Guard Trước Khi Drag",           Keywords="IsPointerHeld guard bool drag pointer touch giữ kiểm tra trước Update return early",         Snippet="if (!_input.IsPointerHeld) return; — guard bắt buộc trước khi đọc Pointer API." },
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — API 1: GetPointerScreenPosition (UI Drag)",    Keywords="GetPointerScreenPosition screen space pixel UI drag RectTransform canvas ScreenPointToLocalPointInRectangle localPosition", Snippet="Chuyển screen position sang Canvas local space để kéo RectTransform. Cần Enable Object Tracker." },
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — GetPointerScreenDelta (Kéo Bằng Delta)",       Keywords="GetPointerScreenDelta delta pixel frame anchoredPosition scroll rect offset drag không cần lưu vị trí cũ", Snippet="_rt.anchoredPosition += delta; — kéo RectTransform hoặc scroll panel mà không cache vị trí." },
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — API 2: GetPointerWorldPosition 3D Drag",       Keywords="GetPointerWorldPosition world space depth float 3D drag DragObject3D OnMouseDown Vector3.Distance", Snippet="Ghi lại depth khi OnMouseDown, dùng GetPointerWorldPosition(depth) trong Update để kéo object 3D." },
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — GetPointerWorldPosition 2D Orthographic",      Keywords="GetPointerWorldPosition 2D ortho orthographic depth Mathf.Abs camera z giữ z object",       Snippet="2D Ortho: depth = Mathf.Abs(Camera.main.transform.position.z). Giữ worldPos.z = transform.position.z." },
            new SearchEntry { TabIndex=4, CodeSubTab=20, Title="Code/Pointer — Kết Hợp ScreenPosition + WorldPosition",       Keywords="kết hợp screen world position drag drop slot UI icon HighlightNearestSlot canvas localPosition", Snippet="ScreenPosition di chuyển icon UI, WorldPosition xác định slot đặt object — drag & drop world slot." },
            new SearchEntry { TabIndex=4, CodeSubTab=7,  Title="Code/ActionMap — Debug: LogActiveProfile()",                 Keywords="LogActiveProfile debug console in profile toàn bộ cấu hình",                               Snippet="_input.LogActiveProfile() in toàn bộ cấu hình profile ra Console." },
            new SearchEntry { TabIndex=4, CodeSubTab=8,  Title="Code/Profile — SwapProfile: Đổi Toàn Bộ Cấu Hình",          Keywords="SwapProfile rebuild InputActionAsset ClearAll registry vehicle character profile swap runtime", Snippet="SwapProfile() rebuild toàn bộ — dùng khi game mode thay đổi hoàn toàn." },
            new SearchEntry { TabIndex=4, CodeSubTab=8,  Title="Code/Profile — SwapProfile vs SetActionMapActive",           Keywords="SwapProfile SetActionMapActive khác nhau khi nào dùng vehicle adventure mode",               Snippet="SwapProfile = đổi hoàn toàn. SetActionMapActive = chỉ bật/tắt nhóm trong profile cũ." },
            new SearchEntry { TabIndex=4, CodeSubTab=8,  Title="Code/Profile — OnProfileSwapped: Event Sau Khi Swap",        Keywords="OnProfileSwapped event listener RefreshControlHints newProfile += -= unsubscribe",          Snippet="OnProfileSwapped += handler — fire sau SwapProfile(), truyền profile mới." },
            new SearchEntry { TabIndex=4, CodeSubTab=10, Title="Code/Rebinding — StartRebinding Interactive",                Keywords="StartRebinding rebind interactive press any key overlay onComplete callback PC gamepad",     Snippet="TISRebindingSystem.StartRebinding(bridge, actionName, target, result => {})." },
            new SearchEntry { TabIndex=4, CodeSubTab=10, Title="Code/Rebinding — ForceApply & ApplyPreset",                  Keywords="ForceApply ApplyPreset preset dictionary batch programmatic binding code",                  Snippet="ForceApply(bridge, name, target, path) — apply trực tiếp không cần nhấn phím." },
            new SearchEntry { TabIndex=4, CodeSubTab=10, Title="Code/Rebinding — GetActivePath & GetDisplayName",            Keywords="GetActivePath GetDisplayName path display label UI settings rebinding",                    Snippet="GetDisplayName(GetActivePath(...)) để hiện tên phím lên UI." },
            new SearchEntry { TabIndex=4, CodeSubTab=10, Title="Code/Rebinding — LoadAndApply & Reset",                     Keywords="LoadAndApply JSON persistence reset ResetAllBindings ResetBinding Awake",                  Snippet="LoadAndApply(bridge) gọi trong Awake sau khi bridge khởi tạo xong." },
            new SearchEntry { TabIndex=4, CodeSubTab=11, Title="Code/DirectionalBtn — WasCastThisFrame & CastDirection",     Keywords="WasCastThisFrame CastDirection DirectionAngle directional skill button cast",               Snippet="btn.WasCastThisFrame → true 1 frame khi cast. btn.CastDirection = hướng." },
            new SearchEntry { TabIndex=4, CodeSubTab=11, Title="Code/DirectionalBtn — OnDirectionCast Event",               Keywords="OnDirectionCast event action skillName direction cast directional button",                 Snippet="btn.OnDirectionCast += (skill, dir) => {}  — fire khi cast thành công." },
            new SearchEntry { TabIndex=4, CodeSubTab=11, Title="Code/DirectionalBtn — TISDirectionalRegistry",              Keywords="TISDirectionalRegistry GetDirection SetVector2Override registry direction",                Snippet="TISDirectionalRegistry.GetDirection(\"Skill1_Dir\") — lấy direction sau cast." },
            new SearchEntry { TabIndex=4, CodeSubTab=12, Title="Code/TargetScanner — CurrentTarget & HasTarget",            Keywords="TISTargetScanner CurrentTarget HasTarget CurrentAimPoint CurrentAngle scan",                Snippet="scanner.HasTarget → CurrentAimPoint dùng cho AimAssistProcess()." },
            new SearchEntry { TabIndex=4, CodeSubTab=12, Title="Code/TargetScanner — ForceScan & ClearTarget",              Keywords="ForceScan ClearTarget enemy died wave spawn scanner",                                     Snippet="ForceScan() quét ngay. ClearTarget() khi target bị destroy." },
            new SearchEntry { TabIndex=4, CodeSubTab=13, Title="Code/MobileSetup — BatTat & Dat Methods",                   Keywords="BatTatXoayCamera DatZoom DatJoystick DatTyLeVung TISMobileInputSetup toggle",              Snippet="DatTyLeVung(0.4f) phân chia 40% joystick / 60% camera zone." },
            new SearchEntry { TabIndex=4, CodeSubTab=13, Title="Code/MobileSetup — SetupCompleted Event & IsReady",         Keywords="SetupCompleted IsReady event TISMobileInputSetup mobile UI ready",                        Snippet="setup.SetupCompleted += OnMobileReady — subscribe trước khi setup hoàn tất." },
            new SearchEntry { TabIndex=4, CodeSubTab=14, Title="Code/AimAssist — AimAssistProcess + AimAssistSettings",     Keywords="AimAssistProcess AimAssistSettings snapRadius assistSpeed intensity camera",               Snippet="bridge.AimAssistProcess(settings, cam.transform, aimPoint, look, 0.45f)." },
            new SearchEntry { TabIndex=4, CodeSubTab=14, Title="Code/Deadzone — ApplyDeadzone Scaled Radial",               Keywords="ApplyDeadzone deadzoneRadius Vector2 analog stick scaled radial",                          Snippet="bridge.ApplyDeadzone(rawInput, 0.2f) — Scaled Radial Deadzone." },
            new SearchEntry { TabIndex=4, CodeSubTab=15, Title="Code/GamepadCursor — SetActiveGamepadCursor",               Keywords="SetActiveGamepadCursor RegisterGamepadCursor TISGamepadVirtualCursor menu UI",             Snippet="bridge.SetActiveGamepadCursor(true) khi mở menu. false khi đóng." },

            // ── Code — Rumble (sub-tab 9) ──────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=9,  Title="Code — Gamepad Rumble: TryRumble & StopRumble",              Keywords="TryRumble StopRumble rumble haptic gamepad lowFrequency highFrequency duration rung tay cầm", Snippet="TryRumble(low, high, duration) — rung có hẹn giờ. StopRumble() dừng ngay lập tức." },
            new SearchEntry { TabIndex=4, CodeSubTab=9,  Title="Code/Rumble — TryRumble Liên Tục: Drift, Danger Zone",      Keywords="TryRumble liên tục drift danger zone IsSkillHeld Update StopRumble vòng lặp rung",          Snippet="Gọi TryRumble(low, high) mỗi frame trong Update, StopRumble() khi điều kiện tắt." },
            new SearchEntry { TabIndex=4, CodeSubTab=9,  Title="Code/Rumble — Điều Kiện Hoạt Động: enableRumble",           Keywords="enableRumble TISGamepadSettings Gamepad.current điều kiện hoạt động rumble profile",         Snippet="3 điều kiện: enableRumble=true + Gamepad.current!=null + gọi qua TISInputBridge." },

            // ── Code — Full Example (sub-tab 16) ───────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=16, Title="Code — Full Example Controller Hoàn Chỉnh",                 Keywords="full example MyPlayerController CharacterController move jump run attack skill camera rumble hoàn chỉnh complete", Snippet="Script tổng hợp: move, jump, run, skills, aim assist, rumble — PC, Gamepad, Mobile." },

            // ── Code — GestureDetector (sub-tab 17) ────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=17, Title="Code — GestureDetector: Nhận Diện Gesture Cảm Ứng",         Keywords="GestureDetector gesture swipe pinch tap double-tap GesturePanDelta cảm ứng ngón tay detect", Snippet="GestureDetector nhận diện swipe, pinch, tap, double-tap trên màn hình cảm ứng." },
            new SearchEntry { TabIndex=4, CodeSubTab=17, Title="Code/Gesture — GesturePanDelta & ToucherCount",             Keywords="GesturePanDelta ToucherCount gesture pan swipe delta số ngón chạm màn hình touch finger",   Snippet="GesturePanDelta: Vector2 delta pan/swipe. ToucherCount: số ngón đang chạm." },

            // ── Code — ObjectTracker (sub-tab 18) ──────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=18, Title="Code — ObjectTracker: Theo Dõi Vật Thể Bằng Touch",         Keywords="ObjectTracker SetObjTracker SetTrackerCamera GetSingleObject DoubleTapOnObject touch track object", Snippet="ObjectTracker: tap/double-tap chọn object. SetObjTracker gán target, SetTrackerCamera gán camera raycast." },
            new SearchEntry { TabIndex=4, CodeSubTab=18, Title="Code/Tracker — DoubleTapOnObject & DoubleTapOnScreen",       Keywords="DoubleTapOnObject DoubleTapOnScreen GetDoubleTapped ToucherClear detect double tap chạm đúp", Snippet="DoubleTapOnObject(obj): detect double-tap trúng đúng GameObject. DoubleTapOnScreen: bất kỳ vị trí." },

            // ── Code — GamepadUIButton (sub-tab 19) ────────────────────────────────────────────────
            new SearchEntry { TabIndex=4, CodeSubTab=19, Title="Code — TISGamepadUIButton: Setup Khởi Tạo Layer Active",    Keywords="TISGamepadUIButton SetAsActiveInLayer UINavigationContext start layer tab subtab content gamepad UI khởi tạo", Snippet="SetAsActiveInLayer() đặt button là active trong layer. Gọi trong Start() cho Tab, SubTab, Content." },
            new SearchEntry { TabIndex=4, CodeSubTab=19, Title="Code/GamepadUI — onBecameActiveInLayer: Redirect Focus",     Keywords="onBecameActiveInLayer AddListener redirect focus EventSystem SetSelectedGameObject content tab đổi callback", Snippet="tabBtn.onBecameActiveInLayer.AddListener(OnTabActive) → redirect EventSystem focus về content đầu." },
            new SearchEntry { TabIndex=4, CodeSubTab=19, Title="Code/GamepadUI — DisableNavigation & EnableNavigation",     Keywords="DisableNavigation EnableNavigation panel overlay popup khóa mở navigation button lock frozen", Snippet="DisableNavigation() khóa button khi popup đè lên. EnableNavigation() mở lại sau khi đóng." },
            new SearchEntry { TabIndex=4, CodeSubTab=19, Title="Code/GamepadUI — GetActiveInLayer: Query Từ Script",        Keywords="GetActiveInLayer NavigationLayer Tab SubTab static query script tìm button đang active layer", Snippet="TISGamepadUIButton.GetActiveInLayer(NavigationLayer.Tab) lấy Tab đang active từ bất kỳ script." },
            new SearchEntry { TabIndex=4, CodeSubTab=19, Title="Code/GamepadUI — NavMode & InputSource Cấu Hình Điển Hình", Keywords="NavMode Horizontal FourDirectional inputSource LT_RT LB_RB Default Tab SubTab Content alwaysActive isFirstSelected", Snippet="Tab: Horizontal+LT_RT+alwaysActive. SubTab: LB_RB. Content: FourDirectional+isFirstSelected." },

            // ── Mobile UI (tab 3) — DirectionalButton Events & ForceCast ──────────────────────────
            new SearchEntry { TabIndex=3, CodeSubTab=-1, Title="Mobile — TISDirectionalSkillButton: Events & ForceCast",    Keywords="OnCancelled OnDragStarted ForceCast TISDirectionalSkillButton event cancel drag force cast tutorial AI", Snippet="OnCancelled, OnDragStarted UnityEvent. ForceCast(dir) cast bằng code cho tutorial/automation." },

            // ── Gamepad UI Setup (tab 7) ────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — Kiến Trúc 3 Lớp: Tab/SubTab/Content",          Keywords="TISGamepadUIButton kiến trúc 3 lớp Tab SubTab Content LT RT LB RB DPad gamepad UI navigation", Snippet="LT/RT=Tab, LB/RB=SubTab, DPad/Stick=Content. Mỗi lớp dùng một TISGamepadUIButton riêng." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — TISGamepadUIButton: Inspector Fields",          Keywords="TISGamepadUIButton layer navigationMode inputSource alwaysActive isFirstSelected holdDelay navUp navDown onSelect onDeselect Inspector", Snippet="layer + inputSource + navigationMode + alwaysActive — 4 fields cốt lõi cần set." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — TISGamepadUIButtonGroup: Auto Navigation",     Keywords="TISGamepadUIButtonGroup RebuildNavigation VerticalList HorizontalRow Grid gridColumns wrap cha parent tự tính", Snippet="Gắn lên parent → RebuildNavigation() tự tính navUp/Down/Left/Right cho tất cả button con." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — TISUINavigationContext: Redirect Focus",       Keywords="TISUINavigationContext singleton TabEntry SubTabEntry firstContent defaultTab defaultSubTab defaultContent redirect focus", Snippet="Singleton 1 trong scene. Khai báo TabEntry/SubTabEntry → tự redirect focus khi Tab/SubTab đổi." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — Luồng Xử Lý Khi Tab / SubTab Đổi",            Keywords="luồng Tab đổi RT LT onBecameActiveInLayer SetAsActiveInLayer MoveContentFocus firstContent UINavigationContext", Snippet="RT nhấn → Tab SetAsActiveInLayer → UINavigationContext redirect firstSubTab + firstContent." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — Khóa Navigation: DisableNavigation & Popup",   Keywords="DisableNavigation EnableNavigation popup overlay dialog khóa mở navigation panel lock input leak gamepad", Snippet="DisableNavigation() khi popup mở. EnableNavigation() khi đóng. Tránh input leak qua popup." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — TISGamepadVirtualCursor: Setup & Bật Tắt",     Keywords="TISGamepadVirtualCursor left stick cursor ảo con trỏ cursorTransform parentCanvas cursorSpeed inject InputSystem", Snippet="Left stick di chuyển cursor, buttonSouth click. Inject InputSystem như chuột thật. Tùy chọn." },
            new SearchEntry { TabIndex=7, CodeSubTab=-1, Title="Gamepad UI — Subscribe Thủ Công Không Dùng Context",        Keywords="subscribe thủ công không dùng UINavigationContext 1 layer Tab onBecameActiveInLayer SetSelectedGameObject code", Snippet="Nếu chỉ 1 layer Tab: tự subscribe onBecameActiveInLayer thay vì dùng UINavigationContext." },

            // ── API Reference (tab 5) ──────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISInputBridge Properties",                           Keywords="MoveDirection LookDelta ZoomDelta IsJumping IsRunning IsAttacking ActiveProfile AimAssistTarget Toucher Gesture", Snippet="Toàn bộ properties: movement, look, button shortcuts, profile, gesture, tracker." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISInputBridge Methods",                              Keywords="GetSkillAction SwapProfile TryRumble OverrideMoveInput SetActionMapActive AimAssistProcess ApplyDeadzone RegisterGamepadCursor", Snippet="Toàn bộ methods của TISInputBridge — skill, rumble, override, map, aimassist." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — Gyro & Touchpad (Bật / Tắt)",                        Keywords="GyroEnabled TouchpadEnabled SetProfileMotion SetActionMotion SetActionGyroEnabled SetActionTouchpadEnabled IsActionGyroEnabled ActionHasGyro ResetMotionSettings OnMotionSettingsChanged save", Snippet="Bật / tắt gyro + touchpad theo toàn game, profile, action — có lưu lựa chọn người chơi." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — <TISMotion> Binding Paths",                          Keywords="TISMotion gyro touchpad touchpadPosition touchpadTouch touchpadPress binding path + Gyro + Touchpad composite TISStickMotion", Snippet="Bind gyro / touchpad như nút tay cầm qua thiết bị ảo <TISMotion>." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISMotionSystem / ITISMotionSource",                  Keywords="Motion GyroRate GyroAim TouchpadVelocity TouchpadPressed BeginGyroCalibration EndGyroCalibration GyroProcessor RegisterMotionSource ITISMotionSource provider SDL3 console", Snippet="Đọc gyro / touchpad trực tiếp, calibration, viết provider riêng." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISGamepadVirtualCursor Events",                      Keywords="IsCursorActive OnExitByNavigationInput OnExitByMouse Exit On Dpad Exit On Mouse virtual cursor", Snippet="Cursor tự thoát bằng D-pad / LB / RB / LT / RT hoặc chuột thật." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISPhysicalMouse / TISTouchCameraZone",               Keywords="TISPhysicalMouse Current chuột thật Mouse.current TISTouchCameraZone AddPointer RemovePointer PointerCount ownership", Snippet="Chuột thật (bỏ qua chuột ảo) và pointer ownership của camera zone mobile." },
            new SearchEntry { TabIndex=4, CodeSubTab=21, Title="Code/Gyro & Touchpad — Bind, Đọc, Calibration",             Keywords="gyro touchpad TISMotion + Gyro LookDelta GetVector2Action calibration BeginGyroCalibration ShowMotionDebugGUI provider DualSense DualShock", Snippet="Look = right stick + gyro + touchpad; action gyro / touchpad riêng; calibration." },
            new SearchEntry { TabIndex=4, CodeSubTab=23, Title="Code/Scene hướng dẫn — Tutorials",                         Keywords="tutorial hướng dẫn scene học demo bài học TISTutLesson chuột bàn phím tay cầm mobile mở scene", Snippet="3 scene học bằng cách chơi + cách thêm bài học mới." },
            new SearchEntry { TabIndex=4, CodeSubTab=22, Title="Code/Gyro Bật/Tắt — Theo Game, Profile, Action",           Keywords="tắt gyro bật gyro SetActionGyroEnabled SetProfileMotion save settings menu Toggle ResetMotionSettings", Snippet="3 mức bật / tắt, lưu lựa chọn người chơi, ví dụ menu Settings." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISRebindingSystem",                                  Keywords="StartRebinding CancelRebinding ForceApply ApplyPreset GetActivePath LoadAndApply ResetAllBindings rebind JSON", Snippet="TISRebindingSystem: interactive + programmatic rebinding, JSON persistence." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISMobileSkillButton",                               Keywords="SkillName HoldTime Interactable SetDisabled SetCooldownFill SetVisualMode OnPress OnRelease OnHold OnHoldTimed", Snippet="Mobile skill button: properties, methods, events." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISDirectionalSkillButton & Registry",               Keywords="WasCastThisFrame CastDirection DirectionAngle IsDragging OnDirectionCast ForceCast TISDirectionalRegistry", Snippet="Directional skill button + TISDirectionalRegistry static API." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISTargetScanner",                                   Keywords="CurrentTarget HasTarget CurrentAimPoint CurrentAngle ForceScan ClearTarget AimPointMode scan", Snippet="TISTargetScanner: auto scan target + kết hợp AimAssistProcess." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISMobileInputSetup",                               Keywords="IsReady SetupCompleted BatTatXoayCamera DatZoom DatJoystick DatTyLeVung DongBoTyLe mobile setup", Snippet="TISMobileInputSetup: toggle/set mobile features + SetupCompleted event." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — TISGamepadVirtualCursor",                            Keywords="SetActiveCursor RegisterGamepadCursor cursorTransform parentCanvas cursorSpeed virtual mouse", Snippet="TISGamepadVirtualCursor: gamepad virtual cursor điều khiển bằng left stick." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — Pointer Position: GetPointerScreenPosition",         Keywords="GetPointerScreenPosition screen space pixel pointer touch drag UI RectTransform canvas api 1", Snippet="[API 1] Vị trí screen space (pixel) của pointer/touch đang giữ. Dùng để di chuyển RectTransform trong Canvas." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — Pointer Position: GetPointerScreenDelta",            Keywords="GetPointerScreenDelta delta screen pixel frame trước drag offset scroll rect bonus utility",  Snippet="Delta screen (pixel) so frame trước. Dùng để tính offset kéo không cần lưu vị trí cũ." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — Pointer Position: IsPointerHeld",                   Keywords="IsPointerHeld bool guard drag pointer touch đang giữ kiểm tra trước khi drag",              Snippet="bool guard — true khi pointer/touch đang được giữ. Kiểm tra trước khi đọc các Pointer API khác." },
            new SearchEntry { TabIndex=5, CodeSubTab=-1, Title="API — Pointer Position: GetPointerWorldPosition",         Keywords="GetPointerWorldPosition world space depth float 3D drag object api 2 ortho orthographic",   Snippet="[API 2] Vị trí World Space tương ứng pointer đang giữ. depth = khoảng cách camera → mặt phẳng đặt object." },

            // ── Setup Tool (tab 6) ─────────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=6, CodeSubTab=-1, Title="Setup Tool — Toolbar",                                     Keywords="Validate Generate Gán Scene Profile Name toolbar nút button",                              Snippet="Toolbar: Mới, Mở .asset, Validate, Generate, Gán Scene." },
            new SearchEntry { TabIndex=6, CodeSubTab=-1, Title="Setup Tool — Bố cục",                                     Keywords="Panel Trái Giữa Phải Binding Editor Action Map Status Bar layout",                        Snippet="3-panel layout: Action Maps / Binding Editor / Platform Settings." },

            // ── Favorites (tab 0) ─────────────────────────────────────────────────────────────────
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — MoveDirection",                                Keywords="MoveDirection WASD joystick leftStick Vector2 di chuyển favorite",                         Snippet="Vector2 MoveDirection: WASD / LeftStick / VirtualJoystick thống nhất." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — LookDelta",                                   Keywords="LookDelta camera xoay chuột right stick kéo ngón delta sensitivity",                      Snippet="LookDelta: delta xoay camera từ chuột / gamepad right stick / kéo ngón." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — ZoomDelta",                                   Keywords="ZoomDelta pinch zoom scroll wheel float consume reset frame",                              Snippet="ZoomDelta: float từ scroll wheel / pinch. Consume mỗi frame." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — IsJumping",                                   Keywords="IsJumping bool shortcut property jump nhảy favorite",                                    Snippet="bool IsJumping: shortcut property — đọc trực tiếp không cần tên action." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — IsRunning",                                   Keywords="IsRunning bool shortcut property run chạy sprint favorite",                              Snippet="bool IsRunning: shortcut property — check shift/leftShoulder/sprint." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — IsAttacking",                                 Keywords="IsAttacking bool shortcut property attack tấn công favorite",                            Snippet="bool IsAttacking: shortcut property — đọc trực tiếp." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetSkillAction",                              Keywords="GetSkillAction bool string name readMode PressDown Hold PressUp favorite",                Snippet="GetSkillAction(name) + overload readMode — đọc button action theo tên." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetVector2Action",                            Keywords="GetVector2Action Vector2 Navigate map scroll RTS favorite",                              Snippet="GetVector2Action(name): Vector2 từ action type Vector2." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetAxisAction",                               Keywords="GetAxisAction float trigger throttle zoom axis favorite",                                 Snippet="GetAxisAction(name): float 0→1 từ trigger / analog axis." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — TryRumble",                                   Keywords="TryRumble StopRumble haptic gamepad rumble lowFreq highFreq duration favorite",           Snippet="TryRumble(low, high, duration) — kích haptic gamepad. StopRumble() dừng." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GesturePanDelta",                             Keywords="GesturePanDelta gesture pan swipe delta Vector2 favorite",                               Snippet="GesturePanDelta: delta gesture pan / swipe từ touch." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — ToucherCount",                                Keywords="ToucherCount số ngón touch finger count favorite",                                       Snippet="ToucherCount: số ngón đang chạm màn hình." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — DoubleTapOnObject",                           Keywords="DoubleTapOnObject double tap object raycast chạm đúp favorite",                          Snippet="DoubleTapOnObject: detect double tap trúng đúng GameObject." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — DoubleTapOnScreen",                           Keywords="DoubleTapOnScreen double tap màn hình anywhere favorite",                                Snippet="DoubleTapOnScreen: detect double tap bất kỳ vị trí nào trên màn hình." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetDoubleTapped",                             Keywords="GetDoubleTapped double tapped object result favorite",                                   Snippet="GetDoubleTapped: lấy object vừa được double tap." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — ToucherClear",                                Keywords="ToucherClear xóa clear touch state reset favorite",                                     Snippet="ToucherClear: xóa trạng thái touch hiện tại." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — SetObjTracker",                               Keywords="SetObjTracker object tracker theo dõi vật thể assign favorite",                          Snippet="SetObjTracker: gán object cần track bằng touch." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — SetTrackerCamera",                            Keywords="SetTrackerCamera camera tracker raycast favorite",                                       Snippet="SetTrackerCamera: gán camera dùng để raycast object tracker." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetSingleObject",                             Keywords="GetSingleObject touch single object raycast result favorite",                            Snippet="GetSingleObject: lấy object đơn đang được tap / touch." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — SetCooldownFill",                             Keywords="SetCooldownFill cooldown fill 0 1 Image Filled mobile button favorite",                  Snippet="SetCooldownFill(0→1): 0=full cooldown (disable), 1=sẵn sàng." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — SetDisabled",                                 Keywords="SetDisabled disabled tạm thời stun cooldown mana favorite",                             Snippet="SetDisabled(bool): tắt button tạm thời. Thấp hơn Interactable=false." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — IsPointerHeld",                              Keywords="IsPointerHeld bool guard drag pointer touch giữ favorite",                              Snippet="bool IsPointerHeld: guard — true khi đang giữ pointer/touch. Đọc trước khi drag." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetPointerScreenPosition",                   Keywords="GetPointerScreenPosition screen space pixel UI drag canvas RectTransform api 1 favorite",  Snippet="GetPointerScreenPosition(): screen position (pixel) của pointer. API 1 — UI Drag." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetPointerScreenDelta",                      Keywords="GetPointerScreenDelta delta pixel frame offset drag scroll bonus utility favorite",         Snippet="GetPointerScreenDelta(): delta so frame trước. Kéo RectTransform không cần cache vị trí." },
            new SearchEntry { TabIndex=0, CodeSubTab=-1, Title="Favorites — GetPointerWorldPosition",                    Keywords="GetPointerWorldPosition world space depth 3D drag ortho api 2 favorite",                  Snippet="GetPointerWorldPosition(depth): world position của pointer. API 2 — World Drag." },
        };

        // ══════════════════════════════════════════════════════
        //  SIDEBAR SEARCH BAR
        // ══════════════════════════════════════════════════════

        private void DrawSidebarSearch()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);

            var searchBg = SearchBg;
            var borderC  = _showSearchResults ? BorderFocus : BorderSub;

            Rect searchAreaRect = GUILayoutUtility.GetRect(SidebarW - 20, 30);
            _searchBoxBottomY = searchAreaRect.yMax + 4f;

            EditorGUI.DrawRect(searchAreaRect, searchBg);
            EditorGUI.DrawRect(new Rect(searchAreaRect.x, searchAreaRect.y,           searchAreaRect.width, 1), borderC);
            EditorGUI.DrawRect(new Rect(searchAreaRect.x, searchAreaRect.yMax - 1,    searchAreaRect.width, 1), borderC);
            EditorGUI.DrawRect(new Rect(searchAreaRect.x, searchAreaRect.y,           1, searchAreaRect.height), borderC);
            EditorGUI.DrawRect(new Rect(searchAreaRect.xMax - 1, searchAreaRect.y,    1, searchAreaRect.height), borderC);

            // Icon
            var iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 13,
                normal    = { textColor = TxtMuted },
                alignment = TextAnchor.MiddleLeft,
                padding   = new RectOffset(8, 0, 0, 0),
            };
            GUI.Label(new Rect(searchAreaRect.x, searchAreaRect.y, 26, 30), "⌕", iconStyle);

            // Text field
            Rect fieldRect = new Rect(searchAreaRect.x + 24, searchAreaRect.y + 1, searchAreaRect.width - 30, 28);
            var fieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12,
                normal   = { textColor = TxtPrimary, background = Tex(searchBg) },
                focused  = { textColor = TxtPrimary, background = Tex(searchBg) },
                padding  = new RectOffset(2, 4, 6, 6),
                border   = new RectOffset(0, 0, 0, 0),
            };
            GUI.SetNextControlName("SearchField");
            var prev     = _searchQuery;
            _searchQuery = GUI.TextField(fieldRect, _searchQuery, fieldStyle);
            _searchFocused = GUI.GetNameOfFocusedControl() == "SearchField";

            if (_searchQuery != prev)
                _showSearchResults = !string.IsNullOrEmpty(_searchQuery);

            // Placeholder
            if (string.IsNullOrEmpty(_searchQuery))
            {
                var phStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    normal   = { textColor = TxtMuted },
                    padding  = new RectOffset(0, 0, 6, 6),
                };
                GUI.Label(new Rect(fieldRect.x + 2, fieldRect.y, fieldRect.width, fieldRect.height), "Tìm kiếm...", phStyle);
            }

            // ESC để đóng
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _searchQuery      = "";
                _showSearchResults = false;
                GUI.FocusControl(null);
                Event.current.Use();
            }

            GUILayout.Space(10);
            GUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════
        //  SEARCH DROPDOWN MODAL
        // ══════════════════════════════════════════════════════

        private void DrawSearchDropdown()
        {
            var results = SearchDB
                .Where(e =>
                {
                    var q = _searchQuery.ToLowerInvariant();
                    return e.Title.ToLowerInvariant().Contains(q)
                        || e.Keywords.ToLowerInvariant().Contains(q)
                        || e.Snippet.ToLowerInvariant().Contains(q);
                })
                .ToArray();

            // Backdrop dim
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ModalBackdrop);

            // Panel geometry
            const float rowH = 60f;
            float dropW = Mathf.Min(position.width - SidebarW - 64f, 620f);
            float maxH  = Mathf.Min(position.height - 120f, 420f);
            float dropH = results.Length == 0 ? 60f : Mathf.Min(results.Length * rowH + 12f, maxH);

            float centerX = SidebarW + (position.width - SidebarW) * 0.5f;
            float dropX   = centerX - dropW * 0.5f;
            float dropY   = position.height * 0.18f;

            Rect panelRect = new Rect(dropX, dropY, dropW, dropH);

            // Shadow + panel bg
            EditorGUI.DrawRect(new Rect(panelRect.x + 4, panelRect.y + 4, panelRect.width, panelRect.height), ModalDropShadow);
            EditorGUI.DrawRect(panelRect, BgSurface);

            // Borders
            EditorGUI.DrawRect(new Rect(panelRect.x,        panelRect.y,        panelRect.width, 1), BorderFocus);
            EditorGUI.DrawRect(new Rect(panelRect.x,        panelRect.yMax - 1, panelRect.width, 1), BorderNorm);
            EditorGUI.DrawRect(new Rect(panelRect.x,        panelRect.y,        1, panelRect.height), BorderFocus);
            EditorGUI.DrawRect(new Rect(panelRect.xMax - 1, panelRect.y,        1, panelRect.height), BorderNorm);

            // Empty state
            if (results.Length == 0)
            {
                var emptySt = new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 12,
                    normal    = { textColor = TxtMuted },
                    alignment = TextAnchor.MiddleCenter,
                };
                GUI.Label(panelRect, "Không tìm thấy kết quả", emptySt);

                if (Event.current.type == EventType.MouseDown)
                {
                    _showSearchResults = false;
                    Event.current.Use();
                    Repaint();
                }
                return;
            }

            // Result row styles
            var titleSt   = new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = TxtPrimary  }, wordWrap = false };
            var sectionSt = new GUIStyle(EditorStyles.label) { fontSize = 9,  fontStyle = FontStyle.Bold, normal = { textColor = AccentBlue  }, wordWrap = false };
            var snippetSt = new GUIStyle(EditorStyles.label) { fontSize = 11,                             normal = { textColor = TxtSecond   }, wordWrap = true  };

            float scrollContent = results.Length * rowH;
            Rect viewRect  = new Rect(panelRect.x, panelRect.y + 6,  panelRect.width,     panelRect.height - 12);
            Rect innerRect = new Rect(0,            0,                panelRect.width - 4, scrollContent);
            _searchScroll  = GUI.BeginScrollView(viewRect, _searchScroll, innerRect, false, false);

            for (int i = 0; i < results.Length; i++)
            {
                var  e   = results[i];
                Rect row = new Rect(0, i * rowH, panelRect.width - 4, rowH);

                bool hover = row.Contains(Event.current.mousePosition);
                if (hover)
                {
                    EditorGUI.DrawRect(row, BgElevated);
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3, row.height), AccentBlue);
                }

                float px      = hover ? 14f : 12f;
                string tabName = _tabLabels[e.TabIndex].Trim();
                if (tabName.Length > 2 && tabName[1] == ' ') tabName = tabName.Substring(2);

                GUI.Label(new Rect(row.x + px, row.y + 6,  row.width - px - 8, 13), tabName.ToUpper(), sectionSt);
                GUI.Label(new Rect(row.x + px, row.y + 21, row.width - px - 8, 18), e.Title,           titleSt);
                GUI.Label(new Rect(row.x + px, row.y + 40, row.width - px - 8, 18), e.Snippet,         snippetSt);

                if (i < results.Length - 1)
                    EditorGUI.DrawRect(new Rect(row.x + 8, row.yMax - 1, row.width - 16, 1), BorderSub);

                if (hover && Event.current.type == EventType.MouseDown)
                {
                    _activeTab = e.TabIndex;
                    if (e.CodeSubTab >= 0) _codeSubTab = e.CodeSubTab;
                    _searchQuery       = "";
                    _showSearchResults = false;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUI.EndScrollView();

            // Click ngoài panel → đóng
            if (Event.current.type == EventType.MouseDown && !panelRect.Contains(Event.current.mousePosition))
            {
                _showSearchResults = false;
                Event.current.Use();
                Repaint();
            }
        }

    }  // partial class TISDocWindow
}
#endif