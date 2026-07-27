using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NPlayer _playerPrefab;
    
    CharacterInputHandler CharacterInputHandler;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Debug.Log("<color=orange>[Spawner]</color> OnPlayerJoined we are Sever. Spawning players...");
            runner.Spawn(_playerPrefab, Utils.GetRandomSpawnPoint(), Quaternion.identity, player);
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
    
    //public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
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
