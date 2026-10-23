// ============================================================
//  TrueInputSystem Tutorials — TISTutViewportFitter.cs
//
//  Đặt Camera.rect khớp với RectTransform "sân chơi" trên Canvas Overlay,
//  để camera 3D chỉ vẽ vào cửa sổ giữa giao diện. Toạ độ chuột / chạm
//  vẫn là toạ độ màn hình thật → ScreenPointToRay, ObjectTracker... chạy đúng.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Viewport Fitter")]
    public class TISTutViewportFitter : MonoBehaviour
    {
        [SerializeField] RectTransform _target;
        Camera _cam;
        static readonly Vector3[] s_Corners = new Vector3[4];

        public RectTransform Target { get => _target; set => _target = value; }

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (_target == null || Screen.width <= 0 || Screen.height <= 0) return;

            // Canvas Screen Space Overlay: world corners = pixel màn hình
            _target.GetWorldCorners(s_Corners);
            float x0 = s_Corners[0].x / Screen.width,  y0 = s_Corners[0].y / Screen.height;
            float x1 = s_Corners[2].x / Screen.width,  y1 = s_Corners[2].y / Screen.height;
            var r = Rect.MinMaxRect(Mathf.Clamp01(x0), Mathf.Clamp01(y0), Mathf.Clamp01(x1), Mathf.Clamp01(y1));
            if (_cam.rect != r) _cam.rect = r;
        }
    }
}
