// [Aemy 2026-10-27] FILE MOI. Chuyen scene kieu crossfade (khong qua man den).
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// Mang 1 Canvas cua scene cu sang scene moi (DontDestroyOnLoad), giu tren cung, cho scene moi qua
    /// may frame nap nang (Awake/Start, PlayFab, shader...) roi moi mo dan -> scene moi hien len tu tu ben duoi.
    /// Khong biet gi ve Boot / Login (SRP): gan len root Canvas nao cung dung duoc.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SceneCrossfadeCarrier : MonoBehaviour
    {
        [SerializeField] private float _fadeTime = 0.7f;
        [Tooltip("Dat tren UI scene moi (Login 50, fader 32760).")]
        [SerializeField] private int _sortingOrder = 30000;
        [Tooltip("So frame lien tiep 'muot' can co truoc khi bat dau mo.")]
        [SerializeField] private int _stableFrames = 3;
        [Tooltip("Frame duoc coi la muot neu ngan hon (giay).")]
        [SerializeField] private float _stableFrameTime = 0.05f;
        [Tooltip("Cho toi da (giay) roi mo du chua muot.")]
        [SerializeField] private float _maxWait = 2f;

        private CanvasGroup _group;
        private bool _carrying;

        private void Awake() { _group = GetComponent<CanvasGroup>(); }

        /// <summary>Goi NGAY TRUOC khi bat scene moi (Single). Canvas o lai toi khi scene moi on dinh roi mo dan.</summary>
        public void Carry()
        {
            if (_carrying) return;
            _carrying = true;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            GetComponent<Canvas>().sortingOrder = _sortingOrder;
            _group.blocksRaycasts = false; // khong chan click / tay cam cua scene moi
            _group.interactable = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StartCoroutine(FadeAway());
        }

        private IEnumerator FadeAway()
        {
            // Cho scene moi qua cac frame dau (thuong giat) -> hieu ung mo khong bi nhay coc.
            float waited = 0f;
            int stable = 0;
            while (stable < _stableFrames && waited < _maxWait)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                waited += dt;
                stable = dt <= _stableFrameTime ? stable + 1 : 0;
            }

            float a0 = _group.alpha, t = 0f;
            while (t < _fadeTime)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f); // 1 frame giat khong lam nhay coc
                float k = Mathf.Clamp01(t / _fadeTime);
                _group.alpha = a0 * (1f - k * k * (3f - 2f * k));
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    }
}
