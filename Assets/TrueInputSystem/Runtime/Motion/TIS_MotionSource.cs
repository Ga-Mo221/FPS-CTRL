// ============================================================
//  TrueInputSystem — TIS_MotionSource.cs
//
//  Lớp trừu tượng cho cảm biến chuyển động (gyro / accelerometer)
//  và touchpad của tay cầm. Mỗi nền tảng cung cấp một provider riêng:
//    - TISSimulatedMotionSource : giả lập bằng bàn phím / chuột (test)
//    - (sau này) SDL3 provider  : PC — DualShock 4 / DualSense / Switch Pro
//    - (sau này) Console        : PS4 / PS5 / Switch qua SDK của nền tảng
//
//  QUY ƯỚC TRỤC (controller-local, giống SDL3 / GamepadMotionHelpers):
//    X = sang phải, Y = hướng lên, Z = hướng về phía người chơi.
//    Gyro (độ/giây), quy tắc bàn tay phải:
//      gyro.x > 0 : mặt trước tay cầm ngóc lên  (pitch lên)
//      gyro.y > 0 : xoay tay cầm sang trái     (yaw trái)
//      gyro.z > 0 : nghiêng tay cầm sang trái  (roll trái)
//    Accel (đơn vị g): tay cầm nằm yên trên bàn → ≈ (0, 1, 0).
//
//  QUY ƯỚC TOUCHPAD:
//    Vị trí chuẩn hoá 0..1, gốc (0,0) ở góc DƯỚI-TRÁI
//    (cùng chiều với toạ độ màn hình Unity).
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem
{
    /// <summary>Một điểm chạm trên touchpad của tay cầm.</summary>
    public struct TISTouchPoint
    {
        /// <summary>Id của ngón tay — đổi giá trị khi ngón tay nhấc lên và chạm lại.</summary>
        public int     id;

        /// <summary><c>true</c> nếu ngón tay đang chạm touchpad.</summary>
        public bool    active;

        /// <summary>Vị trí chuẩn hoá 0..1, gốc ở góc dưới-trái.</summary>
        public Vector2 position;
    }

    /// <summary>
    /// Nguồn dữ liệu cảm biến chuyển động + touchpad của một tay cầm.
    /// TISInputBridge gọi <see cref="Tick"/> mỗi frame trước khi đọc dữ liệu.
    /// </summary>
    public interface ITISMotionSource
    {
        /// <summary>Tên hiển thị (debug), ví dụ "Simulated", "DualSense (SDL3)".</summary>
        string Name { get; }

        /// <summary>
        /// Khi nhiều source cùng CONNECTED, source có Priority cao nhất được dùng.
        /// Quy ước: Simulated = 0, SDL3 (PC) = 100, Console = 200.
        /// </summary>
        int Priority { get; }

        /// <summary><c>true</c> khi provider đang có thiết bị hợp lệ để đọc.</summary>
        bool IsConnected { get; }

        /// <summary>Thiết bị có gyro + accelerometer.</summary>
        bool HasMotion { get; }

        /// <summary>Thiết bị có touchpad.</summary>
        bool HasTouchpad { get; }

        /// <summary>Số điểm chạm tối đa touchpad hỗ trợ (DS4 / DualSense = 2).</summary>
        int MaxTouchPoints { get; }

        /// <summary>Poll dữ liệu mới từ thiết bị. Gọi đúng 1 lần mỗi frame.</summary>
        /// <param name="unscaledDeltaTime">Thời gian thực (không bị timeScale ảnh hưởng).</param>
        void Tick(float unscaledDeltaTime);

        /// <summary>Tốc độ góc thô (độ/giây), CHƯA trừ calibration offset.</summary>
        Vector3 Gyro { get; }

        /// <summary>Gia tốc (đơn vị g).</summary>
        Vector3 Accel { get; }

        /// <summary>Đọc điểm chạm thứ <paramref name="index"/>.</summary>
        bool TryGetTouch(int index, out TISTouchPoint point);

        /// <summary><c>true</c> khi touchpad đang được nhấn xuống (click).</summary>
        bool TouchpadPressed { get; }
    }
}
