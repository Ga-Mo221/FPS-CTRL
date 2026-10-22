using GaMo.TrueInputSystem;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Gắn trên Pause_ panel. Đồng bộ panel Cài đặt theo tab đang active (LT/RT trên tay cầm):
    ///  - Tab CÀI ĐẶT active  -> mở panel Settings (PauseMenuActions.OpenSettings).
    ///  - Tab khác active      -> đóng panel Settings.
    /// Dựa vào onBecameActiveInLayer của TISGamepadUIButton: sự kiện bắn TRƯỚC khi Context
    /// redirect SubTab/Content, nên panel đã bật kịp để focus vào SubTab/Content bên trong.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseTabSettingsSync : MonoBehaviour
    {
        [SerializeField] private PauseMenuActions _actions;
        [SerializeField] private GameObject _settings;          // Pause_ panel/Settings
        [SerializeField] private TISGamepadUIButton _settingsTab; // Menu/Item_Settings/Row
        [SerializeField] private TISGamepadUIButton[] _tabs;      // 3 dòng menu (Tab layer)

        private void Awake()
        {
            if (_actions == null) _actions = GetComponent<PauseMenuActions>();
            if (_settings == null) { var t = transform.Find("Settings"); if (t != null) _settings = t.gameObject; }
            if (_settingsTab == null) { var t = transform.Find("Menu/Item_Settings/Row"); if (t != null) _settingsTab = t.GetComponent<TISGamepadUIButton>(); }
            if (_tabs == null || _tabs.Length == 0)
            {
                var menu = transform.Find("Menu");
                if (menu != null) _tabs = menu.GetComponentsInChildren<TISGamepadUIButton>(true);
            }

            if (_tabs == null) return;
            foreach (var tab in _tabs)
                if (tab != null && tab.layer == TISGamepadUIButton.NavigationLayer.Tab)
                    tab.onBecameActiveInLayer.AddListener(OnTabActivated);
        }

        // Mở Pause: tab mặc định là TIẾP TỤC → đóng Settings ngay (Context chỉ activate tab sau 1 frame,
        // không đóng ở đây thì panel Cài đặt lóe lên 1 frame).
        private void OnEnable()
        {
            if (_settings != null && _settings.activeSelf) _settings.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_tabs == null) return;
            foreach (var tab in _tabs)
                if (tab != null) tab.onBecameActiveInLayer.RemoveListener(OnTabActivated);
        }

        private void OnTabActivated(TISGamepadUIButton tab)
        {
            if (tab == _settingsTab)
            {
                if (_actions != null) _actions.OpenSettings();
                else if (_settings != null) _settings.SetActive(true);
            }
            else if (_settings != null && _settings.activeSelf)
            {
                _settings.SetActive(false);
            }
        }
    }
}
