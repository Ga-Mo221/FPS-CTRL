using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NetworkGameState : NetworkBehaviour
{
    public static NetworkGameState Instance { get; private set; }

    [Header("Match Settings")]
    [SerializeField] private int _totalRounds = 1;
    public int TotalRounds => _totalRounds;
    [SerializeField] private float _roundDurationMinutes = 10f;
    // Thoi gian tran chu phong chon (phong tuy chinh). Networked -> host moi (migration) van dung dung gia tri.
    [Networked] private float _roundMinutesOverride { get; set; }
    private float RoundMinutes => _roundMinutesOverride > 0f ? _roundMinutesOverride : _roundDurationMinutes;
    public void SetRoundMinutes(float minutes) { if (minutes > 0f) _roundMinutesOverride = minutes; }

    [Header("Kill Target Settings")]
    [Tooltip("Solo: 1 player đạt đủ số kill này thì kết thúc trận. Team: Red hoặc Blue đạt đủ thì kết thúc.")]
    [Networked] private int _killTarget { get; set; } = 100;
    public int KillTarget => _killTarget;

    // Truoc day RoundElapsed/TotalElapsed la [Networked] float
    // += DeltaTime MOI TICK -> 2 word dirty x moi client x moi tick. Nay chi sync 2 tick moc (doi 1-2 lan/tran),
    // moi may tu tinh tu Runner.Tick + _roundTimer (da networked).
    // che do choi cua ca tran, sync 1 lan (truoc day nam trong moi NPlayer).
    [Networked] public GamePlayMode Mode { get; private set; }

    /// <summary>[MM-BACKFILL] Con it hon so giay nay trong hiep thi khong nhan nguoi vao giua tran nua.</summary>
    public const float LATE_JOIN_MIN_SECONDS = 60f;
    private string _rosterPublished; // [Aemy 2026-10-22] roster da ghi len SessionInfo (chi ghi lai khi doi)

    [Networked] public int StartTick { get; private set; } // tick bat dau tran (gan trong Spawned khi khong phai resume)
    [Networked] public int EndTick   { get; private set; } // tick khi IsMatchOver lan dau (0 = chua ket thuc)

    /// <summary>Giay CON LAI trong hiep (ten giu nguyen de khong vo UI cu). Dong bang khi tran ket thuc.</summary>
    public float RoundElapsed
    {
        get
        {
            if (Runner == null || Object == null || !Object.IsValid) return 0f;
            if (!GameStarted) return CurrentRound > 1 ? RoundMinutes * 60f : 0f;
            int? target = _roundTimer.TargetTick;
            if (!target.HasValue) return 0f;
            int now = EndTick > 0 ? EndTick : Runner.Tick.Raw;
            return Mathf.Max(0f, (target.Value - now) * Runner.DeltaTime);
        }
    }

    /// <summary>Tong giay da choi ca tran.</summary>
    public float TotalElapsed
    {
        get
        {
            if (Runner == null || Object == null || !Object.IsValid || StartTick <= 0) return 0f;
            int now = EndTick > 0 ? EndTick : Runner.Tick.Raw;
            return Mathf.Max(0f, (now - StartTick) * Runner.DeltaTime);
        }
    }

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
    // Con <=1 nguoi (sau khi tung co >=2) -> ket thuc, nguoi/doi con lai thang.
    [Networked] public NetworkBool EndedByForfeit { get; private set; }
    [Networked] public int ForfeitWinnerTeam { get; private set; }
    [Networked] public NetworkId ForfeitWinnerId { get; private set; }
    private bool _hadTwoPlayers;
    private bool _hadBothTeams; // che do Team: tung co ca 2 doi
    //[Networked] public int AllKill  { get; private set; }

    [Header("Kill Stats Settings")]
    [SerializeField] private float _killUpdateInterval = 0.5f; // host tính lại mỗi 0.5s
    private float _killUpdateTimer;
    private readonly Dictionary<NetworkId, KillInfo> _killLookup = new Dictionary<NetworkId, KillInfo>();

    // ── Persistent kill tracking (chống mất kill khi player rời phòng) ────────
    // Lưu kill snapshot của từng player ở lần update trước để tính delta
    private readonly Dictionary<NetworkId, int> _playerKillSnapshot = new Dictionary<NetworkId, int>();
    // Tích lũy kill của các player đã rời phòng, không bao giờ giảm
    private int _redKillAccumulated  = 0;
    private int _blueKillAccumulated = 0;

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

        // Nếu CurrentRound > 0 nghĩa là resume từ snapshot → không reset
        // [NET-OPT] doi tu "TotalElapsed > 0" vi TotalElapsed gio la thuoc tinh tinh tu tick.
        if (CurrentRound > 0)
        {
            Debug.Log("<color=orange>[NetworkGameState]</color> Resumed from host migration");
            return;
        }

        CurrentRound         = 1;
        if (GameManager.Instance != null) Mode = GameManager.Instance.GetGamePlayMode();
        StartTick            = Runner.Tick.Raw;
        EndTick              = 0;
        GameStarted          = false;
        RoundEnded           = false;
        _redKillAccumulated  = 0;
        _blueKillAccumulated = 0;
        _playerKillSnapshot.Clear();
        EndedByForfeit = false;
        ForfeitWinnerTeam = -1;
        _hadTwoPlayers = false;
        _hadBothTeams = false;
        StartCountdown();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (EndTick == 0 && IsMatchOver())
            EndTick = Runner.Tick.Raw;

        // Cho nguoi moi vao tran dang dien ra (QuickJoin o MenuShell) khi con du thoi gian;
        // dong phong khi tran da ket thuc hoac con < LATE_JOIN_MIN_SECONDS. Kiem tra ~2 lan/giay.
        if (Runner.IsServer && Runner.Tick.Raw % 32 == 0 && Runner.SessionInfo != null && Runner.SessionInfo.IsValid)
        {
            bool joinable = !IsMatchOver() && (!GameStarted || RoundElapsed > LATE_JOIN_MIN_SECONDS);
            if (Runner.SessionInfo.IsOpen != joinable) Runner.SessionInfo.IsOpen = joinable;
            // [Aemy 2026-10-22] Ghi ten + doi nguoi dang trong phong -> nguoi vao sau thay du roster o man ghep tran.
            SessionRoster.Publish(Runner, ref _rosterPublished);
        }

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

        // Con <=1 nguoi choi (sau khi tung co >=2) -> ket thuc, nguoi/doi con lai thang.
        if (GameStarted && !MatchEndedByKillTarget)
        {
            int active = 0; PlayerRef lastRef = default;
            int blue = 0, red = 0;
            foreach (var pr in Runner.ActivePlayers)
            {
                active++; lastRef = pr;
                if (Runner.TryGetPlayerObject(pr, out var po) && po != null)
                {
                    var tp = po.GetComponent<NPlayer>();
                    if (tp != null) { if (tp.NetworkedTeam == PlayerTeam.Blue) blue++; else if (tp.NetworkedTeam == PlayerTeam.Red) red++; }
                }
            }
            if (active >= 2) _hadTwoPlayers = true;
            if (blue > 0 && red > 0) _hadBothTeams = true;

            // Team: 1 doi khong con ai (doi kia van con nguoi) -> doi con lai thang.
            bool isTeam = GameManager.Instance != null && GameManager.Instance.GetGamePlayMode() == GamePlayMode.Team;
            if (isTeam && _hadBothTeams && active >= 1 && (blue == 0 || red == 0))
            {
                ForfeitWinnerTeam = blue > 0 ? (int)PlayerTeam.Blue : (int)PlayerTeam.Red;
                EndedByForfeit = true;
                MatchEndedByKillTarget = true;
                RoundEnded = true;
                CurrentRound = _totalRounds + 1;
            }
            else if (_hadTwoPlayers && active <= 1)
            {
                if (active == 1 && Runner.TryGetPlayerObject(lastRef, out var lastObj) && lastObj != null)
                {
                    var np = lastObj.GetComponent<NPlayer>();
                    if (np != null) { ForfeitWinnerTeam = (int)np.NetworkedTeam; ForfeitWinnerId = np.Object.Id; }
                }
                EndedByForfeit = true;
                MatchEndedByKillTarget = true;
                RoundEnded = true;
                CurrentRound = _totalRounds + 1;
            }
        }

        if (!GameStarted)
        {
            if (_countdownTimer.Expired(Runner))
            {
                GameStarted  = true;
                RoundEnded   = false;
                _roundTimer  = TickTimer.CreateFromSeconds(Runner, RoundMinutes * 60f);
            }
            return;
        }

        if (!RoundEnded)
        {
            if (_roundTimer.Expired(Runner))
            {
                RoundEnded = true;

                if (CurrentRound < _totalRounds)
                {
                    CurrentRound++;
                    GameStarted  = false;
                    RoundEnded   = false;
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
            int assist = player.Assist;
            int totalEXP = player.TotalExp;
            _killLookup[player.Object.Id] = new KillInfo(player.Object.Id, player.nPlayer.NicknameText, team, player._kill, deathcount,  assist, totalEXP, player.nPlayer.AvatarUrlText);
        }

        // ── Build leaderboard đã sort giảm dần theo kill ────────────────────────
        // Dùng danh sách riêng, không sort trực tiếp Dictionary vì Dictionary
        // không đảm bảo (và không nên phụ thuộc vào) thứ tự phần tử.
        _killLeaderboard.Clear();
        _killLeaderboard.AddRange(_killLookup.Values);
        _killLeaderboard.Sort((a, b) => b.Kills.CompareTo(a.Kills));
    }
    
    /// <summary>
    /// Chỉ host gọi (trong FixedUpdateNetwork): cộng thêm delta kill từ
    /// _killLookup vào accumulator, đảm bảo RedKill/BlueKill chỉ tăng chứ
    /// không bao giờ giảm — kể cả khi player rời phòng giữa chừng.
    /// </summary>
    private void ApplyNetworkedKillStats()
    {
        // Tập hợp các player hiện đang online trong lần update này
        var currentIds = new HashSet<NetworkId>(_killLookup.Keys);

        // Phát hiện player đã rời phòng: có trong snapshot nhưng không còn trong _killLookup
        var leftPlayers = new List<NetworkId>();
        foreach (var id in _playerKillSnapshot.Keys)
            if (!currentIds.Contains(id))
                leftPlayers.Add(id);

        // Xóa snapshot của player đã rời — kill của họ đã được cộng vào
        // accumulator ở lần update cuối cùng họ còn online, nên không mất.
        foreach (var id in leftPlayers)
            _playerKillSnapshot.Remove(id);

        // Với từng player hiện online: tính delta so với snapshot trước
        foreach (KillInfo killInfo in _killLookup.Values)
        {
            _playerKillSnapshot.TryGetValue(killInfo.PlayerId, out int prevKills);
            int delta = Mathf.Max(0, killInfo.Kills - prevKills); // chỉ cộng, không trừ

            if (delta > 0)
            {
                if (killInfo.Team == PlayerTeam.Red)
                    _redKillAccumulated  += delta;
                else if (killInfo.Team == PlayerTeam.Blue)
                    _blueKillAccumulated += delta;
            }

            // Cập nhật snapshot cho lần sau
            _playerKillSnapshot[killInfo.PlayerId] = killInfo.Kills;
        }

        // Ghi vào [Networked] để Fusion đồng bộ xuống mọi client
        RedKill  = _redKillAccumulated;
        BlueKill = _blueKillAccumulated;
        //AllKill  = _redKillAccumulated + _blueKillAccumulated;
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
        var _startCountdownDuration = 5f;

        if (GameManager.Instance != null)
            _startCountdownDuration = GameManager.Instance.StartGameDuration;
        
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
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }
}