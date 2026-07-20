using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject _weapon_One_Prefab;
    [SerializeField] private GameObject _weapon_Two_Prefab;
    
    // Weapon One
    private GameObject _weapon_One;
    private WeaponController _weapon_One_CTRL;
    
    // Weapon Two
    private GameObject _weapon_Two;
    private WeaponController _weapon_Two_CTRL;
    
    // Active Weapon
    private bool _weapon_One_Active;
    
    [Header("Constraint Manager")]
    [SerializeField] private ConstraintManager _constraintManager;

    [Header("Weapon Parrent")]
    [SerializeField] private Transform _weapon_Parrent;

    [Header("Camera")] 
    [SerializeField] private Transform _camera_Recoil;
    [SerializeField] private Camera _camera;
    
    [Header("Animator")]
    [SerializeField] private Animator _anim;
    [SerializeField] private CharacterHandler _character_CTRL;

    private void Start()
    {
        CreateWeapon(_weapon_One_Prefab, out _weapon_One, out _weapon_One_CTRL);
        CreateWeapon(_weapon_Two_Prefab, out _weapon_Two, out _weapon_Two_CTRL);
        SwitchWeapon();
    }

    private void CreateWeapon(GameObject prefab, out GameObject instan, out WeaponController ctrl)
    {
        instan = null;
        ctrl = null;
        if (prefab == null) return;
        instan = Instantiate(prefab, _weapon_Parrent);
        ctrl = instan.GetComponent<WeaponController>();
        ctrl.SetCameraCTRL(_camera_Recoil);
        ctrl.Camera_Zoom = _camera;
        ctrl.Manager = this;
        _constraintManager.AddNewWeapon(ctrl);
        instan.SetActive(false);
    }

    public bool SwitchWeapon()
    {
        if (!_weapon_One_Active && _weapon_One != null)
        {
            _weapon_One_Active = true;
            _constraintManager?.SwitchWeapon(_weapon_One_CTRL.Data.ID);
            _weapon_One?.SetActive(true);
            _weapon_Two?.SetActive(false);
            _anim?.SetInteger("WeaponIndex", _weapon_One_CTRL.Data.Index);
            if (_character_CTRL != null)
            {
                _character_CTRL.IsSwitch = false;
            }
            Debug.Log("Switch to Weapon One");
            return true;
        }
        
        if (_weapon_Two == null) return false;
        _weapon_One_Active = false;
        _constraintManager?.SwitchWeapon(_weapon_Two_CTRL.Data.ID);
        _weapon_One?.SetActive(false);
        _weapon_Two?.SetActive(true);
        _anim?.SetInteger("WeaponIndex", _weapon_Two_CTRL.Data.Index);
        if (_character_CTRL != null)
        {
            _character_CTRL.IsSwitch = false;
        }
        Debug.Log("Switch to Weapon Two");
        return true;
    }
    
    public void SetWeaponOnePrefab(GameObject prefab) => _weapon_One_Prefab = prefab;
    public void SetWeaponTwoPrefab(GameObject prefab) => _weapon_Two_Prefab = prefab;

    public WeaponController Anim_Active()
    {
        if (_weapon_One_Active && _weapon_One != null && _weapon_One_CTRL != null)
            return _weapon_One_CTRL;
        if (!_weapon_One_Active && _weapon_Two != null && _weapon_Two_CTRL != null)
            return _weapon_Two_CTRL;
        return null;
    }
}
