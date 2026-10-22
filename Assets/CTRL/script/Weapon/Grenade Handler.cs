using System.Collections.Generic;
using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

public class GrenadeHandler : NetworkBehaviour
{
    [TabGroup("Info", "Info")] 
    [HorizontalGroup("Info/Info/Info", width: 0.2f)]
    [PreviewField(50, ObjectFieldAlignment.Left)]
    [HideLabel]
    public Sprite Icon;
    
    [HorizontalGroup("Info/Info/Info")]
    [VerticalGroup("Info/Info/Info/Info")]
    public string ID;

    [VerticalGroup("Info/Info/Info/Info")]
    public string Name;
    
    [VerticalGroup("Info/Info/Info/Info")]
    [SerializeField] private GrenadeType _grenadeType;

    [TabGroup("Grenade", "Setiing")] 
    [SerializeField] private float _despawn_duration = 2f;
    [TabGroup("Grenade", "Setiing")]
    [SerializeField] private float _Ex_duration = 2f;
    [TabGroup("Grenade", "Setiing")]
    [SerializeField] private float _radius = 4f;
    [TabGroup("Grenade", "Setiing")]
    [SerializeField] private GameObject _explosionPrefab;
    [TabGroup("Grenade", "Setiing")]
    [SerializeField] private Transform _grenadeMesh;

    [TabGroup("Grenade", "Audio")]
    [SerializeField] private AudioSource _source;

    [FoldoutGroup("Grenade/Audio/Drop")]
    [SerializeField] private AudioClip[] _dropClips;
    [FoldoutGroup("Grenade/Audio/Drop")]
    [SerializeField] private AudioClip _dropClip;
    [FoldoutGroup("Grenade/Audio/Drop")]
    [SerializeField] private float _dropVolumeDrop = 0.5f;
    [FoldoutGroup("Grenade/Audio/Drop")]
    [SerializeField] private float _dropMinDistance = 0.1f;
    [FoldoutGroup("Grenade/Audio/Drop")]
    [SerializeField] private float _dropMaxDistance = 0.5f;

    [FoldoutGroup("Grenade/Audio/Explode")]
    [SerializeField] private AudioClip[] _explodeClips;
    [FoldoutGroup("Grenade/Audio/Explode")]
    [SerializeField] private float _explodeVolume = 0.5f;
    [FoldoutGroup("Grenade/Audio/Explode")]
    [SerializeField] private float _explodeMinDistance = 1f;
    [FoldoutGroup("Grenade/Audio/Explode")]
    [SerializeField] private float _explodeMaxDistance = 20f;

    [SerializeField] private LayerMask _groundLayer;

    // ── Đường bay: chỉ đồng bộ tham số ném, mỗi máy tự mô phỏng (GrenadeFlight) ──────────────
    // Trước đây NetworkRigidbody3D gửi vị trí mỗi snapshot → ping > 30ms là giật. Giờ quỹ đạo tất định
    // từ (StartPos, LaunchVel, StartTick); host chỉ báo thêm vị trí nổ.
    [Networked] private Vector3 StartPos { get; set; }
    [Networked] private Vector3 LaunchVel { get; set; }
    [Networked] private int StartTick { get; set; }
    [Networked] private NetworkBool Exploded { get; set; }
    [Networked] private Vector3 ExplodePos { get; set; }

    private readonly GrenadeFlight _authFlight = new GrenadeFlight(); // host: vị trí chính thức theo tick
    private readonly GrenadeFlight _viewFlight = new GrenadeFlight(); // mọi máy: hiển thị mượt theo render time
    private Quaternion _startRot;
    private Vector3 _visualOffset;      // người ném: lệch giữa bản ảo (hiện trước) và bản mạng → mờ dần về 0
    private int _playedBounces;
    private bool _viewReady;
    private float _viewShift;          // người ném không có bản ảo: lùi timeline để bay từ đầu
    private const float OFFSET_BLEND_SPEED = 10f;

    /// <summary>Phần hình của lựu đạn — <see cref="GrenadeLocalVisual"/> copy để hiện ngay trên máy người ném.</summary>
    public Transform MeshTemplate => _grenadeMesh;
    /// <summary>Layer va chạm của đường bay (map tĩnh).</summary>
    public LayerMask FlightMask => _groundLayer;

    private NetworkId _throwByPlayerID;

    private HashSet<Transform> _processedRoots = new HashSet<Transform>();

    private LayerMask _collisionLayer;

    private PlayerRef _throwByPlayerRef;
    private string _throwByPlayerName;

    private TickTimer _explodeTickTimer = TickTimer.None;
    private TickTimer _despawnTimer     = TickTimer.None;

    private List<LagCompensatedHit> _hits = new List<LagCompensatedHit>();

    private NetworkObject  _networkObject;
    private NShootHandler  _nshootHandler;

    private AudioManager _audio;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            Runner.SetIsSimulated(Object, false);

        _startRot = Quaternion.LookRotation(LaunchVel.sqrMagnitude > 1e-6f ? LaunchVel : transform.forward);
        _viewFlight.Reset(StartPos, LaunchVel, _groundLayer, Runner.DeltaTime);
        _playedBounces = 0;
        _visualOffset = Vector3.zero;
        transform.SetPositionAndRotation(StartPos, _startRot);

        // Máy người ném (client) đã hiện sẵn lựu đạn ảo → nối tiếp từ vị trí của nó, không giật/không nhân đôi.
        _viewShift = 0f;
        if (Object.HasInputAuthority && !Object.HasStateAuthority)
        {
            if (GrenadeLocalVisual.Claim(ID, out Vector3 visualPos, out float visualSteps))
            {
                // Animation ném trên máy này có thể chạy trễ hơn timeline dự đoán (ping cao) → bản mạng
                // "đi trước" bản ảo, nối theo vị trí sẽ trượt vèo tới điểm rơi. Lùi timeline bản mạng
                // về đúng thời gian bản ảo đã bay; vị trí nổ cuối cùng vẫn lấy của host (ExplodePos).
                float ahead = ViewSteps() - visualSteps;
                if (ahead > 0f) _viewShift = ahead;
                if (ahead > 6f)
                    Debug.LogWarning($"[Grenade] {ID}: bản mạng đi trước bản ảo {ahead:F1} tick → nối theo timeline bản ảo.");
                _viewFlight.Sample(ViewSteps(), out Vector3 netPos, out _);
                _visualOffset = visualPos - netPos;
                _playedBounces = _viewFlight.Bounces;
            }
            else
            {
                // Không có bản ảo để nối → bản mạng tới trễ ~1 RTT nên đã "ở giữa/cuối" quỹ đạo.
                // Cho bay lại từ đầu trên máy này (trễ một chút nhưng thấy được đường bay).
                float behind = ViewSteps();
                _viewShift = Mathf.Max(0f, behind);
                GrenadeLocalVisual.NoteNetworkArrivedFirst(ID);
                Debug.LogWarning($"[Grenade] {ID}: không có lựu đạn ảo để nối (pending={GrenadeLocalVisual.PendingCount}), " +
                                 $"bản mạng tới trễ {behind:F1} tick → bay lại từ đầu.");
            }
        }
        _viewReady = true;
    }

    public GrenadeType GrenadeType => _grenadeType;

    public void Throw(Vector3 throwForce, PlayerRef throwByPlayerRef, string thrownByPlayerName,
        LayerMask collisionLayer, NetworkId id, NShootHandler nShootHandler = null)
    {
        _throwByPlayerID   = id;
        _nshootHandler     = nShootHandler;
        _collisionLayer    = collisionLayer;
        _networkObject     = GetComponent<NetworkObject>();

        // Rigidbody mass 1 + ForceMode.Impulse trước đây → vận tốc = lực ném.
        StartPos  = transform.position;
        LaunchVel = throwForce;
        StartTick = Runner.Tick;
        _authFlight.Reset(StartPos, LaunchVel, _groundLayer, Runner.DeltaTime);

        _throwByPlayerName = thrownByPlayerName;
        _throwByPlayerRef  = throwByPlayerRef;

        _explodeTickTimer  = TickTimer.CreateFromSeconds(Runner, _Ex_duration);
    }

    /// <summary>Số tick (có phần lẻ) kể từ lúc ném theo đồng hồ render của máy này.
    /// Host + người ném: timeline dự đoán (khớp tay mình); người khác: timeline remote (khớp animation ném họ thấy).</summary>
    private float ViewSteps()
    {
        float now = (Object.HasStateAuthority || Object.HasInputAuthority) ? Runner.LocalRenderTime : Runner.RemoteRenderTime;
        return now / Runner.DeltaTime - StartTick - _viewShift;
    }

    public override void Render()
    {
        if (!_viewReady) return;

        Vector3 pos; float angle;
        if (Exploded)
        {
            pos = ExplodePos; angle = _viewFlight.Angle;
            _visualOffset = Vector3.zero;
        }
        else
        {
            _viewFlight.Sample(ViewSteps(), out pos, out angle);
            _visualOffset *= Mathf.Exp(-OFFSET_BLEND_SPEED * Time.deltaTime);
            pos += _visualOffset;
        }
        transform.SetPositionAndRotation(pos, _startRot * Quaternion.AngleAxis(angle, Vector3.right));

        // Tiếng chạm đất: mỗi máy tự phát theo số lần nảy của mô phỏng.
        while (_playedBounces < _viewFlight.Bounces)
        {
            PlayDropSound(_playedBounces == 0);
            _playedBounces++;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (!Exploded) _authFlight.AdvanceTo(Runner.Tick - StartTick);

        if (_explodeTickTimer.Expired(Runner))
        {
            _despawnTimer    = TickTimer.CreateFromSeconds(Runner, 2f);
            Vector3 center   = _authFlight.Pos;
            ExplodePos       = center;
            Exploded         = true;
            transform.position = center; // HandleFrag/HandleFlash đo khoảng cách từ transform

            int hitCount = 0;
            if (_grenadeType != GrenadeType.Smoke)
            {
                hitCount = Runner.LagCompensation.OverlapSphere(
                    center, _radius, _throwByPlayerRef,
                    _hits, _collisionLayer);
            }

            _processedRoots.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Transform root = null;
                if (_grenadeType != GrenadeType.Smoke)
                {
                    root = _hits[i].Hitbox.transform.root;
                    if (!_processedRoots.Add(root)) continue;
                }

                switch (_grenadeType)
                {
                    case GrenadeType.Frag:
                        if (root != null)
                            HandleFrag(root, i);
                        break;

                    case GrenadeType.Flash:
                        if (root != null)
                            HandleFlash(root);
                        break;

                    case GrenadeType.Smoke:
                        break;
                }
            }

            RPC_SpawnExplosion(center);
            _explodeTickTimer = TickTimer.None;
        }

        if (_despawnTimer.Expired(Runner))
            Runner.Despawn(_networkObject);
    }

    // ── Frag ──────────────────────────────────────────────────────────────
    private void HandleFrag(Transform root, int hitIndex)
    {
        NHPHandler hpHandler = root.GetComponent<NHPHandler>();
        if (hpHandler == null) return;

        // Subscribe target trước để _damageAccumulated hoạt động đúng
        _nshootHandler?.OnGrenadeHit(hpHandler);

        float distance = Vector3.Distance(
            transform.position, _hits[hitIndex].Hitbox.transform.position);

        int  damage  = 0;
        float trauma = 0f;

        if (distance <= _radius * 0.7f)
        {
            damage = 100;
            trauma = 1f;
        }
        else if (distance <= _radius * 0.85f)
        {
            damage = 70;
            trauma = 0.7f;
        }
        else
        {
            damage = 30;
            trauma = 0.4f;
        }

        bool isKill = hpHandler.OnTakeDamage(damage, _throwByPlayerName, "Frag", _throwByPlayerID, trauma);

        // OnDamageDealt đã được invoke bên trong OnTakeDamage
        // → _damageAccumulated[target] đã được cộng tự động qua OnTargetDamaged
        if (isKill && _nshootHandler != null)
            _nshootHandler.OnGrenadeKill(hpHandler);
    }

    // ── Flash ─────────────────────────────────────────────────────────────
    private void HandleFlash(Transform root)
    {
        CharComponentManager componentManager = root.GetComponent<CharComponentManager>();
        if (componentManager == null) return;

        float    distance      = Vector3.Distance(transform.position, componentManager.Camera.transform.position);
        var      nShootHandler = componentManager.NShootHandler;
        Vector3  dir           = (transform.position - componentManager.Camera.transform.position).normalized;
        float    angle         = Vector3.Angle(nShootHandler._aimForward, dir);
        float    anglePercent  = Mathf.InverseLerp(90f, 0f, angle);

        if (anglePercent <= 0) return;

        if (Runner.GetPhysicsScene().Raycast(
                componentManager.Camera.transform.position,
                dir, out RaycastHit hit,
                distance, _groundLayer))
            return;

        PlayerRef targetPlayer = componentManager.NShootHandler.Object.InputAuthority;
        RPC_ApplyFlash(targetPlayer, distance);
    }

    // ── RPC ───────────────────────────────────────────────────────────────
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnExplosion(Vector3 position)
    {
        _grenadeMesh.gameObject.SetActive(false);
        GameObject ex  = Instantiate(_explosionPrefab, position, Quaternion.identity);
        var        des = ex.GetComponent<DestroyGameobject>();
        des.DestroyFrag(_despawn_duration, _explodeClips, _explodeVolume, true, _explodeMinDistance, _explodeMaxDistance);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyFlash([RpcTarget] PlayerRef target, float distance)
    {
        var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayer == null) return;

        var componentManager = localPlayer.GetComponent<CharComponentManager>();
        if (componentManager == null) return;

        componentManager.CameraShake.AddTrauma(0.2f);
        componentManager.GameHudController().Flash(distance, _radius);
    }

    // ── Audio ─────────────────────────────────────────────────────────────
    // Gọi từ Render theo số lần nảy của GrenadeFlight (trước đây OnTriggerEnter của rigidbody host/proxy).
    private void PlayDropSound(bool firstHit)
    {
        if (_audio == null)
            _audio = AudioManager.Instance;
        if (_audio == null) return;

        if (firstHit)
            _audio.PlayClip(_source, _dropClips, _dropVolumeDrop, true, _dropMinDistance, _dropMaxDistance);
        else
            _audio.PlayClip(_source, _dropClip, _dropVolumeDrop, true, _dropMinDistance, _dropMaxDistance);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_grenadeType == GrenadeType.Smoke) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}

public enum GrenadeType
{
    Frag,
    Flash,
    Smoke
}