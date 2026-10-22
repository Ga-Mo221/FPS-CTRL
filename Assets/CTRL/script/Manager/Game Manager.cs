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
    public VolumeController VolumeController;
    
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

    [TabGroup("GameManager", "Player")] 
    public Transform BulletParrent;

    [TabGroup("GameManager", "Player")] 
    public Transform BulletCaseParrent;

    [TabGroup("GameManager", "Player")] 
    [SerializeField] private NPlayer[] _character_Prefabs;
    
    [TabGroup("GameManager", "Player")]
    [SerializeField] private WeaponInfo[] _weapon_Prefabs;

    [FoldoutGroup("GameManager/Player/Grenade")] 
    [SerializeField] private Sprite _fragSprite;
    
    [FoldoutGroup("GameManager/Player/Grenade")] 
    [SerializeField] private GrenadeHandler[] _grenade_Prefabs;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _locaGameOverUI;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private bool _startHostMigration;
    
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

    [Header("Current Balo")] 
    [Sirenix.OdinInspector.ReadOnly] 
    [TabGroup("GameManager", "Runtime")] 
    [SerializeField] private BaloData _currentBalo = new BaloData(
        WeaponID._9MM_DEMON, WeaponID._9MM_DEMON, WeaponID._FRAG_M67, WeaponID._FLASH_M84
        );

    [Header("Current Weapon")] 
    [Sirenix.OdinInspector.ReadOnly] 
    [TabGroup("GameManager", "Runtime")] 
    [SerializeField] private string _currentWeaponOne = WeaponID._9MM_DEMON;
    
    [Sirenix.OdinInspector.ReadOnly]
    [TabGroup("GameManager", "Runtime")]
    [SerializeField] private string _cureentWeaponTwo = WeaponID._9MM_DEMON;

    [Header("Current Loadout")] [Sirenix.OdinInspector.ReadOnly] 
    [TabGroup("GameManager", "Runtime")] 
    [SerializeField] private GamePlaySetupData _setupData = new GamePlaySetupData(
        CharacterName._GHOST,
        new BaloData(WeaponID._9MM_DEMON, WeaponID._9MM_DEMON, WeaponID._FRAG_M67, WeaponID._FLASH_M84),
        new BaloData(WeaponID._9MM_DEMON, WeaponID._9MM_DEMON, WeaponID._FRAG_M67, WeaponID._FLASH_M84),
        new BaloData(WeaponID._9MM_DEMON, WeaponID._9MM_DEMON, WeaponID._FRAG_M67, WeaponID._FLASH_M84)
        );
    
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
    
    public void SetLocalGameOverUI(bool value) => _locaGameOverUI = value;
    public bool GetLocalGameOverUI() => _locaGameOverUI;
    
    public void SetStartHostMigration(bool value) => _startHostMigration = value;
    public bool GetStartHostMigration() => _startHostMigration;
    
    public void setPlayerID(NetworkId playerId) => _playerId = playerId;
    
    public NetworkId GetPlayerID() => _playerId;
    
    public float RespawnTime => _respawnTime;
    
    public float CanTakeDamageTime => _canTakeDamageTime;
    
    public float StartGameDuration => _startGameDuration;
    
    public int KillTarget => _killTarget;
    public void SetKillTarget(int killTarget) => _killTarget = killTarget;

    // Thoi gian tran (phut) chu phong dat cho phong tuy chinh. 0 = dung mac dinh cua NetworkGameState (10 phut).
    private float _roundMinutes = 0f;
    public float RoundMinutes => _roundMinutes;
    public void SetRoundMinutes(float minutes) => _roundMinutes = Mathf.Max(0f, minutes);
    
    public int GetEXPperKill()  => _baseExpPerKill;
    
    public void ResetStreak() => _lastBestStreak = 0;

    public void SetBestStreak()
    {
        _lastBestStreak ++;
        if (_lastBestStreak > _bestStreak)
            _bestStreak = _lastBestStreak;
    }
    
    public int GetBestStreak() => _bestStreak;

    /// <summary>Chuoi kill hien tai cua local player (reset khi chet) - dung cho am thanh dominating/rampage.</summary>
    public int GetCurrentStreak() => _lastBestStreak;
    
    public bool SetOnUIOptions(bool status)
    {
        if (status)
        {
            if (_onUIChatBox) _onUIChatBox = false;
            if (_onUILoadout)
            {
                _onUIOption = false;
                return false;
            }
        }
        _onUIOption = status;
        
        SetMouseVisible();
        return true;
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
        
        SetMouseVisible();
    }
    
    public bool OnUILoadout() => _onUILoadout;

    public bool SetOnuiLoadout(bool status)
    {
        if (status)
        {
            _onUIOption = false;
            _onUIChatBox = false;
            _onUILoadout = true;
        }
        else _onUILoadout = false;

        SetMouseVisible();
        return true;
    }

    public bool OnUI() => _onUIOption || _onUIChatBox || _onUILoadout;

    public void SetConnectionToken(byte[] token) => connectionToken = token;

    // ✅ Trả về đúng 16 byte identity gốc (không kèm payload loadout).
    // Dùng để đăng ký danh tính (Rpc_RegisterToken) - nơi cần length == 16 chính xác.
    // BuildConnectionToken() ở dưới trả về 16 byte này + payload nên length luôn > 16
    // và không dùng được cho mục đích identity.
    public byte[] GetIdentityToken() => connectionToken;

    public byte[] BuildConnectionToken()
    {
        using var ms = new System.IO.MemoryStream();
        using var bw = new System.IO.BinaryWriter(ms);

        bw.Write(connectionToken); // 16 byte identity gốc, không đổi

        WriteStr(bw, _setupData.CharacterName);
        WriteStr(bw, _setupData.Balo_One.Weapon_One);
        WriteStr(bw, _setupData.Balo_One.Weapon_Two);
        WriteStr(bw, _setupData.Balo_One.Grenade_Q);
        WriteStr(bw, _setupData.Balo_One.Grenade_E);

        return ms.ToArray();
    }

    private static void WriteStr(System.IO.BinaryWriter bw, string s)
    {
        s ??= "";
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        bw.Write((byte)Mathf.Min(bytes.Length, 255));
        bw.Write(bytes, 0, Mathf.Min(bytes.Length, 255));
    }

    public GamePlayMode GetGamePlayMode() => _gamePlayMode;
    
    public void SetGamePlayMode(GamePlayMode gamePlayMode) => _gamePlayMode = gamePlayMode;
    public void SetPlayerNickName(string nickname) => _playerNickName = nickname;
    public string GetPlayerNickName() => _playerNickName;
    
    public bool GetCanDragItem() => _canDragItem;
    public void SetCanDragItem(bool canDragItem) => _canDragItem = canDragItem;
    public DropItemWeapon GetDropItemWeapon => _dropItemWeapon;
    public void SetDropItemWeapon(DropItemWeapon item) => _dropItemWeapon = item;
    
    public GamePlaySetupData GetSetupData() => _setupData;
    public void SetSetupData(GamePlaySetupData setupData) => _setupData = setupData;
    // Set nhan vat dang chon (-> _setupData -> token -> NSpawner spawn dung prefab).
    public void SetCharacter(string name) { if (!string.IsNullOrEmpty(name)) _setupData.CharacterName = name; }
    
    public void setCurrentBalo(BaloData data)
    {
        _currentBalo = data;
        _currentWeaponOne = _currentBalo.Weapon_One;
        _cureentWeaponTwo = _currentBalo.Weapon_Two;
    }

    public void SetCurrentWeapon(int index, string name)
    {
        if (index == 1)
            _currentWeaponOne = name;
        else if (index == 2)
            _cureentWeaponTwo =  name;
        else Debug.LogWarning("<color=yellow>[GameManager]</color> Set Current weapon Out Index");
    }

    public (bool, string, string) GetCurrentBalo()
    {
        bool change = false;

        if (!_currentWeaponOne.IsNullOrEmpty() && _currentBalo.Weapon_One != _currentWeaponOne)
        {
            change = true;
            _currentWeaponOne = _currentBalo.Weapon_One;
        }
        
        if (!_cureentWeaponTwo.IsNullOrEmpty() && _currentBalo.Weapon_Two != _cureentWeaponTwo)
        {
            change = true;
            _cureentWeaponTwo = _currentBalo.Weapon_Two;
        }

        return (change, _currentBalo.Weapon_One, _currentBalo.Weapon_Two);
    }

    private void SetMouseVisible(bool state = false)
    {
        if (!state)
        {
            bool visible = false;
            if (_onUIOption || _onUILoadout) visible = true;

            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void NewGame()
    {
        Debug.Log("<color=yellow>[GameManager]</color> NewGame");
        SetStartHostMigration(false);
        SetLocalGameOverUI(false);
        SetOnuiLoadout(true);
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
        
        SetMouseVisible(true);
    }

    public GameObject GetWeapon(string ID)
    {
        if (ID.IsNullOrEmpty()) return null;
        
        if (_weapon_Prefabs == null || _weapon_Prefabs.Length == 0) return null;
        WeaponInfo exact; // uu tien khop dung ID (skin "Vesper_DR" chua chuoi "Vesper")
        if (TryFindExactWeapon(ID, out exact)) return exact.Weapon;
        foreach (var weapon in _weapon_Prefabs)
            if (weapon.ID().Contains(ID))
                return weapon.Weapon;
        return null;
    }

    public Sprite GetWeaponIcon(string ID)
    {
        if (ID == "Frag")
            return _fragSprite;
        
        WeaponInfo exact;
        if (TryFindExactWeapon(ID, out exact)) return exact.Icon();
        foreach (var weapon in _weapon_Prefabs)
        {
            if (weapon.ID().Contains(ID))
                return weapon.Icon();
        }
        
        // Kill feed truyen WeaponName (vd "Vesper Dark Red") chu khong phai ID -> khop them theo ten.
        if (!string.IsNullOrEmpty(ID))
            foreach (var weapon in _weapon_Prefabs)
            {
                var d = weapon.Data();
                if (d != null && string.Equals(d.WeaponName, ID, System.StringComparison.OrdinalIgnoreCase))
                    return weapon.Icon();
            }
        
        return null;
    }

    public WeaponData GetWeaponData(string ID)
    {
        WeaponInfo exact;
        if (TryFindExactWeapon(ID, out exact)) return exact.Data();
        foreach (var weapon in _weapon_Prefabs)
        {
            if (weapon.ID().Contains(ID))
                return weapon.Data();
        }
        
        return null;
    }

    public WeaponInfo[] GetWeaponDataList() => _weapon_Prefabs;

    // Kill feed gui chi so vu khi (byte) thay vi chuoi ten.
    // Moi may dung cung GameManager prefab -> cung thu tu _weapon_Prefabs.
    public int GetWeaponIndexByName(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName) || _weapon_Prefabs == null) return -1;
        for (int i = 0; i < _weapon_Prefabs.Length; i++)
        {
            var d = _weapon_Prefabs[i].Data();
            if (d != null && d.WeaponName == weaponName) return i;
        }
        for (int i = 0; i < _weapon_Prefabs.Length; i++)
            if (string.Equals(_weapon_Prefabs[i].ID(), weaponName, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // DropItemWeapon sync byte chi so thay vi NetworkString ID.
    public int GetWeaponIndexById(string id)
    {
        if (string.IsNullOrEmpty(id) || _weapon_Prefabs == null) return -1;
        for (int i = 0; i < _weapon_Prefabs.Length; i++)
            if (string.Equals(_weapon_Prefabs[i].ID(), id, System.StringComparison.OrdinalIgnoreCase)) return i;
        for (int i = 0; i < _weapon_Prefabs.Length; i++)
            if (_weapon_Prefabs[i].ID().Contains(id)) return i;
        return -1;
    }

    public string GetWeaponIdByIndex(int index)
    {
        if (_weapon_Prefabs == null || index < 0 || index >= _weapon_Prefabs.Length) return null;
        return _weapon_Prefabs[index].ID();
    }

    // NShootHandler sync byte chi so luu dan thay vi NetworkString.
    public int GetGrenadeIndex(string id)
    {
        if (string.IsNullOrEmpty(id) || _grenade_Prefabs == null) return -1;
        for (int i = 0; i < _grenade_Prefabs.Length; i++)
            if (_grenade_Prefabs[i] != null && _grenade_Prefabs[i].ID == id) return i;
        for (int i = 0; i < _grenade_Prefabs.Length; i++)
            if (_grenade_Prefabs[i] != null && _grenade_Prefabs[i].ID.Contains(id)) return i;
        return -1;
    }

    public string GetGrenadeIdByIndex(int index)
    {
        if (_grenade_Prefabs == null || index < 0 || index >= _grenade_Prefabs.Length || _grenade_Prefabs[index] == null) return null;
        return _grenade_Prefabs[index].ID;
    }

    public string GetWeaponNameByIndex(int index)
    {
        if (_weapon_Prefabs == null || index < 0 || index >= _weapon_Prefabs.Length) return null;
        var d = _weapon_Prefabs[index].Data();
        return d != null ? d.WeaponName : _weapon_Prefabs[index].ID();
    }

    // Khop CHINH XAC ID (khong phan biet hoa thuong). Cac ham Get* van giu Contains lam du phong nhu cu.
    private bool TryFindExactWeapon(string ID, out WeaponInfo found)
    {
        found = default(WeaponInfo);
        if (string.IsNullOrEmpty(ID) || _weapon_Prefabs == null) return false;
        foreach (var weapon in _weapon_Prefabs)
            if (string.Equals(weapon.ID(), ID, System.StringComparison.OrdinalIgnoreCase))
            { found = weapon; return true; }
        return false;
    }

    public GrenadeHandler GetGrenade(string name)
    {
        foreach (var gre in _grenade_Prefabs)
        {
            if (gre.ID.Contains(name))
                return gre;
        }
        return null;
    }
    
    public GrenadeHandler[]  GetGrenadeList() => _grenade_Prefabs;

    public NPlayer GetCharacter(string name)
    {
        foreach (var character in _character_Prefabs)
        {
            if (character.Name.Contains(name))
                return character;
        }
        return null;
    }
    
    public NPlayer[] GetCharacterList() => _character_Prefabs;
    // Port tu EclipseStrike: map scene + loadout cache (client-side) cho UI_Package.
    private string _selectedMapScene = "ShortMap1";
    public void SetSelectedMapScene(string sceneName) { if (!string.IsNullOrEmpty(sceneName)) _selectedMapScene = sceneName; }
    public string GetSelectedMapScene() => string.IsNullOrEmpty(_selectedMapScene) ? "ShortMap1" : _selectedMapScene;

    private string[] _loadoutWeapons = new string[2];
    private string[] _loadoutUtilities = new string[2];
    public void SetLoadout(string w0, string w1, string u0, string u1)
    {
        _loadoutWeapons[0] = w0; _loadoutWeapons[1] = w1;
        _loadoutUtilities[0] = u0; _loadoutUtilities[1] = u1;
    }
    public string GetLoadoutWeapon(int i)
    {
        if (i < 0 || i >= 2) return null;
        if (!string.IsNullOrEmpty(_loadoutWeapons[i])) return _loadoutWeapons[i];
        var pp = UnityEngine.PlayerPrefs.GetString("LoadoutActiveW" + i, "");
        return string.IsNullOrEmpty(pp) ? null : pp;
    }
    public string GetLoadoutUtility(int i)
    {
        if (i < 0 || i >= 2) return null;
        if (!string.IsNullOrEmpty(_loadoutUtilities[i])) return _loadoutUtilities[i];
        var pp = UnityEngine.PlayerPrefs.GetString("LoadoutActiveU" + i, "");
        return string.IsNullOrEmpty(pp) ? null : pp;
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

    public string AvatarUrl { get; private set; } 

    public KillInfo(NetworkId playerId, string name, PlayerTeam team, int kills, int deaths, int assist, int totalExp, string avatarUrl = "")
    {
        PlayerId = playerId;
        PlayerName = name;
        Team = team;
        Kills = kills;
        Deaths = deaths;
        Assist = assist;
        TotalExp = totalExp;
        AvatarUrl = avatarUrl;
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
    
    public string ID()
    {
        var ctrl = _weapon_Prefab.GetComponent<WeaponController>();
        var data = ctrl.Data;
        return data.ID;
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

[System.Serializable]
public struct GamePlaySetupData
{
    public string CharacterName;
    public BaloData Balo_One;
    public BaloData Balo_Two;
    public BaloData Balo_Three;

    public GamePlaySetupData(string charName, BaloData one, BaloData two, BaloData three)
    {
        CharacterName = charName;
        Balo_One = one;
        Balo_Two = two;
        Balo_Three = three;
    }
}