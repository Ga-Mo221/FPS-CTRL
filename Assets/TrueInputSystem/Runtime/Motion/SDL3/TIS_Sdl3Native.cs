// ============================================================
//  TrueInputSystem — TIS_Sdl3Native.cs
//
//  P/Invoke tối thiểu vào SDL3 (chỉ phần Gamepad: sensor + touchpad).
//  Không phụ thuộc wrapper bên thứ ba.
//
//  Native library đi kèm TIS (cài TIS là có, không cần thao tác thêm):
//    Windows : TrueInputSystem/Runtime/Motion/SDL3/Plugins/x86_64/SDL3.dll  (SDL 3.4.18, zlib license)
//              SHA-256 + nguồn tải: Plugins/x86_64/SDL3-VERSION.txt (DLL không có chữ ký số)
//  Nền tảng khác — tự thêm nếu cần (thiếu thì chỉ mất gyro / touchpad, không lỗi):
//    macOS   : .../SDL3/Plugins/macOS/libSDL3.dylib
//    Linux   : .../SDL3/Plugins/x86_64/libSDL3.so
//
//  SDL3 dùng C99 `bool` (1 byte) → marshal bằng UnmanagedType.U1.
// ============================================================

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX

using System;
using System.Runtime.InteropServices;

namespace GaMo.TrueInputSystem
{
    internal static class TISSdl3Native
    {
        private const string Lib = "SDL3";

        public const uint SDL_INIT_GAMEPAD = 0x00002000u;   // tự kéo theo JOYSTICK

        // SDL_SensorType
        public const int SDL_SENSOR_ACCEL = 1;
        public const int SDL_SENSOR_GYRO  = 2;

        // SDL_GamepadButton
        public const int SDL_GAMEPAD_BUTTON_TOUCHPAD = 20;

        /// <summary>SDL_STANDARD_GRAVITY — accelerometer trả về m/s².</summary>
        public const float StandardGravity = 9.80665f;

        // ── Init / hints ──────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_SetHint(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_InitSubSystem(uint flags);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_QuitSubSystem(uint flags);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetVersion();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetError();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_free(IntPtr mem);

        // ── Gamepad enumeration ───────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_SetGamepadEventsEnabled([MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_SetJoystickEventsEnabled([MarshalAs(UnmanagedType.U1)] bool enabled);

        /// <summary>Poll trạng thái + phát hiện cắm/rút khi không chạy event loop.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_UpdateGamepads();

        /// <summary>Trả về mảng SDL_JoystickID (uint32) — phải SDL_free.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetGamepads(out int count);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr SDL_OpenGamepad(uint instanceId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_CloseGamepad(IntPtr gamepad);

        /// <summary>Rung (0..65535 mỗi motor) trong durationMs ms (tối đa 0xFFFF). Gửi 0, 0 để dừng.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_RumbleGamepad(IntPtr gamepad, ushort lowFrequency, ushort highFrequency, uint durationMs);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GamepadConnected(IntPtr gamepad);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GetGamepadButton(IntPtr gamepad, int button);

        // ── Sensors ───────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GamepadHasSensor(IntPtr gamepad, int type);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_SetGamepadSensorEnabled(IntPtr gamepad, int type,
            [MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GetGamepadSensorData(IntPtr gamepad, int type,
            [Out] float[] data, int numValues);

        // ── Touchpad ──────────────────────────────────────────

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetNumGamepadTouchpads(IntPtr gamepad);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetNumGamepadTouchpadFingers(IntPtr gamepad, int touchpad);

        /// <summary>x, y chuẩn hoá 0..1, gốc ở góc TRÊN-trái.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool SDL_GetGamepadTouchpadFinger(IntPtr gamepad, int touchpad, int finger,
            out byte down, out float x, out float y, out float pressure);

        // ── Helpers ───────────────────────────────────────────

        public static string GetError()               => PtrToString(SDL_GetError());
        public static string GetGamepadName(IntPtr g) => PtrToString(SDL_GetGamepadName(g));

        /// <summary>"major.minor.patch" của SDL3 đang load.</summary>
        public static string GetVersionString()
        {
            int v = SDL_GetVersion();   // SDL_VERSIONNUM = major*1000000 + minor*1000 + patch
            return $"{v / 1000000}.{v / 1000 % 1000}.{v % 1000}";
        }

        /// <summary>Danh sách instance id của các gamepad đang cắm.</summary>
        public static uint[] GetGamepadIds()
        {
            IntPtr arr = SDL_GetGamepads(out int count);
            if (arr == IntPtr.Zero) return Array.Empty<uint>();
            try
            {
                var ids = new uint[Math.Max(0, count)];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = (uint)Marshal.ReadInt32(arr, i * sizeof(uint));
                return ids;
            }
            finally
            {
                SDL_free(arr);
            }
        }

        private static string PtrToString(IntPtr p)
            => p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(p);
    }
}

#endif
