using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

public class NWeaponManager : NetworkBehaviour
{
    // ───────────────────── Inspector ─────────────────────

    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_One_Prefab;
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_Two_Prefab;

    [HorizontalGroup("Weapon Manager/Weapon/Parent", width: 0.6f)]
    [SerializeField] private Transform _weapon_Parent;
    [HorizontalGroup("Weapon Manager/Weapon/Parent")]
    [HideLabel]
    [SerializeField] private string _parent_Name = "Weapon Parrent";

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

    // ───────────────────── Runtime refs (local, không networked) ─────────────────────

    private GameObject _weapon_One;
    private WeaponController _weapon_One_CTRL;

    private GameObject _weapon_Two;
    private WeaponController _weapon_Two_CTRL;

    private PlayerMotionController _anim;

    // ───────────────────── Networked State ─────────────────────

    // NetworkId của từng weapon để client có thể tìm lại object sau khi Fusion replicate.
    [Networked] private NetworkId _weapon_One_NetId { get; set; }
    [Networked] private NetworkId _weapon_Two_NetId { get; set; }

    // Weapon nào đang active — replicate để client biết.
    [Networked] private NetworkBool _weapon_One_Active { get; set; }

    private ChangeDetector _changeDetector;

    // ───────────────────── Fusion Lifecycle ─────────────────────

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (_componentManager == null)
            _componentManager = GetComponentInParent<CharComponentManager>(true);
        _anim = _componentManager?.PlayerMotionController;

        if (Object.HasStateAuthority)
        {
            // HOST: spawn weapon, lưu NetworkId vào networked state để client đọc được.
            SpawnWeapon(_weapon_One_Prefab, out _weapon_One, out _weapon_One_CTRL);
            SpawnWeapon(_weapon_Two_Prefab, out _weapon_Two, out _weapon_Two_CTRL);

            if (_weapon_One != null)
                _weapon_One_NetId = _weapon_One.GetComponent<NetworkObject>().Id;
            if (_weapon_Two != null)
                _weapon_Two_NetId = _weapon_Two.GetComponent<NetworkObject>().Id;

            // Reset trước khi switch để logic toggle chạy đúng.
            _weapon_One_Active = false;
            SwitchWeapon();
        }
        else
        {
            // CLIENT: weapon đã được Fusion replicate sang scene này (Runner.Spawn từ host).
            // Chỉ cần lookup bằng NetworkId và setup local refs, KHÔNG spawn lại.
            ResolveWeaponRefs();

            // Apply ngay state hiện tại (không chờ DetectChanges vì không có "thay đổi" mới
            // đối với client vừa join — state đã là final từ host).
            ApplyWeaponVisual();
        }
    }

    /// <summary>
    /// Chạy mỗi frame trên MỌI peer — dùng để cập nhật visual khi _weapon_One_Active thay đổi
    /// SAU khi đã spawn (ví dụ player switch weapon).
    /// </summary>
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(_weapon_One_Active):
                    ApplyWeaponVisual();
                    break;

                // Client join muộn: NetId được set bởi host SAU khi client's Spawned() đã chạy
                // → cần resolve lại refs khi NetId replicate về.
                case nameof(_weapon_One_NetId):
                case nameof(_weapon_Two_NetId):
                    if (!Object.HasStateAuthority)
                    {
                        ResolveWeaponRefs();
                        ApplyWeaponVisual();
                    }
                    break;
            }
        }
    }

    // ───────────────────── Spawn (Host only) ─────────────────────

    private void SpawnWeapon(GameObject prefab, out GameObject instance, out WeaponController ctrl)
    {
        instance = null;
        ctrl = null;
        if (prefab == null) return;

        var networkObj = Runner.Spawn(
            prefab,
            _weapon_Parent.position,
            _weapon_Parent.rotation,
            Object.InputAuthority
        );
        instance = networkObj.gameObject;
        
        instance.transform.SetParent(_weapon_Parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        ctrl = instance.GetComponent<WeaponController>();
        if (ctrl == null)
        {
            Debug.LogError($"[NWeaponManager] {prefab.name} thiếu WeaponController!");
            return;
        }
        SetupWeaponCtrl(ctrl);
        instance.gameObject.SetActive(false);
    }

    // ───────────────────── Resolve Refs (Client) ─────────────────────

    /// <summary>
    /// Client lookup weapon object từ NetworkId đã được replicate từ host.
    /// Gọi trong Spawned() và khi NetId thay đổi (replicate về muộn).
    /// </summary>
    private void ResolveWeaponRefs()
    {
        if (_weapon_One == null && _weapon_One_NetId != default)
        {
            if (Runner.TryFindObject(_weapon_One_NetId, out var no))
            {
                _weapon_One = no.gameObject;
                _weapon_One_CTRL = _weapon_One.GetComponent<WeaponController>();
                if (_weapon_One_CTRL != null) SetupWeaponCtrl(_weapon_One_CTRL);
                _weapon_One.SetActive(false);
            }
        }

        if (_weapon_Two == null && _weapon_Two_NetId != default)
        {
            if (Runner.TryFindObject(_weapon_Two_NetId, out var no))
            {
                _weapon_Two = no.gameObject;
                _weapon_Two_CTRL = _weapon_Two.GetComponent<WeaponController>();
                if (_weapon_Two_CTRL != null) SetupWeaponCtrl(_weapon_Two_CTRL);
                _weapon_Two.SetActive(false);
            }
        }
    }

    private void SetupWeaponCtrl(WeaponController ctrl)
    {
        ctrl.SetCameraCTRL(_camera_Recoil);
        ctrl.Camera_Zoom = _camera;
        ctrl.Manager = this;
        _componentManager?.ConstraintManager?.AddNewWeapon(ctrl);
    }

    // ───────────────────── Switch Weapon ─────────────────────

    public bool SwitchWeapon()
    {
        if (!Object.HasStateAuthority) return false;

        if (!_weapon_One_Active && _weapon_One != null)
        {
            _weapon_One_Active = true;
            _componentManager?.ConstraintManager?.SwitchWeapon(_weapon_One_CTRL.Data.ID);
            ApplyWeaponVisual();
            Debug.Log("[NWeaponManager] Switch to Weapon One");
            return true;
        }

        if (_weapon_Two == null) return false;

        _weapon_One_Active = false;
        _componentManager?.ConstraintManager?.SwitchWeapon(_weapon_Two_CTRL.Data.ID);
        ApplyWeaponVisual();
        Debug.Log("[NWeaponManager] Switch to Weapon Two");
        return true;
    }

    private void ApplyWeaponVisual()
    {
        bool oneActive = _weapon_One_Active;

        _weapon_One?.SetActive(oneActive);
        _weapon_Two?.SetActive(!oneActive);

        if (oneActive && _weapon_One_CTRL != null)
            _anim?.WeaponIndex(_weapon_One_CTRL.Data.Index);
        else if (!oneActive && _weapon_Two_CTRL != null)
            _anim?.WeaponIndex(_weapon_Two_CTRL.Data.Index);
    }

    // ───────────────────── Public API ─────────────────────

    public WeaponController Weapon_Active()
    {
        if (_weapon_One_Active && _weapon_One_CTRL != null) return _weapon_One_CTRL;
        if (!_weapon_One_Active && _weapon_Two_CTRL != null) return _weapon_Two_CTRL;
        Debug.LogWarning("<color=red>[NWeaponManager]</color> Không có WeaponController nào active.", this);
        return null;
    }

    public void SetWeaponOnePrefab(GameObject prefab) => _weapon_One_Prefab = prefab;
    public void SetWeaponTwoPrefab(GameObject prefab) => _weapon_Two_Prefab = prefab;

    // ───────────────────── Editor ─────────────────────

#if UNITY_EDITOR
    [Button]
    public void GetSetup()
    {
        foreach (var trans in GetComponentsInChildren<Transform>(true))
        {
            if (trans.name.Contains(_parent_Name))
            {
                _weapon_Parent = trans;
                Debug.Log($"Da tim thay {_parent_Name}");
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