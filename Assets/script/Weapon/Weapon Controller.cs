using System;
using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [Header("Gun")] 
    [SerializeField] private WeaponData _data;
    [SerializeField] private Animator _anim;
    [SerializeField] private WeaponManager  _manager;

    [SerializeField] private TransformConstraint _ads_Pivot;
    
    [Header("CTRL BODY")]
    [SerializeField] private Transform[] _list_Bone_Body;
    
    [Header("CTRL GRENADE")]
    [SerializeField] private Transform[] _list_Bone_Grenade;

    [Header("Camera")]
    [SerializeField] private Transform _camera_Shake;
    [SerializeField] private Camera _camera_Zoom;
    
    [Header("IK Hand Left")]
    [SerializeField] private Transform _hand_Left;
    [SerializeField] private Transform _pole_Left;
    
    [Header("IK Hand Right")]
    [SerializeField] private Transform _hand_Right;
    [SerializeField] private Transform _pole_Right;

    [Header("ADS")]
    [SerializeField] private bool _ads;
    private bool _lastAdsState;
    private Coroutine _blendRoutine;
    [SerializeField] private GunRecoilController _gun_Recoil_CTRL;
    public GunRecoilController GunRecoilCTRL => _gun_Recoil_CTRL;
    [SerializeField] private WeaponSway _sway;
    [SerializeField] private CameraRecoil _cam_Recoil;
    
    // =============================== public Filed ============================================

    public WeaponManager Manager
    {
        set => _manager = value;
        get => _manager;
    }
    
    public WeaponData Data => _data;
    public Camera Camera_Zoom {set => _camera_Zoom = value;}
    public Transform Camera_Shake => _camera_Shake;
    public Transform[] List_Bone_Body => _list_Bone_Body;
    public Transform[] List_Bone_Grenade => _list_Bone_Grenade;
    public  Transform Hand_Left => _hand_Left;
    public  Transform Hand_Right => _hand_Right;
    public  Transform Pole_Left => _pole_Left;
    public  Transform Pole_Right => _pole_Right;
    public TransformConstraint ADS_Pivot => _ads_Pivot;


    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        if (_anim == null) Debug.LogError("Không tìm thấy Animator!");
        if (_data == null) return;
        _cam_Recoil?.LoadData(_data);
        _gun_Recoil_CTRL?.ReloadData(_data);
    }
    
    // ====================================== EVENT ONFIRE =========================
    
    private event Action<GunRecoilController.FiringMode> OnShotFired;
    
    void Start()
    {
        _gun_Recoil_CTRL.OnShotFired += HandleShot;
    }
    
    void HandleShot(GunRecoilController.FiringMode mode)
    {
        _anim.SetTrigger("IsShooting");
    }
    
    void OnDestroy()
    {
        _gun_Recoil_CTRL.OnShotFired -= HandleShot;
    }

    // ========================================== ADS ==================================
    
    private void Update()
    {
        ADSHandler();
    }

    private void ADSHandler()
    {
        if (_ads != _lastAdsState)
        {
            _lastAdsState = _ads;
            _gun_Recoil_CTRL.SetAiming(_ads);
            _cam_Recoil.SetADS(_ads);
            if (_blendRoutine != null)
                StopCoroutine(_blendRoutine);

            float target = _ads ? 1f : 0f;
            float zoom = _ads ? _data.ADS_Zoom : _data.Stand_Zoom;
            float zoom_y = _ads ? _data.ADS_Zoom_Y : _data.Stamd_Zoom_Y;
            _blendRoutine = StartCoroutine(ADSBlend(target, zoom, zoom_y, _data.ADS_duration));
        }
    }

    private IEnumerator ADSBlend(float targetWeight, float zoomTarget, float zoomY, float duration = 0.25f)
    {
        // XÓA HOÀN TOÀN 4 DÒNG CHECK APPROXIMATELY Ở ĐÂY
    
        float start = _ads_Pivot.targets[0].weight;
        float startzoom = _camera_Zoom.fieldOfView;
        float startzoom_y = _camera_Zoom.transform.localPosition.y;
    
        float time = 0f;
        Vector3 _zoomYtarget = _camera_Zoom.transform.localPosition; // Lấy vị trí hiện tại thay vì Vector3.zero để giữ lại trục X và Z

        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = Mathf.Clamp01(time / duration);
        
            _ads_Pivot.targets[0].weight = Mathf.Lerp(start, targetWeight, progress);
            _sway.SetWeight(Mathf.Lerp(start, targetWeight, progress));
            _camera_Zoom.fieldOfView = Mathf.Lerp(startzoom, zoomTarget, progress);
        
            // Giữ nguyên X và Z của camera, chỉ blend trục Y
            _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, Mathf.Lerp(startzoom_y, zoomY, progress), _zoomYtarget.z);
        
            yield return null;
        }

        // Đảm bảo gán chính xác giá trị cuối cùng khi kết thúc
        _ads_Pivot.targets[0].weight = targetWeight;
        _sway.SetWeight(targetWeight);
        _camera_Zoom.fieldOfView = zoomTarget;
        _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, zoomY, _zoomYtarget.z);
    
        _blendRoutine = null;
    }
    
    // ======================= WEAPON SWAY =============================
    
    public void SetLookDelta(Vector2 value) => _sway.SetLookDelta(value);
    public void SetMoveDirection(Vector2 value) => _sway.SetMoveDirection(value);
    
    // ======================= CAMERA RECOIL ===========================
    
    public void SetCameraCTRL(Transform camera) => _cam_Recoil.SetCameraCTRL(camera);
    
    // ======================= FIRE ====================================

    public bool OnFire()
    {
        return _cam_Recoil.OnFire() && _gun_Recoil_CTRL.OnFire();
    }

    public void SwitchMode(ref GunRecoilController.FiringMode current_mode)
    {
        switch (current_mode)
        {
            case GunRecoilController.FiringMode.Semi:
                if (!TrySetMode(ref current_mode, GunRecoilController.FiringMode.Auto))
                    TrySetMode(ref current_mode, GunRecoilController.FiringMode.Burst);
                break;
            case GunRecoilController.FiringMode.Auto:
                if (!TrySetMode(ref current_mode, GunRecoilController.FiringMode.Burst))
                    TrySetMode(ref current_mode, GunRecoilController.FiringMode.Semi);
                break;
            case GunRecoilController.FiringMode.Burst:
                if (!TrySetMode(ref current_mode, GunRecoilController.FiringMode.Semi))
                    TrySetMode(ref current_mode, GunRecoilController.FiringMode.Auto);
                break;
        }
    }
    
    /// <summary>
    /// Gọi SetFiringMode trên controller — controller tự từ chối nếu không được phép.
    /// </summary>
    private bool TrySetMode(ref GunRecoilController.FiringMode current_mode, GunRecoilController.FiringMode mode)
    {
        if (_gun_Recoil_CTRL.SetFiringMode(mode))
        {
            current_mode = mode;
            _cam_Recoil.SetFiringMode(mode);
            Debug.Log($"[GunRecoilDemo] Mode → {mode}");
            return true;
        }
        return false;
    }
    
    // ============================================ ANIMATOR =================================================
    
    public void IsDead(bool isDead) => _anim.SetBool("IsDead", isDead);
    public void IsGrouded(bool isGrouded) => _anim.SetBool("IsGrouded", isGrouded); 
    public void IsCrouching(bool isCrouching) => _anim.SetBool("IsCrouching", isCrouching); 
    public void IsSliding(bool isSliding) => _anim.SetBool("IsSliding", isSliding); 
    public void IsThrow(bool isThrow) => _anim.SetBool("IsThrow", isThrow);
    public void IsSwitching(bool isSwitching) => _anim.SetBool("IsSwitching", isSwitching);
    public void ADS(bool ads)
    {
        _ads = ads;
        _anim.SetBool("ADS", ads);
    }
    public void IsReloading(bool isReloading) => _anim.SetBool("IsReloading", isReloading);
    public void IsJumping() => _anim.SetTrigger("IsJumping");

    public void Move(Vector2 move)
    {
        _anim.SetFloat("MoveX", move.x);
        _anim.SetFloat("MoveY", move.y);
    }
    
    public void GrenadeID(int grenadeID) => _anim.SetInteger("GrenadeID", grenadeID);
}