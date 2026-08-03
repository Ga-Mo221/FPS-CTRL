using System.Collections;
using Fusion;
using UnityEngine;

public class NHPHandler : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHPChanged))]
    private int HP { get; set; }
    [SerializeField] private float _oldHP;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public bool IsDead { get; private set; }

    [Networked, OnChangedRender(nameof(OnDeadCountChanged))]
    public int DeadCount { get; private set; } = 0;
    
    [Networked] private TickTimer _revivePhase1Timer { get; set; } // chờ trước DisableCCC
    [Networked] private TickTimer _revivePhase2Timer { get; set; } // chờ trước RequestRespawn

    public bool SkipSettingStartValues;

    [Header("Regen Settings")]
    [SerializeField] private float _regenDelay = 5f;      // Giây không nhận damage trước khi hồi máu
    [SerializeField] private float _regenInterval = 1f;   // Hồi mỗi X giây
    [SerializeField] private int _regenAmount = 5;        // Lượng máu hồi mỗi lần

    private CharComponentManager _componentManager;
    private UIHPPlayer _uiHPPlayer;
    private PlayerData _data;
    private NCharMovementHandler _movementHandler;
    private HitboxRoot _hitboxRoot;
    private NInGameMessagesHandler _inGameMessagesHandler;
    private NPlayer _nPlayer;

    private float _lastDamageTime = float.MinValue;
    private float _nextRegenTime;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _data = _componentManager.NCharMovementHandler.Data;
        _uiHPPlayer = _componentManager.UihpPlayer;
        _movementHandler = _componentManager.NCharMovementHandler;
        _hitboxRoot = _componentManager.HitboxRoot;
        _inGameMessagesHandler = _componentManager.NInGameMessagesHandler;
        _nPlayer = _componentManager.NPlayer;
    }

    private void Start()
    {
        if (!SkipSettingStartValues)
        {
            NewState();
        }
        _uiHPPlayer.SetHP(HP, _data.HP);
    }

    private void OnEnable()
    {
        StartCoroutine(OnHotMigrationHP());
    }

    IEnumerator OnHotMigrationHP()
    {
        yield return null;
        yield return null;
        _uiHPPlayer.SetHP(HP, _data.HP);
    }

    private void NewState()
    {
        IsDead = false;
        HP = _data.HP;
        _uiHPPlayer.Init(HP, _data, _componentManager);
        _lastDamageTime = float.MinValue;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Phase 1: DisableCCC
        if (_revivePhase1Timer.Expired(Runner))
        {
            _revivePhase1Timer = default;
            _movementHandler.DisableCCC();
            _revivePhase2Timer = TickTimer.CreateFromSeconds(Runner, /* timeRespawn/2 */ 3.5f);
        }

        // Phase 2: RequestRespawn
        if (_revivePhase2Timer.Expired(Runner))
        {
            _revivePhase2Timer = default;
            _movementHandler.RequestRespawn();
        }

        // Regen logic...
        if (IsDead) return;
        if (HP >= _data.HP) return;
        if (Runner.SimulationTime < _lastDamageTime + _regenDelay) return;
        if (Runner.SimulationTime < _nextRegenTime) return;
        HP = Mathf.Min(HP + _regenAmount, _data.HP);
        _nextRegenTime = Runner.SimulationTime + _regenInterval;
    }

    public bool OnTakeDamage(int damage, string damageCausedByPlayerNickName, NetworkId id, float Trauma = 0.2f)
    {
        if (!_movementHandler.CanTakeDamage) return false;
        if (IsDead) return false;

        if (damage > HP)
            damage = HP;
        
        _oldHP = HP;
        HP -= damage;
        _lastDamageTime = Runner.SimulationTime;
        _nextRegenTime = _lastDamageTime + _regenDelay + _regenInterval;
        
        if (Object.HasStateAuthority)
            RPC_ApplyTrauma(Trauma);

        if (HP <= 0)
        {
            _inGameMessagesHandler.SendInGameRPCMessage(damageCausedByPlayerNickName, $"<color=red>Killed</color> <b>{_nPlayer.Nickname.ToString()}</b>");
            IsDead = true;
            StartServerRevive(7f);
            if (Object.Id == id) return false;
            return true;
        }
        return false;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyTrauma(float trauma)
    {
        _componentManager.CameraShake.AddTrauma(trauma);
        AudioManager.Instance.PlayClip(_componentManager.Audio_Body, _data.HitDamageClips, _data.Volume_HitDamage);
    }

    void OnHPChanged()
    {
        if (!Object.HasInputAuthority) return;
        //Debug.Log($"[HPHandler] {Time.time} OnHPChanged value: {HP}", this);
        _uiHPPlayer.SetHP(HP, _data.HP);
    }

    void OnStateChanged()
    {
        //Debug.Log($"[HPHandler] {Time.time} OnStateChanged isDead: {IsDead}", this);
        _uiHPPlayer.SetDead(IsDead);
        _hitboxRoot.HitboxRootActive = !IsDead;
        if (IsDead)
            DeadCount++;
    }

    void OnDeadCountChanged()
    {
        // thay doi ui KDA
    }

    public void OnRespawn()
    {
        if (Object.HasStateAuthority)
            NewState();
        
        _uiHPPlayer.SetHP(_data.HP, _data.HP);
        _uiHPPlayer.SetDead(false);
        _hitboxRoot.HitboxRootActive = true;
    }

    private void StartServerRevive(float timeRespawn)
    {
        if (!Object.HasStateAuthority) return;
        _revivePhase1Timer = TickTimer.CreateFromSeconds(Runner, timeRespawn / 2f);
    }
    
    // IEnumerator ServerRevoveCO(float timeRespawn)
    // {
    //     if (!Object.HasStateAuthority) yield break;
    //     
    //     yield return new WaitForSeconds(timeRespawn/2);
    //     _movementHandler.DisableCCC();
    //     yield return new WaitForSeconds(timeRespawn/2);
    //     _movementHandler.RequestRespawn();
    // }
}