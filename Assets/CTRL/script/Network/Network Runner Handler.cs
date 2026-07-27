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
        
        var clientTask = InitializeNetworkRunner(_networkRunner, GameMode.AutoHostOrClient, NetAddress.Any(), scene);
        Debug.Log("<color=orange>Network Runner Started.</color>");
    }

    protected virtual Task InitializeNetworkRunner
    (
        NetworkRunner runner,
        GameMode gameMode,
        NetAddress address,
        SceneRef scene
    )
    {
        var sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        runner.ProvideInput = true;

        return runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            Address = address,
            Scene = scene,
            SessionName = "TestRoom",
            SceneManager = sceneManager
        });
    }
}
 