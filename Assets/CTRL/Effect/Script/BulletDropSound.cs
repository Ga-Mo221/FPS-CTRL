using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[DefaultExecutionOrder(20)]
public class BulletDropSound : MonoBehaviour
{
    private bool _played;
    private float _duration = 5f;
    private float _volume = 1f;
    private Vector3 _direction;
    
    [SerializeField] private AudioClip[]  _clips;
    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioManager _audio;
    
    private CustomLocalRigidbody _rbLocal;

    private void Awake()
    {
        _audio = AudioManager.Instance;
        _rbLocal = GetComponent<CustomLocalRigidbody>();
    }
    
    private void OnEnable()
    {
        // Đăng ký nhận Callback
        _rbLocal.OnCollisionEnterLocal += HandleCollision;
    }

    private void OnDisable()
    {
        // Hủy đăng ký khi Object bị vô hiệu hóa
        _rbLocal.OnCollisionEnterLocal -= HandleCollision;
    }
    
    private void HandleCollision(CustomLocalRigidbody.CustomCollisionData data)
    {
        if (_played) return;
        _played = true;
        StartCoroutine(Disable());
    }
    
    IEnumerator Disable()
    {
        int index = Random.Range(0, _clips.Length);
        _audio.PlayClip(_source, _clips[index], _volume);
        yield return new WaitForSeconds(_duration);
        gameObject.SetActive(false);
    }
    
    public void ResetValue(Vector3 direction, float duration, float volume, float ejectForce, float torqueForce)
    {
        _played = false;
        _direction = direction.normalized;
        _volume = volume;
        _duration = duration;

        _rbLocal.AddForce(_direction * ejectForce);
        //_rbLocal.AddTorque(Random.onUnitSphere * torqueForce, ForceMode.Impulse);
    }
}