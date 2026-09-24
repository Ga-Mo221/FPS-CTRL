using Game.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SetupManagerTestUI : MonoBehaviour
{
    [TabGroup("Setup Manager", "Balo")]
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo1")]
    [SerializeField] private Button Balo1_weaapon1;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo1")]
    [SerializeField] private Button Balo1_weaapon2;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo1")]
    [SerializeField] private Button Balo1_GrenadeQ;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo1")]
    [SerializeField] private Button Balo1_GrenadeE;
    
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo2")]
    [SerializeField] private Button Balo2_weaapon1;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo2")]
    [SerializeField] private Button Balo2_weaapon2;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo2")]
    [SerializeField] private Button Balo2_GrenadeQ;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo2")]
    [SerializeField] private Button Balo2_GrenadeE;
    
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo3")]
    [SerializeField] private Button Balo3_weaapon1;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo3")]
    [SerializeField] private Button Balo3_weaapon2;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo3")]
    [SerializeField] private Button Balo3_GrenadeQ;
    [TabGroup("Setup Manager/Balo/SubGroup", "Balo3")]
    [SerializeField] private Button Balo3_GrenadeE;
    
    
    [TabGroup("Setup Manager", "Character")]
    [SerializeField] private Button Character;

    [TabGroup("Setup Manager", "UI")] [SerializeField]
    private PanelSelectSetupTestUI PanelItem;

    public GamePlaySetupData _data;

    private void Start()
    {
        Balo1_weaapon1.onClick.AddListener(LoadPanelBalo1_weapon_1);
        Balo1_weaapon2.onClick.AddListener(LoadPanelBalo1_weapon_2);
        Balo1_GrenadeQ.onClick.AddListener(LoadPanelBalo1_Grenade_Q);
        Balo1_GrenadeE.onClick.AddListener(LoadPanelBalo1_Grenade_E);
        
        Balo2_weaapon1.onClick.AddListener(LoadPanelBalo2_weapon_1);
        Balo2_weaapon2.onClick.AddListener(LoadPanelBalo2_weapon_2);
        Balo2_GrenadeQ.onClick.AddListener(LoadPanelBalo2_Grenade_Q);
        Balo2_GrenadeE.onClick.AddListener(LoadPanelBalo2_Grenade_E);
        
        Balo3_weaapon1.onClick.AddListener(LoadPanelBalo3_weapon_1);
        Balo3_weaapon2.onClick.AddListener(LoadPanelBalo3_weapon_2);
        Balo3_GrenadeQ.onClick.AddListener(LoadPanelBalo3_Grenade_Q);
        Balo3_GrenadeE.onClick.AddListener(LoadPanelBalo3_Grenade_E);
        
        Character.onClick.AddListener(LoadPanelPlayer);
        
        
        string charName = PlayerPrefs.GetString("CharacterName", CharacterName._GHOST);
        var balo1 = new BaloData(
                PlayerPrefs.GetString("BL_1_W_1", WeaponID._9MM_DEMON),
                PlayerPrefs.GetString("BL_1_W_2", WeaponID._9MM_DEMON),
                PlayerPrefs.GetString("BL_1_G_Q", WeaponID._FRAG_M67),
                PlayerPrefs.GetString("BL_1_G_E", WeaponID._FLASH_M84)
            );
        
        var balo2 = new BaloData(
            PlayerPrefs.GetString("BL_2_W_1", WeaponID._9MM_DEMON),
            PlayerPrefs.GetString("BL_2_W_2", WeaponID._9MM_DEMON),
            PlayerPrefs.GetString("BL_2_G_Q", WeaponID._FRAG_M67),
            PlayerPrefs.GetString("BL_2_G_E", WeaponID._FLASH_M84)
        );
        
        var balo3 = new BaloData(
            PlayerPrefs.GetString("BL_3_W_1", WeaponID._9MM_DEMON),
            PlayerPrefs.GetString("BL_3_W_2", WeaponID._9MM_DEMON),
            PlayerPrefs.GetString("BL_3_G_Q", WeaponID._FRAG_M67),
            PlayerPrefs.GetString("BL_3_G_E", WeaponID._FLASH_M84)
        );
        
        _data = new GamePlaySetupData(charName, balo1, balo2, balo3);
        
        SaveSetup();
        
        RefreshButtonLabels();
    }

    public void SaveSetup()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetSetupData(_data);
        
        PlayerPrefs.SetString("CharacterName", _data.CharacterName);
        
        //Balo 1
        PlayerPrefs.SetString("BL_1_W_1", _data.Balo_One.Weapon_One);
        PlayerPrefs.SetString("BL_1_W_2", _data.Balo_One.Weapon_Two);
        PlayerPrefs.SetString("BL_1_G_Q", _data.Balo_One.Grenade_Q);
        PlayerPrefs.SetString("BL_1_G_E", _data.Balo_One.Grenade_E);
        
        // Balo 2
        PlayerPrefs.SetString("BL_2_W_1", _data.Balo_Two.Weapon_One);
        PlayerPrefs.SetString("BL_2_W_2", _data.Balo_Two.Weapon_Two);
        PlayerPrefs.SetString("BL_2_G_Q", _data.Balo_Two.Grenade_Q);
        PlayerPrefs.SetString("BL_2_G_E", _data.Balo_Two.Grenade_E);
        
        // Balo 3
        PlayerPrefs.SetString("BL_3_W_1", _data.Balo_Three.Weapon_One);
        PlayerPrefs.SetString("BL_3_W_2", _data.Balo_Three.Weapon_Two);
        PlayerPrefs.SetString("BL_3_G_Q", _data.Balo_Three.Grenade_Q);
        PlayerPrefs.SetString("BL_3_G_E", _data.Balo_Three.Grenade_E);

        RefreshButtonLabels();
    }

    private void RefreshButtonLabels()
    {
        SetButtonLabel(Character, $"CHARACTER: {_data.CharacterName}");

        SetButtonLabel(Balo1_weaapon1, GetWeaponDisplayName(_data.Balo_One.Weapon_One));
        SetButtonLabel(Balo1_weaapon2, GetWeaponDisplayName(_data.Balo_One.Weapon_Two));
        SetButtonLabel(Balo1_GrenadeQ, GetGrenadeDisplayName(_data.Balo_One.Grenade_Q));
        SetButtonLabel(Balo1_GrenadeE, GetGrenadeDisplayName(_data.Balo_One.Grenade_E));

        SetButtonLabel(Balo2_weaapon1, GetWeaponDisplayName(_data.Balo_Two.Weapon_One));
        SetButtonLabel(Balo2_weaapon2, GetWeaponDisplayName(_data.Balo_Two.Weapon_Two));
        SetButtonLabel(Balo2_GrenadeQ, GetGrenadeDisplayName(_data.Balo_Two.Grenade_Q));
        SetButtonLabel(Balo2_GrenadeE, GetGrenadeDisplayName(_data.Balo_Two.Grenade_E));

        SetButtonLabel(Balo3_weaapon1, GetWeaponDisplayName(_data.Balo_Three.Weapon_One));
        SetButtonLabel(Balo3_weaapon2, GetWeaponDisplayName(_data.Balo_Three.Weapon_Two));
        SetButtonLabel(Balo3_GrenadeQ, GetGrenadeDisplayName(_data.Balo_Three.Grenade_Q));
        SetButtonLabel(Balo3_GrenadeE, GetGrenadeDisplayName(_data.Balo_Three.Grenade_E));
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null) return;

        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;
    }

    // Weapon duoc luu theo ID (Data().ID), can do nguoc lai qua danh sach de lay Name hien thi
    private string GetWeaponDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id) || GameManager.Instance == null) return "";

        foreach (var weapon in GameManager.Instance.GetWeaponDataList())
        {
            if (weapon.Data().ID.Contains(id))
                return $"WEAPON: {weapon.Name()}";
        }
        return $"WEAPON: {id}";
    }

    // Grenade duoc luu theo ID, GetGrenade() da tra ve theo ID nen chi can lay .Name
    private string GetGrenadeDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id) || GameManager.Instance == null) return "";

        var grenade = GameManager.Instance.GetGrenade(id);
        return grenade != null ? $"GRENADE: {grenade.Name}" : $"GRENADE: {id}";
    }

    public void LoadPanelBalo1_weapon_1()
    {
        PanelItem.Load(true,false, false,ButtonType.BL1_W1);
    }
    
    public void LoadPanelBalo1_weapon_2()
    {
        PanelItem.Load(true,false, false,ButtonType.BL1_W2);
    }
    
    public void LoadPanelBalo1_Grenade_Q()
    {
        PanelItem.Load(false,true, false,ButtonType.BL1_Q);
    }
    
    public void LoadPanelBalo1_Grenade_E()
    {
        PanelItem.Load(false,true, false,ButtonType.BL1_E);
    }
    
    public void LoadPanelBalo2_weapon_1()
    {
        PanelItem.Load(true,false, false,ButtonType.BL2_W1);
    }
    
    public void LoadPanelBalo2_weapon_2()
    {
        PanelItem.Load(true,false, false,ButtonType.BL2_W2);
    }
    
    public void LoadPanelBalo2_Grenade_Q()
    {
        PanelItem.Load(false,true, false,ButtonType.BL2_Q);
    }
    
    public void LoadPanelBalo2_Grenade_E()
    {
        PanelItem.Load(false,true, false,ButtonType.BL2_E);
    }
    
    public void LoadPanelBalo3_weapon_1()
    {
        PanelItem.Load(true,false, false,ButtonType.BL3_W1);
    }
    
    public void LoadPanelBalo3_weapon_2()
    {
        PanelItem.Load(true,false, false,ButtonType.BL3_W2);
    }
    
    public void LoadPanelBalo3_Grenade_Q()
    {
        PanelItem.Load(false,true, false,ButtonType.BL3_Q);
    }
    
    public void LoadPanelBalo3_Grenade_E()
    {
        PanelItem.Load(false,true, false,ButtonType.BL3_E);
    }

    public void LoadPanelPlayer()
    {
        PanelItem.Load(false,false, true,ButtonType.CHARACTER);
    }

    private void OnDestroy()
    {
        // Balo 1
        if (Balo1_weaapon1 != null) Balo1_weaapon1.onClick.RemoveListener(LoadPanelBalo1_weapon_1);
        if (Balo1_weaapon2 != null) Balo1_weaapon2.onClick.RemoveListener(LoadPanelBalo1_weapon_2);
        if (Balo1_GrenadeQ != null) Balo1_GrenadeQ.onClick.RemoveListener(LoadPanelBalo1_Grenade_Q);
        if (Balo1_GrenadeE != null) Balo1_GrenadeE.onClick.RemoveListener(LoadPanelBalo1_Grenade_E);

        // Balo 2
        if (Balo2_weaapon1 != null) Balo2_weaapon1.onClick.RemoveListener(LoadPanelBalo2_weapon_1);
        if (Balo2_weaapon2 != null) Balo2_weaapon2.onClick.RemoveListener(LoadPanelBalo2_weapon_2);
        if (Balo2_GrenadeQ != null) Balo2_GrenadeQ.onClick.RemoveListener(LoadPanelBalo2_Grenade_Q);
        if (Balo2_GrenadeE != null) Balo2_GrenadeE.onClick.RemoveListener(LoadPanelBalo2_Grenade_E);

        // Balo 3
        if (Balo3_weaapon1 != null) Balo3_weaapon1.onClick.RemoveListener(LoadPanelBalo3_weapon_1);
        if (Balo3_weaapon2 != null) Balo3_weaapon2.onClick.RemoveListener(LoadPanelBalo3_weapon_2);
        if (Balo3_GrenadeQ != null) Balo3_GrenadeQ.onClick.RemoveListener(LoadPanelBalo3_Grenade_Q);
        if (Balo3_GrenadeE != null) Balo3_GrenadeE.onClick.RemoveListener(LoadPanelBalo3_Grenade_E);

        // Character
        if (Character != null) Character.onClick.RemoveListener(LoadPanelPlayer);
    }
}