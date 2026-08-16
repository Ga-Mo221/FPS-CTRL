using Sirenix.OdinInspector;
using UnityEngine;

public class ShootEffectController : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] _particles;
    [SerializeField] private LightCurves _lightCurves;

    [Button]
    public void Play()
    {
        foreach (ParticleSystem p in _particles)
            p.Play();

        _lightCurves.Play();
    }
}
