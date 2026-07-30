using Fusion;
using UnityEngine;

[DefaultExecutionOrder(25)] // sau NCharMovementHandler (20)
public class NShootHandler : NetworkBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private LayerMask _hitMask;
    
    [Networked, OnChangedRender(nameof(OnKillChange))]
    public int _kill {get; set;} = 0;

    // ── Change detector ────────────────────────────────────────────────────
    // Theo dõi _fireCount đã được chuyển sang NCharMovementHandler
    // NShootHandler
    private int _lastFireCount;

    // ── Local cache ────────────────────────────────────────────────────────
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private CharComponentManager _componentManager;
    private WeaponManager _weaponManager;
    

    public override void Spawned()
    {
        _componentManager       = GetComponent<CharComponentManager>();
        _movementHandler        = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
        _weaponManager          = _componentManager.WeaponManager;
    }

    // ── Simulation (chỉ chạy trên StateAuthority = Host) ───────────────────
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (_movementHandler.IsFiring)
        {
            // AimForward lấy trực tiếp từ NInputData — chính xác hơn camera local
            Vector3 aimForward = _movementHandler.AimForward;

            // Gọi FireOnce() trên Host (animation + recoil)
            if (_movementHandler.FireOnce())
            {
                // Tăng FireCount trên NCharMovementHandler → mọi máy detect qua Render()
                _movementHandler.IncrementFireCount();
                
                if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Gun)
                {
                    // Lưu kết quả raycast để sync visual đúng điểm chạm
                    var (hitPos, hitNormal, showEffect) = GetRaycastHitInfo(aimForward);
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

        if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Gun)
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
                
                HandleHit(hit.Point, hit.Normal, hit.Hitbox?.transform.root);
                return (hit.Point, hit.Normal, showEffect);
            }
        }

        // Miss — Debug: ray xanh = trượt
        Vector3 missPoint = origin + aimForward * maxDistance;
        Debug.DrawLine(origin, missPoint, Color.green, 1f);
        return (missPoint, -aimForward, false);
    }
    
    private void HandleHit(Vector3 point, Vector3 normal, Transform root)
    {
        if (root == null) return;
        
        //Debug.Log($"[NShootHandler] {transform.name} hit {root.name} at {point}");

        var HPHandler = root.root.gameObject.GetComponent<NHPHandler>();
        if (HPHandler == null) return;

        if (HPHandler.OnTakeDamage(10))
            _kill++;
    }

    void OnKillChange()
    {
        //Debug.Log($"Ban Da tieu diet {_kill} ke dich");
    }
}