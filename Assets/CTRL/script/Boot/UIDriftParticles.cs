// [Aemy 2026-10-26] FILE MOI.
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hat bui / tan lua troi len cham trong 1 vung UI (khong can Particle System, 1 draw call).
    /// Moi hat: hien dan -> tat dan theo vong doi, lac ngang nhe.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIDriftParticles : MonoBehaviour
    {
        [SerializeField] private int _count = 48;
        [SerializeField] private Vector2 _sizeRange = new Vector2(2f, 5f);
        [SerializeField] private Vector2 _speedRange = new Vector2(12f, 40f);
        [SerializeField] private Vector2 _lifeRange = new Vector2(5f, 11f);
        [SerializeField] private float _sway = 18f;
        [SerializeField] private Color[] _colors =
        {
            new Color(0.31f, 0.84f, 0.79f, 0.85f),
            new Color(1f, 1f, 1f, 0.6f),
            new Color(1f, 0.72f, 0.42f, 0.75f),
        };

        private struct Particle
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos;
            public float Speed, Age, Life, Phase, BaseAlpha;
        }

        private Particle[] _ps;
        private RectTransform _area;

        private void Awake()
        {
            _area = (RectTransform)transform;
            _ps = new Particle[Mathf.Max(0, _count)];
            for (int i = 0; i < _ps.Length; i++)
            {
                var go = new GameObject("p", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                ref var p = ref _ps[i];
                p.Rt = (RectTransform)go.transform;
                p.Img = go.GetComponent<Image>();
                p.Img.raycastTarget = false;
                Respawn(ref p, true);
            }
        }

        private void Respawn(ref Particle p, bool anywhere)
        {
            var r = _area.rect;
            p.Pos = new Vector2(Random.Range(r.xMin, r.xMax), anywhere ? Random.Range(r.yMin, r.yMax) : r.yMin - 10f);
            p.Speed = Random.Range(_speedRange.x, _speedRange.y);
            p.Life = Random.Range(_lifeRange.x, _lifeRange.y);
            p.Age = anywhere ? Random.Range(0f, p.Life) : 0f;
            p.Phase = Random.value * 6.2832f;
            float s = Random.Range(_sizeRange.x, _sizeRange.y);
            p.Rt.sizeDelta = new Vector2(s, s);
            var c = _colors != null && _colors.Length > 0 ? _colors[Random.Range(0, _colors.Length)] : Color.white;
            p.BaseAlpha = c.a;
            c.a = 0f;
            p.Img.color = c;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _ps.Length; i++)
            {
                ref var p = ref _ps[i];
                p.Age += dt;
                if (p.Age >= p.Life) Respawn(ref p, false);
                p.Pos.y += p.Speed * dt;
                p.Rt.anchoredPosition = new Vector2(p.Pos.x + Mathf.Sin(p.Age * 0.9f + p.Phase) * _sway, p.Pos.y);
                var c = p.Img.color;
                c.a = p.BaseAlpha * Mathf.Sin(Mathf.Clamp01(p.Age / p.Life) * Mathf.PI);
                p.Img.color = c;
            }
        }
    }
}
