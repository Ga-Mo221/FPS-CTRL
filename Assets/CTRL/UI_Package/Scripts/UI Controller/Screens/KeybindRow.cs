using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// 1 o gan phim (PC) hoac 1 nut tren so do tay cam (Gamepad), dung TrueInputSystem (TIS) de rebind + luu JSON.
    /// Bam -> StartRebinding (cho phim/nut moi) -> TIS luu vao TIS_Bindings_*.json va rebuild bridge.
    /// _action phai TRUNG ten action trong TIS InputProfile ("Jump", "Reload", "Q", "E", "Run"...).
    ///
    /// [Aemy 2026-10-11] Nang cap cho tay cam:
    ///  - Huy: PC = Esc, tay cam = START.
    ///  - Trong luc cho nut: chan dieu huong menu TIS (ExternalSuppress) de B khong thoat panel, D-pad khong nhay focus;
    ///    nha chan chi sau khi da tha het nut (khong bi "nhay" focus vi nut vua gan con dang giu).
    ///  - Trung nut voi action khac -> TU DOI CHO (action kia nhan nut cu).
    ///  - Moi row tu cap nhat label khi bridge rebuild (rebind/doi cho/khoi phuc mac dinh).
    /// </summary>
    public class KeybindRow : MonoBehaviour
    {
        [Tooltip("Ten action trong TIS InputProfile (khop chinh xac).")]
        [SerializeField] private string _action;
        [Tooltip("Rebind cho PC (ban phim/chuot) hay Gamepad.")]
        [SerializeField] private RebindTarget _target = RebindTarget.PC;
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _keyLabel;
        [Tooltip("(Tuy chon) Label ten hanh dong canh nut - hien huong dan trong luc cho nut moi.")]
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private string _listeningText = "...";
        [SerializeField] private string _gamepadHint = "Nhấn nút mới · START hủy";
        [SerializeField] private string _pcHint = "Nhấn phím mới · ESC hủy";

        private const string GamepadCancelPath = "<Gamepad>/start";
        private const string KeyboardCancelPath = "<Keyboard>/escape";
        private const float ReleaseTimeout = 2f;

        private bool _listening;
        private bool _suppressing;
        private string _hintOriginal;
        private TISInputBridge _hookedBridge;
        private Coroutine _releaseRoutine;

        public string Action { get { return _action; } }
        public RebindTarget Target { get { return _target; } }
        public bool IsListening { get { return _listening; } }

        // ======================= LIFECYCLE =======================
        private void Start()
        {
            RefreshLabel();
            if (_button != null) _button.onClick.AddListener(BeginRebind);
        }

        private void OnEnable()
        {
            HookBridge();
            RefreshLabel();
        }

        private void OnDisable()
        {
            if (_listening) TISRebindingSystem.CancelRebinding();   // dong panel giua chung -> huy, khong treo action
            if (_releaseRoutine != null) { StopCoroutine(_releaseRoutine); _releaseRoutine = null; }
            SetSuppress(false);
            RestoreHint();
            UnhookBridge();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(BeginRebind);
            SetSuppress(false);
            UnhookBridge();
        }

        // Bridge co the sinh SAU row (vd vao MenuShell truc tiep / bridge doi scene) -> tu hook khi xuat hien.
        private void Update()
        {
            if (TISInputBridge.Instance != _hookedBridge) { HookBridge(); RefreshLabel(); }
        }

        private void HookBridge()
        {
            var b = TISInputBridge.Instance;
            if (b == _hookedBridge) return;
            UnhookBridge();
            _hookedBridge = b;
            if (_hookedBridge != null) _hookedBridge.OnProfileSwapped += OnProfileSwapped;
        }

        private void UnhookBridge()
        {
            if (_hookedBridge != null) _hookedBridge.OnProfileSwapped -= OnProfileSwapped;
            _hookedBridge = null;
        }

        private void OnProfileSwapped(TISInputProfileConfig profile) { if (!_listening) RefreshLabel(); }

        // ======================= REBIND =======================
        /// <summary>Bat dau cho phim/nut moi (goi tu onClick).</summary>
        public void BeginRebind()
        {
            var bridge = TISInputBridge.Instance;
            if (bridge == null) { Debug.LogWarning("[KeybindRow] Chua co TISInputBridge (rebind chi chay khi vao tu Login)."); return; }
            if (_listening || TISRebindingSystem.State == RebindState.Listening) return;   // 1 row 1 luc
            if (string.IsNullOrEmpty(_action)) return;

            _listening = true;
            if (_keyLabel != null) _keyLabel.text = _listeningText;
            ShowHint(_target == RebindTarget.Gamepad ? _gamepadHint : _pcHint);
            if (_target == RebindTarget.Gamepad) SetSuppress(true);

            string cancel = _target == RebindTarget.Gamepad ? GamepadCancelPath : KeyboardCancelPath;
            TISRebindingSystem.StartRebinding(bridge, _action, _target, OnRebindDone, cancel);
        }

        private void OnRebindDone(RebindResult result)
        {
            _listening = false;
            if (result.Success) ResolveConflict(result);
            RestoreHint();
            RefreshLabel();
            if (!result.Success && result.FailReason != "cancelled")
                Debug.LogWarning("[KeybindRow] Rebind \"" + _action + "\" that bai: " + result.FailReason);

            // Nha chan dieu huong SAU khi tha het nut (tranh nut vua gan con giu lam nhay focus / B thoat panel).
            if (_suppressing)
            {
                if (isActiveAndEnabled) _releaseRoutine = StartCoroutine(ReleaseWhenIdle());
                else SetSuppress(false);
            }
        }

        // Nut moi da thuoc action khac -> action kia nhan lai nut cu cua action nay (doi cho).
        private void ResolveConflict(RebindResult result)
        {
            var bridge = TISInputBridge.Instance;
            if (bridge == null || string.IsNullOrEmpty(result.NewPath)) return;
            string other = TISRebindingSystem.FindConflict(bridge, _action, _target, result.NewPath);
            if (other == null) return;
            if (string.IsNullOrEmpty(result.OldPath) || string.Equals(result.OldPath, result.NewPath, System.StringComparison.OrdinalIgnoreCase)) return;
            TISRebindingSystem.ForceApply(bridge, other, _target, result.OldPath);
            Debug.Log("[KeybindRow] \"" + _action + "\" lay nut cua \"" + other + "\" -> doi cho: \"" + other + "\" = " + DisplayOf(result.OldPath));
        }

        private IEnumerator ReleaseWhenIdle()
        {
            yield return null;
            float until = Time.unscaledTime + ReleaseTimeout;
            while (Time.unscaledTime < until && !GamepadIdle()) yield return null;
            yield return null;
            SetSuppress(false);
            _releaseRoutine = null;
        }

        private static bool GamepadIdle()
        {
            var gp = Gamepad.current;
            if (gp == null) return true;
            // khong dung CheckStateIsAtDefault: stick bi troi (drift) nhe se khong bao gio "default".
            if (gp.dpad.ReadValue().sqrMagnitude > 0.1f || gp.leftStick.ReadValue().sqrMagnitude > 0.25f) return false;
            return !(gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.buttonWest.isPressed || gp.buttonNorth.isPressed
                  || gp.leftShoulder.isPressed || gp.rightShoulder.isPressed || gp.leftTrigger.isPressed || gp.rightTrigger.isPressed
                  || gp.leftStickButton.isPressed || gp.rightStickButton.isPressed || gp.startButton.isPressed || gp.selectButton.isPressed);
        }

        private void SetSuppress(bool on)
        {
            if (on == _suppressing) return;
            _suppressing = on;
            if (on) TISGamepadUIButton.ExternalSuppress++;
            else TISGamepadUIButton.ExternalSuppress = Mathf.Max(0, TISGamepadUIButton.ExternalSuppress - 1);
        }

        // ======================= LABEL =======================
        private void ShowHint(string text)
        {
            if (_hintLabel == null) return;
            if (_hintOriginal == null) _hintOriginal = _hintLabel.text;
            _hintLabel.text = text;
        }

        private void RestoreHint()
        {
            if (_hintLabel == null || _hintOriginal == null) return;
            _hintLabel.text = _hintOriginal;
            _hintOriginal = null;
        }

        /// <summary>Hien phim/nut dang gan len label. Khong co bridge -> giu label san co.</summary>
        public void RefreshLabel()
        {
            if (_keyLabel == null || _listening) return;
            var bridge = TISInputBridge.Instance;
            if (bridge == null) return;
            _keyLabel.text = DisplayOf(TISRebindingSystem.GetActivePath(bridge, _action, _target));
        }

        private string DisplayOf(string path)
        {
            if (_target == RebindTarget.Gamepad) return GamepadGlyphNames.Short(path);
            string disp = TISRebindingSystem.GetShortDisplayName(path);
            return string.IsNullOrEmpty(disp) ? disp : disp.ToUpperInvariant();
        }
    }
}
