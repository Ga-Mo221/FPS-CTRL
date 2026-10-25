using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Composition root of the main-menu shell. Owns the persistent
    /// <see cref="TopBarController"/> and a set of content panels, and shows exactly
    /// one panel at a time based on the selected tab.
    ///
    /// Open/Closed: to add a tab, add a <see cref="TopBarController.MenuTab"/> value
    /// and a panel entry in the inspector array — no code changes here.
    /// </summary>
    public class ShellController : MonoBehaviour
    {
        [SerializeField] private TopBarController _topBar;

        [Tooltip("Content panels indexed by MenuTab: 0=Play, 1=Loadout, 2=Shop, 3=Settings.")]
        [SerializeField] private GameObject[] _panels = new GameObject[4];

        [SerializeField] private TopBarController.MenuTab _defaultTab = TopBarController.MenuTab.Play;

        private void Awake()
        {
            if (_topBar != null) _topBar.TabSelected += Show;
        }

        private void OnDestroy()
        {
            if (_topBar != null) _topBar.TabSelected -= Show;
        }

        private void Start() => Show(_defaultTab);

        /// <summary>Activate the panel for <paramref name="tab"/> and hide the rest.</summary>
        public void Show(TopBarController.MenuTab tab)
        {
            int index = (int)tab;
            for (int i = 0; i < _panels.Length; i++)
                if (_panels[i] != null) _panels[i].SetActive(i == index);
        }
    }
}
