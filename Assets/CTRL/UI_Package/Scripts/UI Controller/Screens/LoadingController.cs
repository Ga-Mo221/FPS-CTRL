using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Drives the Loading scene: async-loads the target scene (from
    /// <see cref="SceneLoader.NextScene"/>), animates the progress bar, and swaps in
    /// when ready — but not before a minimum on-screen time so it never just flashes.
    /// Unity's async progress caps at 0.9 until you allow activation, so we normalize
    /// it and drive activation ourselves once both the load and the min time are done.
    /// </summary>
    public class LoadingController : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private TMP_Text _percentLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private string _fallbackScene = "MenuShell";
        [SerializeField] private float _minDuration = 1.5f;
        private bool _activating; // [#18]

        private IEnumerator Start()
        {
            string target = string.IsNullOrEmpty(SceneLoader.NextScene) ? _fallbackScene : SceneLoader.NextScene;
            if (_statusLabel != null) _statusLabel.text = "ĐANG TẢI…";
            SetProgress(0f);

            var op = SceneManager.LoadSceneAsync(target);
            op.allowSceneActivation = false;

            float elapsed = 0f;
            while (!op.isDone)
            {
                elapsed += Time.deltaTime;
                float loadProgress = Mathf.Clamp01(op.progress / 0.9f);
                float timeProgress = _minDuration > 0f ? Mathf.Clamp01(elapsed / _minDuration) : 1f;
                SetProgress(Mathf.Min(loadProgress, timeProgress));

                if (op.progress >= 0.9f && elapsed >= _minDuration && !_activating)
                {
                    _activating = true;
                    SetProgress(1f);
                    if (_statusLabel != null) _statusLabel.text = "SẴN SÀNG";
                    { var fd = SceneFader.Instance; if (fd != null) fd.FadeOut(() => op.allowSceneActivation = true); else op.allowSceneActivation = true; } // [#18] fade truoc khi vao scene dich
                }
                yield return null;
            }
        }

        private void SetProgress(float value)
        {
            if (_fill != null) _fill.fillAmount = value;
            if (_percentLabel != null) _percentLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
