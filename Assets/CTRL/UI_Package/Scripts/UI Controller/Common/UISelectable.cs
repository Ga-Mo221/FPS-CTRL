using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace Game.UI
{
    /// <summary>
    /// A single selectable UI element (button-like). Handles ONLY its own
    /// visual state (normal / selected / locked) and raises a click event.
    /// Selection *rules* (how many can be selected) live in <see cref="UISelectionGroup"/>.
    /// Single Responsibility: this class knows nothing about other selectables.
    /// </summary>
    [DisallowMultipleComponent]
    public class UISelectable : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Button that triggers the click. If empty, GetComponent<Button> is used.")]
        [SerializeField] private Button _button;
        [Tooltip("Image whose sprite/color is swapped for the different states. Optional.")]
        [FormerlySerializedAs("_targetImage")]
        [SerializeField] private Graphic _targetGraphic;
        [Tooltip("Optional object shown only while selected (a highlight/checkmark).")]
        [SerializeField] private GameObject _selectedIndicator;

        [Header("Sprite states (optional)")]
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _selectedSprite;
        [SerializeField] private Sprite _lockedSprite;

        [Header("Color states (used when no sprites are set)")]
        [SerializeField] private bool _useColorStates = false;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Label tint (optional 2nd graphic, e.g. the text child)")]
        [Tooltip("Extra graphic (usually a TMP text) tinted by state, independent of the main target. Great for image-background buttons whose text must flip colour when selected.")]
        [SerializeField] private Graphic _label;
        [SerializeField] private Color _labelNormalColor = Color.white;
        [SerializeField] private Color _labelSelectedColor = Color.white;
        [SerializeField] private Color _labelLockedColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("State")]
        [SerializeField] private bool _locked = false;

        /// <summary>Raised when the user clicks this selectable (never raised while locked).</summary>
        public event Action<UISelectable> Clicked;
        /// <summary>[Aemy 2026-10-12] Bam vao o dang KHOA (tay cam A / Enter). Man hinh tu quyet dinh (vd sang Shop).</summary>
        public event Action<UISelectable> LockedClicked;

        /// <summary>Set by the owning <see cref="UISelectionGroup"/> so callbacks can identify it.</summary>
        public int Index { get; internal set; } = -1;

        public bool IsSelected { get; private set; }
        public bool IsLocked => _locked;

        private void Reset()
        {
            _button = GetComponent<Button>();
            _targetGraphic = GetComponent<Graphic>();
        }

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) _button.onClick.AddListener(HandleClick);
            ApplyVisual();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (_locked) { LockedClicked?.Invoke(this); return; }
            Clicked?.Invoke(this);
        }

        /// <summary>Sets the selected visual/state. Does NOT raise Clicked (used by the group).</summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            ApplyVisual();
        }

        /// <summary>Locks/unlocks the element (e.g. weapon not yet unlocked by level).</summary>
        public void SetLocked(bool locked)
        {
            _locked = locked;
            // [Aemy 2026-10-12] KHONG tat interactable nua: o khoa van nhan focus tay cam (di qua duoc, khong bi ket)
            // va bam A -> LockedClicked. Chon/bo chon van bi chan (HandleClick + UISelectionGroup kiem IsLocked).
            if (locked) IsSelected = false;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_selectedIndicator != null)
                _selectedIndicator.SetActive(IsSelected && !_locked);

            if (_targetGraphic != null)
            {
                if (_useColorStates)
                {
                    _targetGraphic.color = _locked ? _lockedColor : (IsSelected ? _selectedColor : _normalColor);
                }
                else
                {
                    var image = _targetGraphic as Image;
                    if (image != null)
                    {
                        Sprite target = _locked ? _lockedSprite : (IsSelected ? _selectedSprite : _normalSprite);
                        if (target != null) image.sprite = target;
                    }
                }
            }

            if (_label != null)
                _label.color = _locked ? _labelLockedColor : (IsSelected ? _labelSelectedColor : _labelNormalColor);
        }

        // [ORDER-BADGE 2026-09-29 Aemy] Hien so thu tu chon (1,2) mo o goc phai. order<=0 = an.
        private TMPro.TMP_Text _orderBadge;
        public void SetOrder(int order)
        {
            if (_orderBadge == null) { if (order <= 0) return; CreateOrderBadge(); }
            if (_orderBadge != null) { _orderBadge.gameObject.SetActive(order > 0); if (order > 0) _orderBadge.text = order.ToString(); }
        }
        private void CreateOrderBadge()
        {
            TMPro.TMP_Text src = GetComponentInChildren<TMPro.TMP_Text>(true);
            var go = new GameObject("OrderBadge", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 0f); rt.sizeDelta = new Vector2(46f, 46f);
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.alignment = TMPro.TextAlignmentOptions.Right; tmp.fontSize = 30; tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.color = new Color(1f, 1f, 1f, 0.35f); tmp.raycastTarget = false;
            if (src != null && src.font != null) tmp.font = src.font;
            go.transform.SetAsLastSibling();
            _orderBadge = tmp;
        }
    }
}