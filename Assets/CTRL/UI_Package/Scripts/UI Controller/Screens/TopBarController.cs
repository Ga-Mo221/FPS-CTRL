using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// The persistent top bar shared across every menu tab: the 4 tab buttons plus
    /// the player summary (name / level / coins / avatar). It raises
    /// <see cref="TabSelected"/>; <see cref="ShellController"/> listens and swaps the
    /// content panel below. Presentation only — it never talks to PlayFab/Fusion.
    /// </summary>
    public class TopBarController : MonoBehaviour
    {
        public enum MenuTab { Play, Loadout, Shop, Settings, Character } // [Aemy 2026-10-10] + Character (tab NHAN VAT)

        [Header("Tabs")]
        [Tooltip("Optional radio group over the 4 tabs (keeps the active one highlighted).")]
        [SerializeField] private UISelectionGroup _tabGroup;
        [SerializeField] private Button _playTab;
        [SerializeField] private Button _loadoutTab;
        [SerializeField] private Button _shopTab;
        [SerializeField] private Button _settingsTab;
        [SerializeField] private Button _characterTab; // [Aemy 2026-10-10]

        [Header("Player info")]
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_Text _level;
        [SerializeField] private TMP_Text _coins;
        [SerializeField] private Image _avatar;

        /// <summary>Raised whenever a tab is chosen.</summary>
        public event Action<MenuTab> TabSelected;

        public MenuTab Current { get; private set; } = MenuTab.Play;

        private void Awake()
        {
            if (_tabGroup != null) _tabGroup.SelectionChanged += OnGroupChanged;
            Hook(_playTab, MenuTab.Play);
            Hook(_loadoutTab, MenuTab.Loadout);
            Hook(_shopTab, MenuTab.Shop);
            Hook(_settingsTab, MenuTab.Settings);
            Hook(_characterTab, MenuTab.Character); // [Aemy 2026-10-10]
        }

        private void OnDestroy()
        {
            if (_tabGroup != null) _tabGroup.SelectionChanged -= OnGroupChanged;
        }

        private void OnGroupChanged(IReadOnlyList<UISelectable> selected)
        {
            if (selected.Count > 0) Select((MenuTab)selected[0].Index);
        }

        private void Hook(Button b, MenuTab tab)
        {
            if (b != null) b.onClick.AddListener(() => Select(tab));
        }

        /// <summary>Select a tab programmatically (also used by the tab buttons).</summary>
        public void Select(MenuTab tab)
        {
            Current = tab;
            TabSelected?.Invoke(tab);
        }

        /// <summary>[Aemy 2026-10-11] Mo tab nhu nguoi choi bam nut (nut + radio group + highlight dong bo). Khong co nut -> Select.</summary>
        public void Open(MenuTab tab)
        {
            Button b = tab == MenuTab.Play ? _playTab : tab == MenuTab.Loadout ? _loadoutTab : tab == MenuTab.Shop ? _shopTab
                     : tab == MenuTab.Settings ? _settingsTab : _characterTab;
            if (b != null && b.isActiveAndEnabled) b.onClick.Invoke(); else Select(tab);
        }

        // ---- Player summary setters ----
        public void SetPlayerName(string value) { if (_playerName != null) _playerName.text = value; }
        public void SetLevel(int level) { if (_level != null) _level.text =$"Level: {level.ToString()}"; }
        public void SetCoins(long coins) { if (_coins != null) _coins.text = coins.ToString("N0"); }
        public void SetAvatar(Sprite sprite) { if (_avatar != null && sprite != null) _avatar.sprite = sprite; }
    }
}
