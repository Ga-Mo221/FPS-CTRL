using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Hien gia tri cua 1 Slider len 1 TMP text (dang % hoac so thap phan).
    /// CACH DUNG: keo _slider + _label; chon _asPercent (0..1 -> %) hoac tat de hien so.
    /// </summary>
    public class SliderReadout : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TMP_Text _label;
        [Tooltip("Bat: gia tri 0..1 hien thanh 0..100%. Tat: hien so thap phan (vd 2.5).")]
        [SerializeField] private bool _asPercent = true;

        private void Start()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnChanged);
                OnChanged(_slider.value);
            }
        }

        private void OnDestroy()
        {
            if (_slider != null) _slider.onValueChanged.RemoveListener(OnChanged);
        }

        private void OnChanged(float v)
        {
            if (_label == null) return;
            _label.text = _asPercent ? (Mathf.RoundToInt(v * 100f) + "%") : v.ToString("0.0");
        }
    }
}
