// ============================================================
//  TrueInputSystem — TISOverrideChannel.cs
//
//  P3 FIX #15: Tách ra từ TISInputBridge.
//  Trách nhiệm DUY NHẤT:
//    - Lưu trữ và cung cấp Move / Look / Zoom override values
//    - Đọc MoveDirection và LookDelta (kết hợp action + override)
//    - Đọc ZoomDelta (pinch trên mobile, scroll trên PC)
//    - Quản lý _zoomConsumedThisFrame (P2 FIX #10)
//
//  TISVirtualJoystick gọi OverrideMoveInput().
//  TISTouchCameraZone gọi OverrideLookInput() / OverrideZoomInput().
//  TISInputBridge.LateUpdate() gọi ResetFrameFlags() để clear consumed flag.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrueInputSystem
{
    /// <summary>
    /// Kênh override cho Move / Look / Zoom input đến từ mobile controls.
    /// Plain class — không phải MonoBehaviour.
    /// </summary>
    public sealed class TISOverrideChannel
    {
        // ══════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════

        // Move
        private Vector2 _moveOverride;
        private bool    _useMoveOverride;

        // Look
        private Vector2 _lookOverride;
        private bool    _useLookOverride;

        // Zoom
        private float   _zoomDelta;
        private bool    _useZoomOverride;

        // P2 FIX #10: single-read flag cho ZoomDelta trên mobile
        private bool    _zoomConsumedThisFrame;

        // Dependencies — set on each Load()
        private bool    _isMobileMode;

        // References to underlying InputActions (supplied by TISActionReader)
        private InputAction _moveAction;
        private InputAction _lookAction;
        private bool        _lookActionIsMouseDelta;
        
        private TISCameraSettings _cameraSettings;
        private Func<TISActiveDevice> _getActiveDevice;

        // ══════════════════════════════════════════════════════
        //  SETUP
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Cập nhật action references sau khi build/swap profile.
        /// </summary>
        public void Load(
            InputAction moveAction,
            InputAction lookAction,
            bool lookActionIsMouseDelta,
            bool isMobileMode,
            TISCameraSettings cameraSettings,
            Func<TISActiveDevice> getActiveDevice)
        {
            _moveAction            = moveAction;
            _lookAction            = lookAction;
            _lookActionIsMouseDelta = lookActionIsMouseDelta;
            _isMobileMode          = isMobileMode;
            _cameraSettings         = cameraSettings;
            _getActiveDevice        = getActiveDevice;

            // Reset override state khi đổi profile
            ClearAll();
        }

        /// <summary>Cập nhật _isMobileMode khi SimulateMobile thay đổi (Editor only).</summary>
        public void SetMobileMode(bool isMobileMode) => _isMobileMode = isMobileMode;

        /// <summary>
        /// Gọi từ TISInputBridge.LateUpdate() — reset per-frame flags (P2 FIX #10).
        /// </summary>
        public void ResetFrameFlags()
        {
            _zoomConsumedThisFrame = false;
        }

        // ══════════════════════════════════════════════════════
        //  READ — MoveDirection
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Hướng di chuyển 2D frame này.
        /// Ưu tiên override (từ joystick ảo), fallback về InputAction.
        /// </summary>
        public Vector2 MoveDirection
        {
            get
            {
                if (_useMoveOverride) return _moveOverride;
                return _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            }
        }

        // ══════════════════════════════════════════════════════
        //  READ — LookDelta
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Camera look delta frame này.
        /// Trên mobile: override từ TISTouchCameraZone.
        /// Trên PC: đọc từ Mouse/delta hoặc right stick.
        /// </summary>
        // Sửa LookDelta getter
        public Vector2 LookDelta
        {
            get
            {
                if (_useLookOverride)
                    return _lookOverride * _cameraSettings.mobileSensitivity;

                if (_lookAction == null) return Vector2.zero;

                var raw = _lookAction.ReadValue<Vector2>();

                bool isMouse =
                    _lookAction.activeControl?.device is Mouse ||
                    (_lookAction.activeControl == null && _lookActionIsMouseDelta);

                if (isMouse)
                {
                    raw /= TISConstants.MouseDeltaNormalizeFactor;
                    raw *= _cameraSettings.mouseSensitivity;
                }
                else if (_isMobileMode)
                {
                    // Mobile đọc từ action (không phải override) — ví dụ virtual joystick look
                    raw *= _cameraSettings.mobileSensitivity;
                }
                else
                {
                    // Gamepad right stick
                    raw *= _cameraSettings.gamepadSensitivity;
                }

                return raw;
            }
        }

        // ══════════════════════════════════════════════════════
        //  READ — ZoomDelta
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Zoom delta frame này.
        /// Mobile: pinch gesture (single-read per frame — P2 FIX #10).
        /// PC: mouse scroll wheel.
        /// </summary>
        public float ZoomDelta
        {
            get
            {
                if (_isMobileMode)
                {
                    if (!_useZoomOverride || _zoomConsumedThisFrame) return 0f;
                    _zoomConsumedThisFrame = true;
                    return _zoomDelta;
                }

                var mouse = Mouse.current;
                return mouse != null ? mouse.scroll.ReadValue().y : 0f;
            }
        }

        // ══════════════════════════════════════════════════════
        //  WRITE — Override setters (gọi từ mobile controls)
        // ══════════════════════════════════════════════════════

        public void OverrideMoveInput(Vector2 value) { _moveOverride = value; _useMoveOverride = true; }
        public void ClearMoveOverride()              { _moveOverride = Vector2.zero; _useMoveOverride = false; }

        public void OverrideLookInput(Vector2 delta) { _lookOverride = delta; _useLookOverride = true; }
        public void ClearLookOverride()              { _lookOverride = Vector2.zero; _useLookOverride = false; }

        public void OverrideZoomInput(float delta)   { _zoomDelta = delta; _useZoomOverride = true; }
        public void ClearZoomOverride()              { _zoomDelta = 0f; _useZoomOverride = false; }

        // ══════════════════════════════════════════════════════
        //  STATE QUERY (dùng cho debug panel)
        // ══════════════════════════════════════════════════════

        public bool UseMoveOverride => _useMoveOverride;
        public bool UseLookOverride => _useLookOverride;
        public bool UseZoomOverride => _useZoomOverride;

        // ══════════════════════════════════════════════════════
        //  RESET
        // ══════════════════════════════════════════════════════

        /// <summary>Reset toàn bộ override state — gọi khi swap profile.</summary>
        public void ClearAll()
        {
            _moveOverride          = Vector2.zero;
            _useMoveOverride       = false;
            _lookOverride          = Vector2.zero;
            _useLookOverride       = false;
            _zoomDelta             = 0f;
            _useZoomOverride       = false;
            _zoomConsumedThisFrame = false;
        }
    }
}
