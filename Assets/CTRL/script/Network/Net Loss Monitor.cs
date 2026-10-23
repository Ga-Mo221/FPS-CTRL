using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

/// <summary>
/// Đo mất gói của 2 luồng dữ liệu Fusion, gắn chung NetworkObject với NetworkGameState.
///
/// • Chiều xuống (host → client, luồng mang biến [Networked]): host gửi RPC dò KHÔNG tin cậy có số thứ tự,
///   client đếm số bị hụt. RPC unreliable đi chung gói UDP với state nên tỉ lệ hụt ≈ tỉ lệ mất gói state.
///   Kèm "State gap": khoảng lâu nhất giữa 2 lần client nhận tick server mới (bình thường ≈ 1/ServerSend).
/// • Chiều lên (client → host, luồng mang input): client gửi RPC dò ngược lại → host đếm hụt.
///   Kèm "Input thiếu": số tick host phải mô phỏng mà KHÔNG có input của client (Fusion gọi OnInputMissing
///   và dùng lại input cũ) → đây là mất input thực sự ảnh hưởng gameplay (gửi dư nhiều bản vẫn không tới kịp).
///
/// Host gom số liệu của từng client, gửi lại cho client đó mỗi cửa sổ và ghi log [NetLoss] định kỳ.
/// </summary>
public class NetLossMonitor : NetworkBehaviour
{
    public static NetLossMonitor Instance { get; private set; }

    [SerializeField] private float _probeRate     = 32f;  // gói dò / giây mỗi chiều (≈ ServerSend rate)
    [SerializeField] private float _windowSeconds = 2f;   // cửa sổ tính %
    [SerializeField] private float _logInterval   = 10f;  // host ghi log tổng hợp
    [SerializeField] private float _warmupSeconds = 2f;   // bỏ qua input thiếu ngay sau khi client vào

    // ── Số liệu của máy này (client: của chính nó; host: tệ nhất trong các client) ──
    public float DownLossPct     { get; private set; }
    public float UpLossPct       { get; private set; }
    public float InputMissingPct { get; private set; }
    public float StateGapMs      { get; private set; }
    public bool  HasData         { get; private set; }

    /// <summary>Dòng hiển thị ngắn cho HUD.</summary>
    public string Summary => !HasData ? "Loss: --" :
        $"Loss ↓{DownLossPct:0.0}% ↑{UpLossPct:0.0}% | Input thiếu {InputMissingPct:0.0}% | Gap {StateGapMs:0}ms" +
        (Runner != null && Runner.IsServer ? " (max client)" : "");

    /// <summary>Đếm gói dò theo số thứ tự. Gói tới muộn/sai thứ tự không tính là mất.</summary>
    private class SeqCounter
    {
        private int _last = -1;
        private int _received, _lost;
        public long TotalReceived, TotalLost;

        public void Add(int seq)
        {
            _received++;
            if (_last < 0) { _last = seq; return; }
            int d = seq - _last;
            if (d > 0) { _lost += d - 1; _last = seq; }
            else if (_lost > 0) _lost--;
        }

        /// <summary>% mất trong cửa sổ vừa qua rồi reset cửa sổ. -1 khi không nhận được gì.</summary>
        public float TakeWindowPct()
        {
            int total = _received + _lost;
            float pct = total > 0 ? _lost * 100f / total : -1f;
            TotalReceived += _received; TotalLost += _lost;
            _received = 0; _lost = 0;
            return pct;
        }

        public float TotalPct => TotalReceived + TotalLost > 0 ? TotalLost * 100f / (TotalReceived + TotalLost) : 0f;
    }

    private class ClientStats
    {
        public readonly SeqCounter Up = new();
        public float JoinTime;
        public int Ticks, Missing;          // cửa sổ hiện tại
        public long TotalTicks, TotalMissing;
        public float UpPct, InputPct;       // cửa sổ gần nhất
        public float DownPct, GapMs;        // client tự báo lên
    }

    // Client
    private readonly SeqCounter _down = new();
    private int _upSeq;
    private int _lastServerTick;
    private float _lastServerTickTime, _gapMaxWindow;

    // Host
    private readonly Dictionary<PlayerRef, ClientStats> _clients = new();
    private int _downSeq;
    private float _logTimer;

    private float _probeTimer, _windowTimer;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public override void Spawned()
    {
        Instance = this;
        _lastServerTickTime = Time.unscaledTime;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Gọi từ INetworkRunnerCallbacks.OnInputMissing (chỉ host nhận callback này).</summary>
    public void NoteInputMissing(PlayerRef player)
    {
        if (_clients.TryGetValue(player, out var s) && Time.unscaledTime - s.JoinTime >= _warmupSeconds)
            s.Missing++;
    }

    public override void FixedUpdateNetwork()
    {
        // Host: mỗi tick tiến tới cần đúng 1 input của mỗi client → mẫu số của "Input thiếu".
        if (!Runner.IsServer || !Runner.IsForward) return;
        float now = Time.unscaledTime;
        foreach (var player in Runner.ActivePlayers)
        {
            if (player == Runner.LocalPlayer) continue;
            var s = GetClient(player);
            if (now - s.JoinTime >= _warmupSeconds) s.Ticks++;
        }
    }

    private ClientStats GetClient(PlayerRef player)
    {
        if (!_clients.TryGetValue(player, out var s))
            _clients[player] = s = new ClientStats { JoinTime = Time.unscaledTime };
        return s;
    }

    private void Update()
    {
        if (Runner == null || !Runner.IsRunning) return;
        float dt = Time.unscaledDeltaTime;

        if (!Runner.IsServer) TrackStateGap();

        // Gói dò
        _probeTimer += dt;
        float interval = 1f / Mathf.Max(1f, _probeRate);
        if (_probeTimer >= interval)
        {
            _probeTimer %= interval;
            if (Runner.IsServer) { if (_clients.Count > 0) RPC_DownProbe(_downSeq++); }
            else RPC_UpProbe(_upSeq++, DownLossPct, StateGapMs);
        }

        // Cửa sổ
        _windowTimer += dt;
        if (_windowTimer >= _windowSeconds)
        {
            _windowTimer = 0f;
            if (Runner.IsServer) CloseHostWindow();
            else CloseClientWindow();
        }

        if (Runner.IsServer)
        {
            _logTimer += dt;
            if (_logTimer >= _logInterval) { _logTimer = 0f; LogHost(); }
        }
    }

    // ── Client ───────────────────────────────────────────────────────────────
    private void TrackStateGap()
    {
        int tick = Runner.LatestServerTick.Raw;
        float now = Time.unscaledTime;
        if (tick != _lastServerTick)
        {
            _lastServerTick = tick;
            _lastServerTickTime = now;
        }
        _gapMaxWindow = Mathf.Max(_gapMaxWindow, (now - _lastServerTickTime) * 1000f);
    }

    private void CloseClientWindow()
    {
        float pct = _down.TakeWindowPct();
        if (pct >= 0f) { DownLossPct = pct; HasData = true; }
        StateGapMs = _gapMaxWindow;
        _gapMaxWindow = 0f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
    private void RPC_DownProbe(int seq) => _down.Add(seq);

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Report([RpcTarget] PlayerRef target, float upLossPct, float inputMissingPct)
    {
        UpLossPct = upLossPct;
        InputMissingPct = inputMissingPct;
    }

    // ── Host ─────────────────────────────────────────────────────────────────
    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
    private void RPC_UpProbe(int seq, float downLossPct, float stateGapMs, RpcInfo info = default)
    {
        var s = GetClient(info.Source);
        s.Up.Add(seq);
        s.DownPct = downLossPct;
        s.GapMs = stateGapMs;
    }

    private void CloseHostWindow()
    {
        // Bỏ client đã rời phòng
        List<PlayerRef> gone = null;
        foreach (var kv in _clients)
            if (!IsActive(kv.Key)) (gone ??= new List<PlayerRef>()).Add(kv.Key);
        if (gone != null) foreach (var p in gone) _clients.Remove(p);

        float down = 0f, up = 0f, input = 0f, gap = 0f;
        foreach (var kv in _clients)
        {
            var s = kv.Value;
            float upPct = s.Up.TakeWindowPct();
            if (upPct >= 0f) s.UpPct = upPct;
            s.InputPct = s.Ticks > 0 ? s.Missing * 100f / s.Ticks : 0f;
            s.TotalTicks += s.Ticks; s.TotalMissing += s.Missing;
            s.Ticks = 0; s.Missing = 0;

            RPC_Report(kv.Key, s.UpPct, s.InputPct);

            down = Mathf.Max(down, s.DownPct); up = Mathf.Max(up, s.UpPct);
            input = Mathf.Max(input, s.InputPct); gap = Mathf.Max(gap, s.GapMs);
        }

        DownLossPct = down; UpLossPct = up; InputMissingPct = input; StateGapMs = gap;
        HasData = _clients.Count > 0;
    }

    private bool IsActive(PlayerRef player)
    {
        foreach (var p in Runner.ActivePlayers) if (p == player) return true;
        return false;
    }

    private void LogHost()
    {
        if (_clients.Count == 0) return;
        var sb = new StringBuilder("<color=orange>[NetLoss]</color>");
        foreach (var kv in _clients)
        {
            var s = kv.Value;
            float inputTotal = s.TotalTicks > 0 ? s.TotalMissing * 100f / s.TotalTicks : 0f;
            sb.Append($"\n  {kv.Key}: ↓{s.DownPct:0.0}% ↑{s.UpPct:0.0}% (tổng ↑{s.Up.TotalPct:0.00}%)" +
                      $" | input thiếu {s.InputPct:0.0}% (tổng {inputTotal:0.00}%, {s.TotalMissing}/{s.TotalTicks} tick)" +
                      $" | gap {s.GapMs:0}ms | rtt {Runner.GetPlayerRtt(kv.Key) * 1000.0:0}ms");
        }
        Debug.Log(sb.ToString(), this);
    }
}
