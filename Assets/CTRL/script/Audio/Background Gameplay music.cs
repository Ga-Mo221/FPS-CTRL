using System;
using UnityEngine;

/// <summary>
/// Nhac nen rieng cua tung map: phat _startClip 1 lan roi noi lien _loopClip lap vo han.
/// - Dong bo voi NetworkGameState: _startClip ket thuc DUNG luc dem nguoc = 0 (tran bat dau) va _loopClip vao ngay luc do.
///   Nguoi vao muon (tran da bat dau) -> phat thang _loopClip.
/// - Noi lien khong ho: _loopClip duoc PlayScheduled tren AudioSource thu 2.
/// - Am luong = _startVolume / _loopVolume (can bang thu cong) x Master x Music trong SettingData,
///   cap nhat ngay khi nguoi choi chinh slider (SettingManager.OnSettingsApplied).
///   AudioSource khong di qua AudioMixer nen tu nhan volume o day (khong bi ap 2 lan).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BackgroundGameplaymusic : MonoBehaviour
{
    private AudioSource _audioSource;
    private AudioSource _loopSource;    // tao luc Awake, chi phat _loopClip

    [SerializeField] private AudioClip _startClip;
    [Range(0,2f)]
    [SerializeField] private float _startVolume = 1f;

    [SerializeField] private AudioClip _loopClip;
    [Range(0,2f)]
    [SerializeField] private float _loopVolume = 1f;

    private float _settingVolume = 1f;  // Master x Music tu SettingData
    private bool _subscribed;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        SetupSource(_audioSource, false);

        _loopSource = gameObject.AddComponent<AudioSource>();
        SetupSource(_loopSource, true);
    }

    [Tooltip("Lech qua so giay nay giua luc loop duoc hen va luc tran bat dau that (lag mang) -> chuyen sang loop ngay.")]
    [SerializeField] private float _resyncThreshold = 0.25f;

    [Tooltip("Thoi gian (giay) fade in khi start clip bat dau phat (ca khi phat tu giua clip luc vao muon).")]
    [SerializeField, Min(0f)] private float _startFadeIn = 1.5f;

    private double _startAtDsp = double.MaxValue; // dspTime start clip bat dau phat (moc tinh fade in)
    private float _startFade = 1f;                // 0..1, nhan them vao volume start clip

    private bool _started;              // da bat dau phat (start hoac loop)
    private bool _loopStarted;          // da chuyen sang loop (that su dang phat)
    private double _loopAtDsp;          // dspTime loop duoc hen phat (= luc dem nguoc ve 0)

    private void Start()
    {
        Subscribe();
        UpdateSettingVolume();
    }

    private void OnDestroy()
    {
        if (_subscribed && SettingManager.Instance != null)
            SettingManager.Instance.OnSettingsApplied -= OnSettingsApplied;
    }

    private void Update()
    {
        // SettingManager co the sinh sau (vd mo thang scene map khi test) -> dang ky lai khi co.
        if (!_subscribed && SettingManager.Instance != null)
        {
            Subscribe();
            UpdateSettingVolume();
        }

        UpdateStartFade();

        var gs = NetworkGameState.Instance;
        if (gs == null || gs.Object == null || !gs.Object.IsValid) return;

        if (!_started)
        {
            if (gs.GameStarted || gs.IsMatchOver())
            {
                // vao muon (tran da bat dau) -> bo qua start clip, vao loop luon
                PlayLoopNow();
                return;
            }

            var remaining = gs.GetCountdownRemaining();
            if (remaining.HasValue) PlaySynced(remaining.Value);
            return;
        }

        // Tran da bat dau that ma loop van con hen o tuong lai (lech dong ho / lag) -> chuyen loop ngay.
        if (!_loopStarted && gs.GameStarted)
        {
            if (_loopAtDsp - AudioSettings.dspTime > _resyncThreshold) PlayLoopNow();
            else _loopStarted = true;
        }
    }

    // ── Phat ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Can start clip sao cho no KET THUC dung luc dem nguoc = 0, roi hen loop clip ngay luc do.
    /// Dem nguoc dai hon start clip -> cho roi moi phat; ngan hon (vao giua dem nguoc) -> phat tu giua clip.
    /// </summary>
    private void PlaySynced(float countdownRemaining)
    {
        _started = true;
        _audioSource.Stop();
        _loopSource.Stop();

        double now = AudioSettings.dspTime;
        _loopAtDsp = now + Mathf.Max(0f, countdownRemaining);

        if (_startClip != null)
        {
            // tinh theo samples/frequency de chinh xac hon clip.length (tranh khe ho khi noi)
            double startLength = (double)_startClip.samples / _startClip.frequency;
            double startAt = _loopAtDsp - startLength;
            _audioSource.clip = _startClip;

            if (startAt >= now)
            {
                _audioSource.timeSamples = 0;
                _audioSource.PlayScheduled(startAt);
                _startAtDsp = startAt;
            }
            else
            {
                int offset = (int)((now - startAt) * _startClip.frequency);
                if (offset < _startClip.samples)
                {
                    _audioSource.timeSamples = offset;
                    _audioSource.Play();
                    _startAtDsp = now; // vao giua clip -> fade in tinh tu luc nay
                }
            }
            _startFade = 0f;
        }

        if (_loopClip != null)
        {
            _loopSource.clip = _loopClip;
            _loopSource.PlayScheduled(_loopAtDsp);
        }

        ApplyVolume();
    }

    private void PlayLoopNow()
    {
        _started = true;
        _loopStarted = true;
        _audioSource.Stop();
        _loopSource.Stop();

        if (_loopClip != null)
        {
            _loopSource.clip = _loopClip;
            _loopSource.Play();
        }

        ApplyVolume();
    }

    private static void SetupSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // nhac nen 2D
        source.pitch = 1f;
    }

    // ── Volume theo setting ──────────────────────────────────────────────────

    private void Subscribe()
    {
        if (_subscribed || SettingManager.Instance == null) return;
        SettingManager.Instance.OnSettingsApplied += OnSettingsApplied;
        _subscribed = true;
    }

    private void OnSettingsApplied(SettingData data) => UpdateSettingVolume();

    private void UpdateSettingVolume()
    {
        var data = SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        _settingVolume = data != null ? data.MasterVolume * data.MusicVolume : 1f;
        ApplyVolume();
    }

    /// <summary>Fade in start clip theo dspTime (dung ca khi start clip duoc hen phat trong tuong lai).</summary>
    private void UpdateStartFade()
    {
        if (_startFade >= 1f) return;
        double elapsed = AudioSettings.dspTime - _startAtDsp;
        _startFade = _startFadeIn > 0f ? Mathf.Clamp01((float)(elapsed / _startFadeIn)) : 1f;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (_audioSource != null) _audioSource.volume = _startVolume * _settingVolume * _startFade;
        if (_loopSource != null) _loopSource.volume = _loopVolume * _settingVolume;
    }
}
