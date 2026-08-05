using Fusion;
using UnityEngine;

public class NetworkGameState : NetworkBehaviour
{
    public static NetworkGameState Instance { get; private set; }

    [Header("Countdown Settings")]
    [SerializeField] private float _startCountdownDuration = 10f;

    [Header("Match Settings")]
    [SerializeField] private int _totalRounds = 1;
    public int TotalRounds => _totalRounds;
    [SerializeField] private float _roundDurationMinutes = 10f;

    [Networked] public float RoundElapsed { get; private set; } // giây đã chơi trong hiệp
    [Networked] public float TotalElapsed { get; private set; } // tổng giây đã chơi cả trận

    // ── Networked State ──────────────────────────────────────────────────────
    [Networked] private TickTimer _countdownTimer { get; set; }
    [Networked] private TickTimer _roundTimer     { get; set; }
    [Networked] public NetworkBool GameStarted    { get; private set; }
    [Networked] public NetworkBool RoundEnded     { get; private set; }
    [Networked] public int CurrentRound           { get; private set; }

    // ── Network & Performance Stats ──────────────────────────────────────────
    [Header("Stats Settings")]
    [SerializeField] private float _statUpdateInterval = 0.5f; // cập nhật mỗi 0.5s

    // FPS
    private float _fpsTimer;
    private int   _fpsFrameCount;
    public float  FPS   { get; private set; }

    // Ping — RTT / 2, tính bằng ms. Trên host luôn = 0 (không có RTT với chính nó)
    public int    PingMs { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public override void Spawned()
    {
        Instance = this;

        if (!HasStateAuthority) return;

        // Nếu TotalElapsed > 0 nghĩa là resume từ snapshot → không reset
        if (TotalElapsed > 0)
        {
            Debug.Log("<color=orange>[NetworkGameState]</color> Resumed from host migration");
            return;
        }

        CurrentRound = 1;
        GameStarted  = false;
        RoundEnded   = false;
        StartCountdown();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (!IsMatchOver())
            TotalElapsed += Runner.DeltaTime;

        if (!GameStarted)
        {
            if (_countdownTimer.Expired(Runner))
            {
                GameStarted  = true;
                RoundEnded   = false;
                RoundElapsed = 0f;
                _roundTimer  = TickTimer.CreateFromSeconds(Runner, _roundDurationMinutes * 60f);
            }
            return;
        }

        if (!RoundEnded)
        {
            RoundElapsed += Runner.DeltaTime;

            if (_roundTimer.Expired(Runner))
            {
                RoundEnded = true;
                if (CurrentRound < _totalRounds)
                {
                    CurrentRound++;
                    GameStarted  = false;
                    RoundElapsed = 0f;
                    StartCountdown();
                    RespawnAllPlayers();
                }
                else if (CurrentRound == _totalRounds)
                {
                    RoundEnded = true;
                    CurrentRound++;
                }
            }
        }
    }

    private void Update()
    {
        // ── FPS ─────────────────────────────────────────────────────────────
        _fpsFrameCount++;
        _fpsTimer += Time.unscaledDeltaTime;

        if (_fpsTimer >= _statUpdateInterval)
        {
            FPS            = _fpsFrameCount / _fpsTimer;
            _fpsFrameCount = 0;
            _fpsTimer      = 0f;

            // ── Ping ─────────────────────────────────────────────────────────
            // GetPlayerRtt trả về Round Trip Time (giây) — chia 2 để ra one-way latency
            // Trên host RTT = 0 vì không có kết nối mạng với chính nó
            if (Runner != null)
            {
                if (Runner.IsServer && !Runner.IsSharedModeMasterClient)
                {
                    // Host thuần túy: không có RTT với chính nó
                    PingMs = 0;
                }
                else
                {
                    // Client hoặc shared mode: mới có ping thực
                    double rtt = Runner.GetPlayerRtt(Runner.LocalPlayer);
                    PingMs = Mathf.RoundToInt((float)(rtt * 500.0)); // *500 = *1000/2 (one-way)
                }
            }
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────
    private void StartCountdown()
    {
        _countdownTimer = TickTimer.CreateFromSeconds(Runner, _startCountdownDuration);
    }

    private void RespawnAllPlayers()
    {
        // Đang trong FixedUpdateNetwork với HasStateAuthority → an toàn, không cần RPC
        foreach (var player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out var playerObj))
            {
                var movementHandler = playerObj.GetComponent<NCharMovementHandler>();
                if (movementHandler != null)
                    movementHandler.RequestRespawn(true);
            }
        }
    }

    // ── Public API cho UI ────────────────────────────────────────────────────
    public float? GetCountdownRemaining()
    {
        if (Object == null || !Object.IsValid) return null;
        return _countdownTimer.RemainingTime(Runner);
    }

    public float? GetRoundRemaining()
    {
        if (Object == null || !Object.IsValid) return null;
        return _roundTimer.RemainingTime(Runner);
    }

    public bool IsMatchOver()
    {
        if (Object == null || !Object.IsValid) return false;
        return RoundEnded && CurrentRound > _totalRounds;
    }

    /// <summary>
    /// Dùng cho scoreboard trên host — đọc ping của từng client.
    /// Trả về -1 nếu Runner chưa sẵn sàng.
    /// </summary>
    public int GetPlayerPingMs(PlayerRef player)
    {
        if (Runner == null) return -1;
        double rtt = Runner.GetPlayerRtt(player);
        return Mathf.RoundToInt((float)(rtt * 1000.0));
    }
}