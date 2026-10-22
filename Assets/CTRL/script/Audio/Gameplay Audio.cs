using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Am thanh gameplay (announcer) CHI PHAT TREN MAY LOCAL (khong RPC, khong bien networked rieng).
/// Gan tren "N Game State Prefab" (cung GameObject voi NetworkGameState) -> moi map co NetworkGameState la tu chay.
/// Cau hinh (clip, moc chuoi, volume) nam trong <see cref="GameplayAudioData"/>; phat qua AudioManager
/// tren AudioSource cua GameObject con "Announcer Audio".
/// - Dem nguoc three/two/one + fight: moi may tu doc GameStarted / GetCountdownRemaining() trong Update.
///   Gia tri dau tien quan sat duoc chi ghi lai, khong phat -> nguoi vao muon khong nghe lai fight / so dem cu.
/// - victory / gameover: UIGameplay goi PlayMatchResult() sau khi tinh thang/thua cho local player.
/// - headshoot / dominating / rampage: nghe <see cref="LocalKillEvents"/> (NInGameMessagesHandler + NShootHandler phat khi local player ha dich).
/// Announcer dung hang doi -> headshot va moc chuoi cung 1 phat ban khong de tieng len nhau.
/// </summary>
[RequireComponent(typeof(NetworkGameState))]
public class GameplayAudio : MonoBehaviour
{
    public static GameplayAudio Instance { get; private set; }

    [SerializeField] private GameplayAudioData _data;
    [Tooltip("AudioSource 2D (GameObject con) de AudioManager phat announcer.")]
    [SerializeField] private AudioSource _audioSource;

    private NetworkGameState _gs;
    private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
    private float _busyUntil;          // PlayOneShot khong bao isPlaying chac chan -> tu tinh theo do dai clip

    private bool _initialized;         // da quan sat frame hop le dau tien
    private bool _lastGameStarted;
    private int _lastCountdownSecond = -1;
    private bool _resultPlayed;

    private void Awake()
    {
        Instance = this;
        _gs = GetComponent<NetworkGameState>();
    }

    private void OnEnable()
    {
        LocalKillEvents.Headshot += OnLocalHeadshotKill;
        LocalKillEvents.Kill += OnLocalKill;
    }

    private void OnDisable()
    {
        LocalKillEvents.Headshot -= OnLocalHeadshotKill;
        LocalKillEvents.Kill -= OnLocalKill;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        UpdateQueue();

        if (_data == null) return;
        if (_gs == null || _gs.Object == null || !_gs.Object.IsValid) return;

        if (!_initialized)
        {
            // NetworkGameState vua spawn tren may nay -> ghi lai trang thai hien tai (khong phat).
            _initialized = true;
            _lastGameStarted = _gs.GameStarted;
            _lastCountdownSecond = CurrentCountdownSecond();
            return;
        }

        if (_gs.IsMatchOver()) return;

        bool started = _gs.GameStarted;
        if (!started)
        {
            // Dem nguoc: phat dung luc so tren UI doi sang 3 / 2 / 1 (cung cach lam tron voi UICountdownStartRound).
            int second = CurrentCountdownSecond();
            if (second != _lastCountdownSecond)
            {
                _lastCountdownSecond = second;
                if (second == 3) Announce(_data.Three, true);
                else if (second == 2) Announce(_data.Two, true);
                else if (second == 1) Announce(_data.One, true);
            }
        }
        else if (!_lastGameStarted)
        {
            // false -> true: hiep vua bat dau (ca hiep sau khi TotalRounds > 1).
            Announce(_data.Fight, true);
            _lastCountdownSecond = -1;
        }
        _lastGameStarted = started;
    }

    private int CurrentCountdownSecond()
    {
        if (_gs.GameStarted) return -1;
        var remaining = _gs.GetCountdownRemaining();
        return remaining.HasValue ? Mathf.CeilToInt(remaining.Value) : -1;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>UIGameplay goi 1 lan khi tran ket thuc (logic thang/thua co san cua game).</summary>
    public void PlayMatchResult(bool localPlayerWon)
    {
        if (_resultPlayed || _data == null) return;
        _resultPlayed = true;
        Announce(localPlayerWon ? _data.Victory : _data.GameOver, true);
    }

    /// <summary>Local player vua ha dich bang headshot (tu kill feed).</summary>
    public void OnLocalHeadshotKill()
    {
        if (!CanAnnounceKill()) return;
        Announce(_data.Headshot);
    }

    /// <summary>So kill cua local player vua tang; streak = chuoi hien tai (GameManager, reset khi chet).</summary>
    public void OnLocalKill(int streak)
    {
        if (!CanAnnounceKill() || streak <= 0) return;

        Announce(_data.GetStreakClip(streak));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private bool CanAnnounceKill()
    {
        return _data != null && !_resultPlayed && _gs != null && _gs.Object != null && _gs.Object.IsValid && !_gs.IsMatchOver();
    }

    /// <summary>Them cau vao hang doi; interrupt = cat cau dang doc + xoa hang doi (dem nguoc, fight, ket qua tran).</summary>
    private void Announce(AudioClip clip, bool interrupt = false)
    {
        if (clip == null) return;
        if (interrupt)
        {
            _queue.Clear();
            _busyUntil = 0f; // AudioManager se Stop() source truoc khi phat
        }
        if (_queue.Count >= _data.MaxQueue) return;
        _queue.Enqueue(clip);
        UpdateQueue();
    }

    private void UpdateQueue()
    {
        if (_queue.Count == 0 || Time.unscaledTime < _busyUntil) return;
        if (AudioManager.Instance == null || _audioSource == null) { _queue.Clear(); return; }

        var clip = _queue.Dequeue();
        _busyUntil = Time.unscaledTime + clip.length;
        AudioManager.Instance.PlayClip2D(_audioSource, clip, _data.Volume);
    }
}
