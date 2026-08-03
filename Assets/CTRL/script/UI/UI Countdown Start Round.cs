using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICountdownStartRound : MonoBehaviour
{
    [SerializeField] private Image _countdown_Background;
    [SerializeField] private TextMeshProUGUI _countdownTimerText;
    [SerializeField] private float _alpha_target = 0.7f;
    [SerializeField] private float _fade_in = 0.5f;
    [SerializeField] private float _start_fade_out = 7f;
    [SerializeField] private float _fade_out = 2f;
    [SerializeField] private float _textPunchScale = 1.4f;  // scale max khi số mới xuất hiện
    [SerializeField] private float _textPunchDuration = 0.25f; // thời gian phóng to rồi về 1
    
    [SerializeField] private TextMeshProUGUI _roundTimerText; // thời gian đã chơi trong round hiện tại.
    [SerializeField] private TextMeshProUGUI _roundCountText; // round hien tai.
    [SerializeField] private TextMeshProUGUI _timerText; // tong thoi gian da choi.

    private bool _isVisible = false;
    private float _currentAlpha = 0f;
    private int _lastDisplayedSecond = -1;
    private float _punchTimer = 0f;
    private bool _isPunching = false;

    private void Update()
    {
        if (NetworkGameState.Instance == null) { HideImmediate(); return; }
        if (NetworkGameState.Instance.Object == null || !NetworkGameState.Instance.Object.IsValid) return;
        
        if (NetworkGameState.Instance.IsMatchOver()) { HideImmediate(); return; }

        UpdateAlwaysOnUI();

        if (!NetworkGameState.Instance.GameStarted)
        {
            var remaining = NetworkGameState.Instance.GetCountdownRemaining();
            if (remaining.HasValue) Show(remaining.Value);
            // GameStarted = false nhưng timer chưa sync → giữ nguyên trạng thái, không Hide
            return; // ← thêm dòng này
        }

        HideImmediate();
    }

    private void UpdateAlwaysOnUI()
    {
        var state = NetworkGameState.Instance;

        // Round timer: thời gian đã chơi trong hiệp hiện tại
        if (_roundTimerText != null)
        {
            float r = state.RoundElapsed;
            int min = Mathf.FloorToInt(r / 60f);
            int sec = Mathf.FloorToInt(r % 60f);
            _roundTimerText.text = $"{min:00}:{sec:00}";
        }

        // Round count: hiệp hiện tại
        if (_roundCountText != null)
        {
            _roundCountText.text = state.TotalRounds > 1
                ? $"Hiệp {state.CurrentRound}/{state.TotalRounds}"
                : $"Hiệp {state.CurrentRound}";
        }

        // Timer: tổng thời gian đã chơi cả trận
        if (_timerText != null)
        {
            float t = state.TotalElapsed;
            int min = Mathf.FloorToInt(t / 60f);
            int sec = Mathf.FloorToInt(t % 60f);
            _timerText.text = $"{min:00}:{sec:00}";
        }
    }

    private void Show(float remaining)
    {
        if (!_isVisible)
        {
            _isVisible = true;
            _countdown_Background.gameObject.SetActive(true);
            _countdownTimerText.gameObject.SetActive(true);
            _currentAlpha = 0f;
            _lastDisplayedSecond = -1;
        }

        // ── Text: số đếm + punch scale ──────────────────────────
        int second = Mathf.CeilToInt(remaining);
        if (second != _lastDisplayedSecond)
        {
            _lastDisplayedSecond = second;
            _countdownTimerText.text = second.ToString();
            _isPunching = true;
            _punchTimer = 0f;
        }

        if (_isPunching)
        {
            _punchTimer += Time.deltaTime;
            float t = _punchTimer / _textPunchDuration;
            if (t >= 1f)
            {
                t = 1f;
                _isPunching = false;
            }
            // Phóng to rồi thu về: punch curve bằng sin
            float scale = Mathf.Lerp(_textPunchScale, 1f, t);
            _countdownTimerText.transform.localScale = Vector3.one * scale;
        }

        // ── Background alpha ─────────────────────────────────────
        float targetAlpha;
        if (remaining <= _start_fade_out)
        {
            // remaining chạy từ 7 về 0
            // t chạy từ 0 về 1 trong vòng _fade_out giây (2 giây cuối)
            float fadeOutT = Mathf.Clamp01((_start_fade_out - remaining) / _fade_out);
            targetAlpha = Mathf.Lerp(_alpha_target, 0f, fadeOutT);
        }
        else
        {
            targetAlpha = _alpha_target;
        }

        // Fade in khi mới hiện
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime / _fade_in);

        SetBackgroundAlpha(_currentAlpha);
    }

    private void HideImmediate()
    {
        if (!_isVisible) return;
        _isVisible = false;
        _countdown_Background.gameObject.SetActive(false);
        _countdownTimerText.gameObject.SetActive(false);
        _countdownTimerText.transform.localScale = Vector3.one;
        _lastDisplayedSecond = -1;
    }

    private void SetBackgroundAlpha(float alpha)
    {
        Color c = _countdown_Background.color;
        c.a = alpha;
        _countdown_Background.color = c;
    }
}