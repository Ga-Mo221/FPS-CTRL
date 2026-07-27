using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using SimpleFPS;
using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponController : NetworkBehaviour
{
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private WeaponData _data;
    
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private Animator _anim;
    
    [TabGroup("Weapon Controller", "Runtime")]
    [SerializeField] private NWeaponManager  _manager;

    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private TransformConstraint _ads_Pivot;
    
    [TabGroup("Weapon Controller", "Transform")]
    [BoxGroup("Weapon Controller/Transform/Hand List")]
    [SerializeField] private Transform[] _list_Bone_Body;
    
    [BoxGroup("Weapon Controller/Transform/Grenade List")]
    [SerializeField] private Transform[] _list_Bone_Grenade;
    [HorizontalGroup("Weapon Controller/Transform/Grenade List/grenade")]
    [HideLabel]
    [SerializeField] private string _grenade_Flash_Name = "RAW_Flash";
    [HorizontalGroup("Weapon Controller/Transform/Grenade List/grenade")]
    [HideLabel]
    [SerializeField] private string _grenade_Frag_Name = "RAW_Frag";
    
    [HorizontalGroup("Weapon Controller/Transform/camera", width: 0.6f)]
    [SerializeField] private Transform _camera_Shake;
    [HorizontalGroup("Weapon Controller/Transform/camera")]
    [HideLabel]
    [SerializeField] private string _camera_ShakeName = "RAW_Camera";
    [TabGroup("Weapon Controller", "Runtime")]
    [SerializeField] private Camera _camera_Zoom;
    
    [BoxGroup("Weapon Controller/Transform/IK Left")]
    [HorizontalGroup("Weapon Controller/Transform/IK Left/IKhand_Left", width: 0.6f)]
    [SerializeField] private Transform _hand_Left;
    [HorizontalGroup("Weapon Controller/Transform/IK Left/IKhand_Left")] 
    [HideLabel]
    [SerializeField] private string _hand_Left_Name = "RAW_Hand_L";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Left/Elbow_Left", width: 0.6f)]
    [SerializeField] private Transform _pole_Left;
    [HorizontalGroup("Weapon Controller/Transform/IK Left/Elbow_Left")]
    [HideLabel]
    [SerializeField] private string _Elbow_Left_Name = "RAW_Elbow_L";
    
    [BoxGroup("Weapon Controller/Transform/IK Right")]
    [HorizontalGroup("Weapon Controller/Transform/IK Right/IKhand_Right", width: 0.6f)]
    [SerializeField] private Transform _hand_Right;
    [HorizontalGroup("Weapon Controller/Transform/IK Right/IKhand_Right")] 
    [HideLabel]
    [SerializeField] private string _hand_Right_Name = "RAW_Hand_R";
    
    [HorizontalGroup("Weapon Controller/Transform/IK Right/Elbow_Right", width: 0.6f)]
    [SerializeField] private Transform _pole_Right;
    [HorizontalGroup("Weapon Controller/Transform/IK Right/Elbow_Right")]
    [HideLabel]
    [SerializeField] private string _Elbow_Right_Name = "RAW_Elbow_R";
    
    // FIX: không dùng bool isFiring set qua Coroutine nữa (gây InvalidOperationException
    // khi coroutine resume ngoài simulation context, và không chạy trên proxy/client khác).
    // Thay bằng bộ đếm FireTick — tăng ngay trong tick lúc bắn, Render() trên MỌI peer sẽ
    // tự phát hiện thay đổi qua ChangeDetector và phát hiệu ứng tương ứng.
    [Networked] public int FireTick { get; set; }

    // Kết quả raycast (chỉ server tính, dùng chung struct ShotInfo có sẵn trong N_Input_Data.cs)
    // được đồng bộ cho mọi client để hiển thị đúng điểm trúng đạn / hiệu ứng.
    [Networked] public ShotInfo LastShot { get; set; }

    private Vector3 _aimForward;
    private ChangeDetector _changeDetector;
    
    private bool _ads;
    private bool _lastAdsState;
    private Coroutine _blendRoutine;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private GunRecoilController _gun_Recoil_CTRL;
    public GunRecoilController GunRecoilCTRL => _gun_Recoil_CTRL;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private WeaponSway _sway;
    [TabGroup("Weapon Controller", "Weapon Data")]
    [SerializeField] private CameraRecoil _cam_Recoil;
    
    [BoxGroup("Weapon Controller/Transform/Bullet")]
    [HorizontalGroup("Weapon Controller/Transform/Bullet/row", width:0.6f)]
    [SerializeField] private Transform _bullet_point;
    [HorizontalGroup("Weapon Controller/Transform/Bullet/row")]
    [HideLabel]
    [SerializeField] private string _bullet_point_name = "Bullet Point";
    
    [BoxGroup("Weapon Controller/Weapon Data/Bullet")]
    [SerializeField] private GameObject _bullet_Prefab;
    [HorizontalGroup("Weapon Controller/Weapon Data/Bullet/row", width: 0.6f)]
    [SerializeField] private GameObject _shoot_Effect;
    [HorizontalGroup("Weapon Controller/Weapon Data/Bullet/row")]
    [HideLabel]
    [SerializeField] private string _bullet_effect_name = "Shoot Effect";
    [SerializeField] private LayerMask _hitMask; // set trong Inspector: layer Player + layer Environment

    public Transform Weapon_Parrent;
    private Transform _ads_Pivot_transform;
    private bool _isCrouch;
    public void SetAdsPivotTransform(Transform transform) =>  _ads_Pivot_transform = transform;
    //public Vector3 Bullet_point => _bullet_point.position;
    
    private AudioManager _audio;
    [SerializeField] private AudioSource _sound;
    
    // =============================== public Filed ============================================

    public NWeaponManager Manager
    {
        set => _manager = value;
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
    public AudioSource Sound => _sound;
    

    private void Awake()
    {
        if (_anim == null)
            _anim = GetComponentInChildren<Animator>();
        if (_anim == null) Debug.LogError("Không tìm thấy Animator!");
        if (_data == null) return;
        _cam_Recoil?.LoadData(_data);
        _gun_Recoil_CTRL?.ReloadData(_data);
    }
    
    // ====================================== EVENT ONFIRE =========================
    
    //private event Action<GunRecoilController.FiringMode> OnShotFired;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    void Start()
    {
        // FIX: subscribe lại OnShotFired, nhưng handler giờ KHÔNG dùng Coroutine nữa —
        // chạy đồng bộ ngay trong lời gọi (an toàn vì OnShotFired chỉ được invoke từ bên
        // trong OnFire()/NetworkTick(), cả hai đều được WeaponController gọi từ trong
        // FixedUpdateNetwork). Điều này cũng xử lý đúng các phát 2, 3 của Burst — vốn được
        // GunRecoilController tự kích hoạt qua NetworkTick() mỗi tick, không chỉ phát đầu.
        _gun_Recoil_CTRL.OnShotFired += HandleShot;
        _audio = AudioManager.Instance;
    }

    void OnDestroy()
    {
        _gun_Recoil_CTRL.OnShotFired -= HandleShot;
    }

    // FIX: không Coroutine, không WaitForSeconds — chỉ tăng FireTick + raycast (nếu server)
    // ngay lập tức, trong đúng simulation context của tick hiện tại.
    private void HandleShot(GunRecoilController.FiringMode mode)
    {
        FireTick++;

        if (Object.HasStateAuthority)
        {
            var (hitPos, hitNormal, show) = GetRaycastHitInfo(_aimForward);
            LastShot = new ShotInfo
            {
                HitPosition = hitPos,
                HitNormal = hitNormal,
                ShowEffect = show
            };
        }
    }

    /// <summary>
    /// FIX: gọi hàm này mỗi network tick từ NCharMovementHandler.FixedUpdateNetwork,
    /// bất kể có bắn hay không — để GunRecoilController.NetworkTick() có cơ hội tự
    /// tiếp diễn các phát bắn còn lại của Burst đúng theo network time.
    /// </summary>
    public void NetworkTick(float networkTime)
    {
        _gun_Recoil_CTRL.NetworkTick(networkTime);
    }

    // FIX: Render() chạy trên mọi peer (host, client bắn, VÀ client khác đang xem proxy).
    // Đây là nơi DUY NHẤT nên phát hiệu ứng thị giác/âm thanh của phát bắn.
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(FireTick))
            {
                PlayFireEffect();
            }
        }
    }

    // FIX: tách hẳn phần hiệu ứng cosmetic ra khỏi logic networked.
    // Hàm này an toàn để gọi từ Render() trên bất kỳ peer nào, không đụng vào
    // [Networked] property nào cả (chỉ ĐỌC LastShot, không ghi).
    private void PlayFireEffect()
    {
        _anim.SetTrigger("IsShooting");
        if (_audio != null) _audio.PlayClip(_sound, Data.FireClip, Data.Volume_Fire);

        // reset effect
        _shoot_Effect.SetActive(false);
        _shoot_Effect.SetActive(true);

        var shot = LastShot;
        SpawnProjectileVisual(shot.HitPosition, shot.HitNormal, shot.ShowEffect);
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
            float Pivot_Height = _ads && _isCrouch ? _data.Ads_Pivot_Height : _data.Stand_Pivot_Height;
            _blendRoutine = StartCoroutine(ADSBlend(target, zoom, zoom_y, Pivot_Height, _data.ADS_duration));
        }
    }

    private IEnumerator ADSBlend(float targetWeight, float zoomTarget, float zoomY, float pivotHeight, float duration = 0.25f)
    {
        // XÓA HOÀN TOÀN 4 DÒNG CHECK APPROXIMATELY Ở ĐÂY
    
        float start = _ads_Pivot.targets[0].weight;
        float startzoom = _camera_Zoom.fieldOfView;
        float startzoom_y = _camera_Zoom.transform.localPosition.y;
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
            _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, Mathf.Lerp(startzoom_y, zoomY, progress), _zoomYtarget.z);
            _ads_Pivot_transform.localPosition = new Vector3(PivotHeight.x, PivotHeight.y, Mathf.Lerp(startHeight, pivotHeight, progress));
        
            yield return null;
        }

        // Đảm bảo gán chính xác giá trị cuối cùng khi kết thúc
        _ads_Pivot.targets[0].weight = targetWeight;
        _sway.SetWeight(targetWeight);
        _camera_Zoom.fieldOfView = zoomTarget;
        _camera_Zoom.transform.localPosition = new Vector3(_zoomYtarget.x, zoomY, _zoomYtarget.z);
        _ads_Pivot_transform.transform.localPosition = new Vector3(PivotHeight.x, PivotHeight.y, pivotHeight);
        _blendRoutine = null;
    }
    
    // ======================= WEAPON SWAY =============================
    
    public void SetLookDelta(Vector2 value) => _sway.SetLookDelta(value);
    public void SetMoveDirection(Vector2 value) => _sway.SetMoveDirection(value);
    
    // ======================= CAMERA RECOIL ===========================
    
    public void SetCameraCTRL(Transform camera) => _cam_Recoil.SetCameraCTRL(camera);
    
    // ======================= FIRE ====================================

    // FIX: toàn bộ hàm này giờ chạy hoàn toàn đồng bộ bên trong FixedUpdateNetwork
    // (được gọi từ NCharMovementHandler.FireOnce). Không còn Coroutine, không còn
    // WaitForSeconds — mọi thao tác trên [Networked] property xảy ra đúng trong
    // simulation context của tick hiện tại, an toàn cho cả resimulation/rollback.
    public bool OnFire(Vector3 aimForward)
    {
        _aimForward = aimForward;

        // FIX: networkTime lấy từ Runner.SimulationTime — deterministic theo tick,
        // được truyền xuống GunRecoilController thay vì để nó tự đọc Time.time.
        float networkTime = (float)Runner.SimulationTime;

        // Camera recoil + gun recoil (procedural, cosmetic, không networked) tự quyết định
        // súng có thực sự bắn được không (cooldown, curve...). Nếu bắn thành công,
        // GunRecoilController sẽ tự invoke OnShotFired → HandleShot() ở trên lo phần
        // FireTick/raycast, nên KHÔNG cần lặp lại ở đây nữa.
        if (!_cam_Recoil.OnFire() || !_gun_Recoil_CTRL.OnFire(networkTime))
            return false;

        return true;
    }

    private void SpawnProjectileVisual(Vector3 hitPosition, Vector3 hitNormal, bool showEffect)
    {
        if (_bullet_Prefab == null) return;
        
        var projectile = Instantiate(_bullet_Prefab, _bullet_point.position, _bullet_point.rotation);
        var projectileVisual = projectile.GetComponent<ProjectileVisual>();
        projectileVisual.SetHit(hitPosition, hitNormal, showEffect);
    }

    private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetRaycastHitInfo(Vector3 AimForward)
    {
        float maxDistance = _data.Range;
        Vector3 origin = transform.position;

        // LagCompensation chỉ available khi NetworkProjectConfig bật Lag Compensation
        // và session được khởi tạo đúng. Nếu null thì fallback Physics.Raycast thông thường.
        if (Runner.LagCompensation != null)
        {
            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
            if (Runner.LagCompensation.Raycast(
                    origin, AimForward, maxDistance,
                    Object.InputAuthority, out var hit,
                    _hitMask, hitOptions))
            {
                bool showEffect = hit.Hitbox == null;

                if (hit.Hitbox != null)
                    Debug.Log($"{Time.time} {transform.name} hit hitbox {hit.Hitbox.transform.root.name}");
                else if (hit.Collider != null)
                    Debug.Log($"{Time.time} {transform.name} hit physic collider {hit.Collider.transform.root.name}");

                return (hit.Point, hit.Normal, showEffect);
            }
        }
        else
        {
            // Fallback: Physics.Raycast thuần Unity khi LagCompensation chưa được cấu hình.
            // Để dùng LagCompensation đúng: bật "Lag Compensation" trong NetworkProjectConfig
            // và đảm bảo các collider cần hit có component HitboxRoot + Hitbox.
            if (Physics.Raycast(origin, AimForward, out RaycastHit physHit, maxDistance, _hitMask))
            {
                Debug.Log($"{Time.time} {transform.name} [Physics fallback] hit {physHit.collider.transform.root.name}");
                return (physHit.point, physHit.normal, true);
            }
        }

        Vector3 missPoint = origin + AimForward * maxDistance;
        return (missPoint, -AimForward, false);
    }

    public void SwitchMode()
    {
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
        
        List<Transform> list_bone_hand = new List<Transform>();
        List<Transform> list_bone_grenade = new List<Transform>();
        foreach (var trans in GetComponentsInChildren<Transform>(true))
        {
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

            if (trans.name.Equals(_bullet_effect_name))
                _shoot_Effect = trans.gameObject;
        }
        _list_Bone_Body = _list_Bone_Body.Concat(list_bone_hand).ToArray();
        _list_Bone_Grenade = _list_Bone_Grenade.Concat(list_bone_grenade).ToArray();
    }
#endif
}