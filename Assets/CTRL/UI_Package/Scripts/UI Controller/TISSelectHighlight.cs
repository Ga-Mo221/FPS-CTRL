using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-04] Vien teal (focus ring) quanh nut DANG DUOC CHON (controller/arrow di toi).
    /// Gon, ro, khong che noi dung, khong doi mau 'active' san co. Hop moi loai nut.
    /// </summary>
    [DisallowMultipleComponent]
    public class TISSelectHighlight : MonoBehaviour
    {
        [SerializeField] private Color _color = new Color(0.31f, 0.86f, 0.80f, 1f); // teal sang
        [SerializeField] private float _thickness = 3f;   // do day vien (px)
        [SerializeField] private float _pad = 2f;         // vien nam ngoai nut bao nhieu (px)

        private GameObject _ring;
        private bool _wasSel;

        private void EnsureRing()
        {
            if (_ring != null) return;
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            _ring = new GameObject("__NavRing", typeof(RectTransform));
            var crt = _ring.GetComponent<RectTransform>();
            crt.SetParent(rt, false);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(-_pad, -_pad);
            crt.offsetMax = new Vector2(_pad, _pad);
            crt.SetAsLastSibling();

            // 4 canh
            MakeEdge(crt, new Vector2(0,1), new Vector2(1,1), new Vector2(0,-_thickness), new Vector2(0,0)); // top
            MakeEdge(crt, new Vector2(0,0), new Vector2(1,0), new Vector2(0,0), new Vector2(0,_thickness));   // bottom
            MakeEdge(crt, new Vector2(0,0), new Vector2(0,1), new Vector2(0,0), new Vector2(_thickness,0));   // left
            MakeEdge(crt, new Vector2(1,0), new Vector2(1,1), new Vector2(-_thickness,0), new Vector2(0,0));  // right

            _ring.SetActive(false);
        }

        private void MakeEdge(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var e = new GameObject("edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var ert = e.GetComponent<RectTransform>();
            ert.SetParent(parent, false);
            ert.anchorMin = aMin; ert.anchorMax = aMax;
            ert.offsetMin = offMin; ert.offsetMax = offMax;
            var img = e.GetComponent<Image>();
            img.color = _color; img.raycastTarget = false;
        }

        private void Awake()  { EnsureRing(); }
        private void OnDisable() { if (_ring != null) _ring.SetActive(false); _wasSel = false; }

        private void Update()
        {
            EnsureRing();
            if (_ring == null) return;
            bool sel = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (sel != _wasSel) { _ring.SetActive(sel); if (sel) _ring.transform.SetAsLastSibling(); _wasSel = sel; }
        }
    }
}
