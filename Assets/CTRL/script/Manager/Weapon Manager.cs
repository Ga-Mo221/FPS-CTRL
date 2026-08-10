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
    
    
    // Weapon One
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponOneRef { get; set; }
    public WeaponController _weapon_One_CTRL;
    
    // Weapon Two
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponTwoRef { get; set; }
    public WeaponController _weapon_Two_CTRL;
    
    // Active Weapon
    [Networked] public byte _weapon_Active { get; set; } = 0;
    
    [TabGroup("Weapon Manager", "Components")]
    [SerializeField] private CharComponentManager _componentManager;


    [TabGroup("Weapon Manager", "Camera")] 
    [SerializeField] private Camera _camera;
    
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow", width: 0.6f)]
    [SerializeField] private Transform _camera_Recoil;
    
#if UNITY_EDITOR
    [HorizontalGroup("Weapon Manager/Camera/RecoilRow")]
    [HideLabel]
    [SerializeField] private string _camera_Name = "Camera Recoil";
    [HorizontalGroup("Weapon Manager/Weapon/Parent")]
    [HideLabel]
    [SerializeField] private string _parent_name = "Weapon Parrent";
#endif
    
    private PlayerMotionController _anim;
    
    // ============================ PUBLIC FIELD ===========================================
    
    public Transform WeaponParent => _weapon_Parrent;
    public Transform Camera_Recoil => _camera_Recoil;
    public Camera Camera => _camera;
    public bool SkipWeaponSpawn { get; set; } = false;
    public bool NeedsResolveAfterMigration { get; set; } = false;

    private void Awake()
    {
        if (_componentManager == null) return;
        _anim = _componentManager.PlayerMotionController;
    }
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority && !SkipWeaponSpawn)
        {
            CreateWeapon(_weapon_Two_Prefab, out var w2);
            _weaponTwoRef = w2;
            CreateWeapon(_weapon_One_Prefab, out var w1);
            _weaponOneRef = w1;
        }
        if (_weapon_One_CTRL == null || _weapon_Two_CTRL == null)
            if (!NeedsResolveAfterMigration)
            {
                ResolveWeaponControllers();
                if (_weapon_One_CTRL != null) _weapon_One_CTRL.Respawn();
                if (_weapon_Two_CTRL != null) _weapon_Two_CTRL.Respawn();
            }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResolveAfterMigration(RpcInfo info = default)
    {
        ResolveWeaponControllers();
        SwitchWeapon();
        Debug.Log($"<color=green>[WeaponManager]</color> ResolveWeaponControllers after migration: {name}", this);
    }

    private void Start()
    {
        if (!NeedsResolveAfterMigration)
        {
            if (Object.HasStateAuthority)
                SwitchWeaponActive(0);
            SwitchWeapon();
        }
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

    public void SwitchWeapon()
    {
        if (_weapon_One_CTRL == null && _weaponOneRef != null || _weapon_Two_CTRL == null && _weaponTwoRef != null)
            ResolveWeaponControllers();
        
        if (_weapon_Active == 1)
            SwitchToWeaponOne();
        if (_weapon_Active == 2)
            SwitchToWeaponTwo();
    }

    public bool SwitchWeaponActive(int weaponId)
    {
        if (weaponId == 0)
        {
            if (_weaponOneRef != null && _weapon_Active != 1)
            {
                _weapon_Active = 1;
                return true;
            }

            if (_weaponTwoRef != null && _weapon_Active != 2)
            {
                _weapon_Active = 2;
                return true;
            }
        }else if (weaponId == 1)
        {
            if (_weaponOneRef != null && _weapon_Active != 1)
            {
                _weapon_Active = 1;
                return true;
            }
        }
        else
        {
            if (_weaponTwoRef != null && _weapon_Active != 2)
            {
                _weapon_Active = 2;
                return true;
            }
        }
        return false;
    }

    private void SwitchToWeaponOne()
    {
        if (_weaponOneRef != null)
        {
            _componentManager.ConstraintManager?.SwitchWeapon(_weapon_One_CTRL.Data.ID);
            _weaponOneRef?.gameObject.SetActive(true);
            _weaponTwoRef?.gameObject.SetActive(false);
            _anim?.WeaponIndex(_weapon_One_CTRL.Data.Index);
            Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon One", this);
        }
    }

    private void SwitchToWeaponTwo()
    {
        if (_weaponTwoRef != null)
        {
            _componentManager.ConstraintManager?.SwitchWeapon(_weapon_Two_CTRL.Data.ID);
            _weaponOneRef?.gameObject.SetActive(false);
            _weaponTwoRef?.gameObject.SetActive(true);
            _anim?.WeaponIndex(_weapon_Two_CTRL.Data.Index);
            Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon Two", this);
        }
    }
    
    public void SetWeaponOnePrefab(GameObject prefab) => _weapon_One_Prefab = prefab;
    public void SetWeaponTwoPrefab(GameObject prefab) => _weapon_Two_Prefab = prefab;

    public WeaponController Weapon_Active()
    {
        if (_weaponOneRef != null && _weapon_One_CTRL != null && _weapon_One_CTRL.gameObject.activeSelf)
            return _weapon_One_CTRL;
        if (_weaponTwoRef != null && _weapon_Two_CTRL != null && _weapon_Two_CTRL.gameObject.activeSelf)
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
