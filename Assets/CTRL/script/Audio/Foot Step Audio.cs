using Sirenix.OdinInspector;
using UnityEngine;

public class FootStepAudio : MonoBehaviour
{
    [SerializeField] private AudioSource _sound;
    private AudioManager _audio;

    [SerializeField] private float _radius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;
    private bool _lateResul;
    private NCharMovementHandler _movementHandler;
    private PlayerData _data;

    private void Awake()
    {
        _movementHandler = GetComponentInParent<NCharMovementHandler>();
        if (_movementHandler != null) _data = _movementHandler.Data;
    }

    private void Start()
    {
        _audio = AudioManager.Instance;
    }

    private void Update()
    {
        if (_data == null) return;
        PlayAudio();
    }

    private void PlayAudio()
    {
        bool Isgrounded = Physics.CheckSphere(transform.position, _radius, _groundLayer, QueryTriggerInteraction.Ignore);

        if (Isgrounded != _lateResul)
        {
            _lateResul = Isgrounded;
            if (_lateResul) 
                _audio.PlayClip(_sound, _data.FootStepClip, _data.Volume_FootStep, false);
        }
    }
    
    //============================= EDITOR ===================================
    
    #if  UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }

    [Button]
    public void Setup()
    {
        if (_sound != null) return;
        
        GameObject audio = new GameObject("Audio");
        audio.transform.parent = transform;
        _sound = audio.AddComponent<AudioSource>();
    }
    
    #endif
}
