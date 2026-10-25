using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Left-nav switcher inside the Settings panel. Listens to the "Chức năng"
    /// <see cref="UISelectionGroup"/> and shows exactly one content panel
    /// (Graphics / Audio / Mouse / Keys / Crosshair / Account) at a time.
    /// Same one-of-many pattern as <see cref="ShellController"/>, scoped to Settings.
    ///
    /// LEAD - cach wire (Inspector): _navGroup = UISelectionGroup cua hang tab (Nav);
    /// _panels = mang cac muc noi dung, PHAI cung so luong va CUNG THU TU voi cac tab trong _navGroup
    /// (vd tab 0 = Do hoa -> _panels[0] = Do hoa). Awake tu subscribe, Start hien _defaultIndex.
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private UISelectionGroup _navGroup;

        [Tooltip("Content panels aligned 1:1 with the nav group items, in the same order.")]
        [SerializeField] private GameObject[] _panels;

        [SerializeField] private int _defaultIndex = 0;

        private void Awake()
        {
            if (_navGroup != null) _navGroup.SelectionChanged += OnNavChanged;
        }

        private void OnDestroy()
        {
            if (_navGroup != null) _navGroup.SelectionChanged -= OnNavChanged;
        }

        private void Start()
        {
            if (_navGroup != null) _navGroup.SelectByIndex(_defaultIndex);
            else ShowOnly(_defaultIndex);
        }

        private void OnNavChanged(IReadOnlyList<UISelectable> selected)
        {
            if (selected.Count > 0) ShowOnly(selected[0].Index);
        }

        /// <summary>Activate the panel at <paramref name="index"/>, hide the rest.</summary>
        public void ShowOnly(int index)
        {
            for (int i = 0; i < _panels.Length; i++)
                if (_panels[i] != null) _panels[i].SetActive(i == index);
        }
    }
}
