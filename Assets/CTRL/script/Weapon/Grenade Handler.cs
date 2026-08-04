using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using Sirenix.OdinInspector;
using UnityEngine;

public class GrenadeHandler : NetworkBehaviour
{
    [SerializeField] private GrenadeType _grenadeType;
    
    [TabGroup("Grenade", "Setiing")]
    [SerializeField] private float _duration = 2f;
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
    private TickTimer _despawnTimer = TickTimer.None;
    
    private List<LagCompensatedHit> _hits = new List<LagCompensatedHit>();
    
    private NetworkObject _networkObject;
    private NetworkRigidbody3D _networkRigidbody;
    private NShootHandler  _nshootHandler;

    private AudioManager _audio;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
        {
            Runner.SetIsSimulated(Object, false);
        }
    }

    public void Throw(Vector3 thorwForce, PlayerRef throwByPlayerRef, string thrownByPlayerName, LayerMask collisionLayer, NetworkId id, NShootHandler nShootHandler = null)
    {
        _throwByPlayerID = id;
        _nshootHandler = nShootHandler;
        _collisionLayer = collisionLayer;
        _networkObject = GetComponent<NetworkObject>();
        _networkRigidbody = GetComponent<NetworkRigidbody3D>();
        
        _networkRigidbody.Rigidbody.AddForce(thorwForce, ForceMode.Impulse);
        
        _throwByPlayerName = thrownByPlayerName;
        _throwByPlayerRef = throwByPlayerRef; 
        
        _explodeTickTimer = TickTimer.CreateFromSeconds(Runner, _duration);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (_explodeTickTimer.Expired(Runner))
            {
                _despawnTimer = TickTimer.CreateFromSeconds(Runner, 5);
                _explodePosition = _grenadeMesh.transform.position;
                
                int hitCount = Runner.LagCompensation.OverlapSphere(transform.position, _radius, _throwByPlayerRef,
                    _hits, _collisionLayer);
                
                _processedRoots.Clear();
                
                for (int i = 0; i < hitCount; i++)
                {
                    Transform root = _hits[i].Hitbox.transform.root;
                    if (!_processedRoots.Add(root)) continue;
                    
                    switch (_grenadeType)
                    {
                        case GrenadeType.Frag:
                            NHPHandler hpHandler = root.GetComponent<NHPHandler>();

                            if (hpHandler != null)
                            {
                                float distance = Vector3.Distance(transform.position, _hits[i].Hitbox.transform.position);
                                if (distance <= _radius * 0.7f)
                                {
                                    if (hpHandler.OnTakeDamage(100, _throwByPlayerName, _throwByPlayerID, 1))
                                        if (_nshootHandler != null)
                                            _nshootHandler._kill++;
                                }
                                else if (distance <= _radius * 0.85f)
                                {
                                    if (hpHandler.OnTakeDamage(70, _throwByPlayerName, _throwByPlayerID, 0.7f))
                                        if (_nshootHandler != null)
                                            _nshootHandler._kill++;
                                }
                                else
                                {
                                    if (hpHandler.OnTakeDamage(30, _throwByPlayerName, _throwByPlayerID, 0.4f))
                                        if (_nshootHandler != null)
                                            _nshootHandler._kill++;
                                }
                            }
                            break;
                        case GrenadeType.Flash:
                            CharComponentManager componentManager = root.GetComponent<CharComponentManager>();

                            if (componentManager != null)
                            {
                                float distance = Vector3.Distance(transform.position, componentManager.Camera.transform.position);
                                var nShootHandler = componentManager.NShootHandler;
                                Vector3 dir = (transform.position - componentManager.Camera.transform.position).normalized;
                                float angle = Vector3.Angle(nShootHandler._aimForward, dir);
                                float anglePercent = Mathf.InverseLerp(90f, 0f, angle);
        
                                if (anglePercent <= 0) continue;
        
                                if (Runner.GetPhysicsScene().Raycast(
                                        componentManager.Camera.transform.position,
                                        dir,
                                        out RaycastHit hit,
                                        distance,
                                        _groundLayer))
                                {
                                    continue;
                                }

                                // Gửi RPC đến đúng player bị flash
                                PlayerRef targetPlayer = componentManager.NShootHandler.Object.InputAuthority;
                                RPC_ApplyFlash(targetPlayer, distance);
                            }
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
            {
                Runner.Despawn(_networkObject);
            }
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnExplosion(Vector3 position)
    {
        GameObject ex = Instantiate(_explosionPrefab, position, Quaternion.identity);
        //Debug.Log($"despawned at {_explodePosition}");
        var des = ex.GetComponent<DestroyGameobject>();
        des.DestroyFrag(_explodeClips, _explodeVolume, true, _explodeMinDistance, _explodeMaxDistance);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyFlash([RpcTarget] PlayerRef target, float distance)
    {
        // Chạy trên máy của đúng client bị flash
        // Lấy local CharComponentManager của player này
        var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayer == null) return;
    
        var componentManager = localPlayer.GetComponent<CharComponentManager>();
        if (componentManager == null) return;
    
        componentManager.CameraShake.AddTrauma(0.2f);
        componentManager.UihpPlayer.Flash(distance, _radius);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // GameObject ex = Instantiate(_explosionPrefab, _explodePosition, Quaternion.identity);
        // Debug.Log($"despawned {runner.name} at {_explodePosition}");
        // var des = ex.GetComponent<DestroyGameobject>();
        // des.DestroyFrag(_explodeClips, _explodeVolume, true, _explodeMinDistance, _explodeMaxDistance);
    }

    private int _count = 0;
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("enter " + other.gameObject.name);
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
