
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Gắn trên Pause_ panel. Nối 3 nút Menu:
    ///   - Item_Resume/Row  -> Resume()      : đóng pause + bỏ cờ option (tiếp tục chơi).
    ///   - Item_Settings/Row -> OpenSettings(): hiện panel Settings.
    ///   - Item_Leave/Row   -> Leave()       : rời trận (NetworkRunnerHandler.LeaveRoom).
    /// Dùng script vì Resume/Leave cần gọi tới singleton runtime (GameManager/Runner) — không nối
    /// persistent-listener tới object runtime được.
    /// </summary>
    public class PauseMenuActions : MonoBehaviour
    {
        [SerializeField] private GameObject _settings;
        [SerializeField] private GameObject _menu;

        public void OpenSettings()
        {
            if (_settings != null) _settings.SetActive(true);
            if (_menu != null) _menu.SetActive(true);
        }

        public void Resume()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetOnUIOptions(false);
            gameObject.SetActive(false); // ẩn Pause_ panel
        }

        private bool _leaving; // chan goi 2 lan (Submit tay cam + click)

        private void OnEnable() { _leaving = false; }

        public void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            Debug.Log("[PauseMenu] Leave() — roi tran");
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // giong nut roi tran o man ket qua: reset state tran (OnUI flags, PlayerList,
                // dan, hien chuot) truoc khi shutdown runner -> ve MenuShell khong bi ket chuot/co UI cu.
                gm.NewGame();
                if (gm.NetworkRunnerHandler != null)
                    gm.NetworkRunnerHandler.LeaveRoom();
                else
                    Debug.LogWarning("[PauseMenu] NetworkRunnerHandler null — khong roi phong duoc.");
            }
            gameObject.SetActive(false);
        }
    }
}
