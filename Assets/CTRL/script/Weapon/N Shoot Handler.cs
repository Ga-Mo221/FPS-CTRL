using Fusion;
using UnityEngine;

/// <summary>
/// Xử lý bắn súng qua mạng. Chạy trên tất cả máy, đảm bảo:
///   - FireOnce()              → chạy local trên mọi máy (animation, recoil, sound)
///   - SpawnProjectileVisual() → chạy local trên mọi máy (muzzle flash, bullet tracer)
///   - Raycast / damage        → chỉ chạy trên StateAuthority (Host)
/// </summary>
[DefaultExecutionOrder(25)] // sau NCharMovementHandler (20)
public class NShootHandler : NetworkBehaviour
{
    [Header("Projectile Visual")]
    [SerializeField] private GameObject _bulletTracerPrefab;   // assign prefab tracer/visual
    [SerializeField] private Transform  _bulletPoint;          // đầu nòng súng

    [Header("Hit Detection")]
    [SerializeField] private LayerMask _hitMask;
    [SerializeField] private float     _maxRange = 200f;

    // ── Networked fire counter ──────────────────────────────────────────────
    // Mỗi lần bắn, Host tăng counter này lên 1.
    // Tất cả máy detect sự thay đổi trong Render() → gọi local effects.
    [Networked] private int _fireCount { get; set; }

    // Lưu AimForward tại thời điểm bắn để spawn visual đúng hướng
    [Networked] private Vector3 _aimForward { get; set; }

    // ── Change detector ────────────────────────────────────────────────────
    private ChangeDetector _changeDetector;

    // ── Local cache ────────────────────────────────────────────────────────
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private CharComponentManager _componentManager;

    public override void Spawned()
    {
        _componentManager        = GetComponent<CharComponentManager>();
        _movementHandler         = _componentManager.NCharMovementHandler;
        _playerMotionController  = _componentManager.PlayerMotionController;

        // Khởi tạo ChangeDetector — theo dõi thay đổi của networked properties
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    // ── Simulation (chỉ chạy trên StateAuthority = Host) ───────────────────
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (_movementHandler.IsFiring)
        {
            // Lấy AimForward từ input authority (đã được lưu trong NCharMovementHandler)
            // hoặc dùng transform.forward nếu không có
            _aimForward = GetAimForward();

            // Tăng counter → tất cả máy sẽ detect thay đổi trong Render()
            _fireCount++;

            // Raycast / damage chỉ chạy trên Host
            DoRaycast(_aimForward);

            // Gọi FireOnce() trên Host (animation + recoil phía Host)
            _movementHandler.FireOnce();
        }
    }

    // ── Render (chạy trên tất cả máy, mỗi frame) ───────────────────────────
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(_fireCount):
                    // _fireCount vừa thay đổi → có phát súng mới
                    OnFireDetected();
                    break;
            }
        }
    }

    // ── Được gọi trên TẤT CẢ máy khi phát hiện _fireCount đổi ────────────
    private void OnFireDetected()
    {
        // Remote clients và Host đều chạy:
        // 1. FireOnce() — animation body + recoil weapon
        //    Với Host thì đã gọi trong FixedUpdateNetwork(), nhưng gọi lại lần
        //    này từ Render() sẽ bị duplicate. Dùng flag để tránh:
        if (!Object.HasStateAuthority)
        {
            _movementHandler.FireOnce();
        }

        // 2. Spawn visual (muzzle flash, bullet tracer) — local trên mọi máy
        SpawnProjectileVisual(_aimForward);
    }

    // ── Visual effect — local, không cần sync ──────────────────────────────
    private void SpawnProjectileVisual(Vector3 aimForward)
    {
        if (_bulletTracerPrefab == null || _bulletPoint == null) return;

        // Spawn tracer/visual tại đầu nòng súng, hướng theo aimForward
        var go = Instantiate(_bulletTracerPrefab, _bulletPoint.position, Quaternion.LookRotation(aimForward));

        // Tự hủy sau 3 giây (hoặc handle trong prefab)
        Destroy(go, 3f);
    }

    // ── Raycast + damage — chỉ chạy trên StateAuthority ───────────────────
    private void DoRaycast(Vector3 aimForward)
    {
        // LagCompensated raycast (cần NetworkProjectConfig bật Lag Compensation)
        // Fallback sang Physics.Raycast nếu chưa bật
        Vector3 origin = _bulletPoint != null ? _bulletPoint.position : transform.position;

        if (Runner.LagCompensation != null)
        {
            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
            if (Runner.LagCompensation.Raycast(
                    origin, aimForward, _maxRange,
                    Object.InputAuthority, out var lagHit,
                    _hitMask, hitOptions))
            {
                HandleHit(lagHit.Point, lagHit.Normal, lagHit.Hitbox?.transform.root);
            }
        }
        else
        {
            if (Physics.Raycast(origin, aimForward, out RaycastHit physHit, _maxRange, _hitMask))
            {
                HandleHit(physHit.point, physHit.normal, physHit.collider.transform.root);
            }
        }
    }

    private void HandleHit(Vector3 point, Vector3 normal, Transform root)
    {
        if (root != null)
            Debug.Log($"[NShootHandler] {transform.name} hit {root.name} at {point}");

        // TODO: ApplyDamage(root, point, normal);
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private Vector3 GetAimForward()
    {
        // Ưu tiên lấy từ camera nếu là local player, ngược lại dùng transform.forward
        var cam = _componentManager.Camera;
        if (cam != null && Object.HasInputAuthority)
            return cam.transform.forward;
        return transform.forward;
    }
}