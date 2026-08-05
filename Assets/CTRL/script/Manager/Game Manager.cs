using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class GameManager : MonoBehaviour
{
    public static GameManager instance {set; get;}
    public bool OnUI = false;
    [SerializeField] private string _playerNickName;
    
    [SerializeField] private GamePlayMode _gamePlayMode = GamePlayMode.Team;
    [SerializeField] public FlashbangEffect FlashbangEffect;

    public Transform BulletParrent;
    public Transform BulletCaseParrent;

    public List<NShootHandler> PlayerList = new List<NShootHandler>();
    private Dictionary<NetworkId, KillInfo> DictionaryKill = new Dictionary<NetworkId, KillInfo>();
    [SerializeField] private int RedKill = 0;
    [SerializeField] private int BlueKill = 0;
    [SerializeField] private int AllKill = 0;
    
    public NetworkRunnerHandler NetworkRunnerHandler;
    
    private float _respawnTime = 5f;
    public float RespawnTime => _respawnTime;

    private byte[] connectionToken;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (connectionToken == null)
        {
            connectionToken = NConnectionTokenUtils.NewToken();
            Debug.Log($"<color=yellow>[GameManager]</color> Player Connection token {NConnectionTokenUtils.HashToken(connectionToken)}");
        }
    }

    private void Update()
    {
        foreach (var player in PlayerList)
        {
            if (player == null) continue;
            if (player.nPlayer.Object == null || !player.nPlayer.Object.IsValid)
                continue;
            PlayerTeam team = player.nPlayer.NetworkedTeam;
            DictionaryKill[player.Object.Id] = new KillInfo(team, player._kill);
        }
        
        int redKill = 0;
        int blueKill = 0;
        foreach (KillInfo killinfo in DictionaryKill.Values)
        {
            if (killinfo.Team == PlayerTeam.Red)
                redKill+= killinfo.Kills;
            if (killinfo.Team == PlayerTeam.Blue)
                blueKill+= killinfo.Kills;
        }
        RedKill = redKill;
        BlueKill = blueKill;
        AllKill = RedKill + BlueKill;
    }

    public void SetConnectionToken(byte[] token) => connectionToken = token;
    
    public byte[] GetConnectionToken() => connectionToken;

    public GamePlayMode GetGamePlayMode() => _gamePlayMode;
    
    public void SetGamePlayMode(GamePlayMode gamePlayMode) => _gamePlayMode = gamePlayMode;
    public void SetPlayerNickName(string nickname) => _playerNickName = nickname;
}

public struct KillInfo
{
    public PlayerTeam Team { get; set; }
    public int Kills { get; set; }

    public KillInfo(PlayerTeam team, int kills)
    {
        Team = team;
        Kills = kills;
    }
}
