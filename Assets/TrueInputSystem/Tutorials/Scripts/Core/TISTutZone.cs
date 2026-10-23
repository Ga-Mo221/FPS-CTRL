// ============================================================
//  TrueInputSystem Tutorials — TISTutZone.cs
//  Vòng đích trên mặt đất: nhấp nháy nhẹ, sáng khi nhân vật đứng trong.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Zone")]
    public class TISTutZone : MonoBehaviour
    {
        public float radius = 1.2f;

        public bool IsInside { get; private set; }
        public bool Reached  { get; private set; }

        Vector3 _baseScale;
        float   _t;

        void Awake() => _baseScale = transform.localScale;

        public bool Contains(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Update()
        {
            var pg = TISTutPlayground.Instance;
            IsInside = pg != null && pg.Player != null && Contains(pg.Player.transform.position);
            if (IsInside) Reached = true;

            _t += Time.deltaTime * (IsInside ? 6f : 2.5f);
            float s = 1f + Mathf.Sin(_t) * (IsInside ? 0.08f : 0.04f);
            transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y, _baseScale.z * s);
        }
    }
}
