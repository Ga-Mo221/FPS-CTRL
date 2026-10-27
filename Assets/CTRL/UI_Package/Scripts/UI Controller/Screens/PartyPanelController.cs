using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Controller trinh bay cho panel "PHONG CUA BAN" (Overlay 2 cua PlayPanel).
    /// Grid party 5 o dong nhat: moi o co the o trang thai CO NGUOI (Filled: avatar+ten+
    /// trang thai+nhan truong) hoac TRONG (Empty: +/MOI). Cac o duoc dan bang
    /// HorizontalLayoutGroup nen bat/tat trang thai khong bao gio lam lech/lan layout.
    /// Chi lo phan hien thi; MenuShellBinder bom du lieu vao va lang nghe su kien.
    /// Slot party ve sau se do Photon Fusion dieu khien.
    /// </summary>
    public class PartyPanelController : MonoBehaviour
    {
        /// <summary>Mot o party. Co 2 nhom con: Filled (co nguoi) va Empty (trong/moi).</summary>
        [Serializable]
        public class MemberSlot
        {
            [Tooltip("Node goc cua o (luon bat; chi doi giua Filled/Empty ben trong).")]
            public GameObject root;
            [Tooltip("Nhom hien thi khi o CO NGUOI: chua avatar/ten/trang thai/truong.")]
            public GameObject filledGroup;
            [Tooltip("Nhom hien thi khi o TRONG: chua nut +/MOI.")]
            public GameObject emptyGroup;
            [Tooltip("Ten operator (trong Filled).")]
            public TMP_Text nameLabel;
            [Tooltip("Avatar (trong Filled).")]
            public Image avatar;
            [Tooltip("Nhan 'TRUONG' - chi bat cho truong phong (trong Filled).")]
            public GameObject leaderTag;
            [Tooltip("Nhan trang thai: San sang / Chuan bi (trong Filled).")]
            public TMP_Text statusLabel;
        }

        [Header("Cac o party (dong nhat)")]
        [SerializeField] private MemberSlot[] _members = new MemberSlot[0];

        [Header("Danh sach ban be")]
        [Tooltip("Nhan header, vi du 'BAN BE · 4 DANG ONLINE'. Dung {0} cho so luong.")]
        [SerializeField] private TMP_Text _friendsLabel;
        [SerializeField] private string _friendsFormat = "BẠN BÈ · {0} ĐANG ONLINE";
        [Tooltip("Cac o avatar ban be hien thi nhanh (Avatar 1/2/3).")]
        [SerializeField] private Image[] _friendAvatars = new Image[0];

        [Header("Nut San Sang")]
        [SerializeField] private Button _readyButton;

        /// <summary>Ban bam nut "SAN SANG".</summary>
        public event Action ReadyClicked;

        /// <summary>So o party co the trinh bay.</summary>
        public int SlotCount => _members != null ? _members.Length : 0;

        /// <summary>[Aemy 2026-10-17] Node goc cua o party (de gan menu chuot phai).</summary>
        public GameObject GetSlotRoot(int index)
        {
            if (_members == null || index < 0 || index >= _members.Length || _members[index] == null) return null;
            return _members[index].root;
        }
        /// <summary>[Aemy 2026-10-17] O dang co nguoi (Filled) hay khong.</summary>
        public bool IsSlotFilled(int index)
        {
            if (_members == null || index < 0 || index >= _members.Length || _members[index] == null) return false;
            var g = _members[index].filledGroup; return g != null && g.activeSelf;
        }

        /// <summary>[Aemy 2026-10-19] Nut "+ MOI" cua o trong (de tay cam bam A tren o trong = moi ban).</summary>
        public Button GetSlotInviteButton(int index)
        {
            if (_members == null || index < 0 || index >= _members.Length || _members[index] == null) return null;
            var g = _members[index].emptyGroup;
            return g != null ? g.GetComponentInChildren<Button>(true) : null;
        }

        /// <summary>So o avatar ban be co the trinh bay.</summary>
        public int FriendAvatarCount => _friendAvatars != null ? _friendAvatars.Length : 0;

        private Sprite[] _defaultAvatars; // [Aemy 2026-10-22] avatar mac dinh cua tung o (tra lai khi nguoi trong o roi di)

        private void Awake()
        {
            int n = SlotCount;
            _defaultAvatars = new Sprite[n];
            for (int i = 0; i < n; i++) if (_members[i] != null && _members[i].avatar != null) _defaultAvatars[i] = _members[i].avatar.sprite;
            if (_readyButton != null)
                _readyButton.onClick.AddListener(RaiseReady);
        }

        private void OnDestroy()
        {
            if (_readyButton != null)
                _readyButton.onClick.RemoveListener(RaiseReady);
        }

        private void RaiseReady()
        {
            if (ReadyClicked != null) ReadyClicked();
        }

        /// <summary>Dua o ve trang thai CO NGUOI + dat thong tin thanh vien.</summary>
        public void SetMember(int index, string operatorName, bool isLeader, string status)
        {
            if (_members == null || index < 0 || index >= _members.Length) return;
            var slot = _members[index];
            if (slot == null) return;
            if (slot.root != null) slot.root.SetActive(true);
            if (slot.filledGroup != null) slot.filledGroup.SetActive(true);
            if (slot.emptyGroup != null) slot.emptyGroup.SetActive(false);
            if (slot.nameLabel != null && operatorName != null) slot.nameLabel.text = operatorName;
            if (slot.leaderTag != null) slot.leaderTag.SetActive(isLeader);
            if (slot.statusLabel != null && !string.IsNullOrEmpty(status)) slot.statusLabel.text = status;
        }

        /// <summary>Gan avatar (sprite tai tu url) cho o party.</summary>
        public void SetMemberAvatar(int index, Sprite sprite)
        {
            if (_members == null || index < 0 || index >= _members.Length) return;
            var slot = _members[index];
            if (slot != null && slot.avatar != null) slot.avatar.sprite = sprite;
        }

        /// <summary>[Aemy 2026-10-22] Tra o ve avatar mac dinh (nguoi cu roi o -> khong de lai anh cua ho).</summary>
        public void ResetMemberAvatar(int index)
        {
            if (_members == null || index < 0 || index >= _members.Length) return;
            var slot = _members[index];
            if (slot == null || slot.avatar == null) return;
            slot.avatar.sprite = _defaultAvatars != null && index < _defaultAvatars.Length ? _defaultAvatars[index] : null;
        }

        /// <summary>Dua o ve trang thai TRONG (hien nut +/MOI). O van hien tren layout.</summary>
        public void SetEmpty(int index)
        {
            if (_members == null || index < 0 || index >= _members.Length) return;
            var slot = _members[index];
            if (slot == null) return;
            if (slot.root != null) slot.root.SetActive(true);
            if (slot.filledGroup != null) slot.filledGroup.SetActive(false);
            if (slot.emptyGroup != null) slot.emptyGroup.SetActive(true);
        }

        /// <summary>Giu de tuong thich: dua o ve trang thai trong (moi).</summary>
        public void ClearMember(int index)
        {
            SetEmpty(index);
        }

        /// <summary>Cap nhat so ban be dang online tren header.</summary>
        public void SetFriendsCount(int count)
        {
            if (_friendsLabel != null)
                _friendsLabel.text = string.Format(_friendsFormat, count);
        }

        /// <summary>Gan avatar cho o ban be thu index (bat o len).</summary>
        public void SetFriendAvatar(int index, Sprite sprite)
        {
            if (_friendAvatars == null || index < 0 || index >= _friendAvatars.Length) return;
            var img = _friendAvatars[index];
            if (img == null) return;
            if (sprite != null) img.sprite = sprite;
            img.gameObject.SetActive(true);
        }

        /// <summary>An cac o avatar ban be tu vi tri startIndex tro di (khi it ban hon so o).</summary>
        public void HideFriendAvatarsFrom(int startIndex)
        {
            if (_friendAvatars == null) return;
            for (int i = Mathf.Max(0, startIndex); i < _friendAvatars.Length; i++)
            {
                if (_friendAvatars[i] != null) _friendAvatars[i].gameObject.SetActive(false);
            }
        }
    }
}
