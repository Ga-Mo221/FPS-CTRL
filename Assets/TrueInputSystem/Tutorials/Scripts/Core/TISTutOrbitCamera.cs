// ============================================================
//  TrueInputSystem Tutorials — TISTutOrbitCamera.cs
//
//  Camera góc 3 xoay quanh nhân vật:
//    LookDelta  → xoay (yaw / pitch), giới hạn pitch theo profile cameraSettings
//    ZoomDelta  → khoảng cách
//  Chuột: chỉ xoay khi giữ chuột phải (để vẫn bấm được UI hướng dẫn) — hoặc khi
//  con trỏ đang bị khoá. Tay cầm / cảm ứng: xoay luôn.
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Orbit Camera")]
    public class TISTutOrbitCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Khoảng cách")]
        public float distance    = 9f;
        public float minDistance = 3f;
        public float maxDistance = 16f;
        public float height      = 1.2f;

        [Header("Góc")]
        public float yaw   = 0f;
        public float pitch = 24f;

        [Header("Tốc độ")]
        [Tooltip("Độ / đơn vị LookDelta khi dùng chuột.")]
        public float mouseDegreesPerUnit = 10f;
        [Tooltip("Độ / giây khi LookDelta = 1 từ tay cầm / cảm ứng.")]
        public float stickDegreesPerSecond = 720f;
        [Tooltip("Độ / đơn vị LookDelta khi vuốt màn hình cảm ứng.")]
        public float touchDegreesPerUnit = 1.6f;
        public float zoomStep = 1.2f;
        public float follow = 10f;

        [Header("Bài học bật / tắt")]
        public bool LookEnabled;
        public bool ZoomEnabled;
        public bool RequireRightMouse = true;

        /// <summary>Lệch camera qua vai (x = phải, y = lên) — dùng cho bài ngắm bắn.</summary>
        public Vector2 shoulder;

        /// <summary>
        /// Lọc độ xoay mỗi frame (độ; x = yaw, y = pitch dương = cúi). Bài Aim Assist gắn
        /// TISInputBridge.AimAssistProcess vào đây.
        /// </summary>
        public System.Func<Vector2, Vector2> LookFilter;

        /// <summary>Tổng góc yaw đã xoay (trị tuyệt đối) — dùng cho nhiệm vụ.</summary>
        public float TotalYaw   { get; private set; }
        public float MinPitch   { get; private set; } = 90f;
        public float MaxPitch   { get; private set; } = -90f;
        public bool  IsRotating { get; private set; }

        Vector3 _focus;
        float   _dist;

        void Start()
        {
            _dist = distance;
            if (target != null) _focus = target.position;
            Snap();
        }

        void LateUpdate()
        {
            var tis = TISInputBridge.Instance;
            float dt = Time.deltaTime;

            IsRotating = false;
            if (tis != null && LookEnabled)
            {
                Vector2 look = tis.LookDelta;
                bool mouse = tis.IsKeyboardMouse;
                bool allow = !mouse || !RequireRightMouse || Cursor.lockState == CursorLockMode.Locked
                             || (TISPhysicalMouse.Current?.rightButton.isPressed ?? false);
                if (allow && (look.sqrMagnitude > 0f || LookFilter != null))
                {
                    // Chuột / vuốt cảm ứng: độ dịch mỗi frame · cần analog: tốc độ (× deltaTime)
                    bool touch = tis.ActiveDevice == TISActiveDevice.Mobile;
                    float k = mouse ? mouseDegreesPerUnit : touch ? touchDegreesPerUnit : stickDegreesPerSecond * dt;
                    var cs = tis.TISCameraSettings;
                    float inv = cs != null && cs.invertY ? -1f : 1f;

                    // Độ xoay frame này: x = yaw, y = pitch (dương = cúi xuống)
                    Vector2 deg = new Vector2(look.x * k, -look.y * k * inv);
                    if (LookFilter != null) deg = LookFilter(deg);

                    yaw += deg.x;
                    TotalYaw += Mathf.Abs(deg.x);
                    float pMin = cs != null ? -cs.pitchMax : -80f;
                    float pMax = cs != null ? -cs.pitchMin : 80f;
                    pitch = Mathf.Clamp(pitch + deg.y, Mathf.Max(pMin, -35f), Mathf.Min(pMax, 75f));
                    IsRotating = deg.sqrMagnitude > 0f;
                }
            }

            if (tis != null && ZoomEnabled)
            {
                float z = tis.ZoomDelta;
                if (Mathf.Abs(z) > 0.001f)
                {
                    float notches = Mathf.Abs(z) > 10f ? z / 120f : z;   // Windows thô = ±120 / nấc
                    distance = Mathf.Clamp(distance - notches * zoomStep, minDistance, maxDistance);
                }
            }

            MinPitch = Mathf.Min(MinPitch, pitch);
            MaxPitch = Mathf.Max(MaxPitch, pitch);

            if (target != null)
                _focus = Vector3.Lerp(_focus, target.position, 1f - Mathf.Exp(-follow * dt));
            _dist = Mathf.Lerp(_dist, distance, 1f - Mathf.Exp(-12f * dt));
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = _focus + Vector3.up * (height + shoulder.y) + rot * Vector3.right * shoulder.x;
            transform.SetPositionAndRotation(pivot - rot * Vector3.forward * _dist, rot);
        }

        /// <summary>Đặt lại góc nhìn mặc định ngay lập tức.</summary>
        public void ResetView(float newYaw = 0f, float newPitch = 24f, float newDistance = 9f)
        {
            yaw = newYaw; pitch = newPitch; distance = newDistance;
            TotalYaw = 0f; MinPitch = 90f; MaxPitch = -90f;
            LookEnabled = ZoomEnabled = false;
            LookFilter = null;
            shoulder = Vector2.zero;
            RequireRightMouse = true;
            Snap();
        }

        void Snap()
        {
            if (target != null) _focus = target.position;
            _dist = distance;
            Apply();
        }
    }
}
