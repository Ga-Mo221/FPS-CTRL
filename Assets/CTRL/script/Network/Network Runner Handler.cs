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
        if (GameManager.instance.NetworkRunnerHandler != null) Destroy(gameObject);
        else GameManager.instance.NetworkRunnerHandler = this;
        
        NetworkRunner networkRunnerInScene = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
        
        if (networkRunnerInScene != null)
            SpawnNetworkRunner = networkRunnerInScene;
        
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (SpawnNetworkRunner == null)
        {
            SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
            SpawnNetworkRunner.name = "Network Runner";

            if (SceneManager.GetActiveScene().name != "MainMenu_Test")
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
                    GameManager.instance.GetConnectionToken(),
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

    protected virtual Task InitializeNetworkRunner
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
            ConnectionToken = GameManager.instance.GetConnectionToken(),
            Scene = _lastSceneRef
        });
    }

    void HostMigrationResume(NetworkRunner runner)
    {
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
                                    GameManager.instance.SetConnectionToken(rawToken);
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
        
        int buildIndex = SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/Setup/{sceneName}.unity");
        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        
        var clientTask = InitializeNetworkRunner
        (
            GetNetworkRunner(),
            GameMode.AutoHostOrClient,
            sessionName,
            lobbyName,
            GameManager.instance.GetConnectionToken(),
            NetAddress.Any(),
            sceneRef
        );
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
            GameManager.instance.GetConnectionToken(),
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
        var asyncLoad = SceneManager.LoadSceneAsync("MainMenu_Test");
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
        
        var asyncLoad = SceneManager.LoadSceneAsync("MainMenu_Test");
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
            Destroy(oldRunner.gameObject);
            SpawnNetworkRunner = Instantiate(_networkRunner_Prefab);
            SpawnNetworkRunner.name = "Network Runner";
            Debug.Log("<color=orange>[NetworkRunnerHandler]</color> Create New Network Runner");
        }
        
        return SpawnNetworkRunner;
    }
}