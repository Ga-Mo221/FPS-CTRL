using System;
using UnityEngine;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Panel "SÚNG CHỦ LỰC": hiển thị các súng nhiều kill nhất.
    /// Presentation thuần — không đụng backend. MenuShellBinder lấy dữ liệu từ
    /// PlayFab (GetTopWeapons) rồi gọi SetWeapon() cho từng slot.
    /// [Aemy 2026-10-22] Scene chỉ có 2 dòng -> tự nhân bản dòng cuối cho đủ _maxSlots (3) và xếp lại
    /// gọn hơn (cao _slotHeight, cách _slotGap) để 3 dòng vẫn nằm trên khung chat khi chat mở.
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

        [Header("Số dòng tối đa (thiếu thì nhân bản dòng cuối)")]
        [SerializeField] private int _maxSlots = 3;
        [Tooltip("Chiều cao mỗi dòng khi phải xếp lại (đơn vị canvas).")]
        [SerializeField] private float _slotHeight = 41f;
        [Tooltip("Khoảng cách giữa 2 dòng khi phải xếp lại.")]
        [SerializeField] private float _slotGap = 8f;

        // Số slot có sẵn (để binder biết lấy top mấy súng).
        public int SlotCount => _slots != null ? Mathf.Min(_slots.Length, _maxSlots) : 0;

        private void Awake() { EnsureSlots(); }

        // Đặt 1 slot theo hạng: tên súng + số kill.
        public void SetWeapon(int index, string weaponName, int kills)
        {
            if (_slots == null || index < 0 || index >= SlotCount) return;
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

        // Thiếu dòng -> nhân bản dòng cuối, rồi xếp lại tất cả từ mép trên của dòng đầu.
        private void EnsureSlots()
        {
            if (_slots == null || _slots.Length == 0 || _slots.Length >= _maxSlots) return;
            var last = _slots[_slots.Length - 1];
            var first = _slots[0];
            if (last == null || last.root == null || first == null || first.root == null) return;

            var list = new System.Collections.Generic.List<Slot>(_slots);
            for (int i = _slots.Length; i < _maxSlots; i++)
            {
                var go = Instantiate(last.root, last.root.transform.parent, false);
                go.name = "Slot " + (i + 1);
                go.SetActive(false);
                list.Add(new Slot
                {
                    root = go,
                    nameLabel = FindSame(go.transform, last.root.transform, last.nameLabel),
                    killsLabel = FindSame(go.transform, last.root.transform, last.killsLabel),
                });
            }
            _slots = list.ToArray();

            var top = (RectTransform)first.root.transform;
            float topEdge = top.anchoredPosition.y + top.rect.height * (1f - top.pivot.y);
            for (int i = 0; i < _slots.Length; i++)
            {
                var rt = _slots[i].root != null ? (RectTransform)_slots[i].root.transform : null;
                if (rt == null) continue;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, _slotHeight);
                float y = topEdge - i * (_slotHeight + _slotGap) - _slotHeight * (1f - rt.pivot.y);
                rt.anchoredPosition = new Vector2(top.anchoredPosition.x, y);
            }
        }

        // Ô chữ trong bản sao = ô cùng đường dẫn tương đối với ô gốc trong dòng mẫu.
        private static TMP_Text FindSame(Transform cloneRoot, Transform srcRoot, TMP_Text srcLabel)
        {
            if (srcLabel == null) return null;
            if (srcLabel.transform == srcRoot) return cloneRoot.GetComponent<TMP_Text>();
            string path = srcLabel.name;
            for (var p = srcLabel.transform.parent; p != null && p != srcRoot; p = p.parent) path = p.name + "/" + path;
            var t = cloneRoot.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}
