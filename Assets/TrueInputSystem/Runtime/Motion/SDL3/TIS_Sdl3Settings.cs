// ============================================================
//  TrueInputSystem — TIS_Sdl3Settings.cs
//
//  Cấu hình provider SDL3 (gyro + touchpad tay cầm trên PC).
//  Hiển thị trong Inspector của TISInputBridge (mục Motion).
//
//  Class này KHÔNG bọc #if nền tảng: field serialize của TISInputBridge phải
//  giống nhau trên mọi nền tảng build (tránh lỗi "different serialization layout").
//  Logic SDL3 thật nằm trong TISSdl3MotionSource (chỉ compile cho Editor / PC).
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    [Serializable]
    public class TISSdl3Settings
    {
        [Tooltip("Chu kỳ (giây) quét tay cầm mới khi chưa có tay cầm nào được mở.")]
        [Range(0.2f, 5f)]
        public float rescanInterval = 1f;

        [Tooltip("Hint SDL_JOYSTICK_ENHANCED_REPORTS: \"auto\" / \"1\" / \"0\".\n" +
                 "Tay PlayStation qua Bluetooth cần enhanced report mới có gyro / touchpad.\n" +
                 "Lưu ý: đã bật thì tay cầm giữ chế độ đó tới khi tắt nguồn.")]
        public string enhancedReports = "auto";

        [Tooltip("Tắt đèn báo số người chơi (player LED) mà SDL bật trên DualSense.")]
        public bool disablePlayerLed = true;

        [Tooltip("Log kết nối / ngắt kết nối tay cầm ra Console.")]
        public bool verboseLog = true;
    }
}
