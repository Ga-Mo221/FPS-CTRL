using UnityEngine;

/// <summary>
/// Cau hinh am thanh gameplay (announcer) dung cho <see cref="GameplayAudio"/>.
/// Nhac nen moi map lam rieng, khong nam o day.
/// </summary>
[CreateAssetMenu(fileName = "Gameplay Audio Data", menuName = "FPS/Data/Gameplay Audio Data")]
public class GameplayAudioData : ScriptableObject
{
    // ─────────────────────────────────────────────
    //  DEM NGUOC / BAT DAU TRAN
    // ─────────────────────────────────────────────

    [Header("Dem nguoc / bat dau tran")]
    [Tooltip("Phat khi dem nguoc con 3 giay.")]
    [SerializeField] private AudioClip _three;
    [Tooltip("Phat khi dem nguoc con 2 giay.")]
    [SerializeField] private AudioClip _two;
    [Tooltip("Phat khi dem nguoc con 1 giay.")]
    [SerializeField] private AudioClip _one;
    [Tooltip("Phat khi hiep bat dau.")]
    [SerializeField] private AudioClip _fight;

    // ─────────────────────────────────────────────
    //  KET THUC TRAN
    // ─────────────────────────────────────────────

    [Header("Ket thuc tran")]
    [Tooltip("Local player thang.")]
    [SerializeField] private AudioClip _victory;
    [Tooltip("Local player thua (hoac hoa o che do Team).")]
    [SerializeField] private AudioClip _gameOver;

    // ─────────────────────────────────────────────
    //  KILL STREAK / HEADSHOT
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class StreakTier
    {
        [Tooltip("So kill lien tiep (khong chet) de phat clip nay.")]
        [Min(1)] public int kills = 3;
        public AudioClip clip;
    }

    [Header("Kill streak / headshot (chi local player)")]
    [SerializeField] private AudioClip _headshot;
    [Tooltip("Cac moc chuoi kill (vd 3 Dominating, 5 Rampage, 7 Unstoppable, 9 Godlike). Chuoi reset khi chet.")]
    [SerializeField] private StreakTier[] _streakTiers;
    [Tooltip("Sau moc cuoi cung, phat clip nay de noi tiep chuoi (de trong = khong phat).")]
    [SerializeField] private AudioClip _streakRepeatClip;
    [Tooltip("Sau moc cuoi cung, cu them N kill lai phat _streakRepeatClip (0 = tat).")]
    [SerializeField, Min(0)] private int _streakRepeatEvery = 2;

    // ─────────────────────────────────────────────
    //  ANNOUNCER
    // ─────────────────────────────────────────────

    [Header("Announcer")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [Tooltip("Toi da so cau cho trong hang doi (tranh doc don dap khi giet lien tuc).")]
    [SerializeField, Min(1)] private int _maxQueue = 3;

    public AudioClip Three => _three;
    public AudioClip Two => _two;
    public AudioClip One => _one;
    public AudioClip Fight => _fight;
    public AudioClip Victory => _victory;
    public AudioClip GameOver => _gameOver;
    public AudioClip Headshot => _headshot;

    /// <summary>
    /// Clip can phat khi chuoi kill = streak (null neu khong co moc nao).
    /// Dung dung moc -> clip cua moc do; vuot moc cuoi -> _streakRepeatClip moi _streakRepeatEvery kill.
    /// </summary>
    public AudioClip GetStreakClip(int streak)
    {
        if (_streakTiers == null || streak <= 0) return null;

        int lastKills = 0;
        foreach (var tier in _streakTiers)
        {
            if (tier == null) continue;
            if (tier.kills == streak) return tier.clip;
            if (tier.kills > lastKills) lastKills = tier.kills;
        }

        if (_streakRepeatClip != null && _streakRepeatEvery > 0 && lastKills > 0
            && streak > lastKills && (streak - lastKills) % _streakRepeatEvery == 0)
            return _streakRepeatClip;
        return null;
    }
    public float Volume => _volume;
    public int MaxQueue => _maxQueue;
}
