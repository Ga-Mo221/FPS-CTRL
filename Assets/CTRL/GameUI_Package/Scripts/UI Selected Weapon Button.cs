using Game.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISelectedWeaponButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _balo_name_text;
    
    [TabGroup("ONE")]
    [SerializeField] private Image _weapon_one_icon_image;
    [TabGroup("ONE")]
    [SerializeField] private TextMeshProUGUI _weapon_one_name_text;
    [TabGroup("ONE")]
    [SerializeField] private TextMeshProUGUI _Weapon_one_default_mg_text;
    [TabGroup("ONE")]
    [SerializeField] private TextMeshProUGUI _Weapon_one_default_max_mg_text;
    
    [TabGroup("TWO")]
    [SerializeField] private Image _weapon_two_icon_image;
    [TabGroup("TWO")]
    [SerializeField] private TextMeshProUGUI _weapon_two_name_text;
    [TabGroup("TWO")]
    [SerializeField] private TextMeshProUGUI _Weapon_two_default_mg_text;
    [TabGroup("TWO")]
    [SerializeField] private TextMeshProUGUI _Weapon_two_default_max_mg_text;

    private WeaponData _weaponDataOne;
    private WeaponData _weaponDataTwo;

    public void EquipWeapon()
    {
        var componentManager = GetComponentInParent<CharComponentManager>(true);
        var weaponManager = componentManager.WeaponManager;

        // Đảm bảo không bao giờ null, dùng string.Empty thay thế
        string weapon1name = _weaponDataOne?.WeaponName ?? string.Empty;
        string weapon2name = _weaponDataTwo?.WeaponName ?? string.Empty;
        
        if (weaponManager != null)
        {
            weaponManager.Rpc_RequestChangeWeapon(weapon1name, weapon2name);
        }
        
        var parent =  GetComponentInParent<SelectLoadoutController>(true);
        if (parent != null)
            parent.gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetOnuiLoadout(false);
            GameManager.Instance.setCurrentBalo(weapon1name, weapon2name);
        }
    }

    public void SetWeapon(WeaponData weaponOneData, WeaponData weaponTwoData)
    {
        _weaponDataOne = weaponOneData;
        _weaponDataTwo = weaponTwoData;
        
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
