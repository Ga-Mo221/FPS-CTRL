using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the "Finding match" screen.
    /// Two team rosters, a title, an elapsed timer, ping and a cancel button.
    /// </summary>
    public class MatchmakingController : MonoBehaviour
    {
        /// <summary>A single roster slot view (avatar + name + optional "YOU"/status).</summary>
        [Serializable]
        public struct RosterSlot
        {
            public Image avatar;
            public TMP_Text nameLabel;
            public TMP_Text statusLabel;
            public GameObject highlight; // [Aemy] nen xanh danh dau local ("BAN"); bat/tat theo slot // "BẠN" / "TRƯỞNG" / empty
        }

        [Header("Header")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _modeLabel;
        [SerializeField] private TMP_Text _timeLabel;

        [Header("Rosters")]
        [SerializeField] private RosterSlot[] _team1Slots;
        [SerializeField] private RosterSlot[] _team2Slots;
        [SerializeField] private TMP_Text _team1HeaderLabel;
        [SerializeField] private TMP_Text _team2HeaderLabel;

        [Header("Footer")]
        [SerializeField] private TMP_Text _pingLabel;
        [SerializeField] private Button _cancelButton;

        [Header("Timer")]
        [Tooltip("Count elapsed search time automatically and display it as mm:ss.")]
        [SerializeField] private bool _autoCountTime = true;

        public event Action CancelClicked;

        private float _elapsed;
        private bool _counting;

        private void Awake()
        {
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancel);
        }

        private void OnEnable()
        {
            if (_autoCountTime) StartTimer();
        }

        private void OnDestroy()
        {
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(OnCancel);
        }

        private void Update()
        {
            if (!_counting) return;
            _elapsed += Time.deltaTime;
            SetTimeLabel(_elapsed);
        }

        private void OnCancel()
        {
            StopTimer();
            CancelClicked?.Invoke();
        }

        // ---- Public setters ----
        public void SetTitle(string title) => Set(_titleLabel, title);
        public void SetMode(string mode) => Set(_modeLabel, mode);
        public void SetPing(int ping) => Set(_pingLabel, $"{ping} ms");

        // ================= [Aemy 2026-10-10] THONG TIN MAP =================
        [Header("Map (Aemy 2026-10-10)")]
        [SerializeField] private TMP_Text _mapLabel;              // ten map (goc tren trai, duoi ten che do)
        [SerializeField] private TMP_Text _mapDescriptionLabel;   // mo ta map (duoi cung)
        [SerializeField] private Image _mapBackground;            // anh nen = anh map dang chon

        /// <summary>Hien ten + mo ta + anh nen cua map dang chon. Goi duoc ca khi man dang an.</summary>
        public void SetMap(string mapName, string description, Sprite background)
        {
            Set(_mapLabel, mapName);
            Set(_mapDescriptionLabel, description);
            if (_mapBackground != null) { _mapBackground.sprite = background; _mapBackground.enabled = background != null; }
        }
        // [UI-INTEGRATION 2026-09-13] Set text thoi gian truc tiep (dung cho countdown vao tran o waiting-room).
        public void SetTimeText(string text) => Set(_timeLabel, text);
        public void SetTeamHeaders(string team1, string team2)
        {
            SetTeamHeaders(team1, team2, null, null);
        }

        // [Aemy 2026-10-17] Tieu de cot (tu tao neu scene chua co) + mau doi: xanh = doi minh, do = doi dich.
        public void SetTeamHeaders(string team1, string team2, Color? color1, Color? color2)
        {
            if (_team1HeaderLabel == null) _team1HeaderLabel = CreateHeader(_team1Slots, "Team1Header");
            if (_team2HeaderLabel == null) _team2HeaderLabel = CreateHeader(_team2Slots, "Team2Header");
            Set(_team1HeaderLabel, team1);
            Set(_team2HeaderLabel, team2);
            if (_team1HeaderLabel != null) { _team1HeaderLabel.gameObject.SetActive(!string.IsNullOrEmpty(team1)); _team1HeaderLabel.color = color1 ?? new Color(0.85f, 0.88f, 0.9f, 1f); }
            if (_team2HeaderLabel != null) { _team2HeaderLabel.gameObject.SetActive(!string.IsNullOrEmpty(team2)); _team2HeaderLabel.color = color2 ?? new Color(0.85f, 0.88f, 0.9f, 1f); }
        }

        private TMP_Text CreateHeader(RosterSlot[] slots, string name)
        {
            var root = SlotRoot(slots, 0);
            if (root == null) return null;
            var rt0 = root.transform as RectTransform;
            if (rt0 == null || rt0.parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(rt0.parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt0.anchorMin; rt.anchorMax = rt0.anchorMax; rt.pivot = rt0.pivot;
            rt.sizeDelta = new Vector2(rt0.rect.width, 30f);
            rt.anchoredPosition = rt0.anchoredPosition + new Vector2(0f, rt0.rect.height * (1f - rt0.pivot.y) + 22f);
            var t = go.AddComponent<TextMeshProUGUI>();
            var src = slots[0].nameLabel;
            if (src != null) { t.font = src.font; t.fontSharedMaterial = src.fontSharedMaterial; }
            t.fontSize = 18; t.fontStyle = FontStyles.Bold; t.raycastTarget = false; t.enableWordWrapping = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            return t;
        }

        public void SetTeam1Member(int index, string name, Sprite avatar, string status = "")
            => SetMember(_team1Slots, index, name, avatar, status);

        public void SetTeam2Member(int index, string name, Sprite avatar, string status = "")
            => SetMember(_team2Slots, index, name, avatar, status);

        public void StartTimer()
        {
            _counting = true;
            SetTimeLabel(_elapsed);
        }

        public void StopTimer() => _counting = false;

        public void ResetTimer()
        {
            _elapsed = 0f;
            SetTimeLabel(_elapsed);
        }

        // ---- Helpers ----
        private void SetMember(RosterSlot[] slots, int index, string name, Sprite avatar, string status)
        {
            if (slots == null || index < 0 || index >= slots.Length) return;
            var slot = slots[index];
            // [ROSTER-FIX 2026-09-29 Aemy] O trong (khong ten & khong status) -> AN ca o cho gon.
            bool empty = string.IsNullOrEmpty(name) && string.IsNullOrEmpty(status);
            var root = SlotRoot(slots, index);
            if (root != null) root.SetActive(!empty);
            if (empty) return;
            if (slot.nameLabel != null) slot.nameLabel.text = name;
            if (slot.avatar != null && avatar != null) slot.avatar.sprite = avatar;
            if (slot.statusLabel != null) slot.statusLabel.text = status;
            if (slot.highlight != null) slot.highlight.SetActive(!string.IsNullOrEmpty(status));
        }

        // [ROSTER-FIX 2026-09-29 Aemy] Suy ra GameObject goc cua 1 o roster (de an/hien) khong can wire Inspector.
        private static UnityEngine.Transform SlotAnchor(RosterSlot s)
        {
            if (s.nameLabel != null) return s.nameLabel.transform;
            if (s.avatar != null) return s.avatar.transform;
            if (s.statusLabel != null) return s.statusLabel.transform;
            return null;
        }
        private UnityEngine.GameObject SlotRoot(RosterSlot[] slots, int index)
        {
            var anchor = SlotAnchor(slots[index]);
            if (anchor == null) return null;
            UnityEngine.Transform other = null;
            for (int i = 0; i < slots.Length; i++) { if (i == index) continue; var a = SlotAnchor(slots[i]); if (a != null) { other = a; break; } }
            var shared = other != null ? Lca(anchor, other) : anchor.parent;
            if (shared == null) return anchor.gameObject;
            var t = anchor;
            while (t.parent != null && t.parent != shared) t = t.parent;
            return t.gameObject;
        }
        private static UnityEngine.Transform Lca(UnityEngine.Transform x, UnityEngine.Transform y)
        {
            var set = new System.Collections.Generic.HashSet<UnityEngine.Transform>();
            for (var t = x; t != null; t = t.parent) set.Add(t);
            for (var t = y; t != null; t = t.parent) if (set.Contains(t)) return t;
            return null;
        }

        private void SetTimeLabel(float seconds)
        {
            if (_timeLabel == null) return;
            int total = Mathf.FloorToInt(seconds);
            _timeLabel.text = $"{total / 60:00}:{total % 60:00}";
        }

        private static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}