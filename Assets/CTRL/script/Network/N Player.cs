using System.Collections;
using Fusion;
using UnityEngine;

public class NPlayer : NetworkBehaviour, IPlayerLeft
{
    // ======================= Network field ==============================
    [Header("Character Name")] [SerializeField]
    public string Name;
    
    [Header("Setting")]
    [Networked] public PlayerTeam NetworkedTeam { get; set; } = PlayerTeam.Zero;
    
    [Networked] public GamePlayMode GamePlayMode { get; set; }
    
    [Networked, OnChangedRender(nameof(OnNicknameChanged))]
    public NetworkString<_16> Nickname { get; private set; }
    
    [Networked] public int Token { get; set; }
    [Networked] public NetworkString<_512> AvatarUrl { get; set; } // [MVP-AVATAR 2026-10-01 Aemy]

    // Lưu raw 16 bytes của Guid token để có thể restore sau host migration
    [Networked, Capacity(16)] public NetworkArray<byte> RawToken => default;
    
    // ===================== Private Field ==================================
    
    [SerializeField] private Transform[] _hide_Mesh;

    private bool _isPublicJoinMessageSent = false;
    
    public CharComponentManager ComponentManager { get; private set; }
    private NInGameMessagesHandler _nInGameMessagesHandler;
    //private UIHPPlayer _uiLocalPlayer;
    private UIPlayerNickName _uiPlayerNickName;
    private NShootHandler _shootHandler;
    public NCharMovementHandler _movementHandler;

    // Cache TRỰC TIẾP GameObject camera đã bị reparent ra ngoài (transform.parent = null),
    // thay vì đi qua _componentManager lúc OnDestroy(). Lý do: khi runner.Shutdown() (host migration)
    // phá hủy hàng loạt, Unity KHÔNG đảm bảo thứ tự OnDestroy giữa các component -
    // _componentManager có thể đã bị hủy trước khi NPlayer.OnDestroy() chạy, khiến
    // (_componentManager != null) trả về false dù bản chất object vẫn từng tồn tại,
    // và ta mất luôn đường tới camera cần Destroy() thủ công -> camera bị leak.
    // Cache thẳng ở đây đảm bảo OnDestroy() luôn dọn được camera, bất kể _componentManager sống hay đã chết.
    private GameObject _detachedCameraRoot;
    
    public bool IsLocalPlayer {get; private set;}

    // [Aemy 2026-10-09] Hook do nhay live: doi thiet bi (PC <-> tay cam) hoac keo slider -> gui lai CameraSpeed.
    private bool _sensHooked;
    private float _lastSentSens = -1f;
    
    // ============================== Public Field =====================================
    
    public static NPlayer Local { get; private set; }

    public override void Spawned()
    {
        InitPlayer();
        GameManager.Instance.PlayerList.Add(_shootHandler);
    }
    
    public void OnHostMigrated()
    {
        // Logic phục hồi state cho Player cũ trên Host mới
        InitPlayer();
    }
    
    private void InitPlayer()
    {
        ComponentManager = GetComponent<CharComponentManager>();
        _nInGameMessagesHandler = ComponentManager.NInGameMessagesHandler;
        //_uiLocalPlayer = _componentManager.UihpPlayer;
        _uiPlayerNickName = ComponentManager.UIPlayerNickName;
        _shootHandler = ComponentManager.NShootHandler;
        _movementHandler = ComponentManager.NCharMovementHandler;
        
        IsLocalPlayer = Object.HasInputAuthority 
                        || (Runner.IsServer && Object.StateAuthority == Runner.LocalPlayer);
        
        if (IsLocalPlayer)
        {
            Local = this;
            
            ComponentManager.DisplayUI();
            
            GameManager.Instance.setPlayerID(Object.Id);
            
            Utils.SetRenderLayerInChildren(_hide_Mesh, LayerMask.NameToLayer("LocalPlayerModel"));
            
            Camera.main?.gameObject.SetActive(false);
            
            //_uiLocalPlayer?.gameObject.SetActive(true);

            var parrentCamera = ComponentManager.Camera.transform.parent.gameObject;
            parrentCamera.SetActive(true);

            // ✅ InitPlayer() có thể chạy 2 lần trên cùng object (Spawned() rồi OnHostMigrated()).
            // Chỉ tách camera ra khỏi hierarchy 1 lần duy nhất - nếu tách lại lần 2, ta sẽ
            // touch vào transform.parent của chính cái root đã bị set null trước đó
            // (parrentCamera.transform.parent.parent lúc này không còn trỏ đúng chỗ),
            // dẫn tới cache lệch đối tượng như bug cũ.
            if (_detachedCameraRoot == null)
            {
                GameObject cameraRoot = parrentCamera.transform.parent.parent.gameObject;
                cameraRoot.name = $"{Object.Id}-Camera-Root";
                cameraRoot.transform.parent = null;

                // ✅ Cache ĐÚNG object đã bị tách ra ngoài (cameraRoot), không phải con của nó
                // (parrentCamera). Trước đây cache nhầm parrentCamera khiến OnDestroy() chỉ
                // Destroy() được phần con, còn cái vỏ "[Id]-Camera-Root" rỗng bị leak lại trong scene.
                _detachedCameraRoot = cameraRoot;
            }

            _uiPlayerNickName.gameObject.SetActive(false);
            
            Rpc_SetNickName(GameManager.Instance.GetPlayerNickName());
            var _av = Backend.Bootstrap.ProfileCache.Cached != null ? Backend.Bootstrap.ProfileCache.Cached.AvatarUrl : null; // [MVP-AVATAR]
            if (!string.IsNullOrEmpty(_av)) Rpc_SetAvatarUrl(_av);
            _lastSentSens = SettingManager.Instance.SettingData.MouseSensitivity;
            SendLookSettings(); // [NET-OPT 2026-10-18 Aemy] 1 RPC gom do nhay + he so ADS
            HookSensitivity(); // [Aemy 2026-10-09]

            // ✅ Gửi token của mình lên server ngay khi spawn, KHÔNG phụ thuộc vào
            // ConnectionToken ở transport-level (không đáng tin cậy qua Photon Relay).
            // Server sẽ dùng token này để nhận diện: player mới hay đang reconnect / resume sau host migration.
            byte[] myToken = GameManager.Instance.GetIdentityToken();
            if (myToken != null && myToken.Length == 16)
            {
                Rpc_RegisterToken(myToken);
            }
            else
            {
                Debug.LogWarning("<color=green>[NPlayer]</color> Identity token invalid, cannot register.", this);
            }
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Local Player.", this);
        }
        else
        {
            //_uiLocalPlayer?.gameObject.SetActive(false);
            GameObject localCamera = ComponentManager.Camera.transform.parent.gameObject;
            localCamera.SetActive(false);
            
            if (!string.IsNullOrEmpty(Nickname.ToString()))
            {
                _uiPlayerNickName.SetNickName(Nickname.ToString());
            }
            
            Debug.Log("<color=green>[NPlayer]</color> Spawned Remote Player.", this);
            StartCoroutine(SetRemotePlayerDisplayNextFrame());
        }

        // ❌ KHÔNG gọi Rpc_SyncTokenToClient ở đây nữa.
        // Lý do: tại thời điểm này Rpc_RegisterToken (gửi từ client lên) CHƯA được server xử lý xong
        // (RPC không chạy đồng bộ, kể cả trên host), nên RawToken lúc này vẫn còn rỗng.
        // Việc sync token về client giờ được kích hoạt từ NSpawner.ResolvePlayerToken,
        // đúng lúc RawToken đã thực sự được ghi.

        // set player này là player object (chỉ khi thực sự có input authority)
        if (!Object.InputAuthority.IsNone)
        {
            Runner.SetPlayerObject(Object.InputAuthority, Object);
        }

        transform.name = $"Player_{Object.Id}";
    }
    
    private IEnumerator SetRemotePlayerDisplayNextFrame()
    {
        // Chờ đến khi Local được set (tối đa vài frame)
        yield return new WaitUntil(() => NPlayer.Local != null);
    
        _uiPlayerNickName.SetPlayerTeam(DisplayTeam);
    
        if (!string.IsNullOrEmpty(Nickname.ToString()))
            _uiPlayerNickName.SetNickName(Nickname.ToString());
    }
    
    private PlayerTeam DisplayTeam
    {
        get
        {
            if (Object.HasInputAuthority) 
                return PlayerTeam.Blue; // luôn là mình

            // Solo mode: địch luôn Red
            if (GamePlayMode == GamePlayMode.Solo)
                return PlayerTeam.Red;

            // Team mode: cùng team với mình → Blue, khác team → Red
            return NetworkedTeam == NPlayer.Local.NetworkedTeam 
                ? PlayerTeam.Blue 
                : PlayerTeam.Red;
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerLeftNetworkObject))
            {
                if (playerLeftNetworkObject == Object)
                {
                    // sẽ chuyển thông báo join và left sang chat box
                    Local.GetComponent<NInGameMessagesHandler>().SenManagerMessage(
                        $"{playerLeftNetworkObject.GetComponent<NPlayer>().Nickname.ToString()} da thoat");

                    // ✅ Giảm count theo team ngay tại đây, lúc này object vẫn còn sống
                    if (NetworkedTeam == PlayerTeam.Blue)
                    {
                        if (NSpawner.Instance.BlueCount > 0) NSpawner.Instance.BlueCount--;
                    }
                    else if (NetworkedTeam == PlayerTeam.Red)
                    {
                        if (NSpawner.Instance.RedCount > 0) NSpawner.Instance.RedCount--;
                    }
                
                    Debug.Log($"<color=green>[NPlayer]</color> Player left. Team: {NetworkedTeam}. Blue: {NSpawner.Instance.BlueCount}, Red: {NSpawner.Instance.RedCount}");
                }
            }
        }

        if (player == Object.InputAuthority)
            Runner.Despawn(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetNickName(string nickName, RpcInfo info = default)
    {
        Debug.Log($"<color=green>[NPlayer]</color> Rpc_SetNickName: {nickName}", this);
        Nickname = nickName;

        if (!_isPublicJoinMessageSent)
        {
            // sẽ chuyển thông báo join và left sang chat box
            _nInGameMessagesHandler.SenManagerMessage($"{nickName} da vao phong.");
            _isPublicJoinMessageSent = true;
        }
    }

    // ✅ Client (InputAuthority) gửi raw token của mình lên server ngay sau khi spawn.
    // Đây là nguồn "sự thật" duy nhất cho danh tính player, thay cho ConnectionToken
    // ở transport-level (không hoạt động qua Photon Relay).
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetAvatarUrl(string url, RpcInfo info = default) { AvatarUrl = url; } // [MVP-AVATAR 2026-10-01]

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RegisterToken(byte[] rawToken, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"<color=green>[NPlayer]</color> Rpc_RegisterToken received from {Object.InputAuthority}, hash = {NConnectionTokenUtils.HashToken(rawToken)}", this);

        NSpawner.Instance.ResolvePlayerToken(Runner, this, Object.InputAuthority, rawToken);
    }

    // ✅ Được NSpawner.ResolvePlayerToken gọi, đúng thời điểm RawToken đã thực sự có dữ liệu.
    // Public để NSpawner (server-side) có thể trigger.
    public void TriggerSyncTokenToClient()
    {
        if (Object.HasStateAuthority)
        {
            Rpc_SyncTokenToClient();
        }
    }
    
    // Gọi từ host mới sau khi resume xong, broadcast xuống tất cả client
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_OnHostMigrated(RpcInfo info = default)
    {
        OnHostMigrated();
    }

    // ✅ Server gửi RawToken về đúng client (InputAuthority) để sync GameManager
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void Rpc_SyncTokenToClient(RpcInfo info = default)
    {
        byte[] raw = RawToken.ToArray();

        // Chỉ update nếu RawToken thực sự có dữ liệu (khác all-zero)
        bool hasData = false;
        foreach (byte b in raw)
        {
            if (b != 0) { hasData = true; break; }
        }

        if (hasData)
        {
            GameManager.Instance.SetConnectionToken(raw);
            Debug.Log($"<color=green>[NPlayer]</color> Rpc_SyncTokenToClient: token synced, hash = {NConnectionTokenUtils.HashToken(raw)}", this);
        }
        else
        {
            Debug.LogWarning("<color=green>[NPlayer]</color> Rpc_SyncTokenToClient: RawToken is empty, skipping sync.", this);
        }
    }

    void OnNicknameChanged()
    {
        Debug.Log($"<color=green>[NPlayer]</color> OnNicknameChanged to {Nickname.ToString()}", this);
        ComponentManager.UIPlayerNickName.SetNickName(Nickname.ToString());
    }

    // [Aemy 2026-10-09] Dang ky 1 lan (InitPlayer co the chay 2 lan do host migration).
    private void HookSensitivity()
    {
        if (_sensHooked || SettingManager.Instance == null) return;
        SettingManager.Instance.OnSensitivityChanged += OnSensitivityChangedLive;
        _sensHooked = true;
    }

    private void UnhookSensitivity()
    {
        if (!_sensHooked) return;
        if (SettingManager.Instance != null) SettingManager.Instance.OnSensitivityChanged -= OnSensitivityChangedLive;
        _sensHooked = false;
    }

    private void OnSensitivityChangedLive(float value)
    {
        if (this == null || Object == null || !Object.IsValid || !IsLocalPlayer || _movementHandler == null) return;
        var sd = SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        float sn = sd != null ? sd.SniperADSSensitivity : _lastSentSniper;
        float rf = sd != null ? sd.RifleADSSensitivity : _lastSentRifle;
        // Apply() ban event moi lan doi setting -> bo qua neu ca 3 gia tri khong doi
        if (Mathf.Approximately(value, _lastSentSens) && Mathf.Approximately(sn, _lastSentSniper) && Mathf.Approximately(rf, _lastSentRifle)) return;
        _lastSentSens = value;
        SendLookSettings();
    }

    // [NET-OPT 2026-10-18 Aemy] (bao cao 9.5) Gui do nhay + he so ADS sniper/rifle trong 1 RPC.
    private float _lastSentSniper = -1f, _lastSentRifle = -1f;
    private void SendLookSettings()
    {
        var sd = SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        if (sd == null || _movementHandler == null) return;
        _lastSentSniper = sd.SniperADSSensitivity;
        _lastSentRifle  = sd.RifleADSSensitivity;
        _movementHandler.SendLookSettings(_lastSentSens, _lastSentSniper, _lastSentRifle);
    }

    void OnDestroy()
    {
        UnhookSensitivity(); // [Aemy 2026-10-09]

        // ✅ Chỉ local player mới có camera bị reparent ra ngoài (mồ côi, không tự despawn theo).
        // Remote player: camera vẫn là con của NetworkObject này -> Fusion/Unity tự destroy theo,
        // không cần xử lý thủ công.
        if (_detachedCameraRoot != null)
        {
            Destroy(_detachedCameraRoot);
        }

        if (Local == this)
        {
            Local = null;
        }
    }
}