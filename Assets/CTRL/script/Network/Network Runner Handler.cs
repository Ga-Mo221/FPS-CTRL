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
            // GameMode = gameMode,
            // Address = address,
            // Scene = scene,
            // SessionName = "TestRoom",
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
                // ✅ Đọc thẳng từ NetworkState thay vì transform
                Vector3 spawnPos = characterController.GetNetworkPosition();
                Quaternion spawnRot = characterController.GetNetworkRotation();

                runner.Spawn(resumeNetworkObject,
                    position: spawnPos,
                    rotation: spawnRot,
                    onBeforeSpawned: (runner, newNetworkObject) =>
                    {
                        // ✅ CopyStateFrom trước
                        newNetworkObject.CopyStateFrom(resumeNetworkObject);
                        
                        if (resumeNetworkObject.TryGetBehaviour<NHPHandler>(out NHPHandler oldHPHandler))
                        {
                            NHPHandler newHPHandler = newNetworkObject.GetComponent<NHPHandler>();
                            newHPHandler.CopyStateFrom(oldHPHandler);
                            
                            // NCharMovementHandler newMovementHandler = newNetworkObject.GetComponent<NCharMovementHandler>();
                            // newMovementHandler.CopyStateFrom(oldHPHandler);

                            //newHPHandler.skipSettingStartValues = true;
                        }

                        if (resumeNetworkObject.TryGetBehaviour<NPlayer>(out var oldNplayer))
                        {
                            FindFirstObjectByType<NSpawner>().SetConnectionTokenMapping(
                                oldNplayer.Token,
                                newNetworkObject.GetComponent<NPlayer>()
                            );
                        }

                        // ✅ Sau CopyStateFrom, ép lại position/rotation vào NetworkCCData
                        if (newNetworkObject.TryGetBehaviour<NetworkCharacterControllerCustom>(out var newCC))
                        {
                            newCC.Teleport(spawnPos, spawnRot);
                        }
                    });
            }
        }

        //StartCoroutine(CleanUpHostMigrationCO());

        Debug.Log("<color=orange>[NetworkRunnerHandler]</color> HostMigrationResume Completed", this);
    }
}
 