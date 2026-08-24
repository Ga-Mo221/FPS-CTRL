using System.Collections.Generic;
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

    [Header("Kill Target Settings")]
    [Tooltip("Solo: 1 player đạt đủ số kill này thì kết thúc trận. Team: Red hoặc Blue đạt đủ thì kết thúc.")]
    [Networked] private int _killTarget { get; set; } = 100;
    public int KillTarget => _killTarget;

    [Networked] public float RoundElapsed { get; private set; } // giây còn lại trong hiệp (remaining)
    [Networked] public float TotalElapsed { get; private set; } // tổng giây đã chơi cả trận

    // ── Networked State ──────────────────────────────────────────────────────
    [Networked] private TickTimer _countdownTimer { get; set; }
    [Networked] private TickTimer _roundTimer     { get; set; }
    [Networked] public NetworkBool GameStarted    { get; private set; }
    [Networked] public NetworkBool RoundEnded     { get; private set; }
    [Networked] public int CurrentRound           { get; private set; }

    // Trận đấu kết thúc sớm vì có bên (hoặc cá nhân) đạt đủ kill target
    [Networked] public NetworkBool MatchEndedByKillTarget { get; private set; }

    // ── Kill Stats (networked) ────────────────────────────────────────────────
    [Networked] public int RedKill  { get; private set; }
    [Networked] public int BlueKill { get; private set; }
    //[Networked] public int AllKill  { get; private set; }

    [Header("Kill Stats Settings")]
    [SerializeField] private float _killUpdateInterval = 0.5f; // host tính lại mỗi 0.5s
    private float _killUpdateTimer;
    private readonly Dictionary<NetworkId, KillInfo> _killLookup = new Dictionary<NetworkId, KillInfo>();

    // Danh sách đã sort giảm dần theo kill, dùng cho scoreboard/leaderboard UI.
    // Không sort trực tiếp trên Dictionary vì Dictionary không đảm bảo thứ tự phần tử.
    private readonly List<KillInfo> _killLeaderboard = new List<KillInfo>();
    public IReadOnlyList<KillInfo> KillLeaderboard => _killLeaderboard;
    private float _killLeaderboardTimer; // dùng trong Update (mọi client)

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

        // ── Cập nhật Kill stats (chỉ host, đồng bộ xuống mọi client) ───────────
        _killUpdateTimer += Runner.DeltaTime;
        if (_killUpdateTimer >= _killUpdateInterval)
        {
            _killUpdateTimer = 0f;
            ApplyNetworkedKillStats();

            if (!MatchEndedByKillTarget && CheckKillTargetReached())
            {
                MatchEndedByKillTarget = true;
                RoundEnded             = true;
                CurrentRound           = _totalRounds + 1; // đảm bảo IsMatchOver() = true
            }
        }

        if (!GameStarted)
        {
            if (_countdownTimer.Expired(Runner))
            {
                GameStarted  = true;
                RoundEnded   = false;
                RoundElapsed = _roundDurationMinutes * 60;
                _roundTimer  = TickTimer.CreateFromSeconds(Runner, _roundDurationMinutes * 60f);
            }
            return;
        }

        if (!RoundEnded)
        {
            RoundElapsed -= Runner.DeltaTime;

            if (_roundTimer.Expired(Runner))
            {
                RoundEnded = true;

                if (CurrentRound < _totalRounds)
                {
                    CurrentRound++;
                    GameStarted  = false;
                    RoundEnded   = false;
                    RoundElapsed = _roundDurationMinutes * 60;
                    StartCountdown();
                    RespawnAllPlayers();
                }
                else
                {
                    // Hiệp cuối cùng kết thúc → trận đấu kết thúc
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
        
        // ── Kill leaderboard (local, chạy trên MỌI instance — cả host lẫn client) ──
        // NShootHandler._kill là field được đồng bộ (networked) trên player, nên mỗi
        // client tự đọc GameManager.Instance.PlayerList và tự build leaderboard của
        // riêng mình, không cần chờ dữ liệu từ host → tránh _killLeaderboard rỗng/lỗi
        // index ở client.
        _killLeaderboardTimer += Time.unscaledDeltaTime;
        if (_killLeaderboardTimer >= _killUpdateInterval)
        {
            _killLeaderboardTimer = 0f;
            RebuildKillLookupAndLeaderboard();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────
    
    public void SetKillTarget(int target) => _killTarget = target;
    public int GetKillTarget() => _killTarget;
    
    private void RebuildKillLookupAndLeaderboard()
    {
        if (GameManager.Instance == null) return;

        _killLookup.Clear();

        foreach (var player in GameManager.Instance.PlayerList)
        {
            if (player == null) continue;
            if (player.nPlayer == null || player.nPlayer.Object == null || !player.nPlayer.Object.IsValid)
                continue;

            PlayerTeam team = player.nPlayer.NetworkedTeam;
            int deathcount = player.GetDeathCount();
            _killLookup[player.Object.Id] = new KillInfo(player.Object.Id, team, player._kill, deathcount);
        }

        // ── Build leaderboard đã sort giảm dần theo kill ────────────────────────
        // Dùng danh sách riêng, không sort trực tiếp Dictionary vì Dictionary
        // không đảm bảo (và không nên phụ thuộc vào) thứ tự phần tử.
        _killLeaderboard.Clear();
        _killLeaderboard.AddRange(_killLookup.Values);
        _killLeaderboard.Sort((a, b) => b.Kills.CompareTo(a.Kills));
    }
    
    /// <summary>
    /// Chỉ host gọi (trong FixedUpdateNetwork): tính tổng RedKill/BlueKill từ
    /// _killLookup (đã được RebuildKillLookupAndLeaderboard build ở Update()) rồi
    /// ghi vào [Networked] properties để Fusion đồng bộ xuống mọi client.
    /// </summary>
    private void ApplyNetworkedKillStats()
    {
        int redKill = 0;
        int blueKill = 0;
        foreach (KillInfo killInfo in _killLookup.Values)
        {
            if (killInfo.Team == PlayerTeam.Red)
                redKill += killInfo.Kills;
            else if (killInfo.Team == PlayerTeam.Blue)
                blueKill += killInfo.Kills;
        }

        RedKill  = redKill;
        BlueKill = blueKill;
        //AllKill  = redKill + blueKill;
    }

    /// <summary>
    /// Kiểm tra điều kiện thắng theo kill target, tuỳ theo GamePlayMode:
    /// - Solo: chỉ cần 1 player bất kỳ đạt đủ _killTarget.
    /// - Team: chỉ cần Red hoặc Blue đạt tổng kill đủ _killTarget.
    /// </summary>
    private bool CheckKillTargetReached()
    {
        if (_killTarget <= 0) return false; // 0 hoặc âm = không giới hạn theo kill
        if (GameManager.Instance == null) return false;

        GamePlayMode mode = GameManager.Instance.GetGamePlayMode();

        if (mode == GamePlayMode.Solo)
        {
            foreach (KillInfo killInfo in _killLookup.Values)
            {
                if (killInfo.Kills >= _killTarget)
                    return true;
            }
            return false;
        }

        // Team mode
        return RedKill >= _killTarget || BlueKill >= _killTarget;
    }

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
        return MatchEndedByKillTarget || (RoundEnded && CurrentRound > _totalRounds);
    }

    /// <summary>
    /// Lấy KillInfo (kill, death, team...) của 1 player theo NetworkId.
    /// Trả về false nếu không tìm thấy (player chưa được cập nhật trong lần UpdateKillStats gần nhất,
    /// hoặc đã rời trận).
    /// </summary>
    public bool TryGetKillInfo(NetworkId playerId, out KillInfo killInfo)
    {
        return _killLookup.TryGetValue(playerId, out killInfo);
    }

    /// <summary>
    /// Lấy vị trí (rank) của 1 player trong KillLeaderboard, tính từ 1 (đứng đầu).
    /// Trả về -1 nếu không tìm thấy player trong leaderboard.
    /// </summary>
    public int GetLeaderboardRank(NetworkId playerId)
    {
        for (int i = 0; i < _killLeaderboard.Count; i++)
        {
            if (_killLeaderboard[i].PlayerId == playerId)
                return i + 1;
        }
        return -1;
    }

    /// <summary>
    /// Dùng cho scoreboard trên host — đọc ping của từng client.
    /// Trả về -1 nếu Runner chưa sẵn sàng.
    /// </summary>
    public int GetPlayerPingMs(PlayerRef player)
    {
        if (Runner == null) return -1;
        double rtt = Runner.GetPlayerRtt(player);
        return Mathf.RoundToInt((float)(rtt * 500.0)); // one-way, đồng nhất với Update()
    }
}