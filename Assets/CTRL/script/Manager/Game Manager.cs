using System.Collections.Generic;
using Fusion;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using WebSocketSharp;

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
    
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private float _canTakeDamageTime = 3f;
    
    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private float _startGameDuration = 10f;

    [TabGroup("GameManager", "Player Settings")]
    [SerializeField] private int _killTarget = 10;
    
    [TabGroup("GameManager", "Player Settings")]
    [Tooltip("Exp nhận được khi kill đủ 100% máu đối thủ")]
    [SerializeField] private int _baseExpPerKill = 150;
    
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
    
    
    // [TabGroup("GameManager", "Runtime")]
    // [Header("KILL")]
    // [Sirenix.OdinInspector.ReadOnly]
    // [SerializeField] private int RedKill = 0;
    //
    // [Sirenix.OdinInspector.ReadOnly]
    // [TabGroup("GameManager", "Runtime")]
    // [SerializeField] private int BlueKill = 0;
    
    // [Sirenix.OdinInspector.ReadOnly]
    // [TabGroup("GameManager", "Runtime")]
    // [SerializeField] private int AllKill = 0;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    public NetworkRunnerHandler NetworkRunnerHandler;

    [Sirenix.OdinInspector.ReadOnly] 
    [TabGroup("GameManager", "Runtime")] 
    [SerializeField] private int _bestStreak;
    
    private int _lastBestStreak;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _onUIOption;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _onUIChatBox;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _onUILoadout = true;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _canDragItem = false;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private DropItemWeapon _dropItemWeapon;
    
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
    
    // ======================== PUBLIC API ================================
    
    public void setPlayerID(NetworkId playerId) => _playerId = playerId;
    
    public NetworkId GetPlayerID() => _playerId;
    
    public float RespawnTime => _respawnTime;
    
    public float CanTakeDamageTime => _canTakeDamageTime;
    
    public float StartGameDuration => _startGameDuration;
    
    public int KillTarget => _killTarget;
    public void SetKillTarget(int killTarget) => _killTarget = killTarget;
    
    public int GetEXPperKill()  => _baseExpPerKill;
    
    public void ResetStreak() => _lastBestStreak = 0;

    public void SetBestStreak()
    {
        _lastBestStreak ++;
        if (_lastBestStreak > _bestStreak)
            _bestStreak = _lastBestStreak;
    }
    
    public int GetBestStreak() => _bestStreak;
    
    public void SetOnUIOptions(bool status)
    {
        if (status)
            if (_onUIChatBox) _onUIChatBox = false;
        _onUIOption = status;
    }
    
    public bool OnUIOption() => _onUIOption;
    public bool OnUIChatBox() => _onUIChatBox;

    public void SetOnUIChatBox(bool status)
    {
        if (status)
        {
            if (!_onUIOption && !_onUILoadout)
                _onUIChatBox = true;
        }
        else _onUIChatBox = false;
    }
    
    public bool OnUILoadout() => _onUILoadout;

    public void SetOnuiLoadout(bool status)
    {
        if (status)
        {
            _onUIOption = false;
            _onUIChatBox = false;
            _onUILoadout = true;
        }
        else _onUILoadout = false;
    }

    public bool OnUI() => _onUIOption || _onUIChatBox || _onUILoadout;

    public void SetConnectionToken(byte[] token) => connectionToken = token;
    
    public byte[] GetConnectionToken() => connectionToken;

    public GamePlayMode GetGamePlayMode() => _gamePlayMode;
    
    public void SetGamePlayMode(GamePlayMode gamePlayMode) => _gamePlayMode = gamePlayMode;
    public void SetPlayerNickName(string nickname) => _playerNickName = nickname;
    public string GetPlayerNickName() => _playerNickName;
    
    public bool GetCanDragItem() => _canDragItem;
    public void SetCanDragItem(bool canDragItem) => _canDragItem = canDragItem;
    public DropItemWeapon GetDropItemWeapon => _dropItemWeapon;
    public void SetDropItemWeapon(DropItemWeapon item) => _dropItemWeapon = item;

    public void NewGame()
    {
        SetOnUIOptions(false);
        SetOnUIChatBox(false);
        _bestStreak = 0;
        _lastBestStreak = 0;
        PlayerList = new List<NShootHandler>();
        //RedKill = 0;
        //BlueKill = 0;
        //AllKill = 0;

        foreach (Transform chill in BulletParrent)
            Destroy(chill.gameObject);
        
        foreach (Transform chill in BulletCaseParrent)
            Destroy(chill.gameObject);
    }

    public GameObject GetWeapon(string name)
    {
        if (name.IsNullOrEmpty()) return null;
        
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
            if (weapon.Name().Contains(name))
                return weapon.Icon();
        }
        
        return null;
    }

    public WeaponData GetWeaponData(string name)
    {
        foreach (var weapon in _weapons)
        {
            if (weapon.Name().Contains(name))
                return weapon.Data();
        }
        
        return null;
    }
}

public struct KillInfo
{
    public NetworkId PlayerId { get; private set; }
    
    public string PlayerName { get; private set; }
    
    public PlayerTeam Team { get; private set; }
    
    public int Kills { get; private set; }
    
    public int Assist { get; private set; }
    
    public int TotalExp { get; private set; }
    
    public int Deaths { get; private set; }

    public KillInfo(NetworkId playerId, string name, PlayerTeam team, int kills, int deaths, int assist, int totalExp)
    {
        PlayerId = playerId;
        PlayerName = name;
        Team = team;
        Kills = kills;
        Deaths = deaths;
        Assist = assist;
        TotalExp = totalExp;
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

    public WeaponData Data()
    {
        var ctrl = _weapon_Prefab.GetComponent<WeaponController>();
        return ctrl.Data;
    }
}