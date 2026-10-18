// ============================================================
//  TrueInputSystem — TIS_Sdl3MotionSource.cs
//
//  Provider gyro + touchpad cho PC (Windows / macOS / Linux) qua SDL3.
//  Hỗ trợ: DualShock 4, DualSense, DualSense Edge, Switch Pro, Joy-Con
//  (USB và Bluetooth). Đã có calibration theo từng tay cầm của SDL.
//
//  PHÂN CHIA VỚI UNITY INPUT SYSTEM:
//    - Nút / cần / trigger : Unity Input System đọc như cũ (rebinding, UI...)
//    - Gyro / accel / touchpad : SDL3 đọc — provider này
//  SDL chỉ bật driver HIDAPI (tắt RawInput / XInput / WGI / DirectInput)
//  để không tranh thiết bị với Unity và không đụng tới tay Xbox.
//
//  Không có SDL3 native library → log hướng dẫn 1 lần, provider = disconnected,
//  TIS tự rơi về provider khác (ví dụ Simulated) hoặc tắt gyro.
//
//  KHÔNG cần add component: TISInputBridge tự tạo provider này khi bật
//  "Use SDL3 On PC" trong Inspector (mục Motion). Cấu hình: TISSdl3Settings.
// ============================================================

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX

using System;
using System.Collections.Generic;
using UnityEngine;
using N = GaMo.TrueInputSystem.TISSdl3Native;

namespace GaMo.TrueInputSystem
{
    public sealed class TISSdl3MotionSource : ITISMotionSource
    {
        private readonly TISSdl3Settings _settings;

        // ── SDL state ─────────────────────────────────────────
        private bool   _sdlReady;
        private bool   _sdlUnavailable;          // thiếu DLL / init lỗi → không thử lại
        private IntPtr _gamepad = IntPtr.Zero;
        private string _gamepadName = "";
        private bool   _hasGyro;
        private bool   _hasAccel;
        private bool   _hasTouchpad;
        private float  _rescanTimer;
        private readonly HashSet<uint> _rejectedIds = new HashSet<uint>();
        private readonly float[] _buf = new float[3];

        // ── Output ────────────────────────────────────────────
        private Vector3       _gyro;
        private Vector3       _accel;
        private TISTouchPoint _touch;
        private int           _nextTouchId;
        private bool          _touchpadPressed;

        // ══════════════════════════════════════════════════════
        //  ITISMotionSource
        // ══════════════════════════════════════════════════════

        public string  Name            => _gamepad != IntPtr.Zero ? $"{_gamepadName} (SDL3)" : "SDL3 (no device)";
        public int     Priority        => 100;
        public bool    IsConnected     => _gamepad != IntPtr.Zero;
        public bool    HasMotion       => _gamepad != IntPtr.Zero && _hasGyro;
        public bool    HasTouchpad     => _gamepad != IntPtr.Zero && _hasTouchpad;
        public int     MaxTouchPoints  => 1;
        public Vector3 Gyro            => _gyro;
        public Vector3 Accel           => _accel;
        public bool    TouchpadPressed => _touchpadPressed;

        public bool TryGetTouch(int index, out TISTouchPoint point)
        {
            point = index == 0 ? _touch : default;
            return index == 0 && HasTouchpad;
        }

        public void Tick(float dt)
        {
            if (!_sdlReady) return;

            try
            {
                N.SDL_UpdateGamepads();

                if (_gamepad != IntPtr.Zero && !N.SDL_GamepadConnected(_gamepad))
                {
                    Log($"Tay cầm \"{_gamepadName}\" đã ngắt kết nối.");
                    CloseGamepad();
                }

                if (_gamepad == IntPtr.Zero)
                {
                    _rescanTimer -= dt;
                    if (_rescanTimer > 0f) return;
                    _rescanTimer = _settings.rescanInterval;
                    TryOpenGamepad();
                    if (_gamepad == IntPtr.Zero) return;
                }

                ReadSensors();
                ReadTouchpad();
            }
            catch (Exception e)
            {
                // EntryPointNotFound (DLL quá cũ) hoặc lỗi native khác → tắt hẳn provider
                Debug.LogError($"[TISSdl3MotionSource] Lỗi SDL3, tắt provider: {e.Message}");
                ShutdownSdl();
                _sdlUnavailable = true;
            }
        }

        // ══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════

        public TISSdl3MotionSource(TISSdl3Settings settings)
        {
            _settings = settings ?? new TISSdl3Settings();
        }

        /// <summary>Khởi tạo SDL3 (gọi khi bật provider).</summary>
        public void Start() => InitSdl();

        /// <summary>Đóng tay cầm + tắt SDL3 (gọi khi tắt provider / huỷ bridge).</summary>
        public void Stop()  => ShutdownSdl();

        /// <summary><c>true</c> khi thiếu DLL hoặc SDL init lỗi — provider sẽ không thử lại.</summary>
        public bool IsUnavailable => _sdlUnavailable;

        // ══════════════════════════════════════════════════════
        //  SDL INIT
        // ══════════════════════════════════════════════════════

        private void InitSdl()
        {
            if (_sdlReady || _sdlUnavailable) return;

            try
            {
                // Chỉ dùng HIDAPI: đọc trực tiếp PS4 / PS5 / Switch, không đăng ký RawInput
                // (tránh tranh raw input với Unity) và không mở tay Xbox.
                N.SDL_SetHint("SDL_JOYSTICK_RAWINPUT",       "0");
                N.SDL_SetHint("SDL_JOYSTICK_WGI",            "0");
                N.SDL_SetHint("SDL_JOYSTICK_DIRECTINPUT",    "0");
                N.SDL_SetHint("SDL_XINPUT_ENABLED",          "0");
                N.SDL_SetHint("SDL_JOYSTICK_HIDAPI_XBOX",    "0");
                N.SDL_SetHint("SDL_JOYSTICK_HIDAPI",         "1");
                N.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
                N.SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS",
                    string.IsNullOrEmpty(_settings.enhancedReports) ? "auto" : _settings.enhancedReports);
                if (_settings.disablePlayerLed)
                    N.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS5_PLAYER_LED", "0");

                if (!N.SDL_InitSubSystem(N.SDL_INIT_GAMEPAD))
                {
                    Debug.LogError($"[TISSdl3MotionSource] SDL_InitSubSystem(GAMEPAD) lỗi: {N.GetError()}");
                    _sdlUnavailable = true;
                    return;
                }

                // Không chạy SDL event loop → tắt event, tự poll bằng SDL_UpdateGamepads()
                N.SDL_SetJoystickEventsEnabled(false);
                N.SDL_SetGamepadEventsEnabled(false);

                _sdlReady    = true;
                _rescanTimer = 0f;
                _rejectedIds.Clear();
                Log($"SDL {N.GetVersionString()} sẵn sàng.");
            }
            catch (DllNotFoundException)
            {
                _sdlUnavailable = true;
                Debug.LogWarning(
                    "[TISSdl3MotionSource] Không tìm thấy SDL3 native library → gyro / touchpad tay cầm PC bị tắt.\n" +
                    "→ SDL3.dll đi kèm TIS tại TrueInputSystem/Runtime/Motion/SDL3/Plugins/x86_64/ — kiểm tra file còn đó\n" +
                    "  và Inspector của DLL bật Editor + Standalone Windows x64 (tải lại: github.com/libsdl-org/SDL/releases, bản win32-x64).");
            }
            catch (EntryPointNotFoundException e)
            {
                _sdlUnavailable = true;
                Debug.LogWarning($"[TISSdl3MotionSource] SDL3 library không đúng phiên bản (cần SDL 3.2+): {e.Message}");
            }
        }

        private void ShutdownSdl()
        {
            if (!_sdlReady) return;
            try
            {
                CloseGamepad();
                N.SDL_QuitSubSystem(N.SDL_INIT_GAMEPAD);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TISSdl3MotionSource] Lỗi khi tắt SDL: {e.Message}");
            }
            _sdlReady = false;
        }

        // ══════════════════════════════════════════════════════
        //  RUNG
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Rung tay cầm SDL3 đang mở. Cần cho DualShock 4 / DualSense qua Bluetooth —
        /// Unity Input System (SetMotorSpeeds) không gửi được gói rung Bluetooth của chúng.
        /// seconds &lt;= 0 → rung tối đa ~65 giây (dừng bằng Rumble(0, 0, 0)).
        /// </summary>
        public bool Rumble(float low, float high, float seconds)
        {
            if (_gamepad == IntPtr.Zero) return false;
            ushort lo = (ushort)(Mathf.Clamp01(low)  * 65535f);
            ushort hi = (ushort)(Mathf.Clamp01(high) * 65535f);
            uint ms = lo == 0 && hi == 0 ? 0u
                    : seconds <= 0f ? 0xFFFFu
                    : (uint)Mathf.Clamp(seconds * 1000f, 1f, 65535f);
            try { return N.SDL_RumbleGamepad(_gamepad, lo, hi, ms); }
            catch (EntryPointNotFoundException) { return false; }   // SDL3.dll cũ không có hàm này
        }

        // ══════════════════════════════════════════════════════
        //  DEVICE
        // ══════════════════════════════════════════════════════

        private void TryOpenGamepad()
        {
            foreach (var id in N.GetGamepadIds())
            {
                // Id là duy nhất cho mỗi lần cắm → tay cầm đã thử (không có gyro / touchpad) thì bỏ qua
                if (_rejectedIds.Contains(id)) continue;

                var gp = N.SDL_OpenGamepad(id);
                if (gp == IntPtr.Zero) { _rejectedIds.Add(id); continue; }

                bool gyro  = N.SDL_GamepadHasSensor(gp, N.SDL_SENSOR_GYRO);
                bool touch = N.SDL_GetNumGamepadTouchpads(gp) > 0
                             && N.SDL_GetNumGamepadTouchpadFingers(gp, 0) > 0;

                if (!gyro && !touch)
                {
                    Log($"Bỏ qua \"{N.GetGamepadName(gp)}\" — không có gyro / touchpad.");
                    N.SDL_CloseGamepad(gp);
                    _rejectedIds.Add(id);
                    continue;
                }

                _gamepad     = gp;
                _gamepadName = N.GetGamepadName(gp);
                _hasGyro     = gyro && N.SDL_SetGamepadSensorEnabled(gp, N.SDL_SENSOR_GYRO, true);
                _hasAccel    = N.SDL_GamepadHasSensor(gp, N.SDL_SENSOR_ACCEL)
                               && N.SDL_SetGamepadSensorEnabled(gp, N.SDL_SENSOR_ACCEL, true);
                _hasTouchpad = touch;

                Log($"Đã mở \"{_gamepadName}\" — gyro={_hasGyro} accel={_hasAccel} touchpad={_hasTouchpad}");
                return;
            }
        }

        private void CloseGamepad()
        {
            if (_gamepad != IntPtr.Zero)
                N.SDL_CloseGamepad(_gamepad);

            _gamepad         = IntPtr.Zero;
            _gamepadName     = "";
            _hasGyro         = _hasAccel = _hasTouchpad = false;
            _gyro            = Vector3.zero;
            _accel           = Vector3.zero;
            _touch           = default;
            _touchpadPressed = false;
            _rescanTimer     = 0f;
        }

        // ══════════════════════════════════════════════════════
        //  READ
        // ══════════════════════════════════════════════════════

        private void ReadSensors()
        {
            // SDL: gyro rad/s, accel m/s², trục giống quy ước TIS (X phải, Y lên, Z về phía người chơi)
            if (_hasGyro && N.SDL_GetGamepadSensorData(_gamepad, N.SDL_SENSOR_GYRO, _buf, 3))
                _gyro = new Vector3(_buf[0], _buf[1], _buf[2]) * Mathf.Rad2Deg;

            if (_hasAccel && N.SDL_GetGamepadSensorData(_gamepad, N.SDL_SENSOR_ACCEL, _buf, 3))
                _accel = new Vector3(_buf[0], _buf[1], _buf[2]) / N.StandardGravity;
        }

        private void ReadTouchpad()
        {
            if (!_hasTouchpad) return;

            _touchpadPressed = N.SDL_GetGamepadButton(_gamepad, N.SDL_GAMEPAD_BUTTON_TOUCHPAD);

            if (!N.SDL_GetGamepadTouchpadFinger(_gamepad, 0, 0, out byte down, out float x, out float y, out _)
                || down == 0)
            {
                _touch.active = false;
                return;
            }

            // SDL không có finger id → tự sinh id mới mỗi lần ngón tay chạm xuống
            if (!_touch.active)
                _touch.id = ++_nextTouchId;

            _touch.active   = true;
            _touch.position = new Vector2(Mathf.Clamp01(x), 1f - Mathf.Clamp01(y));   // SDL gốc trên-trái → TIS gốc dưới-trái
        }

        private void Log(string msg)
        {
            if (_settings.verboseLog) Debug.Log($"[TISSdl3MotionSource] {msg}");
        }
    }
}

#endif
