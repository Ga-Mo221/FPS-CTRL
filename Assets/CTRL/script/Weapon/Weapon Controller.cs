using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    [SerializeField] private WeaponType _type = WeaponType.Gun;
    
    // =========================== NETWORK ==========================================
    [TabGroup("Weapon Controller", "Runtime")]
    [Networked] public NetworkObject OwnerCharacter { get; set; }
    
    [Networked, OnChangedRender(nameof(OnBulletCountChange))]
    private int _currentBulletvalue { get; set; }
    [Networked, OnChangedRender(nameof(OnBulletCountChange))]
    private int _currentMaxBullet { get; set; }
    
    // ============================ LOCAL FILED =====================================
    
    // ========================= WEAPON DATA =============================
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private WeaponData _data;
    
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private Animator _anim;

    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private TransformConstraint _ads_Pivot;
    
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private GunRecoilController _gun_Recoil_CTRL;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private WeaponSway _sway;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private CameraRecoil _cam_Recoil;
    
    [BoxGroup("Weapon Controller/Weapon Data/Bullet")]
    [SerializeField] private GameObject _bullet_Prefab;
    [BoxGroup("Weapon Controller/Weapon Data/Bullet")]
    [SerializeField] private ShootEffectController _shoot_Effect;
    
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private AudioSource _sound;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private BulletCaseController _bulletController;
    
    // ========================= TRANSFORM =============================
    
    [TabGroup("Weapon Controller", "Transform")]
    [BoxGroup("Weapon Controller/Transform/Hand List")]
    [SerializeField] private Transform[] _list_Bone_Body;
    
    [BoxGroup("Weapon Controller/Transform/Grenade List")]
    [SerializeField] private Transform[] _list_Bone_Grenade;
    
    [HorizontalGroup("Weapon Controller/Transform/camera", width: 0.6f)]
    [SerializeField] private Transform _camera_Shake;
    
    [BoxGroup("Weapon Controller/Transform/IK Left")]
    [HorizontalGroup("Weapon Controller/Transform/IK Left/IKhand_Left", width: 0.6f)]
    [SerializeField] private Transform _hand_Left;
    
    [HorizontalGroup("Weapon Controller/Transform/IK Left/Elbow_Left", width: 0.6f)]
    [SerializeField] private Transform _pole_Left;
    
    [BoxGroup("Weapon Controller/Transform/IK Right")]
    [HorizontalGroup("Weapon Controller/Transform/IK Right/IKhand_Right", width: 0.6f)]
    [SerializeField] private Transform _hand_Right;
    
    [HorizontalGroup("Weapon Controller/Transform/IK Right/Elbow_Right", width: 0.6f)]
    [SerializeField] private Transform _pole_Right;
    
    [BoxGroup("Weapon Controller/Transform/Bullet")]
    [HorizontalGroup("Weapon Controller/Transform/Bullet/row", width:0.6f)]
    [SerializeField] private Transform _bullet_point;
    
    [HorizontalGroup("Weapon Controller/Transform/Bullet/ro", width:0.6f)]
    [SerializeField] private Transform _ads_Shoot_Point;
    
    [HorizontalGroup("Weapon Controller/Transform/Bullet/parrent", width: 0.6f)]
    [SerializeField] private Transform _parrentBullet;
    
    // =========================== NAME ==============================
    
#if UNITY_EDITOR
    [HorizontalGroup("Weapon Controller/Transform/Grenade List/grenade")]
    [HideLabel]
    [SerializeField] private string _grenade_Flash_Name = "RAW_Flash";
    [HorizontalGroup("Weapon Controller/Transform/Grenade List/grenade")]
    [HideLabel]
    [SerializeField] private string _grenade_Frag_Name = "RAW_Frag";
    
    [HorizontalGroup("Weapon Controller/Transform/camera")]
    [HideLabel]
    [SerializeField] private string _camera_ShakeName = "RAW_Camera";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Left/IKhand_Left")] 
    [HideLabel]
    [SerializeField] private string _hand_Left_Name = "RAW_Hand_L";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Left/Elbow_Left")]
    [HideLabel]
    [SerializeField] private string _Elbow_Left_Name = "RAW_Elbow_L";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Right/IKhand_Right")] 
    [HideLabel]
    [SerializeField] private string _hand_Right_Name = "RAW_Hand_R";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Right/Elbow_Right")]
    [HideLabel]
    [SerializeField] private string _Elbow_Right_Name = "RAW_Elbow_R";
    
    [HorizontalGroup("Weapon Controller/Transform/Bullet/row")]
    [HideLabel]
    [SerializeField] private string _bullet_point_name = "Bullet Point";
    
    [HorizontalGroup("Weapon Controller/Transform/Bullet/ro")] 
    [HideLabel] 
    [SerializeField] private string _ads_Shoot_Point_Name = "Shoot Point";
    
    [HorizontalGroup("Weapon Controller/Transform/Bullet/parrent")]
    [HideLabel]
    [SerializeField] private string _parrentBullet_name = "Parrent Bullet";
#endif
    
    // ========================= RUNTIME =============================
    
    [TabGroup("Weapon Controller", "Runtime")]
    [SerializeField] private CharComponentManager _componentManager;

    [TabGroup("Weapon Controller", "Runtime")] 
    [SerializeField] private UIWeapon _uiWeapon;
    
    [TabGroup("Weapon Controller", "Runtime")]
    [SerializeField] private Camera _camera_Zoom;

    private bool _ads;
    private bool _lastAdsState;
    private Coroutine _blendRoutine;
    
    private Transform _ads_Pivot_transform;
    private bool _isCrouch;
    
    private AudioManager _audio;
    
    private List<ProjectileVisual> _poolBullet = new List<ProjectileVisual>();
    
    // =============================== public Filed ============================================
    
    public WeaponData Data => _data;
    public Transform Camera_Shake => _camera_Shake;
    public Transform[] List_Bone_Body => _list_Bone_Body;
    public Transform[] List_Bone_Grenade => _list_Bone_Grenade;
    public  Transform Hand_Left => _hand_Left;
    public  Transform Hand_Right => _hand_Right;
    public  Transform Pole_Left => _pole_Left;
    public  Transform Pole_Right => _pole_Right;
    public TransformConstraint ADS_Pivot => _ads_Pivot;
    public AudioSource Sound => _sound;
    public Vector3 ADS_Shoot_Point => _ads_Shoot_Point.position;
    public void SetAdsPivotTransform(Transform transform) =>  _ads_Pivot_transform = transform;
    public GunRecoilController GunRecoilCTRL => _gun_Recoil_CTRL;
    public WeaponType WeaponType => _type;
    
    public override void Spawned()
    {
        AttachToOwner();
        
        switch (_type)
        {
            case WeaponType.Gun:
                _cam_Recoil.SetCameraCTRL(_componentManager.WeaponManager.Camera_Recoil);
                _camera_Zoom = _componentManager.WeaponManager.Camera;
                break;
            case WeaponType.Melee:
                _gun_Recoil_CTRL.SetFiringMode(GunRecoilController.FiringMode.Melee);
                break;
        }
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (_anim == null)
            _anim = GetComponentInChildren<Animator>();
        if (_anim == null) Debug.LogError("Không tìm thấy Animator!");
        if (_data == null) return;
        
        switch (_type)
        {
            case WeaponType.Gun:
                _cam_Recoil?.LoadData(_data);
                break;
            case WeaponType.Melee:
                break;
        }
        
        _gun_Recoil_CTRL?.ReloadData(_data);
    }
    
    private void AttachToOwner()
    {
        if (OwnerCharacter == null) return;

        _componentManager = OwnerCharacter.GetComponent<CharComponentManager>();
        if (_componentManager == null) return;

        _uiWeapon = _componentManager.UIWeapon;

        Transform socket = _componentManager.WeaponManager.WeaponParent;
        transform.SetParent(socket, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Respawn()
    { 
        if (_type != WeaponType.Gun) return;
        
        _currentMaxBullet = _data.MaxBullet;
        _currentBulletvalue = _data.DefaultBullet;
    }
    
    // ====================================== EVENT ONFIRE =========================

    void Start()
    {
        if (_uiWeapon != null)
            _uiWeapon.SetBullet(_currentMaxBullet, _currentBulletvalue, _data);
        
        _audio = AudioManager.Instance;
        
        if (_type != WeaponType.Gun) return;
        
        _parrentBullet.name = $"{_parrentBullet.name} for {transform.root.name} : {transform.name}";
        _parrentBullet.parent = GameManager.instance.BulletParrent;
        
        _gun_Recoil_CTRL.OnShotFired += HandleShot;
    }

    private void OnEnable()
    {
        if (_uiWeapon != null)
            _uiWeapon.SetBullet(_currentMaxBullet, _currentBulletvalue, _data);
    }

    void OnDestroy()
    {
        _gun_Recoil_CTRL.OnShotFired -= HandleShot;
        if (_parrentBullet != null)
            Destroy(_parrentBullet.gameObject);
    }

    private void HandleShot(GunRecoilController.FiringMode mode)
    {
        PlayFireEffect();
    }

    private void PlayFireEffect()
    {
        switch (_type)
        {
            case WeaponType.Gun:
                _currentBulletvalue--;
                if (_bulletController != null)
                    _bulletController.Drop();
                    // reset effect
                    _shoot_Effect.Play();
                break;
            case WeaponType.Melee:
                break;
        }
        _anim.SetTrigger("IsShooting");
        if (_audio != null) _audio.PlayClip(_sound, Data.FireClip, Data.Volume_Fire);
    }

    // ========================================== ADS ==================================
    
    private void Update()
    {
        if (_type != WeaponType.Gun) return;
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
            float zoom_z = _ads ? _data.ADS_Zoom_Z : _data.Stamd_Zoom_Z;
            float Pivot_Height = _ads && _isCrouch ? _data.Ads_Pivot_Height : _data.Stand_Pivot_Height;
            _blendRoutine = StartCoroutine(ADSBlend(target, zoom, zoom_z, Pivot_Height, _data.ADS_duration));
        }
    }

    private IEnumerator ADSBlend(float targetWeight, float zoomTarget, float zoomZ, float pivotHeight, float duration = 0.25f)
    {
        // XÓA HOÀN TOÀN 4 DÒNG CHECK APPROXIMATELY Ở ĐÂY
    
        float start = _ads_Pivot.targets[0].weight;
        float startzoom = _camera_Zoom.fieldOfView;
        float startzoom_z = _camera_Zoom.transform.localPosition.z;
        float startHeight = _ads_Pivot_transform.localPosition.z;
    
        float time = 0f;
        Vector3 _zoomYtarget = _camera_Zoom.transform.localPosition; // Lấy vị trí hiện tại thay vì Vector3.zero để giữ lại trục X và Z
        Vector3 PivotHeight  = _ads_Pivot_transform.localPosition;

        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = Mathf.Clamp01(time / duration);
        
            _ads_Pivot.targets[0].weight = Mathf.Lerp(start, targetWeight, progress);
            _sway.SetWeight(Mathf.Lerp(start, targetWeight, progress));
            _camera_Zoom.fieldOfView = Mathf.Lerp(startzoom, zoomTarget, progress);
        
            // Giữ nguyên X và Z của camera, chỉ blend trục Y
            _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, _zoomYtarget.y, Mathf.Lerp(startzoom_z, zoomZ, progress));
            _ads_Pivot_transform.localPosition = new Vector3(PivotHeight.x, PivotHeight.y, Mathf.Lerp(startHeight, pivotHeight, progress));
        
            yield return null;
        }

        // Đảm bảo gán chính xác giá trị cuối cùng khi kết thúc
        _ads_Pivot.targets[0].weight = targetWeight;
        _sway.SetWeight(targetWeight);
        _camera_Zoom.fieldOfView = zoomTarget;
        _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, _zoomYtarget.y, zoomZ);
        _ads_Pivot_transform.transform.localPosition = new Vector3(PivotHeight.x, PivotHeight.y, pivotHeight);
        _blendRoutine = null;
    }
    
    // ======================= WEAPON SWAY =============================
    
    public void SetLookDelta(Vector2 value) => _sway.SetLookDelta(value);
    public void SetMoveDirection(Vector2 value) => _sway.SetMoveDirection(value);
    
    // ======================= FIRE ====================================

    public void Reload()
    {
        if (_currentMaxBullet >= _data.DefaultBullet)
        {
            _currentMaxBullet -= _data.DefaultBullet - _currentBulletvalue;
            _currentBulletvalue = _data.DefaultBullet;
        }else if (_currentMaxBullet > 0)
        {
            int thieu = _data.DefaultBullet - _currentBulletvalue;
            if (thieu <= _currentMaxBullet)
            {
                _currentBulletvalue = _data.DefaultBullet;
                _currentMaxBullet -= thieu;
            }
            else
            {
                _currentBulletvalue += _currentMaxBullet;
                _currentMaxBullet = 0;
            }
        }
    }

    public bool OnFire()
    {
        switch (_type)
        {
            case WeaponType.Gun:
                if (_currentBulletvalue <= 0) return false;
                return _cam_Recoil.OnFire() && _gun_Recoil_CTRL.OnFire(_data.FireDelay);
            case WeaponType.Melee:
                return _gun_Recoil_CTRL.OnFire(_data.FireDelay);
            default:
                return false;
        }
    }

    public void SpawnProjectileVisual(Vector3 hitPosition, Vector3 hitNormal, bool showEffect)
    {
        if (_bullet_Prefab == null) return;

        var projectileVisual = GetBullet();
        projectileVisual.transform.position = _bullet_point.position;
        projectileVisual.transform.rotation = _bullet_point.rotation;
        projectileVisual.SetHit(hitPosition, hitNormal, showEffect);
    }
    
    private ProjectileVisual GetBullet()
    {
        foreach (var bullet in _poolBullet)
        {
            if (!bullet.gameObject.activeSelf)
            {
                bullet.gameObject.SetActive(true);
                return bullet;
            }
        }

        GameObject newbullet = Instantiate(_bullet_Prefab, _parrentBullet);
        var visual = newbullet.GetComponent<ProjectileVisual>();
        _poolBullet.Add(visual);

        return visual;
    }
    
    public void SwitchMode()
    {
        if (_type != WeaponType.Gun) return;
        
        var current_mode = _gun_Recoil_CTRL.GetFiringMode();
        switch (current_mode)
        {
            case GunRecoilController.FiringMode.Semi:
                if (!TrySetMode(GunRecoilController.FiringMode.Auto))
                    TrySetMode(GunRecoilController.FiringMode.Burst);
                break;
            case GunRecoilController.FiringMode.Auto:
                if (!TrySetMode(GunRecoilController.FiringMode.Burst))
                    TrySetMode(GunRecoilController.FiringMode.Semi);
                break;
            case GunRecoilController.FiringMode.Burst:
                if (!TrySetMode(GunRecoilController.FiringMode.Semi))
                    TrySetMode(GunRecoilController.FiringMode.Auto);
                break;
        }
    }
    
    /// <summary>
    /// Gọi SetFiringMode trên controller — controller tự từ chối nếu không được phép.
    /// </summary>
    private bool TrySetMode(GunRecoilController.FiringMode mode)
    {
        if (_gun_Recoil_CTRL.SetFiringMode(mode))
        {
            _cam_Recoil.SetFiringMode(mode);
            Debug.Log($"[GunRecoilDemo] Mode → {mode}");
            return true;
        }
        return false;
    }

    // Cập nhật UI khi số lượng đạn thay đổi.
    void OnBulletCountChange()
    {
        _uiWeapon.SetBullet(_currentBulletvalue, _currentMaxBullet, _data);
    }
    
    // ============================================ ANIMATOR =================================================
    
    public void IsDead(bool isDead) => _anim.SetBool("IsDead", isDead);
    public void IsGrouded(bool isGrouded) => _anim.SetBool("IsGrouded", isGrouded); 
    public void IsCrouching(bool isCrouching)
    {
        _isCrouch = isCrouching;
        _anim.SetBool("IsCrouching", isCrouching);
    }
    public void IsSliding(bool isSliding) => _anim.SetBool("IsSliding", isSliding); 
    public void IsThrow(bool isThrow) => _anim.SetBool("IsThrow", isThrow);
    public void IsSwitching(bool isSwitching) 
    {
        //Debug.Log($"[WeaponController] {transform.name} IsSwitching: {isSwitching}");
        _anim.SetBool("IsSwitching", isSwitching);
    }
    public bool ADS(bool ads)
    {
        if (_type != WeaponType.Gun) return false;
        _ads = ads;
        _anim.SetBool("ADS", ads);
        return true;
    }
    public bool IsReloading(bool isReloading)
    {
        if (isReloading)
        {
            if (_type != WeaponType.Gun)  return false;
            if (_currentBulletvalue == _data.DefaultBullet) return false;
            if (_currentMaxBullet == 0)  return false;
        }
        
        _anim.SetBool("IsReloading", isReloading);
        return true;
    }
    
    public void IsJumping() => _anim.SetTrigger("IsJumping");
    public void ResetJumping() => _anim.ResetTrigger("IsJumping");

    public void Move(Vector2 move)
    {
        _anim.SetFloat("MoveX", move.x);
        _anim.SetFloat("MoveY", move.y);
    }
    
    public void GrenadeID(int grenadeID) => _anim.SetInteger("GrenadeID", grenadeID);
    
    
    //========================================== EDITOR ============================================
    
#if UNITY_EDITOR
    [Button("Setup")]
    public void LoadSetup()
    {
        _anim = GetComponentInChildren<Animator>(true);
        if (_anim == null) Debug.LogError("Animator not found!");

        _ads_Pivot = GetComponentInChildren<TransformConstraint>(true);
        if (_ads_Pivot == null) Debug.LogError("TransformConstraint not found!");

        _gun_Recoil_CTRL = GetComponentInChildren<GunRecoilController>(true);
        if (_gun_Recoil_CTRL == null) Debug.LogError("GunRecoilController not found!");

        _sway = GetComponentInChildren<WeaponSway>(true);
        if (_sway == null) Debug.LogError("Weapon Sway not found!");
        
        _cam_Recoil = GetComponentInChildren<CameraRecoil>(true);
        if (_cam_Recoil == null) Debug.LogError("CameraRecoil not found!");
        
        _bulletController = GetComponentInChildren<BulletCaseController>(true);
        if (_bulletController == null) Debug.LogError("BulletCaseController not found!");

        _shoot_Effect = GetComponentInChildren<ShootEffectController>(true);
        if (_shoot_Effect == null) Debug.LogError("ShootEffectController not found!");
        
        List<Transform> list_bone_hand = new List<Transform>();
        List<Transform> list_bone_grenade = new List<Transform>();
        foreach (var trans in GetComponentsInChildren<Transform>(true))
        {
            if (trans.name.Equals(_parrentBullet_name))
            {
                _parrentBullet = trans;
                continue;
            }
            
            // camera shake
            if (trans.name.Equals(_camera_ShakeName))
            {
                _camera_Shake = trans;
                continue;
            }

            // hand left
            if (trans.name.Equals(_hand_Left_Name))
            {
                _hand_Left = trans;
                foreach (var bone in trans.GetComponentsInChildren<Transform>(true))
                {
                    if (bone == trans) continue;
                    list_bone_hand.Add(bone);
                }
                continue;
            }
            // elbow left
            if (trans.name.Equals(_Elbow_Left_Name))
            {
                _pole_Left = trans;
                continue;
            }
            
            // hand right
            if (trans.name.Equals(_hand_Right_Name))
            {
                _hand_Right = trans;
                foreach (var bone in trans.GetComponentsInChildren<Transform>(true))
                {
                    if (bone == trans) continue;
                    list_bone_hand.Add(bone);
                }
                continue;
            }
            // elbow right
            if (trans.name.Equals(_Elbow_Right_Name))
            {
                _pole_Right = trans;
                continue;
            }

            if (trans.name.Equals(_grenade_Frag_Name))
            {
                Debug.Log($"tim thay {_grenade_Frag_Name}");
                foreach (var bone in trans.GetComponentsInChildren<Transform>(true))
                    list_bone_grenade.Add(bone);
                continue;
            }

            if (trans.name.Equals(_grenade_Flash_Name))
            {
                Debug.Log($"tim thay {_grenade_Flash_Name}");
                foreach (var bone in trans.GetComponentsInChildren<Transform>(true))
                    list_bone_grenade.Add(bone);
            }
            
            if (trans.name.Equals(_bullet_point_name))
            {
                _bullet_point = trans;
                continue;
            }

            if (trans.name.Equals(_ads_Shoot_Point_Name))
            {
                _ads_Shoot_Point = trans;
                continue;
            }
        }

        _list_Bone_Body = new Transform[0];
        _list_Bone_Grenade = new Transform[0];
        _list_Bone_Body = _list_Bone_Body.Concat(list_bone_hand).ToArray();
        _list_Bone_Grenade = _list_Bone_Grenade.Concat(list_bone_grenade).ToArray();
    }
#endif
}