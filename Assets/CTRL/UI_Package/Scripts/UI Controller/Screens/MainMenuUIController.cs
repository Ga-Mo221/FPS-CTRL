using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the main menu.
    /// Exposes events for every action and public setters to feed in data.
    /// It never talks to networking/PlayFab directly (that is wired by a higher layer).
    /// </summary>
    public class MainMenuUIController : MonoBehaviour
    {
        public enum MenuTab { Play, Loadout, Shop, Settings }

        [Header("Top tabs")]
        [Tooltip("Radio group over the 4 tabs (keeps the active one highlighted). Assign this.")]
        [SerializeField] private UISelectionGroup _tabGroup;
        [SerializeField] private Button _playTabButton;
        [SerializeField] private Button _loadoutTabButton;
        [SerializeField] private Button _shopTabButton;
        [SerializeField] private Button _settingsTabButton;

        [Header("Player info")]
        [SerializeField] private TMP_Text _playerNameLabel;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _coinsLabel;
        [SerializeField] private Image _avatarImage;

        [Header("Mode & Map (radio = pick 1)")]
        [SerializeField] private UISelectionGroup _modeGroup;
        [SerializeField] private UISelectionGroup _mapGroup;
        [SerializeField] private TMP_Text _modeTitleLabel;
        [SerializeField] private TMP_Text _modeDescriptionLabel;

        [Header("Actions")]
        [SerializeField] private Button _readyButton;
        [SerializeField] private Button _startButton;

        [Header("Status")]
        [SerializeField] private TMP_Text _onlineCountLabel;
        [SerializeField] private TMP_Text _queueStatusLabel;
        [SerializeField] private TMP_Text _serverNameLabel;
        [SerializeField] private TMP_Text _pingLabel;

        // ---- Events (subscribe from a game manager) ----
        public event Action<MenuTab> TabSelected;
        public event Action<int> ModeSelected;   // index in _modeGroup
        public event Action<int> MapSelected;     // index in _mapGroup
        public event Action ReadyClicked;
        public event Action StartClicked;

        private void Awake()
        {
            if (_tabGroup != null)
            {
                _tabGroup.SelectionChanged += OnTabChanged;
            }
            else
            {
                Hook(_playTabButton, () => TabSelected?.Invoke(MenuTab.Play));
                Hook(_loadoutTabButton, () => TabSelected?.Invoke(MenuTab.Loadout));
                Hook(_shopTabButton, () => TabSelected?.Invoke(MenuTab.Shop));
                Hook(_settingsTabButton, () => TabSelected?.Invoke(MenuTab.Settings));
            }

            Hook(_readyButton, () => ReadyClicked?.Invoke());
            Hook(_startButton, () => StartClicked?.Invoke());

            if (_modeGroup != null) _modeGroup.SelectionChanged += OnModeChanged;
            if (_mapGroup != null) _mapGroup.SelectionChanged += OnMapChanged;
        }

        private void OnDestroy()
        {
            if (_tabGroup != null) _tabGroup.SelectionChanged -= OnTabChanged;
            if (_modeGroup != null) _modeGroup.SelectionChanged -= OnModeChanged;
            if (_mapGroup != null) _mapGroup.SelectionChanged -= OnMapChanged;
        }

        private void OnTabChanged(System.Collections.Generic.IReadOnlyList<UISelectable> sel)
        {
            if (sel.Count > 0) TabSelected?.Invoke((MenuTab)sel[0].Index);
        }


        private void OnModeChanged(System.Collections.Generic.IReadOnlyList<UISelectable> sel)
        {
            if (sel.Count > 0) ModeSelected?.Invoke(sel[0].Index);
        }

        private void OnMapChanged(System.Collections.Generic.IReadOnlyList<UISelectable> sel)
        {
            if (sel.Count > 0) MapSelected?.Invoke(sel[0].Index);
        }

        // ---- Public setters (presentation) ----
        public void SetPlayerName(string value) => SetText(_playerNameLabel, value);
        public void SetLevel(int level) => SetText(_levelLabel, level.ToString());
        public void SetCoins(long coins) => SetText(_coinsLabel, coins.ToString("N0"));
        public void SetAvatar(Sprite sprite) { if (_avatarImage != null && sprite != null) _avatarImage.sprite = sprite; }

        public void SetModeDescription(string title, string description)
        {
            SetText(_modeTitleLabel, title);
            SetText(_modeDescriptionLabel, description);
        }

        public void SetOnlineCount(int count) => SetText(_onlineCountLabel, count.ToString("N0"));
        public void SetQueueStatus(string status) => SetText(_queueStatusLabel, status);
        public void SetServer(string serverName, int ping)
        {
            SetText(_serverNameLabel, serverName);
            SetText(_pingLabel, $"{ping} ms");
        }

        public void SetStartInteractable(bool value) { if (_startButton != null) _startButton.interactable = value; }

        // ---- Helpers ----
        private static void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
