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
    
    [TabGroup("weaponButton", "Grenade")]
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
            var data = new BaloData(weapon1ID, weapon2ID, _grenade_Q.ID, _grenade_E.ID);
            GameManager.Instance.setCurrentBalo(data);
        }
        
        nshootHandler.SetGrenade(_grenade_Q.ID, _grenade_E.ID);
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
            _weapon_one_name_text.text = weaponOneData.WeaponName;
            _Weapon_one_default_mg_text.text = weaponOneData.DefaultBullet.ToString();
            _Weapon_one_default_max_mg_text.text = $"/{weaponOneData.MaxBullet}";
            
            _weapon_one_icon_image.gameObject.SetActive(true);
            _weapon_one_name_text.gameObject.SetActive(true);
            _Weapon_one_default_mg_text.gameObject.SetActive(true);
            _Weapon_one_default_max_mg_text.gameObject.SetActive(true);
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
            _weapon_two_name_text.text = weaponTwoData.WeaponName;
            _Weapon_two_default_mg_text.text = weaponTwoData.DefaultBullet.ToString();
            _Weapon_two_default_max_mg_text.text = $"/{weaponTwoData.MaxBullet}";
            
            _weapon_two_icon_image.gameObject.SetActive(true);
            _weapon_two_name_text.gameObject.SetActive(true);
            _Weapon_two_default_mg_text.gameObject.SetActive(true);
            _Weapon_two_default_max_mg_text.gameObject.SetActive(true);
        }
        else
        {
            _weapon_two_icon_image.gameObject.SetActive(false);
            _weapon_two_name_text.gameObject.SetActive(false);
            _Weapon_two_default_mg_text.gameObject.SetActive(false);
            _Weapon_two_default_max_mg_text.gameObject.SetActive(false);
        }
    }
}
