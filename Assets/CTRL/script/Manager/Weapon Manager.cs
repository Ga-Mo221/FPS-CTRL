using System;
using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponManager : NetworkBehaviour
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
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    private NetworkObject _weaponOneRef { get; set; }
    private WeaponController _weapon_One_CTRL;
    
    // Weapon Two
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    private NetworkObject _weaponTwoRef { get; set; }
    private WeaponController _weapon_Two_CTRL;
    
    // Active Weapon
    private bool _weapon_One_Active { get; set; } 
    
    [TabGroup("Weapon Manager", "Components")]
    [SerializeField] private CharComponentManager _componentManager;


    [TabGroup("Weapon Manager", "Camera")] 
    [SerializeField] private Camera _camera;
    
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow", width: 0.6f)]
    [SerializeField] private Transform _camera_Recoil;
    
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow")]
    [HideLabel]
    [SerializeField] private string _camera_Name = "Camera Recoil";
    
    private PlayerMotionController _anim;
    
    // ============================ PUBLIC FIELD ===========================================
    
    public Transform WeaponParent => _weapon_Parrent;
    public Transform Camera_Recoil => _camera_Recoil;
    public Camera Camera => _camera;

    private void Awake()
    {
        if (_componentManager == null) return;
        _anim = _componentManager.PlayerMotionController;
    }
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CreateWeapon(_weapon_Two_Prefab, out var w2);
            _weaponTwoRef = w2;
            CreateWeapon(_weapon_One_Prefab, out var w1);
            _weaponOneRef = w1;
        }
        if (_weapon_One_CTRL == null || _weapon_Two_CTRL == null)
            ResolveWeaponControllers();
    }

    private void Start()
    {
        SwitchWeapon();
    }

    private void CreateWeapon(GameObject prefab, out NetworkObject instan)
    {
        instan = null;
        if (prefab == null) return;
        
        instan = Runner.Spawn(
            prefab,
            position: _weapon_Parrent.position,
            rotation: _weapon_Parrent.rotation,
            inputAuthority: Object.InputAuthority,
            onBeforeSpawned: (runner, obj) =>
            {
                // Chạy TRƯỚC khi object replicate ra network — set owner ở đây
                // để đảm bảo mọi client nhận được OwnerCharacter cùng lúc obj spawn
                obj.GetComponent<WeaponController>().OwnerCharacter = Object;
            });
    }

    public bool SwitchWeapon()
    {
        if (!_weapon_One_Active && _weaponOneRef != null)
        {
            _weapon_One_Active = true;
            _componentManager.ConstraintManager?.SwitchWeapon(_weapon_One_CTRL.Data.ID);
            _weaponOneRef?.gameObject.SetActive(true);
            _weaponTwoRef?.gameObject.SetActive(false);
            _anim?.WeaponIndex(_weapon_One_CTRL.Data.Index);
            Debug.Log("Switch to Weapon One", this);
            return true;
        }
        
        if (_weaponTwoRef == null) return false;
        _weapon_One_Active = false;
        _componentManager.ConstraintManager?.SwitchWeapon(_weapon_Two_CTRL.Data.ID);
        _weaponOneRef?.gameObject.SetActive(false);
        _weaponTwoRef?.gameObject.SetActive(true);
        _anim?.WeaponIndex(_weapon_Two_CTRL.Data.Index);
        Debug.Log("Switch to Weapon Two", this);
        return true;
    }
    
    public void SetWeaponOnePrefab(GameObject prefab) => _weapon_One_Prefab = prefab;
    public void SetWeaponTwoPrefab(GameObject prefab) => _weapon_Two_Prefab = prefab;

    public WeaponController Weapon_Active()
    {
        if (_weapon_One_Active && _weaponOneRef != null && _weapon_One_CTRL != null)
            return _weapon_One_CTRL;
        if (!_weapon_One_Active && _weaponTwoRef != null && _weapon_Two_CTRL != null)
            return _weapon_Two_CTRL;
        Debug.Log("<color=red>[WeaponManager]</color> WeaponController not active.", this);
        return null;
    }

    public void Respawn()
    {
        if (_weapon_One_CTRL != null)
            _weapon_One_CTRL.Respawn();
        if (_weapon_Two_CTRL != null)
            _weapon_Two_CTRL.Respawn();
    }

    void OnWeaponRefsChanged()
    {
        if (_weaponOneRef != null)
            _weapon_One_CTRL = _weaponOneRef.GetComponent<WeaponController>();
        if (_weaponTwoRef != null)
            _weapon_Two_CTRL = _weaponTwoRef.GetComponent<WeaponController>();
    }
    
    private void ResolveWeaponControllers()
    {
        if (_weaponOneRef != null && _weapon_One_CTRL == null)
        {
            _weapon_One_CTRL = _weaponOneRef.GetComponent<WeaponController>();
            _componentManager.ConstraintManager.AddNewWeapon(_weapon_One_CTRL);
        }
        if (_weaponTwoRef != null && _weapon_Two_CTRL == null)
        {
            _weapon_Two_CTRL = _weaponTwoRef.GetComponent<WeaponController>();
            _componentManager.ConstraintManager.AddNewWeapon(_weapon_Two_CTRL);
        }
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
