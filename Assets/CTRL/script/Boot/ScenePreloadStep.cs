// [Aemy 2026-10-26] FILE MOI.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Boot
{
    /// <summary>
    /// Nap ngam scene tiep theo (Login) nhung CHUA bat len (allowSceneActivation = false).
    /// Nguoi choi bam phim -> Activate() -> vao Login ngay, khong co khoang den cho load.
    /// </summary>
    public sealed class ScenePreloadStep : IBootStep
    {
        private readonly string _scene;
        private AsyncOperation _op;

        public ScenePreloadStep(string scene) { _scene = scene; }

        public string Label => "Preload " + _scene;
        public float Weight => 3f;

        public IEnumerator Run(Action<float> report)
        {
            _op = SceneManager.LoadSceneAsync(_scene, LoadSceneMode.Single);
            if (_op == null)
            {
                Debug.LogError($"[Boot] Khong nap duoc scene '{_scene}' (chua co trong Build Settings?)");
                report(1f);
                yield break;
            }
            _op.allowSceneActivation = false;
            // allowSceneActivation = false -> progress dung o 0.9 cho toi khi Activate().
            while (_op.progress < 0.9f)
            {
                report(_op.progress / 0.9f);
                yield return null;
            }
            report(1f);
        }

        /// <summary>Bat scene da nap san (goi sau khi man hinh da toi).</summary>
        public void Activate()
        {
            if (_op != null) _op.allowSceneActivation = true;
            else SceneManager.LoadScene(_scene);
        }
    }
}
