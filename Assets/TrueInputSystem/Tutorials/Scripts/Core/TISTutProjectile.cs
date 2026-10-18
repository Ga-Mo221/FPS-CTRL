// ============================================================
//  TrueInputSystem Tutorials — TISTutProjectile.cs
//  Viên đạn / quả bóng đơn giản: bay theo quỹ đạo, nảy trên mặt đất, tự biến mất.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Projectile")]
    public class TISTutProjectile : MonoBehaviour
    {
        public float gravity  = -18f;
        public float lifetime = 3f;
        public float bounce   = 0.45f;

        Vector3 _vel;
        float   _age;
        float   _radius;

        public void Launch(Vector3 velocity)
        {
            _vel = velocity;
            _age = 0f;
            _radius = transform.localScale.y * 0.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            _vel.y += gravity * dt;
            Vector3 p = transform.position + _vel * dt;
            if (p.y < _radius)
            {
                p.y = _radius;
                _vel.y = -_vel.y * bounce;
                _vel.x *= 0.8f; _vel.z *= 0.8f;
            }
            transform.position = p;

            if (_age > lifetime - 0.3f)
                transform.localScale = Vector3.one * (_radius * 2f) * Mathf.Clamp01((lifetime - _age) / 0.3f);
            if (_age >= lifetime) Destroy(gameObject);
        }
    }
}
