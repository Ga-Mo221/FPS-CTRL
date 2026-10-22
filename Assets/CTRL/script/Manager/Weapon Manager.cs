using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;
using WebSocketSharp;

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
    
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    // Weapon One
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponOneRef { get; set; }
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    // Bo sync _weaponOneID (NetworkString<_16> ~17 word):
    // ID luon = Data.ID + "_W1" nen moi may tu tinh -> het coroutine WaitForIDThenAdd (polling + race).
    public string _weaponOneID => _registeredIDOne ?? "";
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    public WeaponController Weapon_One_CTRL;
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    // Weapon Two
    [Networked, OnChangedRender(nameof(OnWeaponRefsChanged))]
    public NetworkObject _weaponTwoRef { get; set; }
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    public string _weaponTwoID => _registeredIDTwo ?? ""; // [NET-OPT] xem _weaponOneID
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    public WeaponController Weapon_Two_CTRL;
    
    [TabGroup("Debug", "Debug")]
    [ReadOnly]
    // Active Weapon
    [Networked] public byte _weapon_Active { get; set; } = 0;

    // Âm thanh vứt / nhặt súng: host tăng bộ đếm khi thao tác THÀNH CÔNG, mọi máy thấy đổi trong Render()
    // thì phát âm 3D tại Audio_Body của nhân vật (không cần thêm RPC; byte quay vòng là đủ).
    [Networked] private byte _dropSoundCount { get; set; }
    [Networked] private byte _pickupSoundCount { get; set; }
    private byte _lastDropSound;
    private byte _lastPickupSound;

    #if UNITY_EDITOR
    [TabGroup("Debug", "Debug")]
    [SerializeField] private bool _debug;
    #endif

    // ── Tracking local (không networked) ─────────────────────────────────────
    // NetworkId của weapon mà Weapon_X_CTRL đang trỏ tới (set lúc resolve).
    // Dùng để callback OnWeaponRefsChanged biết slot nào đã bị thay.
    private NetworkId _slotNetIdOne;
    private NetworkId _slotNetIdTwo;

    // ID đã đăng ký vào ConstraintManager (set lúc AddNewWeapon).
    // Dùng để RemoveWeapon đúng ID, không phụ thuộc _weaponXID đã sync kịp hay chưa.
    private string _registeredIDOne;
    private string _registeredIDTwo;
    
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
        // Người vào sau / host migration: không phát lại âm cũ.
        _lastDropSound = _dropSoundCount;
        _lastPickupSound = _pickupSoundCount;
    }

    public override void Render()
    {
        if (_componentManager == null) return;
        if (_dropSoundCount != _lastDropSound)
        {
            _lastDropSound = _dropSoundCount;
            var d = _componentManager.NCharMovementHandler != null ? _componentManager.NCharMovementHandler.Data : null;
            if (d != null) PlayBodySound(d.DropWeaponClips, d.Volume_DropWeapon);
        }
        if (_pickupSoundCount != _lastPickupSound)
        {
            _lastPickupSound = _pickupSoundCount;
            var d = _componentManager.NCharMovementHandler != null ? _componentManager.NCharMovementHandler.Data : null;
            if (d != null) PlayBodySound(d.PickupWeaponClips, d.Volume_PickupWeapon);
        }
    }

    private void PlayBodySound(AudioClip[] clips, float volume)
    {
        // Chưa gán clip trong PlayerData thì bỏ qua (AudioManager sẽ log lỗi nếu mảng rỗng).
        if (clips == null || clips.Length == 0 || AudioManager.Instance == null) return;
        var source = _componentManager.Audio_Body;
        if (source == null) return;
        AudioManager.Instance.PlayClip(source, clips, volume, false); // không cắt âm thân đang phát (bước chân, nhảy...)
    }

    #region Register / Unregister ConstraintManager

    /// <summary>
    /// Đăng ký weapon vào ConstraintManager và nhớ ID đã đăng ký.
    /// </summary>
    private void RegisterWeapon(int slot, WeaponController ctrl, string id)
    {
        _componentManager.ConstraintManager.AddNewWeapon(ctrl, id);

        if (slot == 1) _registeredIDOne = id;
        else           _registeredIDTwo = id;
    }

    /// <summary>
    /// Gỡ weapon slot One khỏi ConstraintManager (nếu đã đăng ký) và reset tracking.
    /// Dùng chung cho host (SpawnWeaponOne) và client (OnWeaponRefsChanged).
    /// </summary>
    private void UnregisterSlotOne()
    {
        if (!_registeredIDOne.IsNullOrEmpty())
            _componentManager.ConstraintManager.RemoveWeapon(_registeredIDOne);

        _registeredIDOne = null;
        _slotNetIdOne = default;
        Weapon_One_CTRL = null;
    }

    /// <summary>
    /// Gỡ weapon slot Two khỏi ConstraintManager (nếu đã đăng ký) và reset tracking.
    /// </summary>
    private void UnregisterSlotTwo()
    {
        if (!_registeredIDTwo.IsNullOrEmpty())
            _componentManager.ConstraintManager.RemoveWeapon(_registeredIDTwo);

        _registeredIDTwo = null;
        _slotNetIdTwo = default;
        Weapon_Two_CTRL = null;
    }

    #endregion

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

    // Chỉ chạy trên StateAuthority (được gọi từ RPC InputAuthority -> StateAuthority).
    // Client sẽ tự remove trong OnWeaponRefsChanged khi ref đổi.
    private void SpawnWeaponOne(string weaponOne)
    {
        if (Weapon_One_CTRL != null)
        {
            UnregisterSlotOne();
            
            Runner.Despawn(_weaponOneRef);
            _weaponOneRef = null;
        }
        
        _weapon_One_Prefab = GameManager.Instance.GetWeapon(weaponOne);

#if UNITY_EDITOR
        if (_debug && _weapon_One_Prefab == null)
            Debug.LogError($"<color=green>[WeaponManager][Spawn Weapon One]</color> Weapon One == NULL, ID {weaponOne}");
#endif
    }

    private void SpawnWeaponTwo(string weaponTwo)
    {
        if (Weapon_Two_CTRL != null)
        {
            UnregisterSlotTwo();
            
            Runner.Despawn(_weaponTwoRef);
            _weaponTwoRef = null;
        }
        
        _weapon_Two_Prefab = GameManager.Instance.GetWeapon(weaponTwo);
        
#if UNITY_EDITOR
        if (_debug && _weapon_Two_Prefab == null)
            Debug.LogError($"<color=green>[WeaponManager][Spawn Weapon Two]</color> Weapon Two == NULL, ID {weaponTwo}");
#endif
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
        if (prefab == null)
        {
#if UNITY_EDITOR
            if (_debug) Debug.LogError("<color=green>[WeaponManager][Create Weapon]</color> Prefab == NULL");
#endif
            return;
        }
        
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
        
#if UNITY_EDITOR
        if (_debug)
            Debug.Log($"<color=green>[WeaponManager]</color> ResolveWeaponControllers after migration: {name}", this);
#endif
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
    
    // Host chap nhan nhat neu nguoi choi cach item <= khoang nay (client hien nut nhat o 1.5m, du cho tre mang).
    private const float PICKUP_MAX_DISTANCE = 2.5f;
    private const float PICKUP_READY_TIMEOUT = 2f;

    /// <summary>
    /// Client bam nhat vu khi: CHI gui NetworkId cua item. Host tu kiem tra (item con ton tai, chua bi ai nhat,
    /// du gan), doc loai sung + so dan TU CHINH ITEM, xoa item roi trao sung.
    /// Truoc day client gui ten sung + so dan (tin client -> sua goi tin la co dan) va 1 RPC xoa item rieng
    /// (RpcSources.All, khong kiem tra) -> 2 nguoi nhat cung item deu nhan duoc sung.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestPickup(NetworkId itemId, RpcInfo info = default)
    {
        if (!Runner.TryFindObject(itemId, out var itemObj) || itemObj == null || !itemObj.IsValid) { PickupReject("khong tim thay item " + itemId); return; }
        var item = itemObj.GetComponent<DropItemWeapon>();
        if (item == null) { PickupReject("object khong phai DropItemWeapon"); return; }

        float dist = Vector3.Distance(itemObj.transform.position, Object.transform.position);
        if (dist > PICKUP_MAX_DISTANCE) { PickupReject($"qua xa ({dist:F2}m > {PICKUP_MAX_DISTANCE}m)"); return; }

        string weaponId = item.WeaponID;
        if (string.IsNullOrEmpty(weaponId)) { PickupReject("item khong co WeaponID"); return; }
        if (!item.TryClaim()) { PickupReject("item da duoc nhat"); return; } // da co nguoi nhat trong cung tick

        int currentBullet = item.CurrentBullet;
        int maxBullet = item.MaxBullet;
        Runner.Despawn(itemObj);

        // Ưu tiên lấp slot trống (sau khi vứt súng). Không có slot trống → thay món ĐANG CẦM:
        //  - đang cầm súng → súng đó bị tung nhẹ ra trước (nhặt-đổi giống CS2);
        //  - đang cầm dao  → súng mới thay chỗ dao, dao biến mất (dao không rơi ra), vd M4 + dao → 2 M4.
        int slot = _weaponOneRef == null ? 1 : _weaponTwoRef == null ? 2 : _weapon_Active;

        var replaced = slot == 1 ? Weapon_One_CTRL : Weapon_Two_CTRL;
        if (replaced != null && replaced.WeaponType != WeaponType.Melee)
            ThrowDropItem(replaced, SWAP_THROW_SPEED);

        if (slot == 1) SpawnWeaponOne(weaponId);
        else           SpawnWeaponTwo(weaponId);

        if (_weapon_Active != slot)
        {
            _componentManager.NCharMovementHandler?.ResetADS();
            _weapon_Active = (byte)slot; // cầm luôn súng vừa nhặt (OnWeaponRefsChanged → SwitchWeapon)
        }
        EquipNewWeapon(slot);
        _pickupSoundCount++; // nhặt-đổi chỉ phát âm nhặt (không chồng thêm âm vứt cho gọn)
        StartCoroutine(SetBulletWhenReady(slot, currentBullet, maxBullet));
    }

    // ── Vứt súng (phím G / D-pad phải) ──────────────────────────────────────
    private const string EMPTY_SLOT_ID = "-"; // ID giả cho slot trống (chỉ dùng so với balo)

    /// <summary>
    /// Client bấm vứt súng đang cầm. Host quyết định: không vứt dao, không vứt món cuối cùng,
    /// không vứt khi chết / đang đổi súng / nạp đạn / ném lựu đạn. Slot vừa vứt để TRỐNG,
    /// tự đổi sang súng ở slot còn lại; nhặt súng sau đó sẽ lấp vào slot trống.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestDrop(RpcInfo info = default)
    {
        int slot = _weapon_Active;
        var ctrl = slot == 1 ? Weapon_One_CTRL : slot == 2 ? Weapon_Two_CTRL : null;
        var otherRef = slot == 1 ? _weaponTwoRef : _weaponOneRef;

        if (ctrl == null) { DropReject("khong cam sung"); return; }
        if (ctrl.WeaponType == WeaponType.Melee) { DropReject("khong vut dao"); return; }
        if (otherRef == null) { DropReject("mon cuoi cung"); return; }
        if (_componentManager.NHPHandler != null && _componentManager.NHPHandler.IsDead) { DropReject("dang chet"); return; }
        var move = _componentManager.NCharMovementHandler;
        if (move != null)
        {
            var st = move.StateFl;
            if (st.IsSwitching || st.IsReloading || st.IsThrow) { DropReject("dang doi sung / nap dan / nem"); return; }
        }

        ThrowDropItem(ctrl, THROW_SPEED); // súng bay ra theo hướng nhìn rồi rơi; đọc số đạn TRƯỚC khi despawn súng

        if (slot == 1)
        {
            UnregisterSlotOne();
            Runner.Despawn(_weaponOneRef);
            _weaponOneRef = null;
        }
        else
        {
            UnregisterSlotTwo();
            Runner.Despawn(_weaponTwoRef);
            _weaponTwoRef = null;
        }

        if (move != null) move.ResetADS();
        _weapon_Active = (byte)(slot == 1 ? 2 : 1);
        _dropSoundCount++;
        Rpc_SwitchWeapon();
    }

    private void DropReject(string reason)
    {
#if UNITY_EDITOR
        if (_debug) Debug.Log($"<color=green>[WeaponManager]</color> Vut sung bi tu choi ({name}): {reason}", this);
#endif
    }

    private void PickupReject(string reason)
    {
        Debug.LogWarning($"<color=green>[WeaponManager]</color> Nhat sung bi tu choi ({name}, tick {Runner.Tick}): {reason}", this);
    }

    // Sung moi spawn can vai frame moi resolve duoc controller -> cho co gioi han (truoc day async void cho vo han).
    private System.Collections.IEnumerator SetBulletWhenReady(int slot, int currentBullet, int maxBullet)
    {
        float deadline = Time.unscaledTime + PICKUP_READY_TIMEOUT;
        while (Time.unscaledTime < deadline)
        {
            var ctrl = slot == 1 ? Weapon_One_CTRL : Weapon_Two_CTRL;
            if (ctrl != null && ctrl.Object != null && ctrl.Object.IsValid)
            {
                ctrl.SetBullet(currentBullet, maxBullet);
                yield break;
            }
            yield return null;
        }
        Debug.LogWarning($"[WeaponManager] Nhat sung: het {PICKUP_READY_TIMEOUT}s ma sung slot {slot} chua san sang -> giu so dan mac dinh.", this);
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
#if UNITY_EDITOR
            if (_debug)
                Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon One", this);
#endif
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
#if UNITY_EDITOR
            if (_debug)
                Debug.Log("<color=green>[WeaponManager]</color> Switch to Weapon Two", this);
#endif
            _componentManager.ConstraintManager?.SwitchWeapon(_weaponTwoID.ToString());
            _weaponOneRef?.gameObject.SetActive(false);
            _weaponTwoRef?.gameObject.SetActive(true);
            _anim?.WeaponIndex(Weapon_Two_CTRL.Data.Index);
        }
    }

    /// <summary>
    /// Gọi từ animation event DeadVFX — event chạy trên MỌI máy. CHỈ host spawn súng rơi.
    /// Trước đây client chính chủ còn gửi thêm Rpc_RequestDropWeapon → host spawn 2 item chồng nhau
    /// cho mỗi lần client chết (nhặt 1 cái, cái dưới vẫn còn → "phải nhặt 2 lần").
    /// </summary>
    public void HideWeaponActive()
    {
        if (_weapon_Active == 1)
        {
            _weaponOneRef?.gameObject.SetActive(false);
            if (Object.HasStateAuthority)
                SpawnDropItem(Weapon_One_CTRL);
        }
        else if (_weapon_Active == 2)
        {
            _weaponTwoRef?.gameObject.SetActive(false);
            if (Object.HasStateAuthority)
                SpawnDropItem(Weapon_Two_CTRL);
        }
    }

    /// <param name="launchVelocity">null = đặt thẳng xuống đất (vd khi chết). Có giá trị = súng bay ra từ
    /// <paramref name="overridePos"/> với vận tốc này rồi rơi xuống (vứt chủ động, giống CS2).</param>
    private void SpawnDropItem(WeaponController ctrl, Vector3? overridePos = null, Vector3? launchVelocity = null)
    {
        if (ctrl.Data.DropItemPrefab == null) return;

        Vector3 pos = overridePos ?? transform.position;
        // Giữ hướng xoay quanh trục Y của người chơi; tư thế nằm của súng giữ như prefab (giống trước đây).
        Quaternion restRot = Quaternion.Euler(0f, Object.transform.eulerAngles.y, 0f);

        if (!launchVelocity.HasValue)
        {
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
        }

        Runner.Spawn(ctrl.Data.DropItemPrefab, pos, restRot, onBeforeSpawned:
            (runner, obj) =>
            {
                DropItemWeapon item = obj.GetComponent<DropItemWeapon>();
                if (item != null)
                {
                    item.SetBullet(
                        ctrl.CurrentBulletvalue,
                        ctrl.CurrentMaxBullet,
                        ctrl.Data.ID,
                        _componentManager.GameHudController());
                    if (launchVelocity.HasValue)
                        item.Launch(pos, launchVelocity.Value, _groundLayer, restRot);
                    else
                        item.PlaceAt(pos, restRot);
                }
            });
    }

    // ── Ném súng ra trước mặt ────────────────────────────────────────────────
    private const float THROW_EYE_HEIGHT = 1.5f;  // điểm ném ~ ngang mắt (m)
    private const float THROW_SPEED      = 5.5f;  // vứt chủ động (G)
    private const float SWAP_THROW_SPEED = 2.5f;  // nhặt-đổi súng: súng cũ chỉ tung nhẹ ra trước
    private const float THROW_UPWARD     = 1.5f;  // nâng nhẹ lên cho thành đường cong

    private void ThrowDropItem(WeaponController ctrl, float speed)
    {
        var move = _componentManager.NCharMovementHandler;
        Vector3 aim = move != null && move.AimForward.sqrMagnitude > 0.01f ? move.AimForward.normalized : Object.transform.forward;
        Vector3 eye = Object.transform.position + Vector3.up * THROW_EYE_HEIGHT;

        // Đứng sát tường: lùi điểm ném lại để súng không sinh ra bên trong tường.
        Vector3 start = eye + aim * 0.5f;
        if (Physics.SphereCast(eye, 0.1f, aim, out RaycastHit hit, 0.5f, _groundLayer, QueryTriggerInteraction.Ignore))
            start = eye + aim * Mathf.Max(0f, hit.distance - 0.1f);

        SpawnDropItem(ctrl, start, aim * speed + Vector3.up * THROW_UPWARD);
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

    /// <summary>
    /// Callback chung cho cả _weaponOneRef và _weaponTwoRef.
    /// So sánh NetworkId của ref hiện tại với weapon mà slot đang trỏ tới:
    /// nếu khác → weapon cũ đã bị thay/despawn → gỡ khỏi ConstraintManager trước khi resolve weapon mới.
    /// Trên host, SpawnWeaponOne/Two đã Unregister sẵn (tracking đã reset) nên nhánh này bị bỏ qua,
    /// tránh gọi RemoveWeapon hai lần.
    /// </summary>
    void OnWeaponRefsChanged()
    {
        NetworkId curOne = _weaponOneRef != null ? _weaponOneRef.Id : default;
        if (_slotNetIdOne.IsValid && curOne != _slotNetIdOne)
            UnregisterSlotOne();

        NetworkId curTwo = _weaponTwoRef != null ? _weaponTwoRef.Id : default;
        if (_slotNetIdTwo.IsValid && curTwo != _slotNetIdTwo)
            UnregisterSlotTwo();

        bool pendingAsync = ResolveWeaponControllers();
        if (!pendingAsync)
            SwitchWeapon();

        if (GameManager.Instance != null && Object.HasInputAuthority)
        {
            // Slot trống (vừa vứt súng) → đánh dấu khác loadout để lúc hồi sinh GetCurrentBalo() thấy
            // "đã đổi" và trả lại đủ balo (giống khi nhặt súng khác rồi chết).
            if (Weapon_One_CTRL != null)
                GameManager.Instance.SetCurrentWeapon(1, Weapon_One_CTRL.Data.ID);
            else if (_weaponOneRef == null)
                GameManager.Instance.SetCurrentWeapon(1, EMPTY_SLOT_ID);
            if (Weapon_Two_CTRL != null)
                GameManager.Instance.SetCurrentWeapon(2, Weapon_Two_CTRL.Data.ID);
            else if (_weaponTwoRef == null)
                GameManager.Instance.SetCurrentWeapon(2, EMPTY_SLOT_ID);
        }
    }
    
    /// <summary>
    /// Resolve WeaponController references và đăng ký vào ConstraintManager.
    /// Trả về true nếu có ít nhất một weapon đang chờ ID sync qua coroutine (non-authority).
    /// Caller không được gọi SwitchWeapon ngay khi kết quả là true — coroutine sẽ lo.
    /// </summary>
    private bool ResolveWeaponControllers()
    {
        // (bao cao 9.2.4) Moi may dang ky NGAY (ID tu tinh), khong con nhanh cho ID sync.
        // Giu kieu tra ve bool (luon false) de cac cho goi cu khong doi.
        if (_weaponOneRef != null && Weapon_One_CTRL == null)
        {
            Weapon_One_CTRL = _weaponOneRef.GetComponent<WeaponController>();
            if (Weapon_One_CTRL != null)
            {
                _slotNetIdOne = _weaponOneRef.Id;
                if (Object.HasStateAuthority) Weapon_One_CTRL.Respawn();
                RegisterWeapon(1, Weapon_One_CTRL, Weapon_One_CTRL.Data.ID + "_W1");
            }
        }

        if (_weaponTwoRef != null && Weapon_Two_CTRL == null)
        {
            Weapon_Two_CTRL = _weaponTwoRef.GetComponent<WeaponController>();
            if (Weapon_Two_CTRL != null)
            {
                _slotNetIdTwo = _weaponTwoRef.Id;
                if (Object.HasStateAuthority) Weapon_Two_CTRL.Respawn();
                RegisterWeapon(2, Weapon_Two_CTRL, Weapon_Two_CTRL.Data.ID + "_W2");
            }
        }

        return false;
    }

    // Da xoa coroutine WaitForIDThenAdd (ID vu khi gio tu tinh, khong cho sync).

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