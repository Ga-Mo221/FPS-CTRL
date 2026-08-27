using System.Collections.Generic;
using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance {set; get;}
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private string _playerNickName;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private NetworkId _playerId;
    
    //[Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private GamePlayMode _gamePlayMode = GamePlayMode.Team;
    
    [TabGroup("GameManager", "Player Settings")]
    public FlashbangEffect FlashbangEffect;
    
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private float _respawnTime = 5f;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Player Settings")]
    public List<NShootHandler> PlayerList = new List<NShootHandler>();

    [TabGroup("GameManager", "Weapon")] 
    public Transform BulletParrent;

    [TabGroup("GameManager", "Weapon")] 
    public Transform BulletCaseParrent;
    
    [TabGroup("GameManager", "Weapon")] 
    [SerializeField] private Sprite _fragSprite;
    
    [TabGroup("GameManager", "Weapon")]
    [SerializeField] private WeaponInfo[] _weapons;
    
    
    [TabGroup("GameManager", "Runtime")]
    [Header("KILL")]
    [Sirenix.OdinInspector.ReadOnly]
    [SerializeField] private int RedKill = 0;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private int BlueKill = 0;
    
    // [Sirenix.OdinInspector.ReadOnly]
    // [TabGroup("GameManager", "Runtime")]
    // [SerializeField] private int AllKill = 0;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    public NetworkRunnerHandler NetworkRunnerHandler;
    
    private byte[] connectionToken;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
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
        // Kill count giờ được tính bởi host trong NetworkGameState và đồng bộ
        // xuống mọi client qua [Networked] properties. Ở đây chỉ đọc lại để
        // hiển thị trong Odin Inspector cho tiện debug.
        var gs = NetworkGameState.Instance;
        if (gs == null || gs.Object == null || !gs.Object.IsValid) return;

        RedKill  = NetworkGameState.Instance.RedKill;
        BlueKill = NetworkGameState.Instance.BlueKill;
        //AllKill  = NetworkGameState.Instance.AllKill;
    }
    
    // ======================== PUBLIC API ================================
    
    public void setPlayerID(NetworkId playerId) => _playerId = playerId;
    
    public NetworkId GetPlayerID() => _playerId;
    
    public float RespawnTime => _respawnTime;
    
    public bool OnUI {set; get;} = false;

    public void SetConnectionToken(byte[] token) => connectionToken = token;
    
    public byte[] GetConnectionToken() => connectionToken;

    public GamePlayMode GetGamePlayMode() => _gamePlayMode;
    
    public void SetGamePlayMode(GamePlayMode gamePlayMode) => _gamePlayMode = gamePlayMode;
    public void SetPlayerNickName(string nickname) => _playerNickName = nickname;
    public string GetPlayerNickName() => _playerNickName;

    public void NewGame()
    {
        OnUI = false;
        PlayerList = new List<NShootHandler>();
        RedKill = 0;
        BlueKill = 0;
        //AllKill = 0;

        foreach (Transform chill in BulletParrent)
            Destroy(chill.gameObject);
        
        foreach (Transform chill in BulletCaseParrent)
            Destroy(chill.gameObject);
    }

    public GameObject GetWeapon(string name)
    {
        foreach (var weapon in _weapons)
            if (weapon.Name().Contains(name))
                return weapon.Weapon;
        return null;
    }

    public Sprite GetWeaponIcon(string name)
    {
        if (name == "Frag")
            return _fragSprite;
        
        foreach (var weapon in _weapons)
        {
            Debug.Log($"{name} : {weapon.Name()}");
            if (weapon.Name().Contains(name))
            {
                Debug.Log("tra ve icon " + weapon.Icon());
                return weapon.Icon();
            }
        }
        
        return null;
    }
}

public struct KillInfo
{
    public NetworkId PlayerId { get; set; }
    public string PlayerName { get; set; }
    public PlayerTeam Team { get; set; }
    public int Kills { get; set; }
    
    public int Deaths { get; set; }

    public KillInfo(NetworkId playerId, string name, PlayerTeam team, int kills, int deaths)
    {
        PlayerId = playerId;
        PlayerName = name;
        Team = team;
        Kills = kills;
        Deaths = deaths;
    }
}

[System.Serializable]
public struct WeaponInfo
{
    [SerializeField] private GameObject _weapon_Prefab;
    public GameObject Weapon => _weapon_Prefab;

    public string Name()
    {
        var ctrl = _weapon_Prefab.GetComponent<WeaponController>();
        var data = ctrl.Data;
        return data.WeaponName;
    }

    public Sprite Icon()
    {
        var ctrl = _weapon_Prefab.GetComponent<WeaponController>();
        var data = ctrl.Data;
        return data.Icon;
    }
}