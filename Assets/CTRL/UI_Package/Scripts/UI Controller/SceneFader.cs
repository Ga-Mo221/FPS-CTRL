// [UI-INTEGRATION 2026-09-13 - Aemy] FILE MỚI. Fade chuyển scene cho mượt (MenuShell -> SampleScene).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// Lớp phủ đen fade cả màn, sống xuyên scene (DontDestroyOnLoad). Đặt prefab ở Login.
    /// - FadeOut(cb): tối dần rồi gọi cb (dùng ngay trước khi load scene).
    /// - Tự FadeIn sau khi 1 scene mới load xong (nếu đang tối) -> hết cảm giác "cứng" khi vào trận.
    /// Không đụng code project chính; chỉ là overlay UI độc lập.
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _defaultDuration = 0.45f;

        private Coroutine _co;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("~SceneFader");
            go.AddComponent<SceneFader>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (_group == null) _group = GetComponentInChildren<CanvasGroup>(true);
            if (_group == null) BuildOverlay(); // [#18 2026-09-29] tu dung overlay neu chua co prefab
            if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("FaderCanvas", typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            _group = canvasGo.GetComponent<CanvasGroup>();
            var imgGo = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = imgGo.GetComponent<UnityEngine.UI.Image>(); img.color = Color.black; img.raycastTarget = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        // Sau khi scene mới load xong: nếu đang tối thì sáng dần lên.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_group != null && _group.alpha > 0.01f) FadeIn();
        }

        public void FadeOut(Action onDone = null, float duration = -1f)
        {
            StartFade(1f, duration < 0f ? _defaultDuration : duration, onDone);
        }

        public void FadeIn(float duration = -1f)
        {
            StartFade(0f, duration < 0f ? _defaultDuration : duration, null);
        }

        private void StartFade(float target, float duration, Action onDone)
        {
            if (!isActiveAndEnabled || _group == null) { onDone?.Invoke(); return; }
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(FadeCo(target, duration, onDone));
        }

        private IEnumerator FadeCo(float target, float duration, Action onDone)
        {
            _group.blocksRaycasts = true;
            float from = _group.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, target, duration <= 0f ? 1f : t / duration);
                yield return null;
            }
            _group.alpha = target;
            if (target <= 0.01f) _group.blocksRaycasts = false;
            _co = null;
            onDone?.Invoke();
        }
    }
}
