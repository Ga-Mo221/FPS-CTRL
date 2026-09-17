using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;
using WebSocketSharp;
using System.Threading.Tasks;

public class WeaponManager : NetworkBehaviour
{
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_One_Prefab;
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private GameObject _weapon_Two_Prefab;
    
    [TabGroup("Weapon Manager", "Weapon")]
    [SerializeField] private LayerMask _groundLayer;
    
    [HorizontalGroup("Weapon Manager/Weapon/Parent", width: 0.6f)]
    [SerializeField] private Transform _weapon_Parrent;
    
    
    // Weapon One
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponOneRef { get; set; }
    [Networked] public NetworkString<_16> _weaponOneID { get; set; }
    public WeaponController Weapon_One_CTRL;
    
    // Weapon Two
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponTwoRef { get; set; }
    [Networked] public NetworkString<_16> _weaponTwoID { get; set; }
    public WeaponController Weapon_Two_CTRL;
    
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
        InitWeapon();
    }

    #region Get Prefab weapon Local

    /// <summary>
    /// lấy prefab từ library theo tên và vị trí cần thay thế
    /// </summary>
    /// <param name="weaponOne"></param>
    /// <param name="WeaponTwo"></param>
    /// <param name="index"> == 0 là đổi balo, == 1 là thay thế vị trí 1. == 2 là thay thế vị trí 2</param>
    public void SpawnWeapon(string weaponOne, string WeaponTwo)
    {
        SpawnWeaponOne(weaponOne);
        SpawnWeaponTwo(WeaponTwo);
    }

    private void SpawnWeapon(string weaponName)
    {
        if (_weapon_Active == 1)
            SpawnWeaponOne(weaponName);
        else if (_weapon_Active == 2)
            SpawnWeaponTwo(weaponName);
    }

    private void SpawnWeaponOne(string weaponOne)
    {
        if (Weapon_One_CTRL != null)
        {
            Runner.Despawn(_weaponOneRef);
            Weapon_One_CTRL = null;
            _weaponOneRef = null;
        }
        
        _weapon_One_Prefab = GameManager.Instance.GetWeapon(weaponOne);
    }

    private void SpawnWeaponTwo(string weaponTwo)
    {
        if (Weapon_Two_CTRL != null)
        {
            Runner.Despawn(_weaponTwoRef);
            Weapon_Two_CTRL = null;
            _weaponTwoRef = null;
        }
        
        _weapon_Two_Prefab = GameManager.Instance.GetWeapon(weaponTwo);
    }
    
    #endregion

    #region Khởi tạo Weapon Local
    
    private void InitWeapon()
    {
        if (Object.HasStateAuthority && !SkipWeaponSpawn)
        {
            CreateWeapon(_weapon_Two_Prefab, out var w2);
            _weaponTwoRef = w2;
            CreateWeapon(_weapon_One_Prefab, out var w1);
            _weaponOneRef = w1;
        }
        if (Weapon_One_CTRL == null && _weapon_One_Prefab != null || 
            Weapon_Two_CTRL == null && _weapon_Two_Prefab != null)
            if (!NeedsResolveAfterMigration)
            {
                ResolveWeaponControllers();
                if (Weapon_One_CTRL != null) Weapon_One_CTRL.Respawn();
                if (Weapon_Two_CTRL != null) Weapon_Two_CTRL.Respawn();
            }
    }

    private void EquipNewWeapon(int index = 0)
    {
        if (index == 0)
        {
            EquipWeaponTwo();
            EquipWeaponOne();
        }
        else if (index == 1)
            EquipWeaponOne();
        else if (index == 2)
            EquipWeaponTwo();
    }

    private void EquipWeaponOne()
    {
        if (Object.HasStateAuthority)
        {
            CreateWeapon(_weapon_One_Prefab, out var w1);
            _weaponOneRef = w1;
        }
    }

    private void EquipWeaponTwo()
    {
        if (Object.HasStateAuthority)
        {
            CreateWeapon(_weapon_Two_Prefab, out var w2);
            _weaponTwoRef = w2;
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
    
    #endregion

    #region RPC
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_ResolveAfterMigration(RpcInfo info = default)
    {
        ResolveWeaponControllers();
        SwitchWeapon();
        Debug.Log($"<color=green>[WeaponManager]</color> ResolveWeaponControllers after migration: {name}", this);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SwitchWeapon(RpcInfo info = default)
    {
        SwitchWeapon();
    }

    /// <summary>
    /// duoc goi tu UI cua clients
    /// </summary>
    /// <param name="weaponOneName"></param>
    /// <param name="weaponTwoName"></param>
    /// <param name="info"></param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestChangeWeapon(string weaponOneName, string weaponTwoName, RpcInfo info = default)
    {
        SpawnWeapon(weaponOneName, weaponTwoName);
        EquipNewWeapon();
        SwitchWeaponActive(1);
    }
    
    /// <summary>
    /// duoc goi tu input local cua clients khi nhat mot vu khi
    /// </summary>
    /// <param name="weaponName"></param>
    /// <param name="currentBullet"></param>
    /// <param name="maxBullet"></param>
    /// <param name="info"></param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public async void Rpc_RequestChangeWeapon(string weaponName, int currentBullet, int maxBullet, RpcInfo info = default)
    {
        SpawnWeapon(weaponName);
        EquipNewWeapon(_weapon_Active);

        if (_weapon_Active == 1)
        {
            await WaitUntilReady(() => Weapon_One_CTRL != null && Weapon_One_CTRL.Object != null && Weapon_One_CTRL.Object.IsValid);
            Weapon_One_CTRL.SetBullet(currentBullet, maxBullet);
        }
        else if (_weapon_Active == 2)
        {
            await WaitUntilReady(() => Weapon_Two_CTRL != null && Weapon_Two_CTRL.Object != null && Weapon_Two_CTRL.Object.IsValid);
            Weapon_Two_CTRL.SetBullet(currentBullet, maxBullet);
        }
    }

    private async Task WaitUntilReady(System.Func<bool> condition)
    {
        while (!condition())
            await Task.Yield();
    }
    
    // ✅ RPC từ client gửi position thật lên host để spawn
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestDropWeapon(Vector3 dropPosition, byte weaponSlot)
    {
        WeaponController ctrl = weaponSlot == 1 ? Weapon_One_CTRL : Weapon_Two_CTRL;
        if (ctrl == null) return;

        SpawnDropItem(ctrl, dropPosition);
    }
    
    #endregion

    private void Start()
    {
        if (!NeedsResolveAfterMigration)
        {
            if (Object.HasStateAuthority)
                SwitchWeaponActive(0);
            SwitchWeapon();
        }
    }

    #region Switch Weapon
    
    /// <summary>
    /// duoc toi tu animation event
    /// </summary>
    public void SwitchWeapon()
    {
        if (Weapon_One_CTRL == null && _weaponOneRef != null || Weapon_Two_CTRL == null && _weaponTwoRef != null)
            ResolveWeaponControllers();
        
        if (_weapon_Active == 1)
        {
            if (Weapon_One_CTRL == null || !IsWeaponReady(Weapon_One_CTRL)) return;
            
            SwitchToWeaponOne();
            _componentManager.GameHudController().SwitchWeaponUI(Weapon_One_CTRL);
        }
        if (_weapon_Active == 2)
        {
            if (Weapon_Two_CTRL == null || !IsWeaponReady(Weapon_Two_CTRL)) return;
            
            SwitchToWeaponTwo();
            _componentManager.GameHudController().SwitchWeaponUI(Weapon_Two_CTRL);
        }
    }
    
    private bool IsWeaponReady(WeaponController ctrl)
    {
        return ctrl != null 
               && ctrl.Object != null 
               && ctrl.Object.IsValid;
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
            Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon One", this);
            _componentManager.ConstraintManager?.SwitchWeapon(_weaponOneID.ToString());
            _weaponOneRef?.gameObject.SetActive(true);
            _weaponTwoRef?.gameObject.SetActive(false);
            _anim?.WeaponIndex(Weapon_One_CTRL.Data.Index);
        }
    }

    private void SwitchToWeaponTwo()
    {
        if (_weaponTwoRef != null)
        {
            Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon Two", this);
            _componentManager.ConstraintManager?.SwitchWeapon(_weaponTwoID.ToString());
            _weaponOneRef?.gameObject.SetActive(false);
            _weaponTwoRef?.gameObject.SetActive(true);
            _anim?.WeaponIndex(Weapon_Two_CTRL.Data.Index);
        }
    }

    public void HideWeaponActive()
    {
        if (_weapon_Active == 1)
        {
            _weaponOneRef?.gameObject.SetActive(false);

            if (Object.HasStateAuthority)
            {
                SpawnDropItem(Weapon_One_CTRL);
            }
            else if (Object.HasInputAuthority) // ✅ Chỉ client sở hữu mới gửi RPC
            {
                Vector3 pos = transform.position;
                pos.y = 0.5f;
                Rpc_RequestDropWeapon(pos, _weapon_Active);
            }
            // Client khác (proxy) không làm gì cả — host lo việc spawn
        }
        else if (_weapon_Active == 2)
        {
            _weaponTwoRef?.gameObject.SetActive(false);

            if (Object.HasStateAuthority)
            {
                SpawnDropItem(Weapon_Two_CTRL);
            }
            else if (Object.HasInputAuthority) // ✅
            {
                Vector3 pos = transform.position;
                pos.y = 0.5f;
                Rpc_RequestDropWeapon(pos, _weapon_Active);
            }
        }
    }

    private void SpawnDropItem(WeaponController ctrl, Vector3? overridePos = null)
    {
        if (ctrl.Data.DropItemPrefab == null) return;
        
        Vector3 pos = overridePos ?? transform.position;
    
        // ✅ Raycast xuống tìm mặt đất
        if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 10f, _groundLayer))
        {
            pos = hit.point;
            pos.y += 0.1f; // offset nhỏ để không bị clip xuống đất
        }
        else
        {
            pos.y = 0.5f; // fallback nếu không tìm thấy ground
        }

        Runner.Spawn(ctrl.Data.DropItemPrefab, pos, onBeforeSpawned:
            (runner, obj) =>
            {
                DropItemWeapon item = obj.GetComponent<DropItemWeapon>();
                if (item != null)
                {
                    item.SetBullet(
                        ctrl.CurrentBulletvalue,
                        ctrl.CurrentMaxBullet,
                        ctrl.Data.WeaponName,
                        _componentManager.GameHudController());
                }
            });
    }
    
    #endregion

    public WeaponController Weapon_Active()
    {
        if (_weaponOneRef != null && Weapon_One_CTRL != null && Weapon_One_CTRL.gameObject.activeSelf)
            return Weapon_One_CTRL;
        if (_weaponTwoRef != null && Weapon_Two_CTRL != null && Weapon_Two_CTRL.gameObject.activeSelf)
            return Weapon_Two_CTRL;
        // FIX #3: hàm này được gọi ~10 lần/frame từ PlayerMotionController (IsDead, IsCrouching,
        // ADS, Move, v.v.). Debug.Log không điều kiện ở đây sẽ spam console mỗi frame trong bất
        // kỳ khoảng thời gian nào chưa có weapon active (vài frame đầu sau spawn, lúc switch
        // weapon, lúc resolve sau migration) — tốn hiệu năng thật kể cả ở build release.
        // Giữ log lại nhưng chỉ bật trong Editor để debug, không chạy ở build.
#if UNITY_EDITOR
        Debug.Log("<color=red>[WeaponManager]</color> WeaponController not active.", this);
#endif
        return null;
    }

    public void Respawn()
    {
        if (Weapon_One_CTRL != null)
        {
            _weapon_Active = 1;
            Weapon_One_CTRL.Respawn();
        }
        if (Weapon_Two_CTRL != null)
        {
            if (Weapon_One_CTRL == null)
                _weapon_Active = 2;
            Weapon_Two_CTRL.Respawn();
        }
    }

    void OnWeaponRefsChanged()
    {
        // FIX: cleanup phải dựa trên _weapon_X_CTRL / ID ĐANG CACHE (giá trị cũ), KHÔNG phụ
        // thuộc _weaponXRef mới. Điều kiện cũ check "_weaponOneRef != null" — tức là ref MỚI
        // vẫn còn — nên khi despawn thật sự (_weaponOneRef chuyển thành null), khối cleanup
        // không bao giờ chạy: _weapon_One_CTRL giữ tham chiếu dangling tới object đã despawn,
        // và entry cũ trong ConstraintManager không bao giờ được remove (leak). Kiểm tra
        // _weapon_One_CTRL != null (cache cũ) mới đúng ý định "vừa có 1 controller cũ cần dọn".
        if (Weapon_One_CTRL != null)
        {
            var idOne = _weaponOneID.ToString();
            if (!idOne.IsNullOrEmpty())
                _componentManager.ConstraintManager.RemoveWeapon(idOne);
            Weapon_One_CTRL = null;
        }
        if (Weapon_Two_CTRL != null)
        {
            var idTwo = _weaponTwoID.ToString();
            if (!idTwo.IsNullOrEmpty())
                _componentManager.ConstraintManager.RemoveWeapon(idTwo);
            Weapon_Two_CTRL = null;
        }

        // Non-authority: ResolveWeaponControllers trả về true nếu có weapon đang chờ ID sync.
        // SwitchWeapon sẽ được gọi bên trong coroutine sau khi AddNewWeapon hoàn tất.
        // Authority: trả về false — AddNewWeapon đã xong đồng bộ, gọi SwitchWeapon ngay.
        bool pendingAsync = ResolveWeaponControllers();
        if (!pendingAsync)
            SwitchWeapon();
    }
    
    /// <summary>
    /// Resolve WeaponController references và đăng ký vào ConstraintManager.
    /// Trả về true nếu có ít nhất một weapon đang chờ ID sync qua coroutine (non-authority).
    /// Caller không được gọi SwitchWeapon ngay khi kết quả là true — coroutine sẽ lo.
    /// </summary>
    private bool ResolveWeaponControllers()
    {
        bool hasPending = false;

        if (_weaponOneRef != null && Weapon_One_CTRL == null)
        {
            Weapon_One_CTRL = _weaponOneRef.GetComponent<WeaponController>();
            if (Weapon_One_CTRL != null)
            {
                if (Object.HasStateAuthority)
                {
                    // Authority: set ID ngay, add đồng bộ luôn.
                    Weapon_One_CTRL.Respawn();
                    _weaponOneID = new NetworkString<_16>(Weapon_One_CTRL.Data.ID + "_STTWP1");
                    _componentManager.ConstraintManager.AddNewWeapon(Weapon_One_CTRL, _weaponOneID.ToString());
                }
                else
                {
                    // Non-authority: _weaponOneID chưa chắc đã sync về kịp.
                    // Chờ đến khi ID khớp với weapon vừa resolve xong rồi mới AddNewWeapon.
                    hasPending = true;
                    var ctrl = Weapon_One_CTRL;
                    StartCoroutine(WaitForIDThenAdd(
                        ctrl,
                        ctrl.Data.ID + "_STTWP1",
                        () => _weaponOneID.ToString(),
                        () => Weapon_One_CTRL == ctrl,   // hủy nếu weapon đã bị thay
                        isLast: true                      // coroutine này chịu trách nhiệm gọi SwitchWeapon
                    ));
                }
            }
        }

        if (_weaponTwoRef != null && Weapon_Two_CTRL == null)
        {
            Weapon_Two_CTRL = _weaponTwoRef.GetComponent<WeaponController>();
            if (Weapon_Two_CTRL != null)
            {
                if (Object.HasStateAuthority)
                {
                    Weapon_Two_CTRL.Respawn();
                    _weaponTwoID = new NetworkString<_16>(Weapon_Two_CTRL.Data.ID + "_STTWP2");
                    _componentManager.ConstraintManager.AddNewWeapon(Weapon_Two_CTRL, _weaponTwoID.ToString());
                }
                else
                {
                    // Nếu weapon one cũng đang pending, weapon two KHÔNG được gọi SwitchWeapon
                    // (weapon one sẽ lo). Nếu chỉ mình weapon two pending thì nó gọi.
                    bool twoIsLast = !hasPending;
                    hasPending = true;
                    var ctrl = Weapon_Two_CTRL;
                    StartCoroutine(WaitForIDThenAdd(
                        ctrl,
                        ctrl.Data.ID + "_STTWP2",
                        () => _weaponTwoID.ToString(),
                        () => Weapon_Two_CTRL == ctrl,
                        isLast: twoIsLast
                    ));
                }
            }
        }

        return hasPending;
    }

    /// <summary>
    /// Coroutine dành cho non-authority client: poll đến khi NetworkString đã sync về
    /// đúng ID của weapon vừa resolve, rồi mới gọi AddNewWeapon.
    /// Nếu isLast == true, gọi SwitchWeapon sau khi add xong.
    /// </summary>
    private System.Collections.IEnumerator WaitForIDThenAdd(
        WeaponController ctrl,
        string expectedID,
        System.Func<string> getID,
        System.Func<bool> isStillValid,
        bool isLast)
    {
        // Chờ đến khi ID synced về chứa đúng Data.ID của weapon này
        while (true)
        {
            if (!isStillValid()) yield break;   // weapon đã bị replace — hủy

            var currentID = getID();
            if (!currentID.IsNullOrEmpty() && currentID == expectedID)
                break;

            yield return null;
        }

        if (!isStillValid()) yield break;

        var id = getID();
        if (!id.IsNullOrEmpty())
            _componentManager.ConstraintManager.AddNewWeapon(ctrl, id);

        // Chỉ coroutine "cuối cùng" mới gọi SwitchWeapon để tránh gọi nhiều lần
        if (isLast)
            SwitchWeapon();
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