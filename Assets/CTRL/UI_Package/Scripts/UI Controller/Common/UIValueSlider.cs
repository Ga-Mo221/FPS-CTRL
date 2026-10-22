using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// A lightweight, draggable value slider that matches the game's custom UI kit
    /// (see <see cref="UISelectable"/>). Unlike Unity's built-in Slider it drives a
    /// *Filled* image via fillAmount, so the existing "fat fill over a thin track"
    /// art is preserved exactly (no anchor stretching, no handle required).
    ///
    /// Put this on the fill RectTransform itself (the wide, tall bar). It uses its
    /// own rect as the drag area, maps pointer X to a value in [min, max], updates
    /// the fill and an optional numeric readout, and raises <see cref="ValueChanged"/>.
    ///
    /// Single Responsibility: it only knows how to turn pointer input into a clamped
    /// value and reflect that value visually. What the value *means* (volume, FOV,
    /// sensitivity...) is decided by whoever subscribes to the event.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIValueSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Header("Refs")]
        [Tooltip("Image with Type = Filled (Horizontal). If empty, GetComponent<Image> is used.")]
        [SerializeField] private Image _fill;
        [Tooltip("Optional numeric readout label (the 'chỉ số' text).")]
        [SerializeField] private TMP_Text _readout;

        [Header("Range")]
        [SerializeField] private float _min = 0f;
        [SerializeField] private float _max = 1f;
        [Tooltip("Round to whole numbers (e.g. volume, FOV).")]
        [SerializeField] private bool _wholeNumbers = false;
        [Tooltip("Snap step for non-whole values (0 = continuous). E.g. 0.1 for sensitivity.")]
        [SerializeField] private float _step = 0f;
        [SerializeField] private float _value = 0f;

        [Header("Readout formatting")]
        [Tooltip("C# numeric format for the readout, e.g. \"0\", \"0.0\", \"0.00\".")]
        [SerializeField] private string _format = "0";
        [Tooltip("Text appended after the number, e.g. \"%\", \" ms\".")]
        [SerializeField] private string _suffix = "";

        [Header("State")]
        [SerializeField] private bool _interactable = true;

        /// <summary>Raised whenever the value changes (drag, click, or SetValue with notify).</summary>
        public event Action<float> ValueChanged;

        private RectTransform _rt;

        public float Value
        {
            get => _value;
            set => SetValue(value, true);
        }

        public float MinValue => _min;
        public float MaxValue => _max;

        public float Normalized =>
            Mathf.Approximately(_max, _min) ? 0f : Mathf.InverseLerp(_min, _max, _value);

        private void Awake()
        {
            _rt = transform as RectTransform;
            if (_fill == null) _fill = GetComponent<Image>();
            ApplyVisual();
        }

        private void OnEnable() => ApplyVisual();

        /// <summary>Configure range at runtime (e.g. when the settings service loads).</summary>
        public void SetRange(float min, float max, bool wholeNumbers)
        {
            _min = min;
            _max = max;
            _wholeNumbers = wholeNumbers;
            SetValue(_value, false);
        }

        public void SetValue(float value, bool notify)
        {
            float v = Clamp(value);
            bool changed = !Mathf.Approximately(v, _value);
            _value = v;
            ApplyVisual();
            if (notify && changed) ValueChanged?.Invoke(_value);
        }

        /// <summary>Set the value and refresh visuals without raising the event.</summary>
        public void SetValueWithoutNotify(float value) => SetValue(value, false);

        public void SetInteractable(bool interactable) => _interactable = interactable;

        private float Clamp(float value)
        {
            float v = Mathf.Clamp(value, _min, _max);
            if (_wholeNumbers)
                v = Mathf.Round(v);
            else if (_step > 0f)
                v = _min + Mathf.Round((v - _min) / _step) * _step;
            return Mathf.Clamp(v, _min, _max);
        }

        private void ApplyVisual()
        {
            if (_fill != null) _fill.fillAmount = Normalized;
            if (_readout != null) _readout.text = _value.ToString(_format) + _suffix;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_interactable) SetFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_interactable) SetFromPointer(eventData);
        }

        private void SetFromPointer(PointerEventData eventData)
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, eventData.position, eventData.pressEventCamera, out var local))
                return;

            float width = _rt.rect.width;
            if (width <= 0f) return;

            float t = Mathf.Clamp01((local.x - _rt.rect.xMin) / width);
            SetValue(Mathf.Lerp(_min, _max, t), true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_fill == null) _fill = GetComponent<Image>();
            _value = Clamp(_value);
            ApplyVisual();
        }
#endif
    }
}
