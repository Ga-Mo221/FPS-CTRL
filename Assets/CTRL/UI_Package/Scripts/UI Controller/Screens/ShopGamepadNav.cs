// [Aemy 2026-10-12] FILE MOI. Tay cam o CUA HANG: noi the noi bat <-> luoi vat pham.
// [Aemy 2026-10-19] 3 tab (NOI BAT / VU KHI / NHAN VAT) la SubTab -> doi bang LB / RB (giong danh muc Cai dat).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GaMo.TrueInputSystem;
using Game.Shop;

namespace Game.UI
{
    /// <summary>
    /// Gan len ShopPanel. Chay SAU TISDynamicListNav (no noi luoi the 4 huong) va chi sua cac canh "mep":
    ///   the noi bat:  ‹  <->  MUA  <->  ›  --phai--> cot trai cua luoi
    ///   luoi: cot trai --trai--> › (hoac MUA) khi co the noi bat
    /// Tab la SubTab (LB/RB): Content khong nhay len tab nua. Doi tab -> focus vao MUA (Noi bat) hoac the dau luoi.
    /// Mo Cua hang tu cho khac (vd Trang bi -> "Mua o cua hang" mo thang tab VU KHI) -> dong bo tab active cua TIS.
    /// Cap nhat moi frame vi luoi sinh lai khi doi tab / mua xong.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class ShopGamepadNav : MonoBehaviour
    {
        [SerializeField] private ShopController _shop;
        [SerializeField] private TISGamepadUIButton[] _tabs;       // de trong = tim "Tab_*_Bg"
        [SerializeField] private TISGamepadUIButton _buy;          // de trong = tim "BuyButtonBg"
        [SerializeField] private RectTransform _grid;              // de trong = tim "ItemGrid"

        private TISGamepadUIButton _prev, _next;
        private readonly List<TISGamepadUIButton> _cards = new List<TISGamepadUIButton>();
        private int _lastTab = -1;
        private bool _focusPending;
        private float _focusUntil;

        private void Awake()
        {
            if (_shop == null) _shop = GetComponentInChildren<ShopController>(true);
            var tabs = new List<TISGamepadUIButton>();
            foreach (var b in GetComponentsInChildren<TISGamepadUIButton>(true))
            {
                if (b.name.StartsWith("Tab_") && b.name.EndsWith("_Bg")) tabs.Add(b);
                else if (_buy == null && b.name == "BuyButtonBg") _buy = b;
            }
            if (_tabs == null || _tabs.Length == 0) _tabs = tabs.ToArray();
            if (_grid == null) foreach (var t in GetComponentsInChildren<RectTransform>(true)) if (t.name == "ItemGrid") { _grid = t; break; }
        }

        private void OnEnable() { _lastTab = -1; }

        private void LateUpdate()
        {
            if (_tabs == null || _tabs.Length == 0) return;
            FindArrows();
            CollectCards();

            int cur = _shop != null ? Mathf.Clamp(_shop.CurrentTab, 0, _tabs.Length - 1) : 0;
            SyncActiveSubTab(cur);

            bool featOn = Usable(_buy);
            bool prevOn = featOn && Usable(_prev), nextOn = featOn && Usable(_next);

            TISGamepadUIButton topLeft = null; float topY = float.NegativeInfinity, leftX = float.PositiveInfinity;
            foreach (var c in _cards)
            {
                var p = c.transform.position;
                if (p.y > topY + 1f || (Mathf.Abs(p.y - topY) <= 1f && p.x < leftX)) { topLeft = c; topY = p.y; leftX = p.x; }
            }
            float minX = float.PositiveInfinity; foreach (var c in _cards) minX = Mathf.Min(minX, c.transform.position.x);

            // the noi bat (khong con len tab: tab = LB/RB)
            if (featOn)
            {
                _buy.navUp = null; _buy.navDown = null;
                _buy.navLeft = prevOn ? _prev : null;
                _buy.navRight = nextOn ? _next : topLeft;
                if (prevOn) { _prev.navUp = null; _prev.navDown = null; _prev.navLeft = null; _prev.navRight = _buy; }
                if (nextOn) { _next.navUp = null; _next.navDown = null; _next.navLeft = _buy; _next.navRight = topLeft; }
            }

            // mep luoi
            var leftOfGrid = featOn ? (nextOn ? _next : _buy) : null;
            foreach (var c in _cards)
            {
                var p = c.transform.position;
                if (Mathf.Abs(p.y - topY) <= 1f) c.navUp = null;
                if (Mathf.Abs(p.x - minX) <= 1f) c.navLeft = leftOfGrid;
            }

            // doi tab -> dua focus vao noi dung cua tab moi (luoi sinh lai sau vai frame -> cho toi 1s)
            if (cur != _lastTab)
            {
                _focusPending = _lastTab >= 0; // lan dau mo panel: de TIS / firstContent tu lo
                _focusUntil = Time.unscaledTime + 1f;
                _lastTab = cur;
            }
            if (_focusPending)
            {
                var target = featOn ? _buy : topLeft;
                if (target != null) { FocusIfGamepad(target); _focusPending = false; }
                else if (Time.unscaledTime > _focusUntil)
                {
                    // Tab trong (vd NHAN VAT chua co mon nao) -> bo focus dang nam tren nut da an (MUA cua tab truoc).
                    _focusPending = false;
                    var es = EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != null && !es.currentSelectedGameObject.activeInHierarchy)
                        es.SetSelectedGameObject(null);
                }
            }
        }

        // Tab dang mo cua ShopController khac tab active cua TIS (mo Shop thang vao tab VU KHI...) -> dong bo,
        // de LB/RB tinh dung tu tab dang hien.
        private void SyncActiveSubTab(int cur)
        {
            var tab = _tabs[cur];
            if (tab == null || tab.context == null || !tab.isActiveAndEnabled) return;
            var active = tab.context.GetActiveInLayer(TISGamepadUIButton.NavigationLayer.SubTab);
            if (active == tab) return;
            // Panel nay chi bat khi dang o CUA HANG -> SubTab active phai la tab cua Shop (ke ca khi dang la
            // danh muc cua man khac do mo Shop bang chuot / tu man khac).
            tab.context.SetActiveInLayer(tab); // onBecameActiveInLayer -> TISLayerActivateClick -> SelectTab(cur) (cung tab)
        }

        private static void FocusIfGamepad(TISGamepadUIButton target)
        {
            var br = TISInputBridge.Instance;
            if (br != null && !br.IsGamepad) return;
            var es = EventSystem.current;
            if (es != null && target.isActiveAndEnabled) es.SetSelectedGameObject(target.gameObject);
        }

        private void FindArrows()
        {
            if (_prev != null && _next != null) return;
            foreach (var b in GetComponentsInChildren<TISGamepadUIButton>(true))
            {
                if (_prev == null && b.name == "bt_feat_prev") _prev = b;
                else if (_next == null && b.name == "bt_feat_next") _next = b;
            }
        }

        private void CollectCards()
        {
            _cards.Clear();
            if (_grid == null) return;
            foreach (var b in _grid.GetComponentsInChildren<TISGamepadUIButton>(false)) if (b.isActiveAndEnabled) _cards.Add(b);
        }

        private static bool Usable(TISGamepadUIButton b) { return b != null && b.isActiveAndEnabled; }
    }
}
