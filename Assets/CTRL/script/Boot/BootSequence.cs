// [Aemy 2026-10-26] FILE MOI. Scene Boot: man tieu de "Nhan phim bat ky de bat dau" truoc Login.
using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.UI;
using UnityEngine;

namespace Game.Boot
{
    /// <summary>
    /// Dieu phoi scene Boot:
    /// 1. Man tieu de chay intro; cac IBootStep chay ngam (warmup shader, nap san Login).
    /// 2. Xong intro + xong nap -> hien "Nhan phim bat ky de bat dau".
    /// 3. Bam phim -> am xac nhan + hieu ung -> SceneFader toi dan -> bat Login (da nap san).
    /// Chi dieu phoi: hien thi o TitleScreenView, nghe phim o AnyKeyListener, nap scene o ScenePreloadStep (SRP).
    /// Nhac nen: UISfxManager (scene "Boot" nam trong UISfxLibrary.musicScenes) -> nhac menu noi lien sang Login.
    /// </summary>
    public class BootSequence : MonoBehaviour
    {
        [SerializeField] private string _nextScene = "Login_Register";
        [SerializeField] private TitleScreenView _view;
        [SerializeField] private AnyKeyListener _input;
        [Tooltip("Man canh bao truoc man tieu de (de trong = bo qua).")]
        [SerializeField] private WarningScreenView _warnings;
        [Tooltip("Canvas man tieu de mang sang Login roi mo dan (crossfade). De trong = toi den roi sang (cach cu).")]
        [SerializeField] private Game.UI.SceneCrossfadeCarrier _crossfade;
        [Tooltip("Tuy chon: bien dich truoc shader khi dang o man tieu de.")]
        [SerializeField] private ShaderVariantCollection _shaderWarmup;
        [Tooltip("Thoi gian toi thieu truoc khi cho bam (de intro chay het).")]
        [SerializeField] private float _minTitleTime = 2.2f;
        [SerializeField] private float _fadeOutTime = 0.6f;

        private IEnumerator Start()
        {
            // [Aemy 2026-10-27] Nap ngam (warmup + Login) chay SONG SONG ngay tu dau -> thoi gian doc canh bao khong phi.
            var preload = new ScenePreloadStep(_nextScene);
            var steps = new List<IBootStep> { new ShaderWarmupStep(_shaderWarmup), preload };
            bool stepsDone = false;
            StartCoroutine(RunStepsThen(steps, () => stepsDone = true));

            // Canh bao (dong kinh moi lan; luu y khac lan dau). Nhac nen giu im toi khi man tieu de hien.
            if (_warnings != null)
            {
                UISfxManager.HoldMusic(true);
                yield return _warnings.Play(_input);
                UISfxManager.HoldMusic(false);
            }

            float startTime = Time.unscaledTime;
            _view.PlayIntro();
            while (!stepsDone) yield return null;
            while (!_view.IntroDone || Time.unscaledTime - startTime < _minTitleTime) yield return null;

            _view.ShowPrompt();
            Debug.Log("[Boot] San sang, cho nhan phim (t=" + Time.unscaledTime.ToString("0.0") + ")");
            bool pressed = false;
            _input.Listen(() => pressed = true);
            while (!pressed) yield return null;

            UISfxManager.Play(UISfxType.Confirm);
            bool confirmed = false;
            _view.PlayConfirm(() => confirmed = true);
            while (!confirmed) yield return null;

            // [Aemy 2026-10-27] Crossfade: chu/hieu ung tan, nen ve dung khung Login, mang canvas sang Login roi mo dan.
            bool exited = false;
            _view.PlayExit(() => exited = true);
            while (!exited) yield return null;

            if (_crossfade != null)
            {
                _crossfade.Carry();
            }
            else
            {
                var fader = SceneFader.Instance;
                if (fader != null)
                {
                    bool dark = false;
                    fader.FadeOut(() => dark = true, _fadeOutTime);
                    while (!dark) yield return null;
                }
                // SceneFader tu sang lai khi Login load xong.
            }
            Debug.Log("[Boot] Vao " + _nextScene);
            preload.Activate();
        }

        private IEnumerator RunStepsThen(List<IBootStep> steps, System.Action done)
        {
            yield return RunSteps(steps);
            done?.Invoke();
        }

        private IEnumerator RunSteps(List<IBootStep> steps)
        {
            float total = 0f;
            foreach (var s in steps) total += s.Weight;
            if (total <= 0f) total = 1f;

            float done = 0f;
            foreach (var step in steps)
            {
                float w = step.Weight;
                yield return step.Run(p => _view.SetProgress((done + Mathf.Clamp01(p) * w) / total));
                done += w;
            }
            _view.SetProgress(1f);
        }
    }
}
