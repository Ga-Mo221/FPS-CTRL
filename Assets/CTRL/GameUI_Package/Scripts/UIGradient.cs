using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Tints a UI Graphic (Image/Text) with a two-colour gradient by modifying
    /// vertex colours — no extra texture needed. Put it on the background Image
    /// and set Top = accent colour (e.g. cyan for Win, red for Lose), Bottom = base.
    /// The gradient colour multiplies the existing graphic colour/sprite.
    /// </summary>
    [AddComponentMenu("UI/Effects/UI Gradient")]
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        public enum Direction { Vertical, Horizontal }

        [SerializeField] private Color _top = Color.white;
        [SerializeField] private Color _bottom = Color.white;
        [SerializeField] private Direction _direction = Direction.Vertical;

        public Color Top { get { return _top; } set { _top = value; Refresh(); } }
        public Color Bottom { get { return _bottom; } set { _bottom = value; Refresh(); } }

        private static readonly List<UIVertex> _verts = new List<UIVertex>();

        public void SetColors(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            Refresh();
        }

        private void Refresh()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            vh.GetUIVertexStream(_verts);

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < _verts.Count; i++)
            {
                float p = _direction == Direction.Vertical ? _verts[i].position.y : _verts[i].position.x;
                if (p < min) min = p;
                if (p > max) max = p;
            }

            float range = Mathf.Max(0.0001f, max - min);
            for (int i = 0; i < _verts.Count; i++)
            {
                var vtx = _verts[i];
                float p = _direction == Direction.Vertical ? vtx.position.y : vtx.position.x;
                float t = (p - min) / range; // 0 = bottom/left, 1 = top/right
                vtx.color = vtx.color * Color.Lerp(_bottom, _top, t);
                _verts[i] = vtx;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
