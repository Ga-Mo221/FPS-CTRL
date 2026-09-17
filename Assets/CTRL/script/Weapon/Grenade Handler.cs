using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Sirenix.OdinInspector;
using UnityEngine;

public class GrenadeHandler : NetworkBehaviour
{
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

    private Vector3 _explodePosition { get; set; }

    private NetworkId _throwByPlayerID;

    private HashSet<Transform> _processedRoots = new HashSet<Transform>();

    private LayerMask _collisionLayer;

    private PlayerRef _throwByPlayerRef;
    private string _throwByPlayerName;

    private TickTimer _explodeTickTimer = TickTimer.None;
    private TickTimer _despawnTimer     = TickTimer.None;

    private List<LagCompensatedHit> _hits = new List<LagCompensatedHit>();

    private NetworkObject  _networkObject;
    private NetworkRigidbody3D _networkRigidbody;
    private NShootHandler  _nshootHandler;

    private AudioManager _audio;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            Runner.SetIsSimulated(Object, false);
    }

    public void Throw(Vector3 throwForce, PlayerRef throwByPlayerRef, string thrownByPlayerName,
        LayerMask collisionLayer, NetworkId id, NShootHandler nShootHandler = null)
    {
        _throwByPlayerID   = id;
        _nshootHandler     = nShootHandler;
        _collisionLayer    = collisionLayer;
        _networkObject     = GetComponent<NetworkObject>();
        _networkRigidbody  = GetComponent<NetworkRigidbody3D>();

        _networkRigidbody.Rigidbody.AddForce(throwForce, ForceMode.Impulse);

        _throwByPlayerName = thrownByPlayerName;
        _throwByPlayerRef  = throwByPlayerRef;

        _explodeTickTimer  = TickTimer.CreateFromSeconds(Runner, _Ex_duration);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (_explodeTickTimer.Expired(Runner))
        {
            _despawnTimer    = TickTimer.CreateFromSeconds(Runner, 2f);
            _explodePosition = _grenadeMesh.transform.position;
            
            int hitCount = 0;
            if (_grenadeType != GrenadeType.Smoke)
            {
                hitCount = Runner.LagCompensation.OverlapSphere(
                    transform.position, _radius, _throwByPlayerRef,
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

            _grenadeMesh.gameObject.SetActive(false);
            RPC_SpawnExplosion(_explodePosition);
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
    private int _count = 0;
    private void OnTriggerEnter(Collider other)
    {
        if (_audio == null)
            _audio = AudioManager.Instance;

        _count++;
        if (_count >= 2)
        {
            _audio.PlayClip(_source, _dropClip, _dropVolumeDrop, true, _dropMinDistance, _dropMaxDistance);
            return;
        }
        _audio.PlayClip(_source, _dropClips, _dropVolumeDrop, true, _dropMinDistance, _dropMaxDistance);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
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