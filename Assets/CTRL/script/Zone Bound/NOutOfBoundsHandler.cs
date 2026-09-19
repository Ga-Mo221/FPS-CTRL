using Fusion;
using Game.UI;
using UnityEngine;

/// <summary>
/// Gắn trên Player Prefab. Tự động lấy bounds từ ZoneBoundsManager của scene hiện tại.
/// Không cần config gì thêm trên prefab — chỉ cần ZoneBoundsManager có trong scene.
///
/// Flow:
///   - StateAuthority (server) tick mỗi FixedUpdateNetwork.
///   - Nếu player ra ngoài bounds → IsOutOfBounds = true → damage tăng dần → kill trong TimeToKill giây.
///   - Khi IsOutOfBounds thay đổi → OnChangedRender → gọi GameHudController.SetOutOfBoundsWarning().
/// </summary>
public class NOutOfBoundsHandler : NetworkBehaviour
{
    // ──────────────────────────────────────────────────────────────────────
    // Networked State
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>SimulationTime khi bắt đầu ra ngoài vùng. -1 = đang trong vùng.</summary>
    [Networked] private float OutOfBoundsStartTime { get; set; } = -1f;

    /// <summary>SimulationTime của lần tick damage gần nhất.</summary>
    [Networked] private float LastDamageTick { get; set; } = 0f;

    /// <summary>
    /// Trạng thái ra ngoài vùng — sync để OnChangedRender kích hoạt UI đúng 1 lần,
    /// tránh spam RPC mỗi tick.
    /// </summary>
    [Networked, OnChangedRender(nameof(OnOutOfBoundsStateChanged))]
    private NetworkBool IsOutOfBounds { get; set; } = false;

    // ──────────────────────────────────────────────────────────────────────
    // Private refs
    // ──────────────────────────────────────────────────────────────────────

    private NHPHandler            _hpHandler;
    private CharComponentManager  _componentManager;
    private GameHudControllerBase _gameHud;

    // Cache để không gọi Instance mỗi tick
    private MapBoundsData _bounds;

    // ──────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _componentManager = GetComponent<CharComponentManager>();
        if (_componentManager == null) return;

        _hpHandler = _componentManager.NHPHandler;
        _gameHud   = _componentManager.GameHudController();

        // Cache bounds — ZoneBoundsManager phải có trong scene trước khi player spawn.
        // Nếu null, zone damage tự động bị tắt (không crash).
        if (ZoneBoundsManager.Instance != null)
            _bounds = ZoneBoundsManager.Instance.BoundsData;

        if (_bounds == null)
            Debug.LogWarning($"[NOutOfBoundsHandler] {transform.name}: Không tìm thấy MapBoundsData. Zone damage bị tắt.", this);
    }

    public override void FixedUpdateNetwork()
    {
        // Chỉ server tính toán
        if (!Object.HasStateAuthority) return;

        // Không có bounds → bỏ qua (map chưa setup hoặc map không dùng feature này)
        if (_bounds == null)
        {
            // Thử lấy lại nếu ZoneBoundsManager vừa được khởi tạo muộn
            if (ZoneBoundsManager.Instance != null)
                _bounds = ZoneBoundsManager.Instance.BoundsData;
            return;
        }

        if (_hpHandler == null) return;
        if (_hpHandler.IsDead) return;

        bool wasOut     = IsOutOfBounds;
        bool currentOut = !_bounds.IsInsideBounds(transform.position);

        // ── Transition vào / ra ──
        if (currentOut && !wasOut)
        {
            // Mới ra ngoài vùng
            IsOutOfBounds        = true;
            OutOfBoundsStartTime = Runner.SimulationTime;
            LastDamageTick       = Runner.SimulationTime;
            Debug.Log($"[NOutOfBoundsHandler] {transform.name} ra ngoài vùng tại {transform.position}");
        }
        else if (!currentOut && wasOut)
        {
            // Quay về trong vùng — reset hoàn toàn
            IsOutOfBounds        = false;
            OutOfBoundsStartTime = -1f;
            Debug.Log($"[NOutOfBoundsHandler] {transform.name} quay về trong vùng.");
        }

        if (!IsOutOfBounds) return;

        // ── Tính damage tăng dần ──
        // Công thức: damage mỗi tick scale theo elapsed/TimeToKill
        //   - t=0s  → scale 0.5  (nhỏ, cảnh báo)
        //   - t=5s  → scale 2.0  (lớn, chắc chắn kill)
        // Tổng damage đủ để kill trong TimeToKill giây.
        float elapsed     = Runner.SimulationTime - OutOfBoundsStartTime;
        float ticksTotal  = _bounds.TimeToKill / _bounds.DamageTickInterval;
        int   basePerTick = Mathf.CeilToInt(_hpHandler.MaxHP / ticksTotal);
        float scale       = 0.5f + Mathf.Clamp01(elapsed / _bounds.TimeToKill) * 1.5f;
        int   damage      = Mathf.Max(1, Mathf.RoundToInt(basePerTick * scale));

        if (Runner.SimulationTime >= LastDamageTick + _bounds.DamageTickInterval)
        {
            LastDamageTick = Runner.SimulationTime;
            _hpHandler.OnTakeDamage(
                damage,
                damageCausedByPlayerNickName: "Out of Bounds",
                weaponName: "Zone",
                id: default,
                Trauma: 0f   // không rung camera khi zone damage
            );
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // OnChangedRender — chạy trên mọi client khi IsOutOfBounds thay đổi
    // ──────────────────────────────────────────────────────────────────────

    void OnOutOfBoundsStateChanged()
    {
        // Chỉ local player của object này mới cần thấy warning
        if (!Object.HasInputAuthority) return;
        if (_gameHud == null) return;
        
        if (IsOutOfBounds)
            Debug.LogWarning("[zone bound] ra ngoai vong cam");

        //_gameHud.SetOutOfBoundsWarning(IsOutOfBounds);
    }
}