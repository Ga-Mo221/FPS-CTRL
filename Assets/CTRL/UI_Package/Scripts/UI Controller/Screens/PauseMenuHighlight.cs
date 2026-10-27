using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Dieu phoi highlight cho menu Pause: LUON chi 1 muc sang.
    ///  - Khi mo panel: sang muc mac dinh (_defaultIndex, vd TIEP TUC).
    ///  - Hover 1 muc: muc do sang, cac muc khac tat.
    ///  - Roi chuot khoi muc: quay ve sang muc mac dinh.
    /// CACH DUNG:
    ///  - Gan script vao node cha cua cac muc (vd Menu).
    ///  - Moi phan tu _entries: keo 'hoverArea' = vung click cua muc (Row, co Graphic raycast),
    ///    'highlight' = object nen sang cua muc (de trong neu muc do khong co highlight).
    ///  - _defaultIndex = muc sang san khi mo (0 = muc dau tien / TIEP TUC).
    /// (Tu them EventTrigger vao hoverArea luc chay, khong dung nut bam cua muc.)
    /// </summary>
    public class PauseMenuHighlight : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Vung click cua muc (Row) - noi bat su kien hover.")]
            public GameObject hoverArea;
            [Tooltip("Object nen sang cua muc (bat/tat). De trong neu muc khong co highlight.")]
            public GameObject highlight;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];

        [Tooltip("Muc sang san khi mo panel (0 = muc dau, vd TIEP TUC). -1 = khong sang muc nao.")]
        [SerializeField] private int _defaultIndex = 0;

        // Muc dang hover (-1 = khong hover muc nao -> dung mac dinh).
        private int _hover = -1;
        // [Aemy 2026-10-09] Muc dang duoc chon bang tay cam (EventSystem selection). -1 = khong co.
        private int _selected = -1;
        private GaMo.TrueInputSystem.TISUINavigationContext _navContext;

        private void Awake()
        {
            // Gan su kien hover cho tung vung click.
            for (int i = 0; i < _entries.Length; i++)
            {
                int idx = i;
                var e = _entries[i];
                if (e == null || e.hoverArea == null) continue;
                var trig = e.hoverArea.GetComponent<EventTrigger>();
                if (trig == null) trig = e.hoverArea.AddComponent<EventTrigger>();
                AddTrigger(trig, EventTriggerType.PointerEnter, delegate { _hover = idx; Apply(); });
                AddTrigger(trig, EventTriggerType.PointerExit, delegate { if (_hover == idx) _hover = -1; Apply(); });
            }
        }

        // Moi lan mo panel: reset ve muc mac dinh.
        private void OnEnable()
        {
            _hover = -1;
            _selected = -1;
            Apply();
        }

        // [Aemy 2026-10-09] Tay cam: highlight di theo muc dang focus (truoc day chi theo chuot -> D-pad xuong
        // ROI TRAN nhung highlight van dung o TIEP TUC, nhin nhu khong chon duoc).
        private void Update()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            int idx = -1;
            if (sel != null)
                for (int i = 0; i < _entries.Length; i++)
                    if (_entries[i] != null && _entries[i].hoverArea == sel) { idx = i; break; }

            // Focus đang nằm trong panel Cài đặt (không phải dòng menu) → sáng theo tab đang active (LT/RT).
            if (idx < 0)
            {
                if (_navContext == null) _navContext = GetComponentInParent<GaMo.TrueInputSystem.TISUINavigationContext>();
                var activeTab = _navContext != null
                    ? _navContext.GetActiveInLayer(GaMo.TrueInputSystem.TISGamepadUIButton.NavigationLayer.Tab)
                    : null;
                if (activeTab != null)
                    for (int i = 0; i < _entries.Length; i++)
                        if (_entries[i] != null && _entries[i].hoverArea == activeTab.gameObject) { idx = i; break; }
            }

            if (idx != _selected) { _selected = idx; Apply(); }
        }

        // Bat highlight cho muc dang chon (hover neu co, khong thi mac dinh), tat cac muc con lai.
        private void Apply()
        {
            int cur = (_hover >= 0) ? _hover : (_selected >= 0 ? _selected : _defaultIndex);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].highlight != null)
                    _entries[i].highlight.SetActive(i == cur);
            }
        }

        // Them 1 muc su kien vao EventTrigger.
        private void AddTrigger(EventTrigger trig, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(cb);
            trig.triggers.Add(entry);
        }
    }
}
