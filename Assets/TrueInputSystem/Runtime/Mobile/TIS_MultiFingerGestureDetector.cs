using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/Multi Finger Gesture Detector")]
    [DefaultExecutionOrder(-85)] // chạy sau TISTouchCameraZone (-90) để đọc IsPinching đúng
    public class TISMultiFingerGestureDetector : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════

        private TISInputBridge     _inputBridge;
        private TISTouchCameraZone _cameraZone;
        private TISVirtualJoystick _joystick;

        [Header("Zone")]
        [Tooltip("true  = chỉ nhận touch trong camera zone (khớp TISTouchCameraZone).\n" +
                 "false = nhận toàn màn hình.")]
        [SerializeField] private bool _respectCameraZone = true;

        [Tooltip("Tỉ lệ X bắt đầu zone. Gọi SyncZone() để tự lấy từ CameraZone.")]
        [Range(0f, 1f)]
        [SerializeField] private float _zoneStartRatio = 0.5f;

        [Range(0f, 1f)]
        [SerializeField] private float _zoneEndRatio = 1f;

        [Header("Two-Finger Pan")]
        [Tooltip("Deadzone pixel. Midpoint delta dưới ngưỡng này → PanDelta = zero.")]
        [Range(0f, 30f)]
        [SerializeField] private float _panDeadzonePx = 5f;

        [Tooltip("Ngưỡng cos giữa 2 vector di chuyển để xác nhận cùng hướng.\n" +
                 "0.65 ≈ lệch tối đa ~49° · 1.0 = phải hoàn toàn cùng hướng.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float _panConsistencyThreshold = 0.65f;

        [Tooltip("Hệ số nhân lên TwoFingerPanDelta. Không ảnh hưởng TwoFingerPanRaw.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float _panSensitivity = 1f;

        [Header("Debug")]
        [SerializeField] private bool _debugDraw = false;

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC DATA
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Số ngón đang chạm trong zone frame này (đã loại trừ joystick/button).</summary>
        public int ActiveFingerCount { get; private set; }

        /// <summary>
        /// Vị trí screen của ngón thứ <paramref name="index"/>.
        /// Trả về Vector2.zero nếu index nằm ngoài ActiveFingerCount.
        /// </summary>
        public Vector2 GetFingerPosition(int index)
            => (index >= 0 && index < _fingerCount) ? _fingerPositions[index] : Vector2.zero;

        /// <summary>Tâm (centroid) của tất cả ngón đang trong zone.</summary>
        public Vector2 CentroidPosition { get; private set; }

        /// <summary>
        /// Delta của centroid so với frame trước (pixel, screen space).
        /// Zero khi số ngón thay đổi giữa 2 frame (ngón mới chạm / ngón nhấc lên).
        /// </summary>
        public Vector2 CentroidDelta { get; private set; }

        /// <summary>true khi đúng 2 ngón đang di chuyển cùng hướng.</summary>
        public bool IsTwoFingerPanning { get; private set; }

        /// <summary>
        /// Delta midpoint của 2 ngón frame này, đã nhân _panSensitivity.
        /// Zero khi không pan hoặc dưới deadzone.
        /// </summary>
        public Vector2 TwoFingerPanDelta { get; private set; }

        /// <summary>
        /// Delta midpoint của 2 ngón frame này, chưa nhân sensitivity.
        /// Dùng khi bạn muốn tự scale theo ý.
        /// </summary>
        public Vector2 TwoFingerPanRaw { get; private set; }

        /// <summary>Delta pixel của ngón index 0 frame này (raw, screen space).</summary>
        public Vector2 Finger0Delta { get; private set; }

        /// <summary>Delta pixel của ngón index 1 frame này (raw, screen space).</summary>
        public Vector2 Finger1Delta { get; private set; }

        // ══════════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════════════

        private const int MaxTrackedFingers = 5;

        // Frame hiện tại
        private readonly int[]     _fingerIds       = new int[MaxTrackedFingers];
        private readonly Vector2[] _fingerPositions = new Vector2[MaxTrackedFingers];
        private int _fingerCount = 0;

        // Frame trước (để tính delta)
        private readonly int[]     _prevIds      = new int[MaxTrackedFingers];
        private readonly Vector2[] _prevPos      = new Vector2[MaxTrackedFingers];
        private int     _prevCount    = 0;
        private Vector2 _prevCentroid = Vector2.zero;

        // ══════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Chỉ đăng ký với bridge. Mọi dependency khác (_cameraZone, _joystick,
            // zone ratio) được cung cấp qua InjectDependencies() từ TISMobileInputSetup
            // ngay sau khi AddComponent — không cần tự tìm ở đây.
            // Fallback TISInputBridge.Instance chỉ dùng khi component được thêm thủ công
            // vào scene (ngoài luồng TISMobileInputSetup).
            if (_inputBridge == null)
                _inputBridge = TISInputBridge.Instance;

            _inputBridge?.RegisterGestureDetector(this);
        }

        private void OnEnable()  => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            ClearAllOutput();
        }

        private void Start()
        {
            if (!Application.isMobilePlatform && !Application.isEditor) { enabled = false; return; }
            // SyncZone() đọc _cameraZone.ZoneStartRatio — đảm bảo an toàn vì:
            // · TISMobileInputSetup (ExecutionOrder -50) đã chạy Start() trước
            //   và gọi DongBoVungManHinh() → set _cameraZone._zoneStartRatio đúng.
            // · InjectDependencies() đã được gọi trong Awake của MobileInputSetup
            //   → _cameraZone không null tại đây.
            // Nếu component thêm thủ công (không qua MobileInputSetup), SyncZone()
            // có guard (_cameraZone == null → return) nên vẫn an toàn.
            SyncZone();
        }

        private void Update()
        {
            if (Application.isEditor && !Application.isPlaying) return;
            CollectTouches();
            ComputeCentroid();
            ComputeTwoFingerPan();
            SnapshotForNextFrame();
        }

        private void OnDestroy()
        {
            _inputBridge?.UnregisterGestureDetector(this);
        }

        // ══════════════════════════════════════════════════════════════════
        //  CORE: THU THẬP TOUCH
        // ══════════════════════════════════════════════════════════════════

        private void CollectTouches()
        {
            _fingerCount = 0;

            var touches    = Touch.activeTouches;
            int joystickId = _joystick != null ? _joystick.ActiveTouchId : -1;

            for (int i = 0; i < touches.Count && _fingerCount < MaxTrackedFingers; i++)
            {
                var t = touches[i];

                // Loại trừ ngón joystick
                if (t.touchId == joystickId) continue;

                // Loại trừ ngón đang giữ skill button
                if (TISMobileButtonRegistry.IsEnhancedTouchConsumed(t.touchId)) continue;

                // Loại trừ ngoài zone
                if (_respectCameraZone && !InZone(t.screenPosition)) continue;

                _fingerIds[_fingerCount]       = t.touchId;
                _fingerPositions[_fingerCount] = t.screenPosition;
                _fingerCount++;
            }

            ActiveFingerCount = _fingerCount;
        }

        // ══════════════════════════════════════════════════════════════════
        //  CORE: CENTROID
        // ══════════════════════════════════════════════════════════════════

        private void ComputeCentroid()
        {
            if (_fingerCount == 0)
            {
                CentroidPosition = Vector2.zero;
                CentroidDelta    = Vector2.zero;
                return;
            }

            // Tính tâm
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < _fingerCount; i++)
                sum += _fingerPositions[i];
            Vector2 centroid = sum / _fingerCount;

            // Delta chỉ có nghĩa khi số ngón bằng nhau giữa 2 frame
            CentroidDelta    = (_prevCount == _fingerCount) ? centroid - _prevCentroid : Vector2.zero;
            CentroidPosition = centroid;
        }

        // ══════════════════════════════════════════════════════════════════
        //  CORE: TWO-FINGER PAN
        // ══════════════════════════════════════════════════════════════════

        private void ComputeTwoFingerPan()
        {
            // Reset mỗi frame
            TwoFingerPanDelta  = Vector2.zero;
            TwoFingerPanRaw    = Vector2.zero;
            Finger0Delta       = Vector2.zero;
            Finger1Delta       = Vector2.zero;
            IsTwoFingerPanning = false;

            // Điều kiện: đúng 2 ngón, frame trước cũng 2 ngón, CameraZone không đang pinch
            if (_fingerCount != 2 || _prevCount != 2) return;
            //if (_cameraZone != null && _cameraZone.IsPinching) return;

            // Tìm vị trí frame trước theo touchId (không dùng index — thứ tự có thể đổi)
            Vector2 prev0 = FindPrevPos(_fingerIds[0]);
            Vector2 prev1 = FindPrevPos(_fingerIds[1]);

            // Ngón mới chạm trong frame này → chưa có prev → bỏ qua frame đầu
            bool has0 = HasPrevId(_fingerIds[0]);
            bool has1 = HasPrevId(_fingerIds[1]);
            if (!has0 || !has1) return;

            Vector2 delta0 = _fingerPositions[0] - prev0;
            Vector2 delta1 = _fingerPositions[1] - prev1;

            Finger0Delta = delta0;
            Finger1Delta = delta1;

            float mag0 = delta0.magnitude;
            float mag1 = delta1.magnitude;

            // Cả 2 ngón gần như đứng yên → không có gì để đọc
            if (mag0 < 0.01f && mag1 < 0.01f) return;

            // Kiểm tra cùng hướng bằng cosine
            bool sameDirection;
            if (mag0 > 0.01f && mag1 > 0.01f)
            {
                float cos = Vector2.Dot(delta0.normalized, delta1.normalized);
                sameDirection = cos >= _panConsistencyThreshold;
            }
            else
            {
                // 1 ngón gần đứng yên → coi là pan nhưng không enforce hướng
                sameDirection = true;
            }

            if (!sameDirection) return;

            // Midpoint delta
            Vector2 midRaw = (delta0 + delta1) * 0.5f;
            if (midRaw.magnitude < _panDeadzonePx) return;

            TwoFingerPanRaw    = midRaw;
            TwoFingerPanDelta  = midRaw * _panSensitivity;
            IsTwoFingerPanning = true;
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════

        private bool InZone(Vector2 screenPos)
        {
            if (Screen.width <= 0) return false;
            float normX = screenPos.x / Screen.width;
            return normX >= _zoneStartRatio && normX <= _zoneEndRatio;
        }

        private bool HasPrevId(int touchId)
        {
            for (int i = 0; i < _prevCount; i++)
                if (_prevIds[i] == touchId) return true;
            return false;
        }

        private Vector2 FindPrevPos(int touchId)
        {
            for (int i = 0; i < _prevCount; i++)
                if (_prevIds[i] == touchId) return _prevPos[i];
            return Vector2.zero;
        }

        private void SnapshotForNextFrame()
        {
            _prevCount    = _fingerCount;
            _prevCentroid = CentroidPosition;
            for (int i = 0; i < _fingerCount; i++)
            {
                _prevIds[i] = _fingerIds[i];
                _prevPos[i] = _fingerPositions[i];
            }
        }

        private void ClearAllOutput()
        {
            ActiveFingerCount  = 0;
            CentroidPosition   = Vector2.zero;
            CentroidDelta      = Vector2.zero;
            TwoFingerPanDelta  = Vector2.zero;
            TwoFingerPanRaw    = Vector2.zero;
            IsTwoFingerPanning = false;
            Finger0Delta       = Vector2.zero;
            Finger1Delta       = Vector2.zero;
            _fingerCount       = 0;
            _prevCount         = 0;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Đồng bộ zone ratio từ TISTouchCameraZone.
        /// Gọi sau khi TISMobileInputSetup.DatTyLeVung() thay đổi tỉ lệ.
        ///
        /// Yêu cầu thêm vào TISTouchCameraZone.cs:
        ///     public float ZoneStartRatio => _zoneStartRatio;
        ///     public float ZoneEndRatio   => _zoneEndRatio;
        /// </summary>
        /// <summary>
        /// Set trực tiếp zone ratio — ưu tiên hơn SyncZone().
        /// Được gọi bởi TISInputBridge.GestureSyncZone() với giá trị từ MobileInputSetup.
        /// Không phụ thuộc vào timing hay reflection của _cameraZone.
        /// </summary>
        public void SetZoneRatio(float startRatio, Object obj, float endRatio = 1f)
        {
            _zoneStartRatio = startRatio;
            _zoneEndRatio   = endRatio;
            //Debug.Log($"{_zoneStartRatio} and {_zoneEndRatio}", obj);
        }

        /// <summary>
        /// Đồng bộ zone ratio từ _cameraZone (fallback khi dùng không qua MobileInputSetup).
        /// Ưu tiên dùng SetZoneRatio() khi có giá trị trực tiếp từ MobileInputSetup.
        /// </summary>
        public void SyncZone()
        {
            if (_cameraZone == null || !_respectCameraZone) return;
            SetZoneRatio(_cameraZone.ZoneStartRatio, this, _cameraZone.ZoneEndRatio);
        }

        /// <summary>Inject dependency từ code thay vì Inspector.</summary>
        public void InjectDependencies(TISInputBridge bridge, TISTouchCameraZone zone, TISVirtualJoystick joystick)
        {
            // Hủy đăng ký cũ nếu bridge thay đổi
            if (_inputBridge != null && _inputBridge != bridge)
                _inputBridge.UnregisterGestureDetector(this);

            _inputBridge = bridge;
            _cameraZone  = zone;
            _joystick    = joystick;
            SyncZone();

            _inputBridge?.RegisterGestureDetector(this);
        }

        // ══════════════════════════════════════════════════════════════════
        //  DEBUG GUI (Editor only)
        // ══════════════════════════════════════════════════════════════════
        
        
        [Header("Debug Size")]
        [Tooltip("Chiều rộng khung debug (pixel).")]
        [Range(180f, 600f)]
        [SerializeField] private float _debugWidth  = 320f;

        [Tooltip("Khoảng cách từ cạnh trái màn hình.")]
        [Range(0f, 200f)]
        [SerializeField] private float _debugMarginLeft = 10f;

        [Tooltip("Khoảng cách từ cạnh trên màn hình.")]
        [Range(0f, 400f)]
        [SerializeField] private float _debugMarginTop = 60f;

        [Tooltip("Cỡ chữ khung debug.")]
        [Range(10, 28)]
        [SerializeField] private int _debugFontSize = 14;

        // ── màu cho từng loại giá trị ────────────────────────────────────
        // Khai báo static để tránh alloc mỗi frame (OnGUI gọi nhiều lần/frame)
        private static readonly Color _colHeader   = new Color(1.00f, 0.85f, 0.20f); // vàng  — tiêu đề
        private static readonly Color _colLabel    = new Color(0.75f, 0.75f, 0.75f); // xám   — tên field
        private static readonly Color _colInactive = new Color(0.45f, 0.45f, 0.45f); // xám tối — zero / false
        private static readonly Color _colActive   = new Color(0.30f, 1.00f, 0.50f); // xanh lá — đang active
        private static readonly Color _colVec      = new Color(0.40f, 0.80f, 1.00f); // xanh dương — vector có giá trị
        private static readonly Color _colBool     = new Color(1.00f, 0.45f, 0.45f); // đỏ nhạt — bool true
        private static readonly Color _colFinger   = new Color(1.00f, 0.65f, 0.20f); // cam — vị trí ngón

        // Cache style để không new mỗi frame
        private GUIStyle _dbLabelStyle;
        private GUIStyle _dbValueStyle;
        private GUIStyle _dbBgStyle;
        private int      _dbCachedFontSize = -1;

        private void EnsureStyles()
        {
            if (_dbCachedFontSize == _debugFontSize) return; // không đổi → giữ nguyên
            _dbCachedFontSize = _debugFontSize;

            _dbLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = _debugFontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
            };
            _dbValueStyle = new GUIStyle(_dbLabelStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
            };
            _dbBgStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize  = _debugFontSize,
                alignment = TextAnchor.UpperLeft,
            };
        }

        private void OnGUI()
        {
            if (!_debugDraw) return;
            if (!Application.isEditor) return;
            EnsureStyles();

            float lineH    = _debugFontSize + 6f;
            float padX     = 8f;
            float padY     = 6f;
            float labelW   = _debugWidth * 0.52f;
            float valueW   = _debugWidth - labelW - padX * 2f;

            // ── Tính số dòng để biết chiều cao khung ────────────────────
            int rowCount = 10 + ActiveFingerCount; // 10 dòng cố định + 1 dòng/ngón
            float boxH = padY * 2f + lineH + (rowCount * lineH); // header + rows

            float bx = _debugMarginLeft;
            float by = _debugMarginTop;

            // ── Nền khung ────────────────────────────────────────────────
            var bgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
            GUI.Box(new Rect(bx, by, _debugWidth, boxH), GUIContent.none, _dbBgStyle);
            GUI.backgroundColor = bgColor;

            // Vị trí bắt đầu vẽ nội dung (có padding)
            float cx = bx + padX;
            float cy = by + padY;

            // ── Header ───────────────────────────────────────────────────
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "MultiFingerGesture", value: "",
                labelColor: _colHeader, valueColor: _colHeader,
                isHeader: true);

            DrawDivider(cx, cy, _debugWidth - padX * 2f);
            cy += 3f;

            // ── Fingers (đổi màu khi > 0) ────────────────────────────────
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "ActiveFingers",
                value: ActiveFingerCount.ToString(),
                labelColor: _colLabel,
                valueColor: ActiveFingerCount > 0 ? _colActive : _colInactive);

            // ── Centroid Position ─────────────────────────────────────────
            bool centroidActive = CentroidPosition.sqrMagnitude > 0.01f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "CentroidPos",
                value: centroidActive ? CentroidPosition.ToString("F0") : "—",
                labelColor: _colLabel,
                valueColor: centroidActive ? _colVec : _colInactive);

            // ── Centroid Delta (xanh khi di chuyển) ──────────────────────
            bool centroidMoving = CentroidDelta.sqrMagnitude > 0.01f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "CentroidDelta",
                value: centroidMoving ? CentroidDelta.ToString("F2") : "(0, 0)",
                labelColor: _colLabel,
                valueColor: centroidMoving ? _colVec : _colInactive);

            DrawDivider(cx, cy, _debugWidth - padX * 2f);
            cy += 3f;

            // ── IsTwoFingerPanning (đỏ khi true) ─────────────────────────
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "IsTwoFingerPanning",
                value: IsTwoFingerPanning.ToString(),
                labelColor: _colLabel,
                valueColor: IsTwoFingerPanning ? _colBool : _colInactive);

            // ── PanDelta ──────────────────────────────────────────────────
            bool panActive = TwoFingerPanDelta.sqrMagnitude > 0.001f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "PanDelta (scaled)",
                value: panActive ? TwoFingerPanDelta.ToString("F2") : "(0, 0)",
                labelColor: _colLabel,
                valueColor: panActive ? _colVec : _colInactive);

            // ── PanRaw ────────────────────────────────────────────────────
            bool rawActive = TwoFingerPanRaw.sqrMagnitude > 0.001f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "PanRaw",
                value: rawActive ? TwoFingerPanRaw.ToString("F2") : "(0, 0)",
                labelColor: _colLabel,
                valueColor: rawActive ? _colVec : _colInactive);

            DrawDivider(cx, cy, _debugWidth - padX * 2f);
            cy += 3f;

            // ── Finger 0 Delta ────────────────────────────────────────────
            bool f0Active = Finger0Delta.sqrMagnitude > 0.001f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "Finger0 Δ",
                value: f0Active ? Finger0Delta.ToString("F2") : "(0, 0)",
                labelColor: _colLabel,
                valueColor: f0Active ? _colFinger : _colInactive);

            // ── Finger 1 Delta ────────────────────────────────────────────
            bool f1Active = Finger1Delta.sqrMagnitude > 0.001f;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "Finger1 Δ",
                value: f1Active ? Finger1Delta.ToString("F2") : "(0, 0)",
                labelColor: _colLabel,
                valueColor: f1Active ? _colFinger : _colInactive);

            DrawDivider(cx, cy, _debugWidth - padX * 2f);
            cy += 3f;

            // ── CamZone Pinching ──────────────────────────────────────────
            bool pinching = _cameraZone != null && _cameraZone.IsPinching;
            DrawRow(ref cy, cx, labelW, valueW, lineH,
                label: "CamZone Pinching",
                value: _cameraZone != null ? pinching.ToString() : "N/A",
                labelColor: _colLabel,
                valueColor: pinching ? _colBool : _colInactive);

            // ── Vị trí từng ngón ─────────────────────────────────────────
            for (int i = 0; i < ActiveFingerCount; i++)
            {
                Vector2 pos = GetFingerPosition(i);
                DrawRow(ref cy, cx, labelW, valueW, lineH,
                    label: $"Pos[{i}]",
                    value: pos.ToString("F0"),
                    labelColor: _colLabel,
                    valueColor: _colFinger);
            }
        }

        // ── Helpers vẽ ───────────────────────────────────────────────────

        private void DrawRow(
            ref float cy, float cx, float labelW, float valueW, float lineH,
            string label, string value,
            Color labelColor, Color valueColor,
            bool isHeader = false)
        {
            var prevColor = GUI.contentColor;

            // Label
            GUI.contentColor = labelColor;
            _dbLabelStyle.fontStyle = isHeader ? FontStyle.Bold : FontStyle.Normal;
            _dbLabelStyle.fontSize  = isHeader ? _debugFontSize + 1 : _debugFontSize;
            GUI.Label(new Rect(cx, cy, labelW, lineH), label, _dbLabelStyle);

            // Value
            if (!string.IsNullOrEmpty(value))
            {
                GUI.contentColor = valueColor;
                GUI.Label(new Rect(cx + labelW, cy, valueW, lineH), value, _dbValueStyle);
            }

            GUI.contentColor = prevColor;
            cy += lineH;
        }

        private static void DrawDivider(float cx, float cy, float width)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(cx, cy, width, 1f), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
    }
}