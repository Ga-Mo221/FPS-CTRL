// ============================================================
//  TrueInputSystem Tutorials — TISTutHoleFrame.cs
//
//  Nền phủ toàn màn hình, chừa một "cửa sổ" bo góc khớp với RectTransform
//  `hole` — camera 3D (TISTutViewportFitter) vẽ đúng vào cửa sổ đó, nên
//  sân chơi trông như một tấm card bo góc giữa giao diện.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Hole Frame")]
    public class TISTutHoleFrame : MaskableGraphic
    {
        [SerializeField] RectTransform _hole;
        [SerializeField, Min(0f)] float _radius = 18f;
        [SerializeField, Range(2, 16)] int _cornerSegments = 8;

        public RectTransform Hole { get => _hole; set { _hole = value; SetVerticesDirty(); } }

        Rect _lastHole;
        static readonly Vector3[] s_Corners = new Vector3[4];

        // Cửa sổ có thể đổi khi đổi độ phân giải → kiểm tra mỗi frame (rẻ)
        void LateUpdate()
        {
            if (_hole == null) return;
            Rect h = HoleLocalRect();
            if (h != _lastHole) SetVerticesDirty();
        }

        Rect HoleLocalRect()
        {
            _hole.GetWorldCorners(s_Corners);
            Vector2 a = rectTransform.InverseTransformPoint(s_Corners[0]);
            Vector2 b = rectTransform.InverseTransformPoint(s_Corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect o = GetPixelAdjustedRect();
            if (_hole == null) { Quad(vh, o.xMin, o.yMin, o.xMax, o.yMax); return; }

            Rect h = HoleLocalRect();
            _lastHole = h;
            float hx0 = Mathf.Clamp(h.xMin, o.xMin, o.xMax), hx1 = Mathf.Clamp(h.xMax, o.xMin, o.xMax);
            float hy0 = Mathf.Clamp(h.yMin, o.yMin, o.yMax), hy1 = Mathf.Clamp(h.yMax, o.yMin, o.yMax);
            float r = Mathf.Min(_radius, (hx1 - hx0) * 0.5f, (hy1 - hy0) * 0.5f);

            // 4 dải quanh cửa sổ
            Quad(vh, o.xMin, o.yMin, o.xMax, hy0);   // dưới
            Quad(vh, o.xMin, hy1, o.xMax, o.yMax);   // trên
            Quad(vh, o.xMin, hy0, hx0, hy1);         // trái
            Quad(vh, hx1, hy0, o.xMax, hy1);         // phải

            // 4 góc bo: quạt từ đỉnh góc tới cung tròn
            if (r > 0.5f)
            {
                Corner(vh, new Vector2(hx0, hy0), new Vector2(hx0 + r, hy0 + r), r, 180f);
                Corner(vh, new Vector2(hx1, hy0), new Vector2(hx1 - r, hy0 + r), r, 270f);
                Corner(vh, new Vector2(hx1, hy1), new Vector2(hx1 - r, hy1 - r), r, 0f);
                Corner(vh, new Vector2(hx0, hy1), new Vector2(hx0 + r, hy1 - r), r, 90f);
            }
        }

        void Quad(VertexHelper vh, float x0, float y0, float x1, float y1)
        {
            if (x1 - x0 <= 0f || y1 - y0 <= 0f) return;
            int i = vh.currentVertCount;
            Color32 c = color;
            vh.AddVert(new Vector2(x0, y0), c, Vector4.zero);
            vh.AddVert(new Vector2(x1, y0), c, Vector4.zero);
            vh.AddVert(new Vector2(x1, y1), c, Vector4.zero);
            vh.AddVert(new Vector2(x0, y1), c, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        void Corner(VertexHelper vh, Vector2 tip, Vector2 center, float r, float startDeg)
        {
            int i = vh.currentVertCount;
            Color32 c = color;
            vh.AddVert(tip, c, Vector4.zero);
            for (int k = 0; k <= _cornerSegments; k++)
            {
                float t = (startDeg + 90f * k / _cornerSegments) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r, c, Vector4.zero);
            }
            for (int k = 0; k < _cornerSegments; k++)
                vh.AddTriangle(i, i + 1 + k, i + 2 + k);
        }

        // Cửa sổ trong suốt với raycast → click xuyên xuống sân chơi 3D
        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            if (_hole != null && RectTransformUtility.RectangleContainsScreenPoint(_hole, sp, eventCamera))
                return false;
            return base.Raycast(sp, eventCamera);
        }
    }
}
