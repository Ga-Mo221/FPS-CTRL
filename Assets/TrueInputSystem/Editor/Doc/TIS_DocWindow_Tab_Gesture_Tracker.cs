#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        // ══════════════════════════════════════════════════════
        //  SUB-TAB 12 — TISMultiFingerGestureDetector
        // ══════════════════════════════════════════════════════

        private void DrawCode_GestureDetector()
        {
            PageTitle("MultiFingerGestureDetector",
                "Theo dõi centroid, two-finger pan và vị trí từng ngón tay trong camera zone.\n" +
                "Truy cập qua TISInputBridge.Instance — không cần reference trực tiếp.");

            // ── OVERVIEW ─────────────────────────────────────────────────────
            Note("TISMultiFingerGestureDetector là component tự động được TISMobileInputSetup " +
                 "tạo ra. Toàn bộ dữ liệu được expose qua TISInputBridge thông qua các " +
                 "Gesture* properties — script của bạn không cần giữ reference trực tiếp " +
                 "đến detector.");

            Sep();

            // ── SECTION 1: LẤY REFERENCE ─────────────────────────────────────
            H2("1. Lấy Reference — Qua TISInputBridge");
            Body("Mọi dữ liệu gesture đều truy cập qua TISInputBridge.Instance. " +
                 "Detector tự đăng ký với bridge trong Awake() — không cần thêm gì:");
            CodeBlock(
                "using TrueInputSystem;\n" +
                "using UnityEngine;\n\n" +
                "public class MyGestureHandler : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _input = TISInputBridge.Instance;\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        // Tất cả Gesture* properties sẵn sàng dùng\n" +
                "        int fingerCount = _input.GestureActiveFingers;\n" +
                "    }\n" +
                "}");

            Tip("Nếu cần truy cập trực tiếp TISMultiFingerGestureDetector (hiếm), " +
                "dùng FindAnyObjectByType<TISMultiFingerGestureDetector>(). " +
                "Trong hầu hết trường hợp, Gesture* properties của bridge là đủ.");

            Sep();

            // ── SECTION 2: CÁC PROPERTIES ────────────────────────────────────
            H2("2. Toàn Bộ Gesture Properties — Cheat Sheet");
            CodeBlock(
                "// ── Số ngón (đã loại trừ ngón joystick và button) ──────────\n" +
                "int   fingers = _input.GestureActiveFingers;      // 0, 1, 2, 3...\n\n" +
                "// ── Centroid (tâm trung bình của tất cả ngón) ───────────────\n" +
                "Vector2 centPos   = _input.GestureCentroidPosition; // screen space\n" +
                "Vector2 centDelta = _input.GestureCentroidDelta;    // pixel/frame\n\n" +
                "// ── Two-Finger Pan ───────────────────────────────────────────\n" +
                "bool    isPanning = _input.GestureIsTwoFingerPanning;\n" +
                "Vector2 panDelta  = _input.GestureTwoFingerPanDelta; // x sensitivity\n" +
                "Vector2 panRaw    = _input.GestureTwoFingerPanRaw;   // chưa nhân\n\n" +
                "// ── Delta từng ngón riêng lẻ ─────────────────────────────────\n" +
                "Vector2 f0Delta = _input.GestureFinger0Delta; // ngón thứ nhất\n" +
                "Vector2 f1Delta = _input.GestureFinger1Delta; // ngón thứ hai\n\n" +
                "// ── Vị trí screen của ngón theo index ───────────────────────\n" +
                "Vector2 pos0 = _input.GestureGetFingerPosition(0);\n" +
                "Vector2 pos1 = _input.GestureGetFingerPosition(1);\n" +
                "Vector2 pos2 = _input.GestureGetFingerPosition(2); // ngón thứ 3");

            Sep();

            // ── SECTION 3: ĐẾM NGÓN VÀ GUARD ────────────────────────────────
            H2("3. Đếm Ngón — Guard Trước Khi Đọc Dữ Liệu");
            Body("Luôn kiểm tra ActiveFingerCount trước khi đọc position / delta " +
                 "để tránh đọc Vector2.zero khi không có ngón:");
            CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    int fingers = _input.GestureActiveFingers;\n\n" +
                "    if (fingers == 0)\n" +
                "    {\n" +
                "        // Không có ngón trong camera zone\n" +
                "        StopAllGestures();\n" +
                "        return;\n" +
                "    }\n\n" +
                "    if (fingers == 1)\n" +
                "    {\n" +
                "        // 1 ngón — camera rotation (xử lý bởi TISTouchCameraZone)\n" +
                "        // Gesture detector không xử lý 1 ngón trực tiếp\n" +
                "    }\n\n" +
                "    if (fingers == 2)\n" +
                "    {\n" +
                "        // 2 ngón — pinch zoom hoặc two-finger pan\n" +
                "        if (_input.GestureIsTwoFingerPanning)\n" +
                "            HandleTwoFingerPan();\n" +
                "    }\n\n" +
                "    if (fingers >= 3)\n" +
                "    {\n" +
                "        // 3+ ngón — centroid gesture (đặt biệt)\n" +
                "        HandleMultiFingerGesture();\n" +
                "    }\n" +
                "}");

            Sep();

            // ── SECTION 4: TWO-FINGER PAN ─────────────────────────────────────
            H2("4. Two-Finger Pan — Di Chuyển Map / Camera Từ 2 Ngón");
            Body("IsTwoFingerPanning = true khi đúng 2 ngón di chuyển CÙNG hướng " +
                 "(cosine ≥ _panConsistencyThreshold). " +
                 "Pinch zoom không kích hoạt IsTwoFingerPanning vì 2 ngón đi ngược nhau:");
            CodeBlock(
                "// ── Ví dụ: Pan map 2D theo 2 ngón ─────────────────────────\n" +
                "[SerializeField] private Transform _mapCamera;\n" +
                "[SerializeField] private float _panSpeed = 0.01f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (!_input.GestureIsTwoFingerPanning) return;\n\n" +
                "    // TwoFingerPanDelta đã nhân sensitivity từ _panSensitivity\n" +
                "    Vector2 pan = _input.GestureTwoFingerPanDelta;\n\n" +
                "    // Đảo chiều: kéo tay sang phải → map di chuyển sang phải\n" +
                "    _mapCamera.Translate(-pan.x * _panSpeed,\n" +
                "                        -pan.y * _panSpeed, 0f,\n" +
                "                         Space.World);\n" +
                "}");

            GUILayout.Space(8);
            Body("Nếu cần scale riêng, dùng TwoFingerPanRaw thay vì TwoFingerPanDelta:");
            CodeBlock(
                "// TwoFingerPanRaw: delta chưa nhân sensitivity\n" +
                "// Tự scale theo zoom level hiện tại\n" +
                "void Update()\n" +
                "{\n" +
                "    if (!_input.GestureIsTwoFingerPanning) return;\n\n" +
                "    Vector2 raw  = _input.GestureTwoFingerPanRaw;\n" +
                "    float   zoom = _currentZoomLevel;  // 0.5x → 4x\n\n" +
                "    // Khi zoom in: ít pixel di chuyển hơn (scale nghịch zoom)\n" +
                "    _mapCamera.Translate(\n" +
                "        -raw.x * _panSpeedBase / zoom,\n" +
                "        -raw.y * _panSpeedBase / zoom,\n" +
                "         0f, Space.World);\n" +
                "}");

            Sep();

            // ── SECTION 5: CENTROID ────────────────────────────────────────────
            H2("5. Centroid — Tâm Trung Bình Tất Cả Ngón");
            Body("CentroidPosition là tâm tất cả ngón đang trong zone. " +
                 "CentroidDelta là delta của tâm so với frame trước — " +
                 "bằng zero khi số ngón thay đổi (ngón mới chạm hoặc nhấc lên):");
            CodeBlock(
                "// ── Ví dụ: Xoay object 3D theo tâm 2 ngón ─────────────────\n" +
                "[SerializeField] private Transform _target3D;\n" +
                "[SerializeField] private float _orbitSensitivity = 0.3f;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureActiveFingers < 2) return;\n\n" +
                "    // CentroidDelta = 0 khi số ngón vừa thay đổi → bỏ qua frame đó\n" +
                "    Vector2 delta = _input.GestureCentroidDelta;\n" +
                "    if (delta.sqrMagnitude < 0.01f) return;\n\n" +
                "    float yaw   =  delta.x * _orbitSensitivity;\n" +
                "    float pitch = -delta.y * _orbitSensitivity;\n\n" +
                "    _target3D.Rotate(Vector3.up,    yaw,   Space.World);\n" +
                "    _target3D.Rotate(Vector3.right, pitch, Space.Self);\n" +
                "}");

            GUILayout.Space(8);

            Body("Dùng CentroidPosition để tính điểm xoay (pivot point) của gesture:");
            CodeBlock(
                "// ── Ví dụ: Zoom vào điểm giữa 2 ngón (pinch-to-zoom tùy chỉnh)\n" +
                "void HandleCustomPinch()\n" +
                "{\n" +
                "    if (_input.GestureActiveFingers != 2) return;\n\n" +
                "    // Điểm giữa 2 ngón trên screen\n" +
                "    Vector2 centroid = _input.GestureCentroidPosition;\n\n" +
                "    // Chuyển sang world point để zoom vào đúng điểm đó\n" +
                "    Vector3 worldPivot = Camera.main.ScreenToWorldPoint(\n" +
                "        new Vector3(centroid.x, centroid.y, _zoomDepth));\n\n" +
                "    ZoomTowardsPoint(worldPivot, _zoomDelta);\n" +
                "}");

            Sep();

            // ── SECTION 6: DELTA TỪNG NGÓN ───────────────────────────────────
            H2("6. Delta Từng Ngón — Finger0Delta / Finger1Delta");
            Body("Finger0Delta là delta của ngón đang rotate camera (_activeTouchId). " +
                 "Finger1Delta là ngón thứ hai vừa chạm vào. " +
                 "Cả hai bằng zero khi không có 2 ngón hoặc đang pinch:");
            CodeBlock(
                "// ── Ví dụ: Kiểm tra hướng di chuyển từng ngón ──────────────\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureActiveFingers != 2) return;\n\n" +
                "    Vector2 d0 = _input.GestureFinger0Delta;\n" +
                "    Vector2 d1 = _input.GestureFinger1Delta;\n\n" +
                "    // Kiểm tra 2 ngón có đi cùng hướng không\n" +
                "    float mag0 = d0.magnitude;\n" +
                "    float mag1 = d1.magnitude;\n\n" +
                "    if (mag0 > 0.01f && mag1 > 0.01f)\n" +
                "    {\n" +
                "        float cos = Vector2.Dot(d0.normalized, d1.normalized);\n" +
                "        bool  sameDir = cos > 0.65f; // khớp _panConsistencyThreshold\n\n" +
                "        if (sameDir)\n" +
                "            Debug.Log(\"2 ngón cùng hướng — PAN\");\n" +
                "        else\n" +
                "            Debug.Log(\"2 ngón ngược hướng — PINCH/ROTATE\");\n" +
                "    }\n" +
                "}");

            GUILayout.Space(8);

            Body("Ứng dụng thực tế: phân biệt pan vs twist rotate (ngón xoay tròn):");
            CodeBlock(
                "// ── Ví dụ: Twist Rotate — 2 ngón xoay ngược chiều nhau ────\n" +
                "[SerializeField] private Transform _spinTarget;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureActiveFingers != 2) return;\n" +
                "    if (_input.GestureIsTwoFingerPanning) return; // ưu tiên pan\n\n" +
                "    Vector2 d0 = _input.GestureFinger0Delta;\n" +
                "    Vector2 d1 = _input.GestureFinger1Delta;\n\n" +
                "    // Lấy vị trí của 2 ngón\n" +
                "    Vector2 p0 = _input.GestureGetFingerPosition(0);\n" +
                "    Vector2 p1 = _input.GestureGetFingerPosition(1);\n\n" +
                "    // Vector từ tâm tới ngón, và velocity của ngón\n" +
                "    Vector2 axis  = p1 - p0;\n" +
                "    float   cross = axis.x * (d0.y - d1.y) - axis.y * (d0.x - d1.x);\n\n" +
                "    // cross > 0: xoay ngược chiều kim đồng hồ\n" +
                "    float twistDeg = cross * 0.02f;\n" +
                "    _spinTarget.Rotate(Vector3.forward, twistDeg, Space.World);\n" +
                "}");

            Sep();

            // ── SECTION 7: VỊ TRÍ NGÓN THEO INDEX ───────────────────────────
            H2("7. GetFingerPosition — Vị Trí Ngón Theo Index");
            Body("GestureGetFingerPosition(i) trả về screen position của ngón thứ i trong zone. " +
                 "Trả về Vector2.zero nếu i >= ActiveFingerCount:");
            CodeBlock(
                "// ── Ví dụ: Hiển thị debug vị trí từng ngón ─────────────────\n" +
                "void OnGUI()\n" +
                "{\n" +
                "    int count = _input.GestureActiveFingers;\n" +
                "    for (int i = 0; i < count; i++)\n" +
                "    {\n" +
                "        Vector2 pos = _input.GestureGetFingerPosition(i);\n" +
                "        // Flip Y vì OnGUI origin = top-left, screen = bottom-left\n" +
                "        Vector2 guiPos = new Vector2(pos.x, Screen.height - pos.y);\n" +
                "        GUI.Label(new Rect(guiPos.x - 20, guiPos.y - 20, 80, 30),\n" +
                "                  $\"Ngón {i}\");\n" +
                "    }\n" +
                "}");

            GUILayout.Space(8);

            Body("Ứng dụng thực tế: đặt UI element (floating tooltip) tại vị trí ngón:");
            CodeBlock(
                "// ── Ví dụ: Di chuyển FloatingUI theo ngón thứ nhất ─────────\n" +
                "[SerializeField] private RectTransform _floatingUI;\n" +
                "[SerializeField] private Canvas        _canvas;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.GestureActiveFingers == 0)\n" +
                "    {\n" +
                "        _floatingUI.gameObject.SetActive(false);\n" +
                "        return;\n" +
                "    }\n\n" +
                "    _floatingUI.gameObject.SetActive(true);\n\n" +
                "    Vector2 screenPos = _input.GestureGetFingerPosition(0);\n\n" +
                "    // Chuyển screen → canvas local\n" +
                "    RectTransformUtility.ScreenPointToLocalPointInRectangle(\n" +
                "        _canvas.GetComponent<RectTransform>(),\n" +
                "        screenPos, _canvas.worldCamera,\n" +
                "        out Vector2 localPoint);\n\n" +
                "    _floatingUI.anchoredPosition = localPoint + new Vector2(0, 40f);\n" +
                "}");

            Sep();

            // ── SECTION 8: ZONE SYNC ──────────────────────────────────────────
            H2("8. Zone Ratio — Đồng Bộ Vùng Camera");
            Body("Detector chỉ nhận ngón trong camera zone (nửa phải màn hình mặc định). " +
                 "Zone ratio được tự động sync bởi TISMobileInputSetup. " +
                 "Gọi thủ công khi thay đổi tỉ lệ vùng lúc runtime:");
            CodeBlock(
                "// ── Thay đổi tỉ lệ joystick/camera lúc runtime ─────────────\n" +
                "TISMobileInputSetup setup = FindAnyObjectByType<TISMobileInputSetup>();\n\n" +
                "// Đổi sang 40% joystick / 60% camera\n" +
                "setup.DatTyLeVung(0.4f);\n\n" +
                "// Bridge tự gọi GestureSyncZone() bên trong DatTyLeVung\n" +
                "// Không cần gọi thêm gì\n\n" +
                "// ── Hoặc gọi trực tiếp qua Bridge ──────────────────────────\n" +
                "// zoneStartRatio = 0.5f → bắt đầu từ giữa màn hình\n" +
                "// zoneEndRatio   = 1.0f → đến cạnh phải\n" +
                "_input.GestureSyncZone(0.5f, 1.0f);\n\n" +
                "// ── Fallback: đọc từ TISTouchCameraZone ─────────────────────\n" +
                "_input.GestureSyncZone(); // không tham số = đọc lại từ CameraZone");

            Sep();

            // ── SECTION 9: USE CASE TỔNG HỢP ─────────────────────────────────
            H2("9. Use Case Tổng Hợp — RTS Map Controller");
            Body("Kết hợp pan 2 ngón + centroid zoom + finger position cho game RTS:");
            CodeBlock(
                "using TrueInputSystem;\n" +
                "using UnityEngine;\n\n" +
                "public class RTSMapController : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] private Camera   _mapCam;\n" +
                "    [SerializeField] private float    _panSpeed      = 0.008f;\n" +
                "    [SerializeField] private float    _zoomSpeed     = 0.05f;\n" +
                "    [SerializeField] private Vector2  _zoomRange     = new Vector2(3f, 20f);\n\n" +
                "    private TISInputBridge _input;\n" +
                "    private int            _prevFingerCount;\n\n" +
                "    void Start() => _input = TISInputBridge.Instance;\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        int fingers = _input.GestureActiveFingers;\n\n" +
                "        // Reset khi số ngón thay đổi\n" +
                "        if (fingers != _prevFingerCount)\n" +
                "        {\n" +
                "            _prevFingerCount = fingers;\n" +
                "            return; // bỏ qua frame chuyển tiếp\n" +
                "        }\n\n" +
                "        if (fingers == 2)\n" +
                "        {\n" +
                "            // ── Pan ────────────────────────────────────\n" +
                "            if (_input.GestureIsTwoFingerPanning)\n" +
                "            {\n" +
                "                Vector2 pan = _input.GestureTwoFingerPanRaw;\n" +
                "                float   z   = _mapCam.orthographicSize; // dùng zoom để scale\n" +
                "                _mapCam.transform.Translate(\n" +
                "                    -pan.x * _panSpeed * z,\n" +
                "                    -pan.y * _panSpeed * z,\n" +
                "                     0f, Space.World);\n" +
                "            }\n\n" +
                "            // ── Pinch Zoom (đọc từ Bridge) ──────────────\n" +
                "            float zoomDelta = _input.ZoomDelta;\n" +
                "            if (Mathf.Abs(zoomDelta) > 0.0001f)\n" +
                "            {\n" +
                "                // Zoom vào điểm giữa 2 ngón\n" +
                "                Vector2 centroid = _input.GestureCentroidPosition;\n" +
                "                ZoomTowardsCentroid(centroid, zoomDelta);\n" +
                "            }\n" +
                "        }\n" +
                "    }\n\n" +
                "    private void ZoomTowardsCentroid(Vector2 screenPivot, float delta)\n" +
                "    {\n" +
                "        float newSize = Mathf.Clamp(\n" +
                "            _mapCam.orthographicSize - delta * _zoomSpeed * 100f,\n" +
                "            _zoomRange.x, _zoomRange.y);\n\n" +
                "        // Giữ điểm pivot cố định trong world space khi zoom\n" +
                "        Vector3 worldBefore = _mapCam.ScreenToWorldPoint(\n" +
                "            new Vector3(screenPivot.x, screenPivot.y, 0f));\n\n" +
                "        _mapCam.orthographicSize = newSize;\n\n" +
                "        Vector3 worldAfter = _mapCam.ScreenToWorldPoint(\n" +
                "            new Vector3(screenPivot.x, screenPivot.y, 0f));\n\n" +
                "        _mapCam.transform.position += worldBefore - worldAfter;\n" +
                "    }\n" +
                "}");

            Sep();

            // ── SECTION 10: 3-NGÓN ───────────────────────────────────────────
            H2("10. 3+ Ngón — Centroid Gesture Đặc Biệt");
            Body("3 ngón trở lên dùng CentroidDelta vì pan/finger delta chỉ tính cho đúng 2 ngón:");
            CodeBlock(
                "// ── Ví dụ: 3 ngón → reset camera về mặc định ───────────────\n" +
                "private bool _threeFingerHeld;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    int fingers = _input.GestureActiveFingers;\n\n" +
                "    if (fingers >= 3 && !_threeFingerHeld)\n" +
                "    {\n" +
                "        _threeFingerHeld = true;\n" +
                "        OnThreeFingerTap();\n" +
                "    }\n\n" +
                "    if (fingers < 3)\n" +
                "        _threeFingerHeld = false;\n" +
                "}\n\n" +
                "void OnThreeFingerTap()\n" +
                "{\n" +
                "    // Reset view, show menu, screenshot, etc.\n" +
                "    var cam = FindAnyObjectByType<ThirdPersonCamera>();\n" +
                "    cam?.ResetToDefaultAngle();\n" +
                "    Debug.Log(\"3-finger tap: camera reset!\");\n" +
                "}");

            Sep();

            // ── SECTION 11: TIPS ──────────────────────────────────────────────
            H2("11. Tips & Lưu Ý Quan Trọng");
            CodeBlock(
                "// ✓ CentroidDelta = zero khi số ngón thay đổi — luôn guard:\n" +
                "if (_input.GestureCentroidDelta.sqrMagnitude < 0.01f) return;\n\n" +
                "// ✓ IsTwoFingerPanning = false khi đang pinch zoom:\n" +
                "// TISTouchCameraZone.IsPinching = true → pan bị suppress tự động.\n\n" +
                "// ✓ Finger0 = ngón đang rotate camera (_activeTouchId)\n" +
                "// Finger1 = ngón mới thêm vào — thứ tự KHÔNG theo activeTouches index.\n\n" +
                "// ✓ ActiveFingerCount đã loại trừ:\n" +
                "//   - Ngón giữ VirtualJoystick (nửa trái)\n" +
                "//   - Ngón đang giữ TISMobileSkillButton\n" +
                "//   - Ngón ngoài camera zone\n\n" +
                "// ✓ Nếu cần zone toàn màn hình (không respect camera zone):\n" +
                "//   Đặt _respectCameraZone = false trong Inspector của detector.\n\n" +
                "// ✓ Sensitivity của TwoFingerPanDelta chỉnh bằng\n" +
                "//   _panSensitivity trong Inspector (mặc định = 1).\n" +
                "//   Dùng TwoFingerPanRaw để tự scale.");

            Warn("GestureActiveFingers KHÔNG tính ngón đang giữ joystick hoặc button. " +
                 "Nếu người chơi giữ joystick (1 ngón trái) + 2 ngón phải: " +
                 "GestureActiveFingers = 2 (không phải 3).");
        }

        // ══════════════════════════════════════════════════════
        //  SUB-TAB 13 — TISTouchObjectTracker
        // ══════════════════════════════════════════════════════

        private void DrawCode_ObjectTracker()
        {
            PageTitle("Object Tracker — TISTouchObjectTracker",
                "Theo dõi touch vào GameObject 3D/2D, double-tap object và double-tap màn hình.\n" +
                "Tích hợp sẵn trong TISInputBridge — bật qua Inspector, truy cập qua Toucher*() methods.");

            // ── OVERVIEW ──────────────────────────────────────────────────────
            Note("TISTouchObjectTracker là plain class được TISInputBridge tạo và quản lý. " +
                 "Không cần thêm component. Bật / tắt qua field Enable Object Tracker " +
                 "trong TISInputBridge Inspector. Tất cả API truy cập qua Toucher*() methods.");

            Sep();

            // ── SECTION 1: BẬT TRACKER ────────────────────────────────────────
            H2("1. Bật Object Tracker — Inspector Setup");
            Body("Tracker mặc định TẮT để tiết kiệm performance. " +
                 "Bật trong Inspector của TISInputBridge:");
            CodeBlock(
                "// Trong TISInputBridge Inspector:\n" +
                "// ☐ Enable Object Tracker    → tick vào đây\n" +
                "// Raycast Camera             → Camera dùng để raycast (để trống = Camera.main)\n" +
                "// Double-Tap Interval        → 0.35s mặc định\n" +
                "// Double-Tap Distance (px)   → 60px mặc định\n" +
                "// Layer Mask                 → layer nào được raycast\n" +
                "// Raycast Max Distance       → 1000 mặc định\n" +
                "// Use 2D Physics             → true nếu game 2D\n" +
                "// Debug Log                  → log mỗi touch ra Console");

            GUILayout.Space(8);
            Body("Hoặc bật / tắt lúc runtime từ code:");
            CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n\n" +
                "// Bật tracker\n" +
                "input.SetObjectTrackerEnabled(true);\n\n" +
                "// Tắt tracker (khi không cần — tiết kiệm raycast overhead)\n" +
                "input.SetObjectTrackerEnabled(false);\n\n" +
                "// Đổi camera raycast lúc runtime\n" +
                "input.SetTrackerCamera(myNewCamera);");

            Sep();

            // ── SECTION 2: TOÀN BỘ API ───────────────────────────────────────
            H2("2. Toàn Bộ Toucher API — Cheat Sheet");
            CodeBlock(
                "TISInputBridge input = TISInputBridge.Instance;\n\n" +
                "// ── Danh sách & Count ───────────────────────────────────────\n" +
                "IReadOnlyList<int> ids  = input.ToucherObjectIDs;   // tất cả ID đã touch\n" +
                "int                cnt  = input.ToucherCount;        // số lần touch\n" +
                "bool               none = input.ToucherHasNoObjects(); // true nếu chưa touch gì\n\n" +
                "// ── Last touched ─────────────────────────────────────────────\n" +
                "int    lastId   = input.ToucherGetLastID();   // InstanceID, 0 nếu chưa có\n" +
                "string lastName = input.ToucherGetLastName(); // tên GameObject (debug)\n\n" +
                "// ── Was touched ever ─────────────────────────────────────────\n" +
                "bool wasTouched = input.ToucherWasTouched(someGO.GetInstanceID());\n\n" +
                "// ── Double-tap object (true đúng 1 frame) ────────────────────\n" +
                "bool dtObj = input.ToucherIsDoubleTapOnObject(someGO.GetInstanceID());\n\n" +
                "// ── Double-tap màn hình (true đúng 1 frame) ──────────────────\n" +
                "bool dtScreen = input.ToucherIsDoubleTapOnScreen();\n\n" +
                "// ── Lấy GameObject bị double-tap ─────────────────────────────\n" +
                "GameObject dtGO  = input.ToucherGetDoubleTappedObject();             // bất kỳ\n" +
                "GameObject dtGO2 = input.ToucherGetDoubleTappedObject(someId);       // theo ID\n\n" +
                "// ── Reset & Clear ─────────────────────────────────────────────\n" +
                "input.ToucherClearList();  // xóa danh sách touched objects\n" +
                "input.ToucherClearAll();   // xóa toàn bộ (list + double-tap state)\n" +
                "input.ToucherResetDoubleTapForObject(id); // reset double-tap của 1 object\n" +
                "input.ToucherResetDoubleTapScreen();      // reset double-tap màn hình\n\n" +
                "// ── Single-Touch Proxy ────────────────────────────────────────\n" +
                "GameObject single = input.ToucherGetSingleObject(); // GO đang touch (1 ngón), null nếu 0 hoặc >1\n" +
                "bool       isSingle = input.ToucherIsSingleTouch(); // true khi đúng 1 touch đang active\n" +
                "input.ToucherClearTouchCount();                      // reset bộ đếm touch về 0\n\n" +
                "// ── Debug ─────────────────────────────────────────────────────\n" +
                "input.ToucherLogObjects(); // in danh sách ra Console");

            Sep();

            // ── SECTION 3: DETECT TOUCH VÀO OBJECT ───────────────────────────
            H2("3. Detect Touch Vào GameObject — GetLastID & WasTouched");
            Body("Tracker ghi nhận mọi touch Began chạm trúng Collider qua Raycast. " +
                 "Danh sách tích lũy — clear thủ công khi cần reset:");
            CodeBlock(
                "// ── Ví dụ: Chọn unit trong game chiến thuật ────────────────\n" +
                "[SerializeField] private UnitSelector _selector;\n\n" +
                "private TISInputBridge _input;\n" +
                "private int            _prevLastId;\n\n" +
                "void Start() => _input = TISInputBridge.Instance;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    int lastId = _input.ToucherGetLastID();\n" +
                "    if (lastId == 0) return;          // chưa touch gì\n" +
                "    if (lastId == _prevLastId) return; // cùng object frame trước\n\n" +
                "    _prevLastId = lastId;\n\n" +
                "    // Tìm GO từ InstanceID\n" +
                "    // (cần lưu mapping ID → GO ở game layer,\n" +
                "    //  hoặc dùng ToucherGetDoubleTappedObject() trả sẵn GO)\n" +
                "    Debug.Log($\"Vừa touch object ID: {lastId}\");\n" +
                "    _selector.SelectById(lastId);\n" +
                "}");

            GUILayout.Space(8);

            Body("Cách khác: kiểm tra object cụ thể có bị touch không qua WasTouched:");
            CodeBlock(
                "// ── Kiểm tra object cụ thể đã bị touch lần nào chưa ────────\n" +
                "[SerializeField] private GameObject _chest;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherWasTouched(_chest.GetInstanceID()))\n" +
                "    {\n" +
                "        Debug.Log(\"Người chơi đã từng chạm vào rương!\");\n" +
                "        ShowOpenButton();\n" +
                "    }\n" +
                "}");

            Sep();

            // ── SECTION 4: DOUBLE-TAP OBJECT ─────────────────────────────────
            H2("4. Double-Tap Object — true Đúng 1 Frame");
            Body("IsDoubleTapOnObject() trả về true đúng 1 frame khi người chơi " +
                 "tap 2 lần vào cùng GameObject trong DoubleTapInterval giây. " +
                 "Sau đó tự reset — không cần clear thủ công:");
            CodeBlock(
                "// ── Ví dụ: Double-tap unit để mở menu đặc biệt ─────────────\n" +
                "[SerializeField] private GameObject[] _units;\n" +
                "[SerializeField] private UnitMenu     _unitMenu;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    foreach (var unit in _units)\n" +
                "    {\n" +
                "        int id = unit.GetInstanceID();\n" +
                "        if (_input.ToucherIsDoubleTapOnObject(id))\n" +
                "        {\n" +
                "            _unitMenu.OpenFor(unit);\n" +
                "            break; // chỉ xử lý 1 unit mỗi frame\n" +
                "        }\n" +
                "    }\n" +
                "}");

            GUILayout.Space(8);

            Body("Lấy thẳng GameObject bị double-tap qua GetDoubleTappedObject() " +
                 "khi không muốn loop qua danh sách:");
            CodeBlock(
                "// ── Lấy trực tiếp GO bị double-tap — không cần loop ────────\n" +
                "void Update()\n" +
                "{\n" +
                "    GameObject dtGO = _input.ToucherGetDoubleTappedObject();\n" +
                "    if (dtGO == null) return; // không có double-tap frame này\n\n" +
                "    // dtGO là object bị double-tap\n" +
                "    var interactable = dtGO.GetComponent<IInteractable>();\n" +
                "    interactable?.OnDoubleTap();\n" +
                "}");

            GUILayout.Space(8);

            Body("Lọc theo tag hoặc layer sau khi lấy GO:");
            CodeBlock(
                "// ── Double-tap chỉ nhận khi object có tag \"Enemy\" ──────────\n" +
                "void Update()\n" +
                "{\n" +
                "    GameObject dtGO = _input.ToucherGetDoubleTappedObject();\n" +
                "    if (dtGO == null) return;\n" +
                "    if (!dtGO.CompareTag(\"Enemy\")) return;\n\n" +
                "    // Focus camera vào enemy bị double-tap\n" +
                "    var cam = FindAnyObjectByType<ThirdPersonCamera>();\n" +
                "    if (cam != null) cam.target = dtGO.transform;\n" +
                "}");

            Sep();

            // ── SECTION 5: DOUBLE-TAP MÀN HÌNH ───────────────────────────────
            H2("5. Double-Tap Màn Hình — IsDoubleTapOnScreen");
            Body("IsDoubleTapOnScreen() = true đúng 1 frame khi tap 2 lần bất kỳ đâu trên màn hình " +
                 "trong DoubleTapInterval giây và DoubleTapDistancePx pixel. " +
                 "Không cần chạm vào Collider:");
            CodeBlock(
                "// ── Ví dụ: Double-tap màn hình → reset camera ───────────────\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "    {\n" +
                "        var cam = FindAnyObjectByType<ThirdPersonCamera>();\n" +
                "        cam?.ResetToDefaultAngle();\n" +
                "        Debug.Log(\"Double-tap màn hình: camera reset!\");\n" +
                "    }\n" +
                "}");

            GUILayout.Space(8);

            Body("Ứng dụng khác: double-tap để toggle pause, sprint, hoặc show/hide HUD:");
            CodeBlock(
                "// ── Double-tap → toggle Sprint ──────────────────────────────\n" +
                "private bool _sprintToggled;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        _sprintToggled = !_sprintToggled;\n\n" +
                "    float speed = _sprintToggled ? runSpeed : walkSpeed;\n" +
                "    Move(_input.MoveDirection * speed);\n" +
                "}\n\n" +
                "// ── Double-tap → show minimap fullscreen ─────────────────────\n" +
                "void Update2()\n" +
                "{\n" +
                "    if (_input.ToucherIsDoubleTapOnScreen())\n" +
                "        _minimapUI.SetFullscreen(!_minimapUI.IsFullscreen);\n" +
                "}");

            Sep();

            // ── SECTION 6: CLEAR VÀ RESET ────────────────────────────────────
            H2("6. Clear & Reset — Khi Nào Cần Gọi");
            Body("Danh sách touched objects tích lũy vô hạn — cần clear thủ công " +
                 "khi chuyển scene, respawn hoặc khi không cần lịch sử nữa:");
            CodeBlock(
                "// ── Clear khi chuyển scene ─────────────────────────────────\n" +
                "void OnSceneUnload()\n" +
                "{\n" +
                "    _input.ToucherClearAll(); // list + double-tap state\n" +
                "}\n\n" +
                "// ── Clear danh sách touched khi bắt đầu lượt mới ───────────\n" +
                "void OnRoundStart()\n" +
                "{\n" +
                "    _input.ToucherClearList(); // chỉ list, giữ double-tap state\n" +
                "}\n\n" +
                "// ── Reset double-tap của 1 object cụ thể ────────────────────\n" +
                "// VD: sau khi open chest, reset để không double-tap lại ngay\n" +
                "void OnChestOpened(GameObject chest)\n" +
                "{\n" +
                "    _input.ToucherResetDoubleTapForObject(chest.GetInstanceID());\n" +
                "}\n\n" +
                "// ── Reset double-tap màn hình ────────────────────────────────\n" +
                "void OnCutsceneEnd()\n" +
                "{\n" +
                "    // Tránh cutscene end bị trigger double-tap\n" +
                "    _input.ToucherResetDoubleTapScreen();\n" +
                "}");

            Sep();

            // ── SECTION 7: SINGLE-TOUCH PROXY ────────────────────────────────
            H2("7. Single-Touch Proxy — GetSingleObject, IsSingleTouch, ClearTouchCount");
            Body("Ba method mới giúp xử lý trường hợp chính xác 1 ngón chạm màn hình — " +
                 "hữu ích cho game point-and-click, puzzle tap, hoặc bất kỳ mechanic " +
                 "cần phân biệt \"đúng 1 ngón\" với \"nhiều ngón\":");
            CodeBlock(
                "// ── ToucherGetSingleObject() ─────────────────────────────────\n" +
                "// Trả về GameObject đang được touch nếu ĐÚNG 1 touch active.\n" +
                "// Trả về null nếu: 0 ngón, >1 ngón, hoặc 1 ngón không trúng Collider.\n" +
                "GameObject single = _input.ToucherGetSingleObject();\n\n" +
                "// ── ToucherIsSingleTouch() ────────────────────────────────────\n" +
                "// true khi đúng 1 touch đang active trên màn hình.\n" +
                "// Không quan tâm tới raycast — chỉ đếm số ngón chạm.\n" +
                "bool isSingle = _input.ToucherIsSingleTouch();\n\n" +
                "// ── ToucherClearTouchCount() ──────────────────────────────────\n" +
                "// Reset bộ đếm touch nội bộ về 0.\n" +
                "// Dùng sau khi xử lý xong 1 gesture để tránh stale count.\n" +
                "_input.ToucherClearTouchCount();");

            GUILayout.Space(8);
            Body("Ví dụ: nhấn đúng 1 ngón vào object để tương tác — bỏ qua khi đa chạm:");
            CodeBlock(
                "// ── Ví dụ: Point-and-click 1 ngón ──────────────────────────\n" +
                "void Update()\n" +
                "{\n" +
                "    // Bỏ qua nếu người chơi đang dùng 2 ngón (pinch, pan...)\n" +
                "    if (!_input.ToucherIsSingleTouch()) return;\n\n" +
                "    GameObject hit = _input.ToucherGetSingleObject();\n" +
                "    if (hit == null) return;\n\n" +
                "    var interactable = hit.GetComponent<IInteractable>();\n" +
                "    interactable?.Interact();\n" +
                "}");

            GUILayout.Space(8);
            Body("Ví dụ: dùng ClearTouchCount() sau khi hoàn thành 1 gesture cycle:");
            CodeBlock(
                "// ── Ví dụ: Reset count sau khi xử lý xong ───────────────────\n" +
                "void OnGestureComplete()\n" +
                "{\n" +
                "    // Tránh count cũ ảnh hưởng frame kế\n" +
                "    _input.ToucherClearTouchCount();\n" +
                "}");

            Tip("ToucherIsSingleTouch() chỉ đếm touch — không raycast. Kết hợp với " +
                "ToucherGetSingleObject() khi cần cả \"1 ngón\" VÀ \"trúng Collider\".");

            Sep();

            // ── SECTION 8: 2D PHYSICS ─────────────────────────────────────────
            H2("8. Game 2D — Use 2D Physics");
            Body("Với game 2D dùng Collider2D, bật Use 2D Physics trong Inspector. " +
                 "Tracker tự dùng Physics2D.Raycast thay vì Physics.Raycast:");
            CodeBlock(
                "// Trong TISInputBridge Inspector:\n" +
                "// ☑ Use 2D Physics    → tick để dùng Physics2D\n\n" +
                "// Hoặc set lúc runtime (nếu cần switch 2D/3D):\n" +
                "// (không có public method — cần thay đổi qua Inspector)\n\n" +
                "// ── Ví dụ: Chọn tile trong puzzle 2D ────────────────────────\n" +
                "[SerializeField] private PuzzleBoard _board;\n\n" +
                "void Update()\n" +
                "{\n" +
                "    int lastId = _input.ToucherGetLastID();\n" +
                "    if (lastId == 0) return;\n\n" +
                "    // WasTouched check — true nếu đã touch object này bất kỳ lúc nào\n" +
                "    // Kết hợp với frame tracking để chỉ trigger 1 lần per touch\n" +
                "    GameObject dtGO = _input.ToucherGetDoubleTappedObject();\n" +
                "    if (dtGO != null)\n" +
                "    {\n" +
                "        var tile = dtGO.GetComponent<PuzzleTile>();\n" +
                "        tile?.Rotate();\n" +
                "    }\n" +
                "}");

            Sep();

            // ── SECTION 9: USE CASE TỔNG HỢP ─────────────────────────────────
            H2("9. Use Case Tổng Hợp — Unit Selection System");
            Body("Hệ thống chọn unit hoàn chỉnh: single-tap chọn, double-tap mở menu, " +
                 "double-tap màn hình bỏ chọn:");
            CodeBlock(
                "using TrueInputSystem;\n" +
                "using UnityEngine;\n\n" +
                "public class UnitSelectionSystem : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] private UnitInfoPanel _infoPanel;\n" +
                "    [SerializeField] private UnitActionMenu _actionMenu;\n\n" +
                "    private TISInputBridge _input;\n" +
                "    private GameObject    _selectedUnit;\n" +
                "    private int           _prevLastId;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _input = TISInputBridge.Instance;\n" +
                "        // Đảm bảo tracker đang bật\n" +
                "        _input.SetObjectTrackerEnabled(true);\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        HandleDoubleTapScreen();\n" +
                "        HandleDoubleTapUnit();\n" +
                "        HandleSingleTapUnit();\n" +
                "    }\n\n" +
                "    // ── Double-tap màn hình → deselect ──────────────────────\n" +
                "    void HandleDoubleTapScreen()\n" +
                "    {\n" +
                "        if (!_input.ToucherIsDoubleTapOnScreen()) return;\n\n" +
                "        // Nếu double-tap vào object thì không deselect\n" +
                "        if (_input.ToucherGetDoubleTappedObject() != null) return;\n\n" +
                "        Deselect();\n" +
                "    }\n\n" +
                "    // ── Double-tap unit → mở action menu ────────────────────\n" +
                "    void HandleDoubleTapUnit()\n" +
                "    {\n" +
                "        GameObject dtGO = _input.ToucherGetDoubleTappedObject();\n" +
                "        if (dtGO == null) return;\n" +
                "        if (!dtGO.CompareTag(\"Unit\")) return;\n\n" +
                "        _actionMenu.OpenFor(dtGO);\n" +
                "    }\n\n" +
                "    // ── Single-tap unit → select ─────────────────────────────\n" +
                "    void HandleSingleTapUnit()\n" +
                "    {\n" +
                "        int lastId = _input.ToucherGetLastID();\n" +
                "        if (lastId == 0 || lastId == _prevLastId) return;\n" +
                "        _prevLastId = lastId;\n\n" +
                "        // Tìm GO (cần mapping từ game layer)\n" +
                "        // Cách đơn giản: dùng GetDoubleTappedObject() nếu vừa tap\n" +
                "        // Cách tốt: lưu Dictionary<int, GameObject> riêng\n" +
                "        Debug.Log($\"Selected ID: {lastId} — '{_input.ToucherGetLastName()}'\");\n" +
                "        _infoPanel.ShowForId(lastId);\n" +
                "    }\n\n" +
                "    void Deselect()\n" +
                "    {\n" +
                "        _selectedUnit = null;\n" +
                "        _infoPanel.Hide();\n" +
                "        _actionMenu.Close();\n" +
                "        _prevLastId = 0;\n" +
                "    }\n\n" +
                "    void OnDestroy()\n" +
                "    {\n" +
                "        // Không clear tracker ở đây — scene khác có thể cần\n" +
                "        // Chỉ clear khi chắc chắn không cần lịch sử nữa\n" +
                "    }\n" +
                "}");

            Sep();

            // ── SECTION 10: DEBUG VÀ LOG ───────────────────────────────────────
            H2("10. Debug — Log & Inspector");
            Body("Bật Debug Log trong Inspector để xem mọi touch event và double-tap ra Console. " +
                 "Dùng ToucherLogObjects() để in snapshot danh sách lúc runtime:");
            CodeBlock(
                "// ── Log danh sách touched objects ra Console ─────────────────\n" +
                "_input.ToucherLogObjects();\n" +
                "// Output:\n" +
                "// [TISTouchObjectTracker] Danh sách touched (3):\n" +
                "//   [0] \"Enemy_01\" (ID=12345)\n" +
                "//   [1] \"Chest_Forest\" (ID=67890)\n" +
                "//   [2] \"NPC_Merchant\" (ID=11223)\n\n" +
                "// ── Debug trong Inspector (Play Mode) ───────────────────────\n" +
                "// TISInputBridge Inspector → tab Debug → Object Tracker row:\n" +
                "// \"Object Tracker: ON  |  Touched: 3  |  Last: 'Enemy_01'\"\n\n" +
                "// ── Debug bật trong Inspector ────────────────────────────────\n" +
                "// TISInputBridge Inspector → ☑ Debug Log\n" +
                "// → mỗi touch sẽ log: [TISTouchObjectTracker] Touch → \"name\" (ID=...)\n" +
                "// → double-tap log:   [TISTouchObjectTracker] ✓ Double-tap OBJECT \"name\"");

            Sep();

            // ── SECTION 11: TIPS ──────────────────────────────────────────────
            H2("11. Tips & Lưu Ý Quan Trọng");
            CodeBlock(
                "// ✓ Tracker dùng RAYCAST — object cần có Collider (3D) hoặc Collider2D (2D)\n" +
                "//   Không hoạt động với UI elements (Canvas RectTransform)\n\n" +
                "// ✓ ToucherGetLastID() trả về InstanceID — KHÔNG phải index hay tag.\n" +
                "//   Dùng someGO.GetInstanceID() để so sánh.\n\n" +
                "// ✓ ToucherObjectIDs tích lũy vô hạn — clear định kỳ để tránh memory.\n\n" +
                "// ✓ DoubleTapOnScreen() = true ngay cả khi tap vào GameObject.\n" +
                "//   Nếu muốn phân biệt: check GetDoubleTappedObject() != null trước.\n\n" +
                "// ✓ IsDoubleTapOnObject() đọc theo InstanceID — đổi scene sẽ đổi ID.\n" +
                "//   Gọi ClearAll() khi load scene mới để tránh false positive.\n\n" +
                "// ✓ Layer Mask trong Inspector — giới hạn raycast để tối ưu performance.\n" +
                "//   Đặt đúng layer cho units, interactables thay vì để Everything.\n\n" +
                "// ✓ Use 2D Physics cần được set trước Awake của TISInputBridge.\n" +
                "//   Không có public setter lúc runtime — dùng Inspector.");

            Warn("Tracker bị tắt mặc định (Enable Object Tracker = false) vì mỗi touch " +
                 "tốn 1 Raycast. Chỉ bật khi cần thiết và đặt Layer Mask chính xác " +
                 "để giảm overhead trên thiết bị di động low-end.");
        }

    } // partial class TISDocWindow
} // namespace
#endif