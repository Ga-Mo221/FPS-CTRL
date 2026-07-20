// ============================================================
//  TrueInputSystem — TISConstants.cs
//  P3 FIX #19: Tập trung tất cả magic number / hằng số tại đây.
//  Trước đây MaxAnalogTriggers (InputBridge) và MaxMobileButtons
//  (BindingPathOptions) đều = 16 nhưng không liên kết → đổi một
//  bên dễ gây lệch. Từ bây giờ cả hai đọc từ file này.
//
//  CÁCH DÙNG:
//      using TrueInputSystem;
//      int max = TISConstants.MaxAnalogTriggers;
// ============================================================

namespace TrueInputSystem
{
    /// <summary>
    /// Hằng số toàn hệ thống TrueInputSystem.
    /// Mọi magic number nên được khai báo ở đây.
    /// </summary>
    public static class TISConstants
    {
        // ── Input Action ──────────────────────────────────────

        /// <summary>
        /// Số trigger analog tối đa được theo dõi đồng thời.
        /// Dùng bởi TISInputBridge để cấp phát <c>_triggerHeldLastFrame</c>
        /// và <c>_triggerHeldThisFrame</c>.
        /// </summary>
        public const int MaxAnalogTriggers = 16;

        /// <summary>
        /// Số on-screen skill button tối đa hiển thị trong dropdown binding.
        /// Dùng bởi <c>TISBindingPathOptions.Mobile()</c>.
        /// Phải bằng hoặc lớn hơn số button thực tế trong UI.
        /// </summary>
        public const int MaxMobileButtons = 16;

        // ── Trigger ───────────────────────────────────────────

        /// <summary>
        /// Ngưỡng giá trị (0–1) để coi analog trigger là "được nhấn".
        /// Dưới ngưỡng = released, từ ngưỡng trở lên = pressed.
        /// </summary>
        public const float TriggerPressPoint = 0.5f;

        // ── Sensitivity / Normalization ───────────────────────

        /// <summary>
        /// Hệ số chia để chuẩn hoá <c>&lt;Mouse&gt;/delta</c> về đơn vị
        /// tương đương với sensitivity 1.0 ở 1080p.
        /// </summary>
        public const float MouseDeltaNormalizeFactor = 100f;

        // ── Touch / Spatial Matching ──────────────────────────

        /// <summary>
        /// Bán kính pixel dùng trong <c>TISMobileButtonRegistry</c>
        /// để spatial-match uGUI pointer với EnhancedTouch touchId.
        /// 80px rộng hơn nút skill button lớn nhất mặc định (100px → 50px bán kính).
        /// </summary>
        public const float ButtonSpatialMatchRadiusPx = 80f;

        // ── Camera Zone ───────────────────────────────────────

        /// <summary>Tỉ lệ mặc định chiều rộng vùng camera (bên phải màn hình).</summary>
        public const float DefaultCameraZoneWidthRatio = 0.4f;

        // ── Input Buffer ──────────────────────────────────────

        /// <summary>Cửa sổ buffer mặc định (giây) cho <c>TISInputBuffer</c>.</summary>
        public const float DefaultInputBufferWindowSeconds = 0.15f;
    }
}
