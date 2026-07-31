using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NPlayer _playerPrefab;

    // list kiem tra nguoi choi da ton tai trong room chua de phuc vu cho viec chuyen doi host
    private Dictionary<int, NPlayer> mapTokenIDWithNPlayer;
    
    CharacterInputHandler CharacterInputHandler;

    private void Awake()
    {
        mapTokenIDWithNPlayer = new Dictionary<int, NPlayer>();
    }

    private int GetPlayerToken(NetworkRunner runner, PlayerRef player)
    {
        if (runner.LocalPlayer == player)
        {
            return NConnectionTokenUtils.HashToken(GameManager.instance.GetConnectionToken());
        }
        
        var token = runner.GetPlayerConnectionToken(player);

        if (token != null)
            return NConnectionTokenUtils.HashToken(token);
        
        Debug.LogWarning("<color=orange>[Spawner]</color> GetPlayerToken returned invalid token");
        return 0;
    }

    public void SetConnectionTokenMapping(int token, NPlayer nPlayer)
    {
        mapTokenIDWithNPlayer.Add(token, nPlayer);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            int playerToken = GetPlayerToken(runner, player);
            Debug.Log($"<color=orange>[Spawner]</color> OnPlayerJoined we are Sever. Connection token : {playerToken}");

            if (mapTokenIDWithNPlayer.TryGetValue(playerToken, out NPlayer nPlayer))
            {
                Debug.Log($"Found old connection token for token {playerToken}. Assigning controllers to that player.");
                nPlayer.GetComponent<NetworkObject>().AssignInputAuthority(player);
                nPlayer.Spawned();
                return;
            }
            
            Debug.Log($"Spawning new player for connection token {playerToken}");
            NPlayer newNplayer = runner.Spawn(_playerPrefab, Utils.GetRandomSpawnPoint(), Quaternion.identity, player);
            
            newNplayer.Token = playerToken;
            mapTokenIDWithNPlayer[playerToken] = newNplayer;
        }
        else Debug.Log("<color=orange>[Spawner]</color> OnPlayerJoined.");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (CharacterInputHandler == null && NPlayer.Local != null)
            CharacterInputHandler = NPlayer.Local.GetComponent<CharacterInputHandler>();

        if (CharacterInputHandler != null)
        {
            input.Set(CharacterInputHandler.GetNetworkInput());
            CharacterInputHandler.ResetButton();
        }
    }

    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("<color=orange>[Spawner]</color> On Host Migration");

        await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
        FindAnyObjectByType<NetworkRunnerHandler>().StartHostMigration(hostMigrationToken);
    }
    //public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner)    { Debug.Log("<color=orange>[Spawner]</color> Connected to Server", this); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"<color=orange>[Spawner]</color> Disconnected: {reason}", this); }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"<color=orange>[Spawner]</color> Connect Failed: {reason}", this); }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { Debug.Log($"<color=orange>[Spawner]</color> Shutdown: {shutdownReason}", this); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
