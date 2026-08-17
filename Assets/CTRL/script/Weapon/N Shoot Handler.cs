using Fusion;
using UnityEngine;

[DefaultExecutionOrder(25)] // sau NCharMovementHandler (20)
public class NShootHandler : NetworkBehaviour
{
    [SerializeField] private GrenadeHandler _fragPrefab;
    [SerializeField] private GrenadeHandler _flashPrefab;
    [SerializeField] private Transform _throwPointRight;
    [SerializeField] private Transform _throwPointLeft;
    [Tooltip("lực ném")]
    [SerializeField] private float _fore = 10f;
    
    [Header("Hit Detection")]
    [SerializeField] private LayerMask _hitMask;
    
    [Networked, OnChangedRender(nameof(OnKillChange))]
    public int _kill {get; set;} = 0;

    // ── Change detector ────────────────────────────────────────────────────
    // Theo dõi _fireCount đã được chuyển sang NCharMovementHandler
    // NShootHandler
    private int _lastFireCount;
    public Vector3 _aimForward;

    // ── Local cache ────────────────────────────────────────────────────────
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private CharComponentManager _componentManager;
    private WeaponManager _weaponManager;
    public NPlayer nPlayer;
    private AudioSource _hitSound;
    private PlayerData _playerData;

    public override void Spawned()
    {
        _componentManager       = GetComponent<CharComponentManager>();
        _movementHandler        = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
        _weaponManager          = _componentManager.WeaponManager;
        nPlayer                 = _componentManager.NPlayer;
        _hitSound               = _componentManager.Audio_HitSound;
        _playerData             = _movementHandler.Data;
    }

    // ── Simulation (chỉ chạy trên StateAuthority = Host) ───────────────────
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // AimForward lấy trực tiếp từ NInputData — chính xác hơn camera local
        _aimForward = _movementHandler.AimForward;
        Debug.Log($"IsFiring: {_movementHandler.IsFiring}");
        if (_movementHandler.IsFiring)
        {
            // Gọi FireOnce() trên Host (animation + recoil)
            if (_movementHandler.FireOnce())
            {
                // Tăng FireCount trên NCharMovementHandler → mọi máy detect qua Render()
                _movementHandler.IncrementFireCount();
                
                if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Gun)
                {
                    // Lưu kết quả raycast để sync visual đúng điểm chạm
                    var (hitPos, hitNormal, showEffect) = GetRaycastHitInfo(_aimForward);
                    _movementHandler.SetLastShotInfo(hitPos, hitNormal, showEffect);
                }
            }
        }
    }

    // ── Render (chạy trên tất cả máy, mỗi frame) ───────────────────────────

    public override void Render()
    {
        if (_movementHandler.FireCount != _lastFireCount)
        {
            _lastFireCount = _movementHandler.FireCount;
            OnFireDetected();
        }
    }

    // ── Được gọi trên TẤT CẢ máy khi FireCount thay đổi ──────────────────
    private void OnFireDetected()
    {
        // Remote client gọi FireOnce() (Host đã gọi trong FixedUpdateNetwork)
        if (!Object.HasStateAuthority)
        {
            _movementHandler.FireOnce();
        }

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Gun)
        {
            // Spawn visual thông qua WeaponController — mỗi súng dùng prefab đạn riêng
            var shotInfo = _movementHandler.LastShotInfo;
            _playerMotionController
                .ActiveWeponCTRL()
                ?.SpawnProjectileVisual(shotInfo.HitPosition, shotInfo.HitNormal, shotInfo.ShowEffect);
        }
    }

    // ── Raycast + damage — chỉ chạy trên StateAuthority ──────────────────
    private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetRaycastHitInfo(Vector3 aimForward)
    {
        var weaponCtrl = _playerMotionController.ActiveWeponCTRL();
        float maxDistance = weaponCtrl != null ? weaponCtrl.Data.Range : 200f;

        // Lấy origin từ bullet point của weapon nếu có, fallback về transform
        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS)
        {
            origin = weaponCtrl.ADS_Shoot_Point;
        }

        if (Runner.LagCompensation != null)
        {
            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
            if (Runner.LagCompensation.Raycast(
                    origin, aimForward, maxDistance,
                    Object.InputAuthority, out var hit,
                    _hitMask, hitOptions))
            {
                bool showEffect = hit.Hitbox == null;

                if (hit.Hitbox != null)
                {
                    //Debug.Log($"{Time.time} {transform.name} hit hitbox {hit.Hitbox.transform.root.name}");
                    Debug.DrawLine(origin, hit.Point, Color.red, 1f);
                }
                else if (hit.Collider != null)
                {
                    //Debug.Log($"{Time.time} {transform.name} hit physic collider {hit.Collider.transform.name} => {hit.Collider.transform.root.name} hit point {hit.Point}");
                    Debug.DrawLine(origin, hit.Point, Color.green, 1f);
                }
                
                HandleHit(hit.Point, hit.Normal, hit.Hitbox?.transform);
                return (hit.Point, hit.Normal, showEffect);
            }
        }

        // Miss — Debug: ray xanh = trượt
        Vector3 missPoint = origin + aimForward * maxDistance;
        Debug.DrawLine(origin, missPoint, Color.green, 1f);
        return (missPoint, -aimForward, false);
    }
    
    private void HandleHit(Vector3 point, Vector3 normal, Transform hitbox)
    {
        if (hitbox == null) return;
        
        //Debug.Log($"[NShootHandler] {transform.name} hit {root.name} at {point}");
        
        var hitNplayer = hitbox.root.gameObject.GetComponent<NPlayer>();
        var HPHandler = hitbox.root.gameObject.GetComponent<NHPHandler>();
        var bodyParts = hitbox.GetComponent<BodyPartsHandler>();
        if (HPHandler == null && hitNplayer == null && bodyParts == null) return;
        
        if (nPlayer.GamePlayMode == GamePlayMode.Solo)
            HitDamage(HPHandler, bodyParts);
        else if (nPlayer.NetworkedTeam != hitNplayer.NetworkedTeam)
            HitDamage(HPHandler, bodyParts);
    }

    private void HitDamage(NHPHandler HPHandler, BodyPartsHandler bodyParts)
    {
        var ctrl = _weaponManager != null ? _weaponManager.Weapon_Active() : null;
        var damage = ctrl != null ? ctrl.Data.Damage : 10;

        switch (bodyParts.GetBodyParts())
        {
            case BodyParts.Head:
                damage *= 3;
                break;
            case BodyParts.Legs:
                damage = Mathf.RoundToInt(damage * 0.5f);
                break;
            case BodyParts.Hand:
            case BodyParts.Body:
                break;
        }
        
        if (HPHandler.OnTakeDamage(damage, nPlayer.Nickname.ToString(), Object.Id))
            _kill++;
        
        RPC_ApplyAudio();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyAudio()
    {
        AudioManager.Instance.PlayClip(_hitSound, _playerData.TakeDamageClips, _playerData.Volume_TakeDamage, true, 0.5f, 1f);
    }

    public void ThrowFrag()
    {
        if (!Object.HasStateAuthority) return;
        
         Runner.Spawn(_fragPrefab, _throwPointRight.position + _aimForward, Quaternion.LookRotation(_aimForward),
            Object.InputAuthority,
            (runner, spawnedGrenade) =>
        {
            spawnedGrenade.GetComponent<GrenadeHandler>().Throw(_aimForward * _fore, Object.InputAuthority, nPlayer.Nickname.ToString(), _hitMask, Object.Id, this);
        });
    }
    
    public void ThrowFlash()
    {
        if (!Object.HasStateAuthority) return;
        
        Runner.Spawn(_flashPrefab, _throwPointLeft.position + _aimForward, Quaternion.LookRotation(_aimForward),
            Object.InputAuthority,
            (runner, spawnedGrenade) =>
            {
                spawnedGrenade.GetComponent<GrenadeHandler>().Throw(_aimForward * _fore, Object.InputAuthority, nPlayer.Nickname.ToString(), _hitMask, Object.Id, this);
            });
    }

    void OnKillChange()
    {
        //Debug.Log($"Ban Da tieu diet {_kill} ke dich");
    }
}