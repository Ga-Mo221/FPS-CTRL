using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bo chon MAU TAM NGAM: nhieu o mau, chon 1. O dang chon hien vien (Outline).
    /// Phat ColorChanged(Color) + IndexChanged(int). Manager doc SelectedColor/SelectedIndex,
    /// hoac goi SetSelected(i, notify:false) khi nap tu save/PlayFab.
    /// </summary>
    public class CrosshairColorPicker : MonoBehaviour
    {
        [Tooltip("Cac nut o mau, dung thu tu hien thi. Mau lay tu Image.color cua tung nut.")]
        [SerializeField] private Button[] _swatches;
        [SerializeField] private Color _outlineColor = Color.white;
        [SerializeField] private Vector2 _outlineSize = new Vector2(3f, 3f);
        [SerializeField] private int _defaultIndex = 0;

        private Outline[] _outlines;

        public event Action<Color> ColorChanged;
        public event Action<int> IndexChanged;

        public int SelectedIndex { get; private set; }
        public int Count { get { return _swatches != null ? _swatches.Length : 0; } }
        public Color SelectedColor { get { return (SelectedIndex >= 0 && SelectedIndex < Count && _swatches[SelectedIndex] != null) ? ColorOf(SelectedIndex) : Color.white; } }
        public Color ColorAt(int i) { return ColorOf(i); }

        public CrosshairColorPicker() { SelectedIndex = -1; }

        private void Awake()
        {
            EnsureOutlines();
            for (int i = 0; i < Count; i++)
            {
                int idx = i;
                if (_swatches[i] != null) _swatches[i].onClick.AddListener(delegate { Select(idx); });
            }
            SetSelected(SelectedIndex >= 0 ? SelectedIndex : _defaultIndex, false);
        }

        private void EnsureOutlines()
        {
            if (_swatches == null) { _outlines = new Outline[0]; return; }
            _outlines = new Outline[_swatches.Length];
            for (int i = 0; i < _swatches.Length; i++)
            {
                if (_swatches[i] == null) continue;
                var ol = _swatches[i].GetComponent<Outline>();
                if (ol == null) ol = _swatches[i].gameObject.AddComponent<Outline>();
                ol.effectColor = _outlineColor; ol.effectDistance = _outlineSize; ol.enabled = false;
                _outlines[i] = ol;
            }
        }

        private Color ColorOf(int i)
        {
            var img = _swatches[i].GetComponent<Image>();
            return img != null ? img.color : Color.white;
        }

        /// <summary>Chon o mau (co ban su kien). Goi tu onClick.</summary>
        public void Select(int index) { SetSelected(index, true); }

        /// <summary>Dat o mau dang chon. notify=false khi nap tu save de khong ban su kien.</summary>
        public void SetSelected(int index, bool notify)
        {
            if (_outlines == null) EnsureOutlines();
            SelectedIndex = index;
            for (int i = 0; i < _outlines.Length; i++)
                if (_outlines[i] != null) _outlines[i].enabled = (i == index);
            if (notify)
            {
                if (ColorChanged != null) ColorChanged(SelectedColor);
                if (IndexChanged != null) IndexChanged(index);
            }
        }
    }
}
