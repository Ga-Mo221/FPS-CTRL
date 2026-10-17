// [Aemy 2026-10-17] FILE MOI. O chon gia tri dang "◄ GIA TRI ►" (Kết cấu, Khử răng cưa, Ngôn ngữ...).
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Chuot: bam ◄ / ►. Tay cam / ban phim: khi o dang focus (TISGamepadUIButton cung GameObject) -> Trai/Phai doi gia tri,
    /// A / Enter -> gia tri ke tiep. Chi trinh bay + ban Changed(index); SettingsBinder ghi vao SettingData (SRP).
    /// </summary>
    [DisallowMultipleComponent]
    public class UIOptionCycler : MonoBehaviour, ISubmitHandler
    {
        [SerializeField] private TMP_Text _valueLabel;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private string[] _options = new string[0];
        [SerializeField] private bool _wrap = true;
        [SerializeField] private float _repeatDelay = 0.25f;

        private int _index;
        private float _nextTime;

        public event Action<int> Changed;
        public int Index { get { return _index; } }
        public int Count { get { return _options != null ? _options.Length : 0; } }

        private void Awake()
        {
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Step(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Step(1));
            Render();
        }

        /// <summary>Doi danh sach lua chon (vd khi doi ngon ngu) - giu index.</summary>
        public void SetOptions(string[] options)
        {
            _options = options ?? new string[0];
            _index = Mathf.Clamp(_index, 0, Mathf.Max(0, Count - 1));
            Render();
        }

        public void SetIndex(int index, bool notify)
        {
            if (Count == 0) return;
            int i = Mathf.Clamp(index, 0, Count - 1);
            bool changed = i != _index;
            _index = i;
            Render();
            if (notify && changed && Changed != null) Changed(_index);
        }

        public void Step(int dir)
        {
            if (Count == 0) return;
            int i = _index + dir;
            if (_wrap) i = (i + Count) % Count; else i = Mathf.Clamp(i, 0, Count - 1);
            SetIndex(i, true);
        }

        private void Render()
        {
            if (_valueLabel != null && Count > 0) _valueLabel.text = _options[Mathf.Clamp(_index, 0, Count - 1)];
        }

        public void OnSubmit(BaseEventData eventData) { Step(1); }

        private void Update()
        {
            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (!focused || Time.unscaledTime < _nextTime) return;
            int dir = 0;
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 d = gp.dpad.ReadValue(); Vector2 s = gp.leftStick.ReadValue();
                Vector2 v = d.sqrMagnitude > 0.1f ? d : s;
                if (v.x > 0.5f) dir = 1; else if (v.x < -0.5f) dir = -1;
            }
            var kb = Keyboard.current;
            if (dir == 0 && kb != null)
            {
                if (kb.rightArrowKey.isPressed) dir = 1; else if (kb.leftArrowKey.isPressed) dir = -1;
            }
            if (dir == 0) return;
            Step(dir);
            _nextTime = Time.unscaledTime + _repeatDelay;
        }
    }
}
