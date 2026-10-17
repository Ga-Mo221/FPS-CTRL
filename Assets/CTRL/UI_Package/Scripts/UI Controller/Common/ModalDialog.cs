using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Hop thoai dung chung: nhap lieu (Prompt), xac nhan (Confirm), thong bao (Info).
    /// Singleton. Cac man khac goi ModalDialog.Instance.Prompt(...) / Confirm(...) / Info(...).
    /// UI duoc gan san trong scene (dim + panel + title + message + input + 2 nut).
    /// </summary>
    public class ModalDialog : MonoBehaviour
    {
        public static ModalDialog Instance { get; private set; }

        [SerializeField] private GameObject _root;        // lop phu (dim + panel)
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _okButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _okLabel;
        [SerializeField] private TMP_Text _cancelLabel;

        private Action<string> _onConfirmInput;
        private Action _onConfirm;
        private bool _inputMode;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_okButton != null) _okButton.onClick.AddListener(OnOk);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancel);
            Hide();
        }

        /// <summary>Hoi 1 chuoi (co o nhap). onConfirm nhan text nguoi dung go.</summary>
        public void Prompt(string title, string defaultValue, Action<string> onConfirm, string message = null, string placeholder = "")
        {
            _inputMode = true; _onConfirmInput = onConfirm; _onConfirm = null;
            if (_title != null) _title.text = title;
            SetMessage(message);
            if (_input != null)
            {
                _input.gameObject.SetActive(true);
                _input.text = defaultValue ?? "";
                var ph = _input.placeholder as TMP_Text; if (ph != null) ph.text = placeholder ?? "";
            }
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
            SetLabel(_okLabel, "XÁC NHẬN"); SetLabel(_cancelLabel, "HỦY");
            Show();
            if (_input != null) { _input.Select(); _input.ActivateInputField(); }
        }

        /// <summary>Hoi Co/Khong. onConfirm goi khi bam nut dong y.</summary>
        public void Confirm(string title, string message, Action onConfirm, string okText = "ĐỒNG Ý", string cancelText = "HỦY")
        {
            _inputMode = false; _onConfirm = onConfirm; _onConfirmInput = null;
            if (_title != null) _title.text = title;
            SetMessage(message);
            if (_input != null) _input.gameObject.SetActive(false);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
            SetLabel(_okLabel, okText); SetLabel(_cancelLabel, cancelText);
            Show();
        }

        /// <summary>Thong bao 1 nut OK (ket qua / loi).</summary>
        public void Info(string title, string message)
        {
            _inputMode = false; _onConfirm = null; _onConfirmInput = null;
            if (_title != null) _title.text = title;
            SetMessage(message);
            if (_input != null) _input.gameObject.SetActive(false);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
            SetLabel(_okLabel, "OK");
            Show();
        }

        private void SetMessage(string msg)
        {
            if (_message == null) return;
            _message.text = msg ?? "";
            _message.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }
        private void SetLabel(TMP_Text lbl, string s) { if (lbl != null) lbl.text = s; }

        private void OnOk()
        {
            if (_inputMode) { var cb = _onConfirmInput; string v = _input != null ? _input.text : null; Hide(); if (cb != null) cb(v); }
            else { var cb = _onConfirm; Hide(); if (cb != null) cb(); }
        }
        private void OnCancel() { Hide(); }

        // [Aemy 2026-10-10] Ho tro tay cam: khi mo -> chan nav TIS menu phia sau + focus nut OK; dong -> tra focus cu.
        private bool _suppressing;
        private GameObject _prevFocus;
        public bool IsOpen { get { return _root != null && _root.activeSelf; } }
        /// <summary>Nut B tay cam (back toan cuc) goi: tuong duong bam HUY.</summary>
        public void CancelFromBack() { if (IsOpen) OnCancel(); }

        private void Show()
        {
            if (_root == null) return;
            _root.SetActive(true); transform.SetAsLastSibling();
            if (!_suppressing) { GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress++; _suppressing = true; }
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null)
            {
                _prevFocus = es.currentSelectedGameObject;
                var br = GaMo.TrueInputSystem.TISInputBridge.Instance;
                bool pad = br != null && br.IsGamepad;
                var target = (_okButton != null && _okButton.gameObject.activeInHierarchy) ? _okButton.gameObject
                           : (_cancelButton != null && _cancelButton.gameObject.activeInHierarchy) ? _cancelButton.gameObject : null;
                if (pad && target != null) es.SetSelectedGameObject(target);
            }
        }

        private void Hide()
        {
            if (_root != null) _root.SetActive(false);
            if (_suppressing)
            {
                GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress - 1);
                _suppressing = false;
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && _prevFocus != null && _prevFocus.activeInHierarchy) es.SetSelectedGameObject(_prevFocus);
                _prevFocus = null;
            }
        }

        private void OnDestroy()
        {
            if (_suppressing) { GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress - 1); _suppressing = false; }
        }
    }
}
