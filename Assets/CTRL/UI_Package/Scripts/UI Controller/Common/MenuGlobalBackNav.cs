// [Aemy 2026-10-10] FILE MOI. Nut B (tay cam) = "quay lai" toan cuc cho MenuShell.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Gan len goc MenuShell. Bam B (buttonEast) tren tay cam:
    ///  1. Hop thoai (ModalDialog) dang mo        -> HUY.
    ///  2. Popup loi moi / loi moi ket ban        -> bo qua (tranh tu choi nham; dung A/nut tren popup).
    ///  3. Bang Ban be (Social) dang mo            -> dong.
    ///  4. Man Dang tim tran                        -> HUY tim tran.
    ///  5. Cai dat: dang o trong 1 muc             -> de TISSectionBackNav dua ve danh muc;
    ///              dang o danh muc                -> ve tab CHOI.
    ///  6. Trang bi / Cua hang                      -> ve tab CHOI.
    ///  7. Dang o CHOI                              -> khong lam gi.
    /// Overlay tu xu ly B rieng (chon nhan vat, chon avatar) dat ExternalSuppress>0 -> component nay nhuong.
    /// Chay truoc TISSectionBackNav (DefaultExecutionOrder -50) de quyet dinh dua tren focus DAU frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class MenuGlobalBackNav : MonoBehaviour
    {
        [Tooltip("Context TIS cua menu (BG Overall). De trong = tu tim trong con.")]
        [SerializeField] private TISUINavigationContext _context;
        [Tooltip("Tab CHOI tren TopBar (Tab layer). De trong = tu tim theo ten.")]
        [SerializeField] private TISGamepadUIButton _homeTab;
        [SerializeField] private string _homeTabName = "CHƠI";

        [Header("Panel (de trong = tu tim theo ten)")]
        [SerializeField] private GameObject _playPanel;
        [SerializeField] private GameObject _settingPanel;
        [SerializeField] private Transform _settingsCategoryRoot;   // "Chức năng"
        [SerializeField] private GameObject _matchmaking;          // "Match making"
        [SerializeField] private Button _matchmakingCancel;        // "Hủy"
        [SerializeField] private GameObject _socialOverlay;        // SocialPanel/Overlay
        [SerializeField] private GameObject[] _socialPopups;       // InvitePopup, FriendRequestPopup
        [SerializeField] private SocialPanelController _social;

        private void Awake() { AutoWire(); }

        private void AutoWire()
        {
            if (_context == null) _context = GetComponentInChildren<TISUINavigationContext>(true);
            if (_homeTab == null)
                foreach (var b in GetComponentsInChildren<TISGamepadUIButton>(true))
                    if (b.name == _homeTabName && b.layer == TISGamepadUIButton.NavigationLayer.Tab) { _homeTab = b; break; }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (_playPanel == null && t.name == "PlayPanel") _playPanel = t.gameObject;
                if (_settingPanel == null && t.name == "SettingPanel") _settingPanel = t.gameObject;
            }
            if (_settingPanel != null && _settingsCategoryRoot == null)
                foreach (var t in _settingPanel.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Chức năng") { _settingsCategoryRoot = t; break; }

            // Match making / SocialPanel nam ngang hang MenuShell (root khac) -> tim trong scene
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (_matchmaking == null && root.name == "Match making") _matchmaking = root;
                if (_social == null) _social = root.GetComponentInChildren<SocialPanelController>(true);
            }
            if (_matchmaking != null && _matchmakingCancel == null)
                foreach (var b in _matchmaking.GetComponentsInChildren<Button>(true)) if (b.name == "Hủy") { _matchmakingCancel = b; break; }
            if (_social != null)
            {
                if (_socialOverlay == null) { var o = _social.transform.Find("Overlay"); if (o != null) _socialOverlay = o.gameObject; }
                if (_socialPopups == null || _socialPopups.Length == 0)
                {
                    var a = _social.transform.Find("InvitePopup"); var b = _social.transform.Find("FriendRequestPopup");
                    _socialPopups = new[] { a != null ? a.gameObject : null, b != null ? b.gameObject : null };
                }
            }
        }

        private void Update()
        {
            var gp = Gamepad.current;
            if (gp == null || !gp.buttonEast.wasPressedThisFrame) return;

            // 1. Hop thoai
            var dlg = ModalDialog.Instance;
            if (dlg != null && dlg.IsOpen) { dlg.CancelFromBack(); return; }

            // overlay khac tu xu ly B (chon nhan vat / avatar)
            if (TISGamepadUIButton.ExternalSuppress > 0) return;

            // 2. Popup loi moi -> bo qua
            if (_socialPopups != null) foreach (var p in _socialPopups) if (p != null && p.activeInHierarchy) return;

            // 3. Bang ban be
            if (_socialOverlay != null && _socialOverlay.activeInHierarchy && _social != null) { _social.Close(); return; }

            // 4. Dang tim tran -> huy
            if (_matchmaking != null && _matchmaking.activeInHierarchy)
            {
                if (_matchmakingCancel != null && _matchmakingCancel.interactable) _matchmakingCancel.onClick.Invoke();
                return;
            }

            // 5. Cai dat
            if (_settingPanel != null && _settingPanel.activeInHierarchy)
            {
                var es = EventSystem.current;
                var cur = es != null ? es.currentSelectedGameObject : null;
                bool inSettings = cur != null && cur.transform.IsChildOf(_settingPanel.transform);
                bool inCategory = inSettings && _settingsCategoryRoot != null && cur.transform.IsChildOf(_settingsCategoryRoot);
                if (inSettings && !inCategory) return; // dang trong 1 muc -> TISSectionBackNav dua ve danh muc
                GoHome();
                return;
            }

            // 6/7. Trang bi / Cua hang -> ve CHOI; dang o CHOI thi thoi
            if (_playPanel != null && _playPanel.activeInHierarchy) return;
            GoHome();
        }

        private void GoHome()
        {
            if (_context != null && _homeTab != null && _homeTab.isActiveAndEnabled)
                _context.SetActiveInLayer(_homeTab); // -> TISLayerActivateClick -> TopBar.Select(Play) + focus noi dung CHOI
        }
    }
}
