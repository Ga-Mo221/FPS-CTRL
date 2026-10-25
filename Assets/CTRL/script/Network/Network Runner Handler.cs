using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(100)]
public class NetworkRunnerHandler : MonoBehaviour
{
    [SerializeField] private NetworkRunner _networkRunner_Prefab;
    [ReadOnly]
    [SerializeField] private NetworkRunner SpawnNetworkRunner;
    
    [SerializeField] private float _hostMigrationCleanupDelay = 5f;
    private SceneRef _lastSceneRef = SceneRef.None;

    private void Awake()
    {
        if (GameManager.Instance.NetworkRunnerHandler != null) Destroy(gameObject);
        else GameManager.Instance.NetworkRunnerHandler = this;
        
        NetworkRunner networkRunnerInScene = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
        
        if (networkRunnerInScene != null)
            SpawnNetworkRunner = networkRunnerInScene;
        
        DontDestroyOnLoad(gameObject);
    }

    // Scene menu: KHONG auto-start NetworkRunner (vao tran qua FusionMatchHandoff.CreateGame).
    private static bool IsMenuScene(string n)
    {
        return n == "MainMenu_Test" || n == "Login_Register" || n == "Loading" || n == "MenuShell";
    }

    private void Start()
    {
        if (SpawnNetworkRunner == null)
        {
            SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
            SpawnNetworkRunner.name = "Network Runner";

            if (!IsMenuScene(SceneManager.GetActiveScene().name)) // khong auto-start tran trong scene menu (Login_Register/Loading/MenuShell)
            // lấy scene hiện tại rồi chuyển sang sceneref bằng convert của fusion.
            {
                Scene UnityScene = SceneManager.GetActiveScene();
                SceneRef scene = SceneRef.FromIndex(UnityScene.buildIndex);

                var clientTask = InitializeNetworkRunner
                (
                    SpawnNetworkRunner,
                    GameMode.AutoHostOrClient,
                    "TestRoom",
                    "OurLobbyID",
                    GameManager.Instance.BuildConnectionToken(),
                    NetAddress.Any(),
                    scene
                );
            }
            Debug.Log("<color=orange>Network Runner Started.</color>");
        }
    }

    public void StartHostMigration(HostMigrationToken hostMigrationToken)
    {
        SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
        SpawnNetworkRunner.name = "Network Runner - Migration";
        
        var clientTask = InitializeNetworkRunnerHostMigration(SpawnNetworkRunner, hostMigrationToken);
        Debug.Log("<color=orange>Host Migration Start.</color>");
    }

    private INetworkSceneManager GetSceneManager(NetworkRunner runner)
    {
        var sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        
        return sceneManager;
    }

    protected virtual Task<StartGameResult> InitializeNetworkRunner
    (
        NetworkRunner runner,
        GameMode gameMode,
        string sessionName,
        string lobbyName,
        byte[] connectionToken,
        NetAddress address,
        SceneRef scene,
        Dictionary<string, SessionProperty> sessionProperties = null
    )
    {
        var sceneManager = GetSceneManager(runner);

        runner.ProvideInput = true;

        return runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            Address = address,
            Scene = scene,
            SessionName = sessionName,
            CustomLobbyName = lobbyName, // có thể dùng để phân loại các chế độ chơi.
            SceneManager = sceneManager,
            ConnectionToken = connectionToken,
            SessionProperties = sessionProperties
        });
    }
    
    protected virtual Task InitializeNetworkRunnerHostMigration
    (
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken
    )
    {
        var sceneManager = GetSceneManager(runner);

        runner.ProvideInput = true;

        return runner.StartGame(new StartGameArgs
        {
            SceneManager = sceneManager,
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            ConnectionToken = GameManager.Instance.BuildConnectionToken(),
            Scene = _lastSceneRef
        });
    }

    void HostMigrationResume(NetworkRunner runner)
    {
        if (GameManager.Instance != null) GameManager.Instance.SetStartHostMigration(true);
        
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> HostMigrationResume Started", this);
        var weaponManagersToResolve = new List<WeaponManager>();

        foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNetworkObject.TryGetBehaviour<NetworkGameState>(out var oldGameState))
            {
                runner.Spawn(resumeNetworkObject,
                    onBeforeSpawned: (runner, newNetworkObject) =>
                    {
                        newNetworkObject.CopyStateFrom(resumeNetworkObject);
                        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> NetworkGameState resumed");
                    });
                continue;
            }
            
            if (resumeNetworkObject.TryGetBehaviour<NetworkCharacterControllerCustom>(out var characterController))
            {
                // Đọc thẳng từ NetworkState thay vì transform
                Vector3 spawnPos = characterController.GetNetworkPosition();
                Quaternion spawnRot = characterController.GetNetworkRotation();

                runner.Spawn(resumeNetworkObject,
                    position: spawnPos,
                    rotation: spawnRot,
                    onBeforeSpawned: (runner, newNetworkObject) =>
                    {
                        newNetworkObject.CopyStateFrom(resumeNetworkObject);

                        if (resumeNetworkObject.TryGetBehaviour<NPlayer>(out var oldNPlayer))
                        {
                            NPlayer newNPlayer = newNetworkObject.GetComponent<NPlayer>();
                            newNPlayer.CopyStateFrom(oldNPlayer);
                        }

                        if (resumeNetworkObject.TryGetBehaviour<NShootHandler>(out var oldNShootHandler))
                        {
                            NShootHandler newNShootHandler = newNetworkObject.GetComponent<NShootHandler>();
                            newNShootHandler.CopyStateFrom(oldNShootHandler);
                        }
                        
                        if (resumeNetworkObject.TryGetBehaviour<NHPHandler>(out NHPHandler oldHPHandler))
                        {
                            NHPHandler newHPHandler = newNetworkObject.GetComponent<NHPHandler>();
                            newHPHandler.CopyStateFrom(oldHPHandler);

                            newHPHandler.SkipSettingStartValues = true;
                            Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> NHPPlayer Backup");
                        }

                        if (resumeNetworkObject.TryGetBehaviour<NCharMovementHandler>(
                                out NCharMovementHandler oldNCharHandler))
                        {
                            NCharMovementHandler newNCharHandler = newNetworkObject.GetComponent<NCharMovementHandler>();
                            newNCharHandler.CopyStateFrom(oldNCharHandler);
                            Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> NCharMovemenHandler Backup");
                        }

                        if (newNetworkObject.TryGetBehaviour<WeaponManager>(out var newWM))
                        {
                            newWM.CopyStateFrom(resumeNetworkObject.GetBehaviour<WeaponManager>());
                            newWM.SkipWeaponSpawn = true;
                            newWM.NeedsResolveAfterMigration = true;

                            // ✅ Collect để resolve sau
                            weaponManagersToResolve.Add(newWM);
                        }

                        if (resumeNetworkObject.TryGetBehaviour<NPlayer>(out var oldNplayer))
                        {
                            // ✅ Nếu object này thuộc về local player (client vừa được promote làm host mới):
                            // restore RawToken từ snapshot vào GameManager
                            // để GetPlayerToken() trong OnPlayerJoined trả về đúng token cũ
                            // thay vì token mới được tạo lúc khởi động
                            if (resumeNetworkObject.StateAuthority == runner.LocalPlayer)
                            {
                                byte[] rawToken = oldNplayer.RawToken.ToArray();

                                bool hasData = false;
                                foreach (byte b in rawToken)
                                {
                                    if (b != 0) { hasData = true; break; }
                                }

                                if (hasData)
                                {
                                    GameManager.Instance.SetConnectionToken(rawToken);
                                    Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> Restored local player token from snapshot, hash: {NConnectionTokenUtils.HashToken(rawToken)}");
                                }
                                else
                                {
                                    Debug.LogWarning("<color=orange>[NetworkRunnerHandler]</color> RawToken from snapshot is empty, keeping current GameManager token.");
                                }
                            }

                            var newNPlayer = newNetworkObject.GetComponent<NPlayer>();
    
                            FindFirstObjectByType<NSpawner>().SetConnectionTokenMapping(
                                oldNplayer.Token,
                                newNPlayer
                            );

                            // ✅ THÊM DÒNG NÀY: bind PlayerRef -> NetworkObject ngay tại đây
                            // Khi OnPlayerJoined chạy sau, TryGetPlayerObject sẽ tìm thấy
                            // và skip spawn placeholder.
                            if (!resumeNetworkObject.InputAuthority.IsNone)
                            {
                                runner.SetPlayerObject(resumeNetworkObject.InputAuthority, newNetworkObject);
                            }
                        }

                        // Sau CopyStateFrom, ép lại position/rotation vào NetworkCCData
                        if (newNetworkObject.TryGetBehaviour<NetworkCharacterControllerCustom>(out var newCC))
                        {
                            newCC.Teleport(spawnPos, spawnRot);
                        }
                    });
            }
        }
        
        foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        {
            if (!resumeNetworkObject.TryGetBehaviour<WeaponController>(out var oldWeaponCtrl))
                continue;

            runner.Spawn(resumeNetworkObject,
                onBeforeSpawned: (runner, newNetworkObject) =>
                {
                    newNetworkObject.CopyStateFrom(resumeNetworkObject);

                    if (newNetworkObject.TryGetBehaviour<WeaponController>(out var newWeaponCtrl))
                    {
                        newWeaponCtrl.CopyStateFrom(oldWeaponCtrl);
                        newWeaponCtrl.OwnerCharacter = oldWeaponCtrl.OwnerCharacter;
                        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> {newWeaponCtrl.transform.name}");
                    }
                });
        }

        foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNetworkObject.TryGetBehaviour<NPlayer>(out var nplayer))
                nplayer.OnHostMigrated();
        }

        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> HostMigrationResume Completed", this);
        
        FindFirstObjectByType<NSpawner>().RecalculateTeamCounts();

        // ✅ Dọn những player không reconnect lại sau host migration (rời game hẳn giữa lúc
        // migration đang diễn ra) - tránh object của họ treo lại vĩnh viễn trong scene.
        // Chờ 1 khoảng trước khi dọn, để tránh dọn nhầm những player đang trên đường reconnect.
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(HostMigrationCleanUpCO(runner));
            StartCoroutine(ResolveWeaponManagersAfterMigration(weaponManagersToResolve));
        }
    }
    
    private IEnumerator ResolveWeaponManagersAfterMigration(List<WeaponManager> managers)
    {
        // Chờ 1 frame để Spawned() của WeaponManager + WeaponController chạy xong
        yield return null;

        foreach (var wm in managers)
        {
            if (wm == null || !wm.Object.IsValid) continue;

            // ✅ RPC broadcast xuống tất cả client thay vì chỉ chạy local
            wm.Rpc_ResolveAfterMigration();
            Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> Rpc_ResolveAfterMigration sent: {wm.name}");
        }
    }

    private IEnumerator HostMigrationCleanUpCO(NetworkRunner runner)
    {
        yield return new WaitForSeconds(_hostMigrationCleanupDelay);

        // Chỉ host mới có quyền dọn (Despawn cần StateAuthority/IsServer).
        if (runner == null || !runner.IsServer) yield break;

        NSpawner spawner = FindFirstObjectByType<NSpawner>();
        if (spawner != null)
        {
            spawner.OnHostMigrationCleanUp();
        }
    }

    public void CreateGame(string sessionName, string lobbyName, string sceneName)
    {
        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> Create session {sessionName}" +
                  $"in lobby {lobbyName} scene {sceneName}");
        
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(SceneName.GetPath(sceneName));
        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        
        var clientTask = CreateGameAsync(sessionName, lobbyName, sceneRef, buildIndex);
    }

    private async Task CreateGameAsync(string sessionName, string lobbyName, SceneRef sceneRef, int buildIndex)
    {
        StartGameResult res;
        try
        {
            res = await InitializeNetworkRunner
            (
                GetNetworkRunner(),
                GameMode.AutoHostOrClient,
                sessionName,
                lobbyName,
                GameManager.Instance.BuildConnectionToken(),
                NetAddress.Any(),
                sceneRef,
                BuildSessionProps(buildIndex) // de nguoi khac tim + vao tran dang dien ra
            );
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NetworkRunnerHandler] CreateGame loi: " + ex.Message);
            SpawnNetworkRunner = null;
            StartGameFailed?.Invoke(ShutdownReason.Error);
            return;
        }
        if (res.Ok) return;
        Debug.LogWarning($"[NetworkRunnerHandler] CreateGame that bai: {res.ShutdownReason}");
        SpawnNetworkRunner = null;
        StartGameFailed?.Invoke(res.ShutdownReason);
    }

    // ===================== LOI KET NOI =====================
    /// <summary>CreateGame (ghep tran PlayFab / solo) that bai -> menu dua nguoi choi ve va bao ly do.</summary>
    public static event Action<ShutdownReason> StartGameFailed;

    /// <summary>Ly do lan vao lobby Fusion gan nhat that bai (null = thanh cong / chua thu).</summary>
    public ShutdownReason? LastLobbyFailReason { get; private set; }

    /// <summary>Het CCU cua goi Photon: moi buoc ket noi tiep deu se bi tu choi -> dung tim tran, khong thu lai.</summary>
    public static bool IsServerFull(string reason) => reason == nameof(ShutdownReason.MaxCcuReached);

    /// <summary>Cau bao loi cho nguoi choi theo ShutdownReason (tieng Viet, LocTable dich khi chon English).</summary>
    public static string DescribeFailure(string reason)
    {
        switch (reason)
        {
            case nameof(ShutdownReason.MaxCcuReached):
                return "Máy chủ đang quá tải (đã đủ số người chơi trực tuyến). Vui lòng thử lại sau ít phút.";
            case nameof(ShutdownReason.GameIsFull):
                return "Phòng đã đầy.";
            case nameof(ShutdownReason.GameNotFound):
            case nameof(ShutdownReason.GameClosed):
                return "Phòng không còn tồn tại.";
            case nameof(ShutdownReason.PhotonCloudTimeout):
            case nameof(ShutdownReason.ConnectionTimeout):
            case nameof(ShutdownReason.ConnectionRefused):
            case nameof(ShutdownReason.OperationTimeout):
                return "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.";
            default:
                return "Không vào được trận. Vui lòng thử lại.";
        }
    }

    // ===================== VAO TRAN DANG DIEN RA =====================
    // PlayFab Matchmaking chot tran ngay khi du MIN nguoi (vd 2) -> nguoi thu 3 tim sau bi dua sang tran khac
    // du tran kia moi co 2 nguoi. Khac phuc o tang Fusion: moi phong cong khai (IsVisible) gan thuoc tinh
    // che do/map/phien ban; nguoi bam "Bat dau" se xem danh sach phong trong lobby Fusion truoc, con cho
    // + cung che do (+ cung map neu khong chon Ngau nhien) thi VAO THANG phong do; khong co moi tao ve PlayFab.
    // Host tu dong DONG phong khi tran sap het / da ket thuc (NetworkGameState).

    /// <summary>Doi so nay khi layout state mang thay doi -> ban build cu khong vao nham phong ban moi.</summary>
    public const int NET_PROTOCOL = 20261025; // [Aemy 2026-10-24] NetworkGameState them phong cho (InLobby / _lobbyTimer); roster "r" + co L/G + avatar
    public const string PROP_MODE = "m", PROP_MAP = "map", PROP_VER = "v";

    public static Dictionary<string, SessionProperty> BuildSessionProps(int mapBuildIndex)
    {
        var gm = GameManager.Instance;
        return new Dictionary<string, SessionProperty>
        {
            { PROP_MODE, (int)(gm != null ? gm.GetGamePlayMode() : GamePlayMode.Solo) },
            { PROP_MAP,  mapBuildIndex },
            { PROP_VER,  NET_PROTOCOL },
            { SessionRoster.PROP, "" }, // [Aemy 2026-10-22] Fusion chi cho UPDATE key co san -> tao rong tu dau, host ghi roster sau
        };
    }

    /// <summary>
    /// Vao lobby Fusion cua che do (Solo/Team) va tra ve danh sach phong dau tien nhan duoc (hoac rong khi het timeout).
    /// Runner van o trong lobby sau do -> danh sach tiep tuc duoc day qua NSpawner.SessionListUpdated.
    /// </summary>
    public void QuerySessions(string lobbyName, float timeoutSeconds, Action<List<SessionInfo>> onList)
    {
        StartCoroutine(QuerySessionsCo(lobbyName, timeoutSeconds, onList));
    }

    // Danh sach phong moi nhat cua lobby dang o (Photon tu day cap nhat khi phong doi).
    public List<SessionInfo> LastSessionList { get; private set; }
    private string _lobbyJoined;
    private bool _sessionHooked;

    private void HookSessionList()
    {
        if (_sessionHooked) return;
        NSpawner.SessionListUpdated += OnSessionList;
        _sessionHooked = true;
    }
    private void OnSessionList(List<SessionInfo> l) { LastSessionList = l != null ? new List<SessionInfo>(l) : new List<SessionInfo>(); }
    private void OnDestroy() { if (_sessionHooked) NSpawner.SessionListUpdated -= OnSessionList; }

    private IEnumerator QuerySessionsCo(string lobbyName, float timeoutSeconds, Action<List<SessionInfo>> onList)
    {
        HookSessionList();
        var runner = GetNetworkRunner();
        bool inLobby = runner.LobbyInfo.IsValid && _lobbyJoined == lobbyName && LastSessionList != null;
        if (inLobby) { if (onList != null) onList(new List<SessionInfo>(LastSessionList)); yield break; }
        if (runner.IsRunning) { if (onList != null) onList(new List<SessionInfo>()); yield break; } // dang trong phong -> khong hoi
        LastSessionList = null;
        LastLobbyFailReason = null;
        _lobbyJoined = lobbyName;
        var join = runner.JoinSessionLobby(SessionLobby.Custom, lobbyName);
        float t = 0f;
        while (LastSessionList == null && t < timeoutSeconds)
        {
            if (join.IsCompleted && (join.IsFaulted || join.IsCanceled || !join.Result.Ok))
            {
                LastLobbyFailReason = join.IsFaulted || join.IsCanceled ? ShutdownReason.Error : join.Result.ShutdownReason;
                Debug.LogWarning($"[NetworkRunnerHandler] Vao lobby [{lobbyName}] that bai: {LastLobbyFailReason}");
                _lobbyJoined = null;
                SpawnNetworkRunner = null; // runner loi da shutdown -> lan sau tao moi
                break;
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (onList != null) onList(LastSessionList != null ? new List<SessionInfo>(LastSessionList) : new List<SessionInfo>());
    }

    /// <summary>Vao phong co san bang GameMode.Client (KHONG tu tao phong moi neu phong vua dong).</summary>
    public void JoinExistingSession(string sessionName, string lobbyName, Action<bool, string> onResult)
    {
        var task = JoinExistingSessionAsync(sessionName, lobbyName, onResult);
    }

    private async Task JoinExistingSessionAsync(string sessionName, string lobbyName, Action<bool, string> onResult)
    {
        var runner = GetNetworkRunner();
        var sceneManager = GetSceneManager(runner);
        runner.ProvideInput = true;
        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> QuickJoin session {sessionName}");
        StartGameResult res;
        try
        {
            res = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                Address = NetAddress.Any(),
                SessionName = sessionName,
                CustomLobbyName = lobbyName,
                SceneManager = sceneManager,
                ConnectionToken = GameManager.Instance.BuildConnectionToken(),
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NetworkRunnerHandler] QuickJoin loi: " + ex.Message);
            SpawnNetworkRunner = null;
            if (onResult != null) onResult(false, ex.Message);
            return;
        }
        if (!res.Ok)
        {
            Debug.LogWarning($"[NetworkRunnerHandler] QuickJoin that bai: {res.ShutdownReason}");
            SpawnNetworkRunner = null;
        }
        if (onResult != null) onResult(res.Ok, res.Ok ? null : res.ShutdownReason.ToString());
    }

    // PHONG TUY CHINH (ma phong): chu phong = GameMode.Host, ban be = GameMode.Client.
    // Session an khoi danh sach phong (IsVisible=false) -> chi ai co ma moi vao duoc, khong ghep nguoi la.
    // Client vao ma khong ton tai -> StartGame tra loi (khong tu tao phong moi nhu AutoHostOrClient).
    public void StartCustomGame(string sessionName, string lobbyName, string sceneName, bool asHost, Action<bool, string> onResult)
    {
        var task = StartCustomGameAsync(sessionName, lobbyName, sceneName, asHost, onResult);
    }

    private async Task StartCustomGameAsync(string sessionName, string lobbyName, string sceneName, bool asHost, Action<bool, string> onResult)
    {
        var runner = GetNetworkRunner();
        var sceneManager = GetSceneManager(runner);
        runner.ProvideInput = true;
        SceneRef sceneRef = SceneRef.None;
        if (asHost)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SceneName.GetPath(sceneName));
            sceneRef = SceneRef.FromIndex(buildIndex);
        }
        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> Custom {(asHost ? "HOST" : "JOIN")} session {sessionName} scene {sceneName}");
        StartGameResult res;
        try
        {
            res = await runner.StartGame(new StartGameArgs
            {
                GameMode = asHost ? GameMode.Host : GameMode.Client,
                Address = NetAddress.Any(),
                Scene = sceneRef,
                SessionName = sessionName,
                CustomLobbyName = lobbyName,
                SceneManager = sceneManager,
                ConnectionToken = GameManager.Instance.BuildConnectionToken(),
                IsVisible = false,
                IsOpen = true,
                PlayerCount = 10,
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NetworkRunnerHandler] Custom StartGame loi: " + ex.Message);
            SpawnNetworkRunner = null;
            if (onResult != null) onResult(false, ex.Message);
            return;
        }
        if (!res.Ok)
        {
            Debug.LogWarning($"[NetworkRunnerHandler] Custom StartGame that bai: {res.ShutdownReason}");
            SpawnNetworkRunner = null; // runner hong tu huy -> lan sau GetNetworkRunner tao moi
        }
        if (onResult != null) onResult(res.Ok, res.Ok ? null : res.ShutdownReason.ToString());
    }

    public void JoinGame(SessionInfo sessionInfo, string lobbyName)
    {
        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> Join Game {sessionInfo.Name}");
        
        //var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

        var clientTask = InitializeNetworkRunner
        (
            GetNetworkRunner(),
            GameMode.AutoHostOrClient,
            sessionInfo.Name,
            lobbyName,
            GameManager.Instance.BuildConnectionToken(),
            NetAddress.Any(),
            SceneRef.None
        );
    }
    
    public void SaveCurrentScene(SceneRef sceneRef)
    {
        _lastSceneRef = sceneRef;
        Debug.Log($"<color=orange>[NetworkRunnerHandler]</color>  {_lastSceneRef}");
    }
    
    public void LeaveRoom()
    {
        var task = LeaveRoomAsync();
    }

    private async Task LeaveRoomAsync()
    {
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> LeaveRoom started.");

        if (SpawnNetworkRunner == null)
        {
            Debug.LogWarning("<color=orange>[NetworkRunnerHandler]</color> NetworkRunner is null.");
            return;
        }

        // ✅ Set flag trước khi shutdown
        PlayerPrefs.SetInt("ReturnToSessionList", 1);
        PlayerPrefs.Save();

        await SpawnNetworkRunner.Shutdown();
        SpawnNetworkRunner = null;

        // Chờ scene load xong hoàn toàn
        var asyncLoad = SceneManager.LoadSceneAsync("MenuShell") /* ve MenuShell thay vi MainMenu_Test */;
        while (!asyncLoad.isDone)
            await Task.Yield();

        SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
        SpawnNetworkRunner.name = "Network Runner";
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> OK");
    }
    
    public void OnJoinLobby(string lobbyName)
    {
        var clientTask = JoinLobby(lobbyName);
    }

    private async Task JoinLobby(string lobbyName)
    {
        if (SpawnNetworkRunner == null)
        {
            SpawnNetworkRunner = GetNetworkRunner();
        }
        
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> JoinLobby started.");

        var result = await SpawnNetworkRunner.JoinSessionLobby(SessionLobby.Custom, lobbyName);
        
        if (!result.Ok)
            Debug.LogError($"<color=orange>[NetworkRunnerHandler]</color> Unable to join lobby [{lobbyName}]: {result}");
        else Debug.Log($"Joined lobby [{lobbyName}]");
    }
    
    public void OnLeaveLobby()
    {
        var clientTask = LeaveLobby();
    }

    private async Task LeaveLobby()
    {
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> LeaveLobby started.");

        if (SpawnNetworkRunner == null)
        {
            Debug.LogWarning("<color=orange>[NetworkRunnerHandler]</color> NetworkRunner is null, cannot leave lobby.");
            return;
        }

        await SpawnNetworkRunner.Shutdown();
        SpawnNetworkRunner = null;
        
        var asyncLoad = SceneManager.LoadSceneAsync("MenuShell") /* ve MenuShell thay vi MainMenu_Test */;
        while (!asyncLoad.isDone)
            await Task.Yield();
        
        // Khởi tạo lại runner mới để sẵn sàng cho lần join sau
        SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
        SpawnNetworkRunner.name = "Network Runner";

        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> Left lobby successfully.");
    }

    public NetworkRunner GetNetworkRunner()
    {
        if (SpawnNetworkRunner == null)
        {
            var oldRunner = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
            if (oldRunner != null) Destroy(oldRunner.gameObject); // null-check tranh NRE
            SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
            SpawnNetworkRunner.name = "Network Runner";
            Debug.Log("<color=orange>[NetworkRunnerHandler]</color> Create New Network Runner");
        }
        
        return SpawnNetworkRunner;
    }
}