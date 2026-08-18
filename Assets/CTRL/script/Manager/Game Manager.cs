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
    
    [SerializeField] private float _respawnTime = 5f;
    public float RespawnTime => _respawnTime;
    
    [SerializeField] private WeaponInfo[] _weapons;
    
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

    public void NewGame()
    {
        OnUI = false;
        PlayerList = new List<NShootHandler>();
        DictionaryKill = new Dictionary<NetworkId, KillInfo>();
        RedKill = 0;
        BlueKill = 0;
        AllKill = 0;

        foreach (Transform chill in BulletParrent)
            Destroy(chill.gameObject);
        
        foreach (Transform chill in BulletCaseParrent)
            Destroy(chill.gameObject);
    }

    public GameObject GetWeapon(string name)
    {
        foreach (var weapon in _weapons)
            if (weapon.Name.Contains(name))
                return weapon.Weapon;
        return null;
    }
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

[System.Serializable]
public struct WeaponInfo
{
    [SerializeField] private string _name;
    [SerializeField] private GameObject _weapon_Prefab;
    
    public string Name => _name;
    public GameObject Weapon => _weapon_Prefab;
}
