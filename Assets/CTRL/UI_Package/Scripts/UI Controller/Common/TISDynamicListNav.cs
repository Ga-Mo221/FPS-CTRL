using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-04] Tu dong noi navigation (TIS) cho 1 danh sach SINH LUC CHAY
    /// (friend list, shop item grid...). Gan len container chua cac item (vd ItemGrid, Content).
    /// - Khi so luong / thu tu con thay doi (OnTransformChildrenChanged) hoac khi bat len,
    ///   tu quet tat ca TISGamepadUIButton con dang active va noi navUp/Down/Left/Right theo
    ///   VI TRI THAT tren man hinh (luc runtime layout da tinh xong, nen dung world position).
    /// - Khong can sua code controller.
    /// - Item template phai co san TISGamepadUIButton (+ TISSelectHighlight) de clone ke thua.
    /// </summary>
    [DisallowMultipleComponent]
    public class TISDynamicListNav : MonoBehaviour
    {
        [Tooltip("Context de gan cho cac item. De trong = lay TISUINavigationContext o cha.")]
        [SerializeField] private TISUINavigationContext _context;

        [Tooltip("Nut TIS nam NGOAI list (vd tab phia tren / o tim kiem). " +
                 "Item o mep tren se navUp ve day; nut nay se navDown vao item dau tien.")]
        [SerializeField] private TISGamepadUIButton _enterFrom;

        [Tooltip("Cac nut enter PHU (vd nhieu tab). Tu tab nao cung xuong duoc list.")]
        [SerializeField] private TISGamepadUIButton[] _enterFromExtra;

        [Tooltip("Bo qua cac con ten nay khi quet (vd template an).")]
        [SerializeField] private string _skipNameContains = "";

        private bool _dirty;
        private int _lastCount = -1;

        private void Awake()
        {
            if (_context == null) _context = GetComponentInParent<TISUINavigationContext>();
        }

        private void OnEnable() { _dirty = true; }
        private void OnTransformChildrenChanged() { _dirty = true; }

        private void LateUpdate()
        {
            // rebuild khi dirty, hoac khi so luong button active doi (phong truong hop chi doi active)
            int c = CountActive();
            if (_dirty || c != _lastCount)
            {
                _dirty = false;
                _lastCount = c;
                Rebuild();
            }
        }

        private int CountActive()
        {
            int n = 0;
            var all = GetComponentsInChildren<TISGamepadUIButton>(false);
            for (int i = 0; i < all.Length; i++)
                if (all[i].isActiveAndEnabled) n++;
            return n;
        }

        public void Rebuild()
        {
            var btns = new List<TISGamepadUIButton>();
            var all = GetComponentsInChildren<TISGamepadUIButton>(false);
            foreach (var b in all)
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                if (!string.IsNullOrEmpty(_skipNameContains) && b.name.Contains(_skipNameContains)) continue;
                btns.Add(b);
            }
            if (btns.Count == 0) return;

            // vi tri world (luc runtime hop le)
            var pos = new Dictionary<TISGamepadUIButton, Vector3>(btns.Count);
            foreach (var b in btns) pos[b] = b.transform.position;

            foreach (var b in btns)
            {
                if (_context != null) b.SetContext(_context);
                Vector3 bp = pos[b];
                TISGamepadUIButton up = null, down = null, left = null, right = null;
                float su = float.MaxValue, sd = float.MaxValue, sl = float.MaxValue, sr = float.MaxValue;
                foreach (var o in btns)
                {
                    if (o == b) continue;
                    Vector3 d = pos[o] - bp;
                    if (d.y > 0.5f)  { float s = Score(d, Vector2.up);    if (s < su) { su = s; up = o; } }
                    if (d.y < -0.5f) { float s = Score(d, Vector2.down);  if (s < sd) { sd = s; down = o; } }
                    if (d.x < -0.5f) { float s = Score(d, Vector2.left);  if (s < sl) { sl = s; left = o; } }
                    if (d.x > 0.5f)  { float s = Score(d, Vector2.right); if (s < sr) { sr = s; right = o; } }
                }
                b.navUp = up; b.navDown = down; b.navLeft = left; b.navRight = right;
            }

            // bridge mep tren voi cac nut ngoai list (enterFrom + enterFromExtra)
            TISGamepadUIButton firstItem = null; float bestY = float.NegativeInfinity, bestX = 0f;
            foreach (var b in btns)
            {
                Vector3 p = pos[b];
                if (firstItem == null || p.y > bestY + 1f || (Mathf.Abs(p.y - bestY) <= 1f && p.x < bestX))
                { firstItem = b; bestY = p.y; bestX = p.x; }
            }
            var enters = new List<TISGamepadUIButton>();
            if (_enterFrom != null) enters.Add(_enterFrom);
            if (_enterFromExtra != null) foreach (var e in _enterFromExtra) if (e != null) enters.Add(e);
            TISGamepadUIButton primaryEnter = null;
            foreach (var e in enters) if (e.isActiveAndEnabled) { primaryEnter = e; break; }
            if (primaryEnter == null && enters.Count > 0) primaryEnter = enters[0];
            foreach (var b in btns) if (b.navUp == null) b.navUp = primaryEnter;
            if (firstItem != null) foreach (var e in enters) e.navDown = firstItem;
        }

        private static float Score(Vector3 delta, Vector2 dir)
        {
            Vector2 d2 = new Vector2(delta.x, delta.y);
            float dist = d2.magnitude;
            if (dist < 0.0001f) return float.MaxValue;
            float along = Vector2.Dot(d2 / dist, dir);
            if (along <= 0.2f) return float.MaxValue;
            float perp = Mathf.Abs(Vector2.Dot(d2, new Vector2(-dir.y, dir.x)));
            return dist + perp * 2f + (1f - along) * dist;
        }
    }
}
