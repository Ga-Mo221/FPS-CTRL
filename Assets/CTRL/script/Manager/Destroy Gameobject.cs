using System.Collections;
using UnityEngine;

public class DestroyGameobject : MonoBehaviour
{
    [SerializeField] private AudioSource _source;
    public void DestroyFrag(AudioClip[] clips, float volume, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        AudioManager.Instance.PlayClip(_source, clips, volume, stop, minDistance, maxDistance);

        StartCoroutine(destroy(5f));
    }

    IEnumerator destroy(float time)
    {
        yield return new WaitForSeconds(time);
        Destroy(gameObject);
    }
}