using System.Collections.Generic;
using Fusion;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════════════
//  SpawnManager — MonoBehaviour, đặt cùng scene với NSpawner.
//
//  Nhiệm vụ:
//    1. Giữ danh sách SpawnNode (List<Transform>) — kéo thả trong Inspector.
//    2. Giữ DeathRecord list — server ghi lại mỗi khi có player chết.
//    3. Cung cấp API cho NSpawner và NCharMovementHandler gọi.
//
//  QUAN TRỌNG: Chỉ chạy logic spawn trên server (IsServer / HasStateAuthority).
//  Client không gọi EvaluateXxx trực tiếp.
// ══════════════════════════════════════════════════════════════════════════════
[DefaultExecutionOrder(-1000)]

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Nodes — kéo Transform vào theo thứ tự trải đều bản đồ")]
    [SerializeField] private List<Transform> _spawnNodes = new();
    [Header("Team Anchors — điểm spawn đầu tiên của mỗi đội (Team mode)")]
    [SerializeField] private Transform _blueAnchor;
    [SerializeField] private Transform _redAnchor;

    [Header("Wall LayerMask — chọn layer chặn Line-of-Sight (thường là 'Default' hoặc 'Wall')")]
    [SerializeField] private LayerMask _wallMask = ~0;

    [Header("Death heatmap — giữ tối đa N record gần nhất")]
    [SerializeField] private int _maxDeathRecords = 30;

    [Header("Spawn Randomness — độ đa dạng khi random giữa các vị trí spawn hợp lệ")]
    [Tooltip("0 = luôn chọn vị trí điểm cao nhất (gần như không random). " +
             "1 = random gần như đều giữa mọi vị trí hợp lệ, bất kể điểm cao/thấp. " +
             "0.4–0.6 là vùng cân bằng.")]
    [Range(0f, 1f)]
    [SerializeField] private float _spawnRandomness = 0.5f;

    // Server-only, không cần Networked
    private readonly List<DeathRecord> _recentDeaths = new();

    private void Awake()
    {
        Instance = this;
        SpawnEvaluator.WallMask = _wallMask;
        SpawnEvaluator.SpawnRandomness = _spawnRandomness;
        Debug.Log($"<color=green>[SpawnManager]</color> Spawn Evaluator initialized!");
    }

    private void OnValidate()
    {
        // Cho phép chỉnh slider trong Play mode và thấy hiệu lực ngay, không cần restart.
        SpawnEvaluator.SpawnRandomness = _spawnRandomness;
    }

    // ── Public API ────────────────────────────────────────────────────────────
    
    public Transform BlueAnchor => _blueAnchor;
    public Transform RedAnchor  => _redAnchor;

    /// <summary>
    /// Gọi từ NSpawner.OnPlayerJoined() — spawn đầu game / đầu hiệp.
    /// </summary>
    public SpawnResult GetInitialSpawnPoint(NetworkRunner runner, NPlayer player)
    {
        if (_spawnNodes == null || _spawnNodes.Count == 0)
        {
            Debug.LogWarning("<color=green>[SpawnManager]</color> No spawn nodes assigned! Falling back to origin.");
            return new SpawnResult(new Vector3(0, 4, 0), Quaternion.identity);
        }

        return SpawnEvaluator.EvaluateInitialSpawn(runner, player, _spawnNodes, _blueAnchor, _redAnchor);
    }

    /// <summary>
    /// Gọi từ NCharMovementHandler.Respawn() — hồi sinh mid-game.
    /// </summary>
    public SpawnResult GetRespawnPoint(NetworkRunner runner, NPlayer player)
    {
        if (_spawnNodes == null || _spawnNodes.Count == 0)
        {
            Debug.LogWarning("<color=green>[SpawnManager]</color> No spawn nodes assigned! Falling back to origin.");
            return new SpawnResult(new Vector3(0, 4, 0), Quaternion.identity);
        }

        return SpawnEvaluator.EvaluateMidGameRespawn(runner, player, _spawnNodes, _recentDeaths);
    }

    /// <summary>
    /// Gọi từ NHPHandler (hoặc bất kỳ chỗ nào xử lý player death) khi player chết.
    /// Chỉ gọi trên server.
    /// </summary>
    public void RecordDeath(Vector3 position, float simulationTime)
    {
        _recentDeaths.Add(new DeathRecord(position, simulationTime));

        // Giữ list gọn — xóa record cũ nhất khi vượt max
        if (_recentDeaths.Count > _maxDeathRecords)
            _recentDeaths.RemoveAt(0);
    }

    // ── (Optional) Gizmos để debug trong Scene View ───────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_spawnNodes == null) return;
        for (int i = 0; i < _spawnNodes.Count; i++)
        {
            if (_spawnNodes[i] == null) continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_spawnNodes[i].position, 1f);
            // Vẽ forward direction
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_spawnNodes[i].position, _spawnNodes[i].forward * 2f);

            UnityEditor.Handles.Label(
                _spawnNodes[i].position + Vector3.up * 1.5f,
                $"Node {i}");
        }
    }
#endif
}