using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

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

    public void PlayClip(AudioSource source, AudioClip clip, float volume, float pitch, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        if (source == null) Debug.LogError("<color=red>[AudioManager]</color> source is null");
        if (clip == null) Debug.LogError("<color=red>[AudioManager]</color> clips is null");
        if (source == null || clip == null) return;
        if (stop) Stop(source);
        source.spatialBlend = 1f;
        source.volume = volume;
        source.pitch = pitch;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.PlayOneShot(clip);
    }
    
    public void PlayClip(AudioSource source, AudioClip clip, float volume, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        if (source == null) Debug.LogError("<color=red>[AudioManager]</color> source is null");
        if (clip == null) Debug.LogError("<color=red>[AudioManager]</color> clips is null");
        if (source == null || clip == null) return;
        if (stop) Stop(source);
        source.spatialBlend = 1f;
        source.volume = volume;
        source.pitch = Random.Range(0.8f, 1.2f);
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.PlayOneShot(clip);
    }

    public void PlayClip(AudioSource source, AudioClip[] clips, float volume, float pitch, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        if (source == null) Debug.LogError("<color=red>[AudioManager]</color> source is null");
        if (clips == null || clips.Length == 0) Debug.LogError("<color=red>[AudioManager]</color> clips is null");
        if (source == null || clips.Length == 0) return;
        if (stop) Stop(source);
        int index = Random.Range(0, clips.Length);
        source.spatialBlend = 1f;
        source.volume = volume;
        source.pitch = pitch;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.PlayOneShot(clips[index]);
    }
    
    public void PlayClip(AudioSource source, AudioClip[] clips, float volume, bool stop = true, float minDistance = 1, float maxDistance = 10)
    {
        if (source == null) Debug.LogError("<color=red>[AudioManager]</color> source is null");
        if (clips == null || clips.Length == 0) Debug.LogError("<color=red>[AudioManager]</color> clips is null");
        if (source == null || clips.Length == 0) return;
        if (stop) Stop(source);
        int index = Random.Range(0, clips.Length);
        source.spatialBlend = 1f;
        source.volume = volume;
        source.pitch = Random.Range(0.8f, 1.2f);
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.PlayOneShot(clips[index]);
    }

    public void PlayClip(
        AudioSource source, 
        AudioClip[] clips, 
        float volume, 
        float startTime, 
        float loopTime, 
        float endTime,
        bool stop = true, 
        float minDistance = 1,
        float maxDistance = 10)
    {
        if (source == null) Debug.LogError("<color=red>[AudioManager]</color> source is null");
        if (clips == null || clips.Length == 0) Debug.LogError("<color=red>[AudioManager]</color> clips is null");
        if (source == null || clips == null || clips.Length == 0) return;

        if (stop) Stop(source);

        int index = Random.Range(0, clips.Length);
        
        // Cấu hình AudioSource
        source.spatialBlend = 1f;
        source.clip = clips[index];
        source.pitch = Random.Range(0.8f, 1.2f);
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.loop = true; // Bật loop để đảm bảo âm thanh không bị ngắt giữa chừng
        source.Play();

        // Chạy chuỗi xử lý âm lượng qua các giai đoạn
        StartCoroutine(PlayClipSequence(source, volume, startTime, loopTime, endTime));
    }

    private IEnumerator PlayClipSequence(AudioSource source, float targetVolume, float startTime, float loopTime, float endTime)
    {
        // 1. Fade in (StartVolume)
        if (startTime > 0)
        {
            yield return StartCoroutine(StartVolume(source, targetVolume, startTime));
        }
        else
        {
            if (source != null) source.volume = targetVolume;
        }

        // 2. Duy trì âm lượng (LoopVolume)
        if (loopTime > 0)
        {
            yield return StartCoroutine(LoopVolume(source, targetVolume, loopTime));
        }

        // 3. Fade out (EndVolume)
        if (endTime > 0)
        {
            yield return StartCoroutine(EndVolume(source, endTime));
        }
    }

    IEnumerator StartVolume(AudioSource source, float targetVolume, float duration)
    {
        if (source == null) yield break;

        float currentTime = 0f;
        source.volume = 0f; // Bắt đầu từ 0

        while (currentTime < duration)
        {
            if (source == null) yield break; // Kiểm tra nếu source bị destroy giữa chừng

            currentTime += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, currentTime / duration);
            yield return null;
        }

        source.volume = targetVolume;
    }

    IEnumerator LoopVolume(AudioSource source, float targetVolume, float duration)
    {
        if (source == null) yield break;

        source.volume = targetVolume;
        yield return new WaitForSeconds(duration);
    }

    IEnumerator EndVolume(AudioSource source, float duration)
    {
        if (source == null) yield break;

        float currentTime = 0f;
        float startVolume = source.volume; // Lấy volume hiện tại làm điểm bắt đầu giảm

        while (currentTime < duration)
        {
            if (source == null) yield break;

            currentTime += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, currentTime / duration);
            yield return null;
        }

        source.volume = 0f;
        source.Stop(); // Dừng phát nhạc sau khi hoàn tất fade out
    }

    private void Stop(AudioSource source)
    {
        if (source != null && source.isPlaying) source.Stop();
    }
}