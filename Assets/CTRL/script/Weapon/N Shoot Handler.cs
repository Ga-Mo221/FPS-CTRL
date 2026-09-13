using System.Collections.Generic;
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

    // ── Networked stats ────────────────────────────────────────────────────
    [Networked, OnChangedRender(nameof(OnKillChange))]
    public int _kill { get; private set; } = 0;

    /// <summary>Số lần hỗ trợ kill (deal damage nhưng người khác kết liễu)</summary>
    [Networked, OnChangedRender(nameof(OnAssistChange))]
    public int Assist { get; private set; } = 0;

    [Networked] public int TotalExp { get; private set; } = 0;

    // ── Melee ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Timer delay từ lúc nhấn attack đến lúc hitbox check — đồng bộ với animation swing.
    /// Networked để Host quản lý timing chính xác theo tick.
    /// </summary>
    [Networked] private TickTimer _meleeHitTimer { get; set; }

    [Header("Melee Active Hit Window")]
    [Tooltip("Sau khi hết delay swing, hitbox check sẽ tiếp tục chạy MỖI TICK trong khoảng thời gian này (giây) thay vì chỉ 1 tick duy nhất — giúp dễ trúng đòn hơn với đối tượng đang di chuyển / độ trễ mạng.")]
    [SerializeField] private float _meleeHitWindowDuration = 0.15f;

    /// <summary>
    /// Cửa sổ thời gian (nhiều tick) mà HandleMeleeHit() được phép chạy sau khi _meleeHitTimer hết hạn.
    /// Networked để mọi tick simulate/resimulate trên Host đều nhất quán.
    /// </summary>
    [Networked] private TickTimer _meleeHitWindowEndTimer { get; set; }

    /// <summary>
    /// Các target (theo NetworkId) đã bị đánh trúng trong pha vung vũ khí hiện tại.
    /// Dùng để tránh double-hit / double-damage khi check hit lặp lại nhiều tick trong cùng 1 window.
    /// Reset mỗi khi TriggerMeleeAttack() bắt đầu 1 swing mới.
    /// </summary>
    private readonly HashSet<NetworkId> _meleeAlreadyHitThisSwing = new HashSet<NetworkId>();

    /// <summary>Đảm bảo wall-hit visual chỉ spawn 1 lần / swing dù window check nhiều tick.</summary>
    private bool _meleeWallHitRegisteredThisSwing;

    /// <summary>
    /// Counter tăng mỗi khi HandleMeleeHit() xác định cần spawn wall-hit visual.
    /// Render() trên mọi client detect thay đổi → OnMeleeHitDetected() → SpawnProjectileVisual.
    /// Tách riêng khỏi FireCount vì melee có delay giữa FireCount và hitbox check.
    /// </summary>
    [Networked] private int _meleeHitCount { get; set; }
    private int _lastMeleeHitCount;

    /// <summary>
    /// Dot product threshold để lọc target trong hình nón trước mặt.
    /// cos(50°) ≈ 0.64 → target phải nằm trong góc 50° tính từ aimForward.
    /// </summary>
    private const float MeleeDotThreshold = 0.64f;

    // ── Change detector ────────────────────────────────────────────────────
    private int _lastFireCount;
    public Vector3 _aimForward;

    // ── Exp & damage tracking (local only, không cần sync) ────────────────
    // Key: NetworkId của target, Value: tổng damage đã deal cho target đó trong vòng sống hiện tại
    private readonly Dictionary<NetworkId, int> _damageAccumulated = new Dictionary<NetworkId, int>();

    // Cache NHPHandler đã subscribe để quản lý event đúng cách
    private readonly Dictionary<NetworkId, NHPHandler> _subscribedTargets = new Dictionary<NetworkId, NHPHandler>();
    private readonly Dictionary<NetworkId, System.Action<NetworkId, int>> _onDamageDealtHandlers 
        = new Dictionary<NetworkId, System.Action<NetworkId, int>>();
    private readonly Dictionary<NetworkId, System.Action<NetworkId>> _onDiedHandlers 
        = new Dictionary<NetworkId, System.Action<NetworkId>>();
    private readonly Dictionary<NetworkId, System.Action> _onRespawnedHandlers 
        = new Dictionary<NetworkId, System.Action>();

    /// <summary>Invoke local khi nhận exp — UI subscribe để hiển thị "+Xexp"</summary>
    public event System.Action<int, bool> OnExpGained; // (amount, isKill)

    // ── Local cache ────────────────────────────────────────────────────────
    private NCharMovementHandler _movementHandler;
    private PlayerMotionController _playerMotionController;
    private CharComponentManager _componentManager;
    private WeaponManager _weaponManager;
    public NPlayer nPlayer;
    private AudioSource _hitSound;
    private PlayerData _playerData;
    private NHPHandler _nHPHandler;

    public override void Spawned()
    {
        _componentManager       = GetComponent<CharComponentManager>();
        _movementHandler        = _componentManager.NCharMovementHandler;
        _playerMotionController = _componentManager.PlayerMotionController;
        _weaponManager          = _componentManager.WeaponManager;
        nPlayer                 = _componentManager.NPlayer;
        _hitSound               = _componentManager.Audio_HitSound;
        _playerData             = _movementHandler.Data;
        _nHPHandler             = _componentManager.NHPHandler;
    }

    public int GetDeathCount() => _nHPHandler.DeadCount;

    // ── Simulation (chỉ chạy trên StateAuthority = Host) ───────────────────
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        _aimForward = _movementHandler.AimForward;

        if (_movementHandler.IsFiring)
        {
            if (_movementHandler.FireOnce())
            {
                _movementHandler.IncrementFireCount();

                if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Rifle ||
                    _weaponManager.Weapon_Active().WeaponType == WeaponType.Sniper)
                {
                    var (hitPos, hitNormal, showEffect) = GetRaycastHitInfo(_aimForward);
                    _movementHandler.SetLastShotInfo(hitPos, hitNormal, showEffect);
                }

                if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Shotgun)
                {
                    var shotInfo = GetShotgunHitInfo(_aimForward);
                    _movementHandler.SetLastShotInfo(shotInfo);
                }
            }
        }

        // Melee hit check — chạy sau delay để khớp với animation swing
        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
        {
            // Delay ban đầu (khớp animation swing) vừa hết → mở cửa sổ active hit
            if (_meleeHitTimer.Expired(Runner))
            {
                _meleeHitTimer = default;
                _meleeHitWindowEndTimer = TickTimer.CreateFromSeconds(Runner, _meleeHitWindowDuration);
                //_movementHandler.ResetRightPuch();
            }

            // Trong cửa sổ active — check hit MỖI TICK cho tới khi trúng player hoặc hết giờ,
            // thay vì chỉ check đúng 1 tick như trước → dễ trigger trúng đòn hơn nhiều.
            if (_meleeHitWindowEndTimer.IsRunning)
            {
                bool windowExpired = _meleeHitWindowEndTimer.Expired(Runner);
                bool hitConfirmed  = HandleMeleeHit(_aimForward);

                if (hitConfirmed || windowExpired)
                    _meleeHitWindowEndTimer = default;
            }
        }
    }

    public void TriggerMeleeAttack()
    {
        if (!Object.HasStateAuthority) return;

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
        {
            var weaponCtrl = _playerMotionController.ActiveWeponCTRL();
            float hitDelay = weaponCtrl?.Data.MeleeHitDelay ?? 0.2f;
            if (_movementHandler.RightPuch && weaponCtrl)
            {
                if (_movementHandler.StateFl.IsCrouch)
                    hitDelay = weaponCtrl.Data.HitCrouchRightDelay;
                else hitDelay = weaponCtrl.Data.HitStandRightDelay;
            }
            _meleeHitTimer = TickTimer.CreateFromSeconds(Runner, hitDelay);

            // FIX double-visual khi combo: hủy hẳn window của swing TRƯỚC (nếu vẫn đang chạy)
            // trước khi reset state. Nếu không, FixedUpdateNetwork() vẫn còn cho HandleMeleeHit()
            // của swing cũ chạy thêm vài tick nữa, và vì _meleeAlreadyHitThisSwing /
            // _meleeWallHitRegisteredThisSwing vừa bị Clear() bên dưới, nó tưởng đây là swing
            // "chưa hit gì" nên ghi nhận wall-hit thêm 1 lần nữa → _meleeHitCount tăng 2 lần
            // liên tiếp trong vài chục ms → Render() trên client gọi SpawnProjectileVisual 2 lần.
            _meleeHitWindowEndTimer = default;

            // Bắt đầu swing mới → reset trạng thái hit của swing trước
            _meleeAlreadyHitThisSwing.Clear();
            _meleeWallHitRegisteredThisSwing = false;
        }
    }

    // ── Render (chạy trên tất cả máy, mỗi frame) ───────────────────────────
    public override void Render()
    {
        // Rifle / Shotgun — FireCount tăng cùng tick với SetLastShotInfo nên data luôn sẵn
        if (_movementHandler.FireCount != _lastFireCount)
        {
            _lastFireCount = _movementHandler.FireCount;
            OnFireDetected();
        }

        // Melee wall-hit visual — dùng counter riêng vì có delay giữa FireCount và hitbox check.
        // Host ghi SetLastShotInfo trước rồi mới tăng _meleeHitCount trong cùng một tick,
        // nên khi client detect thay đổi thì LastShotInfo đã có data đúng.
        if (_meleeHitCount != _lastMeleeHitCount)
        {
            _lastMeleeHitCount = _meleeHitCount;
            OnMeleeHitDetected();
        }
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        var ids = new List<NetworkId>(_subscribedTargets.Keys);
        foreach (var id in ids)
            UnsubscribeTarget(id);
    }

    private void OnFireDetected()
    {
        if (!Object.HasStateAuthority)
            _playerMotionController.ActiveWeponCTRL()?.OnFireVisualOnly();

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Rifle ||
            _weaponManager.Weapon_Active()?.WeaponType == WeaponType.Sniper)
        {
            var shotInfo = _movementHandler.LastShotInfo;
            _playerMotionController
                .ActiveWeponCTRL()
                ?.SpawnProjectileVisual(shotInfo.HitPosition, shotInfo.HitNormal, shotInfo.ShowEffect);
        }

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Shotgun)
        {
            var shotInfo   = _movementHandler.LastShotInfo;
            var weaponCtrl = _playerMotionController.ActiveWeponCTRL();
            int count      = Mathf.Max(1, shotInfo.PelletCount); // fallback an toàn
            for (int i = 0; i < count; i++)
                weaponCtrl?.SpawnProjectileVisual(
                    shotInfo.GetHitPosition(i),
                    shotInfo.GetHitNormal(i),
                    shotInfo.GetShowEffect(i));
        }

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
        {
            if (_movementHandler.RightPuch)
                _movementHandler.ResetADS();
        }
        // Melee: không spawn projectile visual ở đây — OnMeleeHitDetected() xử lý riêng
        // OnFireVisualOnly() ở đầu hàm đã trigger animation recoil + camera recoil rồi
    }

    /// <summary>
    /// Gọi khi _meleeHitCount thay đổi — tức là Host vừa xác nhận có wall-hit visual cần spawn.
    /// Chạy trên mọi client, data LastShotInfo đã được Host ghi trước đó trong cùng tick.
    /// </summary>
    private void OnMeleeHitDetected()
    {
        var shotInfo = _movementHandler.LastShotInfo;
        _playerMotionController
            .ActiveWeponCTRL()
            ?.SpawnProjectileVisual(shotInfo.HitPosition, shotInfo.HitNormal, shotInfo.ShowEffect);
    }

    // ── Raycast + damage — chỉ chạy trên StateAuthority ──────────────────
    private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetRaycastHitInfo(Vector3 aimForward)
    {
        var weaponCtrl    = _playerMotionController.ActiveWeponCTRL();
        float maxDistance = weaponCtrl != null ? weaponCtrl.Data.Range : 200f;

        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS)
            origin = weaponCtrl.ADS_Shoot_Point;

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
                    Debug.DrawLine(origin, hit.Point, Color.red, 1f);
                else if (hit.Collider != null)
                    Debug.DrawLine(origin, hit.Point, Color.green, 1f);

                HandleHit(hit.Point, hit.Normal, hit.Hitbox?.transform);
                return (hit.Point, hit.Normal, showEffect);
            }
        }

        Vector3 missPoint = origin + aimForward * maxDistance;
        Debug.DrawLine(origin, missPoint, Color.green, 1f);
        return (missPoint, -aimForward, false);
    }

    // ── Shotgun: bắn N pellets trong hình nón spread ──────────────────────
    private ShotInfo GetShotgunHitInfo(Vector3 aimForward)
    {
        var weaponCtrl  = _playerMotionController.ActiveWeponCTRL();
        var weaponData  = weaponCtrl?.Data;
        float maxDist   = weaponData != null ? weaponData.Range       : 50f;
        int   pellets   = weaponData != null ? weaponData.PelletCount : 6;
        float spread    = weaponData != null ? weaponData.SpreadAngle : 5f;

        // ADS thu hẹp spread
        if (_movementHandler.StateFl.IsADS)
            spread *= 0.4f;

        pellets = Mathf.Clamp(pellets, 1, 8); // tối đa 8 slot trong ShotInfo

        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS && weaponCtrl != null)
            origin = weaponCtrl.ADS_Shoot_Point;

        var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

        ShotInfo result = new ShotInfo { PelletCount = pellets };

        for (int i = 0; i < pellets; i++)
        {
            // Tạo hướng lệch ngẫu nhiên trong hình nón
            Vector3 pelletDir = GetSpreadDirection(aimForward, spread);

            if (Runner.LagCompensation != null &&
                Runner.LagCompensation.Raycast(
                    origin, pelletDir, maxDist,
                    Object.InputAuthority, out var hit,
                    _hitMask, hitOptions))
            {
                bool showEffect = hit.Hitbox == null;
                HandleHit(hit.Point, hit.Normal, hit.Hitbox?.transform);
                result = result.SetPellet(i, hit.Point, hit.Normal, showEffect);

                if (hit.Hitbox != null)
                    Debug.DrawLine(origin, hit.Point, Color.red, 1f);
                else if (hit.Collider != null)
                    Debug.DrawLine(origin, hit.Point, Color.yellow, 1f);
            }
            else
            {
                // Miss — pellet bay hết tầm
                Vector3 missPoint = origin + pelletDir * maxDist;
                Debug.DrawLine(origin, missPoint, Color.cyan, 1f);
                result = result.SetPellet(i, missPoint, -pelletDir, false);
            }
        }

        return result;
    }

    /// <summary>
    /// Trả về hướng ngẫu nhiên lệch khỏi forward trong hình nón spreadAngle độ.
    /// </summary>
    private static Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        // Random điểm trên đĩa tròn (uniform distribution)
        float angle  = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = UnityEngine.Random.Range(0f, 1f);   // sqrt để uniform trên đĩa
        radius = Mathf.Sqrt(radius);

        // Chuyển sang local space của forward
        Quaternion rot     = Quaternion.LookRotation(forward);
        float halfAngleRad = spreadAngle * 0.5f * Mathf.Deg2Rad;
        float tanHalf      = Mathf.Tan(halfAngleRad);

        Vector3 localOffset = new Vector3(
            radius * Mathf.Cos(angle) * tanHalf,
            radius * Mathf.Sin(angle) * tanHalf,
            1f
        );

        return (rot * localOffset.normalized);
    }

    // ── Melee hit detection ───────────────────────────────────────────────
    /// <summary>
    /// OverlapSphere → Dot product (lọc phía trước) → Raycast confirm (lọc xuyên tường).
    /// Chỉ chạy trên StateAuthority sau khi _meleeHitTimer expired.
    /// Nếu trúng tường (showEffect = true): ghi SetLastShotInfo trước, tăng _meleeHitCount sau
    /// để Render() trên mọi client nhận đúng data trong cùng snapshot.
    /// </summary>
    /// <summary>
    /// Được gọi mỗi tick trong suốt _meleeHitWindowDuration (không phải chỉ 1 tick).
    /// Dùng _meleeAlreadyHitThisSwing để 1 target chỉ có thể bị đánh trúng (deal damage) đúng 1 lần / swing,
    /// dù hàm này chạy lại nhiều lần liên tiếp trong cùng window.
    /// Trả về true nếu tick này vừa xác nhận trúng 1 player (báo cho FixedUpdateNetwork dừng window sớm).
    /// </summary>
    private bool HandleMeleeHit(Vector3 aimForward)
    {
        var weaponCtrl  = _playerMotionController.ActiveWeponCTRL();
        float range     = weaponCtrl?.Data.Range       ?? 1.5f;
        float radius    = weaponCtrl?.Data.MeleeRadius ?? 0.6f;

        Vector3 origin       = _componentManager.Camera.transform.position;
        Vector3 sphereCenter = origin + aimForward * (range * 0.5f);

    #if UNITY_EDITOR
        DrawDebugSphere(sphereCenter, radius, Color.yellow, 1f);
        Debug.DrawLine(origin, sphereCenter, Color.white, 1f);
    #endif

        var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
        if (Runner.LagCompensation == null) return false;

        bool anyPlayerHitThisTick = false;

        // ShotInfo mặc định — showEffect = false, ghi đè ở cuối nếu cần
        var shotInfo = new ShotInfo
        {
            HitPosition = origin + aimForward * (range + radius),
            HitNormal   = -aimForward,
            ShowEffect  = false,
            PelletCount = 0
        };

        // ── Tầng 0: Raycast trung tâm ─────────────────────────────────────
        float centerRayDist = range + radius;
        bool  centerHitWall = false;

        if (Runner.LagCompensation.Raycast(
                origin, aimForward, centerRayDist,
                Object.InputAuthority, out var centerHit,
                _hitMask, hitOptions))
        {
            if (centerHit.Hitbox != null)
            {
                Transform  centerHitbox    = centerHit.Hitbox.transform;
                GameObject centerRoot      = centerHitbox.root.gameObject;
                var        centerHPHandler = centerRoot.GetComponent<NHPHandler>();
                bool       alreadyHitCenter = centerHPHandler != null &&
                                               _meleeAlreadyHitThisSwing.Contains(centerHPHandler.Object.Id);

                if (!alreadyHitCenter)
                {
                    // Trúng player — deal damage full, showEffect = false, kết thúc tick này
    #if UNITY_EDITOR
                    Debug.DrawLine(origin, centerHit.Point, Color.red, 1f);
    #endif
                    if (centerHPHandler != null)
                        _meleeAlreadyHitThisSwing.Add(centerHPHandler.Object.Id);

                    HandleHit(centerHit.Point, centerHit.Normal, centerHitbox);
                    _movementHandler.SetLastShotInfo(shotInfo); // showEffect = false
                    RPC_ApplyAudio();
                    return true;
                }
            }
            // Trúng tường — ghi nhận, chờ xem OverlapSphere có trúng player không
            else if (centerHit.Collider != null)
            {
                centerHitWall        = true;
                shotInfo.HitPosition = centerHit.Point;
                shotInfo.HitNormal   = centerHit.Normal;
    #if UNITY_EDITOR
                Debug.DrawLine(origin, centerHit.Point, Color.yellow, 1f);
    #endif
            }
        }

        // ── Tầng 1: OverlapSphere fallback ────────────────────────────────
        var hits  = new List<LagCompensatedHit>();
        int count = Runner.LagCompensation.OverlapSphere(
            sphereCenter, radius,
            Object.InputAuthority,
            hits, _hitMask, hitOptions);

        if (count > 0)
        {
            foreach (var hit in hits)
            {
                if (hit.Hitbox == null) continue;

                Transform  hitboxTransform = hit.Hitbox.transform;
                GameObject root            = hitboxTransform.root.gameObject;
                var        hpHandler       = root.GetComponent<NHPHandler>();

                if (hpHandler != null && _meleeAlreadyHitThisSwing.Contains(hpHandler.Object.Id))
                    continue;

                // ── Tầng 2: Dot product ───────────────────────────────────
                Vector3 hitboxCenter = hitboxTransform.position;
                Vector3 dirToTarget  = (hitboxCenter - origin).normalized;

                float dot = Vector3.Dot(aimForward, dirToTarget);
                if (dot < MeleeDotThreshold) continue;

                // ── Tầng 3: Raycast confirm ───────────────────────────────
                float distToHitbox = Vector3.Distance(origin, hitboxCenter) + 0.3f;

                if (!Runner.LagCompensation.Raycast(
                        origin, dirToTarget, distToHitbox,
                        Object.InputAuthority, out var confirmHit,
                        _hitMask, hitOptions))
                    continue;

                if (confirmHit.Hitbox == null) continue;
                if (confirmHit.Hitbox.transform.root.gameObject != root) continue;

                Transform confirmedHitbox = confirmHit.Hitbox.transform;
                if (hpHandler != null)
                    _meleeAlreadyHitThisSwing.Add(hpHandler.Object.Id);
                anyPlayerHitThisTick = true;
    #if UNITY_EDITOR
                Debug.DrawLine(origin, confirmHit.Point, Color.magenta, 1f);
    #endif
                // Trúng player qua OverlapSphere — damage giảm 1/2
                HandleHit(confirmHit.Point, confirmHit.Normal, confirmedHitbox, 0.5f);
            }
        }

        // Wall-hit visual — chỉ spawn 1 lần / swing (dù window chạy nhiều tick),
        // và chỉ khi chưa từng trúng player nào trong suốt swing này.
        if (centerHitWall && !anyPlayerHitThisTick &&
            _meleeAlreadyHitThisSwing.Count == 0 && !_meleeWallHitRegisteredThisSwing)
        {
            shotInfo.ShowEffect = true;
            _meleeWallHitRegisteredThisSwing = true;
            // Ghi data trước, trigger Render() sau — đảm bảo client đọc đúng LastShotInfo
            _movementHandler.SetLastShotInfo(shotInfo);
            _meleeHitCount++;
        }

        if (anyPlayerHitThisTick)
            RPC_ApplyAudio();

        return anyPlayerHitThisTick;
    }

    // ── Debug helpers ────────────────────────────────────────────────────
#if UNITY_EDITOR
    private static void DrawDebugSphere(Vector3 center, float radius, Color color, float duration)
    {
        int segments = 16;
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step * Mathf.Deg2Rad;
            float a2 = (i + 1) * step * Mathf.Deg2Rad;
            // Vòng XY
            Debug.DrawLine(
                center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * radius,
                center + new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0) * radius,
                color, duration);
            // Vòng XZ
            Debug.DrawLine(
                center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius,
                center + new Vector3(Mathf.Cos(a2), 0, Mathf.Sin(a2)) * radius,
                color, duration);
            // Vòng YZ
            Debug.DrawLine(
                center + new Vector3(0, Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                center + new Vector3(0, Mathf.Cos(a2), Mathf.Sin(a2)) * radius,
                color, duration);
        }
    }
#endif

    private void HandleHit(Vector3 point, Vector3 normal, Transform hitbox, float damageMultiplier = 1f)
    {
        if (hitbox == null) return;

        var hitNplayer = hitbox.root.gameObject.GetComponent<NPlayer>();
        var HPHandler  = hitbox.root.gameObject.GetComponent<NHPHandler>();
        var bodyParts  = hitbox.GetComponent<BodyPartsHandler>();

        // Fix: cần HPHandler, hitNplayer VÀ bodyParts đều hợp lệ mới tiếp tục
        if (HPHandler == null || hitNplayer == null || bodyParts == null) return;

        if (nPlayer.GamePlayMode == GamePlayMode.Solo)
            HitDamage(HPHandler, bodyParts, hitNplayer, damageMultiplier);
        else if (nPlayer.NetworkedTeam != hitNplayer.NetworkedTeam)
            HitDamage(HPHandler, bodyParts, hitNplayer, damageMultiplier);
    }

    private void HitDamage(NHPHandler HPHandler, BodyPartsHandler bodyParts, NPlayer targetPlayer, float damageMultiplier = 1f)
    {
        var ctrl   = _weaponManager?.Weapon_Active();
        var damage = ctrl != null ? ctrl.Data.Damage : 10;
        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee && _movementHandler.RightPuch)
            damage = ctrl != null ? ctrl.Data.RightDamage : 20;

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

        damage = Mathf.RoundToInt(damage * damageMultiplier);

        // Subscribe event của target nếu chưa theo dõi
        SubscribeTarget(HPHandler);

        var weaponIcon = ctrl?.Data.WeaponName;

        // OnTakeDamage sẽ invoke OnDamageDealt bên trong → _damageAccumulated update
        bool isKill = HPHandler.OnTakeDamage(damage, nPlayer.Nickname.ToString(), weaponIcon, Object.Id);

        if (isKill)
        {
            // Lấy tổng damage đã tích lũy (bao gồm phát vừa deal)
            var targetId = HPHandler.Object.Id;
            int totalDmg = _damageAccumulated.TryGetValue(targetId, out var acc) ? acc : 0;
            GainExpFromDamage(targetId, totalDmg, HPHandler.MaxHP, isKill: true);
            _kill++;
        }

        RPC_ApplyAudio();
    }

    // ── Subscribe / Unsubscribe event của target ──────────────────────────
    private void SubscribeTarget(NHPHandler HPHandler)
    {
        var targetId = HPHandler.Object.Id;
        if (_subscribedTargets.ContainsKey(targetId)) return;

        _subscribedTargets[targetId] = HPHandler;
        var capturedId = targetId;

        // Lưu lại delegate để có thể unsubscribe sau
        System.Action<NetworkId, int> onDamage = (attackerId, actualDmg) =>
            OnTargetDamaged(capturedId, attackerId, actualDmg);

        System.Action<NetworkId> onDied = (killerId) =>
            OnTargetDied(capturedId, killerId, HPHandler.MaxHP);

        System.Action onRespawned = () =>
            OnTargetRespawned(capturedId);

        HPHandler.OnDamageDealt += onDamage;
        HPHandler.OnDied        += onDied;
        HPHandler.OnRespawned   += onRespawned;

        _onDamageDealtHandlers[targetId] = onDamage;
        _onDiedHandlers[targetId]        = onDied;
        _onRespawnedHandlers[targetId]   = onRespawned;
    }
    
    private void UnsubscribeTarget(NetworkId targetId)
    {
        if (!_subscribedTargets.TryGetValue(targetId, out var HPHandler)) return;
        if (HPHandler == null) { CleanupTarget(targetId); return; }

        if (_onDamageDealtHandlers.TryGetValue(targetId, out var onDamage))
            HPHandler.OnDamageDealt -= onDamage;

        if (_onDiedHandlers.TryGetValue(targetId, out var onDied))
            HPHandler.OnDied -= onDied;

        if (_onRespawnedHandlers.TryGetValue(targetId, out var onRespawned))
            HPHandler.OnRespawned -= onRespawned;

        CleanupTarget(targetId);
    }

    private void CleanupTarget(NetworkId targetId)
    {
        _subscribedTargets.Remove(targetId);
        _onDamageDealtHandlers.Remove(targetId);
        _onDiedHandlers.Remove(targetId);
        _onRespawnedHandlers.Remove(targetId);
        _damageAccumulated.Remove(targetId);
    }

    // ── Callbacks từ NHPHandler ───────────────────────────────────────────

    /// <summary>Tích lũy damage do MÌNH deal cho target</summary>
    private void OnTargetDamaged(NetworkId targetId, NetworkId attackerId, int actualDamage)
    {
        if (Object == null || !Object.IsValid) return;
        // Chỉ tính damage của mình
        if (attackerId != Object.Id) return;

        if (!_damageAccumulated.ContainsKey(targetId))
            _damageAccumulated[targetId] = 0;

        _damageAccumulated[targetId] += actualDamage;
    }

    /// <summary>Target chết — nếu không phải mình kill thì tính assist + exp</summary>
    private void OnTargetDied(NetworkId targetId, NetworkId killerId, int targetMaxHP)
    {
        if (Object == null || !Object.IsValid) return;
        // Mình kill → đã xử lý trong HitDamage, bỏ qua
        if (killerId == Object.Id) return;

        if (!_damageAccumulated.TryGetValue(targetId, out int totalDmg) || totalDmg <= 0)
            return;

        // Assist: deal damage nhưng người khác kết liễu
        GainExpFromDamage(targetId, totalDmg, targetMaxHP, isKill: false);
        Assist++;
    }

    /// <summary>Target respawn → reset tích lũy, bắt đầu vòng sống mới</summary>
    private void OnTargetRespawned(NetworkId targetId)
    {
        if (Object == null || !Object.IsValid) return;
        _damageAccumulated.Remove(targetId);
        // Không unsubscribe vì target có thể bị bắn lại trong vòng tiếp theo
    }

    // ── Exp logic (local only) ────────────────────────────────────────────

    /// <summary>
    /// Tính và trao exp dựa trên tổng damage đã deal so với maxHP của target.
    /// - Kill:   có thể nhận tối đa 100% baseExp nếu target còn đủ máu khi bắt đầu bị mình đánh
    /// - Assist: nhận theo tỉ lệ damage / maxHP
    /// </summary>
    private void GainExpFromDamage(NetworkId targetId, int totalDamage, int targetMaxHP, bool isKill)
    {
        if (targetMaxHP <= 0) return;

        float expPercent    = Mathf.Clamp01((float)totalDamage / targetMaxHP);
        int _baseExpPerKill = 100;
        if (GameManager.Instance != null)
            _baseExpPerKill = GameManager.Instance.GetEXPperKill();
        int expGained = Mathf.RoundToInt(_baseExpPerKill * expPercent);

        if (expGained <= 0)
        {
            _damageAccumulated.Remove(targetId);
            return;
        }

        TotalExp += expGained;
        RPC_ShowExpUI(expGained, isKill);
        _damageAccumulated.Remove(targetId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShowExpUI(int amount, bool isKill)
    {
        OnExpGained?.Invoke(amount, isKill);
    }

    // ── RPC ───────────────────────────────────────────────────────────────
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyAudio()
    {
        AudioManager.Instance.PlayClip(_hitSound, _playerData.TakeDamageClips, _playerData.Volume_TakeDamage, true, 0.5f, 1f);

        var HUD = _componentManager.GameHudController();
        if (HUD != null)
            HUD.CrosshairHit();
    }

    // ── Grenade Exp Integration ───────────────────────────────────────────

    /// <summary>
    /// GrenadeHandler gọi trước OnTakeDamage để subscribe event của target.
    /// Đảm bảo _damageAccumulated hoạt động đúng khi grenade deal damage.
    /// </summary>
    public void OnGrenadeHit(NHPHandler targetHP)
    {
        if (!Object.HasStateAuthority) return;
        SubscribeTarget(targetHP);
    }

    /// <summary>
    /// GrenadeHandler gọi sau khi OnTakeDamage trả về isKill = true.
    /// Tính exp từ damage đã tích lũy và tăng kill count.
    /// </summary>
    public void OnGrenadeKill(NHPHandler targetHP)
    {
        if (!Object.HasStateAuthority) return;

        var targetId = targetHP.Object.Id;
        int totalDmg = _damageAccumulated.TryGetValue(targetId, out var acc) ? acc : 0;
        GainExpFromDamage(targetId, totalDmg, targetHP.MaxHP, isKill: true);
        _kill++;
    }

    // ── Grenade ───────────────────────────────────────────────────────────
    public void ThrowFrag()
    {
        if (!Object.HasStateAuthority) return;

        Runner.Spawn(_fragPrefab, _throwPointRight.position + _aimForward, Quaternion.LookRotation(_aimForward),
            Object.InputAuthority,
            (runner, spawnedGrenade) =>
            {
                spawnedGrenade.GetComponent<GrenadeHandler>().Throw(
                    _aimForward * _fore,
                    Object.InputAuthority,
                    nPlayer.Nickname.ToString(),
                    _hitMask,
                    Object.Id,
                    this);
            });
    }

    public void ThrowFlash()
    {
        if (!Object.HasStateAuthority) return;

        Runner.Spawn(_flashPrefab, _throwPointLeft.position + _aimForward, Quaternion.LookRotation(_aimForward),
            Object.InputAuthority,
            (runner, spawnedGrenade) =>
            {
                spawnedGrenade.GetComponent<GrenadeHandler>().Throw(
                    _aimForward * _fore,
                    Object.InputAuthority,
                    nPlayer.Nickname.ToString(),
                    _hitMask,
                    Object.Id,
                    this);
            });
    }

    // ── OnChanged callbacks ────────────────────────────────────────────────
    void OnKillChange()
    {
        if (GameManager.Instance != null && Object.HasInputAuthority)
            GameManager.Instance.SetBestStreak();
    }

    void OnAssistChange()
    {
        if (!Object.HasInputAuthority) return;
        Debug.Log($"[Stats] Assists: {Assist}");
        // GameManager.Instance?.UpdateAssist(_assist);
    }
}