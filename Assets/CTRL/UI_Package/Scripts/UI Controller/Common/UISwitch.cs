using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Cong tac bat/tat tu che (Image-based) cho panel Settings.
    /// Click de dao trang thai; tu doi sprite nen + vi tri/sprite/mau cua knob (nut tron).
    /// Phat su kien Changed(bool). Binder/gameplay doc qua IsOn hoac goi SetValue.
    /// Gan component nay len object NEN cua cong tac (co Image, la vung bam).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UISwitch : MonoBehaviour, IPointerClickHandler
    {
        [Header("Refs")]
        [Tooltip("Image nen cong tac. De trong = lay Image tren chinh object nay.")]
        [SerializeField] private Image _bg;
        [Tooltip("RectTransform cua knob (nut tron) - se truot trai/phai.")]
        [SerializeField] private RectTransform _knob;
        [Tooltip("Image cua knob - de doi sprite/mau. De trong = lay tu _knob.")]
        [SerializeField] private Image _knobImage;

        [Header("Sprites theo trang thai (tuy chon)")]
        [SerializeField] private Sprite _bgOff;
        [SerializeField] private Sprite _bgOn;
        [SerializeField] private Sprite _knobOff;
        [SerializeField] private Sprite _knobOn;

        [Header("Mau knob (ap khi khong dung sprite rieng)")]
        [SerializeField] private Color _knobColorOff = new Color(0.78f, 0.76f, 0.76f, 1f); // #C7C2C2
        [SerializeField] private Color _knobColorOn = Color.white;

        [Header("Vi tri knob (X, anchor giua)")]
        [SerializeField] private float _knobOffX = -13f;
        [SerializeField] private float _knobOnX = 13f;

        [Header("Trang thai")]
        [SerializeField] private bool _isOn = false;
        [SerializeField] private bool _interactable = true;

        /// <summary>Ban ra moi khi nguoi dung doi trang thai (hoac SetValue notify=true).</summary>
        public event Action<bool> Changed;

        /// <summary>Trang thai hien tai (true = bat).</summary>
        public bool IsOn { get { return _isOn; } }
        public bool Interactable { get { return _interactable; } set { _interactable = value; } }

        private void Awake() { CacheRefs(); Apply(); }

        private void CacheRefs()
        {
            if (_bg == null) _bg = GetComponent<Image>();
            if (_knobImage == null && _knob != null) _knobImage = _knob.GetComponent<Image>();
        }

        /// <summary>Click chuot -> dao trang thai (neu cho phep).</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (!_interactable) return;
            SetValue(!_isOn, true);
        }

        /// <summary>Dat trang thai. notify=true se ban su kien Changed.</summary>
        public void SetValue(bool on, bool notify)
        {
            _isOn = on;
            Apply();
            if (notify && Changed != null) Changed(_isOn);
        }

        /// <summary>Dat trang thai ma KHONG ban su kien (dung khi nap gia tri tu save/PlayFab).</summary>
        public void SetValueWithoutNotify(bool on) { SetValue(on, false); }

        /// <summary>Dao trang thai va ban su kien.</summary>
        public void Toggle() { SetValue(!_isOn, true); }

        // Cap nhat hinh anh (nen + knob) theo _isOn.
        private void Apply()
        {
            if (_bg != null)
            {
                Sprite s = _isOn ? _bgOn : _bgOff;
                if (s != null) _bg.sprite = s;
            }
            if (_knobImage != null)
            {
                Sprite ks = _isOn ? _knobOn : _knobOff;
                if (ks != null) _knobImage.sprite = ks;
                _knobImage.color = _isOn ? _knobColorOn : _knobColorOff;
            }
            if (_knob != null)
            {
                Vector2 p = _knob.anchoredPosition;
                p.x = _isOn ? _knobOnX : _knobOffX;
                _knob.anchoredPosition = p;
            }
        }
    }
}
