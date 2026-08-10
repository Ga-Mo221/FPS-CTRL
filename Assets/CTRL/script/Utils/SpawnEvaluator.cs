using System.Collections.Generic;
using Fusion;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════════════
//  SpawnEvaluator — Server-only utility
//  Chạy HOÀN TOÀN trên StateAuthority (server/host).
//  Không có NetworkBehaviour, không tốn bandwidth.
//
//  Trả về SpawnResult gồm:
//    • Position  — vị trí teleport
//    • Rotation  — rotation từ Transform của SpawnNode (dùng cho _ncc.Teleport)
// ══════════════════════════════════════════════════════════════════════════════

public readonly struct SpawnResult
{
    public readonly Vector3   Position;
    public readonly Quaternion Rotation;

    public SpawnResult(Vector3 pos, Quaternion rot)
    {
        Position = pos;
        Rotation = rot;
    }
}

public static class SpawnEvaluator
{
    // ── Tuning ───────────────────────────────────────────────────────────────

    // Solo mode
    private const float SOLO_ENEMY_LOS_DISQUALIFY_DIST = 9999f; // LoS luôn disqualify
    private const float SOLO_ENEMY_PENALTY              = -200f;
    private const float SOLO_ENEMY_RADIUS               = 25f;   // unit: Unity world units
    private const float SOLO_DEATH_PENALTY              = -120f;
    private const float SOLO_DEATH_RADIUS               = 15f;
    private const float SOLO_DEATH_WINDOW_SEC           = 10f;

    // Team mode — mid-game respawn
    private const float TEAM_ENEMY_PENALTY              = -200f;
    private const float TEAM_ENEMY_RADIUS               = 25f;
    private const float TEAM_ANCHOR_BONUS               = 150f;  // thưởng gần đồng đội
    private const float TEAM_ANCHOR_RADIUS              = 30f;
    private const float TEAM_DEATH_PENALTY              = -100f;
    private const float TEAM_DEATH_RADIUS               = 12f;
    private const float TEAM_DEATH_WINDOW_SEC           = 8f;

    // Initial team spawn (đầu game / đầu hiệp)
    // Khoảng cách tối thiểu giữa 2 cluster team (world units)
    private const float INITIAL_TEAM_MIN_SEPARATION     = 30f;

    // Độ "gắt" khi random giữa các candidate hợp lệ, dựa trên score (weighted random).
    // 0f  = luôn chọn node có score cao nhất (gần như không random)
    // 1f  = random gần như đều bất kể score cao/thấp (miễn hợp lệ)
    // 0.4f–0.6f là vùng cân bằng: vẫn ưu tiên node tốt nhưng đủ đa dạng, tránh lặp y hệt mỗi round.
    // Gán từ SpawnManager (Inspector) trong Awake() — không phải const để có thể chỉnh runtime.
    public static float SpawnRandomness = 0.5f;

    // Wall LayerMask — gán trong Inspector qua SpawnManager, hoặc dùng "Default"
    // Chỉnh lại nếu wall layer của bạn có tên khác
    public static LayerMask WallMask = ~0; // default: tất cả layer chặn LoS

    // ── Entry points ─────────────────────────────────────────────────────────

    /// <summary>
    /// MID-GAME RESPAWN — dùng khi player chết và hồi sinh trong hiệp.
    /// Gọi từ NCharMovementHandler.Respawn() thay cho Utils.GetRandomSpawnPoint().
    /// </summary>
    public static SpawnResult EvaluateMidGameRespawn(
        NetworkRunner        runner,
        NPlayer              spawningPlayer,
        List<Transform>      spawnNodes,
        List<DeathRecord>    recentDeaths)
    {
        bool isTeamMode = spawningPlayer.GamePlayMode == GamePlayMode.Team;

        return isTeamMode
            ? EvaluateTeamRespawn(runner, spawningPlayer, spawnNodes, recentDeaths)
            : EvaluateSoloRespawn(runner, spawningPlayer, spawnNodes, recentDeaths);
    }

    /// <summary>
    /// INITIAL SPAWN — dùng khi player join vào đầu game hoặc đầu hiệp mới.
    /// Gọi từ NSpawner.OnPlayerJoined() và RPC_RespawnAllPlayers().
    /// </summary>
    // Entry point — thêm 2 param anchor
    public static SpawnResult EvaluateInitialSpawn(
        NetworkRunner   runner,
        NPlayer         spawningPlayer,
        List<Transform> spawnNodes,
        Transform       blueAnchor = null,   // nullable — nếu null thì fallback tự tìm
        Transform       redAnchor  = null)
    {
        bool isTeamMode = spawningPlayer.GamePlayMode == GamePlayMode.Team;

        Debug.Log($"<color=green>[SpawnManager]</color> Player {spawningPlayer.Nickname} la Solo {!isTeamMode}");
        return isTeamMode
            ? EvaluateInitialTeamSpawn(runner, spawningPlayer, spawnNodes, blueAnchor, redAnchor)
            : EvaluateInitialSoloSpawn(runner, spawningPlayer, spawnNodes);
    }

    // ── Solo respawn ─────────────────────────────────────────────────────────

    // Solo: không có khái niệm đồng đội → chỉ tránh địch và vùng vừa chết.
    // Ưu tiên: (1) không bị địch nhìn thấy, (2) xa địch nhất có thể.
    private static SpawnResult EvaluateSoloRespawn(
        NetworkRunner     runner,
        NPlayer           spawningPlayer,
        List<Transform>   spawnNodes,
        List<DeathRecord> recentDeaths)
    {
        var enemies = GatherAliveEnemies(runner, spawningPlayer);

        var candidates = new List<(Transform node, float score)>();

        foreach (var node in spawnNodes)
        {
            if (node == null) continue;

            // Hard cut 1: occupied
            if (IsOccupied(node.position, runner, spawningPlayer)) continue;

            // Hard cut 2: Line-of-Sight từ bất kỳ địch nào
            if (AnyEnemyHasLoS(node.position, enemies)) continue;

            float score = 0f;

            // Distance penalty
            foreach (var e in enemies)
            {
                float dist = Vector3.Distance(node.position, e.position);
                if (dist < SOLO_ENEMY_RADIUS)
                    score += SOLO_ENEMY_PENALTY * (1f - dist / SOLO_ENEMY_RADIUS);
            }

            // Death heatmap penalty
            score += DeathHeatmapPenalty(node.position, recentDeaths,
                                         SOLO_DEATH_PENALTY, SOLO_DEATH_RADIUS,
                                         SOLO_DEATH_WINDOW_SEC,
                                         runner.SimulationTime);

            candidates.Add((node, score));
        }

        // Không có candidate hợp lệ nào (mọi node đều bị occupy hoặc bị LoS)
        // → fallback: xa địch nhất, bỏ qua LoS
        if (candidates.Count == 0)
        {
            var fallbackNode = FallbackFarthestFromEnemies(spawnNodes, enemies);
            return ToResult(fallbackNode, spawnNodes);
        }

        // Weighted random trên toàn bộ candidate hợp lệ — tránh việc luôn hồi sinh
        // đúng 1 node điểm cao nhất khi vị trí địch không đổi giữa các lần chết.
        var chosen = WeightedRandomPick(candidates);
        return ToResult(chosen, spawnNodes);
    }

    // Solo initial: tìm các node chưa bị player khác đứng, random trong top-N xa nhất.
    // Đảm bảo không 2 player spawn cùng điểm, đồng thời có yếu tố ngẫu nhiên.
    private static SpawnResult EvaluateInitialSoloSpawn(
        NetworkRunner   runner,
        NPlayer         spawningPlayer,
        List<Transform> spawnNodes)
    {
        if (spawnNodes.Count == 0)
            return new SpawnResult(new Vector3(0, 4, 0), Quaternion.identity);

        // Thu thập vị trí các player đã spawn (trừ chính mình)
        var occupiedPositions = new List<Vector3>();
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            var np = obj.GetComponent<NPlayer>();
            if (np == null || np == spawningPlayer) continue;
            occupiedPositions.Add(obj.transform.position);
        }

        // Tính score cho mỗi node: càng xa tất cả player đã spawn càng tốt
        var candidates = new List<(Transform node, float score)>();
        foreach (var node in spawnNodes)
        {
            if (node == null) continue;

            // Hard cut: node quá gần player đang đứng
            if (IsOccupied(node.position, runner, spawningPlayer)) continue;

            float minDist = float.MaxValue;
            foreach (var pos in occupiedPositions)
                minDist = Mathf.Min(minDist, Vector3.Distance(node.position, pos));

            // Nếu chưa ai spawn thì mọi node đều hợp lệ với score 0
            float score = occupiedPositions.Count == 0 ? 0f : minDist;
            candidates.Add((node, score));
        }

        // Fallback nếu tất cả node đều bị chiếm
        if (candidates.Count == 0)
        {
            var fallback = spawnNodes[Random.Range(0, spawnNodes.Count)];
            return ToResult(fallback, spawnNodes);
        }

        // Weighted random trên toàn bộ candidate hợp lệ (không chỉ top-3 xa nhất)
        var chosen = WeightedRandomPick(candidates);
        return ToResult(chosen, spawnNodes);
    }

    // ── Team respawn ─────────────────────────────────────────────────────────

    // Team mid-game: gần đồng đội, xa địch, không bị nhìn thấy.
    private static SpawnResult EvaluateTeamRespawn(
        NetworkRunner     runner,
        NPlayer           spawningPlayer,
        List<Transform>   spawnNodes,
        List<DeathRecord> recentDeaths)
    {
        var enemies    = GatherAliveEnemies(runner, spawningPlayer);
        var teammates  = GatherAliveTeammates(runner, spawningPlayer);

        var candidates = new List<(Transform node, float score)>();

        foreach (var node in spawnNodes)
        {
            if (node == null) continue;
            if (IsOccupied(node.position, runner, spawningPlayer)) continue;
            if (AnyEnemyHasLoS(node.position, enemies)) continue;

            float score = 0f;

            // Enemy distance penalty
            foreach (var e in enemies)
            {
                float dist = Vector3.Distance(node.position, e.position);
                if (dist < TEAM_ENEMY_RADIUS)
                    score += TEAM_ENEMY_PENALTY * (1f - dist / TEAM_ENEMY_RADIUS);
            }

            // Teammate anchor bonus — kéo về phía đồng đội
            foreach (var t in teammates)
            {
                float dist = Vector3.Distance(node.position, t.position);
                if (dist < TEAM_ANCHOR_RADIUS)
                    score += TEAM_ANCHOR_BONUS * (1f - dist / TEAM_ANCHOR_RADIUS);
            }

            // Death heatmap penalty
            score += DeathHeatmapPenalty(node.position, recentDeaths,
                                         TEAM_DEATH_PENALTY, TEAM_DEATH_RADIUS,
                                         TEAM_DEATH_WINDOW_SEC,
                                         runner.SimulationTime);

            candidates.Add((node, score));
        }

        if (candidates.Count == 0)
        {
            var fallbackNode = FallbackFarthestFromEnemies(spawnNodes, enemies);
            return ToResult(fallbackNode, spawnNodes);
        }

        var chosen = WeightedRandomPick(candidates);
        return ToResult(chosen, spawnNodes);
    }

    // Team initial: 2 team chia 2 cụm node cách xa nhau nhất có thể.
    // Thuật toán:
    //   1. Tìm cặp node (A, B) có khoảng cách xa nhất → đây là "anchor" của 2 team.
    //   2. Team Blue cluster = tất cả node gần anchor A hơn anchor B.
    //   3. Team Red  cluster = tất cả node gần anchor B hơn anchor A.
    //   4. Mỗi player được assign node tiếp theo trong cluster của team mình.
    private static SpawnResult EvaluateInitialTeamSpawn(
        NetworkRunner   runner,
        NPlayer         spawningPlayer,
        List<Transform> spawnNodes,
        Transform       blueAnchor,
        Transform       redAnchor)
    {
        if (spawnNodes.Count == 0)
            return new SpawnResult(new Vector3(0, 4, 0), Quaternion.identity);

        bool isBlue = spawningPlayer.NetworkedTeam == PlayerTeam.Blue;

        // ── Player đầu tiên của mỗi đội → spawn thẳng vào anchor ─────────────
        int teamSpawnedCount = CountTeamAlreadySpawned(runner, spawningPlayer.NetworkedTeam, spawningPlayer);

        if (teamSpawnedCount == 0)
        {
            // Ưu tiên anchor được chỉ định trong Inspector
            Transform anchor = isBlue ? blueAnchor : redAnchor;
            if (anchor != null)
                return ToResult(anchor, spawnNodes);
            
            // Nếu anchor chưa được gán → fallback tự tìm như cũ
            Debug.LogWarning($"[SpawnEvaluator] {(isBlue ? "Blue" : "Red")}Anchor chưa được gán trong Inspector, dùng fallback.");
        }

        // ── Các player tiếp theo → cluster như cũ ────────────────────────────
        // Bước 1: xác định posA / posB để phân cluster
        // Ưu tiên dùng anchor nếu có, nếu không thì tìm cặp xa nhất
        Vector3 posA, posB;

        if (blueAnchor != null && redAnchor != null)
        {
            posA = blueAnchor.position;
            posB = redAnchor.position;
        }
        else
        {
            // Fallback: tìm cặp node xa nhất (logic cũ)
            int anchorA = 0, anchorB = 1;
            float maxDist = 0f;
            for (int i = 0; i < spawnNodes.Count; i++)
            {
                for (int j = i + 1; j < spawnNodes.Count; j++)
                {
                    float d = Vector3.Distance(spawnNodes[i].position, spawnNodes[j].position);
                    if (d > maxDist) { maxDist = d; anchorA = i; anchorB = j; }
                }
            }
            posA = spawnNodes[anchorA].position;
            posB = spawnNodes[anchorB].position;
        }

        // Bước 2: phân cluster
        var blueCluster = new List<Transform>();
        var redCluster  = new List<Transform>();

        foreach (var node in spawnNodes)
        {
            if (node == null) continue;
            float dA = Vector3.Distance(node.position, posA);
            float dB = Vector3.Distance(node.position, posB);
            if (dA <= dB) blueCluster.Add(node);
            else          redCluster.Add(node);
        }

        if (blueCluster.Count == 0) blueCluster.Add(spawnNodes[0]);
        if (redCluster.Count  == 0) redCluster.Add(spawnNodes[spawnNodes.Count - 1]);

        // Bước 3: lấy node trong cluster của team, tránh node đang bị đứng,
        // random trong top-N xa nhất so với đồng đội đã spawn.
        var cluster = isBlue ? blueCluster : redCluster;

        // Thu thập vị trí đồng đội đã spawn để chọn node tản ra
        var teammatePositions = new List<Vector3>();
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            var np = obj.GetComponent<NPlayer>();
            if (np == null || np == spawningPlayer) continue;
            if (np.NetworkedTeam != spawningPlayer.NetworkedTeam) continue;
            teammatePositions.Add(obj.transform.position);
        }

        var candidates = new List<(Transform node, float score)>();
        foreach (var node in cluster)
        {
            if (node == null) continue;
            if (IsOccupied(node.position, runner, spawningPlayer)) continue;

            float minDist = float.MaxValue;
            foreach (var pos in teammatePositions)
                minDist = Mathf.Min(minDist, Vector3.Distance(node.position, pos));

            float score = teammatePositions.Count == 0 ? 0f : minDist;
            candidates.Add((node, score));
        }

        // Fallback nếu cluster bị chiếm hết
        if (candidates.Count == 0)
        {
            var fallback = cluster[Random.Range(0, cluster.Count)];
            return ToResult(fallback, spawnNodes);
        }

        var chosen = WeightedRandomPick(candidates);
        return ToResult(chosen, spawnNodes);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Chọn ngẫu nhiên có trọng số trong toàn bộ candidate hợp lệ, thay vì
    /// giới hạn cứng vào top-N theo 1 tiêu chí (ví dụ "xa nhất"). Trọng số tỉ lệ
    /// theo score: node điểm cao hơn có xác suất được chọn cao hơn, nhưng mọi
    /// node hợp lệ đều có cơ hội — tránh việc lặp lại vài điểm spawn quen thuộc
    /// mỗi round. Điều chỉnh độ "gắt" qua SpawnRandomness (set từ SpawnManager Inspector).
    /// </summary>
    private static Transform WeightedRandomPick(List<(Transform node, float score)> candidates)
    {
        if (candidates.Count == 1) return candidates[0].node;

        float minScore = float.MaxValue;
        float maxScore = float.MinValue;
        foreach (var c in candidates)
        {
            if (c.score < minScore) minScore = c.score;
            if (c.score > maxScore) maxScore = c.score;
        }

        float range = maxScore - minScore;

        // Tất cả candidate có score gần bằng nhau → không có lý do gì để thiên vị
        // node nào, random đều cho nhanh.
        if (range < 0.0001f)
            return candidates[Random.Range(0, candidates.Count)].node;

        // Temperature: SpawnRandomness càng cao → exponent càng nhỏ → các trọng số
        // càng gần nhau → random càng đều. SpawnRandomness = 0 → exponent = 1
        // (weighted thuần theo score, gần như luôn chọn node tốt nhất khi range lớn).
        float exponent = Mathf.Max(0.01f, 1f - Mathf.Clamp01(SpawnRandomness));

        float totalWeight = 0f;
        var weights = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            float normalized = (candidates[i].score - minScore) / range; // [0,1]
            // +0.05f để node tệ nhất vẫn có trọng số > 0 (không bao giờ bị loại tuyệt đối)
            float weight = Mathf.Pow(normalized + 0.05f, exponent);
            weights[i] = weight;
            totalWeight += weight;
        }

        float r = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative) return candidates[i].node;
        }

        // An toàn: sai số float, trả về phần tử cuối
        return candidates[candidates.Count - 1].node;
    }

    private static List<Transform> GatherAliveEnemies(NetworkRunner runner, NPlayer self)
    {
        var result = new List<Transform>();
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            var np = obj.GetComponent<NPlayer>();
            if (np == null || np == self) continue;

            bool isEnemy = self.GamePlayMode == GamePlayMode.Solo
                ? true  // solo: tất cả là địch
                : np.NetworkedTeam != self.NetworkedTeam;

            if (!isEnemy) continue;

            var nhpHandler = obj.GetComponent<NHPHandler>();
            // Bỏ qua địch đã chết (nếu có IsAlive property — thêm sau)
            if (nhpHandler != null && nhpHandler.IsDead) continue; // xem ghi chú bên dưới *

            result.Add(obj.transform);
        }
        return result;
    }

    private static List<Transform> GatherAliveTeammates(NetworkRunner runner, NPlayer self)
    {
        var result = new List<Transform>();
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            var np = obj.GetComponent<NPlayer>();
            if (np == null || np == self) continue;
            if (np.NetworkedTeam != self.NetworkedTeam) continue;

            var nhpHandler = obj.GetComponent<NHPHandler>();
            if (nhpHandler != null && nhpHandler.IsDead) continue;

            result.Add(obj.transform);
        }
        return result;
    }

    private static bool IsOccupied(Vector3 pos, NetworkRunner runner, NPlayer self)
    {
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            if (obj.GetComponent<NPlayer>() == self) continue;
            if (Vector3.Distance(pos, obj.transform.position) < 2f) return true;
        }
        return false;
    }

    private static bool AnyEnemyHasLoS(Vector3 nodePos, List<Transform> enemies)
    {
        // Offset lên cao một chút để tránh ground clipping
        Vector3 nodeEye = nodePos + Vector3.up * 1.6f;
        foreach (var e in enemies)
        {
            Vector3 enemyEye = e.position + Vector3.up * 1.6f;
            if (!Physics.Linecast(nodeEye, enemyEye, WallMask))
                return true; // không bị vật cản nào chặn → địch thấy được
        }
        return false;
    }

    private static float DeathHeatmapPenalty(
        Vector3          nodePos,
        List<DeathRecord> deaths,
        float            penalty,
        float            radius,
        float            windowSec,
        float simulationTime)
    {
        if (deaths == null) return 0f;
        float score = 0f;
        float now   = simulationTime;
        foreach (var d in deaths)
        {
            float age = now - d.Timestamp;
            if (age > windowSec) continue;
            float dist = Vector3.Distance(nodePos, d.Position);
            if (dist < radius)
            {
                float ageFactor  = 1f - age / windowSec;
                float distFactor = 1f - dist / radius;
                score += penalty * ageFactor * distFactor;
            }
        }
        return score;
    }

    private static Transform FallbackFarthestFromEnemies(
        List<Transform> spawnNodes,
        List<Transform> enemies)
    {
        Transform best     = spawnNodes[0];
        float     bestDist = float.MinValue;

        foreach (var node in spawnNodes)
        {
            if (node == null) continue;
            float minEnemyDist = float.MaxValue;
            foreach (var e in enemies)
                minEnemyDist = Mathf.Min(minEnemyDist, Vector3.Distance(node.position, e.position));

            if (enemies.Count == 0) minEnemyDist = 0;
            if (minEnemyDist > bestDist)
            {
                bestDist = minEnemyDist;
                best     = node;
            }
        }
        return best;
    }

    private static int CountTeamAlreadySpawned(NetworkRunner runner, PlayerTeam team, NPlayer exclude)
    {
        int count = 0;
        foreach (var p in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(p, out var obj)) continue;
            var np = obj.GetComponent<NPlayer>();
            if (np == null || np == exclude) continue;   // ✅ bỏ qua chính spawningPlayer
            if (np.NetworkedTeam == team)
                count++;
        }
        return count;
    }

    private static SpawnResult ToResult(Transform node, List<Transform> fallbackList)
    {
        if (node != null)
        {
            LogSpawnChoice(node, fallbackList);
            return new SpawnResult(node.position, node.rotation);
        }

        // Không bao giờ nên chạy tới đây, nhưng để an toàn
        var fallback = fallbackList[Random.Range(0, fallbackList.Count)];
        LogSpawnChoice(fallback, fallbackList);
        return new SpawnResult(fallback.position, fallback.rotation);
    }

    /// <summary>
    /// In ra vị trí spawn cuối cùng, index của node trong list spawn gốc (spawnNodes)
    /// và tên GameObject của node. Nếu node không nằm trong spawnNodes (ví dụ anchor
    /// team được kéo riêng trong Inspector) thì index sẽ là -1.
    /// </summary>
    private static void LogSpawnChoice(Transform node, List<Transform> spawnNodes)
    {
        int index = spawnNodes != null ? spawnNodes.IndexOf(node) : -1;
        Debug.Log($"<color=cyan>[SpawnEvaluator]</color> Spawn tại '{node.name}' " +
                  $"(index {index} trong spawnNodes), position = {node.position}");
    }
}

// ── Death record ──────────────────────────────────────────────────────────────
// Lưu vị trí + thời điểm chết. Server-only, không cần Networked.
// Thêm vào SpawnManager.cs và pass vào EvaluateMidGameRespawn().

public struct DeathRecord
{
    public Vector3 Position;
    public float   Timestamp; // Time.time tại thời điểm chết

    public DeathRecord(Vector3 pos, float simulationTime)
    {
        Position  = pos;
        Timestamp = simulationTime;
    }
}

// ── Ghi chú (*) ───────────────────────────────────────────────────────────────
// mv.StateFl.IsDead: cần thêm property này vào PlayerStateFlags nếu chưa có.
// Ví dụ: public bool IsDead => (flags & Flags.Dead) != 0;
// Hoặc dùng _hpHandler.IsDead qua GetComponent<NHPHandler>() nếu đã public.
// Nếu chưa có, xóa dòng check đó — worst case là tính địch đã chết vào penalty,
// không crash và không gây bug nghiêm trọng.