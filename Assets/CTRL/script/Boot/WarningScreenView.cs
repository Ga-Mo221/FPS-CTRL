// [Aemy 2026-10-27] FILE MOI. Man canh bao nen den truoc man tieu de (scene Boot).
using System;
using System.Collections;
using UnityEngine;

namespace Game.Boot
{
    /// <summary>
    /// Hien lan luot cac trang canh bao tren nen den:
    ///  - Trang co firstLaunchOnly = false (vd CANH BAO DONG KINH): hien MOI lan mo game.
    ///  - Trang co firstLaunchOnly = true (ban dang phat trien, tuong tac truc tuyen, tai nghe): chi lan dau
    ///    (luu PlayerPrefs theo may). Doi noi dung quan trong -> tang SeenKey (v2...) de hien lai cho moi nguoi.
    /// Moi trang tu qua sau `duration`; sau `_skipLockTime` giay thi bam phim bat ky de qua som.
    /// Chi lo hien thi + thoi gian (SRP): nghe phim qua AnyKeyListener, BootSequence quyet dinh khi nao goi.
    /// </summary>
    public class WarningScreenView : MonoBehaviour
    {
        public const string SeenKey = "EclipseStrike.BootNoticesSeen.v1";

        [Serializable]
        public class Page
        {
            public CanvasGroup group;
            [Tooltip("Tu chuyen trang sau (giay).")]
            public float duration = 5f;
            [Tooltip("Chi hien lan dau mo game tren may nay.")]
            public bool firstLaunchOnly;
        }

        [SerializeField] private CanvasGroup _root;
        [SerializeField] private Page[] _pages = new Page[0];
        [Tooltip("\"NHAN PHIM BAT KY DE TIEP TUC\" - hien khi da cho bo qua.")]
        [SerializeField] private CanvasGroup _continueHint;
        [SerializeField] private float _skipLockTime = 1f;
        [SerializeField] private float _fadeTime = 0.45f;

        private void Awake()
        {
            if (_root != null) { _root.alpha = 1f; _root.blocksRaycasts = false; }
            foreach (var p in _pages) if (p != null && p.group != null) p.group.alpha = 0f;
            if (_continueHint != null) _continueHint.alpha = 0f;
        }

        /// <summary>Chay het cac trang can hien; xong thi nen den mo dan (man tieu de hien len sau).</summary>
        public IEnumerator Play(AnyKeyListener input)
        {
            bool seen = false;
            try { seen = PlayerPrefs.GetInt(SeenKey, 0) == 1; } catch { }
            bool showedFirstLaunch = false;

            foreach (var page in _pages)
            {
                if (page == null || page.group == null) continue;
                if (page.firstLaunchOnly && seen) continue;
                yield return ShowPage(page, input);
                if (page.firstLaunchOnly) showedFirstLaunch = true;
            }

            if (showedFirstLaunch)
            {
                try { PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save(); } catch { }
            }

            yield return Fade(_root, 0f, _fadeTime);
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private IEnumerator ShowPage(Page page, AnyKeyListener input)
        {
            yield return Fade(page.group, 1f, _fadeTime);

            float t = 0f;
            bool skip = false, listening = false;
            while (t < page.duration && !skip)
            {
                t += Time.unscaledDeltaTime;
                if (!listening && t >= _skipLockTime && input != null)
                {
                    listening = true;
                    input.Listen(() => skip = true);
                }
                if (_continueHint != null)
                {
                    float target = listening ? 0.45f + 0.35f * (0.5f + 0.5f * Mathf.Cos((t - _skipLockTime) * 3f)) : 0f;
                    _continueHint.alpha = Mathf.MoveTowards(_continueHint.alpha, target, Time.unscaledDeltaTime * 3f);
                }
                yield return null;
            }
            if (input != null) input.Stop();

            float hint0 = _continueHint != null ? _continueHint.alpha : 0f;
            float e = 0f;
            while (e < _fadeTime)
            {
                e += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float k = 1f - Mathf.Clamp01(e / _fadeTime);
                page.group.alpha = k;
                if (_continueHint != null) _continueHint.alpha = hint0 * k;
                yield return null;
            }
            page.group.alpha = 0f;
        }

        private static IEnumerator Fade(CanvasGroup g, float target, float time)
        {
            if (g == null) yield break;
            float from = g.alpha, t = 0f;
            while (t < time)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                g.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / time));
                yield return null;
            }
            g.alpha = target;
        }
    }
}
