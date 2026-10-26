// [Aemy 2026-10-26] FILE MOI.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Boot
{
    /// <summary>
    /// Hien thi man tieu de: intro (logo thu khoang chu + phong to nhe), nen troi cham, glitch logo dinh ky,
    /// thanh nap, chu "Nhan phim bat ky" nhap nho, hieu ung khi bam. Khong biet gi ve scene / input (SRP).
    /// Chu tieng Viet trong scene -> Loc tu dich sang English theo LocTable.
    /// </summary>
    public class TitleScreenView : MonoBehaviour
    {
        [Header("Goc")]
        [SerializeField] private CanvasGroup _root;

        [Header("Nen (troi + phong cham)")]
        [SerializeField] private RectTransform _background;
        [SerializeField] private float _bgBaseScale = 1.06f;
        [SerializeField] private float _bgZoom = 0.04f;
        [SerializeField] private float _bgPeriod = 26f;
        [SerializeField] private Vector2 _bgPan = new Vector2(24f, 12f);

        [Header("Tieu de")]
        [SerializeField] private RectTransform _titleGroup;
        [SerializeField] private TMP_Text _title;
        [Tooltip("Ban sao tieu de mau nhan, lech nhe -> vien sac sai; giat manh khi glitch.")]
        [SerializeField] private TMP_Text _titleGhost;
        [SerializeField] private Vector2 _ghostOffset = new Vector2(3f, -2f);
        [SerializeField] private float _ghostAlpha = 0.35f;
        [SerializeField] private float _titleSpacingFrom = 60f;
        [SerializeField] private float _titleSpacingTo = 8f;
        [SerializeField] private float _introTime = 1.8f;
        [SerializeField] private CanvasGroup _subtitle;
        [SerializeField] private RectTransform _lineLeft;
        [SerializeField] private RectTransform _lineRight;
        [SerializeField] private float _lineWidth = 160f;

        [Header("Glitch")]
        [SerializeField] private Vector2 _glitchEvery = new Vector2(3.5f, 7f);
        [SerializeField] private float _glitchShift = 12f;

        [Header("Nap / Nhan phim")]
        [SerializeField] private CanvasGroup _loading;
        [SerializeField] private Image _barFill;
        [SerializeField] private CanvasGroup _prompt;

        [Header("Khac")]
        [SerializeField] private Image _flash;
        [SerializeField] private TMP_Text _version;

        [Header("Thoat (truoc khi sang Login)")]
        [SerializeField] private CanvasGroup _eclipse;
        [SerializeField] private CanvasGroup _particles;
        [SerializeField] private float _exitTime = 0.6f;

        public bool IntroDone { get; private set; }

        private float _shownProgress, _targetProgress;
        private bool _promptOn;
        private float _promptT;
        private float _nextGlitch;
        private bool _glitching;
        private bool _exiting;
        private Vector2 _titleBase;

        private void Awake()
        {
            if (_version != null) _version.text = "v" + Application.version;
            if (_root != null) _root.alpha = 0f;
            if (_subtitle != null) _subtitle.alpha = 0f;
            if (_loading != null) _loading.alpha = 0f;
            if (_prompt != null) _prompt.alpha = 0f;
            if (_barFill != null) _barFill.fillAmount = 0f;
            if (_flash != null) SetAlpha(_flash, 0f);
            if (_title != null) _titleBase = _title.rectTransform.anchoredPosition;
            SetLine(0f);
            ScheduleGlitch();
        }

        // ---------------- API ----------------

        public void PlayIntro() => StartCoroutine(IntroCo());

        public void SetProgress(float p) => _targetProgress = Mathf.Clamp01(p);

        public void ShowPrompt()
        {
            _promptOn = true;
            _promptT = 0f;
        }

        /// <summary>Hieu ung khi bam: chu nhay nhanh, loe sang, logo nay nhe. Xong goi done.</summary>
        public void PlayConfirm(Action done) => StartCoroutine(ConfirmCo(done));

        /// <summary>
        /// [Aemy 2026-10-27] Roi man tieu de: chu / vanh nhat thuc / hat tan dan, nen troi ve dung khung anh cua Login
        /// (scale 1, giua) -> khi crossfade sang Login hai anh nen trung nhau, khong bi "nhay" khung.
        /// </summary>
        public void PlayExit(Action done) => StartCoroutine(ExitCo(done));

        // ---------------- Animation ----------------

        private IEnumerator IntroCo()
        {
            float t = 0f;
            while (t < _introTime)
            {
                t += Time.unscaledDeltaTime;
                float e = EaseOutCubic(Mathf.Clamp01(t / _introTime));
                if (_root != null) _root.alpha = Mathf.Clamp01(t / 0.8f);
                if (_title != null)
                {
                    _title.characterSpacing = Mathf.Lerp(_titleSpacingFrom, _titleSpacingTo, e);
                    _title.alpha = e;
                }
                if (_titleGroup != null) _titleGroup.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, e);

                float s = EaseOutCubic(Mathf.Clamp01((t - _introTime * 0.4f) / (_introTime * 0.5f)));
                if (_subtitle != null) _subtitle.alpha = s;
                if (_loading != null && !_promptOn) _loading.alpha = s;
                SetLine(s);
                SyncGhost();
                yield return null;
            }
            if (_title != null) { _title.characterSpacing = _titleSpacingTo; _title.alpha = 1f; }
            if (_titleGroup != null) _titleGroup.localScale = Vector3.one;
            SetLine(1f);
            SyncGhost();
            IntroDone = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            // Nen: phong + troi rat cham (khong lo mep vi luon >= _bgBaseScale). Luc thoat do ExitCo dieu khien.
            if (_background != null && !_exiting)
            {
                float w = 2f * Mathf.PI / Mathf.Max(1f, _bgPeriod);
                float k = 0.5f + 0.5f * Mathf.Sin(t * w);
                _background.localScale = Vector3.one * (_bgBaseScale + _bgZoom * k);
                _background.anchoredPosition = new Vector2(Mathf.Sin(t * w * 0.77f) * _bgPan.x, Mathf.Cos(t * w * 0.59f) * _bgPan.y);
            }

            // Thanh nap chay muot toi tien do that.
            _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress, dt * 1.5f);
            if (_barFill != null) _barFill.fillAmount = _shownProgress;

            // "Nhan phim bat ky": hien dan roi nhap nho; khoi "Dang tai" tat dan.
            if (_promptOn)
            {
                _promptT += dt;
                float fadeIn = Mathf.Clamp01(_promptT / 0.5f);
                float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Cos(_promptT * 3.2f));
                if (_prompt != null) _prompt.alpha = fadeIn * pulse;
                if (_loading != null) _loading.alpha = Mathf.MoveTowards(_loading.alpha, 0f, dt * 3f);
            }

            if (IntroDone && !_glitching && !_exiting && t >= _nextGlitch) StartCoroutine(GlitchCo());
        }

        private IEnumerator GlitchCo()
        {
            _glitching = true;
            int frames = UnityEngine.Random.Range(3, 6);
            for (int i = 0; i < frames; i++)
            {
                if (_titleGhost != null)
                {
                    _titleGhost.rectTransform.anchoredPosition = _titleBase + new Vector2(
                        UnityEngine.Random.Range(-_glitchShift, _glitchShift), UnityEngine.Random.Range(-_glitchShift * 0.3f, _glitchShift * 0.3f));
                    _titleGhost.alpha = 0.8f;
                }
                if (_title != null)
                    _title.rectTransform.anchoredPosition = _titleBase + new Vector2(UnityEngine.Random.Range(-3f, 3f), 0f);
                yield return new WaitForSecondsRealtime(0.04f);
            }
            if (_title != null) _title.rectTransform.anchoredPosition = _titleBase;
            SyncGhost();
            ScheduleGlitch();
            _glitching = false;
        }

        private IEnumerator ConfirmCo(Action done)
        {
            _promptOn = false;
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (_prompt != null) _prompt.alpha = (Mathf.FloorToInt(t / 0.06f) % 2 == 0) ? 1f : 0.15f;
                if (_flash != null) SetAlpha(_flash, 0.35f * (1f - k));
                if (_titleGroup != null) _titleGroup.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (_prompt != null) _prompt.alpha = 1f;
            if (_flash != null) SetAlpha(_flash, 0f);
            if (_titleGroup != null) _titleGroup.localScale = Vector3.one;
            done?.Invoke();
        }

        private IEnumerator ExitCo(Action done)
        {
            _exiting = true;
            _promptOn = false;
            Vector3 bgScale0 = _background != null ? _background.localScale : Vector3.one;
            Vector2 bgPos0 = _background != null ? _background.anchoredPosition : Vector2.zero;
            float prompt0 = _prompt != null ? _prompt.alpha : 0f;
            float loading0 = _loading != null ? _loading.alpha : 0f;
            float sub0 = _subtitle != null ? _subtitle.alpha : 0f;

            float t = 0f;
            while (t < _exitTime)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float e = EaseInOutCubic(Mathf.Clamp01(t / _exitTime));
                float o = 1f - e;
                if (_background != null)
                {
                    _background.localScale = Vector3.Lerp(bgScale0, Vector3.one, e);
                    _background.anchoredPosition = Vector2.Lerp(bgPos0, Vector2.zero, e);
                }
                if (_title != null) _title.alpha = o;
                if (_titleGhost != null) _titleGhost.alpha = o * _ghostAlpha;
                if (_titleGroup != null) _titleGroup.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, e);
                if (_subtitle != null) _subtitle.alpha = sub0 * o;
                if (_prompt != null) _prompt.alpha = prompt0 * o;
                if (_loading != null) _loading.alpha = loading0 * o;
                SetLine(o);
                if (_eclipse != null)
                {
                    _eclipse.alpha = o;
                    _eclipse.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, e);
                }
                if (_particles != null) _particles.alpha = o;
                yield return null;
            }
            done?.Invoke();
        }

        // ---------------- Helpers ----------------

        private void SyncGhost()
        {
            if (_titleGhost == null || _title == null) return;
            _titleGhost.characterSpacing = _title.characterSpacing;
            _titleGhost.rectTransform.anchoredPosition = _titleBase + _ghostOffset;
            _titleGhost.alpha = _title.alpha * _ghostAlpha;
        }

        private void SetLine(float k)
        {
            if (_lineLeft != null) _lineLeft.sizeDelta = new Vector2(_lineWidth * k, _lineLeft.sizeDelta.y);
            if (_lineRight != null) _lineRight.sizeDelta = new Vector2(_lineWidth * k, _lineRight.sizeDelta.y);
        }

        private void ScheduleGlitch() =>
            _nextGlitch = Time.unscaledTime + UnityEngine.Random.Range(_glitchEvery.x, _glitchEvery.y);

        private static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        private static float EaseOutCubic(float x) { float i = 1f - x; return 1f - i * i * i; }

        private static float EaseInOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
