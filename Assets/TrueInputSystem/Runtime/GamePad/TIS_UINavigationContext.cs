using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Manager trung tâm cho toàn bộ hệ thống navigation.
    ///
    /// Trách nhiệm:
    /// • Nhận đăng ký từ tất cả TISGamepadUIButton khi Start.
    /// • Giữ state "button nào đang active" cho từng layer (Tab, SubTab, Content).
    /// • Kiểm tra tính hợp lệ trước khi cho phép chuyển button ở bất kỳ layer nào.
    /// • Detect và tự động restore khi toàn bộ selection bị mất.
    ///
    /// Button KHÔNG tự giữ state — chỉ báo "tôi muốn move" rồi Context quyết định.
    /// </summary>
    [AddComponentMenu("TIS/Demo/TIS UI Navigation Context")]
    [DefaultExecutionOrder(-60)]
    public class TISUINavigationContext : MonoBehaviour
    {
        // ── Tab / SubTab mapping ──────────────────────────────────────────────

        [System.Serializable]
        public class TabEntry
        {
            public TISGamepadUIButton tabButton;

            [Tooltip("SubTab đầu tiên sẽ được highlight khi tab này được chọn.")]
            public TISGamepadUIButton firstSubTab;

            [Tooltip("Content button đầu tiên sẽ được focus khi tab này được chọn.")]
            public TISGamepadUIButton firstContent;
        }

        [System.Serializable]
        public class SubTabEntry
        {
            public TISGamepadUIButton subTabButton;

            [Tooltip("Content button đầu tiên sẽ được focus khi subtab này được chọn.")]
            public TISGamepadUIButton firstContent;
        }

        [Header("Tab Mapping")]    public List<TabEntry>    tabs    = new List<TabEntry>();
        [Header("SubTab Mapping")] public List<SubTabEntry> subTabs = new List<SubTabEntry>();

        [Header("Initial Selection")]
        public TISGamepadUIButton defaultTab;
        public TISGamepadUIButton defaultSubTab;
        public TISGamepadUIButton defaultContent;

        [Header("Debug")]
        [Tooltip("Bật để log mỗi lần chuyển Tab / SubTab / Content và mỗi lần nhấn button.")]
        public bool debugLog = true;

        // ── Internal state — Context là nơi DUY NHẤT giữ state này ───────────

        /// <summary>
        /// Button đang active trong mỗi layer.
        /// Tab/SubTab → virtual selected (alwaysActive, visual Highlighted).
        /// Content    → button được ghi nhận là "đang focus" theo EventSystem.
        /// </summary>
        private readonly Dictionary<TISGamepadUIButton.NavigationLayer, TISGamepadUIButton>
            activeButtons = new Dictionary<TISGamepadUIButton.NavigationLayer, TISGamepadUIButton>();

        /// <summary>
        /// Toàn bộ button đã đăng ký, nhóm theo layer để query nhanh.
        /// </summary>
        private readonly Dictionary<TISGamepadUIButton.NavigationLayer, List<TISGamepadUIButton>>
            registeredByLayer = new Dictionary<TISGamepadUIButton.NavigationLayer, List<TISGamepadUIButton>>
            {
                { TISGamepadUIButton.NavigationLayer.Tab,     new List<TISGamepadUIButton>() },
                { TISGamepadUIButton.NavigationLayer.SubTab,  new List<TISGamepadUIButton>() },
                { TISGamepadUIButton.NavigationLayer.Content, new List<TISGamepadUIButton>() },
            };

        // Lookup nhanh cho tab/subtab mapping
        private readonly Dictionary<TISGamepadUIButton, TabEntry>    tabLookup    = new();
        private readonly Dictionary<TISGamepadUIButton, SubTabEntry> subTabLookup = new();

        // ── Debug ─────────────────────────────────────────────────────────────

        internal void Log(string message)
        {
            if (debugLog)
                Debug.Log($"[TIS Nav] {message}", this);
        }

        private static string N(TISGamepadUIButton btn) => btn != null ? btn.name : "null";

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            RebuildLookups();
        }

        private void RebuildLookups()
        {
            tabLookup.Clear();
            subTabLookup.Clear();
            foreach (var entry in tabs)
                if (entry.tabButton != null) tabLookup[entry.tabButton] = entry;
            foreach (var entry in subTabs)
                if (entry.subTabButton != null) subTabLookup[entry.subTabButton] = entry;
        }

        private bool _started;

        private void Start()
        {
            // Buttons đăng ký qua Start() của chúng; Context có DefaultExecutionOrder -60
            // nên Start của Context chạy trước — RestoreInitialSelection() gọi lại sau
            // 1 frame để đảm bảo tất cả button đã đăng ký xong.
            _started = true;
            StartCoroutine(InitNextFrame());
        }

        private System.Collections.IEnumerator InitNextFrame()
        {
            yield return null;
            RestoreInitialSelection();
        }

        private void OnEnable()
        {
            // Khi GameObject được enable lại (không phải lần đầu Start),
            // restore về trạng thái ban đầu sau 1 frame.
            // Không dựa vào activeButtons.Count: lúc tắt, mọi button OnDisable → OnButtonDisabled
            // đã xoá sạch state → lần bật lại Count == 0, trước đây bỏ qua restore → không Tab/SubTab
            // nào active → LT/RT · LB/RB "chết" (vd mở Pause lần 2).
            if (_started)
                StartCoroutine(InitNextFrame());
        }

        private void Update()
        {
            // Guard: chỉ check khi player đang có input navigation
            if (!HasAnyNavigationInput()) return;

            // Detect mất Content selection → tự restore.
            // Focus còn trỏ vào object ĐÃ ẨN (vd nút của panel vừa đóng) cũng coi là mất:
            // EventSystem không tự bỏ chọn khi object bị tắt → trước đây focus kẹt ở đó mãi.
            if (EventSystem.current != null)
            {
                var cur = EventSystem.current.currentSelectedGameObject;
                if (cur == null || !cur.activeInHierarchy)
                    RestoreContentSelection();
            }
        }
        
        /// <summary>
        /// Scans all TISGamepadUIButton components that are children of this GameObject
        /// and registers them with this context. Safe to call at runtime or from the editor.
        /// </summary>
        public void ScanChildButtons()
        {
            // Clear existing registrations
            foreach (var list in registeredByLayer.Values)
                list.Clear();

            var buttonvalue = 0;
            var buttons = GetComponentsInChildren<TISGamepadUIButton>(includeInactive: true);
            foreach (var btn in buttons)
            {
                buttonvalue++;
                btn.SetContext(this);          // wire the button back to this context
                var list = registeredByLayer[btn.layer];
                if (!list.Contains(btn))
                    list.Add(btn);
            }
            
            Debug.Log($"Đẫ quét thành công {buttonvalue} con bên trong {transform.name}");

            RebuildLookups();
        }

        // ── Registration API (gọi từ Button.Start / OnDisable / OnDestroy) ───

        public void RegisterButton(TISGamepadUIButton btn)
        {
            var list = registeredByLayer[btn.layer];
            if (!list.Contains(btn))
                list.Add(btn);
        }

        public void UnregisterButton(TISGamepadUIButton btn)
        {
            registeredByLayer[btn.layer].Remove(btn);

            // Nếu button đang active bị unregister → clear state
            if (activeButtons.TryGetValue(btn.layer, out var current) && current == btn)
                activeButtons.Remove(btn.layer);
        }

        /// <summary>
        /// Gọi khi button bị disable (OnDisable). Nếu button này đang là active
        /// trong layer của nó, Context cập nhật state nhưng KHÔNG tự restore —
        /// để Update() detect qua EventSystem rồi xử lý đúng lúc.
        /// </summary>
        public void OnButtonDisabled(TISGamepadUIButton btn)
        {
            if (activeButtons.TryGetValue(btn.layer, out var current) && current == btn)
                activeButtons.Remove(btn.layer);
        }

        /// <summary>
        /// Gọi từ Button.OnSelect (EventSystem trigger) để Context ghi nhận
        /// Content button nào đang được chọn.
        /// </summary>
        public void NotifyButtonSelected(TISGamepadUIButton btn)
        {
            if (btn.layer == TISGamepadUIButton.NavigationLayer.Content)
                activeButtons[btn.layer] = btn;
        }

        // ── State query API (gọi từ Button.Update) ───────────────────────────

        /// <summary>
        /// Kiểm tra button này có đang là active trong layer của nó không.
        /// Dùng để Tab/SubTab quyết định có đọc input không.
        /// </summary>
        public bool IsActiveInLayer(TISGamepadUIButton btn)
        {
            return activeButtons.TryGetValue(btn.layer, out var current) && current == btn;
        }

        /// <summary>
        /// Lấy button đang active trong layer chỉ định.
        /// </summary>
        public TISGamepadUIButton GetActiveInLayer(TISGamepadUIButton.NavigationLayer layer)
        {
            activeButtons.TryGetValue(layer, out var btn);
            return btn;
        }

        // ── Move request API (gọi từ Button khi có input) ────────────────────

        /// <summary>
        /// Button Content đang có focus yêu cầu chuyển sang target.
        /// Context kiểm tra: target có hợp lệ không, có đang active không?
        /// </summary>
        public void RequestContentMove(TISGamepadUIButton from, TISGamepadUIButton target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            // Content KHONG duoc nhay len nut Tab / SubTab bang D-pad / stick
            // (vd hang map bam len -> thanh che do). Tab = LT/RT, SubTab = LB/RB. Nut layer nhan focus that
            // se doc truc ngang theo vong Tab/SubTab va giu trang thai Selected -> loan focus.
            if (from != null && from.layer == TISGamepadUIButton.NavigationLayer.Content
                && target.layer != TISGamepadUIButton.NavigationLayer.Content)
            {
                Log($"<color=#EF9A9A>[Content]</color> chan \"{N(from)}\" → {target.layer} \"{N(target)}\"");
                return;
            }
            if (EventSystem.current == null) return;

            Log($"<color=#6EC6FF>[Content]</color> \"{N(from)}\" → \"{N(target)}\"");

            EventSystem.current.SetSelectedGameObject(target.gameObject);
            TISGamepadUIButton.s_NextMoveTime[TISGamepadUIButton.NavigationLayer.Content]
                = Time.unscaledTime + from.holdDelay;
        }

        /// <summary>
        /// Button Tab/SubTab yêu cầu chuyển sang target trong cùng layer.
        /// Context kiểm tra tính hợp lệ, cập nhật state, và redirect Content nếu cần.
        /// </summary>
        public void RequestLayerMove(TISGamepadUIButton from, TISGamepadUIButton target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;

            var layer = from.layer;

            // Kiểm tra: button hiện tại trong layer có phải là 'from' không?
            // (tránh trường hợp stale button gửi request)
            if (!activeButtons.TryGetValue(layer, out var current) || current != from) return;

            Log($"<color=#FFD54F>[{layer}]</color> \"{N(from)}\" → \"{N(target)}\"");

            // Thực hiện chuyển
            ActivateButtonInLayer(target);

            TISGamepadUIButton.s_NextMoveTime[layer] = Time.unscaledTime + from.holdDelay;
        }

        // ── Internal activation logic ─────────────────────────────────────────

        /// <summary>
        /// Set button thành active trong layer của nó.
        /// Cập nhật state, visual, và redirect Content/SubTab nếu là Tab/SubTab.
        /// </summary>
        private void ActivateButtonInLayer(TISGamepadUIButton btn)
        {
            var layer = btn.layer;

            // Deactivate button cũ
            if (activeButtons.TryGetValue(layer, out var prev) && prev != null && prev != btn)
                prev.ClearActiveVisual();

            // Ghi state mới
            activeButtons[layer] = btn;

            // Apply visual và fire event cho button mới
            btn.ApplyActiveVisual();

            // Redirect sub-layers theo hierarchy
            if (layer == TISGamepadUIButton.NavigationLayer.Tab)
                OnTabActivated(btn);
            else if (layer == TISGamepadUIButton.NavigationLayer.SubTab)
                OnSubTabActivated(btn);
        }

        /// <summary>
        /// Khi Tab đổi: redirect SubTab active và Content focus.
        /// </summary>
        private void OnTabActivated(TISGamepadUIButton tabBtn)
        {
            if (!tabLookup.TryGetValue(tabBtn, out var entry)) return;

            // Clear toàn bộ SubTab buttons (không chỉ button đang track trong activeButtons),
            // vì button cũ có thể vẫn giữ visual Highlighted mà không được track đúng.
            foreach (var subTabBtn in registeredByLayer[TISGamepadUIButton.NavigationLayer.SubTab])
            {
                if (subTabBtn != null)
                    subTabBtn.ClearActiveVisual();
            }
            activeButtons.Remove(TISGamepadUIButton.NavigationLayer.SubTab);

            if (entry.firstSubTab != null)
            {
                Log($"    ↳ auto <color=#FFD54F>[SubTab]</color> → \"{N(entry.firstSubTab)}\"");
                ActivateButtonInLayer(entry.firstSubTab);
            }

            // Redirect Content focus: ưu tiên firstContent của SubTab đầu tiên
            TISGamepadUIButton targetContent = entry.firstContent;
            if (entry.firstSubTab != null && subTabLookup.TryGetValue(entry.firstSubTab, out var subEntry)
                && subEntry.firstContent != null)
                targetContent = subEntry.firstContent;

            MoveContentFocus(targetContent);
        }

        /// <summary>
        /// Khi SubTab đổi: redirect Content focus về firstContent của subtab mới.
        /// </summary>
        private void OnSubTabActivated(TISGamepadUIButton subTabBtn)
        {
            if (!subTabLookup.TryGetValue(subTabBtn, out var entry)) return;
            MoveContentFocus(entry.firstContent);
        }

        /// <summary>
        /// Chuyển EventSystem focus sang Content button mới.
        /// Fire OnDeselect thủ công trên button cũ đề phòng Unity bỏ qua
        /// khi button cũ đã inactive.
        /// </summary>
        private void MoveContentFocus(TISGamepadUIButton newContent)
        {
            if (newContent == null || EventSystem.current == null) return;

            Log($"    ↳ auto <color=#6EC6FF>[Content]</color> focus → \"{N(newContent)}\"");

            var currentGO = EventSystem.current.currentSelectedGameObject;
            if (currentGO != null && currentGO != newContent.gameObject)
            {
                var currentBtn = currentGO.GetComponent<TISGamepadUIButton>();
                if (currentBtn != null)
                    currentBtn.onDeselect?.Invoke();
            }

            EventSystem.current.SetSelectedGameObject(newContent.gameObject);
        }

        // ── Restore logic ─────────────────────────────────────────────────────

        /// <summary>
        /// Restore toàn bộ Tab/SubTab active state và Content focus về default.
        /// Gọi khi Start, OnEnable, hoặc khi toàn bộ selection bị mất.
        /// </summary>
        private void RestoreInitialSelection()
        {
            if (defaultTab != null)     ActivateButtonInLayer(defaultTab);
            if (defaultSubTab != null)  ActivateButtonInLayer(defaultSubTab);
            if (defaultContent != null) MoveContentFocus(defaultContent);
        }

        /// <summary>
        /// Chỉ restore Content focus (không reset Tab/SubTab) — dùng khi
        /// Content selection bị mất nhưng Tab/SubTab vẫn đúng.
        /// </summary>
        private void RestoreContentSelection()
        {
            // Ưu tiên restore về Content button đã active trước đó
            if (activeButtons.TryGetValue(TISGamepadUIButton.NavigationLayer.Content, out var lastContent)
                && lastContent != null && lastContent.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(lastContent.gameObject);
                return;
            }

            // Không có last Content hợp lệ → dùng defaultContent
            if (defaultContent != null && defaultContent.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(defaultContent.gameObject);
        }

        // ── Input detection (dùng cho Update guard) ───────────────────────────

        private bool HasAnyNavigationInput()
        {
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f)   return true;
                // LT/RT (Tab) và LB/RB (SubTab) cũng là input điều hướng
                if (gamepad.leftTrigger.isPressed  || gamepad.rightTrigger.isPressed)  return true;
                if (gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed) return true;
                // Virtual cursor đang bật → left stick dành cho cursor
                if (!TISGamepadVirtualCursor.IsCursorActive
                    && gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                // Chỉ phím mũi tên — WASD là phím di chuyển, không được kéo focus UI về
                if (kb.upArrowKey.isPressed    || kb.downArrowKey.isPressed)  return true;
                if (kb.leftArrowKey.isPressed  || kb.rightArrowKey.isPressed) return true;
            }

            return false;
        }

        // ── Legacy public API (backward compat) ───────────────────────────────

        /// <summary>
        /// Gọi trực tiếp để force set một button thành active trong layer của nó.
        /// Tương đương SetAsActiveInLayer() cũ trên Button.
        /// </summary>
        public void SetActiveInLayer(TISGamepadUIButton btn)
        {
            if (btn != null) ActivateButtonInLayer(btn);
        }

        public TISGamepadUIButton GetDefaultContent() => defaultContent;

        /// <summary>
        /// Focus lại Content button (last active → defaultContent) nếu selection hiện tại
        /// không còn là button của context này — ví dụ sau khi tắt virtual cursor.
        /// </summary>
        public void RestoreFocusIfLost()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var cur = es.currentSelectedGameObject;
            if (cur != null && cur.activeInHierarchy
                && cur.TryGetComponent<TISGamepadUIButton>(out var btn) && btn.context == this)
                return;

            RestoreContentSelection();
        }
    }
}