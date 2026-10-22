using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using NewTouch      = UnityEngine.InputSystem.EnhancedTouch.Touch;
using NewTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Plain class theo dõi touch vào GameObject, double-tap object và double-tap màn hình.
    /// Được TISInputBridge tạo và điều khiển — không cần component riêng.
    /// </summary>
    public class TISObjectTracker
    {
        // ══════════════════════════════════════════════════════
        //  CONFIG (set bởi TISInputBridge)
        // ══════════════════════════════════════════════════════

        public Camera    RaycastCamera       { get; set; }
        public float     DoubleTapInterval   { get; set; } = 0.35f;
        public float     DoubleTapDistancePx { get; set; } = 60f;
        public LayerMask RaycastLayerMask    { get; set; } = ~0;
        public float     RaycastMaxDistance  { get; set; } = 1000f;
        public bool      Use2DPhysics        { get; set; } = false;
        public bool      DebugLog            { get; set; } = false;

        // ══════════════════════════════════════════════════════
        //  RUNTIME — TOUCHED OBJECT LIST
        // ══════════════════════════════════════════════════════

        private readonly List<int>              _touchedObjectIDs = new List<int>();
        private readonly Dictionary<int, string>     _idToName         = new Dictionary<int, string>();
        private readonly Dictionary<int, GameObject> _idToGameObject   = new Dictionary<int, GameObject>();

        // ══════════════════════════════════════════════════════
        //  RUNTIME — DOUBLE-TAP (PER OBJECT)
        // ══════════════════════════════════════════════════════

        private readonly Dictionary<int, float> _lastTapTimeByID      = new Dictionary<int, float>();
        private readonly HashSet<int>            _doubleTappedThisFrame = new HashSet<int>();

        // ══════════════════════════════════════════════════════
        //  RUNTIME — DOUBLE-TAP (SCREEN)
        // ══════════════════════════════════════════════════════

        private float   _lastScreenTapTime     = -99f;
        private Vector2 _lastScreenTapPosition = Vector2.zero;
        private bool    _screenDoubleTappedThisFrame;

        // ══════════════════════════════════════════════════════
        //  RUNTIME — SINGLE TOUCH
        // ══════════════════════════════════════════════════════
        
        /// <summary>Vị trí Vector3 (World Space) nơi vừa chạm vào GameObject ở frame này.</summary>
        private Vector3 _lastHitWorldPosition = Vector3.zero;

        /// <summary>GameObject được tap trúng trong frame này (single touch). Reset mỗi Tick.</summary>
        private GameObject _singleTouchThisFrame;

        /// <summary>true nếu có bất kỳ touch nào vào màn hình trong frame này (dù trúng hay không trúng object).</summary>
        private bool _anyTouchThisFrame;

        // ══════════════════════════════════════════════════════
        //  RUNTIME — POINTER DRAG TRACKING
        // ══════════════════════════════════════════════════════

        /// <summary>Vị trí screen (pixel) của pointer/touch đang active — cập nhật mỗi frame khi đang giữ.</summary>
        private Vector2 _currentPointerScreenPos  = Vector2.zero;

        /// <summary>Vị trí screen frame trước — dùng để tính delta drag.</summary>
        private Vector2 _previousPointerScreenPos = Vector2.zero;

        /// <summary>true khi có ít nhất 1 pointer/touch đang giữ (Moved hoặc Stationary).</summary>
        private bool _isPointerHeld;

        // ══════════════════════════════════════════════════════
        //  RUNTIME — INTERNAL TRACKING
        // ══════════════════════════════════════════════════════

        private readonly HashSet<int> _activePointerIDs = new HashSet<int>();

        // P2 FIX #9: Pre-allocated list để thay thế RemoveWhere(lambda) — không GC mỗi frame.
        private readonly List<int> _staleIds = new List<int>(8);

        // ══════════════════════════════════════════════════════
        //  PUBLIC — READ-ONLY ACCESS
        // ══════════════════════════════════════════════════════

        /// <summary>Danh sách InstanceID của tất cả GameObject đã touch trúng (mới nhất ở cuối).</summary>
        public IReadOnlyList<int> TouchedObjectIDs => _touchedObjectIDs;

        /// <summary>Số lượng touch ghi nhận (bao gồm trùng lặp nếu touch nhiều lần).</summary>
        public int TouchedCount => _touchedObjectIDs.Count;
        
        /// <summary>
        /// Trả về vị trí Vector3 (World Space) tại điểm va chạm trên GameObject ở frame hiện tại.
        /// Nếu frame này không chạm trúng GameObject nào, trả về Vector3.zero.
        /// </summary>
        public Vector3 GetTouchWorldPosition()
        {
            return _lastHitWorldPosition;
        }

        // ══════════════════════════════════════════════════════
        //  TICK — gọi bởi TISInputBridge.Update()
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Cập nhật tracker mỗi frame. TISInputBridge gọi trong Update().
        /// </summary>
        public void Tick()
        {
            // Tracker dùng EnhancedTouch. Trước đây nó "ăn theo" camera zone / joystick bật hộ;
            // giờ các control mobile đã chuyển sang uGUI pointer nên tracker tự bật (1 lần).
            EnsureEnhancedTouch();

            _doubleTappedThisFrame.Clear();
            _screenDoubleTappedThisFrame = false;
            _singleTouchThisFrame        = null;
            _anyTouchThisFrame           = false;
            _lastHitWorldPosition        = Vector3.zero;

            // Drag tracking: lưu frame trước rồi poll lại
            _previousPointerScreenPos = _currentPointerScreenPos;
            _isPointerHeld            = false;
            PollPointerPosition();

            ProcessEnhancedTouches();

            // Mouse fallback: chỉ chạy trên platform có chuột — kiểm tra runtime,
            // không dùng #if để tránh bake cứng khi build DLL.
            if (!Application.isMobilePlatform)
                ProcessMouseFallback();
        }

        // ══════════════════════════════════════════════════════
        //  INPUT PROCESSING — NEW INPUT SYSTEM (EnhancedTouch)
        // ══════════════════════════════════════════════════════

        private void ProcessEnhancedTouches()
        {
            var touches = NewTouch.activeTouches;

            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.phase != NewTouchPhase.Began) continue;
                if (_activePointerIDs.Contains(t.touchId)) continue;

                _activePointerIDs.Add(t.touchId);
                HandleTouchBegin(t.touchId, t.screenPosition);
            }

            // Xóa ID ngón đã nhấc lên
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.phase == NewTouchPhase.Ended || t.phase == NewTouchPhase.Canceled)
                    _activePointerIDs.Remove(t.touchId);
            }

            // P2 FIX #9: Thay RemoveWhere(lambda) bằng manual loop trên List tạm
            // để tránh delegate allocation mỗi frame trên Unity Mono.
            _staleIds.Clear();
            foreach (int id in _activePointerIDs)
            {
                bool found = false;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].touchId == id) { found = true; break; }
                if (!found) _staleIds.Add(id);
            }
            for (int i = 0; i < _staleIds.Count; i++)
                _activePointerIDs.Remove(_staleIds[i]);
        }

        // ══════════════════════════════════════════════════════
        //  INPUT PROCESSING — MOUSE FALLBACK (Desktop / Editor)
        //  Guard bằng runtime check (!Application.isMobilePlatform),
        //  KHÔNG dùng #if để tránh bake cứng khi build DLL.
        // ══════════════════════════════════════════════════════

        private void ProcessMouseFallback()
        {
            var mouse = TISPhysicalMouse.Current;   // chỉ chuột thật — click của virtual cursor không phải "touch"
            if (mouse == null) return;

            const int MousePointerID = -9999;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!_activePointerIDs.Contains(MousePointerID))
                {
                    _activePointerIDs.Add(MousePointerID);
                    HandleTouchBegin(MousePointerID, mouse.position.ReadValue());
                }
            }
            if (mouse.leftButton.wasReleasedThisFrame)
                _activePointerIDs.Remove(MousePointerID);
        }

        // ══════════════════════════════════════════════════════
        //  POINTER POSITION — CONTINUOUS DRAG POLLING
        //  Chạy mỗi frame trong Tick() — độc lập với HandleTouchBegin
        //  (HandleTouchBegin chỉ fire đúng frame Began)
        // ══════════════════════════════════════════════════════

        private void PollPointerPosition()
        {
            // New Input System — touch
            var activeTouches = NewTouch.activeTouches;
            if (activeTouches.Count > 0)
            {
                _currentPointerScreenPos = activeTouches[0].screenPosition;
                _isPointerHeld = true;
                return;
            }

            // Mouse fallback — desktop/editor only, kiểm tra runtime
            if (!Application.isMobilePlatform)
            {
                var mouse = TISPhysicalMouse.Current;
                if (mouse != null && mouse.leftButton.isPressed)
                {
                    _currentPointerScreenPos = mouse.position.ReadValue();
                    _isPointerHeld = true;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  CORE LOGIC
        // ══════════════════════════════════════════════════════

        private void HandleTouchBegin(int pointerID, Vector2 screenPos)
        {
            float now = Time.unscaledTime;

            // ── Ghi nhận có touch vào màn hình frame này ─────
            _anyTouchThisFrame = true;

            // ── Double-tap toàn màn hình ─────────────────────
            float screenDeltaTime = now - _lastScreenTapTime;
            float screenDist      = Vector2.Distance(screenPos, _lastScreenTapPosition);

            if (screenDeltaTime <= DoubleTapInterval && screenDist <= DoubleTapDistancePx)
            {
                _screenDoubleTappedThisFrame = true;
                _lastScreenTapTime = -99f; // reset để không fire lại
                if (DebugLog)
                    Debug.Log($"[TISTouchObjectTracker] ✓ Double-tap SCREEN at {screenPos:F0}");
            }
            else
            {
                _lastScreenTapTime     = now;
                _lastScreenTapPosition = screenPos;
            }

            // ── Raycast tìm GameObject ───────────────────────
            int hitID = RaycastGetInstanceID(screenPos, out string hitName, out GameObject hitGO, out Vector3 hitPoint);
            if (hitID == 0) return;

            _lastHitWorldPosition = hitPoint;

            _touchedObjectIDs.Add(hitID);
            _idToName[hitID]       = hitName;
            _idToGameObject[hitID] = hitGO;

            // ── Single touch object frame này ────────────────
            _singleTouchThisFrame = hitGO;

            if (DebugLog)
                Debug.Log($"[TISTouchObjectTracker] Touch → \"{hitName}\" (ID={hitID})");

            // ── Double-tap theo Object ───────────────────────
            if (_lastTapTimeByID.TryGetValue(hitID, out float lastTime))
            {
                float delta = now - lastTime;
                if (delta <= DoubleTapInterval)
                {
                    _doubleTappedThisFrame.Add(hitID);
                    _lastTapTimeByID[hitID] = -99f; // reset
                    if (DebugLog)
                        Debug.Log($"[TISTouchObjectTracker] ✓ Double-tap OBJECT \"{hitName}\" (ID={hitID})");
                    return;
                }
            }

            _lastTapTimeByID[hitID] = now;
        }

        // ══════════════════════════════════════════════════════
        //  RAYCAST
        // ══════════════════════════════════════════════════════

        private int RaycastGetInstanceID(Vector2 screenPos, out string hitName, out GameObject hitGO, out Vector3 hitPoint)
        {
            hitName = "";
            hitGO   = null;
            hitPoint = Vector3.zero;

            var cam = RaycastCamera != null ? RaycastCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[TISTouchObjectTracker] Không có Camera! Assign camera qua TISInputBridge.");
                return 0;
            }

            if (Use2DPhysics)
            {
                Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
                var hit2D = Physics2D.Raycast(worldPos, Vector2.zero, RaycastMaxDistance, RaycastLayerMask);
                if (hit2D.collider == null) return 0;
                var go2D = hit2D.collider.gameObject;
                hitName = go2D.name;
                hitGO   = go2D;
                hitPoint = hit2D.point;
                return go2D.GetInstanceID();
            }
            else
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                if (!Physics.Raycast(ray, out RaycastHit hit, RaycastMaxDistance, RaycastLayerMask))
                    return 0;
                var go = hit.collider.gameObject;
                hitName = go.name;
                hitGO   = go;
                hitPoint = hit.point;
                return go.GetInstanceID();
            }
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — TOUCHED OBJECT LIST
        // ══════════════════════════════════════════════════════

        /// <summary>InstanceID của GameObject được touch trúng cuối cùng. 0 nếu chưa có.</summary>
        public int GetLastTouchedID()
        {
            if (_touchedObjectIDs.Count == 0) return 0;
            return _touchedObjectIDs[_touchedObjectIDs.Count - 1];
        }

        /// <summary>Tên GameObject được touch trúng cuối cùng (debug). Rỗng nếu chưa có.</summary>
        public string GetLastTouchedName()
        {
            int id = GetLastTouchedID();
            if (id == 0) return "";
            return _idToName.TryGetValue(id, out string name) ? name : "";
        }

        /// <summary>true nếu chưa touch trúng object nào.</summary>
        public bool HasNoTouchedObjects() => _touchedObjectIDs.Count == 0;

        /// <summary>Xóa danh sách touched objects. Gọi khi chuyển scene / reset.</summary>
        public void ClearTouchedList()
        {
            _touchedObjectIDs.Clear();
            _idToName.Clear();
            _idToGameObject.Clear();
            if (DebugLog)
                Debug.Log("[TISTouchObjectTracker] ✓ Danh sách touched objects đã được xóa.");
        }

        /// <summary>Xóa toàn bộ trạng thái: danh sách, double-tap, màn hình.</summary>
        private bool _enhancedTouchEnabledByTracker;

        private void EnsureEnhancedTouch()
        {
            if (_enhancedTouchEnabledByTracker) return;
            EnhancedTouchSupport.Enable();
            _enhancedTouchEnabledByTracker = true;
        }

        /// <summary>Trả lại EnhancedTouch đã bật — TISInputBridge gọi khi bị huỷ.</summary>
        public void Dispose()
        {
            if (!_enhancedTouchEnabledByTracker) return;
            EnhancedTouchSupport.Disable();
            _enhancedTouchEnabledByTracker = false;
        }

        public void ClearAll()
        {
            ClearTouchedList();
            _lastTapTimeByID.Clear();
            _doubleTappedThisFrame.Clear();
            _lastScreenTapTime           = -99f;
            _lastScreenTapPosition       = Vector2.zero;
            _screenDoubleTappedThisFrame = false;
            _activePointerIDs.Clear();
            if (DebugLog)
                Debug.Log("[TISTouchObjectTracker] ✓ Toàn bộ trạng thái đã được xóa.");
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — DOUBLE-TAP
        // ══════════════════════════════════════════════════════

        /// <summary>true đúng 1 frame khi double-tap vào GameObject có InstanceID cho trước.</summary>
        public bool IsDoubleTapOnObject(int instanceID)
        {
            bool result = _doubleTappedThisFrame.Contains(instanceID);
            return  result;
        }

        /// <summary>true đúng 1 frame khi double-tap bất kỳ vị trí nào trên màn hình.</summary>
        public bool IsDoubleTapOnScreen()
            => _screenDoubleTappedThisFrame;

        /// <summary>
        /// GameObject bị double-tap trong frame này. null nếu không có.
        /// Nếu nhiều object cùng bị double-tap, trả về object đầu tiên tìm được.
        /// </summary>
        public GameObject GetDoubleTappedObject()
        {
            foreach (int id in _doubleTappedThisFrame)
            {
                if (_idToGameObject.TryGetValue(id, out GameObject go) && go != null)
                    return go;
            }
            return null;
        }

        /// <summary>
        /// GameObject bị double-tap khớp instanceID. null nếu không bị double-tap frame này.
        /// </summary>
        public GameObject GetDoubleTappedObject(int instanceID)
        {
            if (!_doubleTappedThisFrame.Contains(instanceID)) return null;
            return _idToGameObject.TryGetValue(instanceID, out GameObject go) ? go : null;
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — CONVENIENCE
        // ══════════════════════════════════════════════════════

        /// <summary>true nếu GameObject có instanceID đã từng được touch trúng.</summary>
        public bool WasTouched(int instanceID)
            => _touchedObjectIDs.Contains(instanceID);

        /// <summary>Xóa lịch sử double-tap của một object cụ thể.</summary>
        public void ResetDoubleTapForObject(int instanceID)
        {
            _lastTapTimeByID.Remove(instanceID);
            _doubleTappedThisFrame.Remove(instanceID);
        }

        /// <summary>Đặt lại trạng thái double-tap màn hình.</summary>
        public void ResetDoubleTapScreen()
        {
            _lastScreenTapTime           = -99f;
            _screenDoubleTappedThisFrame = false;
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — SINGLE TOUCH
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// true nếu có bất kỳ touch nào vào màn hình trong frame này,
        /// dù có trúng GameObject hay không.
        /// Tương đương kiểm tra "đã tap vào màn hình chưa".
        /// </summary>
        public bool IsSingleTouchOnScreen()
            => _anyTouchThisFrame;

        /// <summary>
        /// GameObject được tap trúng trong frame này (single touch).
        /// null nếu không có touch hoặc không trúng object nào.
        /// Chỉ có giá trị đúng 1 frame sau khi tap.
        /// </summary>
        public GameObject GetSingleTouchedObject()
            => _singleTouchThisFrame;

        /// <summary>
        /// Xóa bộ đếm (danh sách) touched objects.
        /// Dùng khi muốn reset count mà không cần reset toàn bộ trạng thái.
        /// Alias rõ nghĩa hơn cho ClearTouchedList().
        /// </summary>
        public void ClearTouchCount()
            => ClearTouchedList();

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — DRAG POSITION
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// [API 1 — UI Drag] Vị trí screen space (pixel) của pointer/touch đang giữ.
        /// Cập nhật liên tục mỗi frame — dùng trực tiếp để di chuyển RectTransform khi kéo item trong Canvas.
        /// Trả về Vector2.zero nếu không có pointer đang active.
        /// </summary>
        public Vector2 GetPointerScreenPosition()
            => _isPointerHeld ? _currentPointerScreenPos : Vector2.zero;

        /// <summary>
        /// Delta vị trí screen (pixel) so với frame trước.
        /// Dùng để tính tốc độ hoặc offset kéo trong UI mà không cần tự lưu vị trí cũ.
        /// Trả về Vector2.zero nếu không có pointer đang active.
        /// </summary>
        public Vector2 GetPointerScreenDelta()
            => _isPointerHeld
                ? _currentPointerScreenPos - _previousPointerScreenPos
                : Vector2.zero;

        /// <summary>true khi có pointer/touch đang được giữ (drag đang diễn ra).</summary>
        public bool IsPointerHeld => _isPointerHeld;

        /// <summary>
        /// [API 2 — World Drag] Vị trí World Space tương ứng với pointer/touch đang giữ.
        /// Dùng để kéo thả hoặc vứt GameObject trong không gian 3D hoặc 2D.
        ///
        /// Cách truyền depth:
        ///   3D — truyền khoảng cách từ camera đến mặt phẳng muốn đặt object:
        ///        float depth = Vector3.Distance(cam.transform.position, dragTarget.position);
        ///
        ///   2D (Orthographic) — truyền |camera.z| để giữ object trên mặt phẳng XY:
        ///        float depth = Mathf.Abs(Camera.main.transform.position.z);
        ///
        /// Trả về Vector3.zero nếu không có pointer active hoặc không tìm được Camera.
        /// </summary>
        /// <param name="depth">Khoảng cách từ camera đến mặt phẳng đặt object (đơn vị: Unity unit).</param>
        public Vector3 GetPointerWorldPosition(float depth = 10f)
        {
            if (!_isPointerHeld) return Vector3.zero;

            var cam = RaycastCamera != null ? RaycastCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[TISObjectTracker] GetPointerWorldPosition: Không tìm được Camera.");
                return Vector3.zero;
            }

            return cam.ScreenToWorldPoint(new Vector3(
                _currentPointerScreenPos.x,
                _currentPointerScreenPos.y,
                depth
            ));
        }

        // ══════════════════════════════════════════════════════
        //  DEBUG
        // ══════════════════════════════════════════════════════

        public void LogTouchedObjects()
        {
            if (_touchedObjectIDs.Count == 0)
            {
                Debug.Log("[TISTouchObjectTracker] Chưa có object nào được touch.");
                return;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[TISTouchObjectTracker] Danh sách touched ({_touchedObjectIDs.Count}):");
            for (int i = 0; i < _touchedObjectIDs.Count; i++)
            {
                int id = _touchedObjectIDs[i];
                string name = _idToName.TryGetValue(id, out string n) ? n : "?";
                sb.AppendLine($"  [{i}] \"{name}\" (ID={id})");
            }
            Debug.Log(sb.ToString());
        }
    }
}