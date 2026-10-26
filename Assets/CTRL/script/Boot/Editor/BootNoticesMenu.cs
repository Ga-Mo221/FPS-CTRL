// [Aemy 2026-10-27] FILE MOI.
using UnityEditor;
using UnityEngine;

namespace Game.Boot.EditorTools
{
    /// <summary>Xoa danh dau "da xem luu y lan dau" -> lan Play sau hien lai trang canh bao chi-lan-dau (de test).</summary>
    internal static class BootNoticesMenu
    {
        [MenuItem("Eclipse Strike/Hiện lại cảnh báo lần đầu", false, 1)]
        private static void ResetSeen()
        {
            PlayerPrefs.DeleteKey(WarningScreenView.SeenKey);
            PlayerPrefs.Save();
            Debug.Log("[Boot] Đã xoá đánh dấu — lần mở game sau sẽ hiện lại các lưu ý lần đầu.");
        }
    }
}
