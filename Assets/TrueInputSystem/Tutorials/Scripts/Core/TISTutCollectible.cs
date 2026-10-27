// ============================================================
//  TrueInputSystem Tutorials — TISTutCollectible.cs
//  Vật thu thập (ngôi sao): xoay, nhấp nhô, biến mất khi nhân vật chạm.
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Collectible")]
    public class TISTutCollectible : MonoBehaviour
    {
        public float pickRadius = 1.1f;
        public event Action<TISTutCollectible> OnCollected;

        public bool Collected { get; private set; }

        Vector3 _base;
        float   _phase;
        float   _pop;

        void Start()
        {
            _base = transform.position;
            _phase = UnityEngine.Random.value * 6f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Collected)
            {
                _pop += dt * 5f;
                transform.localScale = Vector3.one * 0.6f * Mathf.Max(0f, 1f + _pop - _pop * _pop * 2f);
                if (_pop >= 1f) gameObject.SetActive(false);
                return;
            }

            _phase += dt;
            transform.position = _base + Vector3.up * (Mathf.Sin(_phase * 2.4f) * 0.18f);
            transform.Rotate(0f, 120f * dt, 0f, Space.World);

            var pg = TISTutPlayground.Instance;
            if (pg == null || pg.Player == null) return;
            Vector3 p = pg.Player.transform.position + Vector3.up * 0.4f;
            if ((p - transform.position).sqrMagnitude <= pickRadius * pickRadius)
            {
                Collected = true;
                OnCollected?.Invoke(this);
            }
        }
    }
}
