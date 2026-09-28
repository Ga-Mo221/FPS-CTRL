using Sirenix.OdinInspector;
using TMPro;
using GaMo.TrueInputSystem;
using UnityEngine;
using UnityEngine.UI;

public class UIWeaponController : MonoBehaviour
{
    [TabGroup("UI", "weapon")]
    [SerializeField] private TextMeshProUGUI _weapon_name_Text;
    [TabGroup("UI", "weapon")]
    [SerializeField] private TextMeshProUGUI _magAmmo_Text;
    [TabGroup("UI", "weapon")]
    [SerializeField] private TextMeshProUGUI _magMaxAmmo_Text;
    [TabGroup("UI", "weapon")]
    [SerializeField] private TextMeshProUGUI _reloadAmmoKey;
    [TabGroup("UI", "weapon")]
    [SerializeField] private TextMeshProUGUI _dragItemKey;

    [TabGroup("UI", "weapon")]
    [SerializeField] private Image _weaponIcon;
    
    [TabGroup("UI", "Grenade")]
    [SerializeField] private TextMeshProUGUI _fragKey;
    [TabGroup("UI", "Grenade")]
    [SerializeField] private TextMeshProUGUI _flashKey;
    
    [TabGroup("UI", "Switch Mode")]
    [SerializeField] private TextMeshProUGUI _switchmodeKey;
    [TabGroup("UI", "Switch Mode")] 
    [SerializeField] private Image _weaponMode;
    [TabGroup("UI", "Switch Mode")]
    [SerializeField] private Sprite _sigleModeSprite;
    [TabGroup("UI", "Switch Mode")] 
    [SerializeField] private Sprite _autoModeSprite;
    [TabGroup("UI", "Switch Mode")] 
    [SerializeField] private Sprite _burstModeSprite;

    private void Start()
    {
        DisplayPBindingKeyMap();
    }

    public void DisplayPBindingKeyMap()
    {
        var settingManager = SettingManager.Instance;
        var tis = TISInputBridge.Instance;

        if (settingManager != null && tis != null)
        {
            if (settingManager.SettingData.Platform == AimAssistPlatform.PC)
            {
                string path_Frag = TISRebindingSystem.GetActivePath(tis, "Q", RebindTarget.PC);
                _fragKey.text = TISRebindingSystem.GetDisplayName(path_Frag);
        
                string path_Flash = TISRebindingSystem.GetActivePath(tis, "E", RebindTarget.PC);
                _flashKey.text = TISRebindingSystem.GetDisplayName(path_Flash);
                
                string SwitchModePath = TISRebindingSystem.GetActivePath(tis, "Mode", RebindTarget.PC);
                _switchmodeKey.text = TISRebindingSystem.GetDisplayName(SwitchModePath);
                
                string ReloadPath = TISRebindingSystem.GetActivePath(tis, "Reload", RebindTarget.PC);
                _reloadAmmoKey.text = TISRebindingSystem.GetDisplayName(ReloadPath);
                
                string DragItemPath = TISRebindingSystem.GetActivePath(tis, "Drag", RebindTarget.PC);
                _dragItemKey.text = TISRebindingSystem.GetShortDisplayName(DragItemPath);
            }
            else if (settingManager.SettingData.Platform == AimAssistPlatform.Gamepad)
            {
                string path_Frag = TISRebindingSystem.GetActivePath(tis, "Q", RebindTarget.Gamepad);
                _fragKey.text = TISRebindingSystem.GetShortDisplayName(path_Frag);
        
                string path_Flash = TISRebindingSystem.GetActivePath(tis, "E", RebindTarget.Gamepad);
                _flashKey.text = TISRebindingSystem.GetShortDisplayName(path_Flash);
                
                string SwitchModePath = TISRebindingSystem.GetActivePath(tis, "Mode", RebindTarget.Gamepad);
                _switchmodeKey.text = TISRebindingSystem.GetShortDisplayName(SwitchModePath);
                
                string ReloadPath = TISRebindingSystem.GetActivePath(tis, "Reload", RebindTarget.Gamepad);
                _reloadAmmoKey.text = TISRebindingSystem.GetShortDisplayName(ReloadPath);
                
                string DragItemPath = TISRebindingSystem.GetActivePath(tis, "Drag", RebindTarget.Gamepad);
                _dragItemKey.text = TISRebindingSystem.GetShortDisplayName(DragItemPath);
            }
        }
    }
    
    public void SetBullet(int currentBullet, int maxBullet)
    {
        _magAmmo_Text.text = $"{currentBullet}";
        _magMaxAmmo_Text.text = $"/ {maxBullet}";
    }

    public void SetWeaponActive(WeaponController ctrl)
    {
        if (ctrl == null) return;
        
        if (ctrl.Object == null || !ctrl.Object.IsValid)
        {
            Debug.LogWarning("[UIWeaponController] SetWeaponActive called before WeaponController was Spawned — skipping.");
            return;
        }
        
        _weaponIcon.sprite = ctrl.Data.Icon;
        _weapon_name_Text.text = ctrl.Data.WeaponName;
        SetBullet(ctrl.CurrentBulletvalue, ctrl.CurrentMaxBullet);
        SetWeaponMode(ctrl.GetMode());
        
        if (ctrl.WeaponType != WeaponType.Melee)
        {
            _magAmmo_Text.gameObject.SetActive(true);
            _magMaxAmmo_Text.gameObject.SetActive(true);
            _weaponMode.transform.parent.gameObject.SetActive(true);
            _reloadAmmoKey.transform.parent.parent.gameObject.SetActive(true);
        }
        else
        {
            _magAmmo_Text.gameObject.SetActive(false);
            _magMaxAmmo_Text.gameObject.SetActive(false);
            _weaponMode.transform.parent.gameObject.SetActive(false);
            _reloadAmmoKey.transform.parent.parent.gameObject.SetActive(false);
        }
    }

    public void SetWeaponMode(GunRecoilController.FiringMode mode)
    {
        switch (mode)
        {
            case GunRecoilController.FiringMode.Auto:
                _weaponMode.gameObject.SetActive(true);
                _weaponMode.sprite = _autoModeSprite;
                break;
            case GunRecoilController.FiringMode.Semi:
                _weaponMode.gameObject.SetActive(true);
                _weaponMode.sprite = _sigleModeSprite;
                break;
            case GunRecoilController.FiringMode.Burst:
                _weaponMode.gameObject.SetActive(true);
                _weaponMode.sprite = _burstModeSprite;
                break;
            case GunRecoilController.FiringMode.Melee:
                _weaponMode.gameObject.SetActive(false);
                break;
        }
    }
}
