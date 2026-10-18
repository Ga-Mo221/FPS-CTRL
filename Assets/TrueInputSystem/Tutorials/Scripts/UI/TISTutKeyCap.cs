// ============================================================
//  TrueInputSystem Tutorials — TISTutKeyCap.cs
//
//  Phím / nút minh hoạ: sáng màu nhấn khi control thật đang được nhấn.
//  controlPath theo cú pháp Input System: "<Keyboard>/w", "<Mouse>/leftButton",
//  "<Gamepad>/buttonSouth", "<Gamepad>/leftTrigger"...
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Key Cap")]
    public class TISTutKeyCap : MonoBehaviour
    {
        [SerializeField] TISTutShape _shape;
        [SerializeField] TMP_Text    _label;
        [SerializeField] string      _controlPath = "<Keyboard>/space";
        [SerializeField, Range(0.05f, 1f)] float _threshold = 0.3f;

        InputControl _control;
        float        _retry;
        float        _lit;
        bool         _forced;

        public string ControlPath
        {
            get => _controlPath;
            set { _controlPath = value; _control = null; _retry = 0f; }
        }

        public string Text
        {
            get => _label != null ? _label.text : "";
            set { if (_label != null) _label.text = value; }
        }

        public void Setup(TISTutShape shape, TMP_Text label, string controlPath)
        {
            _shape = shape; _label = label;
            ControlPath = controlPath;
            Apply();
        }

        /// <summary>Ép sáng (dùng cho nút ảo / minh hoạ không có control thật).</summary>
        public void SetForced(bool on) => _forced = on;

        public bool IsPressed { get; private set; }

        void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            _lit = 0f;
            Apply();
        }

        void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

        void OnDeviceChange(InputDevice d, InputDeviceChange c)
        {
            if (c == InputDeviceChange.Added || c == InputDeviceChange.Removed || c == InputDeviceChange.Reconnected)
            {
                _control = null;
                _retry = 0f;
            }
        }

        void Update()
        {
            if (_control == null && !string.IsNullOrEmpty(_controlPath) && Time.unscaledTime >= _retry)
            {
                _control = InputSystem.FindControl(_controlPath);
                _retry = Time.unscaledTime + 1f;           // chưa cắm thiết bị → thử lại sau 1 giây
            }

            bool pressed = _forced;
            if (!pressed && _control != null && _control.device.added)
            {
                pressed = _control is ButtonControl b
                    ? b.ReadValue() >= _threshold
                    : _control.EvaluateMagnitude() >= _threshold;
            }
            IsPressed = pressed;

            float target = pressed ? 1f : 0f;
            if (!Mathf.Approximately(_lit, target))
            {
                _lit = Mathf.MoveTowards(_lit, target, Time.unscaledDeltaTime * 12f);
                Apply();
            }
        }

        void Apply()
        {
            var th = TISTutTheme.Current;
            if (_shape != null)
            {
                _shape.color = Color.Lerp(th.card, th.Accent(), _lit);
                _shape.BorderColor = th.ink;
                _shape.rectTransform.localScale = Vector3.one * (1f - 0.06f * _lit);
            }
            if (_label != null) _label.color = th.ink;
        }
    }
}
