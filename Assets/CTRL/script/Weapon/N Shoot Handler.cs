using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DefaultExecutionOrder(25)] // sau NCharMovementHandler (20)
public class NShootHandler : NetworkBehaviour
{
    // [NET-OPT 2026-10-18 Aemy] (bao cao 2.2) 2 NetworkString<_16> (~34 word, UTF-32) -> 1 ushort:
    // byte thap = chi so luu dan Q, byte cao = E trong GameManager.GetGrenadeList(); 255 = mac dinh (Frag/Flash).
    // GrenadeQ/GrenadeE giu nguyen ten + kieu tra ve chuoi nen code cu (.ToString()) khong doi.
    [Networked] private ushort _grenadeSlots { get; set; } = 0xFFFF;
    public string GrenadeQ { get { return GrenadeIdOf((byte)(_grenadeSlots & 0xFF), WeaponID._FRAG_M67); } }
    public string GrenadeE { get { return GrenadeIdOf((byte)(_grenadeSlots >> 8), WeaponID._FLASH_M84); } }

    private static string GrenadeIdOf(byte idx, string fallback)
    {
        if (idx == 255) return fallback;
        var gm = GameManager.Instance;
        string id = gm != null ? gm.GetGrenadeIdByIndex(idx) : null;
        return string.IsNullOrEmpty(id) ? fallback : id;
    }

    private static byte GrenadeIndexOf(string id)
    {
        var gm = GameManager.Instance;
        int i = gm != null ? gm.GetGrenadeIndex(id) : -1;
        return i >= 0 && i < 255 ? (byte)i : (byte)255;
    }
    
    [SerializeField] private Transform _throwPointRight;
    [SerializeField] private Transform _throwPointLeft;

    /// <summary>
    /// dung de phan biet nem Q hay E, 0 la khong nem, 1 la Q va 2 la E
    /// </summary>
    [Networked]
    public byte _grende_ID { private set; get; } = 0;
    
    [Tooltip("lực ném")]
    [SerializeField] private float _fore = 10f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask _hitMask;

    // ── Networked stats ────────────────────────────────────────────────────
    // [NET-OPT 2026-10-18 Aemy] (bao cao 9.4) _kill + Assist gop 2 ushort trong 1 word, 1 callback chung.
    [Networked, OnChangedRender(nameof(OnScoreChanged))]
    private uint _score { get; set; }
    public int _kill
    {
        get { return (int)(_score & 0xFFFF); }
        private set { _score = (_score & 0xFFFF0000u) | (uint)Mathf.Clamp(value, 0, 65535); }
    }

    /// <summary>Số lần hỗ trợ kill (deal damage nhưng người khác kết liễu)</summary>
    public int Assist
    {
        get { return (int)(_score >> 16); }
        private set { _score = (_score & 0xFFFFu) | ((uint)Mathf.Clamp(value, 0, 65535) << 16); }
    }
    private int _lastRenderedKill, _lastRenderedAssist;
    void OnScoreChanged()
    {
        if (_kill != _lastRenderedKill) { _lastRenderedKill = _kill; OnKillChange(); }
        if (Assist != _lastRenderedAssist) { _lastRenderedAssist = Assist; OnAssistChange(); }
    }

    [Networked] public int TotalExp { get; private set; } = 0;

    // ── Melee ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Timer delay từ lúc nhấn attack đến lúc hitbox check — đồng bộ với animation swing.
    /// Networked để Host quản lý timing chính xác theo tick.
    /// </summary>
    // [NET-OPT 2026-10-18 Aemy] (bao cao 3.1) _meleeHitTimer khai bao nhung khong dung -> xoa.

    [Header("Melee Active Hit Window")]
    [Tooltip("Sau khi hết delay swing, hitbox check sẽ tiếp tục chạy MỖI TICK trong khoảng thời gian này (giây) thay vì chỉ 1 tick duy nhất — giúp dễ trúng đòn hơn với đối tượng đang di chuyển / độ trễ mạng.")]
    [SerializeField] private float _meleeHitWindowDuration = 0.15f;

    /// <summary>
    /// Cửa sổ thời gian (nhiều tick) mà HandleMeleeHit() được phép chạy sau khi _meleeHitTimer hết hạn.
    /// Networked để mọi tick simulate/resimulate trên Host đều nhất quán.
    /// </summary>
    // [NET-OPT 2026-10-18 Aemy] (bao cao 3.2) cua so ~150ms chi host doc -> field thuong.
    private TickTimer _meleeHitWindowEndTimer;

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

    // ── [NET-OPT 2026-10-18 Aemy] Ket qua raycast cua host (local, khong sync) ──
    private struct LocalShot { public Vector3 Pos; public Vector3 Normal; public bool Effect; }
    private readonly LocalShot[] _hostShot = new LocalShot[8];
    private int _hostShotCount;
    private LocalShot _hostMeleeWallHit;
    private readonly List<LagCompensatedHit> _meleeHits = new List<LagCompensatedHit>();

    /// <summary>Seed shotgun theo tick (tat dinh): host va client predict cung tick -> cung seed.</summary>
    private ushort ShotSeedFor(int tick)
    {
        unchecked
        {
            uint h = (uint)tick * 2654435761u ^ (uint)Object.Id.Raw * 2246822519u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (ushort)(h & 0xFFFF);
        }
    }

    /// <summary>So gia ngau nhien tat dinh [0,1) tu (seed, k).</summary>
    private static float Hash01(ushort seed, int k)
    {
        unchecked
        {
            uint h = (uint)seed * 374761393u + (uint)k * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ── Change detector ────────────────────────────────────────────────────
    private int _lastFireCount;
    public Vector3 _aimForward;

    // ── Client-Side Prediction ─────────────────────────────────────────────
    // FIX Bug #5: đã bỏ _predictedFireTick / _predictedFireCount — không cần nữa vì
    // OnFireDetected() giờ cho InputAuthority (client thuần) return sớm hoàn toàn thay
    // vì so khớp một giá trị predicted duy nhất (không đủ khi có nhiều predicted shot
    // in-flight cùng lúc ở ping cao).

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
        // [NET-OPT 2026-10-18 Aemy] nguoi vao sau / sau migration: khong phat lai su kien cu
        _lastFireCount          = _movementHandler.FireCount;
        _lastMeleeHitCount      = _meleeHitCount;
        _lastRenderedKill       = _kill;
        _lastRenderedAssist     = Assist;
    }

    public int GetDeathCount() => _nHPHandler.DeadCount;

    public void SetGrenade(string grenade1, string grenade2)
    {
        _grenadeSlots = (ushort)(GrenadeIndexOf(grenade1) | (GrenadeIndexOf(grenade2) << 8));
    }

    /// <summary>
    /// dung de set la dang nem q hay e. 0 = khong nem, 1 = Q, 2 = E
    /// </summary>
    /// <param name="index"></param>
    public void SetGrenadeQ_E(byte index) => _grende_ID = index;

    // ── Simulation ─────────────────────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        _aimForward = _movementHandler.AimForward;

        // ── HOST: authoritative damage & raycast ──────────────────────────
        if (Object.HasStateAuthority)
        {
            if (_movementHandler.IsFiring)
            {
                if (_movementHandler.FireOnce())
                {
                    _movementHandler.IncrementFireCount();

                    // Khi Host fire từ buffer timer (không có input packet thật trong tick này),
                    // AimForward hiện tại có thể là giá trị cũ từ tick trước.
                    // → Dùng LastConfirmedAimForward (hướng của packet thật cuối cùng) để raycast.
                    // Khi có packet thật, cả hai đều bằng nhau nên không ảnh hưởng gì.
                    Vector3 rayDir = _movementHandler.LastConfirmedAimForward != Vector3.zero
                        ? _movementHandler.LastConfirmedAimForward
                        : _aimForward;

                    // [NET-OPT 2026-10-18 Aemy] Ket qua raycast KHONG ghi vao state nua; host giu cache local
                    // de tu dung visual cho chinh minh trong Render (proxy tu raycast, xem OnFireDetected).
                    if (_weaponManager.Weapon_Active().WeaponType == WeaponType.Rifle ||
                        _weaponManager.Weapon_Active().WeaponType == WeaponType.Sniper)
                    {
                        var (hitPos, hitNormal, showEffect) = GetRaycastHitInfo(rayDir);
                        _hostShotCount = 1;
                        _hostShot[0] = new LocalShot { Pos = hitPos, Normal = hitNormal, Effect = showEffect };
                    }

                    if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Shotgun)
                    {
                        ushort seed = ShotSeedFor(Runner.Tick.Raw);
                        _movementHandler.SetShotSeed(seed);
                        GetShotgunHitInfo(rayDir, seed);
                    }
                }
            }

            // Melee hit check — chạy sau delay để khớp với animation swing
            if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
            {
                if (_meleeHitWindowEndTimer.IsRunning)
                {
                    bool windowExpired = _meleeHitWindowEndTimer.Expired(Runner);
                    bool hitConfirmed  = HandleMeleeHit(_aimForward);

                    if (hitConfirmed || windowExpired)
                        _meleeHitWindowEndTimer = default;
                }
            }
        }

        // ── CLIENT PREDICTION: visual ngay lập tức, không chờ Host ────────
        // Chỉ chạy trên client thuần (không phải Host), không phải resimulate
        if (Object.HasInputAuthority && !Object.HasStateAuthority && Runner.IsForward)
        {
            if (_movementHandler.IsFiring && _movementHandler.FireOnce())
            {
                var weaponType = _weaponManager.Weapon_Active()?.WeaponType;

                // Visual ngay: animation recoil, camera recoil, muzzle flash, âm thanh
                _playerMotionController.ActiveWeponCTRL()?.OnFireVisualOnly();

                // Spawn projectile visual dựa trên raycast cục bộ (chỉ để visual, không deal damage)
                if (weaponType == WeaponType.Rifle || weaponType == WeaponType.Sniper)
                {
                    var (hitPos, hitNormal, showEffect) = GetLocalRaycastVisual(_aimForward);
                    _playerMotionController.ActiveWeponCTRL()
                        ?.SpawnProjectileVisual(hitPos, hitNormal, showEffect);
                }
                else if (weaponType == WeaponType.Shotgun)
                {
                    // cung cong thuc seed voi host (theo tick) -> pellet visual trung huong pellet that
                    SpawnLocalShotgunVisual(_aimForward, ShotSeedFor(Runner.Tick.Raw));
                }
            }
        }
    }

    public void TriggerMeleeAttack()
    {
        if (!Object.HasStateAuthority) return;
        if (_weaponManager.Weapon_Active()?.WeaponType != WeaponType.Melee) return;

        // Hủy window của swing cũ (nếu còn chạy) — tránh double-visual
        _meleeHitWindowEndTimer = default;

        // Reset trạng thái hit của swing trước
        _meleeAlreadyHitThisSwing.Clear();
        _meleeWallHitRegisteredThisSwing = false;

        // KHÔNG set _meleeHitTimer nữa — Animation Event sẽ kích hoạt hitbox
        // _meleeHitTimer chỉ giữ lại nếu cần fallback (xem ghi chú bên dưới)
    }

    /// <summary>
    /// Gọi từ Animation Event đúng frame vung vũ khí chạm điểm tác động.
    /// Thay thế hoàn toàn cho _meleeHitTimer delay cũ — chính xác hơn theo từng frame.
    /// StateAuthority check bên trong để Proxy không xử lý.
    /// </summary>
    public void OnMeleeHitAnimEvent()
    {
        if (!Object.HasStateAuthority) return;
        if (_weaponManager.Weapon_Active()?.WeaponType != WeaponType.Melee) return;

        // Mở cửa sổ hit ngay lập tức (không cần delay nữa)
        _meleeHitWindowEndTimer = TickTimer.CreateFromSeconds(Runner, _meleeHitWindowDuration);
    }

    // ── Render (chạy trên tất cả máy, mỗi frame) ───────────────────────────
    public override void Render()
    {
        // Rifle / Shotgun — FireCount (va ShotSeed voi shotgun) tang cung tick nen data luon san.
        // FIX Bug #4: ping cao / snapshot thưa có thể khiến FireCount nhảy nhiều đơn vị
        // giữa 2 lần Render() liên tiếp. Trước đây chỉ so sánh != nên chỉ chạy OnFireDetected()
        // một lần dù FireCount tăng 3-4 → mất visual những phát ở giữa.
        // [NET-OPT 2026-10-18 Aemy] FireCount la byte quay vong -> delta theo modulo 256, chan spam visual toi da 4.
        int fireDelta = (byte)(_movementHandler.FireCount - _lastFireCount);
        if (fireDelta > 4) fireDelta = 4;
        if (fireDelta > 0)
        {
            _lastFireCount = _movementHandler.FireCount;

            // InputAuthority (client thuần) đã tự predict visual theo đúng nhịp ngay khi
            // bắn (xem FixedUpdateNetwork ở trên) — loop lại ở đây sẽ không tái tạo đúng
            // cadence (nó sẽ chạy dồn N lần trong 1 frame) mà chỉ trùng lặp hiệu ứng cuối.
            // OnFireDetected() bên dưới đã tự return sớm cho InputAuthority, nên chỉ cần
            // gọi 1 lần cho gọn — còn Host và proxy (nhìn người khác bắn) mới cần loop
            // đủ delta để không mất visual những phát bị "gộp" do snapshot thưa.
            if (Object.HasInputAuthority && !Object.HasStateAuthority)
            {
                OnFireDetected();
            }
            else
            {
                for (int i = 0; i < fireDelta; i++)
                    OnFireDetected();
            }
        }

        // Melee wall-hit visual — dùng counter riêng vì có delay giữa FireCount và hitbox check.
        // Host tang _meleeHitCount khi trung tuong; may khac tu raycast de dung visual.
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
        // InputAuthority (client thuần, không phải Host) đã tự predict toàn bộ visual ngay trong
        // FixedUpdateNetwork (OnFireVisualOnly() + spawn projectile visual bằng local raycast)
        // -> bỏ qua hoàn toàn, không cần làm lại gì ở đây.
        if (Object.HasInputAuthority && !Object.HasStateAuthority)
            return;

        // FIX: StateAuthority (Host) đã tự áp dụng hiệu ứng bắn THẬT (audio, camera recoil, gun
        // recoil kick, trừ đạn) qua FireOnce() -> OnFire() -> OnShotFired -> HandleShot() ->
        // PlayFireEffect() ngay trong FixedUpdateNetwork(). Trước đây guard ở trên chỉ chặn nhánh
        // InputAuthority-dự-đoán, còn Host vẫn lọt xuống gọi OnFireVisualOnly() ở đây một lần nữa
        // (TriggerRecoilVisual() -> OnShotFired -> PlayFireEffect()) MỖI KHI Render() thấy FireCount
        // đổi — tức là PlayFireEffect() chạy 2 lần / phát bắn, trừ CurrentBulletvalue 2 lần và có
        // thể xuống âm. Chỉ proxy thuần (không StateAuthority, không InputAuthority) mới thực sự
        // cần OnFireVisualOnly() ở đây, vì proxy không tự simulate bắn — chỉ phản chiếu qua FireCount.
        if (!Object.HasStateAuthority)
            _playerMotionController.ActiveWeponCTRL()?.OnFireVisualOnly();

        // SpawnProjectileVisual (điểm/effect trúng đạn):
        //  - Host: dung cache ket qua raycast that (LagCompensation) cua tick ban.
        //  - Proxy: [NET-OPT 2026-10-18 Aemy] tu raycast cuc bo theo huong suy tu TargetYaw + NetworkedPitch
        //    (truoc day doc LastShotInfo ~228B/nguoi trong state). Sai lech chi o visual, damage do host tinh.
        var activeType = _weaponManager.Weapon_Active()?.WeaponType;
        if (activeType == WeaponType.Rifle || activeType == WeaponType.Sniper || activeType == WeaponType.Shotgun)
        {
            var weaponCtrl = _playerMotionController.ActiveWeponCTRL();
            if (Object.HasStateAuthority)
            {
                for (int i = 0; i < _hostShotCount; i++)
                    weaponCtrl?.SpawnProjectileVisual(_hostShot[i].Pos, _hostShot[i].Normal, _hostShot[i].Effect);
            }
            else if (activeType == WeaponType.Shotgun)
            {
                SpawnLocalShotgunVisual(_movementHandler.DerivedAimForward, _movementHandler.ShotSeed);
            }
            else
            {
                var (hitPos, hitNormal, showEffect) = GetLocalRaycastVisual(_movementHandler.DerivedAimForward);
                weaponCtrl?.SpawnProjectileVisual(hitPos, hitNormal, showEffect);
            }
        }

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
        {
            if (_movementHandler.RightPuch)
                _movementHandler.ResetADS();
        }
        // Melee: không spawn projectile visual ở đây — OnMeleeHitDetected() xử lý riêng.
        // OnFireVisualOnly() (nếu có chạy, tức là proxy) đã trigger animation recoil + camera
        // recoil rồi; Host thì tự đã có recoil thật từ FixedUpdateNetwork nên không cần nữa.
    }

    /// <summary>
    /// Gọi khi _meleeHitCount thay đổi — tức là Host vừa xác nhận có wall-hit visual cần spawn.
    /// Chạy trên mọi client: host dung cache diem trung, may khac tu raycast.
    /// </summary>
    private void OnMeleeHitDetected()
    {
        var weaponCtrl = _playerMotionController.ActiveWeponCTRL();
        if (Object.HasStateAuthority)
        {
            weaponCtrl?.SpawnProjectileVisual(_hostMeleeWallHit.Pos, _hostMeleeWallHit.Normal, true);
            return;
        }
        // [NET-OPT 2026-10-18 Aemy] May khac tu raycast tu camera theo huong ngam, tam Range + MeleeRadius.
        Vector3 aim = Object.HasInputAuthority && _aimForward.sqrMagnitude > 0.0001f ? _aimForward : _movementHandler.DerivedAimForward;
        float range  = weaponCtrl != null ? weaponCtrl.Data.Range : 1.5f;
        float radius = weaponCtrl != null ? weaponCtrl.Data.MeleeRadius : 0.6f;
        Vector3 origin = _componentManager.Camera.transform.position;
        if (Physics.Raycast(origin, aim, out RaycastHit hit, range + radius, _hitMask) &&
            hit.collider.GetComponentInParent<NHPHandler>() == null)
            weaponCtrl?.SpawnProjectileVisual(hit.point, hit.normal, true);
    }

    // ── Client-side local raycast (visual only, không deal damage) ─────────

    /// <summary>
    /// Raycast cục bộ trên client để lấy hitPosition/hitNormal cho visual ngay lập tức.
    /// Dùng Physics.Raycast thông thường (không LagCompensation) vì chỉ cần visual gần đúng.
    /// Damage thật vẫn do Host tính qua LagCompensation.
    /// </summary>
    private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetLocalRaycastVisual(Vector3 aimForward)
    {
        var weaponCtrl    = _playerMotionController.ActiveWeponCTRL();
        float maxDistance = weaponCtrl != null ? weaponCtrl.Data.Range : 200f;

        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS && weaponCtrl != null)
            origin = weaponCtrl.ADS_Shoot_Point;

        if (Physics.Raycast(origin, aimForward, out RaycastHit hit, maxDistance, _hitMask))
        {
            // showEffect = false nếu trúng hitbox (player), true nếu trúng wall
            bool isPlayer  = hit.collider.GetComponentInParent<NHPHandler>() != null;
            bool showEffect = !isPlayer;
            return (hit.point, hit.normal, showEffect);
        }

        return (origin + aimForward * maxDistance, -aimForward, false);
    }

    /// <summary>
    /// Spawn visual cho tất cả pellet shotgun phía client ngay lập tức.
    /// </summary>
    private void SpawnLocalShotgunVisual(Vector3 aimForward, ushort seed)
    {
        var weaponCtrl  = _playerMotionController.ActiveWeponCTRL();
        var weaponData  = weaponCtrl?.Data;
        float maxDist   = weaponData != null ? weaponData.Range       : 50f;
        int   pellets   = weaponData != null ? weaponData.PelletCount : 6;
        float spread    = weaponData != null ? weaponData.SpreadAngle : 5f;

        if (_movementHandler.StateFl.IsADS)
            spread *= 0.4f;

        pellets = Mathf.Clamp(pellets, 1, 8);

        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS && weaponCtrl != null)
            origin = weaponCtrl.ADS_Shoot_Point;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 pelletDir = GetSpreadDirection(aimForward, spread, seed, i);

            if (Physics.Raycast(origin, pelletDir, out RaycastHit hit, maxDist, _hitMask))
            {
                bool isPlayer   = hit.collider.GetComponentInParent<NHPHandler>() != null;
                bool showEffect = !isPlayer;
                weaponCtrl?.SpawnProjectileVisual(hit.point, hit.normal, showEffect);
            }
            else
            {
                weaponCtrl?.SpawnProjectileVisual(origin + pelletDir * maxDist, -pelletDir, false);
            }
        }
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
    private void GetShotgunHitInfo(Vector3 aimForward, ushort seed)
    {
        var weaponCtrl  = _playerMotionController.ActiveWeponCTRL();
        var weaponData  = weaponCtrl?.Data;
        float maxDist   = weaponData != null ? weaponData.Range       : 50f;
        int   pellets   = weaponData != null ? weaponData.PelletCount : 6;
        float spread    = weaponData != null ? weaponData.SpreadAngle : 5f;

        // ADS thu hẹp spread
        if (_movementHandler.StateFl.IsADS)
            spread *= 0.4f;

        pellets = Mathf.Clamp(pellets, 1, 8); // toi da 8 pellet (giu nhu cu)

        Vector3 origin = _componentManager.Camera.transform.position;
        if (_movementHandler.StateFl.IsADS && weaponCtrl != null)
            origin = weaponCtrl.ADS_Shoot_Point;

        var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

        _hostShotCount = pellets;

        for (int i = 0; i < pellets; i++)
        {
            // Huong lech trong hinh non — TAT DINH theo seed (host, client predict, proxy ra cung huong)
            Vector3 pelletDir = GetSpreadDirection(aimForward, spread, seed, i);

            if (Runner.LagCompensation != null &&
                Runner.LagCompensation.Raycast(
                    origin, pelletDir, maxDist,
                    Object.InputAuthority, out var hit,
                    _hitMask, hitOptions))
            {
                bool showEffect = hit.Hitbox == null;
                HandleHit(hit.Point, hit.Normal, hit.Hitbox?.transform);
                _hostShot[i] = new LocalShot { Pos = hit.Point, Normal = hit.Normal, Effect = showEffect };

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
                _hostShot[i] = new LocalShot { Pos = missPoint, Normal = -pelletDir, Effect = false };
            }
        }
    }

    /// <summary>
    /// Trả về hướng ngẫu nhiên lệch khỏi forward trong hình nón spreadAngle độ.
    /// </summary>
    private static Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle, ushort seed, int pelletIndex)
    {
        // [NET-OPT 2026-10-18 Aemy] (bao cao 2.1) Truoc day UnityEngine.Random -> moi may 1 huong pellet khac nhau.
        // Nay dung hash tat dinh tu (seed, pellet) -> host, client predict va proxy cung ra 1 huong.
        float angle  = Hash01(seed, pelletIndex * 2)     * 360f * Mathf.Deg2Rad;
        float radius = Hash01(seed, pelletIndex * 2 + 1);   // sqrt để uniform trên đĩa
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
    /// Nếu trúng tường (showEffect = true): luu cache local cho host roi tăng _meleeHitCount
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

        // Diem trung tuong (chi host giu de tu dung visual; may khac tu raycast)
        Vector3 wallPos    = origin + aimForward * (range + radius);
        Vector3 wallNormal = -aimForward;

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
                    RPC_ApplyAudio();
                    return true;
                }
            }
            // Trúng tường — ghi nhận, chờ xem OverlapSphere có trúng player không
            else if (centerHit.Collider != null)
            {
                centerHitWall = true;
                wallPos       = centerHit.Point;
                wallNormal    = centerHit.Normal;
    #if UNITY_EDITOR
                Debug.DrawLine(origin, centerHit.Point, Color.yellow, 1f);
    #endif
            }
        }

        // ── Tầng 1: OverlapSphere fallback ────────────────────────────────
        // [NET-OPT 2026-10-18 Aemy] (bao cao 9.8) tai su dung list (truoc day new List moi tick trong window)
        var hits  = _meleeHits;
        hits.Clear();
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
            _meleeWallHitRegisteredThisSwing = true;
            _hostMeleeWallHit = new LocalShot { Pos = wallPos, Normal = wallNormal, Effect = true };
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
    // [NET-OPT 2026-10-18 Aemy] (bao cao 6.3) hit marker tan suat cao, mat 1 cai khong sao -> Unreliable (khong gui lai).
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, Channel = RpcChannel.Unreliable)]
    private void RPC_ApplyAudio()
    {
        AudioManager.Instance.PlayClip(_hitSound, _playerData.TakeDamageClips, _playerData.Volume_TakeDamage, true, 0.5f, 1f);

        var HUD = _componentManager.GameHudController();
        if (HUD != null)
            HUD.CrosshairHit();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetGrenadeOnUI(string grenadeQ, string grenadeE)
    {
        SetGrenade(grenadeQ, grenadeE);
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
    public void ThrowGrenade()
    {
        if (!Object.HasStateAuthority) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        GrenadeHandler grenade = null;
        Vector3 throwPoint = Vector3.zero;
        switch (_grende_ID)
        {
            case 1: grenade = gm.GetGrenade(GrenadeQ.ToString()); break;
            case 2: grenade = gm.GetGrenade(GrenadeE.ToString()); break;
        }

        // [NET-OPT 2026-10-18 Aemy] (bao cao 9.8) null-check TRUOC khi doc GrenadeType (truoc day NRE),
        // va van reset _grende_ID de khong ket trang thai nem.
        if (grenade == null) { _grende_ID = 0; return; }

        switch (grenade.GrenadeType)
        {
            case GrenadeType.Smoke:
            case GrenadeType.Frag:
                throwPoint = _throwPointRight.position;
                break;
            case GrenadeType.Flash:
                throwPoint = _throwPointLeft.position;
                break;
        }

        Vector3 dir = _aimForward.sqrMagnitude > 0.0001f ? _aimForward : transform.forward;
        Runner.Spawn(grenade, throwPoint + dir, Quaternion.LookRotation(dir),
            Object.InputAuthority,
            (runner, spawnedGrenade) =>
            {
                spawnedGrenade.GetComponent<GrenadeHandler>().Throw(
                    dir * _fore,
                    Object.InputAuthority,
                    nPlayer.Nickname.ToString(),
                    _hitMask,
                    Object.Id,
                    this);
            });

        _grende_ID = 0;
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