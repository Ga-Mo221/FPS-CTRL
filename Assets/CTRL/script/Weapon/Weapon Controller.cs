using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Game.UI;
using Sirenix.OdinInspector;
using TrueInputSystem;
using UnityEngine;
using GaMo.BlenderEditorTools;

public class WeaponController : NetworkBehaviour
{
    [SerializeField] private WeaponType _type = WeaponType.Rifle;
    
    // =========================== NETWORK ==========================================
    [TabGroup("Weapon Controller", "Runtime")]
    [Networked] public NetworkObject OwnerCharacter { get; set; }
    
    [Networked, OnChangedRender(nameof(OnBulletCountChange))]
    [ReadOnly]
    public int CurrentBulletvalue { get; private set; }
    
    [Networked, OnChangedRender(nameof(OnBulletCountChange))]
    [ReadOnly]
    public int CurrentMaxBullet { get; private set; }
    
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
    [SerializeField] private AudioSource _sound_shoot;
    
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private AudioSource _sound_Slider;
    
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

    [TabGroup("Weapon Controller", "Runtime")] [SerializeField]
    private GameHudControllerBase _gameHudController;
    
    [TabGroup("Weapon Controller", "Runtime")]
    [SerializeField] private Camera _camera_Zoom;

    private bool _isDead = false;
    private bool _ads;
    private bool _lastAdsState;
    private Coroutine _blendRoutine;
    private Coroutine _fireFailedDelay;
    
    private Transform _ads_Pivot_transform;
    private bool _isCrouch;
    
    private AudioManager _audio;
    
    private List<GameObject> _poolBullet = new List<GameObject>();

    [Networked] private bool _rightPuch {get; set;}
    
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
    public AudioSource Sound_Slider => _sound_Slider;
    public Vector3 ADS_Shoot_Point => _ads_Shoot_Point.position;
    public void SetAdsPivotTransform(Transform transform) =>  _ads_Pivot_transform = transform;
    public GunRecoilController GunRecoilCTRL => _gun_Recoil_CTRL;
    public WeaponType WeaponType => _type;

    private Vector3 _ads_pivot_position_original;
    private Quaternion _ads_pivot_rotation_original;
    
    public override void Spawned()
    {
        AttachToOwner();
        
        switch (_type)
        {
            case WeaponType.Sniper:
            case WeaponType.Shotgun:
            case WeaponType.Rifle:
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
            case WeaponType.Sniper:
            case WeaponType.Shotgun:
            case WeaponType.Rifle:
                _cam_Recoil?.LoadData(_data);
                break;
            case WeaponType.Melee:
                break;
        }
        
        _ads_pivot_position_original = _ads_Pivot.transform.localPosition;
        _ads_pivot_rotation_original = _ads_Pivot.transform.localRotation;
        
        _gun_Recoil_CTRL?.ReloadData(_data);
    }
    
    private void AttachToOwner()
    {
        if (OwnerCharacter == null) return;

        _componentManager = OwnerCharacter.GetComponent<CharComponentManager>();
        if (_componentManager == null) return;

        _gameHudController = _componentManager.GameHudController();

        Transform socket = _componentManager.WeaponManager.WeaponParent;
        transform.SetParent(socket, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Respawn()
    { 
        _ads = false;
        _lastAdsState = true;
        
        if (_type == WeaponType.Melee) return;
        
        CurrentMaxBullet = _data.MaxBullet;
        CurrentBulletvalue = _data.DefaultBullet;
    }
    
    // ====================================== EVENT ONFIRE =========================
    private TISInputBridge _bridge;

    void Start()
    {
        _bridge = TISInputBridge.Instance;
        
        if (_gameHudController != null)
            _gameHudController.SetBullet(CurrentMaxBullet, CurrentBulletvalue);
        
        _audio = AudioManager.Instance;
        
        _gun_Recoil_CTRL.OnShotFired += HandleShot;
        
        _parrentBullet.name = $"{_parrentBullet.name} for {transform.root.name} : {transform.name}";
        _parrentBullet.parent = GameManager.Instance.BulletParrent;
    }

    void OnEnable()
    {
        _ads = false;
        _lastAdsState = true;

        _ads_Pivot.transform.localPosition = _ads_pivot_position_original;
        _ads_Pivot.transform.localRotation = _ads_pivot_rotation_original;
       
        if (Object != null)
            CurrentBullet(CurrentBulletvalue);
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
            case WeaponType.Rifle:
            case WeaponType.Shotgun:
            case WeaponType.Sniper:
                if (Object.HasStateAuthority)
                    CurrentBulletvalue--;
                if (_bulletController != null)
                    _bulletController.Drop();
                    
                _shoot_Effect.Play();
                _anim.SetTrigger("IsShooting");
                
                if (Object.HasInputAuthority && _bridge != null)
                    _bridge.TryRumble(0.2f, 0.5f, 0.1f);
                
                if (_audio != null) _audio.PlayClip(_sound_shoot, Data.FireClip, Data.Volume_Fire);
                break;
            case WeaponType.Melee:
                if (!_rightPuch)
                    _anim.SetTrigger("IsShooting");
                
                // rung va am thanh dựa trên delay va cham
                break;
        }
        
        
    }

    // ========================================== ADS ==================================
    
    private void Update()
    {
        if (_type == WeaponType.Melee) return;
        ADSHandler();
    }

    /// <summary>
    /// Advance đồng hồ simulation của recoil controller (cooldown + burst continuation).
    /// CHỈ được gọi từ FixedUpdateNetwork() (hoặc hàm được gọi trực tiếp từ đó, ví dụ
    /// NCharMovementHandler.ADSHandler()). KHÔNG gọi từ Update(), UI, hay bất kỳ nơi nào
    /// ngoài simulation path — nếu không, HandleBurstTick() có thể advance burst state
    /// ngoài tick simulate và sẽ không rollback được khi Fusion resimulate.
    /// </summary>
    public void AdvanceRecoilTick()
    {
        if (Runner != null)
            _gun_Recoil_CTRL.NetworkTick(Runner.SimulationTime);
    }

    /// <summary>
    /// Query thuần "có thể bắn phát tiếp theo không", KHÔNG side-effect.
    /// Yêu cầu AdvanceRecoilTick() đã được gọi trong cùng tick trước đó
    /// (CanFire() bên dưới làm việc này giúp caller trong simulation path).
    /// </summary>
    public bool CanFireQuery() => _gun_Recoil_CTRL.CanFire;

    /// <summary>
    /// Advance tick rồi trả về CanFire. CHỈ gọi từ trong FixedUpdateNetwork
    /// (ví dụ NCharMovementHandler.ADSHandler() cho melee). Nếu cần query
    /// nhiều lần trong cùng tick mà không muốn advance lại, dùng CanFireQuery().
    /// </summary>
    public bool CanFire()
    {
        AdvanceRecoilTick();
        return CanFireQuery();
    }

    private void ADSHandler()
    {
        if (_isDead)
        {
            _ads = !_lastAdsState;
        }
        if (_ads != _lastAdsState)
        {
            _lastAdsState = _ads;
            ADSBlend(_ads);
        }
    }

    private void ADSBlend(bool ads)
    {
        _gun_Recoil_CTRL.SetAiming(ads);
        _cam_Recoil.SetADS(ads);
        if (_blendRoutine != null)
            StopCoroutine(_blendRoutine);

        float target = ads ? 1f : 0f;
        float zoom = ads ? _data.ADS_Zoom : _data.Stand_Zoom;
        float zoom_z = ads ? _data.ADS_Zoom_Z : _data.Stamd_Zoom_Z;
        float Pivot_Height = ads && _isCrouch ? _data.Ads_Pivot_Height : _data.Stand_Pivot_Height;
        _blendRoutine = StartCoroutine(ADSBlend(target, zoom, zoom_z, Pivot_Height, _data.ADS_duration));
    }

    private IEnumerator ADSBlend(float targetWeight, float zoomTarget, float zoomZ, float pivotHeight, float duration = 0.25f)
    {
        float start = _ads_Pivot.targets[0].weight;
        // float start = 0;
        // if (_ads_Pivot.targets.Count > 0 && _ads_Pivot.targets[0] != null)
        //     start = _ads_Pivot.targets[0].weight;
        
        float startzoom = _camera_Zoom.fieldOfView;
        float startzoom_z = _camera_Zoom.transform.localPosition.z;
        // float startzoom = 0;
        // float startzoom_z = 0;
        // if (_camera_Zoom != null)
        // {
        //     startzoom = _camera_Zoom.fieldOfView;
        //     startzoom_z = _camera_Zoom.transform.localPosition.z;
        // }

        float startHeight = _ads_Pivot_transform.localPosition.z;
        // float startHeight = 0;
        // if (_ads_Pivot_transform != null)
        //     startHeight = _ads_Pivot_transform.localPosition.z;
    
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

    public bool Reload()
    {
        if (!Object.HasStateAuthority) return false;
        if (CurrentMaxBullet >= _data.BulletPlusCount)
        {

            int bullet;
            if (_data.BulletPlusCount == _data.DefaultBullet)
                bullet = _data.BulletPlusCount - CurrentBulletvalue;
            else
            {
                var currentBullet = CurrentBulletvalue;
                if (currentBullet + _data.BulletPlusCount >= _data.DefaultBullet)
                    bullet = _data.DefaultBullet - CurrentBulletvalue;
                else bullet = _data.BulletPlusCount;
            }
            
            CurrentMaxBullet -= bullet;
            CurrentBulletvalue += bullet;
        }
        else if (CurrentMaxBullet > 0)
        {
            int bullet;
            if (_data.BulletPlusCount == _data.DefaultBullet)
                bullet = _data.BulletPlusCount - CurrentBulletvalue;
            else
            {
                var currentBullet = CurrentBulletvalue;
                if (currentBullet + _data.BulletPlusCount >= _data.DefaultBullet)
                    bullet = _data.DefaultBullet - CurrentBulletvalue;
                else bullet = _data.BulletPlusCount;
            }
            
            if (bullet <= CurrentMaxBullet)
            {
                CurrentBulletvalue += bullet;
                CurrentMaxBullet -= bullet;
            }
            else
            {
                CurrentBulletvalue += CurrentMaxBullet;
                CurrentMaxBullet = 0;
            }
        }
        
        if (CurrentBulletvalue >=  _data.DefaultBullet)
            return true;

        if (CurrentMaxBullet == 0)
            return true;
        
        return false;
    }

    public bool OnFire(bool right, bool crounch)
    {
        _rightPuch = right;
        _gun_Recoil_CTRL.NetworkTick(Runner.SimulationTime);
        
        switch (_type)
        {
            case WeaponType.Rifle:
            case WeaponType.Shotgun:
            case WeaponType.Sniper:
                if (CurrentBulletvalue <= 0)
                {
                    // FIX: RPC_ApplyAudio() chỉ StateAuthority được phép gửi (RpcSources.StateAuthority).
                    // OnFire() còn được gọi từ nhánh client-side prediction trong
                    // NShootHandler.FixedUpdateNetwork() (InputAuthority nhưng KHÔNG có StateAuthority)
                    // để dự đoán visual/audio ngay lập tức — gọi RPC ở nhánh đó vi phạm quyền gửi và bị
                    // Fusion reject ("Local simulation is not allowed to send this RPC").
                    // Máy đang predict chính là máy cần nghe âm thanh "hết đạn" nên phát trực tiếp cục
                    // bộ, không cần round-trip RPC tới chính mình. Chỉ khi chạy trên StateAuthority
                    // (Host, có thể điều khiển 1 máy khác) mới cần RPC để báo cho InputAuthority phát âm.
                    if (Object.HasStateAuthority)
                        RPC_ApplyAudio();
                    else if (Object.HasInputAuthority)
                        ApplyFireFailedAudio();
                    return false;
                }
                return _cam_Recoil.OnFire() && _gun_Recoil_CTRL.OnFire(_data.FireDelay);
            case WeaponType.Melee:
                float delay = !right ? _data.FireDelay : crounch ? _data.FireCrouchRightDelay : _data.FireStandRightDelay;
                return _gun_Recoil_CTRL.OnFire(delay);
            default:
                return false;
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyAudio()
    {
        ApplyFireFailedAudio();
    }

    /// <summary>
    /// Phần logic audio/coroutine thuần cục bộ, không cần dữ liệu network — factor ra khỏi
    /// RPC_ApplyAudio() để nhánh client-prediction (không có quyền gửi RPC) gọi trực tiếp.
    /// </summary>
    private void ApplyFireFailedAudio()
    {
        if (GetMode() == GunRecoilController.FiringMode.Auto)
        {
            if (_fireFailedDelay == null && gameObject.activeInHierarchy)
                _fireFailedDelay = StartCoroutine(LoadFireFailed(0.25f));
        }
        else
            AudioManager.Instance.PlayClip(_sound_shoot, _data.FireFailedClip, _data.Volume_Fire_Failed, false, 0.5f, 1f);
    }

    private IEnumerator LoadFireFailed(float delay)
    {
        AudioManager.Instance.PlayClip(_sound_shoot, _data.FireFailedClip, _data.Volume_Fire_Failed, false, 0.5f, 1f);
        yield return new WaitForSeconds(delay);
        _fireFailedDelay = null;
    }

    public void SetBullet(int currentBullet, int maxBullet)
    {
        CurrentBulletvalue = currentBullet;
        CurrentMaxBullet = maxBullet;
    }

    /// <summary>
    /// Chỉ trigger camera recoil + gun recoil visual, không check cooldown.
    /// Dùng cho remote client khi nhận FireCount từ Host — Host đã xác nhận
    /// phát bắn hợp lệ, client chỉ cần phản chiếu hiệu ứng ngay lập tức.
    /// </summary>
    public void OnFireVisualOnly()
    {
        _gun_Recoil_CTRL.TriggerRecoilVisual();
        if (_type == WeaponType.Melee) return;
        _cam_Recoil.OnFire();
    }

    public void SpawnProjectileVisual(Vector3 hitPosition, Vector3 hitNormal, bool showEffect)
    {
        if (_bullet_Prefab == null) return;
        //Debug.Log($"SpawnProjectileVisual {hitPosition}");

        var gameObjectprojectileVisual = GetBullet();
        
        if (WeaponType == WeaponType.Melee)
        {
            gameObjectprojectileVisual.transform.position = hitPosition;
            gameObjectprojectileVisual.transform.rotation = Quaternion.LookRotation(hitNormal);
        }else
        {
            gameObjectprojectileVisual.transform.position = _bullet_point.position;
            gameObjectprojectileVisual.transform.rotation = _bullet_point.rotation;
            var projectileVisual = gameObjectprojectileVisual.GetComponent<ProjectileVisual>();
            projectileVisual.SetHit(hitPosition, hitNormal, showEffect);
        }
    }
    
    private GameObject GetBullet()
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
        _poolBullet.Add(newbullet);

        return newbullet;
    }
    
    public void SwitchMode()
    {
        if (_type == WeaponType.Melee) return;
        
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
        if (_gameHudController != null)
            _gameHudController.SetWeaponMode(GetMode());
    }
    
    public GunRecoilController.FiringMode GetMode() => _gun_Recoil_CTRL.GetFiringMode();
    
    /// <summary>
    /// Gọi SetFiringMode trên controller — controller tự từ chối nếu không được phép.
    /// </summary>
    private bool TrySetMode(GunRecoilController.FiringMode mode)
    {
        if (_gun_Recoil_CTRL.SetFiringMode(mode))
        {
            _cam_Recoil.SetFiringMode(mode);
            Debug.Log($"[GunRecoilDemo] Mode → {mode}");
            _audio.PlayClip(_sound_Slider, _data.Switch_ModelClip, _data.Volume_Switch_Model);
            return true;
        }
        return false;
    }

    // Cập nhật UI khi số lượng đạn thay đổi.
    void OnBulletCountChange()
    {
        if (_gameHudController != null)
            _gameHudController.SetBullet(CurrentBulletvalue, CurrentMaxBullet);
        
        if (Object != null)
            CurrentBullet(CurrentBulletvalue);
    }
    
    // ============================================ ANIMATOR =================================================
    
    public void IsDead(bool isDead)
    {
        _isDead = isDead;
        _anim.SetBool("IsDead", isDead);
    }
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

    public void SetShotIndex(int index)
    {
        _anim.SetInteger("ShootIndex", index);
    }
    
    public bool ADS(bool ads)
    {
        if (_isDead) return false;
        _anim.SetBool("ADS", ads);
        if (_type == WeaponType.Melee) return true;
        _ads = ads;
        return true;
    }

    public bool IsReloading(bool isReloading)
    {
        if (isReloading)
        {
            if (_type == WeaponType.Melee) return false;
            if (CurrentBulletvalue == _data.DefaultBullet) return false;
            if (CurrentMaxBullet == 0)  return false;
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
    
    private void CurrentBullet(int value)
    {
        _anim.SetInteger("CurrentBullet", value);
        if (_componentManager != null && _componentManager.Animator != null)
            _componentManager.Animator.SetInteger("CurrentBullet", value);
    }
    
    
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