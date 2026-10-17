using Game.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISelectedWeaponButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _balo_name_text;
    
    [TabGroup("WeaponButton", "Weapon")]
    [TabGroup("WeaponButton/Weapon/SubTab", "ONE")]
    [SerializeField] private Image _weapon_one_icon_image;
    [TabGroup("WeaponButton/Weapon/SubTab", "ONE")]
    [SerializeField] private TextMeshProUGUI _weapon_one_name_text;
    [TabGroup("WeaponButton/Weapon/SubTab", "ONE")]
    [SerializeField] private TextMeshProUGUI _Weapon_one_default_mg_text;
    [TabGroup("WeaponButton/Weapon/SubTab", "ONE")]
    [SerializeField] private TextMeshProUGUI _Weapon_one_default_max_mg_text;
    
    [TabGroup("WeaponButton/Weapon/SubTab", "TWO")]
    [SerializeField] private Image _weapon_two_icon_image;
    [TabGroup("WeaponButton/Weapon/SubTab", "TWO")]
    [SerializeField] private TextMeshProUGUI _weapon_two_name_text;
    [TabGroup("WeaponButton/Weapon/SubTab", "TWO")]
    [SerializeField] private TextMeshProUGUI _Weapon_two_default_mg_text;
    [TabGroup("WeaponButton/Weapon/SubTab", "TWO")]
    [SerializeField] private TextMeshProUGUI _Weapon_two_default_max_mg_text;
    
    [TabGroup("WeaponButton", "Grenade")]
    [TabGroup("WeaponButton/Grenade/SubTab", "Q")]
    [SerializeField] private Image _grenade_Q_image;
    [TabGroup("WeaponButton/Grenade/SubTab", "Q")]
    [SerializeField] private TextMeshProUGUI _grenade_Q_text;
    
    [TabGroup("WeaponButton/Grenade/SubTab", "E")]
    [SerializeField] private Image _grenade_E_image;
    [TabGroup("WeaponButton/Grenade/SubTab", "E")]
    [SerializeField] private TextMeshProUGUI _grenade_E_text;
    
    private WeaponData _weaponDataOne;
    private WeaponData _weaponDataTwo;
    private GrenadeHandler _grenade_Q;
    private GrenadeHandler _grenade_E;

    public void EquipWeapon()
    {
        var componentManager = GetComponentInParent<CharComponentManager>(true);
        var weaponManager = componentManager.WeaponManager;
        var nshootHandler = componentManager.NShootHandler;

        // Đảm bảo không bao giờ null, dùng string.Empty thay thế
        string weapon1ID = _weaponDataOne?.ID ?? string.Empty;
        string weapon2ID = _weaponDataTwo?.ID ?? string.Empty;
        // [Aemy 2026-10-01] null-guard grenade (balo co the thieu utility) -> tranh NRE.
        string gQ = _grenade_Q != null ? _grenade_Q.ID : string.Empty;
        string gE = _grenade_E != null ? _grenade_E.ID : string.Empty;
        
        if (weaponManager != null)
        {
            weaponManager.Rpc_RequestChangeWeapon(weapon1ID, weapon2ID);
        }
        
        var parent =  GetComponentInParent<SelectLoadoutController>(true);
        if (parent != null)
            parent.gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetOnuiLoadout(false);
            var data = new BaloData(weapon1ID, weapon2ID, gQ, gE);
            GameManager.Instance.setCurrentBalo(data);
        }
        
        if (nshootHandler != null)
            nshootHandler.RPC_SetGrenadeOnUI(gQ, gE);
    }

    /// <summary>[Aemy 2026-10-11] Ten balo (vd "BALO 1 · ALPHA").</summary>
    public void SetTitle(string title)
    {
        if (_balo_name_text != null && !string.IsNullOrEmpty(title)) _balo_name_text.text = title;
    }

    // [Aemy 2026-10-11] Loai sung nam o WeaponController tren prefab (WeaponData khong co) -> tra qua GameManager.
    private static bool IsMelee(WeaponData d)
    {
        if (d == null || GameManager.Instance == null) return false;
        var prefab = GameManager.Instance.GetWeapon(d.ID);
        var ctrl = prefab != null ? prefab.GetComponent<WeaponController>() : null;
        return ctrl != null && ctrl.WeaponType == WeaponType.Melee;
    }

    public void SetWeapon(WeaponData weaponOneData, WeaponData weaponTwoData, GrenadeHandler grenade_Q, GrenadeHandler grenade_E)
    {
        _weaponDataOne = weaponOneData;
        _weaponDataTwo = weaponTwoData;
        _grenade_Q = grenade_Q;
        _grenade_E = grenade_E;
        
        if (weaponOneData != null)
        {
            _weapon_one_icon_image.sprite = weaponOneData.Icon;
            Game.Skins.WeaponDisplayName.Apply(_weapon_one_name_text, weaponOneData); // [Aemy 2026-10-11] skin: ten goc + ten skin, 1 dong
            _Weapon_one_default_mg_text.text = weaponOneData.DefaultBullet.ToString();
            _Weapon_one_default_max_mg_text.text = $"/{weaponOneData.MaxBullet}";
            
            _weapon_one_icon_image.gameObject.SetActive(true);
            _weapon_one_name_text.gameObject.SetActive(true);
            bool ammo1 = !IsMelee(weaponOneData);   // [Aemy 2026-10-11] dao (Melee) khong dung dan -> an so dan
            _Weapon_one_default_mg_text.gameObject.SetActive(ammo1);
            _Weapon_one_default_max_mg_text.gameObject.SetActive(ammo1);
        }
        else
        {
            _weapon_one_icon_image.gameObject.SetActive(false);
            _weapon_one_name_text.gameObject.SetActive(false);
            _Weapon_one_default_mg_text.gameObject.SetActive(false);
            _Weapon_one_default_max_mg_text.gameObject.SetActive(false);
        }
        
        if (weaponTwoData != null)
        {
            _weapon_two_icon_image.sprite = weaponTwoData.Icon;
            Game.Skins.WeaponDisplayName.Apply(_weapon_two_name_text, weaponTwoData); // [Aemy 2026-10-11]
            _Weapon_two_default_mg_text.text = weaponTwoData.DefaultBullet.ToString();
            _Weapon_two_default_max_mg_text.text = $"/{weaponTwoData.MaxBullet}";
            
            _weapon_two_icon_image.gameObject.SetActive(true);
            _weapon_two_name_text.gameObject.SetActive(true);
            bool ammo2 = !IsMelee(weaponTwoData);
            _Weapon_two_default_mg_text.gameObject.SetActive(ammo2);
            _Weapon_two_default_max_mg_text.gameObject.SetActive(ammo2);
        }
        else
        {
            _weapon_two_icon_image.gameObject.SetActive(false);
            _weapon_two_name_text.gameObject.SetActive(false);
            _Weapon_two_default_mg_text.gameObject.SetActive(false);
            _Weapon_two_default_max_mg_text.gameObject.SetActive(false);
        }

        if (grenade_Q != null)
        {
            _grenade_Q_image.sprite = grenade_Q.Icon;
            _grenade_Q_text.text = grenade_Q.Name;
            
            _grenade_Q_image.gameObject.SetActive(true);
            _grenade_Q_text.gameObject.SetActive(true);
        }
        else
        {
            _grenade_Q_image.gameObject.SetActive(false);
            _grenade_Q_text.gameObject.SetActive(false);
        }

        if (grenade_E != null)
        {
            _grenade_E_image.sprite = grenade_E.Icon;
            _grenade_E_text.text = grenade_E.Name;
            
            _grenade_E_image.gameObject.SetActive(true);
            _grenade_E_text.gameObject.SetActive(true);
        }
        else
        {
            _grenade_E_image.gameObject.SetActive(false);
            _grenade_E_text.gameObject.SetActive(false);
        }
    }
}
