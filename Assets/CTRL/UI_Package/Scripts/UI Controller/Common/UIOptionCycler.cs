// [Aemy 2026-10-17] FILE MOI. O chon gia tri dang "◄ GIA TRI ►" (Kết cấu, Khử răng cưa, Ngôn ngữ...).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Chuot: bam ◄ / ►. Tay cam / ban phim: khi o dang focus (TISGamepadUIButton cung GameObject) -> Trai/Phai doi gia tri,
    /// A / Enter -> gia tri ke tiep. Chi trinh bay + ban Changed(index); SettingsBinder ghi vao SettingData (SRP).
    /// [Aemy 2026-10-19] Doi bang tay cam/phim -> nut ◄ / ► "nhay" (phong to + mau Pressed) de thay ro dang bam ben nao.
    /// LUU Y: navLeft/navRight cua TISGamepadUIButton phai = null de TIS nhuong truc ngang cho component nay.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIOptionCycler : MonoBehaviour, ISubmitHandler
    {
        [SerializeField] private TMP_Text _valueLabel;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private string[] _options = new string[0];
        [SerializeField] private bool _wrap = true;
        [Tooltip("Giu huong: cho bao lau moi bat dau tu lap (giay).")]
        [SerializeField] private float _holdDelay = 0.4f;
        [Tooltip("Giu huong: khoang lap lai (giay).")]
        [SerializeField] private float _repeatDelay = 0.2f;
        [Tooltip("Thoi gian hieu ung nhay nut ◄ / ► khi doi bang tay cam (giay).")]
        [SerializeField] private float _pulseTime = 0.16f;
        [SerializeField] private float _pulseScale = 1.3f;

        private int _index;
        private float _nextTime;
        private int _heldDir; // huong dang giu (0 = khong giu) -> bam moi (doi huong / tha ra bam lai) an NGAY, khong bi chan boi repeat
        private Coroutine _pulse;
        private Transform _pulseTarget;
        private Vector3 _pulseBase = Vector3.one;

        public event Action<int> Changed;
        public int Index { get { return _index; } }
        public int Count { get { return _options != null ? _options.Length : 0; } }

        private void Awake()
        {
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Step(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Step(1));
            Render();
        }

        private void OnDisable() { StopPulse(); _heldDir = 0; }

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

        public void OnSubmit(BaseEventData eventData) { Step(1); Pulse(_nextButton); }

        private void Update()
        {
            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (!focused || TISGamepadUIButton.ExternalSuppress > 0) { _heldDir = 0; return; } // overlay dang mo -> khong doi gia tri phia sau
            int dir = 0;
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 d = gp.dpad.ReadValue();
                Vector2 s = TISGamepadVirtualCursor.IsCursorActive ? Vector2.zero : gp.leftStick.ReadValue();
                Vector2 v = d.sqrMagnitude > 0.1f ? d : s;
                // Chi nhan khi truc ngang troi hon truc doc (tranh vua xuong dong vua doi gia tri khi bam cheo).
                if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                {
                    if (v.x > 0.5f) dir = 1; else if (v.x < -0.5f) dir = -1;
                }
            }
            var kb = Keyboard.current;
            if (dir == 0 && kb != null)
            {
                if (kb.rightArrowKey.isPressed) dir = 1; else if (kb.leftArrowKey.isPressed) dir = -1;
            }
            if (dir == 0) { _heldDir = 0; return; }
            bool fresh = dir != _heldDir;              // vua bam / doi huong -> an ngay
            if (!fresh && Time.unscaledTime < _nextTime) return; // dang giu -> cho toi luot lap
            Step(dir);
            Pulse(dir > 0 ? _nextButton : _prevButton);
            _nextTime = Time.unscaledTime + (fresh ? _holdDelay : _repeatDelay);
            _heldDir = dir;
        }

        // ------------------------------------------------------------------ hieu ung ◄ / ►
        private void Pulse(Button b)
        {
            if (b == null || !isActiveAndEnabled) return;
            StopPulse();
            _pulseTarget = b.transform;
            _pulseBase = _pulseTarget.localScale;
            _pulse = StartCoroutine(PulseRoutine(b));
        }

        private void StopPulse()
        {
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = null;
            if (_pulseTarget != null) _pulseTarget.localScale = _pulseBase;
            _pulseTarget = null;
        }

        private IEnumerator PulseRoutine(Button b)
        {
            var g = b.targetGraphic;
            var cb = b.colors;
            if (g != null) g.CrossFadeColor(cb.pressedColor * cb.colorMultiplier, 0f, true, true);
            float t = 0f, dur = Mathf.Max(0.05f, _pulseTime);
            bool faded = false;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = 1f + (_pulseScale - 1f) * Mathf.Sin(k * Mathf.PI); // 1 -> max -> 1
                _pulseTarget.localScale = _pulseBase * s;
                if (!faded && k >= 0.5f && g != null) { g.CrossFadeColor(cb.normalColor * cb.colorMultiplier, dur * 0.5f, true, true); faded = true; }
                yield return null;
            }
            _pulseTarget.localScale = _pulseBase;
            if (!faded && g != null) g.CrossFadeColor(cb.normalColor * cb.colorMultiplier, 0.05f, true, true);
            _pulse = null;
            _pulseTarget = null;
        }
    }
}
