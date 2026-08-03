using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

public class BulletCaseController : MonoBehaviour
{
    [Range(0,1f)]
    [SerializeField] private float _volume = 1f;
    [Range(0,5f)]
    [SerializeField] private float _duration = 2f;
    
    [SerializeField] private float _ejectForce = 2f;
    [SerializeField] private float _torqueForce = 0.2f;
    
    
    [SerializeField] private BulletDropSound _bulletDropSoundPrefab;
    [SerializeField] private Transform _parrent;

    private List<BulletDropSound> _pool = new List<BulletDropSound>();

    [SerializeField] private float _forward = 0.3f;
    [SerializeField] private float _right = 1.0f;
    [SerializeField] private float _up = 0.5f;
    [SerializeField] private float _random = 0.15f;

    private void Start()
    {
        _parrent.name = $"Bullet Cace for {transform.root.name}";
        _parrent.parent = GameManager.instance.BulletCaseParrent;
    }

    private void OnDestroy()
    {
        if (_parrent != null)
            Destroy(_parrent.gameObject);
    }

    [Button]
    public void Drop()
    {
        BulletDropSound shell = Get();

        shell.transform.SetPositionAndRotation(
            transform.position,
            transform.rotation
        );

        Vector3 direction =
            transform.forward * _forward +
            transform.right * _right +
            transform.up * _up;

        direction += Random.insideUnitSphere * _random;

        shell.ResetValue(direction.normalized, _duration, _volume, _ejectForce, _torqueForce);
    }
    
    private BulletDropSound Get()
    {
        foreach (var shell in _pool)
        {
            if (!shell.gameObject.activeSelf)
            {
                shell.gameObject.SetActive(true);
                return shell;
            }
        }

        BulletDropSound shellNew = Instantiate(_bulletDropSoundPrefab, _parrent);
        _pool.Add(shellNew);

        return shellNew;
    }
}
