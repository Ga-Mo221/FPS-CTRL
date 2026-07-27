using UnityEngine;

namespace TrueInputSystem
{
    /// <summary>
    /// Extension methods thêm mobile button support cho TISInputBridge.
    /// </summary>
    public static class TISInputBridgeExtensions
    {
        /// <summary>
        /// Set trạng thái on-screen button.
        /// Gọi bởi <see cref="TISMobileSkillButton"/>.
        /// </summary>
        /// <param name="bridge">TISInputBridge instance.</param>
        /// <param name="skillName">Tên action (phải khớp với profile).</param>
        /// <param name="pressed">true = đang nhấn, false = nhả.</param>
        public static void SetMobileButtonState(
            this TISInputBridge bridge, string skillName, bool pressed)
        {
            TISMobileButtonRegistry.SetState(skillName, pressed);
        }

        /// <summary>
        /// Lấy trạng thái on-screen button trực tiếp từ registry.
        /// Bypass InputAction — dùng khi cần check nhanh không qua profile.
        /// </summary>
        public static bool GetMobileButtonState(
            this TISInputBridge bridge, string skillName)
        {
            return TISMobileButtonRegistry.GetState(skillName);
        }

        /// <summary>
        /// Xoá registry khi swap profile — tránh trạng thái "stuck pressed".
        /// TISInputBridge.SwapProfile() nên gọi method này.
        /// </summary>
        public static void ClearMobileButtons(this TISInputBridge bridge)
        {
            TISMobileButtonRegistry.ClearAll();
        }
    }
}
