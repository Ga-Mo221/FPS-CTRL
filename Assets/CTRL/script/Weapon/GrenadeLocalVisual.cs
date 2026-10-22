using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Lựu đạn "ảo" chỉ có trên máy NGƯỜI NÉM (client), hiện ngay lúc animation ném tới event thả tay —
/// không phải chờ host spawn + gửi về (~1 RTT). Bay bằng cùng <see cref="GrenadeFlight"/> với bản mạng.
/// Khi bản mạng (GrenadeHandler) tới nơi, nó nhận vị trí hiện tại của bản ảo rồi huỷ bản ảo (<see cref="Claim"/>).
/// Host từ chối ném (hết lựu đạn...) → bản ảo tự huỷ sau <see cref="UNCLAIMED_LIFETIME"/> giây.
/// </summary>
public class GrenadeLocalVisual : MonoBehaviour
{
    private const float UNCLAIMED_LIFETIME = 2f;

    private static readonly List<GrenadeLocalVisual> s_Pending = new List<GrenadeLocalVisual>();

    private readonly GrenadeFlight _flight = new GrenadeFlight();
    private NetworkRunner _runner;
    private string _grenadeId;
    private float _startTime;
    private float _spawnRealtime;
    private Quaternion _startRot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_Pending.Clear();
        s_ArrivedFirst.Clear();
    }

    public static int PendingCount => s_Pending.Count;

    // Bản mạng tới TRƯỚC khi animation trên máy này kịp tạo bản ảo (ping cao) → bản ảo tạo sau sẽ bị bỏ,
    // tránh 2 quả cùng bay.
    private static readonly List<(string id, float time)> s_ArrivedFirst = new List<(string, float)>();

    public static void NoteNetworkArrivedFirst(string grenadeId) =>
        s_ArrivedFirst.Add((grenadeId, Time.realtimeSinceStartup));

    private static bool ConsumeArrivedFirst(string grenadeId)
    {
        float now = Time.realtimeSinceStartup;
        for (int i = s_ArrivedFirst.Count - 1; i >= 0; i--)
            if (now - s_ArrivedFirst[i].time > UNCLAIMED_LIFETIME) s_ArrivedFirst.RemoveAt(i);
        for (int i = 0; i < s_ArrivedFirst.Count; i++)
        {
            if (s_ArrivedFirst[i].id != grenadeId) continue;
            s_ArrivedFirst.RemoveAt(i);
            return true;
        }
        return false;
    }

    public static void Spawn(NetworkRunner runner, GrenadeHandler prefab, Vector3 startPos, Vector3 velocity)
    {
        if (runner == null || prefab == null || prefab.MeshTemplate == null)
        {
            Debug.LogWarning($"[Grenade] Không tạo được lựu đạn ảo: runner={runner != null} prefab={(prefab != null ? prefab.ID : "null")} " +
                             $"mesh={(prefab != null && prefab.MeshTemplate != null)}");
            return;
        }
        if (ConsumeArrivedFirst(prefab.ID)) return; // bản mạng đã tới và đang bay rồi

        var root = new GameObject("Local Grenade (" + prefab.ID + ")");
        Quaternion rot = Quaternion.LookRotation(velocity.sqrMagnitude > 1e-6f ? velocity : Vector3.forward);
        root.transform.SetPositionAndRotation(startPos, rot);

        // Chỉ copy phần hình (mesh) của prefab, đặt đúng local pose như trong prefab.
        Transform tpl = prefab.MeshTemplate;
        GameObject mesh = Instantiate(tpl.gameObject, root.transform);
        mesh.transform.localPosition = tpl.localPosition;
        mesh.transform.localRotation = tpl.localRotation;
        mesh.transform.localScale = tpl.localScale;
        foreach (var col in mesh.GetComponentsInChildren<Collider>()) col.enabled = false;

        var v = root.AddComponent<GrenadeLocalVisual>();
        v._runner = runner;
        v._grenadeId = prefab.ID;
        v._startTime = runner.LocalRenderTime;
        v._spawnRealtime = Time.realtimeSinceStartup;
        v._startRot = rot;
        v._flight.Reset(startPos, velocity, prefab.FlightMask, runner.DeltaTime);
        s_Pending.Add(v);
    }

    /// <summary>
    /// Bản mạng của người ném gọi khi Spawned: lấy bản ảo cũ nhất cùng loại, trả về vị trí hiện tại
    /// và thời gian đã bay (số tick, có phần lẻ) để bản mạng nối tiếp ĐÚNG timeline của bản ảo.
    /// </summary>
    public static bool Claim(string grenadeId, out Vector3 visualPos, out float visualSteps)
    {
        for (int i = 0; i < s_Pending.Count; i++)
        {
            GrenadeLocalVisual v = s_Pending[i];
            if (v == null || v._grenadeId != grenadeId) continue;
            s_Pending.RemoveAt(i);
            visualPos = v.transform.position;
            visualSteps = v.ElapsedSteps();
            Destroy(v.gameObject);
            return true;
        }
        visualPos = default;
        visualSteps = 0f;
        return false;
    }

    private float ElapsedSteps() => Mathf.Max(0f, (_runner.LocalRenderTime - _startTime) / _runner.DeltaTime);

    private void Update()
    {
        if (_runner == null || !_runner.IsRunning ||
            Time.realtimeSinceStartup - _spawnRealtime > UNCLAIMED_LIFETIME)
        {
            Destroy(gameObject);
            return;
        }

        _flight.Sample(ElapsedSteps(), out Vector3 pos, out float angle);
        transform.SetPositionAndRotation(pos, _startRot * Quaternion.AngleAxis(angle, Vector3.right));
    }

    private void OnDestroy() => s_Pending.Remove(this);
}
