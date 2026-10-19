using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Vien teal (focus ring) quanh nut DANG DUOC CHON (controller/arrow di toi).
    /// Gon, ro, khong che noi dung, khong doi mau 'active' san co. Hop moi loai nut.
    /// - Nut nho (dau "+", chu 1 dong) -> vien toi thieu _minSize de nhin thay ro.
    ///                   - Configure(...) cho nut tao luc chay (popup), CreateRing(...) dung chung cho menu tu ve.
    /// </summary>
    [DisallowMultipleComponent]
    public class TISSelectHighlight : MonoBehaviour
    {
        [SerializeField] private Color _color = new Color(0.31f, 0.86f, 0.80f, 1f); // teal sang
        [SerializeField] private float _thickness = 3f;   // do day vien (px)
        [SerializeField] private float _pad = 2f;         // vien nam ngoai nut bao nhieu (px)
        [Tooltip("Kich thuoc vien toi thieu (px). Nut nho hon -> vien noi rong ra quanh tam nut.")]
        [SerializeField] private Vector2 _minSize = new Vector2(44f, 40f);

        private GameObject _ring;
        private bool _wasSel;

        /// <summary>Doi mau / do day / khoang cach vien (vd nut popup mau teal -> vien trang). Dung lai vien.</summary>
        public void Configure(Color color, float thickness, float pad)
        {
            _color = color; _thickness = thickness; _pad = pad;
            if (_ring != null) { Destroy(_ring); _ring = null; _wasSel = false; }
            EnsureRing();
        }

        /// <summary>Tao vien 4 canh (tat san) lam con cua host. Dung chung cho menu khong qua EventSystem (UIContextMenu).</summary>
        public static GameObject CreateRing(RectTransform host, Color color, float thickness, float pad)
        {
            var ring = new GameObject("__NavRing", typeof(RectTransform));
            var crt = ring.GetComponent<RectTransform>();
            crt.SetParent(host, false);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(-pad, -pad);
            crt.offsetMax = new Vector2(pad, pad);
            crt.SetAsLastSibling();
            MakeEdge(crt, color, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), new Vector2(0, 0)); // top
            MakeEdge(crt, color, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, thickness));  // bottom
            MakeEdge(crt, color, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(thickness, 0));  // left
            MakeEdge(crt, color, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), new Vector2(0, 0)); // right
            ring.SetActive(false);
            return ring;
        }

        private void EnsureRing()
        {
            if (_ring != null) return;
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;
            _ring = CreateRing(rt, _color, _thickness, _pad);
        }

        private static void MakeEdge(RectTransform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var e = new GameObject("edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var ert = e.GetComponent<RectTransform>();
            ert.SetParent(parent, false);
            ert.anchorMin = aMin; ert.anchorMax = aMax;
            ert.offsetMin = offMin; ert.offsetMax = offMax;
            var img = e.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
        }

        // Nut nho hon _minSize -> noi vien ra deu 2 ben (tinh luc hien, vi layout co the doi kich thuoc nut).
        private void FitRing()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null || _ring == null) return;
            Vector2 size = rt.rect.size;
            // Chi noi cho nut nho kieu icon / chu ngan (rong < 2 x min). Thanh truot dai (520 x 6) giu vien sat nhu cu.
            if (size.x >= _minSize.x * 2f) { var c0 = (RectTransform)_ring.transform; c0.offsetMin = new Vector2(-_pad, -_pad); c0.offsetMax = new Vector2(_pad, _pad); return; }
            float px = _pad + Mathf.Max(0f, (_minSize.x - size.x) * 0.5f);
            float py = _pad + Mathf.Max(0f, (_minSize.y - size.y) * 0.5f);
            var crt = (RectTransform)_ring.transform;
            crt.offsetMin = new Vector2(-px, -py);
            crt.offsetMax = new Vector2(px, py);
        }

        private void Awake()  { EnsureRing(); }
        private void OnDisable() { if (_ring != null) _ring.SetActive(false); _wasSel = false; }

        private void Update()
        {
            EnsureRing();
            if (_ring == null) return;
            bool sel = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            if (sel != _wasSel)
            {
                if (sel) { FitRing(); _ring.transform.SetAsLastSibling(); }
                _ring.SetActive(sel);
                _wasSel = sel;
            }
        }
    }
}
