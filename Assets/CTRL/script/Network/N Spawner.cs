using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NPlayer _playerPrefab;
    [SerializeField] private NetworkGameState _networkGameStatePrefab;
    private NetworkGameState _networkGameState;

    public static NSpawner Instance { get; private set; }

    // map token-hash -> NPlayer "chính thức" (đã được server công nhận) của token đó.
    // Dùng để nhận diện reconnect / resume sau host migration.
    private Dictionary<int, NPlayer> mapTokenIDWithNPlayer;

    CharacterInputHandler CharacterInputHandler;
    
    public byte BlueCount = 0;
    public byte RedCount = 0;

    private void Awake()
    {
        Instance = this;
        mapTokenIDWithNPlayer = new Dictionary<int, NPlayer>();
    }

    public void SetConnectionTokenMapping(int token, NPlayer nPlayer)
    {
        mapTokenIDWithNPlayer[token] = nPlayer;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
        {
            Debug.Log("<color=orange>[Spawner]</color> OnPlayerJoined (client).");
            return;
        }
        
        // Chỉ spawn mới khi chưa có (lần đầu, không phải migration)
        if (_networkGameState == null && NetworkGameState.Instance == null)
        {
            _networkGameState = runner.Spawn(_networkGameStatePrefab);
        }
        else if (_networkGameState == null && NetworkGameState.Instance != null)
        {
            // Resume từ migration đã tạo rồi, chỉ cần lấy reference
            _networkGameState = NetworkGameState.Instance;
        }

        // ✅ QUAN TRỌNG: nếu player này đã có PlayerObject rồi (ví dụ vừa được resume
        // từ snapshot host migration, NPlayer.Spawned() của object resume đã tự gọi
        // Runner.SetPlayerObject), thì KHÔNG spawn thêm bản mới.
        // Trước đây không check điều này -> luôn spawn 1 object placeholder trùng lặp,
        // rồi phải despawn nó ngay giữa lúc Spawned() của chính nó đang chạy dở
        // (RPC token trùng -> despawn) -> gãy toàn bộ chuỗi khởi tạo (WeaponManager,
        // NHPHandler, NCharMovementHandler chưa kịp Spawned() đã bị destroy theo)
        // -> hàng loạt NullReferenceException / InvalidOperationException sau host migration.
        if (runner.TryGetPlayerObject(player, out NetworkObject existingPlayerObj) && existingPlayerObj != null)
        {
            Debug.Log($"<color=orange>[Spawner]</color> OnPlayerJoined: player {player} already has a PlayerObject (resumed/existing). Skip spawn.");
            
            if (existingPlayerObj.TryGetBehaviour<NPlayer>(out var existingNplayer))
            {
                mapTokenIDWithNPlayer[existingNplayer.Token] = existingNplayer;
            }
            return;
        }

        // ✅ Không còn phụ thuộc ConnectionToken ở transport-level (không đáng tin cậy qua Relay).
        // Spawn ngay lập tức; danh tính (token) sẽ được chính client gửi lên qua RPC ngay sau khi Spawned().
        Debug.Log($"<color=orange>[Spawner]</color> OnPlayerJoined we are Server. Spawning placeholder player for {player}");

        NPlayer newNplayer = runner.Spawn(
            _playerPrefab,
            Utils.GetRandomSpawnPoint(),
            Quaternion.identity,
            player,
            onBeforeSpawned: (runner, obj) =>
            {
                // Chạy SAU khi object tạo, TRƯỚC khi Spawned() được gọi
                // => InitPlayer() đọc gamePlayMode đã có giá trị đúng rồi
                NPlayer nPlayer = obj.GetComponent<NPlayer>();
                nPlayer.GamePlayMode = GameManager.instance.GetGamePlayMode();

                // Team mode: server gán team cân bằng
                if (nPlayer.GamePlayMode == GamePlayMode.Team)
                    nPlayer.NetworkedTeam = AssignTeam();
            }
        );
        
        runner.SetPlayerObject(player, newNplayer.Object);
    }
    
    private PlayerTeam AssignTeam()
    {
        // Auto-balance: ai ít hơn thì vào
        if (BlueCount <= RedCount)
        {
            BlueCount++;
            return PlayerTeam.Blue;
        }
        RedCount++;
        return PlayerTeam.Red;
    }

    // ✅ Được gọi từ NPlayer (server-side) khi nhận được Rpc_RegisterToken từ client.
    // Trả về NPlayer "chính thức" nên dùng cho player này (có thể là chính newNplayer,
    // hoặc một NPlayer cũ đã tồn tại từ trước - trường hợp reconnect / resume sau host migration).
    public void ResolvePlayerToken(NetworkRunner runner, NPlayer newNplayer, PlayerRef player, byte[] rawToken)
    {
        if (rawToken == null || rawToken.Length != 16)
        {
            Debug.LogWarning($"<color=orange>[Spawner]</color> ResolvePlayerToken: invalid token from player {player}, keeping as new player.");
            return;
        }

        int hash = NConnectionTokenUtils.HashToken(rawToken);

        if (mapTokenIDWithNPlayer.TryGetValue(hash, out NPlayer existingNplayer)
            && existingNplayer != null
            && existingNplayer != newNplayer)
        {
            // ✅ Token đã tồn tại và trỏ tới 1 NPlayer object khác (còn sống) => đây là reconnect
            // (client cũ rớt mạng rồi join lại, hoặc client vừa resume sau host migration).
            // Chuyển InputAuthority của object CŨ sang player mới, rồi despawn object MỚI vừa spawn thừa.
            Debug.Log($"<color=orange>[Spawner]</color> Token {hash} matches existing NPlayer. Reassigning input authority to player {player} and discarding duplicate.");
            
            existingNplayer.Object.AssignInputAuthority(player);
            runner.SetPlayerObject(player, existingNplayer.Object);
            
            if (newNplayer != null && newNplayer.Object != null && newNplayer.Object != existingNplayer.Object)
            {
                // ⚠️ KHÔNG despawn ngay tại đây. Hàm này thường được gọi từ Rpc_RegisterToken,
                // mà Rpc_RegisterToken lại được gọi ngay trong Spawned() của chính newNplayer
                // (RPC loopback trên host chạy đồng bộ) -> nếu despawn ngay sẽ phá object
                // đang giữa chừng khởi tạo (sibling components như WeaponManager, NHPHandler
                // chưa kịp Spawned()), gây NullReferenceException / InvalidOperationException
                // hàng loạt ngay sau đó. Dời việc despawn sang frame kế tiếp cho an toàn.
                StartCoroutine(DespawnNextFrame(runner, newNplayer.Object));
            }

            mapTokenIDWithNPlayer[hash] = existingNplayer;

            // ✅ Sync lại token cho client, vì input authority vừa được chuyển sang player mới
            // (object cũ đã có RawToken sẵn, nhưng đảm bảo GameManager phía client luôn khớp).
            existingNplayer.TriggerSyncTokenToClient();
            
            StartCoroutine(ResumeAfterAuthorityAssigned(existingNplayer));
        }
        else
        {
            // ✅ Token mới (hoặc trỏ tới chính newNplayer) => đây thực sự là player mới.
            newNplayer.Token = hash;

            for (int i = 0; i < 16; i++)
                newNplayer.RawToken.Set(i, rawToken[i]);

            mapTokenIDWithNPlayer[hash] = newNplayer;

            Debug.Log($"<color=orange>[Spawner]</color> New player registered, token hash: {hash}");

            // ✅ RawToken vừa được ghi xong -> giờ mới sync về client là an toàn (không còn rỗng).
            newNplayer.TriggerSyncTokenToClient();
            //StartCoroutine(ResumeAfterAuthorityAssigned(existingNplayer));
        }
    }
    
    private IEnumerator ResumeAfterAuthorityAssigned(NPlayer nPlayer)
    {
        yield return null;
    
        if (nPlayer != null && nPlayer.Object.IsValid)
        {
            nPlayer.Rpc_OnHostMigrated();
        }
    }

    private IEnumerator DespawnNextFrame(NetworkRunner runner, NetworkObject obj)
    {
        yield return null; // chờ 1 frame để Spawned() (và các sibling component) unwind xong hoàn toàn
        if (obj != null)
        {
            runner.Despawn(obj);
        }
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
    
    public void RecalculateTeamCounts()
    {
        BlueCount = 0;
        RedCount = 0;

        foreach (var kvp in mapTokenIDWithNPlayer)
        {
            NPlayer np = kvp.Value;
            if (np == null || np.Object == null || !np.Object.IsValid) continue;

            if (np.NetworkedTeam == PlayerTeam.Blue)
                BlueCount++;
            else if (np.NetworkedTeam == PlayerTeam.Red)
                RedCount++;
        }

        Debug.Log($"<color=orange>[Spawner]</color> RecalculateTeamCounts: Blue={BlueCount}, Red={RedCount}");
    }
    
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("<color=orange>[Spawner]</color> Connected to Server", this); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"<color=orange>[Spawner]</color> Disconnected: {reason}", this); }
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

    // ✅ Được NetworkRunnerHandler gọi (sau một khoảng chờ) mỗi khi host migration resume xong.
    // Dọn những NPlayer đã resume từ snapshot cũ nhưng chủ nhân của nó KHÔNG reconnect lại
    // trong session mới (rời game hẳn trong lúc host migration đang diễn ra).
    // Không có bước này, object của họ sẽ treo lại vĩnh viễn trong scene.
    public void OnHostMigrationCleanUp()
    {
        Debug.Log("<color=orange>[Spawner]</color> OnHostMigrationCleanUp started");

        // Copy ra danh sách trước khi duyệt, vì Despawn có thể ảnh hưởng tới dictionary
        // nếu có logic khác (OnDestroy, callback...) chạy xen vào trong lúc duyệt.
        var entries = new List<KeyValuePair<int, NPlayer>>(mapTokenIDWithNPlayer);

        foreach (var entry in entries)
        {
            NPlayer nPlayer = entry.Value;
            if (nPlayer == null || nPlayer.Object == null || !nPlayer.Object.IsValid)
                continue;

            if (nPlayer.Object.InputAuthority.IsNone)
            {
                Debug.Log($"<color=orange>[Spawner]</color> Player with token {entry.Key} did not reconnect after host migration. Despawning.");
                
                if (nPlayer.NetworkedTeam == PlayerTeam.Blue)
                {
                    if (BlueCount > 0) BlueCount--;
                }
                else if (nPlayer.NetworkedTeam == PlayerTeam.Red)
                {
                    if (RedCount > 0) RedCount--;
                }
                
                nPlayer.Runner.Despawn(nPlayer.Object);
                mapTokenIDWithNPlayer.Remove(entry.Key);
            }
        }

        Debug.Log("<color=orange>[Spawner]</color> OnHostMigrationCleanUp completed");
    }
}