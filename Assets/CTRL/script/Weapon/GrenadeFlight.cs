using UnityEngine;

/// <summary>
/// Mô phỏng đường bay lựu đạn TẤT ĐỊNH, bước cố định = 1 tick Fusion.
/// Mọi máy chạy cùng (vị trí ném, vận tốc, tick ném) + cùng hình học map tĩnh → ra cùng quỹ đạo,
/// nên KHÔNG cần đồng bộ vị trí khi bay (hết giật khi ping cao). Chỉ va chạm với layer map
/// (không tính người chơi — vị trí người chơi mỗi máy khác nhau sẽ làm lệch quỹ đạo).
/// </summary>
public sealed class GrenadeFlight
{
    private const float RADIUS       = 0.05f;  // bán kính va chạm (gần collider cũ 0.04–0.05)
    private const float SKIN         = 0.01f;  // đẩy ra khỏi bề mặt sau va chạm
    private const float BOUNCE       = 0.25f;  // giống Grenade.physicMaterial (Bounciness 0.25)
    private const float FRICTION     = 0.35f;  // mất vận tốc tiếp tuyến mỗi lần chạm
    private const float REST_SPEED   = 0.6f;   // chậm hơn mức này trên mặt phẳng → nằm yên
    private const float SOUND_SPEED  = 1.0f;   // vận tốc pháp tuyến tối thiểu để phát tiếng chạm
    private const float SPIN_DEG     = 540f;   // lộn vòng khi bay (độ/giây)
    private const int   MAX_STEPS    = 64 * 8; // an toàn: tối đa ~8s

    public Vector3 Pos { get; private set; }
    public Vector3 PrevPos { get; private set; }
    public float Angle { get; private set; }
    public float PrevAngle { get; private set; }
    public int Steps { get; private set; }
    public int Bounces { get; private set; }
    public bool Resting { get; private set; }

    private Vector3 _vel;
    private float _spin;
    private float _step;
    private LayerMask _mask;

    public void Reset(Vector3 startPos, Vector3 velocity, LayerMask mask, float stepSeconds)
    {
        Pos = PrevPos = startPos;
        Angle = PrevAngle = 0f;
        Steps = 0;
        Bounces = 0;
        Resting = false;
        _vel = velocity;
        _spin = SPIN_DEG;
        _step = stepSeconds > 0f ? stepSeconds : 1f / 64f;
        _mask = mask;
    }

    /// <summary>Chạy tới đúng <paramref name="steps"/> bước (chỉ tiến, không lùi).</summary>
    public void AdvanceTo(int steps)
    {
        while (Steps < steps) Step();
    }

    /// <summary>Vị trí/góc nội suy tại thời điểm thực <paramref name="tSteps"/> (tính bằng số bước, có phần lẻ).</summary>
    public void Sample(float tSteps, out Vector3 pos, out float angle)
    {
        if (tSteps < 0f) tSteps = 0f;
        int whole = Mathf.FloorToInt(tSteps);
        if (whole + 1 < Steps) whole = Steps - 1; // thời gian lùi nhẹ (jitter) → giữ ở bước hiện có
        AdvanceTo(whole + 1);
        float frac = Mathf.Clamp01(tSteps - whole);
        pos = Vector3.Lerp(PrevPos, Pos, frac);
        angle = Mathf.Lerp(PrevAngle, Angle, frac);
    }

    private void Step()
    {
        PrevPos = Pos;
        PrevAngle = Angle;
        Steps++;
        if (Resting) return;

        _vel += Physics.gravity * _step;
        Vector3 pos = Pos;
        Vector3 move = _vel * _step;
        float dist = move.magnitude;

        if (dist > 1e-6f && Physics.SphereCast(pos, RADIUS, move / dist, out RaycastHit hit, dist,
                _mask, QueryTriggerInteraction.Ignore))
        {
            Vector3 n = hit.normal;
            pos += move / dist * hit.distance + n * SKIN;

            float vn = Vector3.Dot(_vel, n);
            Vector3 vNormal = vn * n;
            Vector3 vTangent = _vel - vNormal;
            if (-vn > SOUND_SPEED) Bounces++;

            _vel = vTangent * (1f - FRICTION) - vNormal * BOUNCE;
            _spin *= 0.6f;

            if (n.y > 0.6f && _vel.magnitude < REST_SPEED)
            {
                _vel = Vector3.zero;
                Resting = true;
            }
        }
        else
        {
            pos += move;
        }

        Pos = pos;
        Angle += _spin * _step;
        if (Steps >= MAX_STEPS) Resting = true;
    }
}
