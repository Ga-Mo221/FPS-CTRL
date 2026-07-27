using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class GunRecoilController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  ENUMS & CONFIG
    // ─────────────────────────────────────────────

    public enum FiringMode { Semi, Auto, Burst }

    // ─────────────────────────────────────────────
    //  EVENTS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Phát mỗi khi một viên đạn thực sự được bắn ra (Semi, Auto, hoặc từng phát trong Burst).
    /// Parameter: FiringMode của phát bắn đó.
    /// </summary>
    public event Action<FiringMode> OnShotFired;

    [Header("Data")]
    [SerializeField] private GunRecoilData data;

    [Header("State")]
    [SerializeField] private FiringMode firingMode = FiringMode.Semi;
    [SerializeField] private int burstCount = 3;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    // Rotation recoil
    private Vector3 _currentRot;
    private Vector3 _targetRot;

    // Location recoil
    private Vector3 _currentLoc;
    private Vector3 _targetLoc;

    // Noise (spring-damper)
    private Vector3 _noiseVelocity;
    private Vector3 _noisePos;
    private float   _lastFireTime;      // dùng để fade noise khi idle

    // Pushback (spring-damper)
    private float _pushCurrent;
    private float _pushVelocity;

    // Curve timers
    private float _semiTimer;
    private float _autoTimer;
    private bool  _semiActive;
    private bool  _autoActive;

    // Aiming
    private bool _isAiming;

    // Burst
    private int   _burstRemaining;
    private float _burstFireRate;
    private float _burstNextShotTime;

    // Cooldown
    private float _nextFireTime;

    // FIX: toàn bộ timing quyết định "có bắn hay không" (cooldown + burst continuation)
    // giờ chạy theo _time (do WeaponController cấp qua Tick()/OnFire(), lấy từ
    // Runner.SimulationTime) thay vì Time.time. Time.time không tua lại được khi Fusion
    // resimulate, khiến client dự đoán và server tính cooldown lệch nhau. _time
    // đại diện cho thời điểm tick hiện tại, xác định (deterministic) theo simulation.
    private float _time;

    // Roll smooth
    private float _currentRollZ;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (data == null)
        {
            //Debug.LogError("[GunRecoilController] GunRecoilData chưa được assign!", this);
            //enabled = false;
            return;
        }

        if (!data.Validate(out string error))
            Debug.LogError($"[GunRecoilController] GunRecoilData invalid: {error}", this);

        // Tự động chọn mode mặc định theo data
        var defaultMode = ToFiringMode(data.FirstAllowedMode());
        if (!IsModeAllowed(firingMode))
        {
            Debug.LogWarning($"[GunRecoilController] Mode '{firingMode}' không được phép " +
                             $"trên data này. Chuyển về '{defaultMode}'.");
            firingMode = defaultMode;
        }
    }

    private void LateUpdate()
    {
        if (data == null) return;

        // FIX: HandleBurstTick() ĐÃ CHUYỂN sang NetworkTick() bên dưới — không còn gọi
        // ở đây nữa. LateUpdate giờ chỉ lo phần recoil visual (curve/noise/pushback/transform),
        // các phần này thuần cosmetic (không quyết định có bắn hay không, không networked),
        // nên vẫn an toàn khi chạy theo Time.deltaTime của Unity như cũ.
        TickCurveTimers();
        UpdateNoise();
        UpdatePushback();
        ApplyTransform();
    }

    /// <summary>
    /// FIX: gọi hàm này mỗi network tick từ FixedUpdateNetwork (qua WeaponController),
    /// bất kể người chơi có đang bấm nút bắn hay không. Đây là nơi DUY NHẤT quyết định
    /// burst có tiếp tục bắn phát kế tiếp hay không — thay cho HandleBurstTick() dựa vào
    /// Time.time trong LateUpdate trước đây (chạy ngoài simulation, không resimulate được,
    /// và không đồng bộ được với WeaponController.FireTick).
    /// </summary>
    public void NetworkTick(float networkTime)
    {
        _time = networkTime;
        HandleBurstTick();
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Gọi mỗi khi người chơi nhấn/giữ nút bắn.
    /// Controller tự xử lý cooldown và chặn spam — WeaponController không cần lo timing.
    /// Trả về true nếu phát bắn thực sự được thực hiện.
    /// </summary>
    /// <summary>
    /// FIX: nhận networkTime từ caller (WeaponController, lấy từ Runner.SimulationTime)
    /// thay vì tự đọc Time.time. Đảm bảo quyết định "có bắn được không" xác định theo
    /// đúng tick mô phỏng — an toàn khi Fusion resimulate/rollback, và khớp với thời điểm
    /// server thực sự tính toán, tránh lệch giữa client dự đoán và server.
    /// </summary>
    public bool OnFire()
    {
        _time = Time.time;

        if (Time.time < _nextFireTime) return false;

        switch (firingMode)
        {
            case FiringMode.Semi:
                FireSemi();
                _nextFireTime = Time.time + data.FireDelay;
                break;

            case FiringMode.Auto:
                FireAuto(data.FireDelay);
                _nextFireTime = Time.time + data.FireDelay;
                break;

            case FiringMode.Burst:
                TriggerBurst(data.FireDelay);
                // Cooldown = thời gian hoàn thành toàn bộ loạt
                _nextFireTime = Time.time + burstCount * data.FireDelay;
                break;
        }

        return true;
    }

    /// <summary>Cập nhật trạng thái ADS.</summary>
    public void SetAiming(bool aiming) => _isAiming = aiming;

    /// <summary>
    /// Đổi firing mode — từ chối nếu mode không được phép trong GunRecoilData.
    /// Trả về true nếu đổi thành công.
    /// </summary>
    public bool SetFiringMode(FiringMode mode)
    {
        if (!IsModeAllowed(mode))
        {
            Debug.LogWarning($"[GunRecoilController] Mode '{mode}' không được phép. " +
                             $"Allowed: {data.allowedModes}");
            return false;
        }
        firingMode = mode;
        return true;
    }
    
    public FiringMode GetFiringMode() => firingMode;

    /// <summary>Đặt số phát trong một loạt burst (tối thiểu 1).</summary>
    public void SetBurstCount(int count) => burstCount = Mathf.Max(1, count);

    /// <summary>
    /// Đổi súng giữa trận — gán GunRecoilData mới, reset toàn bộ recoil state,
    /// sau đó load lại firingMode và burstCount từ data mới.
    ///
    /// Quy tắc chọn firingMode sau khi reload:
    ///   1. Nếu <paramref name="preferredMode"/> được truyền vào và được data mới cho phép → dùng nó.
    ///   2. Nếu mode hiện tại vẫn hợp lệ trên data mới → giữ nguyên.
    ///   3. Ngược lại → chuyển về FirstAllowedMode() của data mới.
    ///
    /// burstCount luôn được reset về data.BurstCount của súng mới.
    ///
    /// Trả về false (và không thay đổi gì) nếu newData null hoặc không pass Validate().
    /// </summary>
    /// <param name="newData">GunRecoilData của khẩu súng mới.</param>
    /// <param name="preferredMode">
    ///     Mode muốn dùng sau khi đổi súng. Truyền null để giữ mode hiện tại nếu hợp lệ,
    ///     hoặc tự động chọn mode đầu tiên hợp lệ.
    /// </param>
    public bool ReloadData(GunRecoilData newData, FiringMode? preferredMode = null)
    {
        // ── 1. Validate ──────────────────────────────────────────────
        if (newData == null)
        {
            Debug.LogError("[GunRecoilController] ReloadData: newData là null — bỏ qua.", this);
            return false;
        }

        if (!newData.Validate(out string error))
        {
            Debug.LogError($"[GunRecoilController] ReloadData: GunRecoilData '{newData.name}' " +
                           $"không hợp lệ: {error}", this);
            return false;
        }

        return ApplyNewData(newData, preferredMode);
    }

    /// <summary>
    /// Overload tiện lợi — đổi súng trực tiếp bằng WeaponData thay vì phải lôi riêng
    /// GunRecoilData ra ngoài. Controller tự đọc weapon.RecoilData bên trong.
    /// Trả về false nếu weapon null, thiếu RecoilData, hoặc RecoilData không hợp lệ.
    /// </summary>
    /// <param name="weapon">WeaponData của khẩu súng mới (chứa RecoilData bên trong).</param>
    /// <param name="preferredMode">Xem tài liệu của overload ReloadData(GunRecoilData, ...).</param>
    public bool ReloadData(WeaponData weapon, FiringMode? preferredMode = null)
    {
        if (weapon == null)
        {
            Debug.LogError("[GunRecoilController] ReloadData: WeaponData là null — bỏ qua.", this);
            return false;
        }

        if (!weapon.HasRecoilData)
        {
            Debug.LogError($"[GunRecoilController] ReloadData: WeaponData '{weapon.name}' " +
                           $"chưa được gán RecoilData.", this);
            return false;
        }

        return ReloadData(weapon.RecoilData, preferredMode);
    }

    /// <summary>
    /// Phần logic gán data + reset state dùng chung cho cả 2 overload ReloadData.
    /// LƯU Ý: newData được giả định là đã pass Validate() ở caller (ReloadData(GunRecoilData, ...)).
    /// </summary>
    private bool ApplyNewData(GunRecoilData newData, FiringMode? preferredMode)
    {
        // ── 2. Gán data mới ──────────────────────────────────────────
        data = newData;

        // ── 3. Reset toàn bộ recoil state ────────────────────────────
        _currentRot      = Vector3.zero;
        _targetRot       = Vector3.zero;
        _currentLoc      = Vector3.zero;
        _targetLoc       = Vector3.zero;
        _noiseVelocity   = Vector3.zero;
        _noisePos        = Vector3.zero;
        _pushCurrent     = 0f;
        _pushVelocity    = 0f;
        _semiTimer       = 0f;
        _autoTimer       = 0f;
        _semiActive      = false;
        _autoActive      = false;
        _burstRemaining  = 0;
        _burstNextShotTime = 0f;
        _nextFireTime    = 0f;
        _lastFireTime    = -999f;   // đảm bảo noise fade ngay lập tức
        _currentRollZ    = 0f;

        // ── 4. Load lại firingMode từ allowedModes của data mới ──────
        //   Ưu tiên: preferredMode → mode hiện tại → FirstAllowedMode()
        FiringMode resolvedMode;
        if (preferredMode.HasValue && IsModeAllowed(preferredMode.Value))
        {
            resolvedMode = preferredMode.Value;
        }
        else if (IsModeAllowed(firingMode))
        {
            resolvedMode = firingMode;  // mode cũ vẫn hợp lệ → giữ nguyên
        }
        else
        {
            resolvedMode = ToFiringMode(data.FirstAllowedMode());
            Debug.LogWarning($"[GunRecoilController] ReloadData: mode '{firingMode}' không được phép " +
                             $"trên '{data.name}'. Chuyển về '{resolvedMode}'.");
        }
        firingMode = resolvedMode;

        // ── 5. Load lại burstCount từ data mới ───────────────────────
        burstCount = Mathf.Max(1, data.BurstCount);

        return true;
    }

    /// <summary>Kiểm tra mode có được phép không (dùng để UI ẩn/hiện nút chuyển mode).</summary>
    public bool IsModeAllowed(FiringMode mode) =>
        data != null && data.IsModeAllowed(ToAllowedModes(mode));

    // ─────────────────────────────────────────────
    //  MODE CONVERSION HELPERS
    // ─────────────────────────────────────────────

    private static GunRecoilData.AllowedModes ToAllowedModes(FiringMode mode) => mode switch
    {
        FiringMode.Semi  => GunRecoilData.AllowedModes.Semi,
        FiringMode.Auto  => GunRecoilData.AllowedModes.Auto,
        FiringMode.Burst => GunRecoilData.AllowedModes.Burst,
        _                => GunRecoilData.AllowedModes.Semi,
    };

    private static FiringMode ToFiringMode(GunRecoilData.AllowedModes mode) => mode switch
    {
        GunRecoilData.AllowedModes.Auto  => FiringMode.Auto,
        GunRecoilData.AllowedModes.Burst => FiringMode.Burst,
        _                                => FiringMode.Semi,
    };

    // ─────────────────────────────────────────────
    //  FIRE LOGIC
    // ─────────────────────────────────────────────

    private void FireSemi()
    {
        _lastFireTime = Time.time;

        ApplyRotationKick(isAuto: false);
        ApplyTranslationKick(isAuto: false);
        ApplyPushKick();

        _semiTimer  = 0f;
        _semiActive = true;
        _autoActive = false;        // dừng auto curve nếu đang chạy
        _autoTimer  = 0f;

        OnShotFired?.Invoke(FiringMode.Semi);
    }

    private void FireAuto(float fireRateDelay)
    {
        _lastFireTime = Time.time;

        ApplyRotationKick(isAuto: true);
        ApplyTranslationKick(isAuto: true);
        ApplyPushKick();

        // FIX: reset timer về 0 mỗi phát, giống Semi — tránh timer trôi dạt rơi vào
        // đoạn giữa curve (có thể là đoạn âm) một cách khó đoán trước.
        // Vì _targetRot vẫn cộng dồn qua ApplyRotationKick() phía trên, việc curve chạy
        // lại từ đầu mỗi phát vẫn tạo hiệu ứng giật chồng lên nhau khi bắn liên tục.
        _autoTimer  = 0f;
        _autoActive = true;
        _semiActive = false;
        _semiTimer  = 0f;

        OnShotFired?.Invoke(FiringMode.Auto);
    }

    private void TriggerBurst(float fireRateDelay)
    {
        if (_burstRemaining > 0) return; // burst đang chạy, bỏ qua trigger mới
        _burstRemaining    = burstCount;
        _burstFireRate     = fireRateDelay;
        _burstNextShotTime = _time;
    }

    // FIX: giờ được gọi từ NetworkTick() (mỗi network tick, kể cả khi không bấm nút),
    // dùng _time thay vì Time.time — đảm bảo phát 2, 3 của burst cũng được kích
    // hoạt đúng trong FixedUpdateNetwork, để WeaponController có thể nghe OnShotFired
    // và tăng FireTick/raycast một cách an toàn (network-safe) cho từng phát trong loạt.
    private void HandleBurstTick()
    {
        if (_burstRemaining <= 0) return;
        if (_time < _burstNextShotTime) return;

        // Burst dùng FireBurstShot riêng — không động vào _autoActive
        FireBurstShot();
        _burstRemaining--;
        _burstNextShotTime = _time + _burstFireRate;
    }

    /// <summary>
    /// Bắn một phát trong burst — dùng semi curve nhưng
    /// không ảnh hưởng đến _autoActive flag.
    /// </summary>
    private void FireBurstShot()
    {
        _lastFireTime = Time.time;

        ApplyRotationKick(isAuto: false);
        ApplyTranslationKick(isAuto: false);
        ApplyPushKick();

        _semiTimer  = 0f;
        _semiActive = true;
        // _autoActive giữ nguyên trạng thái hiện tại

        OnShotFired?.Invoke(FiringMode.Burst);
    }

    // ─────────────────────────────────────────────
    //  KICK HELPERS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Tính kick rotation và cộng vào _targetRot.
    /// isAuto = true → dùng extraRot multiplier.
    /// </summary>
    private void ApplyRotationKick(bool isAuto)
    {
        Vector3 aimMult = _isAiming ? data.aimRotMultiplier : Vector3.one;

        // Pitch: đơn giản random trong [min, max]
        float pitchVal = Random.Range(data.pitch.x, data.pitch.y) * aimMult.x;

        // Roll: random min trong [roll.x, roll.y], random max trong [roll.z, roll.w]
        // FIX: trước đây bỏ qua roll.y và roll.w
        float rollMin = Random.Range(data.roll.x, data.roll.y);
        float rollMax = Random.Range(data.roll.z, data.roll.w);
        float rollBase = Random.Range(rollMin, rollMax);
        float rollVal  = rollBase * (isAuto ? data.extraRot.x : 1f) * aimMult.y;

        // Yaw: tương tự roll
        // FIX: trước đây bỏ qua yaw.y và yaw.w
        float yawMin  = Random.Range(data.yaw.x, data.yaw.y);
        float yawMax  = Random.Range(data.yaw.z, data.yaw.w);
        float yawBase = Random.Range(yawMin, yawMax);
        float yawVal  = yawBase * (isAuto ? data.extraRot.z : 1f) * aimMult.z;

        _targetRot += new Vector3(pitchVal, yawVal, rollVal);
    }

    /// <summary>
    /// Tính kick translation và cộng vào _targetLoc.
    /// isAuto = true → dùng extraLoc multiplier.
    /// FIX: extraLoc giờ được sử dụng (trước đây khai báo nhưng bỏ qua).
    /// </summary>
    private void ApplyTranslationKick(bool isAuto)
    {
        Vector3 aimMult  = _isAiming ? data.aimLocMultiplier : Vector3.one;
        Vector3 extraMult = isAuto ? data.extraLoc : Vector3.one;

        float kbVal = Random.Range(data.kickback.x, data.kickback.y) * extraMult.z * aimMult.z;
        float kuVal = Random.Range(data.kickUp.x,   data.kickUp.y)   * extraMult.y * aimMult.y;
        float krVal = Random.Range(data.kickRight.x, data.kickRight.y) * extraMult.x * aimMult.x;

        _targetLoc += new Vector3(krVal, kuVal, kbVal);
    }

    private void ApplyPushKick()
    {
        _pushVelocity += data.pushAmount;
    }

    // ─────────────────────────────────────────────
    //  TICK — CURVES
    // FIX: curve timers có điều kiện dừng khi vượt duration
    // FIX: semi dùng smoothRot.x, auto dùng smoothRot.y riêng biệt
    // ─────────────────────────────────────────────

    private void TickCurveTimers()
    {
        float dt = Time.deltaTime * data.playRate;

        // ── SEMI ──
        if (_semiActive)
        {
            _semiTimer += dt;

            float t = _semiTimer;
            Vector3 rotScale = new Vector3(
                data.semiRotX.Evaluate(t),
                data.semiRotY.Evaluate(t),
                data.semiRotZ.Evaluate(t));

            _currentRot = new Vector3(
                Mathf.Lerp(_currentRot.x, _targetRot.x, rotScale.x),
                Mathf.Lerp(_currentRot.y, _targetRot.y, rotScale.y),
                Mathf.Lerp(_currentRot.z, _targetRot.z, rotScale.z));

            Vector3 locScale = new Vector3(
                data.semiLocX.Evaluate(t),
                data.semiLocY.Evaluate(t),
                data.semiLocZ.Evaluate(t));

            _currentLoc = new Vector3(
                Mathf.Lerp(_currentLoc.x, _targetLoc.x, locScale.x),
                Mathf.Lerp(_currentLoc.y, _targetLoc.y, locScale.y),
                Mathf.Lerp(_currentLoc.z, _targetLoc.z, locScale.z));

            // FIX: semi decay dùng smoothRot.x
            _targetRot = Vector3.Lerp(_targetRot, Vector3.zero, dt * data.smoothRot.x);
            _targetLoc = Vector3.Lerp(_targetLoc, Vector3.zero, dt * data.smoothLoc.x);

            // FIX: tắt semi khi curve kết thúc — KHÔNG snap cứng, để block decay
            // (phía dưới, khi !_semiActive && !_autoActive) xử lý mượt theo Time.deltaTime.
            if (_semiTimer >= data.SemiCurveDuration)
            {
                _semiActive = false;
                _semiTimer  = 0f;
            }
        }

        // ── AUTO ──
        if (_autoActive)
        {
            _autoTimer += dt;

            float t = _autoTimer;
            Vector3 rotScale = new Vector3(
                data.autoRotX.Evaluate(t),
                data.autoRotY.Evaluate(t),
                data.autoRotZ.Evaluate(t));

            _currentRot = new Vector3(
                Mathf.Lerp(_currentRot.x, _targetRot.x, rotScale.x),
                Mathf.Lerp(_currentRot.y, _targetRot.y, rotScale.y),
                Mathf.Lerp(_currentRot.z, _targetRot.z, rotScale.z));

            Vector3 locScale = new Vector3(
                data.autoLocX.Evaluate(t),
                data.autoLocY.Evaluate(t),
                data.autoLocZ.Evaluate(t));

            _currentLoc = new Vector3(
                Mathf.Lerp(_currentLoc.x, _targetLoc.x, locScale.x),
                Mathf.Lerp(_currentLoc.y, _targetLoc.y, locScale.y),
                Mathf.Lerp(_currentLoc.z, _targetLoc.z, locScale.z));

            // FIX: auto decay dùng smoothRot.y (riêng biệt với semi)
            _targetRot = Vector3.Lerp(_targetRot, Vector3.zero, dt * data.smoothRot.y);
            _targetLoc = Vector3.Lerp(_targetLoc, Vector3.zero, dt * data.smoothLoc.y);

            // FIX: tắt auto khi curve kết thúc — KHÔNG snap cứng (đây từng là nguồn gây
            // giật mạnh ngược chiều: Lerp(0.5f) không nhân Time.deltaTime nên nhảy tức thời
            // 50% trong đúng 1 frame). Để block decay phía dưới xử lý mượt theo Time.deltaTime.
            if (_autoTimer >= data.AutoCurveDuration)
            {
                _autoActive = false;
                _autoTimer  = 0f;
            }
        }

        // FIX: decay _currentRot/_currentLoc về 0 khi không còn curve nào active
        if (!_semiActive && !_autoActive)
        {
            float decaySpeed = data.smoothRot.x * 0.5f;
            _currentRot = Vector3.Lerp(_currentRot, Vector3.zero, Time.deltaTime * decaySpeed);
            _currentLoc = Vector3.Lerp(_currentLoc, Vector3.zero, Time.deltaTime * decaySpeed);
            _targetRot  = Vector3.Lerp(_targetRot,  Vector3.zero, Time.deltaTime * decaySpeed);
            _targetLoc  = Vector3.Lerp(_targetLoc,  Vector3.zero, Time.deltaTime * decaySpeed);
        }
    }

    // ─────────────────────────────────────────────
    //  TICK — NOISE
    // FIX: noise fade ra 0 khi idle (sau idleNoiseDecayTime giây)
    // ─────────────────────────────────────────────

    private void UpdateNoise()
    {
        float dt = Time.deltaTime;

        // Scale noise về 0 khi không bắn
        float timeSinceFire  = Time.time - _lastFireTime;
        float noiseIntensity = data.noiseScalar *
            Mathf.Clamp01(1f - timeSinceFire / Mathf.Max(data.idleNoiseDecayTime, 0.001f));

        // Spring-damper X
        float targetNX = Random.Range(data.noiseX.x, data.noiseX.y) * noiseIntensity;
        _noiseVelocity.x += (targetNX - _noisePos.x) * data.noiseAccel.x * dt;
        _noiseVelocity.x -= _noiseVelocity.x * data.noiseDamp.x * dt;
        _noisePos.x      += _noiseVelocity.x * dt;

        // Spring-damper Y
        float targetNY = Random.Range(data.noiseY.x, data.noiseY.y) * noiseIntensity;
        _noiseVelocity.y += (targetNY - _noisePos.y) * data.noiseAccel.y * dt;
        _noiseVelocity.y -= _noiseVelocity.y * data.noiseDamp.y * dt;
        _noisePos.y      += _noiseVelocity.y * dt;
    }

    // ─────────────────────────────────────────────
    //  TICK — PUSHBACK
    // ─────────────────────────────────────────────

    private void UpdatePushback()
    {
        float dt = Time.deltaTime;
        _pushVelocity += (0f - _pushCurrent) * data.pushAccel * dt;
        _pushVelocity -= _pushVelocity * data.pushDamp * dt;
        _pushCurrent  += _pushVelocity * dt;
    }

    // ─────────────────────────────────────────────
    //  APPLY FINAL TRANSFORM
    // ─────────────────────────────────────────────

    private void ApplyTransform()
    {
        // Roll smoothing
        float targetRollZ = _currentRot.z;
        if (data.smoothRollEnabled)
            _currentRollZ = Mathf.Lerp(_currentRollZ, targetRollZ, Time.deltaTime * data.smoothRot.z);
        else
            _currentRollZ = targetRollZ;

        // Final rotation = curve recoil + noise offset
        Vector3 finalRot = new Vector3(
            _currentRot.x + _noisePos.y,
            _currentRot.y + _noisePos.x,
            _currentRollZ);

        // Final position = curve loc + pushback (Z axis)
        Vector3 finalPos = _currentLoc + new Vector3(0f, 0f, _pushCurrent);
        
        transform.localRotation = Quaternion.Euler(finalRot);
        transform.localPosition = finalPos;
    }

    // ─────────────────────────────────────────────
    //  DEBUG GIZMOS
    // ─────────────────────────────────────────────

#if UNITY_EDITOR
    // private void OnDrawGizmosSelected()
    // {
    //     UnityEditor.Handles.color = Color.yellow;
    //     UnityEditor.Handles.Label(
    //         transform.position + Vector3.up * 0.12f,
    //         $"Mode: {firingMode}  Aiming: {_isAiming}\n" +
    //         $"Rot:  {_currentRot:F2}\n" +
    //         $"Loc:  {_currentLoc:F3}\n" +
    //         $"Push: {_pushCurrent:F3}\n" +
    //         $"Noise:{_noisePos:F3}\n" +
    //         $"Burst remaining: {_burstRemaining}");
    // }
#endif
}