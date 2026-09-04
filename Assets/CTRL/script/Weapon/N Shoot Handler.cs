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

    // ── Change detector ────────────────────────────────────────────────────
    private int _lastFireCount;
    public Vector3 _aimForward;

    // ── Exp & damage tracking (local only, không cần sync) ────────────────
    // Key: NetworkId của target, Value: tổng damage đã deal cho target đó trong vòng sống hiện tại
    private readonly Dictionary<NetworkId, int> _damageAccumulated = new Dictionary<NetworkId, int>();

    // Cache NHPHandler đã subscribe để quản lý event đúng cách
    private readonly Dictionary<NetworkId, NHPHandler> _subscribedTargets = new Dictionary<NetworkId, NHPHandler>();
    
    // Thêm vào đầu class
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
                    // bắn nhiều raycat.
                }

                if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
                {
                    // vũ khí cận chiến.
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
            // bắn nhiều raycat.
        }

        if (_weaponManager.Weapon_Active()?.WeaponType == WeaponType.Melee)
        {
            // vũ khí cận chiến.
        }
    }

    // ── Raycast + damage — chỉ chạy trên StateAuthority ──────────────────
    private (Vector3 hitPosition, Vector3 hitNormal, bool showEffect) GetRaycastHitInfo(Vector3 aimForward)
    {
        var weaponCtrl  = _playerMotionController.ActiveWeponCTRL();
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

    private void HandleHit(Vector3 point, Vector3 normal, Transform hitbox)
    {
        if (hitbox == null) return;

        var hitNplayer = hitbox.root.gameObject.GetComponent<NPlayer>();
        var HPHandler  = hitbox.root.gameObject.GetComponent<NHPHandler>();
        var bodyParts  = hitbox.GetComponent<BodyPartsHandler>();
        if (HPHandler == null && hitNplayer == null && bodyParts == null) return;

        if (nPlayer.GamePlayMode == GamePlayMode.Solo)
            HitDamage(HPHandler, bodyParts, hitNplayer);
        else if (nPlayer.NetworkedTeam != hitNplayer.NetworkedTeam)
            HitDamage(HPHandler, bodyParts, hitNplayer);
    }

    private void HitDamage(NHPHandler HPHandler, BodyPartsHandler bodyParts, NPlayer targetPlayer)
    {
        var ctrl   = _weaponManager?.Weapon_Active();
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

        // Subscribe event của target nếu chưa theo dõi
        SubscribeTarget(HPHandler);

        var weaponIcon = ctrl?.Data.WeaponName;

        // OnTakeDamage sẽ invoke OnDamageDealt bên trong → _damageAccumulated update
        bool isKill = HPHandler.OnTakeDamage(damage, nPlayer.Nickname.ToString(), weaponIcon, Object.Id);

        if (isKill)
        {
            // Lấy tổng damage đã tích lũy (bao gồm phát vừa deal)
            var targetId   = HPHandler.Object.Id;
            int totalDmg   = _damageAccumulated.TryGetValue(targetId, out var acc) ? acc : 0;
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

        // Capture targetId cho lambda
        var capturedId = targetId;

        HPHandler.OnDamageDealt += (attackerId, actualDmg) =>
            OnTargetDamaged(capturedId, attackerId, actualDmg);

        HPHandler.OnDied += (killerId) =>
            OnTargetDied(capturedId, killerId, HPHandler.MaxHP);

        HPHandler.OnRespawned += () =>
            OnTargetRespawned(capturedId);
    }

    // ── Callbacks từ NHPHandler ───────────────────────────────────────────

    /// <summary>Tích lũy damage do MÌNH deal cho target</summary>
    private void OnTargetDamaged(NetworkId targetId, NetworkId attackerId, int actualDamage)
    {
        // Chỉ tính damage của mình
        if (attackerId != Object.Id) return;

        if (!_damageAccumulated.ContainsKey(targetId))
            _damageAccumulated[targetId] = 0;

        _damageAccumulated[targetId] += actualDamage;
    }

    /// <summary>Target chết — nếu không phải mình kill thì tính assist + exp</summary>
    private void OnTargetDied(NetworkId targetId, NetworkId killerId, int targetMaxHP)
    {
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

        float expPercent = Mathf.Clamp01((float)totalDamage / targetMaxHP);
        int _baseExpPerKill = 100;
        if (GameManager.Instance != null)
            _baseExpPerKill = GameManager.Instance.GetEXPperKill();
        int   expGained  = Mathf.RoundToInt(_baseExpPerKill * expPercent);

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