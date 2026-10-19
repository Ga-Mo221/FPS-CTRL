using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private class ClientJoinData
    {
        public string CharacterName;
        public string WeaponOne;
        public string WeaponTwo;
        public string GrenadeQ;
        public string GrenadeE;
    }

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
    
    [ReadOnly]
    [SerializeField] private UISessionListHandler _uiSessionListHandler;

    private void Awake()
    {
        Instance = this;
        mapTokenIDWithNPlayer = new Dictionary<int, NPlayer>();
        _uiSessionListHandler = FindAnyObjectByType<UISessionListHandler>(FindObjectsInactive.Include);
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
            _networkGameState = runner.Spawn(_networkGameStatePrefab, onBeforeSpawned: (runner, obj) =>
            {
                var state = obj.GetComponent<NetworkGameState>();

                int killTarget = 10;

                if (GameManager.Instance != null)
                    killTarget = GameManager.Instance.KillTarget;
                
                state.SetKillTarget(killTarget);
                if (GameManager.Instance != null) state.SetRoundMinutes(GameManager.Instance.RoundMinutes); // [Aemy 2026-10-19] phong tuy chinh
            });
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
        
        ClientJoinData joinData = DecodeJoinData(runner.GetPlayerConnectionToken(player));

        NPlayer prefabToSpawn = _playerPrefab;
        if (joinData != null && !string.IsNullOrEmpty(joinData.CharacterName))
        {
            var resolved = GameManager.Instance.GetCharacter(joinData.CharacterName);
            if (resolved != null)
                prefabToSpawn = resolved;
            else
                Debug.LogWarning($"<color=orange>[Spawner]</color> Character '{joinData.CharacterName}' không tìm thấy, dùng mặc định.");
        }

        NPlayer newNplayer = runner.Spawn(
            prefabToSpawn,
            Vector3.up * 200f,
            Quaternion.identity,
            player,
            onBeforeSpawned: (runner, obj) =>
            {
                NPlayer nPlayer = obj.GetComponent<NPlayer>();
                nPlayer.GamePlayMode = GameManager.Instance.GetGamePlayMode();

                if (nPlayer.GamePlayMode == GamePlayMode.Team)
                    nPlayer.NetworkedTeam = AssignTeam();

                WeaponManager weaponManager = obj.GetComponent<WeaponManager>();
                if (weaponManager != null)
                {
                    string w1 = joinData?.WeaponOne;
                    string w2 = joinData?.WeaponTwo;
                    weaponManager.SpawnWeapon(
                        string.IsNullOrEmpty(w1) ? WeaponID._M4 : w1,
                        string.IsNullOrEmpty(w2) ? WeaponID._9MM_DEMON : w2
                    );
                }

                NShootHandler shootHandler = obj.GetComponent<NShootHandler>();
                if (shootHandler != null)
                {
                    string gq = joinData?.GrenadeQ;
                    string ge = joinData?.GrenadeE;
                    shootHandler.SetGrenade(
                        string.IsNullOrEmpty(gq) ? WeaponID._FRAG_M67 : gq,
                        string.IsNullOrEmpty(ge) ? WeaponID._FLASH_M84 : ge
                    );
                }

                if (SpawnManager.Instance != null)
                {
                    NetworkRunner spawnRunner = FindAnyObjectByType<NetworkRunnerHandler>().GetNetworkRunner();
                    SpawnResult spawnResult = SpawnManager.Instance.GetInitialSpawnPoint(spawnRunner, nPlayer);

                    var ncc = obj.GetComponent<NetworkCharacterControllerCustom>();
                    if (ncc != null)
                        ncc.Teleport(spawnResult.Position, spawnResult.Rotation);
                    else
                        obj.transform.SetPositionAndRotation(spawnResult.Position, spawnResult.Rotation);
                }
                else
                {
                    Debug.LogWarning("[NSpawner] SpawnManager not found, using random fallback.");
                    obj.transform.position = Utils.GetRandomSpawnPoint();
                }
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
        FindAnyObjectByType<NetworkRunnerHandler>(FindObjectsInactive.Include).StartHostMigration(hostMigrationToken);
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
    
    // [MM-BACKFILL 2026-10-18 Aemy] Phat danh sach phong cho QuickJoin (vao tran dang dien ra) o MenuShell.
    public static event System.Action<List<SessionInfo>> SessionListUpdated;

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        if (SessionListUpdated != null) SessionListUpdated(sessionList);
        if (_uiSessionListHandler == null) return;

        if (sessionList.Count == 0)
        {
            Debug.Log("[Spawner] Joined lobby no sessions found", this);
            _uiSessionListHandler.OnNoSessionsFound();
        }
        else
        {
            _uiSessionListHandler.ClearList();
            foreach (SessionInfo sessionInfo in sessionList)
            {
                _uiSessionListHandler.AddToList(sessionInfo);
                Debug.Log($"[Spawner] Found session {sessionInfo.Name} player count ({sessionInfo.PlayerCount}/{sessionInfo.MaxPlayers})");
            }
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"<color=orange>[Spawner]</color> Shutdown: {shutdownReason}", this);
        mapTokenIDWithNPlayer.Clear();
        BlueCount = 0;
        RedCount = 0;
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // ✅ Cả host lẫn client đều lưu scene ở đây
        // Dùng SceneManager vì lúc này scene đã load xong hoàn toàn
        Scene activeScene = SceneManager.GetActiveScene();
    
        if (activeScene.name != "MainMenu_Test") // bỏ qua MainMenu
        {
            int buildIndex = activeScene.buildIndex;
            FindAnyObjectByType<NetworkRunnerHandler>(FindObjectsInactive.Include)
                .SaveCurrentScene(SceneRef.FromIndex(buildIndex));
            
            Debug.Log($"<color=orange>[Spawner]</color> Scene saved on load done: {activeScene.name} (index {buildIndex})");
        }
    }
    
    private ClientJoinData DecodeJoinData(byte[] token)
    {
        if (token == null || token.Length < 16) return null;

        try
        {
            using var ms = new System.IO.MemoryStream(token);
            using var br = new System.IO.BinaryReader(ms);

            br.ReadBytes(16); // skip identity

            return new ClientJoinData
            {
                CharacterName = ReadStr(br),
                WeaponOne     = ReadStr(br),
                WeaponTwo     = ReadStr(br),
                GrenadeQ      = ReadStr(br),
                GrenadeE      = ReadStr(br),
            };
        }
        catch
        {
            Debug.LogWarning("<color=orange>[Spawner]</color> Token không có loadout payload hợp lệ, dùng mặc định.");
            return null;
        }
    }
    
    private static string ReadStr(System.IO.BinaryReader br)
    {
        byte len = br.ReadByte();
        return System.Text.Encoding.UTF8.GetString(br.ReadBytes(len));
    }
    
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("<color=orange>[Spawner]</color> Connected to Server", this); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"<color=orange>[Spawner]</color> Disconnected: {reason}", this); }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.LogError($"<color=orange>[Spawner]</color> Connect Failed: {reason}", this); }
    public void OnSceneLoadStart(NetworkRunner runner) { }
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

        // ✅ Duyệt theo object NPlayer thật đang tồn tại trong scene, KHÔNG duyệt theo
        // mapTokenIDWithNPlayer. Dictionary key theo token hash có thể bị trùng/ghi đè
        // (ví dụ khi 2 player cùng có Token = 0 do lỗi đăng ký token trước đó), khiến
        // 1 object bị mất khỏi dictionary hoàn toàn (leak vĩnh viễn, không ai dọn) trong khi
        // object còn lại bị hiểu nhầm là "không reconnect" và bị despawn oan.
        // Duyệt trực tiếp theo NetworkObject.InputAuthority đảm bảo không bỏ sót và
        // không despawn nhầm object đang có chủ.
        var allPlayers = FindObjectsByType<NPlayer>(FindObjectsSortMode.None);

        foreach (var nPlayer in allPlayers)
        {
            if (nPlayer == null || nPlayer.Object == null || !nPlayer.Object.IsValid)
                continue;

            if (!nPlayer.Object.InputAuthority.IsNone)
                continue; // vẫn có chủ, không đụng vào

            Debug.Log($"<color=orange>[Spawner]</color> Player {nPlayer.name} (token {nPlayer.Token}) did not reconnect after host migration. Despawning.");

            if (nPlayer.NetworkedTeam == PlayerTeam.Blue)
            {
                if (BlueCount > 0) BlueCount--;
            }
            else if (nPlayer.NetworkedTeam == PlayerTeam.Red)
            {
                if (RedCount > 0) RedCount--;
            }

            // Dọn mọi entry trong dictionary đang trỏ tới object ma này (phòng trường hợp
            // trùng key khiến nó bị lưu dưới key khác với Token hiện tại của nó).
            var staleKeys = new List<int>();
            foreach (var kv in mapTokenIDWithNPlayer)
            {
                if (kv.Value == nPlayer)
                    staleKeys.Add(kv.Key);
            }
            foreach (var key in staleKeys)
                mapTokenIDWithNPlayer.Remove(key);

            nPlayer.Runner.Despawn(nPlayer.Object);
        }

        Debug.Log("<color=orange>[Spawner]</color> OnHostMigrationCleanUp completed");
    }
}