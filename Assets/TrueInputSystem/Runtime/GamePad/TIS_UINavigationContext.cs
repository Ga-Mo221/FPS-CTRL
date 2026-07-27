using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TrueInputSystem
{
    /// <summary>
    /// Manager trung tâm cho navigation 3 layer.
    ///
    /// Cách dùng:
    /// 1. Đặt component này trên 1 GameObject trong scene.
    /// 2. Tạo TabEntry cho mỗi Tab button — khai báo SubTab đầu tiên của tab đó.
    /// 3. Tạo SubTabEntry cho mỗi SubTab button — khai báo Content button đầu tiên của subtab đó.
    /// 4. Assign defaultTab, defaultSubTab, defaultContent.
    ///
    /// Khi LT/RT đổi Tab  → UINavigationContext tự redirect SubTab và Content focus.
    /// Khi LB/RB đổi SubTab → UINavigationContext tự redirect Content focus.
    /// </summary>
    [AddComponentMenu("TIS/Demo/TIS UI Navigation Context")]
    [DefaultExecutionOrder(-60)]
    public class TISUINavigationContext : MonoBehaviour
    {
        public static TISUINavigationContext Instance { get; private set; }

        // ── Mapping: Tab → SubTab đầu tiên + Content đầu tiên ────────────────────
        [System.Serializable]
        public class TabEntry
        {
            public TISGamepadUIButton tabButton;

            [Tooltip("SubTab đầu tiên sẽ được highlight khi tab này được chọn.")]
            public TISGamepadUIButton firstSubTab;

            [Tooltip("Content button đầu tiên sẽ được focus khi tab này được chọn.")]
            public TISGamepadUIButton firstContent;
        }

        // ── Mapping: SubTab → Content đầu tiên ───────────────────────────────────
        [System.Serializable]
        public class SubTabEntry
        {
            public TISGamepadUIButton subTabButton;

            [Tooltip("Content button đầu tiên sẽ được focus khi subtab này được chọn.")]
            public TISGamepadUIButton firstContent;
        }

        [Header("Tab Mapping")] public List<TabEntry> tabs = new List<TabEntry>();

        [Header("SubTab Mapping")] public List<SubTabEntry> subTabs = new List<SubTabEntry>();

        [Header("Initial Selection")] public TISGamepadUIButton defaultTab;
        public TISGamepadUIButton defaultSubTab;
        public TISGamepadUIButton defaultContent;

        // ── Internal lookup ───────────────────────────────────────────────────────
        private Dictionary<TISGamepadUIButton, TabEntry> tabLookup = new();
        private Dictionary<TISGamepadUIButton, SubTabEntry> subTabLookup = new();

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Build lookup tables
            foreach (var entry in tabs)
                if (entry.tabButton != null)
                    tabLookup[entry.tabButton] = entry;

            foreach (var entry in subTabs)
                if (entry.subTabButton != null)
                    subTabLookup[entry.subTabButton] = entry;
        }

        private void Start()
        {
            // Subscribe vào onBecameActiveInLayer của tất cả Tab và SubTab buttons
            foreach (var entry in tabs)
            {
                if (entry.tabButton == null) continue;
                // Capture để dùng trong lambda
                var captured = entry;
                entry.tabButton.onBecameActiveInLayer.AddListener(_ => OnTabBecameActive(captured.tabButton));
            }

            foreach (var entry in subTabs)
            {
                if (entry.subTabButton == null) continue;
                var captured = entry;
                entry.subTabButton.onBecameActiveInLayer.AddListener(_ => OnSubTabBecameActive(captured.subTabButton));
            }

            // Khởi tạo trạng thái ban đầu
            if (defaultTab != null) defaultTab.SetAsActiveInLayer();
            if (defaultSubTab != null) defaultSubTab.SetAsActiveInLayer();
            // SetSelectedGameObject sẽ trigger OnSelect trên defaultContent qua EventSystem
            if (defaultContent != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(defaultContent.gameObject);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Manually fire OnDeselect trên content button hiện tại rồi mới đổi focus.
        /// Unity chỉ tự gọi OnDeselect khi button cũ vẫn active — nếu tab/subtab
        /// ẩn content cũ trước khi đổi, OnDeselect bị bỏ qua. Hàm này đảm bảo
        /// event luôn được gọi bất kể trạng thái active của GameObject.
        /// </summary>
        private void MoveContentFocus(TISGamepadUIButton newContent)
        {
            if (newContent == null || EventSystem.current == null) return;

            // Lấy content button đang được focus hiện tại
            var currentGO = EventSystem.current.currentSelectedGameObject;
            if (currentGO != null && currentGO != newContent.gameObject)
            {
                var currentBtn = currentGO.GetComponent<TISGamepadUIButton>();
                if (currentBtn != null)
                {
                    // Fire onDeselect thủ công — đảm bảo event được gọi
                    // dù button cũ có bị inactive hay không
                    currentBtn.onDeselect?.Invoke();
                }
            }

            EventSystem.current.SetSelectedGameObject(newContent.gameObject);
        }

        // ── Callbacks ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Khi Tab đổi: highlight SubTab đầu tiên của tab mới, focus Content đầu tiên.
        /// </summary>
        private void OnTabBecameActive(TISGamepadUIButton tabBtn)
        {
            if (!tabLookup.TryGetValue(tabBtn, out var entry)) return;

            // Redirect SubTab highlight — SetAsActiveInLayer đã tự deselect SubTab cũ
            if (entry.firstSubTab != null)
                entry.firstSubTab.SetAsActiveInLayer();

            // Redirect Content focus: ưu tiên firstContent của SubTab đầu tiên của tab mới
            TISGamepadUIButton targetContent = entry.firstContent;
            if (entry.firstSubTab != null && subTabLookup.TryGetValue(entry.firstSubTab, out var subEntry))
                if (subEntry.firstContent != null)
                    targetContent = subEntry.firstContent;

            MoveContentFocus(targetContent);
        }

        /// <summary>
        /// Khi SubTab đổi: focus Content button đầu tiên của subtab mới.
        /// </summary>
        private void OnSubTabBecameActive(TISGamepadUIButton subTabBtn)
        {
            if (!subTabLookup.TryGetValue(subTabBtn, out var entry)) return;

            MoveContentFocus(entry.firstContent);
        }
    }
}