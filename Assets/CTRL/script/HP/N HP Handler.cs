using System.Collections;
using Fusion;
using Game.UI;
using UnityEngine;

public class NHPHandler : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHPChanged))]
    private int HP { get; set; }
    //[SerializeField] private float _oldHP;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public bool IsDead { get; private set; }

    [Networked] public int DeadCount { get; private set; } = 0;
    
    [Networked] private TickTimer _revivePhase1Timer { get; set; } // chờ trước DisableCCC
    [Networked] private TickTimer _revivePhase2Timer { get; set; } // chờ trước RequestRespawn
    
    [Networked, OnChangedRender(nameof(OnRespawnCountdownChanged))]
    private int RespawnCountdown { get; set; }
    
    [Networked] private TickTimer _earlyReviveAllowedTimer { get; set; }

    public bool SkipSettingStartValues;

    [Header("Regen Settings")]
    [SerializeField] private float _regenDelay = 5f;      // Giây không nhận damage trước khi hồi máu
    [SerializeField] private float _regenInterval = 1f;   // Hồi mỗi X giây
    [SerializeField] private int _regenAmount = 5;        // Lượng máu hồi mỗi lần

    private CharComponentManager _componentManager;
    private GameHudControllerBase _gameHudController;
    private PlayerData _data;
    private NCharMovementHandler _movementHandler;
    private HitboxRoot _hitboxRoot;
    private NInGameMessagesHandler _inGameMessagesHandler;
    private NPlayer _nPlayer;
    private DissolvingController _dissolvingController;

    private float _lastDamageTime = float.MinValue;
    private float _nextRegenTime;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        
        if (_componentManager == null) return;
        
        _data = _componentManager.NCharMovementHandler.Data;
        _gameHudController = _componentManager.GameHudController();
        _movementHandler = _componentManager.NCharMovementHandler;
        _hitboxRoot = _componentManager.HitboxRoot;
        _inGameMessagesHandler = _componentManager.NInGameMessagesHandler;
        _nPlayer = _componentManager.NPlayer;
        _dissolvingController = _componentManager.DissolvingController;
    }

    private void Start()
    {
        if (!SkipSettingStartValues)
        {
            NewState();
        }
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

        if (_data != null)
            HP = _data.HP;
        else HP = 10000;
        
        if (_gameHudController != null && _data != null && _componentManager != null)
            _gameHudController.InitHPStart(HP, _data, _componentManager);
        _lastDamageTime = float.MinValue;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        
        // Tính countdown hiển thị (tổng thời gian còn lại của cả 2 phase)
        if (IsDead)
        {
            float remaining = 0f;
            if (_revivePhase1Timer.IsRunning)
                remaining = _revivePhase1Timer.RemainingTime(Runner) ?? 0f;
            // Phase 2 luôn là 3.5f, cộng thêm nếu phase 1 chưa xong
            if (_revivePhase1Timer.IsRunning)
                remaining += 3.5f;
            else if (_revivePhase2Timer.IsRunning)
                remaining = _revivePhase2Timer.RemainingTime(Runner) ?? 0f;

            RespawnCountdown = Mathf.CeilToInt(remaining); // làm tròn lên để hiển thị 1 trước khi spawn
        }
        else
        {
            RespawnCountdown = 0;
        }

        // Phase 1: DisableCCC
        if (_revivePhase1Timer.Expired(Runner))
        {
            _revivePhase1Timer = default;
            if (_movementHandler != null)
                _movementHandler.DisableCCC();
            
            _revivePhase2Timer = TickTimer.CreateFromSeconds(Runner, /* timeRespawn/2 */ 3.5f);
        }

        // Phase 2: RequestRespawn
        if (_revivePhase2Timer.Expired(Runner))
        {
            _revivePhase2Timer = default;
            
            if (_movementHandler != null)
                _movementHandler.RequestRespawn();
        }

        // Regen logic...
        if (IsDead) return;
        if (HP >= (_data != null ? _data.HP: 10000)) return;
        if (Runner.SimulationTime < _lastDamageTime + _regenDelay) return;
        if (Runner.SimulationTime < _nextRegenTime) return;
        HP = Mathf.Min(HP + _regenAmount, _data != null ? _data.HP : 10000);
        _nextRegenTime = Runner.SimulationTime + _regenInterval;
    }

    public bool OnTakeDamage(int damage, string damageCausedByPlayerNickName, string weaponName, NetworkId id, float Trauma = 0.2f)
    {
        if (_movementHandler != null && !_movementHandler.CanTakeDamage) return false;
        if (IsDead) return false;

        if (damage > HP)
            damage = HP;
        
        //_oldHP = HP;
        HP -= damage;
        _lastDamageTime = Runner.SimulationTime;
        _nextRegenTime = _lastDamageTime + _regenDelay + _regenInterval;
        
        if (Object.HasStateAuthority)
            RPC_ApplyHitDamage(Trauma, damageCausedByPlayerNickName, weaponName);

        if (HP <= 0)
        {
            _inGameMessagesHandler.SendInGameRPCKill(
                damageCausedByPlayerNickName, weaponName,  _nPlayer != null ? _nPlayer.Nickname.ToString() : "Player Test"
                );
            IsDead = true;
            StartServerRevive(GameManager.Instance.RespawnTime);
            if (Object.Id == id) return false;
            return true;
        }
        return false;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyHitDamage(float trauma, string killer, string weaponName)
    {
        if (_componentManager != null)
        {
            _componentManager.CameraShake.AddTrauma(trauma);
            _componentManager.GameHudController().SetDeadStatus(killer, weaponName);
        }
        
        if (_componentManager != null && _data != null)
            AudioManager.Instance.PlayClip(_componentManager.Audio_Body, _data.HitDamageClips, _data.Volume_HitDamage);
    }

    void OnHPChanged()
    {
        if (!Object.HasInputAuthority) return;
        //Debug.Log($"[HPHandler] {Time.time} OnHPChanged value: {HP}", this);
        if (_gameHudController != null)
            _gameHudController.SetHP(HP);
    }

    void OnStateChanged()
    {
        //Debug.Log($"[HPHandler] {Time.time} OnStateChanged isDead: {IsDead}", this);
        if (_gameHudController != null)
            _gameHudController.SetDead(IsDead);
        
        if (_hitboxRoot == null)
            _hitboxRoot = GetComponent<HitboxRoot>();
        
        _hitboxRoot.HitboxRootActive = !IsDead;
        if (IsDead)
        {
            if (_nPlayer != null && _componentManager != null && !_nPlayer.IsLocalPlayer)
                _componentManager.UIPlayerNickName.gameObject.SetActive(false);
            DeadCount++;
            
            if (GameManager.Instance != null && Object.HasInputAuthority)
                GameManager.Instance.ResetStreak();
        }
        else
        {
            if (_dissolvingController != null)
                _dissolvingController.ResetMaterials();
            if (_nPlayer != null && _componentManager != null && !_nPlayer.IsLocalPlayer)
                _componentManager.UIPlayerNickName.gameObject.SetActive(true);
        }
    }

    public void OnRespawn()
    {
        if (Object.HasStateAuthority)
            NewState();
        
        if (_gameHudController != null)
        {
            _gameHudController.SetHP(_data.HP);
            _gameHudController.SetDead(false);
        }
        
        if (_hitboxRoot == null)
            _hitboxRoot = GetComponent<HitboxRoot>();
        
        _hitboxRoot.HitboxRootActive = true;
    }

    private void StartServerRevive(float timeRespawn)
    {
        if (!Object.HasStateAuthority) return;
        _revivePhase1Timer = TickTimer.CreateFromSeconds(Runner, timeRespawn / 2f);
        _earlyReviveAllowedTimer = TickTimer.CreateFromSeconds(Runner, 3f);
        
        if (Runner.IsServer && SpawnManager.Instance != null)
            SpawnManager.Instance.RecordDeath(transform.position, Runner.SimulationTime);
    }
    
    void OnRespawnCountdownChanged()
    {
        // Chỉ update UI trên máy local
        if (!Object.HasInputAuthority) return;
    
        if (_gameHudController != null)
            _gameHudController.SetRecoverTime(RespawnCountdown);
    }
    
    public void RequestEarlyRevive()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsDead) return;
        if (!_earlyReviveAllowedTimer.Expired(Runner)) return;
        
        IsDead = false;

        // Hủy cả 2 timer
        _revivePhase1Timer = default;
        _revivePhase2Timer = default;

        // Kích hoạt hồi sinh ngay
        _movementHandler.DisableCCC();
        _movementHandler.RequestRespawn();
    }
}