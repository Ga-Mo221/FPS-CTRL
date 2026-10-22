using Fusion;
using Game.UI;
using UnityEngine;

public class DropItemWeapon : NetworkBehaviour
{
    [Networked] public int CurrentBullet { get; private set; }
    [Networked] public int MaxBullet { get; private set; }
    // NetworkString<_16> -> byte chi so trong GameManager._weapon_Prefabs.
    [Networked] private byte _weaponIdx { get; set; } = 255;
    public string WeaponID { get { var gm = GameManager.Instance; return gm != null ? (gm.GetWeaponIdByIndex(_weaponIdx) ?? "") : ""; } }
    // chi host dung; DropItemWeapon khong nam trong vong resume migration -> field thuong.
    private TickTimer DespawnTimer;

    private GameHudControllerBase _gameHudController;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, 10f);
        }

        // ✅ Client tự lấy HUD của mình sau khi spawn
        // Thay bằng cách lấy HUD phù hợp với project của bạn
        if (NPlayer.Local != null)
        {
            _gameHudController = NPlayer.Local
                .GetComponentInChildren<GameHudControllerBase>();
        }

        // Không còn NetworkTransform: vị trí lấy từ state networked.
        if (_landed)
        {
            transform.SetPositionAndRotation(_restPos, _restRot);
            _settled = true;
        }
        else
        {
            transform.SetPositionAndRotation(_startPos, _restRot);
            _velocity   = _launchVel;
            _flightTime = 0f;
            _flying     = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    // ── Bay khi bị vứt (giống CS2) ───────────────────────────────────────────────────────────
    // Host chỉ đồng bộ ĐIỂM XUẤT PHÁT + VẬN TỐC BAN ĐẦU; MỖI MÁY tự mô phỏng quỹ đạo theo frame (mượt,
    // không giật như khi NetworkTransform kéo theo từng snapshot). Khi host thấy súng chạm đất → đồng bộ
    // VỊ TRÍ TIẾP ĐẤT; client nào lệch một chút thì trượt nhẹ về đúng điểm đó.
    private const float GRAVITY    = -15f;   // m/s² (nặng hơn thực tế một chút cho dứt khoát)
    private const float RADIUS     = 0.08f;  // bán kính va chạm của súng (m)
    private const float BOUNCE     = 0.3f;   // giữ lại bao nhiêu vận tốc khi đập tường
    private const float SPIN_DEG   = 360f;   // tốc độ lộn vòng khi bay (độ/giây)
    private const float MAX_FLIGHT = 4f;     // an toàn: quá thời gian này thì coi như đã chạm đất
    private const float MAX_STEP   = 1f / 60f; // chia nhỏ bước mô phỏng để FPS thấp vẫn va chạm đúng
    private const float SETTLE_SPEED = 12f;  // tốc độ client trượt về vị trí tiếp đất của host

    [Networked] private Vector3 _startPos { get; set; }
    [Networked] private Vector3 _launchVel { get; set; }
    [Networked] private int _collisionMask { get; set; }
    [Networked] private Quaternion _restRot { get; set; }
    [Networked] private Vector3 _restPos { get; set; }
    [Networked] private NetworkBool _landed { get; set; }

    // Mô phỏng cục bộ (mỗi máy tự chạy).
    private bool _flying;
    private bool _settled;
    private Vector3 _velocity;
    private float _flightTime;

    /// <summary>Host: gọi trong onBeforeSpawned — súng bay ra từ startPos với vận tốc ban đầu rồi rơi xuống.</summary>
    public void Launch(Vector3 startPos, Vector3 velocity, LayerMask collisionMask, Quaternion restRotation)
    {
        _startPos = startPos;
        _launchVel = velocity;
        _collisionMask = collisionMask.value;
        _restRot = restRotation;
        _landed = false;
    }

    /// <summary>Host: gọi trong onBeforeSpawned — đặt thẳng xuống đất (vd súng rơi khi chết).</summary>
    public void PlaceAt(Vector3 pos, Quaternion rotation)
    {
        _restPos = pos;
        _restRot = rotation;
        _landed = true;
    }

    private void UpdateFlight()
    {
        if (_flying)
        {
            // Host đã báo tiếp đất mà máy này còn đang bay (lệch thời gian) → thôi bay, trượt về điểm tiếp đất.
            if (_landed && !Object.HasStateAuthority) { _flying = false; return; }

            float remaining = Time.deltaTime;
            while (_flying && remaining > 0f)
            {
                float dt = Mathf.Min(remaining, MAX_STEP);
                remaining -= dt;
                StepFlight(dt);
            }
            return;
        }

        if (_landed && !_settled)
        {
            float t = 1f - Mathf.Exp(-SETTLE_SPEED * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, _restPos, t),
                Quaternion.Slerp(transform.rotation, _restRot, t));
            if ((transform.position - _restPos).sqrMagnitude < 0.0001f)
            {
                transform.SetPositionAndRotation(_restPos, _restRot);
                _settled = true;
            }
        }
    }

    private void StepFlight(float dt)
    {
        _flightTime += dt;
        _velocity.y += GRAVITY * dt;

        Vector3 pos  = transform.position;
        Vector3 step = _velocity * dt;
        float dist   = step.magnitude;

        if (dist > 1e-5f && Physics.SphereCast(pos, RADIUS, step / dist, out RaycastHit hit, dist,
                _collisionMask, QueryTriggerInteraction.Ignore))
        {
            pos += step / dist * hit.distance + hit.normal * 0.01f;
            if (hit.normal.y > 0.6f)
            {
                LandLocal(pos);
                return;
            }
            _velocity = Vector3.Reflect(_velocity, hit.normal) * BOUNCE; // đập tường → nảy ngược, mất đà
        }
        else
        {
            pos += step;
        }

        transform.position = pos;
        transform.Rotate(Vector3.right, SPIN_DEG * dt, Space.Self);

        if (_flightTime >= MAX_FLIGHT) LandLocal(pos);
    }

    private void LandLocal(Vector3 pos)
    {
        _flying = false;
        _velocity = Vector3.zero;
        transform.SetPositionAndRotation(pos, _restRot); // nằm như súng rơi bình thường

        // Chỉ host quyết định vị trí tiếp đất chính thức.
        if (Object.HasStateAuthority)
        {
            _restPos = pos;
            _landed = true;
            _settled = true;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (IsCurrentDragItem())
            _gameHudController?.SetDragItemVisible(false, null);
    }

    private bool IsCurrentDragItem()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.GetDropItemWeapon == this;
    }

    private void Update()
    {
        if (Object != null && Object.IsValid) UpdateFlight();

        // ✅ Guard cả hai null
        if (_gameHudController == null || NPlayer.Local == null) return;

        float dis = Vector3.Distance(Object.transform.position, NPlayer.Local.transform.position);
        if (dis < 1.5f)
            _gameHudController.SetDragItemVisible(true, this);
        else if (IsCurrentDragItem())
            // Chi item DANG duoc chon moi duoc xoa trang thai. Truoc day MOI item o xa deu goi (false, null)
            // moi frame -> ghi de item o gan (tuy thu tu Update) -> bam nhat luc trung null, phai bam lai.
            _gameHudController.SetDragItemVisible(false, null);
    }

    public void SetBullet(int current, int max, string weaponID, GameHudControllerBase hud)
    {
        CurrentBullet = current;
        MaxBullet = max;
        var gm = GameManager.Instance;
        int idx = gm != null ? gm.GetWeaponIndexById(weaponID) : -1;
        if (idx < 0 || idx > 254) Debug.LogWarning("[DropItemWeapon] Khong tim thay vu khi '" + weaponID + "' trong GameManager -> khong nhat duoc.");
        _weaponIdx = (byte)(idx >= 0 && idx <= 254 ? idx : 255);
        _gameHudController = hud; // Vẫn giữ để host set luôn
    }

    // Host: danh dau item da duoc nhat (chan 2 nguoi nhat cung item trong cung tick).
    // Truoc day co RPC_RequestDespawn (RpcSources.All) cho moi may xoa item ma khong kiem tra -> da bo,
    // viec nhat + xoa item gio do WeaponManager.Rpc_RequestPickup tren host quyet dinh.
    private bool _claimed;

    public bool TryClaim()
    {
        if (!Object.HasStateAuthority || _claimed) return false;
        _claimed = true;
        return true;
    }
}