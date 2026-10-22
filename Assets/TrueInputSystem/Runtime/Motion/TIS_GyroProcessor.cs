// ============================================================
//  TrueInputSystem — TIS_GyroProcessor.cs
//
//  Xử lý gyro thô → tốc độ yaw/pitch 2D (độ/giây), độc lập nền tảng:
//    1. Calibration  : trừ drift offset (tự động khi đứng yên hoặc thủ công)
//    2. Gravity      : ước lượng hướng "lên" từ accelerometer (dùng cho Player space)
//    3. Space        : Local / Player space → yaw / pitch
//    4. Deadzone     : bỏ nhiễu rất nhỏ
//    5. Tightening   : giảm tỉ lệ input nhỏ để chống rung tay
//    6. Soft-tier smoothing : chỉ làm mượt chuyển động chậm, không thêm độ trễ cho flick
//
//  Output Rate: x > 0 = quay phải, y > 0 = nhìn lên (cùng chiều với right stick).
//  Plain class — không phải MonoBehaviour.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem
{
    public sealed class TISGyroProcessor
    {
        // Player space: cho phép yaw lớn hơn hình chiếu lên trục trọng lực một chút
        // để người chơi "lean" tay cầm vẫn quay đủ nhanh (theo JibbSmart — Player Space Gyro).
        private const float YawRelaxFactor      = 1.41f;
        private const float GravitySmoothTime   = 0.25f;
        private const int   CalibrationBatch    = 10;
        private const float CalibrationBlend    = 0.2f;

        // ── Calibration ───────────────────────────────────────
        private Vector3 _offset;
        private bool    _hasOffset;
        private float   _stillTimer;
        private Vector3 _calibSum;
        private int     _calibCount;
        private Vector3 _prevAccel;
        private bool    _hasPrevAccel;

        private bool    _manualCalibrating;
        private Vector3 _manualSum;
        private int     _manualCount;

        // ── Gravity / filtering ───────────────────────────────
        private Vector3 _up = Vector3.up;
        private Vector2 _smoothed;

        // ══════════════════════════════════════════════════════
        //  OUTPUT
        // ══════════════════════════════════════════════════════

        /// <summary>Tốc độ yaw/pitch sau xử lý (độ/giây). x = phải, y = lên.</summary>
        public Vector2 Rate           { get; private set; }

        /// <summary>Gyro 3D đã trừ calibration offset (độ/giây).</summary>
        public Vector3 CalibratedGyro { get; private set; }

        /// <summary>Hướng "lên" (đã chuẩn hoá) theo hệ trục tay cầm, ước lượng từ accelerometer.</summary>
        public Vector3 Up             => _up;

        /// <summary>Drift offset hiện tại (độ/giây).</summary>
        public Vector3 Offset         => _offset;

        /// <summary><c>true</c> khi đã có ít nhất một lần calibration.</summary>
        public bool    IsCalibrated   => _hasOffset;

        /// <summary><c>true</c> khi tay cầm đang được coi là đứng yên (đủ lâu để tự hiệu chỉnh).</summary>
        public bool    IsStill        { get; private set; }

        /// <summary><c>true</c> trong lúc đang calibration thủ công.</summary>
        public bool    IsManualCalibrating => _manualCalibrating;

        // ══════════════════════════════════════════════════════
        //  CALIBRATION API
        // ══════════════════════════════════════════════════════

        /// <summary>Bắt đầu calibration thủ công — người chơi đặt tay cầm yên trên bàn.</summary>
        public void BeginManualCalibration()
        {
            _manualCalibrating = true;
            _manualSum   = Vector3.zero;
            _manualCount = 0;
        }

        /// <summary>Kết thúc calibration thủ công và áp offset trung bình. Trả về <c>false</c> nếu chưa có mẫu nào.</summary>
        public bool EndManualCalibration()
        {
            _manualCalibrating = false;
            if (_manualCount == 0) return false;

            _offset    = _manualSum / _manualCount;
            _hasOffset = true;
            return true;
        }

        /// <summary>Đặt offset trực tiếp (ví dụ khôi phục từ save data).</summary>
        public void SetOffset(Vector3 offset)
        {
            _offset    = offset;
            _hasOffset = true;
        }

        /// <summary>Xoá toàn bộ trạng thái — gọi khi đổi thiết bị.</summary>
        public void Reset()
        {
            _offset            = Vector3.zero;
            _hasOffset         = false;
            _stillTimer        = 0f;
            _calibSum          = Vector3.zero;
            _calibCount        = 0;
            _hasPrevAccel      = false;
            _manualCalibrating = false;
            _up                = Vector3.up;
            _smoothed          = Vector2.zero;
            Rate               = Vector2.zero;
            CalibratedGyro     = Vector3.zero;
            IsStill            = false;
        }

        // ══════════════════════════════════════════════════════
        //  PROCESS
        // ══════════════════════════════════════════════════════

        /// <param name="rawGyro">Gyro thô (độ/giây), trục controller-local.</param>
        /// <param name="accel">Gia tốc (g). Truyền <c>Vector3.zero</c> nếu thiết bị không có accelerometer.</param>
        /// <param name="dt">Unscaled delta time.</param>
        public void Process(Vector3 rawGyro, Vector3 accel, float dt, TISMotionSettings s)
        {
            if (dt <= 0f || s == null) return;

            UpdateGravity(accel, dt);
            UpdateCalibration(rawGyro, accel, dt, s);

            var g = rawGyro - _offset;
            CalibratedGyro = g;

            var rate = ToYawPitch(g, s.gyroSpace);
            rate = ApplyDeadzone(rate, s.gyroDeadzone);
            rate = ApplyTightening(rate, s.tighteningThreshold);
            rate = ApplySoftTierSmoothing(rate, dt, s.smoothingThreshold, s.smoothingTime);
            Rate = rate;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════

        private void UpdateGravity(Vector3 accel, float dt)
        {
            // Không có accelerometer (hoặc rơi tự do) → giữ hướng cũ
            if (accel.sqrMagnitude < 0.01f) return;

            float t = 1f - Mathf.Exp(-dt / GravitySmoothTime);
            _up = Vector3.Lerp(_up, accel.normalized, t).normalized;
        }

        private void UpdateCalibration(Vector3 rawGyro, Vector3 accel, float dt, TISMotionSettings s)
        {
            bool accelStill = !_hasPrevAccel
                              || (accel - _prevAccel).magnitude < s.stillnessAccelThreshold;
            _prevAccel    = accel;
            _hasPrevAccel = true;

            if (_manualCalibrating)
            {
                _manualSum += rawGyro;
                _manualCount++;
                IsStill = false;
                return;
            }

            bool gyroStill = (rawGyro - _offset).magnitude < s.stillnessGyroThreshold;

            if (!s.autoCalibrate || !gyroStill || !accelStill)
            {
                _stillTimer = 0f;
                _calibSum   = Vector3.zero;
                _calibCount = 0;
                IsStill     = false;
                return;
            }

            _stillTimer += dt;
            IsStill = _stillTimer >= s.stillnessTime;
            if (!IsStill) return;

            _calibSum += rawGyro;
            _calibCount++;
            if (_calibCount < CalibrationBatch) return;

            var avg = _calibSum / _calibCount;
            _offset     = _hasOffset ? Vector3.Lerp(_offset, avg, CalibrationBlend) : avg;
            _hasOffset  = true;
            _calibSum   = Vector3.zero;
            _calibCount = 0;
        }

        private Vector2 ToYawPitch(Vector3 g, TISGyroSpace space)
        {
            float yaw;
            if (space == TISGyroSpace.Player)
            {
                // Hình chiếu tốc độ góc lên trục "lên" của thế giới (bỏ trục pitch X)
                float worldYaw = _up.y * g.y + _up.z * g.z;
                float yawMag   = Mathf.Sqrt(g.y * g.y + g.z * g.z);
                yaw = Mathf.Sign(worldYaw) * Mathf.Min(Mathf.Abs(worldYaw) * YawRelaxFactor, yawMag);
            }
            else
            {
                yaw = g.y;
            }

            // gyro.y > 0 = quay trái → đổi dấu để x > 0 = quay phải
            return new Vector2(-yaw, g.x);
        }

        private static Vector2 ApplyDeadzone(Vector2 rate, float deadzone)
        {
            if (deadzone <= 0f) return rate;
            float mag = rate.magnitude;
            if (mag <= deadzone) return Vector2.zero;
            return rate * ((mag - deadzone) / mag);
        }

        private static Vector2 ApplyTightening(Vector2 rate, float threshold)
        {
            if (threshold <= 0f) return rate;
            float mag = rate.magnitude;
            if (mag >= threshold) return rate;
            return rate * (mag / threshold);
        }

        private Vector2 ApplySoftTierSmoothing(Vector2 rate, float dt, float threshold, float smoothTime)
        {
            float a = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, smoothTime));
            _smoothed = Vector2.Lerp(_smoothed, rate, a);

            if (threshold <= 0f) return rate;

            // mag ≤ threshold/2 → hoàn toàn smoothed; mag ≥ threshold → hoàn toàn direct
            float half    = threshold * 0.5f;
            float directW = Mathf.Clamp01((rate.magnitude - half) / half);
            return rate * directW + _smoothed * (1f - directW);
        }
    }
}
