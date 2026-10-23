// ============================================================
//  TrueInputSystem — TIS_MotionSystem.cs
//
//  Trách nhiệm:
//    - Giữ danh sách ITISMotionSource đã đăng ký (simulated, SDL3, console...)
//    - Mỗi frame: tick mọi source, chọn source CONNECTED có Priority cao nhất
//    - Tick TISGyroProcessor, tính GyroAim (độ/giây, đã gate activation / cursor)
//    - Theo dõi trạng thái touchpad: delta, press / release
//
//  TISInputBridge ghi GyroAim / TouchpadVelocity vào thiết bị ảo <TISMotion> mỗi frame
//  → bind trong Setup Tool (gyro / touchpad riêng, hoặc "+ Gyro / + Touchpad" cho stick).
//  TISGamepadVirtualCursor đọc TouchpadDelta / GyroRate để di chuyển con trỏ.
//  Plain class — không phải MonoBehaviour.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    public sealed class TISMotionSystem
    {
        private readonly List<ITISMotionSource> _sources = new List<ITISMotionSource>();
        private ITISMotionSource       _active;
        private readonly TISGyroProcessor _gyro = new TISGyroProcessor();

        private TISMotionSettings      _settings;
        private Func<string, bool>     _isActionHeld;
        private Func<bool>             _isCursorActive;

        // Touchpad tracking
        private bool    _wasTouching;
        private int     _lastTouchId;
        private Vector2 _lastTouchPos;
        private bool    _padPressed;
        private bool    _padPressedPrev;

        /// <summary>Được gọi khi source đang active thay đổi (có thể là <c>null</c>).</summary>
        public event Action<ITISMotionSource> OnActiveSourceChanged;

        // ══════════════════════════════════════════════════════
        //  SETUP
        // ══════════════════════════════════════════════════════

        /// <summary>Cập nhật settings sau khi build/swap profile.</summary>
        /// <param name="isActionHeld">Đọc trạng thái Hold của action (dùng cho gyro activation).</param>
        /// <param name="isCursorActive">Virtual cursor đang bật → gyro không xoay camera.</param>
        public void Load(TISMotionSettings settings, Func<string, bool> isActionHeld, Func<bool> isCursorActive)
        {
            _settings       = settings;
            _isActionHeld   = isActionHeld;
            _isCursorActive = isCursorActive;
        }

        /// <summary>Đăng ký một source. Source CONNECTED có Priority cao nhất sẽ được dùng.</summary>
        public void AddSource(ITISMotionSource source)
        {
            if (source != null && !_sources.Contains(source))
                _sources.Add(source);
        }

        /// <summary>Gỡ một source đã đăng ký.</summary>
        public void RemoveSource(ITISMotionSource source)
        {
            _sources.Remove(source);
            if (ReferenceEquals(_active, source)) SetActive(null);
        }

        // ══════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════

        /// <summary>Source đang được dùng (có thể <c>null</c>).</summary>
        public ITISMotionSource Source => _active;

        /// <summary>Tất cả source đã đăng ký (debug).</summary>
        public IReadOnlyList<ITISMotionSource> Sources => _sources;

        /// <summary>Bộ xử lý gyro — truy cập calibration / offset / debug.</summary>
        public TISGyroProcessor GyroProcessor => _gyro;

        /// <summary>Công tắc tổng gyro (Inspector TISInputBridge). OFF = bỏ qua gyro của mọi source.</summary>
        public bool GyroEnabled     { get; set; } = true;

        /// <summary>Công tắc tổng touchpad (Inspector TISInputBridge). OFF = bỏ qua touchpad của mọi source.</summary>
        public bool TouchpadEnabled { get; set; } = true;

        public bool HasGyro     => GyroEnabled     && _active != null && _active.HasMotion;
        public bool HasTouchpad => TouchpadEnabled && _active != null && _active.HasTouchpad;

        /// <summary>Tốc độ yaw/pitch đã xử lý (độ/giây), KHÔNG phụ thuộc activation. x = phải, y = lên.</summary>
        public Vector2 GyroRate      { get; private set; }

        /// <summary><c>true</c> khi gyro đang được cộng vào LookDelta frame này.</summary>
        public bool    IsGyroAimActive { get; private set; }

        /// <summary>
        /// Tốc độ gyro dùng cho NGẮM (độ/giây): đã áp invert + điều kiện kích hoạt
        /// (Enable Gyro, Activation, tắt khi virtual cursor bật). Đây là giá trị của
        /// control <c>&lt;TISMotion&gt;/gyro</c> — độ nhạy áp ở binding (TISProfileBuilder).
        /// </summary>
        public Vector2 GyroAim       { get; private set; }

        /// <summary>Tốc độ vuốt touchpad (đơn vị chuẩn hoá / giây, x phải, y lên) — control <c>&lt;TISMotion&gt;/touchpad</c>.</summary>
        public Vector2 TouchpadVelocity { get; private set; }

        /// <summary><c>true</c> khi có ngón tay trên touchpad (điểm chạm 0).</summary>
        public bool    IsTouching    { get; private set; }

        /// <summary>Vị trí điểm chạm 0, chuẩn hoá 0..1 (gốc dưới-trái).</summary>
        public Vector2 TouchpadPosition { get; private set; }

        /// <summary>Delta điểm chạm 0 so với frame trước (đơn vị chuẩn hoá). Bằng 0 ở frame ngón tay vừa chạm.</summary>
        public Vector2 TouchpadDelta { get; private set; }

        public bool TouchpadPressed            => _padPressed;
        public bool TouchpadPressedThisFrame   => _padPressed && !_padPressedPrev;
        public bool TouchpadReleasedThisFrame  => !_padPressed && _padPressedPrev;

        // ══════════════════════════════════════════════════════
        //  CALIBRATION (proxy)
        // ══════════════════════════════════════════════════════

        public void BeginGyroCalibration()     => _gyro.BeginManualCalibration();
        public bool EndGyroCalibration()       => _gyro.EndManualCalibration();

        // ══════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════

        /// <summary>Gọi 1 lần mỗi frame từ TISInputBridge.Update().</summary>
        public void Tick(float unscaledDeltaTime)
        {
            _padPressedPrev = _padPressed;
            _padPressed     = false;
            ResetOutputs();

            SetActive(TickSourcesAndPickBest(unscaledDeltaTime));
            if (_active == null || _settings == null) return;

            if (HasGyro)
                TickGyro(unscaledDeltaTime);

            if (HasTouchpad)
            {
                TickTouchpad();
                // Delta / frame → tốc độ / giây: binding không phụ thuộc FPS
                if (unscaledDeltaTime > 0f)
                    TouchpadVelocity = TouchpadDelta / unscaledDeltaTime;
            }
            else
                _wasTouching = false;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════

        private ITISMotionSource TickSourcesAndPickBest(float dt)
        {
            ITISMotionSource best = null;

            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];

                // Provider MonoBehaviour bị Destroy → interface vẫn non-null nhưng object đã chết
                if (s == null || (s is UnityEngine.Object o && o == null))
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                // Tick mọi source để chúng tự phát hiện cắm / rút thiết bị
                s.Tick(dt);

                if (s.IsConnected && (best == null || s.Priority > best.Priority))
                    best = s;
            }
            return best;
        }

        private void SetActive(ITISMotionSource source)
        {
            if (ReferenceEquals(_active, source)) return;

            _active = source;
            _gyro.Reset();
            ResetOutputs();
            _wasTouching = false;
            _padPressed = _padPressedPrev = false;
            OnActiveSourceChanged?.Invoke(source);
        }

        private void ResetOutputs()
        {
            GyroRate         = Vector2.zero;
            GyroAim          = Vector2.zero;
            TouchpadVelocity = Vector2.zero;
            IsGyroAimActive  = false;
            TouchpadDelta    = Vector2.zero;
            IsTouching       = false;
        }

        private void TickGyro(float dt)
        {
            _gyro.Process(_active.Gyro, _active.Accel, dt, _settings);
            GyroRate = _gyro.Rate;

            IsGyroAimActive = _settings.enableGyro
                              && !(_isCursorActive?.Invoke() ?? false)
                              && IsActivationSatisfied();

            if (!IsGyroAimActive) return;

            GyroAim = new Vector2(
                GyroRate.x * (_settings.invertGyroX ? -1f : 1f),
                GyroRate.y * (_settings.invertGyroY ? -1f : 1f));
        }

        private bool IsActivationSatisfied()
        {
            switch (_settings.gyroActivation)
            {
                case TISGyroActivation.WhileActionHeld:
                    return IsActivationActionHeld();
                case TISGyroActivation.DisabledWhileActionHeld:
                    return !IsActivationActionHeld();
                default:
                    return true;
            }
        }

        private bool IsActivationActionHeld()
        {
            if (string.IsNullOrEmpty(_settings.gyroActivationAction) || _isActionHeld == null)
                return false;
            return _isActionHeld(_settings.gyroActivationAction);
        }

        private void TickTouchpad()
        {
            _padPressed = _active.TouchpadPressed;

            if (!_active.TryGetTouch(0, out var touch) || !touch.active)
            {
                _wasTouching = false;
                return;
            }

            IsTouching       = true;
            TouchpadPosition = touch.position;

            // Chỉ tính delta khi cùng một ngón tay đang kéo — tránh nhảy cursor khi chạm lại
            if (_wasTouching && touch.id == _lastTouchId)
                TouchpadDelta = touch.position - _lastTouchPos;

            _wasTouching  = true;
            _lastTouchId  = touch.id;
            _lastTouchPos = touch.position;
        }
    }
}
