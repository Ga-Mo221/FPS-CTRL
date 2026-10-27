using System.Collections;
using Fusion;
using Game.UI;
using UnityEngine;

public class NHPHandler : NetworkBehaviour
{
    //HP (ushort) + DeadCount (byte) + IsDead (byte) gop vao 1 word.
    // 1 callback chung roi tach lai: HP doi -> OnHPChanged, IsDead doi -> OnStateChanged (hanh vi nhu cu).
    [Networked, OnChangedRender(nameof(OnHPStateChanged))]
    private uint _hpState { get; set; }

    public int HP
    {
        get { return (int)(_hpState & 0xFFFF); }
        private set { _hpState = (_hpState & 0xFFFF0000u) | (uint)Mathf.Clamp(value, 0, 65535); }
    }

    public int DeadCount
    {
        get { return (int)((_hpState >> 16) & 0xFF); }
        private set { _hpState = (_hpState & 0xFF00FFFFu) | ((uint)Mathf.Clamp(value, 0, 255) << 16); }
    }

    public bool IsDead
    {
        get { return (_hpState >> 24) != 0; }
        private set { _hpState = (_hpState & 0x00FFFFFFu) | (value ? 0x01000000u : 0u); }
    }

    private int _cbLastHP;
    private bool _cbLastDead;
    void OnHPStateChanged()
    {
        int hp = HP; bool dead = IsDead;
        if (hp != _cbLastHP) { _cbLastHP = hp; OnHPChanged(); }
        if (dead != _cbLastDead) { _cbLastDead = dead; OnStateChanged(); }
    }

    // Thay RPC_ApplyHitDamage (moi vien trung + 2 string):
    // nan nhan tu suy rung camera/am thanh tu chenh lech HP. Trauma luong tu hoa x100 vao 1 byte,
    // chi dirty khi muc trauma doi (dan thuong luon 0.2 -> gan nhu khong bao gio dirty).
    [Networked] private byte _hitTraumaQ { get; set; }
    private int _lastHP = int.MinValue;

    [Networked] private TickTimer _revivePhase1Timer { get; set; }
    [Networked] private TickTimer _revivePhase2Timer { get; set; }

    [Networked, OnChangedRender(nameof(OnRespawnCountdownChanged))]
    private int RespawnCountdown { get; set; }

    [Networked] private TickTimer _earlyReviveAllowedTimer { get; set; }

    public bool SkipSettingStartValues;

    [Header("Regen Settings")]
    [SerializeField] private float _regenDelay = 5f;
    [SerializeField] private float _regenInterval = 1f;
    [SerializeField] private int _regenAmount = 5;

    // ── Local events (không sync, chỉ dùng để tính exp local) ─────────────
    /// <summary>Invoke khi nhận damage: (attackerObjectId, actualDamage)</summary>
    public event System.Action<NetworkId, int> OnDamageDealt;

    /// <summary>Invoke khi respawn xong → attacker reset tích lũy damage</summary>
    public event System.Action OnRespawned;

    /// <summary>Invoke khi chết: (killerObjectId) — NetworkId.None nếu không xác định</summary>
    public event System.Action<NetworkId> OnDied;

    // ── Expose HP để NShootHandler đọc local ──────────────────────────────
    public int MaxHP => _data != null ? _data.HP : 10000;

    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;
    private PlayerData _data;
    private NCharMovementHandler _movementHandler;
    private HitboxRoot _hitboxRoot;
    private NInGameMessagesHandler _inGameMessagesHandler;
    private NPlayer _nPlayer;
    private DissolvingController _dissolvingController;
    private WeaponManager _weaponManager;

    private float _lastDamageTime = float.MinValue;
    private float _nextRegenTime;

    // Lưu killer id để truyền vào OnDied
    private NetworkId _lastKillerId;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();

        if (_componentManager == null) return;

        _data                  = _componentManager.NCharMovementHandler.Data;
        _gameHudController     = _componentManager.GameHudController();
        _movementHandler       = _componentManager.NCharMovementHandler;
        _hitboxRoot            = _componentManager.HitboxRoot;
        _inGameMessagesHandler = _componentManager.NInGameMessagesHandler;
        _nPlayer               = _componentManager.NPlayer;
        _dissolvingController  = _componentManager.DissolvingController;
        _weaponManager         = _componentManager.WeaponManager;
        _lastHP                = HP;
        _cbLastHP              = HP;
        _cbLastDead            = IsDead;
    }

    private void Start()
    {
        if (!SkipSettingStartValues)
            NewState();

        if (_data != null)
            _gameHudController.SetHP(HP);
    }

    private void OnEnable()
    {
        StartCoroutine(OnHotMigrationHP());
    }

    IEnumerator OnHotMigrationHP()
    {
        yield return null;
        yield return null;
        if (_gameHudController != null)
            _gameHudController.SetHP(HP);
    }

    private void NewState()
    {
        IsDead = false;

        HP = _data != null ? _data.HP : 10000;

        if (_gameHudController != null && _data != null && _componentManager != null)
            _gameHudController.InitHPStart(HP, _data, _componentManager);

        _lastDamageTime = float.MinValue;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Cập nhật countdown hồi sinh hiển thị
        if (IsDead)
        {
            float remaining = 0f;
            if (_revivePhase1Timer.IsRunning)
                remaining = (_revivePhase1Timer.RemainingTime(Runner) ?? 0f) + 3.5f;
            else if (_revivePhase2Timer.IsRunning)
                remaining = _revivePhase2Timer.RemainingTime(Runner) ?? 0f;

            RespawnCountdown = Mathf.CeilToInt(remaining);
        }
        else
        {
            RespawnCountdown = 0;
        }

        // Phase 1: DisableCCC
        if (_revivePhase1Timer.Expired(Runner))
        {
            _revivePhase1Timer = default;
            _movementHandler?.DisableCCC();
            _revivePhase2Timer = TickTimer.CreateFromSeconds(Runner, 3.5f);
        }

        // Phase 2: RequestRespawn
        if (_revivePhase2Timer.Expired(Runner))
        {
            _revivePhase2Timer = default;
            _movementHandler?.RequestRespawn();
        }

        // Regen HP
        if (IsDead) return;
        int maxHP = _data != null ? _data.HP : 10000;
        if (HP >= maxHP) return;
        if (Runner.SimulationTime < _lastDamageTime + _regenDelay) return;
        if (Runner.SimulationTime < _nextRegenTime) return;
        HP = Mathf.Min(HP + _regenAmount, maxHP);
        _nextRegenTime = Runner.SimulationTime + _regenInterval;
    }

    /// <summary>
    /// Xử lý nhận damage.
    /// Trả về true nếu đây là phát kill VÀ killer không phải chính mình (Object.Id != id).
    /// </summary>
    public bool OnTakeDamage(int damage, string damageCausedByPlayerNickName, string weaponName, NetworkId id, float Trauma = 0.2f, bool headshot = false)
    {
        if (_movementHandler != null && !_movementHandler.CanTakeDamage) return false;
        if (IsDead) return false;

        // Clamp để tránh overkill — actualDamage = damage thực sự deal
        int actualDamage = Mathf.Min(damage, HP);
        HP -= actualDamage;

        _lastDamageTime = Runner.SimulationTime;
        _nextRegenTime  = _lastDamageTime + _regenDelay + _regenInterval;

        // Broadcast local để attacker tính exp tích lũy
        OnDamageDealt?.Invoke(id, actualDamage);

        if (Object.HasStateAuthority)
            _hitTraumaQ = (byte)Mathf.Clamp(Mathf.RoundToInt(Trauma * 100f), 0, 255);

        if (HP <= 0)
        {
            _lastKillerId = id;
            // gui NetworkId + chi so vu khi thay vi 3 chuoi
            _inGameMessagesHandler.SendInGameRPCKill(id, damageCausedByPlayerNickName, weaponName, headshot);
            IsDead = true;
            DeadCount++; // tang o simulation tren host, khong con o callback render
            StartServerRevive(GameManager.Instance.RespawnTime);
            if (Object.Id == id) return false;
            return true;
        }
        return false;
    }

    void OnHPChanged()
    {
        int prev = _lastHP;
        _lastHP = HP;
        if (!Object.HasInputAuthority) return;
        _gameHudController?.SetHP(HP);

        // HP giam = vua trung don (regen/hoi sinh lam HP tang nen bi bo qua).
        // Ten ke giet + vu khi cho man hinh chet: chuyen sang RPC_KillGame (NInGameMessagesHandler).
        int dmg = prev == int.MinValue ? 0 : prev - HP;
        if (dmg <= 0 || _componentManager == null) return;
        float trauma = _hitTraumaQ / 100f;
        if (trauma > 0f && _componentManager.CameraShake != null)
            _componentManager.CameraShake.AddTrauma(trauma);
        if (_data != null && AudioManager.Instance != null && _data.HitDamageClips != null && _data.HitDamageClips.Length > 0)
            AudioManager.Instance.PlayClip(_componentManager.Audio_Body, _data.HitDamageClips, _data.Volume_HitDamage);
    }

    void OnStateChanged()
    {
        _gameHudController?.SetDead(IsDead);

        if (_hitboxRoot == null)
            _hitboxRoot = GetComponent<HitboxRoot>();

        _hitboxRoot.HitboxRootActive = !IsDead;

        if (IsDead)
        {
            if (_nPlayer != null && _componentManager != null && !_nPlayer.IsLocalPlayer)
                _componentManager.UIPlayerNickName.gameObject.SetActive(false);

            // DeadCount++ da chuyen vao OnTakeDamage (host). Ghi bien networked
            // trong OnChangedRender (chay o Render tren MOI may) la sai.

            // Thông báo cho các attacker biết target đã chết
            OnDied?.Invoke(_lastKillerId);

            if (GameManager.Instance != null && Object.HasInputAuthority)
                GameManager.Instance.ResetStreak();
        }
        else
        {
            if (Object.HasInputAuthority)
            {
                if (GameManager.Instance != null && _weaponManager != null)
                {
                    var (change, weaponOne, weaponTwo) = GameManager.Instance.GetCurrentBalo();
                    if (change)
                    {
                        _weaponManager.Rpc_RequestChangeWeapon(weaponOne, weaponTwo);
                        Debug.Log($"<color=green>[NCharMovementHandler]</color> Reset Balo");
                    }
                }
            }
            
            _dissolvingController?.ResetMaterials();

            if (_nPlayer != null && _componentManager != null && !_nPlayer.IsLocalPlayer)
                _componentManager.UIPlayerNickName.gameObject.SetActive(true);
        }
    }

    public void OnRespawn()
    {
        if (Object.HasStateAuthority)
            NewState();
        _lastHP = HP;

        if (_gameHudController != null)
        {
            _gameHudController.SetHP(_data.HP);
            _gameHudController.SetDead(false);
        }

        if (_hitboxRoot == null)
            _hitboxRoot = GetComponent<HitboxRoot>();

        _hitboxRoot.HitboxRootActive = true;

        // Thông báo attacker reset tích lũy damage cho target này
        OnRespawned?.Invoke();
    }

    private void StartServerRevive(float timeRespawn)
    {
        if (!Object.HasStateAuthority) return;
        _revivePhase1Timer        = TickTimer.CreateFromSeconds(Runner, timeRespawn / 2f);
        _earlyReviveAllowedTimer  = TickTimer.CreateFromSeconds(Runner, 3f);

        if (Runner.IsServer && SpawnManager.Instance != null)
            SpawnManager.Instance.RecordDeath(transform.position, Runner.SimulationTime);
    }

    void OnRespawnCountdownChanged()
    {
        if (!Object.HasInputAuthority) return;
        _gameHudController?.SetRecoverTime(RespawnCountdown);
    }

    public void RequestEarlyRevive()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsDead) return;
        if (!_earlyReviveAllowedTimer.Expired(Runner)) return;

        IsDead = false;
        _revivePhase1Timer = default;
        _revivePhase2Timer = default;
        _movementHandler.DisableCCC();
        _movementHandler.RequestRespawn();
    }
}