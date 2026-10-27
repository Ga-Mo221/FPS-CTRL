// [Aemy 2026-10-26] FILE MOI.
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Xoay cham + nhap nho alpha cho 1 Graphic (vd vanh nhat thuc). Thoi gian khong phu thuoc timeScale.</summary>
    [RequireComponent(typeof(Graphic))]
    public class UISpinPulse : MonoBehaviour
    {
        [SerializeField] private float _spinDegPerSec = -4f;
        [SerializeField] private float _pulsePerSec = 0.35f;
        [SerializeField] private Vector2 _alphaRange = new Vector2(0.6f, 1f);
        [SerializeField] private float _scalePulse = 0.015f;

        private Graphic _g;
        private Color _base;

        private void Awake()
        {
            _g = GetComponent<Graphic>();
            _base = _g.color;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            transform.localRotation = Quaternion.Euler(0f, 0f, t * _spinDegPerSec);
            float k = 0.5f + 0.5f * Mathf.Sin(t * _pulsePerSec * 2f * Mathf.PI);
            var c = _base;
            c.a = _base.a * Mathf.Lerp(_alphaRange.x, _alphaRange.y, k);
            _g.color = c;
            transform.localScale = Vector3.one * (1f + _scalePulse * k);
        }
    }
}
