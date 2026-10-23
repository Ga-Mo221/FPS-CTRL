using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Manages selection rules across a set of <see cref="UISelectable"/> items.
    /// One reusable component covers every selection pattern in the game:
    ///   maxSelection = 1  -> radio (mode, map, loadout tabs)
    ///   maxSelection = 2  -> "pick up to 2" (primary weapons, utilities)
    ///   maxSelection = 0  -> unlimited multi-select
    /// Open/Closed: new behaviours are configured via fields, not by editing callers.
    /// </summary>
    public class UISelectionGroup : MonoBehaviour
    {
        public enum OverflowPolicy
        {
            /// <summary>Ignore the new click once the limit is reached.</summary>
            RejectNew,
            /// <summary>Deselect the oldest selection to make room for the new one.</summary>
            ReplaceOldest
        }

        [Header("Items")]
        [Tooltip("The selectables managed by this group, in display order.")]
        [SerializeField] private List<UISelectable> _items = new List<UISelectable>();

        [Header("Rules")]
        [Tooltip("Max items selectable at once. 1 = radio, 0 = unlimited.")]
        [Min(0)][SerializeField] private int _maxSelection = 1;

        [Tooltip("What to do when the user picks a new item while at the limit.")]
        [SerializeField] private OverflowPolicy _overflowPolicy = OverflowPolicy.ReplaceOldest;

        [Tooltip("If true, clicking a selected item deselects it. For a radio you usually want this OFF so one stays chosen.")]
        [SerializeField] private bool _allowToggleOff = true;

        [Tooltip("Index selected on Start (-1 = none). Handy for radios that need a default.")]
        [SerializeField] private int _defaultIndex = -1;

        /// <summary>Raised whenever the selection changes. Argument = currently selected items (selection order).</summary>
        public event Action<IReadOnlyList<UISelectable>> SelectionChanged;

        // Ordered by selection time so ReplaceOldest works.
        private readonly List<UISelectable> _selected = new List<UISelectable>();

        public IReadOnlyList<UISelectable> Selected => _selected;
        public int MaxSelection => _maxSelection;

        private void Awake()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null) continue;
                item.Index = i;
                item.Clicked += OnItemClicked;
            }
        }

        private void Start()
        {
            if (_defaultIndex >= 0 && _defaultIndex < _items.Count)
                SelectByIndex(_defaultIndex, notify: false);
        }

        private void OnDestroy()
        {
            foreach (var item in _items)
                if (item != null) item.Clicked -= OnItemClicked;
        }

        private void OnItemClicked(UISelectable item)
        {
            if (item.IsSelected)
            {
                if (_allowToggleOff)
                    Deselect(item, notify: true);
                // radio with toggle-off disabled: clicking the active item does nothing
                return;
            }

            if (_maxSelection > 0 && _selected.Count >= _maxSelection)
            {
                if (_maxSelection == 1)
                {
                    // Radio: swap.
                    Deselect(_selected[0], notify: false);
                }
                else if (_overflowPolicy == OverflowPolicy.ReplaceOldest)
                {
                    Deselect(_selected[0], notify: false);
                }
                else // RejectNew
                {
                    return;
                }
            }

            Select(item, notify: true);
        }

        private void Select(UISelectable item, bool notify)
        {
            if (item == null || item.IsLocked || _selected.Contains(item)) return;
            item.SetSelected(true);
            _selected.Add(item);
            RefreshOrderBadges();
            if (notify) RaiseChanged();
        }

        private void Deselect(UISelectable item, bool notify)
        {
            if (item == null || !_selected.Contains(item)) return;
            item.SetSelected(false);
            _selected.Remove(item);
            RefreshOrderBadges();
            if (notify) RaiseChanged();
        }

        // [ORDER-BADGE 2026-09-29 Aemy] So thu tu chon (chi nhom multi-select).
        private void RefreshOrderBadges()
        {
            if (_maxSelection == 1) return;
            for (int i = 0; i < _items.Count; i++) if (_items[i] != null) _items[i].SetOrder(0);
            for (int i = 0; i < _selected.Count; i++) if (_selected[i] != null) _selected[i].SetOrder(i + 1);
        }

        private void RaiseChanged() => SelectionChanged?.Invoke(_selected);

        // ---- Public API (for controllers / external code) ----

        public void SelectByIndex(int index, bool notify = true)
        {
            if (index < 0 || index >= _items.Count) return;
            var item = _items[index];
            if (item == null || item.IsSelected) return;

            if (_maxSelection == 1 && _selected.Count > 0)
                Deselect(_selected[0], notify: false);

            Select(item, notify);
        }

        public void ClearSelection(bool notify = true)
        {
            for (int i = _selected.Count - 1; i >= 0; i--)
                _selected[i].SetSelected(false);
            _selected.Clear();
            RefreshOrderBadges();
            if (notify) RaiseChanged();
        }

        /// <summary>Returns the managed item at a display index (null if out of range).</summary>
        public UISelectable GetItem(int index)
            => (index >= 0 && index < _items.Count) ? _items[index] : null;

        /// <summary>Number of items managed by this group.</summary>
        public int ItemCount => _items.Count;


        /// <summary>Indices of currently selected items, in selection order.</summary>
        public List<int> GetSelectedIndices()
        {
            var result = new List<int>(_selected.Count);
            foreach (var s in _selected) result.Add(s.Index);
            return result;
        }

        /// <summary>Replaces the managed items at runtime (e.g. list built dynamically).</summary>
        public void SetItems(IList<UISelectable> items)
        {
            OnDestroy();
            _items.Clear();
            _selected.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    it.Index = i;
                    it.Clicked += OnItemClicked;
                    _items.Add(it);
                }
            }
        }
    }
}