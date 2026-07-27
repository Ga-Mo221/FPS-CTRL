using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class FootStepAudio : MonoBehaviour
{
    [SerializeField] private AudioSource _sound;
    private AudioManager _audio;

    [SerializeField] private float _radius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;
    private bool _lateResul;

    private void Start()
    {
        _audio = AudioManager.Instance;
    }

    private void Update()
    {
        PlayAudio();
    }

    private void PlayAudio()
    {
        bool Isgrounded = Physics.CheckSphere(transform.position, _radius, _groundLayer, QueryTriggerInteraction.Ignore);

        if (Isgrounded != _lateResul)
        {
            _lateResul = Isgrounded;
            if (_lateResul) _audio.FootStep(_sound);
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
