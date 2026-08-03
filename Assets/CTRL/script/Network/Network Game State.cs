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
    
    [Networked] public float RoundElapsed   { get; private set; } // giây đã chơi trong hiệp
    [Networked] public float TotalElapsed   { get; private set; } // tổng giây đã chơi cả trận

    // ── Networked State ──────────────────────────────────────────
    [Networked] private TickTimer _countdownTimer { get; set; }
    [Networked] private TickTimer _roundTimer     { get; set; }
    [Networked] public NetworkBool GameStarted    { get; private set; }
    [Networked] public NetworkBool RoundEnded     { get; private set; }
    [Networked] public int CurrentRound           { get; private set; }
    
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
                GameStarted   = true;
                RoundEnded    = false;
                RoundElapsed  = 0f;
                _roundTimer   = TickTimer.CreateFromSeconds(Runner, _roundDurationMinutes * 60f);
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
                    RPC_RespawnAllPlayers();
                }else if (CurrentRound == _totalRounds)
                {
                    RoundEnded = true;
                    CurrentRound++;
                }
            }
        }
    }

    private void StartCountdown()
    {
        _countdownTimer = TickTimer.CreateFromSeconds(Runner, _startCountdownDuration);
    }
    
    // NetworkGameState.cs
    // Thêm RPC respawn all players
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RespawnAllPlayers()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out var playerObj))
            {
                var movementHandler = playerObj.GetComponent<NCharMovementHandler>();
                if (movementHandler != null)
                    movementHandler.RequestRespawn();
            }
        }
    }

    // ── Helpers cho UI ──────────────────────────────────────────
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
    public bool   IsMatchOver()
    {
        if (Object == null || !Object.IsValid) return false;
        return RoundEnded && CurrentRound > _totalRounds;
    }
}