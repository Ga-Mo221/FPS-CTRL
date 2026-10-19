
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Gan len Pause_ panel. Bam B tren tay cam:
    ///  - Dang o trong 1 muc Cai dat   -> de TISSectionBackNav dua ve hang tab (Nav).
    ///  - Dang o hang tab Cai dat      -> dong Cai dat, focus lai muc CAI DAT trong menu.
    ///  - Dang o menu (Tiep tuc/...)   -> TIEP TUC choi (dong Pause) — thuc hien khi NHA nut
    ///    de lan bam B (= Ngoi) khong lot xuong nhan vat ngay frame dong Pause.
    /// DefaultExecutionOrder -50: quyet dinh dua tren focus dau frame, truoc TISSectionBackNav.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class PauseBackNav : MonoBehaviour
    {
        [SerializeField] private PauseMenuActions _actions;
        [SerializeField] private GameObject _settings;          // Pause_ panel/Settings
        [SerializeField] private Transform _settingsNav;        // Settings/Nav
        [SerializeField] private GameObject _settingsMenuRow;   // Menu/Item_Settings/Row

        private bool _resumeOnRelease;

        private void Awake()
        {
            if (_actions == null) _actions = GetComponent<PauseMenuActions>();
            if (_settings == null) { var t = transform.Find("Settings"); if (t != null) _settings = t.gameObject; }
            if (_settingsNav == null && _settings != null) _settingsNav = _settings.transform.Find("Nav");
            if (_settingsMenuRow == null) { var t = transform.Find("Menu/Item_Settings/Row"); if (t != null) _settingsMenuRow = t.gameObject; }
        }

        private void OnEnable() { _resumeOnRelease = false; }

        private void Update()
        {
            var gp = Gamepad.current;
            if (gp == null) return;
            if (TISGamepadUIButton.ExternalSuppress > 0) return;

            if (gp.buttonEast.wasPressedThisFrame)
            {
                if (_settings != null && _settings.activeInHierarchy)
                {
                    var es = EventSystem.current;
                    var cur = es != null ? es.currentSelectedGameObject : null;
                    bool inSettings = cur != null && cur.transform.IsChildOf(_settings.transform);
                    bool inNav = inSettings && _settingsNav != null && cur.transform.IsChildOf(_settingsNav);
                    if (inSettings && !inNav) return; // TISSectionBackNav xu ly
                    _settings.SetActive(false);
                    if (es != null && _settingsMenuRow != null && _settingsMenuRow.activeInHierarchy) es.SetSelectedGameObject(_settingsMenuRow);
                    return;
                }
                _resumeOnRelease = true;
                return;
            }

            if (_resumeOnRelease && gp.buttonEast.wasReleasedThisFrame)
            {
                _resumeOnRelease = false;
                if (_actions != null) _actions.Resume();
            }
        }
    }
}
