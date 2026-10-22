// ============================================================
//  TrueInputSystem Tutorials — TISTutShape.cs
//
//  Graphic vẽ khối bo góc với cạnh trái / phải có thể nghiêng
//  (kiểu nút "DISCOVER OUR PLATFORM →"). Không cần sprite.
//
//  slantLeft  > 0 : đỉnh TRÊN-trái lùi vào trong  (cạnh trái nghiêng "/")
//  slantLeft  < 0 : đỉnh DƯỚI-trái lùi vào trong  (cạnh trái nghiêng "\")
//  slantRight > 0 : đỉnh DƯỚI-phải lùi vào trong  (cạnh phải nghiêng "/")
//  slantRight < 0 : đỉnh TRÊN-phải lùi vào trong  (cạnh phải nghiêng "\")
//
//  borderWidth > 0 → vẽ viền màu borderColor, ruột màu color.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GaMo.TrueInputSystem.Tutorials
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Shape")]
    public class TISTutShape : MaskableGraphic
    {
        [SerializeField, Min(0f)] float _radius = 16f;
        [SerializeField] float _slantLeft;
        [SerializeField] float _slantRight;
        [SerializeField, Min(0f)] float _borderWidth;
        [SerializeField] Color _borderColor = Color.black;
        [SerializeField, Range(2, 16)] int _cornerSegments = 6;

        public float Radius      { get => _radius;      set { _radius = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public float SlantLeft   { get => _slantLeft;   set { _slantLeft = value;  SetVerticesDirty(); } }
        public float SlantRight  { get => _slantRight;  set { _slantRight = value; SetVerticesDirty(); } }
        public float BorderWidth { get => _borderWidth; set { _borderWidth = Mathf.Max(0f, value); SetVerticesDirty(); } }
        public Color BorderColor { get => _borderColor; set { _borderColor = value; SetVerticesDirty(); } }

        static readonly List<Vector2> s_Poly = new List<Vector2>(80);
        static readonly Vector2[] s_Quad = new Vector2[4];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0.5f || r.height <= 0.5f) return;

            if (_borderWidth > 0.01f)
            {
                Color bc = _borderColor;
                bc.a *= color.a;
                AddShape(vh, r, _slantLeft, _slantRight, _radius, bc);

                float b = Mathf.Min(_borderWidth, Mathf.Min(r.width, r.height) * 0.5f - 0.5f);
                Rect inner = new Rect(r.x + b, r.y + b, r.width - 2f * b, r.height - 2f * b);
                float k = inner.height / r.height;          // giữ nguyên góc nghiêng
                AddShape(vh, inner, _slantLeft * k, _slantRight * k, Mathf.Max(0f, _radius - b), color);
            }
            else
            {
                AddShape(vh, r, _slantLeft, _slantRight, _radius, color);
            }
        }

        void AddShape(VertexHelper vh, Rect r, float sl, float sr, float radius, Color32 c)
        {
            float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;

            // Tứ giác ngược chiều kim đồng hồ: BL, BR, TR, TL
            s_Quad[0] = new Vector2(x0 + Mathf.Max(0f, -sl), y0);
            s_Quad[1] = new Vector2(x1 - Mathf.Max(0f,  sr), y0);
            s_Quad[2] = new Vector2(x1 - Mathf.Max(0f, -sr), y1);
            s_Quad[3] = new Vector2(x0 + Mathf.Max(0f,  sl), y1);

            BuildRounded(s_Quad, Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f), _cornerSegments, s_Poly);

            int start = vh.currentVertCount;
            Vector2 center = r.center;
            vh.AddVert(center, c, Vector4.zero);
            for (int i = 0; i < s_Poly.Count; i++)
                vh.AddVert(s_Poly[i], c, Vector4.zero);

            int n = s_Poly.Count;
            for (int i = 0; i < n; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % n);
        }

        /// <summary>Bo tròn mọi đỉnh của đa giác lồi (CCW) bằng cung tiếp tuyến bán kính r.</summary>
        internal static void BuildRounded(Vector2[] quad, float r, int segments, List<Vector2> outPts)
        {
            outPts.Clear();
            int n = quad.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = quad[i];
                Vector2 a = quad[(i + n - 1) % n];
                Vector2 b = quad[(i + 1) % n];

                if (r <= 0.01f) { outPts.Add(p); continue; }

                Vector2 u = a - p, v = b - p;
                float lu = u.magnitude, lv = v.magnitude;
                if (lu < 1e-3f || lv < 1e-3f) { outPts.Add(p); continue; }
                u /= lu; v /= lv;

                float half = Mathf.Acos(Mathf.Clamp(Vector2.Dot(u, v), -1f, 1f)) * 0.5f;
                if (half < 1e-3f) { outPts.Add(p); continue; }

                float d = r / Mathf.Tan(half);
                float dMax = Mathf.Min(lu, lv) * 0.5f;
                float rr = r;
                if (d > dMax) { d = dMax; rr = d * Mathf.Tan(half); }

                Vector2 bis = (u + v).normalized;
                Vector2 c = p + bis * (rr / Mathf.Sin(half));
                Vector2 s = p + u * d;
                Vector2 e = p + v * d;

                float a0 = Mathf.Atan2(s.y - c.y, s.x - c.x);
                float a1 = Mathf.Atan2(e.y - c.y, e.x - c.x);
                while (a1 < a0) a1 += Mathf.PI * 2f;          // CCW

                for (int k = 0; k <= segments; k++)
                {
                    float t = Mathf.Lerp(a0, a1, k / (float)segments);
                    outPts.Add(c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rr);
                }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
