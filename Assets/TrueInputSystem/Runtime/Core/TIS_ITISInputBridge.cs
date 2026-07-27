// ============================================================
//  TrueInputSystem — ITISInputBridge.cs
//  P3 FIX #16: Interface cho TISInputBridge.
//
//  VẤN ĐỀ CŨ: Toàn bộ game code phụ thuộc trực tiếp vào
//  concrete class TISInputBridge. Không thể:
//    - Inject mock input để unit test
//    - Replay input từ recording
//    - Swap implementation mà không sửa tất cả caller
//
//  CÁCH DÙNG (script game):
//    // Thay vì:
//    [SerializeField] private TISInputBridge _bridge;
//
//    // Dùng:
//    [SerializeField] private TISInputBridgeComponent _bridgeComponent;
//    private ITISInputBridge _bridge;
//    void Awake() => _bridge = _bridgeComponent;
//
//    // Hoặc lấy qua singleton (vẫn hoạt động):
//    private ITISInputBridge _bridge;
//    void Awake() => _bridge = TISInputBridge.Instance;
//
//  LƯU Ý: TISInputBridge implements interface này — không cần
//  sửa code hiện có nếu vẫn dùng concrete type. Interface chỉ
//  cần thiết khi muốn test hoặc swap implementation.
// ============================================================

using UnityEngine;

namespace TrueInputSystem
{
    /// <summary>
    /// Interface đọc input — tất cả game script nên type-hint qua đây.
    /// TISInputBridge implement interface này.
    /// </summary>
    public interface ITISInputBridge
    {
        // ── Movement / Camera ─────────────────────────────────

        /// <summary>Hướng di chuyển 2D (WASD, left stick, virtual joystick).</summary>
        Vector2 MoveDirection { get; }

        /// <summary>Delta xoay camera frame này (mouse delta, right stick, swipe).</summary>
        Vector2 LookDelta { get; }

        /// <summary>Delta zoom frame này (scroll wheel, pinch).</summary>
        float ZoomDelta { get; }

        // ── Convenience properties ─────────────────────────────

        bool IsJumping   { get; }
        bool IsRunning   { get; }
        bool IsAttacking { get; }

        // ── Skill Actions ─────────────────────────────────────

        /// <summary>
        /// Đọc trạng thái action theo tên, dùng readMode từ profile.
        /// </summary>
        bool GetSkillAction(string actionName);

        /// <summary>
        /// Đọc trạng thái action theo tên, override readMode.
        /// </summary>
        bool GetSkillAction(string actionName, TISButtonReadMode mode);

        bool IsSkillDown(string actionName);
        bool IsSkillHeld(string actionName);
        bool IsSkillReleased(string actionName);

        // ── Axis / Vector2 ────────────────────────────────────

        Vector2 GetVector2Action(string actionName);
        float   GetAxisAction(string actionName);

        // ── Gesture (Multi-finger) ────────────────────────────

        int     GestureActiveFingers     { get; }
        Vector2 GestureCentroidPosition  { get; }
        Vector2 GestureCentroidDelta     { get; }
        bool    GestureIsTwoFingerPanning { get; }
        Vector2 GestureTwoFingerPanDelta  { get; }
        Vector2 GestureTwoFingerPanRaw    { get; }
        Vector2 GestureFinger0Delta       { get; }
        Vector2 GestureFinger1Delta       { get; }
        Vector2 GestureGetFingerPosition(int index);

        // ── Object Tracker ────────────────────────────────────

        bool       ToucherIsSingleTouch();
        bool       ToucherIsDoubleTapOnScreen();
        bool       ToucherIsDoubleTapOnObject(int instanceID);
        GameObject ToucherGetSingleObject();
        GameObject ToucherGetDoubleTappedObject();
        int        ToucherGetLastID();
        bool       ToucherWasTouched(int instanceID);
        void       ToucherClearAll();

        // ── Drag Position ─────────────────────────────────────

        /// <summary>
        /// [API 1 — UI Drag] Vị trí screen space (pixel) của pointer/touch đang giữ.
        /// Cập nhật liên tục mỗi frame.
        /// Dùng để di chuyển RectTransform khi kéo item trong Canvas UI.
        /// Vector2.zero nếu không có pointer active.
        /// </summary>
        Vector2 GetPointerScreenPosition();

        /// <summary>
        /// Delta screen (pixel) của pointer so với frame trước.
        /// Dùng để tính offset kéo trong UI.
        /// Vector2.zero nếu không có pointer active.
        /// </summary>
        Vector2 GetPointerScreenDelta();

        /// <summary>
        /// [API 2 — World Drag] Vị trí World Space tương ứng với pointer/touch đang giữ.
        /// Dùng để kéo thả / vứt GameObject trong không gian 3D hoặc 2D.
        /// Vector3.zero nếu không có pointer active hoặc không tìm được Camera.
        /// </summary>
        Vector3 GetPointerWorldPosition(float depth = 10f);

        /// <summary>true khi pointer/touch đang được giữ (drag đang diễn ra).</summary>
        bool IsPointerHeld { get; }

        // ── Profile Info ──────────────────────────────────────

        TISCameraSettings  TISCameraSettings  { get; }
        TISMobileSettings  TISMobileSettings  { get; }
        TISGamepadSettings TISGamepadSettings { get; }
        
        // ── Active Device ─────────────────────────────────────

        /// <summary>
        /// Loại thiết bị input người chơi đang dùng tích cực trong frame này.
        /// Dùng để đổi UI hint (hiện icon bàn phím, gamepad, hay on-screen button).
        /// </summary>
        TISActiveDevice ActiveDevice { get; }

        /// <summary>
        /// true khi người chơi đang dùng Keyboard hoặc Mouse.
        /// </summary>
        bool IsKeyboardMouse { get; }

        /// <summary>
        /// true khi người chơi đang dùng Gamepad / Controller.
        /// </summary>
        bool IsGamepad { get; }

        /// <summary>
        /// true khi đang chạy trên Mobile (hoặc SimulateMobileInEditor).
        /// </summary>
        bool IsMobile { get; }

        // ── Override Channels (Mobile / Internal) ─────────────

        void OverrideMoveInput(Vector2 value);
        void ClearMoveOverride();
        void OverrideLookInput(Vector2 delta);
        void ClearLookOverride();
        void OverrideZoomInput(float delta);
        void ClearZoomOverride();

        /// <summary>
        /// Ghi đè giá trị của một action Vector2 theo tên.
        /// Dùng khi TISDirectionalSkillButton (hoặc code khác) muốn inject
        /// Direction vào action mà không cần lấy component button.
        /// </summary>
        void OverrideVector2Action(string actionName, Vector2 value);

        /// <summary>
        /// Xóa override của action Vector2, trả về đọc InputSystem bình thường.
        /// </summary>
        void ClearVector2Override(string actionName);

        // ── Action Map ────────────────────────────────────────

        void SetActionMapActive(string mapName, bool active);
        void DisableAllMaps();
        void EnableDefaultMaps();

        // ── Gesture Sync ──────────────────────────────────────

        void GestureSyncZone(float zoneStartRatio, float zoneEndRatio = 1f);
        void GestureSyncZone();

        // ── Profile Swap ──────────────────────────────────────

        void SwapProfile(TISInputProfileConfig newProfile);
    }
}