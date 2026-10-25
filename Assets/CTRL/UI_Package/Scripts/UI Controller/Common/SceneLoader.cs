using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// Tiny scene-flow helper. Callers say "go to scene X (through the loading
    /// screen)"; it remembers the target and switches to the Loading scene, whose
    /// <see cref="LoadingController"/> then async-loads X and swaps in when ready.
    /// A static field is enough because only one transition is in flight at a time.
    /// </summary>
    public static class SceneLoader
    {
        /// <summary>The scene the Loading screen should load next (set by callers).</summary>
        public static string NextScene { get; private set; }

        public static void LoadWithLoading(string nextScene, string loadingScene = "Loading")
        {
            NextScene = nextScene;
            { var fd = SceneFader.Instance; if (fd != null) fd.FadeOut(() => SceneManager.LoadScene(loadingScene)); else SceneManager.LoadScene(loadingScene); } // [#18] fade
        }
    }
}
