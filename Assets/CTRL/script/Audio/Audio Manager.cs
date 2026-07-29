using UnityEngine;

[DefaultExecutionOrder(10)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [SerializeField] private AudioClip[] _FootStep;
    
    [SerializeField] private AudioClip _Dirt_Slide;
    
    [SerializeField] private AudioClip[] _Jump;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayClip(AudioSource source, AudioClip clip, float volume = 1, float pitch = 1)
    {
        if (source == null || clip == null) return;
        Stop(source);
        source.volume = volume;
        source.pitch = pitch;
        source.PlayOneShot(clip);
    }

    public void PlayClipAtPoint(AudioClip clip, Vector3 point, float volume = 1)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, point, volume);
    }

    public void Jump(AudioSource source)
    {
        int index = Random.Range(0, _Jump.Length);
        PlayClip(source, _Jump[index], 0.15f, Random.Range(0.8f, 1.2f));
    }

    public void FootStep(AudioSource source)
    {
        int index = Random.Range(0, _FootStep.Length);
        PlayClip(source, _FootStep[index], 0.15f, Random.Range(0.8f, 1.2f));
    }

    public void Dirt(AudioSource source)
    {
        PlayClip(source, _Dirt_Slide, 0.4f, Random.Range(0.8f, 1.2f));
    }

    private void Stop(AudioSource source)
    {
        if (source != null && source.isPlaying) source.Stop();
    }
}