// ============================================================
//  TrueInputSystem — TIS_PhysicalMouse.cs
//
//  TISGamepadVirtualCursor thêm một Mouse ảo và ghi vị trí / click vào nó
//  mỗi frame. Input System đặt Mouse.current = chuột vừa update gần nhất,
//  nên Mouse.current có thể là chuột ảo → các chỗ đọc "chuột của người chơi"
//  (active device, scroll zoom, aim assist platform, object tracker...)
//  sẽ đọc nhầm.
//
//  Dùng TISPhysicalMouse.Current thay cho Mouse.current ở những chỗ cần
//  chuột THẬT. Các Mouse ảo tự đăng ký qua RegisterVirtual().
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    public static class TISPhysicalMouse
    {
        private static readonly List<InputDevice> _virtualDevices = new List<InputDevice>();
        private static Mouse _cached;

        /// <summary>Đánh dấu <paramref name="device"/> là thiết bị ảo (bị bỏ qua bởi <see cref="Current"/>).</summary>
        public static void RegisterVirtual(InputDevice device)
        {
            if (device != null && !_virtualDevices.Contains(device))
                _virtualDevices.Add(device);
        }

        public static void UnregisterVirtual(InputDevice device) => _virtualDevices.Remove(device);

        public static bool IsVirtual(InputDevice device) => device != null && _virtualDevices.Contains(device);

        /// <summary>Chuột thật đang dùng (không bao giờ là chuột ảo của virtual cursor). <c>null</c> nếu không có.</summary>
        public static Mouse Current
        {
            get
            {
                var current = Mouse.current;
                if (current != null && !IsVirtual(current))
                    return _cached = current;

                if (_cached != null && _cached.added && !IsVirtual(_cached))
                    return _cached;

                _cached = null;
                foreach (var device in InputSystem.devices)
                {
                    if (device is Mouse m && !IsVirtual(m))
                    {
                        _cached = m;
                        break;
                    }
                }
                return _cached;
            }
        }

        // Hỗ trợ Enter Play Mode khi tắt Domain Reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _virtualDevices.Clear();
            _cached = null;
        }
    }
}
