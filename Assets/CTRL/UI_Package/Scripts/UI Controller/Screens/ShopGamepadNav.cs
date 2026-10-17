// [Aemy 2026-10-12] FILE MOI. Tay cam o CUA HANG: noi hang tab <-> the noi bat <-> luoi vat pham.
using System.Collections.Generic;
using UnityEngine;
using GaMo.TrueInputSystem;
using Game.Shop;

namespace Game.UI
{
    /// <summary>
    /// Gan len ShopPanel. Chay SAU TISDynamicListNav (no noi luoi the 4 huong) va chi sua cac canh "mep":
    ///   Tab (NOI BAT/SUNG/NHAN VAT) --xuong--> nut MUA cua the noi bat (tab Noi bat) / the dau luoi (tab khac)
    ///   the noi bat:  ‹  <->  MUA  <->  ›  --phai--> cot trai cua luoi
    ///   luoi: hang tren --len--> tab dang mo ; cot trai --trai--> › (hoac MUA) khi co the noi bat
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

        private void LateUpdate()
        {
            if (_tabs == null || _tabs.Length == 0) return;
            FindArrows();
            CollectCards();

            int cur = _shop != null ? Mathf.Clamp(_shop.CurrentTab, 0, _tabs.Length - 1) : 0;
            var curTab = _tabs[cur];
            bool featOn = Usable(_buy);
            bool prevOn = featOn && Usable(_prev), nextOn = featOn && Usable(_next);

            TISGamepadUIButton topLeft = null; float topY = float.NegativeInfinity, leftX = float.PositiveInfinity;
            foreach (var c in _cards)
            {
                var p = c.transform.position;
                if (p.y > topY + 1f || (Mathf.Abs(p.y - topY) <= 1f && p.x < leftX)) { topLeft = c; topY = p.y; leftX = p.x; }
            }
            float minX = float.PositiveInfinity; foreach (var c in _cards) minX = Mathf.Min(minX, c.transform.position.x);

            // hang tab
            foreach (var t in _tabs) if (t != null) t.navDown = featOn ? _buy : topLeft;
            if (_tabs[0] != null) _tabs[0].navLeft = null;
            if (_tabs[_tabs.Length - 1] != null) _tabs[_tabs.Length - 1].navRight = null;

            // the noi bat
            if (featOn)
            {
                _buy.navUp = curTab; _buy.navDown = null;
                _buy.navLeft = prevOn ? _prev : null;
                _buy.navRight = nextOn ? _next : topLeft;
                if (prevOn) { _prev.navUp = curTab; _prev.navDown = null; _prev.navLeft = null; _prev.navRight = _buy; }
                if (nextOn) { _next.navUp = curTab; _next.navDown = null; _next.navLeft = _buy; _next.navRight = topLeft; }
            }

            // mep luoi
            var leftOfGrid = featOn ? (nextOn ? _next : _buy) : null;
            foreach (var c in _cards)
            {
                var p = c.transform.position;
                if (Mathf.Abs(p.y - topY) <= 1f) c.navUp = curTab;
                if (Mathf.Abs(p.x - minX) <= 1f) c.navLeft = leftOfGrid;
            }
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
