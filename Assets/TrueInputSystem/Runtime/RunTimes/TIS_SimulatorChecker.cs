#if UNITY_EDITOR
using UnityEngine;

namespace TrueInputSystem
{
    public static class TISSimulatorChecker
    {
        /// <summary>
        /// True nếu đang chạy trong Device Simulator
        /// </summary>
        public static bool IsRunningInDeviceSimulator()
        {
            return UnityEngine.SystemInfo.deviceType !=
                   UnityEngine.Device.SystemInfo.deviceType;
        }

        /// <summary>
        /// True nếu đang chạy Game View bình thường
        /// </summary>
        public static bool IsRunningInGameView()
        {
            return !IsRunningInDeviceSimulator();
        }
    }
}
#endif