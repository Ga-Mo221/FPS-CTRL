using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GaMo.TrueInputSystem;
using UnityEngine.EventSystems;


namespace Game.UI
{
    /// <summary>
    /// Chuyen doi giua view PC (danh sach phim) va view TAY CAM (so do controller) trong tab Phim bam.
    /// Nut PC bat cac object PC + tat controller view, va nguoc lai. Cap nhat mau nut dang chon.
    ///
    /// LEAD - cach wire (Inspector): _pcObjects = header + 2 cot list phim PC; _controllerView = container so do tay cam;
    /// _pcButton/_controllerButton = 2 nut gat; _pcBg/_controllerBg + _pcLabel/_controllerLabel = anh nen + chu de doi mau chon.
    /// Awake tu gan onClick cho 2 nut; OnEnable mac dinh ve view PC.
    /// </summary>
    public class KeybindViewSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] _pcObjects;      // header + 2 cot list PC
        [SerializeField] private GameObject _controllerView;   // container so do tay cam
        [SerializeField] private Button _pcButton;
        [SerializeField] private Button _controllerButton;
        [SerializeField] private Image _pcBg;
        [SerializeField] private Image _controllerBg;
        [SerializeField] private TMP_Text _pcLabel;
        [SerializeField] private TMP_Text _controllerLabel;

        [SerializeField] private Color _onBg = new Color(0.31f, 0.84f, 0.79f, 0.16f);
        [SerializeField] private Color _offBg = new Color(1f, 1f, 1f, 0.03f);
        [SerializeField] private Color _onText = new Color(0.31f, 0.84f, 0.79f, 1f);
        [SerializeField] private Color _offText = new Color(0.62f, 0.65f, 0.70f, 1f);

        private void Awake()
        {
            if (_pcButton != null) _pcButton.onClick.AddListener(ShowPC);
            if (_controllerButton != null) _controllerButton.onClick.AddListener(ShowController);
        }
        // [Aemy] Auto-follow: view keybind luon theo thiet bi TIS dang dung. Nut PC/Tay cam chi la PREVIEW thu cong.
        private TISActiveDevice _lastDevice = (TISActiveDevice)(-1);
        private void OnEnable() { SyncFromDevice(true); }
        private void Update() { SyncFromDevice(false); }

        private void SyncFromDevice(bool force)
        {
            var bridge = TISInputBridge.Instance;
            if (bridge == null) return;
            var d = bridge.ActiveDevice;
            if (!force && d == _lastDevice) return;
            _lastDevice = d;
            SetView(d == TISActiveDevice.Gamepad);
        }

        /// <summary>Hien view PC (danh sach phim ban phim/chuot). Co the goi tu Button.onClick hoac code.</summary>
        public void ShowPC() { SetView(false); }

        /// <summary>Hien view TAY CAM (so do controller). Co the goi tu Button.onClick hoac code.</summary>
        public void ShowController() { SetView(true); }

private void SetView(bool controller)
        {
            if (_pcObjects != null) foreach (var o in _pcObjects) if (o != null) o.SetActive(!controller);
            if (_controllerView != null) _controllerView.SetActive(controller);
            if (_pcBg != null) _pcBg.color = controller ? _offBg : _onBg;
            if (_controllerBg != null) _controllerBg.color = controller ? _onBg : _offBg;
            if (_pcLabel != null) _pcLabel.color = controller ? _offText : _onText;
            if (_controllerLabel != null) _controllerLabel.color = controller ? _onText : _offText;

            // [Aemy 2026-10-05] #1 FIX: doi view xong, dua focus vao nut dau cua view dang hien
            // (chi khi dang dung tay cam) de dpad chinh duoc ngay, khong ket o nut PC da bi an.
            FocusActiveView(controller);
        }

        private void FocusActiveView(bool controller)
        {
            var bridge = TISInputBridge.Instance;
            if (bridge != null && !bridge.IsGamepad) return;   // chuot/phim: khong cuop focus
            if (EventSystem.current == null) return;

            TISGamepadUIButton target = null;
            if (controller)
            {
                if (_controllerView != null) target = PickFirst(_controllerView.transform);
            }
            else if (_pcObjects != null)
            {
                foreach (var o in _pcObjects)
                {
                    if (o == null) continue;
                    target = PickFirst(o.transform);
                    if (target != null) break;
                }
            }
            if (target != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private TISGamepadUIButton PickFirst(Transform root)
        {
            var all = root.GetComponentsInChildren<TISGamepadUIButton>(true);
            TISGamepadUIButton firstContent = null;
            foreach (var b in all)
            {
                if (!b.gameObject.activeInHierarchy) continue;
                if (b.layer != TISGamepadUIButton.NavigationLayer.Content) continue;
                if (b.isFirstSelected) return b;
                if (firstContent == null) firstContent = b;
            }
            return firstContent;
        }
    }
}
