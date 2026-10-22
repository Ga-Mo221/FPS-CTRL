using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the Register panel.
    /// - Raises <see cref="RegisterSubmitted"/> / <see cref="LoginRequested"/> for the shell + binder.
    /// - Raises <see cref="UsernameChanged"/> (debounced) so the binder can query availability.
    /// - Owns the password-strength meter itself: it's pure client-side UX guidance, so it
    ///   doesn't need a service round-trip and updates live as the user types.
    /// </summary>
    public class RegisterController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField _operatorName;
        [SerializeField] private TMP_InputField _email;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] private TMP_InputField _confirmPassword;
        [SerializeField] private Toggle _agree;

        [Header("Buttons")]
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _loginButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _availabilityLabel;
        [SerializeField] private TMP_Text _strengthLabel;
        [Tooltip("The four strength segments, left to right.")]
        [SerializeField] private Image[] _strengthBars;

        [Header("Colors")]
        [SerializeField] private Color _availableColor = new Color(0.31f, 0.84f, 0.79f, 1f);
        [SerializeField] private Color _takenColor = new Color(0.90f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color _barEmpty = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color _barWeak = new Color(0.90f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color _barFair = new Color(0.95f, 0.66f, 0.28f, 1f);
        [SerializeField] private Color _barGood = new Color(0.85f, 0.80f, 0.30f, 1f);
        [SerializeField] private Color _barStrong = new Color(0.31f, 0.84f, 0.79f, 1f);

        [Tooltip("Seconds after typing stops before the username availability check fires.")]
        [SerializeField] private float _usernameDebounce = 0.5f;

        /// <summary>operatorName, email, password, confirmPassword, agreedToTerms.</summary>
        public event Action<string, string, string, string, bool> RegisterSubmitted;
        public event Action LoginRequested;
        /// <summary>Raised after the user stops typing the operator name (debounced).</summary>
        public event Action<string> UsernameChanged;

        private Coroutine _debounce;

        private void Awake()
        {
            if (_registerButton != null) _registerButton.onClick.AddListener(Submit);
            if (_loginButton != null) _loginButton.onClick.AddListener(() => LoginRequested?.Invoke());
            if (_password != null) _password.onValueChanged.AddListener(OnPasswordChanged);
            if (_operatorName != null) _operatorName.onValueChanged.AddListener(OnUsernameTyped);
        }

        private void OnEnable()
        {
            if (_password != null) UpdateStrength(_password.text);
        }

        private void Submit()
        {
            RegisterSubmitted?.Invoke(
                _operatorName != null ? _operatorName.text : string.Empty,
                _email != null ? _email.text : string.Empty,
                _password != null ? _password.text : string.Empty,
                _confirmPassword != null ? _confirmPassword.text : string.Empty,
                _agree != null && _agree.isOn);
        }
        
        // ---- Username availability (debounced so we don't spam the service) ----
        private void OnUsernameTyped(string value)
        {
            if (_debounce != null) StopCoroutine(_debounce);
            if (isActiveAndEnabled) _debounce = StartCoroutine(DebounceUsername(value));
        }

        private IEnumerator DebounceUsername(string value)
        {
            yield return new WaitForSeconds(_usernameDebounce);
            UsernameChanged?.Invoke(value);
        }

        /// <param name="available">null = neutral/unknown, true = free, false = taken.</param>
        public void SetAvailability(string message, bool? available = null)
        {
            if (_availabilityLabel == null) return;
            _availabilityLabel.text = message;
            if (available.HasValue)
                _availabilityLabel.color = available.Value ? _availableColor : _takenColor;
        }

        // ---- Password strength (pure client-side) ----
        private void OnPasswordChanged(string value) => UpdateStrength(value);

        private void UpdateStrength(string password)
        {
            int score = PasswordStrength.Score(password);
            if (_strengthLabel != null) _strengthLabel.text = PasswordStrength.Label(score);

            if (_strengthBars == null) return;
            Color on = ColorForScore(score);
            for (int i = 0; i < _strengthBars.Length; i++)
                if (_strengthBars[i] != null)
                    _strengthBars[i].color = i < score ? on : _barEmpty;
        }

        private Color ColorForScore(int score)
        {
            switch (score)
            {
                case 1: return _barWeak;
                case 2: return _barFair;
                case 3: return _barGood;
                case 4: return _barStrong;
                default: return _barEmpty;
            }
        }
    

        /// <summary>Xóa sạch các ô nhập + phản hồi (gọi sau khi đăng ký thành công).</summary>
        public void Clear()
        {
            if (_operatorName != null) _operatorName.text = string.Empty;
            if (_email != null) _email.text = string.Empty;
            if (_password != null) _password.text = string.Empty;
            if (_confirmPassword != null) _confirmPassword.text = string.Empty;
            if (_agree != null) _agree.isOn = false;
            SetAvailability(string.Empty);
            UpdateStrength(string.Empty);
        }
}
}
