using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the Login panel. Exposes intent as events and
    /// handles only pure-UI behaviour (show/hide password). It never talks to
    /// PlayFab/auth directly — a higher layer subscribes to <see cref="LoginSubmitted"/>
    /// / <see cref="GoogleClicked"/> and calls the auth service. Navigation events
    /// (<see cref="ForgotRequested"/>, <see cref="RegisterRequested"/>) are consumed
    /// by <see cref="AuthShellController"/>.
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField _email;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] private Toggle _remember;

        [Header("Buttons")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _googleButton;
        [SerializeField] private Button _showPasswordButton;
        [SerializeField] private Button _forgotButton;
        [SerializeField] private Button _registerButton;

        public event Action<string, string, bool> LoginSubmitted;
        public event Action GoogleClicked;
        public event Action ForgotRequested;
        public event Action RegisterRequested;

        private bool _passwordVisible;

        private void Awake()
        {
            if (_loginButton != null) _loginButton.onClick.AddListener(Submit);
            if (_googleButton != null) _googleButton.onClick.AddListener(() => GoogleClicked?.Invoke());
            if (_forgotButton != null) _forgotButton.onClick.AddListener(() => ForgotRequested?.Invoke());
            if (_registerButton != null) _registerButton.onClick.AddListener(() => RegisterRequested?.Invoke());
            if (_showPasswordButton != null) _showPasswordButton.onClick.AddListener(TogglePasswordVisibility);
        }

        // [REMEMBER 2026-09-29 Aemy] Prefill o dang nhap tu thong tin da luu (ghi nho dang nhap).
        public void Prefill(string usernameOrEmail, string password, bool remember)
        {
            if (_email != null) _email.text = usernameOrEmail != null ? usernameOrEmail : "";
            if (_password != null) _password.text = password != null ? password : "";
            if (_remember != null) _remember.isOn = remember;
        }

        private void Submit()
        {
            string email = _email != null ? _email.text : string.Empty;
            string pass = _password != null ? _password.text : string.Empty;
            bool remember = _remember != null && _remember.isOn;
            LoginSubmitted?.Invoke(email, pass, remember);
        }

        private void TogglePasswordVisibility()
        {
            if (_password == null) return;
            _passwordVisible = !_passwordVisible;
            _password.contentType = _passwordVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            _password.ForceLabelUpdate();
        }
    }
}
