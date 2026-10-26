
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Audio
{
    /// <summary>
    /// - Tu tao 1 lan khi game chay (RuntimeInitializeOnLoadMethod), DontDestroyOnLoad.
    /// - Moi scene moi + quet dinh ky: gan <see cref="UIButtonSfx"/> cho moi Selectable chua co (ke ca panel dang an,
    ///   nut sinh luc chay). Nut da gan tay UIButtonSfx trong scene thi giu nguyen cau hinh do.
    /// - Nhac nen: scene nam trong UISfxLibrary.musicScenes -> phat lap (fade in); scene khac -> fade out.
    /// - Am luong theo mixer: UI -> group UI (slider "UI & Announcer"), nhac -> group Music.
    /// </summary>
    public class UISfxManager : MonoBehaviour
    {
        private static UISfxManager _instance;
        private UISfxLibrary _lib;
        private AudioSource _sfx, _music;
        private float _lastPlay = -1f, _lastClick = -1f, _scanTimer;
        private Coroutine _fade;
        private static bool _held; // [Aemy 2026-10-27] tam giu nhac (vd man canh bao truoc man tieu de)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _held = false; }

        /// <summary>
        /// [Aemy 2026-10-27] Giu nhac nen im (hold = true) du scene co trong musicScenes; tha ra -> cap nhat lai theo scene.
        /// Boot: im trong luc hien canh bao, nhac menu bat dau dung luc man tieu de hien.
        /// </summary>
        public static void HoldMusic(bool hold)
        {
            _held = hold;
            if (_instance != null) _instance.UpdateMusic(SceneManager.GetActiveScene().name);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var lib = UISfxLibrary.Load();
            if (lib == null) return; // chua co asset -> khong lam gi
            var go = new GameObject("~UISfxManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UISfxManager>();
            _instance.Init(lib);
        }

        private void Init(UISfxLibrary lib)
        {
            _lib = lib;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false; _sfx.spatialBlend = 0f; _sfx.outputAudioMixerGroup = lib.uiGroup;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false; _music.spatialBlend = 0f; _music.loop = true; _music.volume = 0f;
            _music.outputAudioMixerGroup = lib.musicGroup; _music.clip = lib.menuMusic;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (_instance == this) _instance = null; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachAll();
            if (mode == LoadSceneMode.Single) UpdateMusic(scene.name);
        }

        private void Update()
        {
            _scanTimer += Time.unscaledDeltaTime;
            if (_scanTimer >= 1.5f) { _scanTimer = 0f; AttachAll(); }
        }

        // ---- Gan am cho nut ----
        private void AttachAll()
        {
            if (_lib == null) return;
            var all = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in all)
            {
                if (s == null || s is Scrollbar) continue;
                if (s.GetComponent<UIButtonSfx>() != null) continue;
                var c = s.gameObject.AddComponent<UIButtonSfx>();
                c.clickType = _lib.ClassifyClick(s);
            }
        }

        // ---- API ----
        public static void Play(UISfxType type)
        {
            if (_instance != null) _instance.PlayInternal(type);
        }

        /// <summary>Am "di chuyen" (hover chuot / tay cam doi o). Bo qua neu vua bam (tranh 2 tieng chong).</summary>
        public static void PlayHover()
        {
            if (_instance == null) return;
            if (Time.unscaledTime - _instance._lastClick < 0.15f) return;
            _instance.PlayInternal(UISfxType.Hover);
        }

        private void PlayInternal(UISfxType type)
        {
            if (_lib == null || type == UISfxType.None) return;
            var e = _lib.Get(type);
            if (e == null && type != UISfxType.Click && type != UISfxType.Hover) e = _lib.Get(UISfxType.Click); // thieu clip -> dung Click
            if (e == null) return;
            float now = Time.unscaledTime;
            if (now - _lastPlay < _lib.minInterval) return;
            _lastPlay = now;
            if (type != UISfxType.Hover) _lastClick = now;
            _sfx.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            _sfx.PlayOneShot(e.clip, e.volume);
        }

        // ---- Nhac nen ----
        private void UpdateMusic(string sceneName)
        {
            if (_lib == null || _lib.menuMusic == null) return;
            bool want = !_held && _lib.musicScenes.Contains(sceneName);
            if (_fade != null) StopCoroutine(_fade);
            if (want)
            {
                if (!_music.isPlaying) { _music.clip = _lib.menuMusic; _music.volume = 0f; _music.Play(); }
                _fade = StartCoroutine(FadeTo(_lib.musicVolume, _lib.musicFadeIn, false));
            }
            else if (_music.isPlaying) _fade = StartCoroutine(FadeTo(0f, _lib.musicFadeOut, true));
        }

        private IEnumerator FadeTo(float target, float time, bool stopAtEnd)
        {
            float start = _music.volume, t = 0f;
            while (t < time) { t += Time.unscaledDeltaTime; _music.volume = Mathf.Lerp(start, target, time > 0f ? t / time : 1f); yield return null; }
            _music.volume = target;
            if (stopAtEnd) _music.Stop();
            _fade = null;
        }
    }
}
