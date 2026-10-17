using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-04] Cho phep dieu khien UIValueSlider / UISwitch bang tay cam / phim mui ten.
    /// Gan CUNG GameObject voi 1 TISGamepadUIButton (de object nhan EventSystem focus + highlight + nav doc).
    /// Khi object dang focus: Trai/Phai chinh gia tri slider (giu de truot), hoac Off/On cho switch.
    /// LUU Y: de navLeft/navRight cua TISGamepadUIButton = null -> TIS khong dieu huong ngang, nhuong cho component nay.
    /// Nav doc (len/xuong) van do TISGamepadUIButton.navUp/navDown xu ly nhu thuong.
    /// </summary>
    [DisallowMultipleComponent]
    public class TISValueNav : MonoBehaviour, ISubmitHandler
    {
        [SerializeField] private UIValueSlider _slider;
        [SerializeField] private UISwitch _switch;
        [Tooltip("Moi lan bam = bao nhieu % cua khoang gia tri (slider).")]
        [SerializeField] private float _stepFraction = 0.05f;
        [Tooltip("Khoang lap lai khi giu huong (giay).")]
        [SerializeField] private float _repeatDelay = 0.12f;

        private float _nextTime;

        private void Awake()
        {
            if (_slider == null) _slider = GetComponentInChildren<UIValueSlider>(true);
            if (_switch == null) _switch = GetComponentInChildren<UISwitch>(true);
        }

        private void Update()
        {
            bool focused = EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject;
            if (!focused) return;

            int dir = ReadX();
            if (dir == 0) return;
            if (Time.unscaledTime < _nextTime) return;

            if (_slider != null)
            {
                float range = _slider.MaxValue - _slider.MinValue;
                if (range <= 0f) return;
                float step = (range <= 5f) ? 0.1f : range * _stepFraction; // [Aemy 2026-10-05] range nho (volume/do nhay 0..1) -> 0.1/lan
                _slider.SetValue(_slider.Value + dir * step, true);
                _nextTime = Time.unscaledTime + _repeatDelay;
            }
            else if (_switch != null)
            {
                bool want = dir > 0;
                if (_switch.IsOn != want) _switch.SetValue(want, true);
                _nextTime = Time.unscaledTime + _repeatDelay * 3f;
            }
        }

        // [Aemy 2026-10-10] Bam A (tay cam) / Enter khi dang focus cong tac -> dao bat/tat.
        // (Chi Submit, khong dung onClick -> click chuot van do UISwitch tu xu ly, khong bi dao 2 lan.)
        public void OnSubmit(BaseEventData eventData)
        {
            if (_switch != null && _slider == null && _switch.Interactable) _switch.Toggle();
        }

        private int ReadX()
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 d = gp.dpad.ReadValue();
                Vector2 s = gp.leftStick.ReadValue();
                Vector2 v = d.sqrMagnitude > 0.1f ? d : s;
                if (v.x > 0.5f) return 1;
                if (v.x < -0.5f) return -1;
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) return 1;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) return -1;
            }
            return 0;
        }
    }
}
