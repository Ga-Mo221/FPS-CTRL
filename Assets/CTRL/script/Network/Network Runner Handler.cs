using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour
{
    [SerializeField] private NetworkRunner _networkRunner_Prefab;
    private NetworkRunner _networkRunner;

    // ✅ Thời gian chờ (giây) sau khi host migration resume xong, để những player
    // chưa kịp reconnect (đang trên đường kết nối lại) có cơ hội quay về đúng object cũ
    // trước khi bị coi là "không quay lại" và bị dọn.
    [SerializeField] private float _hostMigrationCleanupDelay = 5f;


    private void Start()
    {
        _networkRunner = Instantiate(_networkRunner_Prefab);
        _networkRunner.name = "Network Runner";

        // lấy scene hiện tại rồi chuyển sang sceneref bằng convert của fusion.
        Scene UnityScene = SceneManager.GetActiveScene();
        SceneRef scene = SceneRef.FromIndex(UnityScene.buildIndex);
        
        var clientTask = InitializeNetworkRunner
        (
            _networkRunner, 
            GameMode.AutoHostOrClient, 
            GameManager.instance.GetConnectionToken(),
            NetAddress.Any(), 
            scene
        );
        Debug.Log("<color=orange>Network Runner Started.</color>");
    }

    public void StartHostMigration(HostMigrationToken hostMigrationToken)
    {
        _networkRunner = Instantiate(_networkRunner_Prefab);
        _networkRunner.name = "Network Runner - Migration";
        
        var clientTask = InitializeNetworkRunnerHostMigration(_networkRunner, hostMigrationToken);
        Debug.Log("<color=orange>Host Migration Start.</color>");
    }

    INetworkSceneManager GetSceneManager(NetworkRunner runner)
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
        byte[] connectionToken,
        NetAddress address,
        SceneRef scene
    )
    {
        var sceneManager = GetSceneManager(runner);

        runner.ProvideInput = true;

        return runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            Address = address,
            Scene = scene,
            SessionName = "TestRoom",
            SceneManager = sceneManager,
            ConnectionToken = connectionToken
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
            ConnectionToken = GameManager.instance.GetConnectionToken()
        });
    }

    void HostMigrationResume(NetworkRunner runner)
    {
        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> HostMigrationResume Started", this);

        foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        {
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

                        if (resumeNetworkObject.TryGetBehaviour<WeaponManager>(out WeaponManager oldWeaponManager))
                        {
                            WeaponManager newWeaponManager = newNetworkObject.GetComponent<WeaponManager>();
                            newWeaponManager.CopyStateFrom(oldWeaponManager);
                        
                            //newWeaponManager.SkipWeaponSpawn = true;
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
        
        // foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        // {
        //     if (!resumeNetworkObject.TryGetBehaviour<WeaponController>(out var oldWeaponCtrl))
        //         continue;
        //
        //     runner.Spawn(resumeNetworkObject,
        //         onBeforeSpawned: (runner, newNetworkObject) =>
        //         {
        //             newNetworkObject.CopyStateFrom(resumeNetworkObject);
        //
        //             if (newNetworkObject.TryGetBehaviour<WeaponController>(out var newWeaponCtrl))
        //             {
        //                 newWeaponCtrl.CopyStateFrom(oldWeaponCtrl);
        //             
        //                 // ✅ Restore OwnerCharacter - quan trọng vì đây là reference
        //                 // sang NPlayer object, cần remap sang object mới sau migration
        //                 newWeaponCtrl.OwnerCharacter = oldWeaponCtrl.OwnerCharacter;
        //                 newWeaponCtrl.SkipRespawnBullet = true;
        //             
        //                 Debug.Log($"<color=orange>[NetworkRunnerHandler]</color> WeaponController resumed: {resumeNetworkObject.name}");
        //             }
        //         });
        // }

        foreach (var resumeNetworkObject in runner.GetResumeSnapshotNetworkObjects())
        {
            if (resumeNetworkObject.TryGetBehaviour<NPlayer>(out var nplayer))
                nplayer.OnHostMigrated();
        }

        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> HostMigrationResume Completed", this);

        // ✅ Dọn những player không reconnect lại sau host migration (rời game hẳn giữa lúc
        // migration đang diễn ra) - tránh object của họ treo lại vĩnh viễn trong scene.
        // Chờ 1 khoảng trước khi dọn, để tránh dọn nhầm những player đang trên đường reconnect.
        StartCoroutine(HostMigrationCleanUpCO(runner));
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
}