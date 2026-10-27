using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the Forgot-password panel. Raises
    /// <see cref="ResetRequested"/> with the entered email for a higher layer to call
    /// the recovery service, and <see cref="BackRequested"/> for the shell to return
    /// to Login. <see cref="SetStatus"/> lets that layer push the "email sent /
    /// resend in 00:42" message back into the status pill.
    /// </summary>
    public class ForgotPasswordController : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _email;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _statusText;

        public event Action<string> ResetRequested;
        public event Action BackRequested;

        private void Awake()
        {
            if (_resetButton != null)
                _resetButton.onClick.AddListener(() => ResetRequested?.Invoke(_email != null ? _email.text : string.Empty));
            if (_backButton != null)
                _backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        public void SetStatus(string message) { if (_statusText != null) _statusText.text = message; }
    }
}
