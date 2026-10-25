// ============================================================
//  TrueInputSystem — TIS_SimulatedMotionSource.cs
//
//  Provider giả lập gyro + touchpad bằng bàn phím / chuột để test
//  gyro aim và touchpad cursor khi chưa có tay cầm.
//
//  KHÔNG cần add component: TISInputBridge tự tạo provider này khi bật
//  "Simulate Motion In Editor" trong Inspector (mục Motion).
//
//  Điều khiển mặc định (đổi được trong Inspector của TISInputBridge):
//    Gyro   : I / K = pitch lên / xuống, J / L = yaw trái / phải, U / O = roll
//             giữ Right Shift = nhanh hơn
//    Touch  : giữ T = đặt ngón tay lên touchpad (vị trí = vị trí chuột trên màn hình)
//             G     = nhấn (click) touchpad
//    Drift / noise: dùng để test auto-calibration.
//
//  Priority = 0: nếu có tay cầm thật (SDL3 / console) đang kết nối thì
//  provider đó được dùng, provider giả lập tự nhường.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    [Serializable]
    public class TISSimulatedMotionSource : ITISMotionSource
    {
        [Tooltip("Giả lập gyro bằng phím.")]
        public bool simulateGyro     = true;
        [Tooltip("Giả lập touchpad bằng chuột (giữ Touch Key).")]
        public bool simulateTouchpad = true;

        [Header("Gyro Keys")]
        public Key pitchUpKey   = Key.I;
        public Key pitchDownKey = Key.K;
        public Key yawLeftKey   = Key.J;
        public Key yawRightKey  = Key.L;
        public Key rollLeftKey  = Key.U;
        public Key rollRightKey = Key.O;
        public Key fastKey      = Key.RightShift;

        [Tooltip("Tốc độ góc (độ/giây) khi giữ phím.")]
        public float turnRate = 60f;

        [Tooltip("Hệ số nhân khi giữ Fast Key.")]
        public float fastMultiplier = 4f;

        [Tooltip("Thời gian (giây) để tốc độ tăng / giảm — mô phỏng tay người thay vì bước nhảy.")]
        public float rampTime = 0.08f;

        [Header("Sensor Imperfections")]
        [Tooltip("Drift cố định (độ/giây) — dùng để test auto / manual calibration.")]
        public Vector3 drift = new Vector3(0.6f, -0.4f, 0.2f);

        [Tooltip("Biên độ nhiễu ngẫu nhiên (độ/giây).")]
        public float noise = 0.3f;

        [Tooltip("Gia tốc khi tay cầm đứng yên (g). (0,1,0) = nằm ngang, (0,0,1) = dựng đứng.")]
        public Vector3 restingAccel = new Vector3(0f, 1f, 0f);

        [Header("Touchpad Keys")]
        public Key touchKey = Key.T;
        public Key clickKey = Key.G;

        [NonSerialized] private Vector3       _angularVelocity;
        [NonSerialized] private Vector3       _gyro;
        [NonSerialized] private TISTouchPoint _touch;
        [NonSerialized] private int           _nextTouchId;
        [NonSerialized] private bool          _touchPressed;

        // ══════════════════════════════════════════════════════
        //  ITISMotionSource
        // ══════════════════════════════════════════════════════

        public string  Name            => "Simulated (Keyboard/Mouse)";
        public int     Priority        => 0;   // thấp nhất — tay cầm thật luôn được ưu tiên
        public bool    IsConnected     => true;
        public bool    HasMotion       => simulateGyro;
        public bool    HasTouchpad     => simulateTouchpad;
        public int     MaxTouchPoints  => 1;
        public Vector3 Gyro            => _gyro;
        public Vector3 Accel           => restingAccel;
        public bool    TouchpadPressed => _touchPressed;

        public bool TryGetTouch(int index, out TISTouchPoint point)
        {
            point = index == 0 ? _touch : default;
            return index == 0;
        }

        public void Tick(float dt)
        {
            var kb = Keyboard.current;
            TickGyro(kb, dt);
            TickTouchpad(kb);
        }

        /// <summary>Xoá trạng thái runtime — gọi khi bật lại provider.</summary>
        public void ResetState()
        {
            _angularVelocity = Vector3.zero;
            _gyro            = Vector3.zero;
            _touch           = default;
            _touchPressed    = false;
        }

        // ══════════════════════════════════════════════════════
        //  SIMULATION
        // ══════════════════════════════════════════════════════

        private void TickGyro(Keyboard kb, float dt)
        {
            var target = Vector3.zero;
            if (kb != null)
            {
                target.x = Axis(kb, pitchUpKey,  pitchDownKey);
                target.y = Axis(kb, yawLeftKey,  yawRightKey);   // gyro.y > 0 = quay trái
                target.z = Axis(kb, rollLeftKey, rollRightKey);  // gyro.z > 0 = nghiêng trái

                float speed = turnRate * (IsPressed(kb, fastKey) ? fastMultiplier : 1f);
                target *= speed;
            }

            float t = rampTime <= 0f ? 1f : 1f - Mathf.Exp(-dt / rampTime);
            _angularVelocity = Vector3.Lerp(_angularVelocity, target, t);

            var n = noise > 0f ? UnityEngine.Random.insideUnitSphere * noise : Vector3.zero;
            _gyro = _angularVelocity + drift + n;
        }

        private void TickTouchpad(Keyboard kb)
        {
            _touchPressed = kb != null && IsPressed(kb, clickKey);

            // KHÔNG dùng Mouse.current: nếu là chuột ảo của virtual cursor, touch sẽ đọc lại
            // vị trí cursor → delta đẩy cursor → vòng lặp phản hồi làm cursor nhảy loạn.
            var mouse = TISPhysicalMouse.Current;
            bool touching = kb != null && IsPressed(kb, touchKey) && mouse != null;
            if (!touching)
            {
                _touch.active = false;
                return;
            }

            if (!_touch.active)
                _touch.id = ++_nextTouchId;

            var mp = mouse.position.ReadValue();
            _touch.active   = true;
            _touch.position = new Vector2(
                Mathf.Clamp01(mp.x / Mathf.Max(1, Screen.width)),
                Mathf.Clamp01(mp.y / Mathf.Max(1, Screen.height)));
        }

        private static float Axis(Keyboard kb, Key positive, Key negative)
            => (IsPressed(kb, positive) ? 1f : 0f) - (IsPressed(kb, negative) ? 1f : 0f);

        private static bool IsPressed(Keyboard kb, Key key)
            => key != Key.None && kb[key].isPressed;
    }
}
