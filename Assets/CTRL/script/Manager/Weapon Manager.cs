using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_One_Prefab;
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_Two_Prefab;
    
    [HorizontalGroup("Weapon Manager/Weapon/Parent", width: 0.6f)]
    [SerializeField] private Transform _weapon_Parrent;
    
    [HorizontalGroup("Weapon Manager/Weapon/Parent")]
    [HideLabel]
    [SerializeField] private string _parent_name = "Weapon Parrent";
    
    
    // Weapon One
    [SerializeField] private GameObject _weapon_One;
    [SerializeField] private WeaponController _weapon_One_CTRL;
    
    // Weapon Two
    [SerializeField] private GameObject _weapon_Two;
    [SerializeField] private WeaponController _weapon_Two_CTRL;
    
    // Active Weapon
    [SerializeField] private bool _weapon_One_Active;
    
    [TabGroup("Weapon Manager", "Components")]
    [SerializeField] private CharComponentManager _componentManager;


    [TabGroup("Weapon Manager", "Camera")] 
    [SerializeField] private Camera _camera;
    public Camera Camera => _camera;
    
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow", width: 0.6f)]
    [SerializeField] private Transform _camera_Recoil;
    
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow")]
    [HideLabel]
    [SerializeField] private string _camera_Name = "Camera Recoil";
    
    private PlayerMotionController _anim;
    [TabGroup("Weapon Manager", "Components")]
    //[SerializeField] private CharacterHandler _character_CTRL;

    private void Awake()
    {
        if (_componentManager == null) return;
        _anim = _componentManager.PlayerMotionController;
    }

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
        _componentManager.ConstraintManager.AddNewWeapon(ctrl);
        instan.SetActive(false);
    }

    public bool SwitchWeapon()
    {
        if (!_weapon_One_Active && _weapon_One != null)
        {
            _weapon_One_Active = true;
            _componentManager.ConstraintManager?.SwitchWeapon(_weapon_One_CTRL.Data.ID);
            _weapon_One?.SetActive(true);
            _weapon_Two?.SetActive(false);
            _anim?.WeaponIndex(_weapon_One_CTRL.Data.Index);
            Debug.Log("Switch to Weapon One");
            return true;
        }
        
        if (_weapon_Two == null) return false;
        _weapon_One_Active = false;
        _componentManager.ConstraintManager?.SwitchWeapon(_weapon_Two_CTRL.Data.ID);
        _weapon_One?.SetActive(false);
        _weapon_Two?.SetActive(true);
        _anim?.WeaponIndex(_weapon_Two_CTRL.Data.Index);
        Debug.Log("Switch to Weapon Two");
        return true;
    }
    
    public void SetWeaponOnePrefab(GameObject prefab) => _weapon_One_Prefab = prefab;
    public void SetWeaponTwoPrefab(GameObject prefab) => _weapon_Two_Prefab = prefab;

    public WeaponController Weapon_Active()
    {
        if (_weapon_One_Active && _weapon_One != null && _weapon_One_CTRL != null)
            return _weapon_One_CTRL;
        if (!_weapon_One_Active && _weapon_Two != null && _weapon_Two_CTRL != null)
            return _weapon_Two_CTRL;
        Debug.Log("<color=red>[WeaponManager]</color> WeaponController not active.", this);
        return null;
    }
    
    
#if UNITY_EDITOR
    [Button]
    public void GetSetup()
    {
        foreach (var trans in GetComponentsInChildren<Transform>(true))
        {
            if (trans.name.Contains(_parent_name))
            {
                _weapon_Parrent = trans;
                Debug.Log($"Da tim thay {_parent_name}");
                continue;
            }

            if (trans.name.Contains(_camera_Name))
            {
                _camera_Recoil = trans;
                Debug.Log($"Da tim thay {_camera_Name}");
            }
        }
        
        _componentManager = GetComponentInChildren<CharComponentManager>(true);
        if (_componentManager != null) Debug.Log($"Da tim thay {_componentManager.name}");
        _camera = GetComponentInChildren<Camera>(true);
        if (_camera != null) Debug.Log($"Da tim thay {_camera.name}");
    }
#endif
}
