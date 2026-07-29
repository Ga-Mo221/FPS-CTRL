using TrueInputSystem;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Camera Recoil — rung/lắc camera chính mỗi phát bắn.
///
/// Layer riêng biệt với GunRecoilController (layer đó điều khiển weapon model).
/// Cả hai cùng chạy song song và đọc chung 1 GunRecoilData, đảm bảo mỗi khẩu súng
/// chỉ cần author giá trị recoil ở MỘT nơi (asset GunRecoilData / WeaponData.RecoilData).
///
/// Gắn lên PlayerCamera (hoặc object cha của camera). Gọi:
///   - LoadData(GunRecoilData) / LoadData(WeaponData)  khi đổi súng
///   - SetFiringMode(FiringMode)                        khi đổi chế độ bắn (đồng bộ với GunRecoilController)
///   - OnFire()  từ weapon logic khi bắn Semi/Auto (1 lần/phát)
///              hoặc khi Burst: gọi 1 lần duy nhất — CameraRecoil tự bắn đủ burstCount phát bên trong
///
/// FireDelay và BurstCount đọc thẳng từ GunRecoilData — không cần truyền riêng.
/// </summary>
public class CameraRecoil : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────

    [Header("Data")]
    [Tooltip("Nguồn dữ liệu recoil — đọc từ section 'Camera Shake' của GunRecoilData.\n" +
             "Gán qua LoadData() khi đổi súng để camera luôn khớp với khẩu súng đang cầm.")]
    [SerializeField] private GunRecoilData data;
    [SerializeField] private WeaponData _weaponData;

    [Header("Camera Controller")]
    [SerializeField] private Transform camera_CTRL;

    [Header("State")]
    [SerializeField] private GunRecoilController.FiringMode firingMode = GunRecoilController.FiringMode.Semi;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private Vector3 currentRotation;
    private Vector3 targetRotation;

    private bool  _isAiming;

    // Semi / Auto cooldown
    private float _nextFireTime;

    // Burst
    private int   _burstRemaining;
    private float _burstNextShotTime;
    private float _burstFireRate;
    
    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _weaponData = GetComponent<WeaponController>().Data;
        if (data != null)
            ApplyDataToFields();
        
    }

    private void Update()
    {
        if (camera_CTRL == null) return;

        HandleBurstTick();

        targetRotation  = Vector3.Lerp(targetRotation,  Vector3.zero, data.camReturnSpeed * Time.deltaTime);
        currentRotation = Vector3.Slerp(currentRotation, targetRotation, data.camSnappiness * Time.deltaTime);
        camera_CTRL.localRotation = Quaternion.Euler(currentRotation);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    public void SetCameraCTRL(Transform camera) => camera_CTRL = camera;

    public void SetADS(bool ads) => _isAiming = ads;

    /// <summary>
    /// Đổi firing mode — phải đồng bộ với GunRecoilController.SetFiringMode().
    /// Từ chối nếu mode không được phép trong GunRecoilData hiện tại.
    /// </summary>
    public bool SetFiringMode(GunRecoilController.FiringMode mode)
    {
        if (data == null) return false;
        if (!data.IsModeAllowed(ToAllowedModes(mode)))
        {
            Debug.LogWarning($"[CameraRecoil] SetFiringMode: mode '{mode}' không được phép " +
                             $"trên data '{data.name}'. Allowed: {data.allowedModes}", this);
            return false;
        }

        firingMode = mode;
        return true;
    }

    // ─────────────────────────────────────────────
    //  FIRE — entry point từ weapon logic
    // ─────────────────────────────────────────────

    /// <summary>
    /// Gọi mỗi khi người chơi bắn.
    /// - Semi / Auto : gọi 1 lần mỗi phát bắn thực sự (sau khi weapon logic xác nhận viên đạn ra).
    /// - Burst       : gọi 1 lần khi trigger burst — CameraRecoil tự bắn đủ burstCount phát bên trong
    ///                 theo đúng FireDelay, khớp với GunRecoilController.TriggerBurst().
    ///
    /// Trả về true nếu camera recoil thực sự được kích hoạt.
    /// </summary>
    public bool OnFire()
    {
        if (data == null) return false;

        switch (firingMode)
        {
            case GunRecoilController.FiringMode.Semi:
                if (Time.time < _nextFireTime) return false;
                FireOnce();
                _nextFireTime = Time.time + _weaponData.FireDelay;
                return true;

            case GunRecoilController.FiringMode.Auto:
                if (Time.time < _nextFireTime) return false;
                FireOnce();
                _nextFireTime = Time.time + _weaponData.FireDelay;
                return true;

            case GunRecoilController.FiringMode.Burst:
                // Không bắn nếu burst đang chạy hoặc còn trong cooldown
                if (_burstRemaining > 0) return false;
                if (Time.time < _nextFireTime) return false;
                TriggerBurst();
                // Cooldown = thời gian hoàn thành toàn bộ loạt burst, giống GunRecoilController
                _nextFireTime = Time.time + data.BurstCount * _weaponData.FireDelay;
                return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────
    //  LOAD DATA — đổi súng giữa trận
    // ─────────────────────────────────────────────

    /// <summary>
    /// Nạp bộ tham số camera shake từ GunRecoilData.
    /// Gọi mỗi khi đổi súng để camera rung đúng với khẩu súng mới.
    /// Trả về false nếu newData null hoặc không hợp lệ.
    /// </summary>
    public bool LoadData(GunRecoilData newData)
    {
        if (newData == null)
        {
            Debug.LogError("[CameraRecoil] LoadData: newData là null — bỏ qua.", this);
            return false;
        }

        if (!newData.Validate(out string error))
        {
            Debug.LogError($"[CameraRecoil] LoadData: GunRecoilData '{newData.name}' " +
                           $"không hợp lệ: {error}", this);
            return false;
        }

        data = newData;
        ApplyDataToFields();
        ResetState();
        return true;
    }

    /// <summary>
    /// Overload tiện lợi — nạp trực tiếp từ WeaponData.
    /// Dùng cùng WeaponData đã truyền cho GunRecoilController.ReloadData(weapon).
    /// </summary>
    public bool LoadData(WeaponData weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("[CameraRecoil] LoadData: WeaponData là null — bỏ qua.", this);
            return false;
        }

        if (!weapon.HasRecoilData)
        {
            Debug.LogError($"[CameraRecoil] LoadData: WeaponData '{weapon.name}' " +
                           $"chưa được gán RecoilData.", this);
            return false;
        }

        return LoadData(weapon.RecoilData);
    }

    // ─────────────────────────────────────────────
    //  BURST TICK (internal)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Giống HandleBurstTick() trong GunRecoilController — xử lý từng phát burst theo thời gian thực.
    /// </summary>
    private void HandleBurstTick()
    {
        if (_burstRemaining <= 0) return;
        if (Time.time < _burstNextShotTime) return;

        FireOnce();
        _burstRemaining--;
        _burstNextShotTime = Time.time + _burstFireRate;
    }

    private void TriggerBurst()
    {
        _burstRemaining    = data.BurstCount;
        _burstFireRate     = _weaponData.FireDelay;
        _burstNextShotTime = Time.time; // phát đầu tiên ngay lập tức
    }

    // ─────────────────────────────────────────────
    //  FIRE — single shot
    // ─────────────────────────────────────────────

    private void FireOnce()
    {
        float aimReduction = 1f - (_isAiming ? data.camAimWeight : 0f);
        targetRotation += new Vector3(
            data.camRecoilX,
            Random.Range(-data.camRecoilY, data.camRecoilY),
            Random.Range(-data.camRecoilZ, data.camRecoilZ)) * aimReduction;
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private void ApplyDataToFields()
    {
        // Resolve firingMode mặc định từ allowedModes của data mới
        if (!data.IsModeAllowed(ToAllowedModes(firingMode)))
            firingMode = ToFiringMode(data.FirstAllowedMode());
    }

    private void ResetState()
    {
        targetRotation     = Vector3.zero;
        currentRotation    = Vector3.zero;
        _nextFireTime      = 0f;
        _burstRemaining    = 0;
        _burstNextShotTime = 0f;
    }

    private static GunRecoilData.AllowedModes ToAllowedModes(GunRecoilController.FiringMode mode) => mode switch
    {
        GunRecoilController.FiringMode.Semi  => GunRecoilData.AllowedModes.Semi,
        GunRecoilController.FiringMode.Auto  => GunRecoilData.AllowedModes.Auto,
        GunRecoilController.FiringMode.Burst => GunRecoilData.AllowedModes.Burst,
        _                => GunRecoilData.AllowedModes.Semi,
    };

    private static GunRecoilController.FiringMode ToFiringMode(GunRecoilData.AllowedModes mode) => mode switch
    {
        GunRecoilData.AllowedModes.Auto  => GunRecoilController.FiringMode.Auto,
        GunRecoilData.AllowedModes.Burst => GunRecoilController.FiringMode.Burst,
        _                                => GunRecoilController.FiringMode.Semi,
    };
}