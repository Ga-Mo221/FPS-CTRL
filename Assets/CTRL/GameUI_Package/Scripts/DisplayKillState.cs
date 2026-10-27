using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Thông báo kill giữa phía trên màn hình (HEADSHOT, DOMINATING, RAMPAGE...) - CHỈ cho local player.
// Nghe LocalKillEvents (cùng nguồn với GameplayAudio) -> không tự dò kill lần 2, không RPC/biến networked.
// Gắn trên "KIller" (luôn active); bật/tắt "Panel" con: fade in (Slide kéo dài ra) -> giữ (Slide co lại
// như thanh thời gian) -> fade out. Sự kiện mới đến khi đang hiện -> chạy lại animation từ alpha hiện tại.
// Mốc chuỗi mặc định khớp GameplayAudioData: 3 Dominating, 5 Rampage, 7 Unstoppable, 9 Godlike,
// sau đó cứ thêm N kill lại hiện Legendary. Đổi mốc ở đây thì nhớ đổi cả bên GameplayAudioData.
public class DisplayKillState : MonoBehaviour
{
    [FoldoutGroup("UI")]
    [Required, SerializeField] private CanvasGroup _panel;              // CanvasGroup trên "Panel" (fade alpha)
    [FoldoutGroup("UI")]
    [SerializeField] private Image _iconKilled;
    [FoldoutGroup("UI")]
    [SerializeField] private TextMeshProUGUI _textKilled;
    [FoldoutGroup("UI")]
    [Tooltip("Đường kẻ ngang dưới icon: kéo dài ra khi fade in, co lại trong lúc giữ.")]
    [SerializeField] private RectTransform _slide;

    [FoldoutGroup("Thời gian")]
    [SerializeField, Min(0f)] private float _fadeInTime = 0.15f;
    [FoldoutGroup("Thời gian")]
    [SerializeField, Min(0f)] private float _holdTime = 1.5f;
    [FoldoutGroup("Thời gian")]
    [SerializeField, Min(0f)] private float _fadeOutTime = 0.4f;
    [FoldoutGroup("Thời gian")]
    [Tooltip("Panel phóng từ scale này về 1 khi fade in (1 = tắt hiệu ứng).")]
    [SerializeField, Min(0.1f)] private float _popScale = 1.25f;
    [FoldoutGroup("Thời gian")]
    [Tooltip("Slide co dần về 0 trong lúc giữ (thanh thời gian).")]
    [SerializeField] private bool _slideAsTimer = true;
    [FoldoutGroup("Thời gian")]
    [Tooltip("Đang hiện chuỗi kill thì headshot cùng lúc không đè lên (cùng 1 phát bắn).")]
    [SerializeField] private bool _streakOverHeadshot = true;

    [FoldoutGroup("Headshot")]
    [SerializeField] private string _headShotTitle = "HEADSHOT";
    [FoldoutGroup("Headshot")]
    [SerializeField] private Sprite _head_shot_sprite;

    [FoldoutGroup("Chuỗi kill")]
    [SerializeField, Min(1)] private int _dominatingKills = 3;
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private string _dominatingTitle = "DOMINATING";
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private Sprite _dominating_sprite;

    [FoldoutGroup("Chuỗi kill")]
    [SerializeField, Min(1)] private int _rampageKills = 5;
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private string _rampageTitle = "RAMPAGE";
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private Sprite _rampage_sprite;

    [FoldoutGroup("Chuỗi kill")]
    [SerializeField, Min(1)] private int _unstoppableKills = 7;
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private string _unstoppableTitle = "UNSTOPPABLE";
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private Sprite _unstoppable_sprite;

    [FoldoutGroup("Chuỗi kill")]
    [SerializeField, Min(1)] private int _godlikeKills = 9;
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private string _godlikeTitle = "GODLIKE";
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private Sprite _goldlike_sprite;

    [FoldoutGroup("Chuỗi kill")]
    [Tooltip("Sau mốc Godlike, cứ thêm N kill lại hiện Legendary (0 = tắt).")]
    [SerializeField, Min(0)] private int _legendaryEvery = 2;
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private string _legendaryTitle = "LEGENDARY";
    [FoldoutGroup("Chuỗi kill")]
    [SerializeField] private Sprite _legendary_sprite;

    private Coroutine _routine;
    private bool _showingStreak;
    private float _slideWidth;

    private void Awake()
    {
        if (_slide != null) _slideWidth = _slide.localScale.x;
        if (_panel != null)
        {
            // Thông báo không được chặn click / chuột của HUD bên dưới.
            _panel.interactable = false;
            _panel.blocksRaycasts = false;
        }
        Hide();
    }

    private void OnEnable()
    {
        LocalKillEvents.Headshot += OnHeadshot;
        LocalKillEvents.Kill += OnKill;
    }

    private void OnDisable()
    {
        LocalKillEvents.Headshot -= OnHeadshot;
        LocalKillEvents.Kill -= OnKill;
        // Coroutine dừng khi tắt object -> ẩn hẳn, tránh panel kẹt giữa chừng.
        Hide();
    }

    // ---- Sự kiện ----

    private void OnHeadshot()
    {
        if (_streakOverHeadshot && _showingStreak && _routine != null) return;
        Show(_headShotTitle, _head_shot_sprite, false);
    }

    private void OnKill(int streak)
    {
        if (streak <= 0) return;

        if (streak == _dominatingKills) Show(_dominatingTitle, _dominating_sprite, true);
        else if (streak == _rampageKills) Show(_rampageTitle, _rampage_sprite, true);
        else if (streak == _unstoppableKills) Show(_unstoppableTitle, _unstoppable_sprite, true);
        else if (streak == _godlikeKills) Show(_godlikeTitle, _goldlike_sprite, true);
        else if (_legendaryEvery > 0 && streak > _godlikeKills && (streak - _godlikeKills) % _legendaryEvery == 0)
            Show(_legendaryTitle, _legendary_sprite, true);
    }

    // ---- Hiển thị ----

    // Hiện thông báo; đang hiện thì chạy lại animation (không chồng 2 coroutine).
    public void Show(string title, Sprite icon, bool isStreak)
    {
        if (_panel == null) return;

        if (_textKilled != null) _textKilled.text = title;
        if (_iconKilled != null)
        {
            _iconKilled.sprite = icon;
            _iconKilled.enabled = icon != null; // chưa có sprite cho mốc này -> chỉ hiện chữ
        }

        _showingStreak = isStreak;
        if (_routine != null) StopCoroutine(_routine);
        _panel.gameObject.SetActive(true);
        _routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        var panelRect = _panel.transform;

        // Fade in từ alpha hiện tại (sự kiện liên tiếp không bị nháy về 0).
        float startAlpha = _panel.alpha;
        float t = _fadeInTime > 0f ? startAlpha * _fadeInTime : 0f;
        while (t < _fadeInTime)
        {
            float k = t / _fadeInTime;
            float ease = 1f - (1f - k) * (1f - k);
            _panel.alpha = k;
            panelRect.localScale = Vector3.one * Mathf.Lerp(_popScale, 1f, ease);
            SetSlide(ease);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _panel.alpha = 1f;
        panelRect.localScale = Vector3.one;
        SetSlide(1f);

        // Giữ
        t = 0f;
        while (t < _holdTime)
        {
            if (_slideAsTimer) SetSlide(1f - t / _holdTime);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_slideAsTimer) SetSlide(0f);

        // Fade out
        t = 0f;
        while (t < _fadeOutTime)
        {
            _panel.alpha = 1f - t / _fadeOutTime;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        _routine = null;
        Hide();
    }

    private void SetSlide(float k)
    {
        if (_slide == null) return;
        var s = _slide.localScale;
        s.x = _slideWidth * k;
        _slide.localScale = s;
    }

    private void Hide()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        _showingStreak = false;
        if (_panel == null) return;
        _panel.alpha = 0f;
        _panel.transform.localScale = Vector3.one;
        SetSlide(1f);
        _panel.gameObject.SetActive(false);
    }
}
