// ============================================================
//  TrueInputSystem — TISTargetScanner.cs
//
//  Gắn vào Camera.
//  Tự động tìm target gần tâm màn hình nhất trong phạm vi
//  góc (snapRadius) rồi trả về cho TISInputBridge.AimAssistProcess.
//
//  Cách dùng:
//    1. Gắn component này lên Camera.
//    2. Điền tags hoặc layers cần quét vào Inspector.
//    3. Trong camera-controller của bạn, thay vì gọi:
//         lookDelta = bridge.AimAssistProcess(cam, manualTarget, lookDelta, intensity);
//       Hãy gọi:
//         lookDelta = scanner.Process(lookDelta);
//       hoặc lấy target thủ công:
//         Transform t = scanner.CurrentTarget;
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Target Scanner")]
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class TISTargetScanner : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════

        [Header("Target Filter")]
        [Tooltip("Danh sách Tag cần tìm. Để trống nếu dùng Layer.")]
        [SerializeField] private List<string> _targetTags = new List<string> { "Player" };

        [Tooltip("Layer mask cần tìm. Dùng kết hợp hoặc thay thế Tag.")]
        [SerializeField] private LayerMask _targetLayers = 0;

        [Tooltip("Bật để dùng Layer mask thay vì Tag list.")]
        [SerializeField] private bool _useLayerInsteadOfTag = false;

        [Header("Scan Settings")]
        [Tooltip("Khoảng cách tối đa (m) để xét target. 0 = không giới hạn.")]
        [SerializeField] private float _maxDistance = 50f;

        [Tooltip("Góc tối đa (độ) từ tâm màn hình để xét target. "
               + "Nên bằng hoặc nhỏ hơn AimAssist.SnapRadius.")]
        [SerializeField] private float _scanAngle = 10f;

        [Tooltip("Bao nhiêu giây quét lại một lần (0 = mỗi frame).")]
        [SerializeField] private float _scanInterval = 0.1f;

        [Tooltip("Bật để kiểm tra vật cản (Raycast) trước khi chọn target.")]
        [SerializeField] private bool _requireLineOfSight = true;

        [Tooltip("Layer mask dùng cho Raycast kiểm tra vật cản.")]
        [SerializeField] private LayerMask _obstacleLayers = ~0;

        [Tooltip("Điểm ngắm trên target: Center = gốc Transform, "
               + "Collider = tâm Collider, Custom = dùng _aimPoint offset.")]
        [SerializeField] private AimPointMode _aimPointMode = AimPointMode.ColliderCenter;

        [Tooltip("Offset tùy chỉnh (Local Space của target) khi dùng AimPointMode.Custom.")]
        [SerializeField] private Vector3 _aimPointOffset = new Vector3(0f, 1.4f, 0f);
        
        // Thêm vào vùng INSPECTOR fields
        [Header("Debug Overlay")]
        [Tooltip("Hiển thị thông tin target trên Game View (chỉ hoạt động trong Editor).")]
        [SerializeField] private bool _showDebugOverlay = false;

        [Tooltip("Vị trí hiển thị overlay trên màn hình.")]
        [SerializeField] private Vector2 _overlayPosition = new Vector2(10f, 10f);

        // ══════════════════════════════════════════════════════
        //  PUBLIC READ-ONLY
        // ══════════════════════════════════════════════════════

        /// <summary>Target đang được lock hiện tại. null nếu không có target nào trong phạm vi.</summary>
        public Transform CurrentTarget    { get; private set; }

        /// <summary>Vị trí world-space điểm ngắm trên CurrentTarget.</summary>
        public Vector3   CurrentAimPoint  { get; private set; }

        /// <summary>Góc lệch (độ) giữa tâm màn hình và CurrentTarget. -1 nếu không có target.</summary>
        public float     CurrentAngle     { get; private set; } = -1f;

        // ══════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════

        private Camera   _camera;
        private float    _scanTimer;
        private TISInputBridge _input;

        // Cache danh sách collider để tránh alloc mỗi frame
        private readonly Collider[] _colliderBuffer = new Collider[64];

        // ══════════════════════════════════════════════════════
        //  ENUM
        // ══════════════════════════════════════════════════════

        public enum AimPointMode
        {
            Center,         // gốc Transform
            ColliderCenter, // tâm Bounds của Collider
            Custom          // offset tùy chỉnh
        }

        // ══════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _input = TISInputBridge.Instance;
        }

        private void Start()
        {
            if (_input == null) _input = TISInputBridge.Instance;
        }

        private void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval <= 0f ? 0f : _scanInterval;
                ScanForTarget();
            }
        }
        
        // Thêm vào sau Update() hoặc LateUpdate()
        private void OnGUI()
        {
            if (!_showDebugOverlay) return;
            if (!Application.isEditor) return;

            // ── 1. Vòng tròn phạm vi quét ở giữa màn hình ────────────
            DrawScanCircle();

            // ── 2. Text overlay ───────────────────────────────────────
            if (!Application.isPlaying) return;
            DrawTextOverlay();
        }

        private void DrawScanCircle()
        {
            // Tính bán kính pixel từ góc scanAngle và FOV của camera
            float fov        = _camera != null ? _camera.fieldOfView : 60f;
            float screenH    = Screen.height;
            float screenW    = Screen.width;
            float centerX    = screenW * 0.5f;
            float centerY    = screenH * 0.5f;

            // Bán kính pixel = tỉ lệ giữa scanAngle và half-FOV vertical
            float halfFovRad    = fov * 0.5f * Mathf.Deg2Rad;
            float scanAngleRad  = _scanAngle * Mathf.Deg2Rad;
            float radiusPx      = (screenH * 0.5f) * Mathf.Tan(scanAngleRad) / Mathf.Tan(halfFovRad);

            int segments = 64;
            Vector2 prev = Vector2.zero;

            // Màu: đỏ nếu có target, xanh lá nếu không
            Color circleColor = CurrentTarget != null
                ? new Color(0.2f, 1f, 0.3f, 0.85f)
                : new Color(0f, 0.85f, 1f, 0.5f);

            // Vẽ bằng GL hoặc Handles — dùng GUI.DrawTexture pixel-by-pixel
            // Unity OnGUI không có DrawCircle native, dùng trick vẽ từng đoạn thẳng nhỏ
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var cur = new Vector2(
                    centerX + Mathf.Cos(angle) * radiusPx,
                    centerY + Mathf.Sin(angle) * radiusPx);

                if (i > 0)
                    DrawLine(prev, cur, circleColor, 1.5f);

                prev = cur;
            }

            // Dấu chấm ở tâm
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(centerX - 2f, centerY - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Vẽ đoạn thẳng trong OnGUI bằng cách xoay GUI matrix
        private void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            Vector2 dir    = (to - from);
            float length   = dir.magnitude;
            float angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 center = (from + to) * 0.5f;

            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(-angle, center);
        }

        private void DrawTextOverlay()
        {
            float x     = _overlayPosition.x;
            float y     = _overlayPosition.y;
            float w     = 280f;
            float lineH = 20f;
            float pad   = 8f;
            float boxH  = lineH * 4 + pad * 2;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, boxH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.color = new Color(0.25f, 0.75f, 1f);
            GUI.Label(new Rect(x + pad, y + pad, w, lineH), "◈ TIS Target Scanner");
            GUI.color = Color.white;

            float yLine = y + pad + lineH;

            if (CurrentTarget != null)
            {
                GUI.color = new Color(0.3f, 1f, 0.4f);
                GUI.Label(new Rect(x + pad, yLine, w, lineH), $"● Target: {CurrentTarget.name}");
                GUI.color = Color.white;
                yLine += lineH;

                Color angleCol = CurrentAngle < 3f  ? new Color(0.3f, 1f, 0.4f)
                               : CurrentAngle < 7f  ? new Color(1f, 0.85f, 0.2f)
                                                    : new Color(1f, 0.35f, 0.35f);
                GUI.color = angleCol;
                GUI.Label(new Rect(x + pad, yLine, w, lineH),
                    $"  Angle: {CurrentAngle:F2}°  /  Scan: {_scanAngle}°");
                GUI.color = Color.white;
                yLine += lineH;

                float dist = Vector3.Distance(transform.position, CurrentAimPoint);
                GUI.Label(new Rect(x + pad, yLine, w, lineH),
                    $"  Distance: {dist:F1} m  /  Max: {(_maxDistance > 0f ? _maxDistance + " m" : "∞")}");
            }
            else
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(x + pad, yLine, w, lineH), "○ No target in range");
                GUI.color = Color.white;
                yLine += lineH;

                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(x + pad, yLine, w, lineH),
                    $"  Scan angle: {_scanAngle}°   Interval: {_scanInterval:F2}s");
                GUI.color = Color.white;
            }
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Buộc quét lại ngay lập tức thay vì chờ _scanInterval.
        /// </summary>
        public void ForceScan() => ScanForTarget();

        /// <summary>
        /// Xóa target đang lock (dùng khi target chết, ra ngoài tầm nhìn, v.v.).
        /// </summary>
        public void ClearTarget()
        {
            CurrentTarget   = null;
            CurrentAimPoint = Vector3.zero;
            CurrentAngle    = -1f;
        }

        // ══════════════════════════════════════════════════════
        //  CORE SCAN
        // ══════════════════════════════════════════════════════

        private void ScanForTarget()
        {
            Transform bestTarget    = null;
            Vector3   bestAimPoint  = Vector3.zero;
            float     bestAngle     = float.MaxValue;

            // --- Lấy danh sách ứng viên ---
            if (_useLayerInsteadOfTag)
            {
                ScanByLayer(ref bestTarget, ref bestAimPoint, ref bestAngle);
            }
            else
            {
                ScanByTags(ref bestTarget, ref bestAimPoint, ref bestAngle);
            }

            CurrentTarget   = bestTarget;
            CurrentAimPoint = bestAimPoint;
            CurrentAngle    = bestTarget != null ? bestAngle : -1f;
            
            if (_input != null)
                _input.AimAssistTarget = CurrentTarget;
        }

        // ── Quét theo Tag ──────────────────────────────────────

        private void ScanByTags(ref Transform best, ref Vector3 bestPoint, ref float bestAngle)
        {
            foreach (string tag in _targetTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;

                GameObject[] candidates = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject go in candidates)
                {
                    TryCandidate(go.transform, ref best, ref bestPoint, ref bestAngle);
                }
            }
        }

        // ── Quét theo Layer (dùng OverlapSphere cho hiệu suất tốt hơn) ──

        private void ScanByLayer(ref Transform best, ref Vector3 bestPoint, ref float bestAngle)
        {
            float radius = _maxDistance > 0f ? _maxDistance : 1000f;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, radius, _colliderBuffer, _targetLayers);

            for (int i = 0; i < count; i++)
            {
                TryCandidate(_colliderBuffer[i].transform, ref best, ref bestPoint, ref bestAngle);
            }
        }

        // ── Đánh giá từng ứng viên ────────────────────────────

        private void TryCandidate(Transform candidate, ref Transform best,
                                  ref Vector3 bestPoint, ref float bestAngle)
        {
            // 1. Loại bỏ chính camera
            if (candidate == transform) return;

            // 2. Kiểm tra khoảng cách
            Vector3 aimPoint = GetAimPoint(candidate);
            float dist = Vector3.Distance(transform.position, aimPoint);
            if (_maxDistance > 0f && dist > _maxDistance) return;

            // 3. Kiểm tra góc từ tâm màn hình
            Vector3 dir = (aimPoint - transform.position).normalized;

            // Bỏ qua nếu phía sau camera
            if (Vector3.Dot(transform.forward, dir) <= 0f) return;

            Vector3 localDir = transform.InverseTransformDirection(dir);
            float yaw   =  Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
            float angle = Mathf.Sqrt(yaw * yaw + pitch * pitch);

            if (angle > _scanAngle) return;

            // 4. Kiểm tra line-of-sight
            if (_requireLineOfSight)
            {
                Vector3 origin    = transform.position;
                Vector3 direction = (aimPoint - origin).normalized;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, dist, _obstacleLayers))
                {
                    // Nếu vật đầu tiên bị chặn không phải là chính target → bị cản
                    if (hit.transform != candidate && !hit.transform.IsChildOf(candidate))
                        return;
                }
            }

            // 5. Chọn target gần tâm màn hình nhất (góc nhỏ nhất)
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best      = candidate;
                bestPoint = aimPoint;
            }
        }

        // ── Tính điểm ngắm trên target ────────────────────────

        private Vector3 GetAimPoint(Transform target)
        {
            switch (_aimPointMode)
            {
                case AimPointMode.ColliderCenter:
                    Collider col = target.GetComponentInChildren<Collider>();
                    if (col != null) return col.bounds.center;
                    return target.position;

                case AimPointMode.Custom:
                    return target.TransformPoint(_aimPointOffset);

                default: // Center
                    return target.position;
            }
        }
    }
}