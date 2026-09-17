using System.Collections;
using UnityEngine;

public class DestroyGameobject : MonoBehaviour
{
    [SerializeField] private AudioSource _source;
    public void DestroyFrag(float duration, AudioClip[] clips, float volume, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        AudioManager.Instance.PlayClip(_source, clips, volume, stop, minDistance, maxDistance);

        StartCoroutine(destroy(duration));
    }

    IEnumerator destroy(float time)
    {
        yield return new WaitForSeconds(time);
        Destroy(gameObject);
    }
}