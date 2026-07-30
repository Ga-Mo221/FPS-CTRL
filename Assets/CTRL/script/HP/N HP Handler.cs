using System.Collections;
using Fusion;
using UnityEngine;

public class NHPHandler : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnHPChanged))]
    private int HP { get; set; }

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public bool IsDead { get; private set; }

    [Networked, OnChangedRender(nameof(OnDeadCountChanged))]
    public int DeadCount { get; private set; } = 0;

    [Header("Regen Settings")]
    [SerializeField] private float _regenDelay = 5f;      // Giây không nhận damage trước khi hồi máu
    [SerializeField] private float _regenInterval = 1f;   // Hồi mỗi X giây
    [SerializeField] private int _regenAmount = 5;        // Lượng máu hồi mỗi lần

    private CharComponentManager _componentManager;
    private UIHPPlayer _uiHPPlayer;
    private PlayerData _data;
    private NCharMovementHandler _movementHandler;
    private HitboxRoot _hitboxRoot;

    private float _lastDamageTime = float.MinValue;
    private float _nextRegenTime;

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _data = _componentManager.NCharMovementHandler.Data;
        _uiHPPlayer = _componentManager.UihpPlayer;
        _movementHandler = _componentManager.NCharMovementHandler;
        _hitboxRoot = _componentManager.HitboxRoot;
        NewState();
    }

    private void NewState()
    {
        IsDead = false;
        HP = _data.HP;
        _uiHPPlayer.Init(HP);
        _lastDamageTime = float.MinValue;
    }

    public override void FixedUpdateNetwork()
    {
        // Chỉ Host mới xử lý regen
        if (!HasStateAuthority) return;
        if (IsDead) return;
        if (HP >= _data.HP) return;

        // Chưa đủ thời gian nghỉ sau damage
        if (Runner.SimulationTime < _lastDamageTime + _regenDelay) return;

        // Chờ đến tick hồi máu tiếp theo
        if (Runner.SimulationTime < _nextRegenTime) return;

        HP = Mathf.Min(HP + _regenAmount, _data.HP);
        _nextRegenTime = Runner.SimulationTime + _regenInterval;
    }

    public bool OnTakeDamage(int damage)
    {
        if (IsDead) return false;

        HP -= damage;
        _lastDamageTime = Runner.SimulationTime;
        _nextRegenTime = _lastDamageTime + _regenDelay + _regenInterval;

        if (HP <= 0)
        {
            IsDead = true;
            
            StartCoroutine(ServerRevoveCO(7f));
            return true;
        }
        return false;
    }

    void OnHPChanged()
    {
        //Debug.Log($"[HPHandler] {Time.time} OnHPChanged value: {HP}", this);
        _uiHPPlayer.SetHP(HP);
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
        NewState();
    }

    IEnumerator ServerRevoveCO(float timeRespawn)
    {
        _movementHandler.StateFl = new PlayerStateFlags();
        yield return new WaitForSeconds(timeRespawn);
        _movementHandler.RequestRespawn();
    }
}