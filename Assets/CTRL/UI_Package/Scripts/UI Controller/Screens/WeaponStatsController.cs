using System;
using UnityEngine;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Panel "SÚNG CHỦ LỰC": hiển thị các súng nhiều kill nhất.
    /// Presentation thuần — không đụng backend. MenuShellBinder lấy dữ liệu từ
    /// PlayFab (GetTopWeapons) rồi gọi SetWeapon() cho từng slot.
    /// </summary>
    public class WeaponStatsController : MonoBehaviour
    {
        // Một dòng súng: root để bật/tắt, ô tên súng, ô số kill.
        [Serializable]
        public class Slot
        {
            public GameObject root;
            public TMP_Text nameLabel;
            public TMP_Text killsLabel;
        }

        [SerializeField] private Slot[] _slots;
        [Tooltip("Hậu tố sau số kill, ví dụ \" kills\".")]
        [SerializeField] private string _killsSuffix = " kills";

        // Số slot có sẵn (để binder biết lấy top mấy súng).
        public int SlotCount => _slots != null ? _slots.Length : 0;

        // Đặt 1 slot theo hạng: tên súng + số kill.
        public void SetWeapon(int index, string weaponName, int kills)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            var s = _slots[index];
            if (s == null) return;
            if (s.root != null) s.root.SetActive(true);
            if (s.nameLabel != null) s.nameLabel.text = weaponName;
            if (s.killsLabel != null) s.killsLabel.text = kills + _killsSuffix;
        }

        // Ẩn các slot từ startIndex trở đi (khi không đủ dữ liệu).
        public void HideFrom(int startIndex)
        {
            if (_slots == null) return;
            for (int i = Mathf.Max(0, startIndex); i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].root != null) _slots[i].root.SetActive(false);
        }
    }
}
