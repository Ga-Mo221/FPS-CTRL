using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // Một dòng trong kill feed, hiển thị dạng: "Người hạ gục [vũ khí] Nạn nhân".
    // Script này chỉ lo phần hiển thị. KillFeedView sẽ gọi SetData() để đổ dữ liệu vào.
    public class KillFeedEntryView : MonoBehaviour
    {
        // Ba ô chữ của một dòng: tên người hạ gục, icon vũ khí, tên nạn nhân.
        [SerializeField] private TMP_Text _killerLabel;
        [SerializeField] private Image _weaponIcon;
        [SerializeField] private TMP_Text _victimLabel;

        // Màu tô riêng cho tên khi người hạ gục chính là người chơi cục bộ (mình).
        [Tooltip("Màu tô cho tên khi người hạ gục là người chơi cục bộ.")]
        [SerializeField] private Color _localPlayerColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color _defaultKillerColor; // màu tên mặc định, lưu lại lúc Awake

        // Đổ dữ liệu vào dòng. Nếu killerIsLocal = true thì tô tên bằng màu riêng.
        public void SetData(string killer, string weaponname, string victim, bool killerIsLocal = false)
        {
            if (_killerLabel != null)
            {
                _killerLabel.text = killer;
                _killerLabel.color = killerIsLocal ? _localPlayerColor : _defaultKillerColor;
            }
            
            Sprite icon = null;
            var gamemanager = GameManager.Instance;
            if (gamemanager != null)
            {
                icon = gamemanager.GetWeaponIcon(weaponname);
                Debug.Log(icon + " " + weaponname);
            }
            if (_weaponIcon != null && icon != null) _weaponIcon.sprite = icon;
            if (_victimLabel != null) _victimLabel.text = victim;
        }
    }
}
