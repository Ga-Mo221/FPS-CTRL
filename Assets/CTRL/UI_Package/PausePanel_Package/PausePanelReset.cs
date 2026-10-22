using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Gan vao Pause_ panel. Moi lan mo Pause (panel enable): ve trang thai mac dinh
    /// - an panel Settings, hien Menu - de khong bi ket o man Cai dat tu lan truoc.
    /// LEAD - cach wire: gan component nay len object Pause_ panel, keo _settings = child Settings,
    /// _menu = child Menu. Khong can goi gi - tu chay o OnEnable moi lan panel bat len.
    /// </summary>
    public class PausePanelReset : MonoBehaviour
    {
        [Tooltip("Panel Settings (se bi an moi lan mo Pause).")]
        [SerializeField] private GameObject _settings;
        [Tooltip("Panel Menu (se duoc hien moi lan mo Pause).")]
        [SerializeField] private GameObject _menu;

        /// <summary>Panel Pause bat len -> reset ve Menu, an Settings.</summary>
        private void OnEnable()
        {
            if (_settings != null) _settings.SetActive(false);
            if (_menu != null) _menu.SetActive(true);
        }
    }
}
