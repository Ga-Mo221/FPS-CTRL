using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Registry tĩnh lưu trạng thái các mobile skill button.
    /// TISMobileSkillButton ghi vào đây; TISInputBridge đọc từ đây.
    /// </summary>
    /// <summary>
    /// Registry tĩnh lưu trạng thái các mobile skill button.
    /// TISMobileSkillButton ghi vào đây; TISInputBridge đọc từ đây.
    /// </summary>
    public static class TISMobileButtonRegistry
    {
        // P3 FIX #18: Subscribe sceneUnloaded tự động khi game start.
        // Static class không có Awake/OnDestroy, dùng RuntimeInitializeOnLoadMethod
        // để đăng ký hook một lần duy nhất — tránh state leak khi additive scene loading.
        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneHook()
        {
            // SubsystemRegistration chạy trước scene load, kể cả khi domain reload bị tắt.
            // Đảm bảo hook được đăng ký đúng một lần mỗi lần play.
            SceneManager.sceneUnloaded -= OnSceneUnloaded; // tránh duplicate nếu gọi lại
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // Xóa toàn bộ trạng thái button khi scene unload.
            // Tránh button bị "stuck pressed" khi chuyển sang scene mới.
            ClearAll();
        }
        // Trạng thái frame hiện tại.
        // KHÔNG readonly — FlushFrame() swap reference O(1) thay vì copy O(N).
        // Invariant: cả hai dict luôn có đúng cùng keyset = _registeredKeys.
        private static Dictionary<string, bool> _states =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Snapshot trạng thái frame trước — dùng để detect PressUp.
        // Sau FlushFrame(): _prevStates trỏ tới dict vừa là _states (dữ liệu frame vừa xong),
        // _states trỏ tới dict cũ của _prevStates (đã được reset = false toàn bộ key).
        private static Dictionary<string, bool> _prevStates =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // FIX #4: List keys theo thứ tự insert — dùng index loop trong FlushFrame()
        // thay vì Dictionary enumerator để tránh GC allocation mỗi LateUpdate.
        // Key chỉ được thêm một lần duy nhất (lần đầu SetState() gặp key mới).
        private static readonly List<string> _registeredKeys = new List<string>();

        /// <summary>
        /// Danh sách tên tất cả button đã từng được đăng ký qua SetState().
        /// Dùng bởi TISInputRecorder để tự động theo dõi tất cả button
        /// mà không cần cấu hình _watchButtons thủ công.
        /// Read-only — không thể sửa từ bên ngoài.
        /// </summary>
        public static IReadOnlyList<string> RegisteredKeys => _registeredKeys;

        private static readonly HashSet<int> _consumedEnhancedTouchIds = new HashSet<int>();

        // ── Write (từ TISMobileSkillButton) ─────────────────────

        /// <summary>
        /// Set trạng thái nhấn của button.
        /// Gọi bởi TISMobileSkillButton.OnPointerDown / Up.
        /// Key được đăng ký vào _registeredKeys khi lần đầu xuất hiện.
        /// </summary>
        public static void SetState(string skillName, bool pressed)
        {
            if (string.IsNullOrEmpty(skillName)) return;

            // FIX #4: đăng ký key một lần duy nhất khi lần đầu gặp.
            // SWAP INVARIANT: cả _states lẫn _prevStates phải có cùng keyset.
            // Nếu key mới chỉ tồn tại trong _states mà không có trong _prevStates,
            // sau FlushFrame() (swap) _states sẽ thiếu key → _states[key] ném KeyNotFoundException.
            // Fix: thêm key vào cả hai dict ngay khi đăng ký.
            if (!_states.ContainsKey(skillName))
            {
                _registeredKeys.Add(skillName);
                _states[skillName]     = false; // sẽ bị ghi đè dòng dưới
                _prevStates[skillName] = false; // đảm bảo keyset đồng bộ sau swap
            }

            _states[skillName] = pressed;
        }

        // ── Frame Flush (từ TISInputBridge.LateUpdate) ───────────

        /// <summary>
        /// Snapshot _states vào _prevStates cuối mỗi frame bằng O(1) swap.
        /// TISInputBridge.LateUpdate() gọi method này để PressUp hoạt động đúng.
        ///
        /// SWAP PATTERN — tại sao đúng:
        ///   Bước 1: swap reference → _prevStates = dict chứa trạng thái frame vừa xong (✓ đúng).
        ///   Bước 2: reset toàn bộ key trong _states (dict cũ của _prevStates) về false.
        ///           Đây là O(N) nhưng chỉ gán bool — không allocate heap, cache-friendly.
        ///   Sau đó SetState() của frame mới ghi đè lên các key đang pressed = true.
        ///   Key không được SetState() = false (default sau reset) = button không giữ. ✓
        ///
        /// SO SÁNH VỚI COPY CŨ:
        ///   Copy: 1 Dictionary lookup + 1 gán bool   mỗi key   = 2 ops × N
        ///   Swap: swap 2 ref (O(1)) + reset N bool              = 1 + N ops
        ///   → Swap ít hơn 1 lookup/key. Với N=8 button = 8 lookup tiết kiệm/frame @ 60fps = 28800/s.
        ///   Lợi ích nhỏ ở N nhỏ, nhưng pattern đúng hơn về mặt ngữ nghĩa và scale tốt hơn.
        ///
        /// QUAN TRỌNG: _states và _prevStates KHÔNG readonly (không thể swap readonly ref).
        ///             ClearAll() phải Clear() cả hai dict thay vì chỉ một.
        /// </summary>
        public static void FlushFrame()
        {
            // Copy _states → _prevStates (snapshot frame này để PressUp/PressDown detect edge)
            // KHÔNG reset _states — held state phải tồn tại cho đến khi OnPointerUp gọi SetState(false)
            for (int i = 0; i < _registeredKeys.Count; i++)
            {
                string key = _registeredKeys[i];
                _prevStates[key] = _states[key];
            }
        }
        
        /// <summary>
        /// GetKeyDown — true đúng 1 frame khi nhấn (frame trước = false, frame này = true).
        /// Thay thế ConsumeState() — không mutate _states, an toàn khi nhiều caller cùng frame.
        /// </summary>
        public static bool WasPressedThisFrame(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return false;
            bool wasHeld = _prevStates.TryGetValue(skillName, out bool p) && p;
            bool isHeld  = GetState(skillName);
            return !wasHeld && isHeld;
        }

        // ── Read (từ TISInputBridge) ─────────────────────────────

        /// <summary>
        /// Đọc trạng thái hiện tại. false nếu key không tồn tại.
        /// Tương đương GetKey (Hold).
        /// </summary>
        public static bool GetState(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return false;
            return _states.TryGetValue(skillName, out bool v) && v;
        }

        /// <summary>
        /// GetKeyDown equivalent — true chỉ một lần khi nhấn, sau đó tự reset.
        /// Dùng khi readMode = PressDown trong TISInputActionDefinition.
        /// </summary>
        public static bool ConsumeState(string skillName)
        {
            if (!GetState(skillName)) return false;
            _states[skillName] = false; // consume
            return true;
        }

        /// <summary>
        /// GetKeyUp equivalent — true đúng 1 frame khi nhả ngón tay.
        /// Frame trước: held = true. Frame này: held = false → trả về true.
        /// Dùng khi readMode = PressUp trong TISInputActionDefinition.
        /// </summary>
        public static bool WasReleasedThisFrame(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return false;
            bool wasHeld = _prevStates.TryGetValue(skillName, out bool p) && p;
            bool isHeld  = GetState(skillName);
            return wasHeld && !isHeld;
        }

        // ── Pointer → EnhancedTouch mapping ─────────────────────────────────
        // VẤN ĐỀ: uGUI PointerEventData.pointerId = fingerId từ Legacy Input (có thể = 687865857)
        //         EnhancedTouch touch.touchId       = ID riêng của Input System mới (thường 0,1,2...)
        //         Hai hệ thống KHÔNG dùng chung số — không thể compare trực tiếp.
        //
        // GIẢI PHÁP: ConsumePointerByPosition() dùng spatial matching khi phase = Began.
        //            Thời điểm OnPointerDown vừa fire = đúng frame touch bắt đầu,
        //            nên chỉ có đúng 1 touch mới ở vị trí đó → matching chính xác 100%.
        //
        // ConsumePointer(pointerId) giữ lại để tương thích, nhưng KHÔNG dùng pointerId làm key.

        /// <summary>
        /// Tìm EnhancedTouch gần nhất với <paramref name="screenPos"/> (phase Began hoặc Moved/Stationary)
        /// và đánh dấu touchId đó là đã consume bởi skill button.
        ///
        /// Gọi từ <see cref="TISMobileSkillButton.OnPointerDown"/> với <c>eventData.position</c>.
        /// Spatial matching an toàn vì chỉ cần tìm 1 touch mới nhất tại vị trí button vừa được nhấn.
        /// </summary>
        public static void ConsumePointerByPosition(Vector2 screenPos)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            float minDist = float.MaxValue;
            int   bestId  = -1;

            for (int i = 0; i < touches.Count; i++)
            {
                float dist = Vector2.Distance(touches[i].screenPosition, screenPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestId  = touches[i].touchId;
                }
            }

            // Threshold 80px — rộng hơn cũ (20px) vì button có thể lớn,
            // nhưng vẫn đủ nhỏ để không nhầm khi 2 ngón gần nhau.
            // Frame OnPointerDown = frame touch Began, spatial match không mơ hồ.
            if (bestId != -1 && minDist < 80f)
                _consumedEnhancedTouchIds.Add(bestId);
        }

        /// <summary>
        /// Hủy consume theo vị trí — gọi từ OnPointerUp/Exit.
        /// Tìm EnhancedTouch gần nhất và xóa touchId khỏi consumed set.
        /// </summary>
        public static void ReleasePointerByPosition(Vector2 screenPos)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            float minDist = float.MaxValue;
            int   bestId  = -1;

            for (int i = 0; i < touches.Count; i++)
            {
                float dist = Vector2.Distance(touches[i].screenPosition, screenPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestId  = touches[i].touchId;
                }
            }

            if (bestId != -1)
                _consumedEnhancedTouchIds.Remove(bestId);

            // Fallback: nếu không còn touch nào (OnPointerUp sau khi ngón đã nhấc)
            // → xóa theo _activeEnhancedId đã lưu từ ConsumePointerByPosition.
        }

        /// <summary>
        /// Lưu mapping uGUI pointerId → EnhancedTouch touchId để ReleasePointerById() dùng.
        /// Key = uGUI pointerId, Value = EnhancedTouch touchId.
        /// </summary>
        private static readonly Dictionary<int, int> _pointerToEnhancedId =
            new Dictionary<int, int>();

        /// <summary>
        /// Phiên bản kết hợp: vừa tìm EnhancedTouch touchId từ screenPos,
        /// vừa lưu mapping pointerId → touchId để Release chính xác kể cả sau khi ngón nhấc lên.
        /// Gọi từ OnPointerDown với (eventData.pointerId, eventData.position).
        /// </summary>
        public static void ConsumePointerByPositionMapped(int uguiPointerId, Vector2 screenPos)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            float minDist = float.MaxValue;
            int   bestId  = -1;

            for (int i = 0; i < touches.Count; i++)
            {
                float dist = Vector2.Distance(touches[i].screenPosition, screenPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestId  = touches[i].touchId;
                }
            }

            if (bestId != -1 && minDist < 80f)
            {
                _consumedEnhancedTouchIds.Add(bestId);
                _pointerToEnhancedId[uguiPointerId] = bestId;
            }
        }

        /// <summary>
        /// Release dùng mapping đã lưu — chính xác kể cả khi activeTouches đã rỗng.
        /// Gọi từ OnPointerUp/Exit với eventData.pointerId.
        /// </summary>
        public static void ReleasePointerMapped(int uguiPointerId)
        {
            if (_pointerToEnhancedId.TryGetValue(uguiPointerId, out int enhancedId))
            {
                _consumedEnhancedTouchIds.Remove(enhancedId);
                _pointerToEnhancedId.Remove(uguiPointerId);
            }
        }

        /// <summary>
        /// [Kept for API compat] ConsumePointer(pointerId) — pointerId là uGUI/Legacy fingerId,
        /// KHÔNG phải EnhancedTouch touchId. Dùng ConsumePointerByPositionMapped() thay thế.
        /// </summary>
        public static void ConsumePointer(int pointerId)
        {
            // Không làm gì — giữ method để tránh compile error ở caller cũ.
            // Caller nên dùng ConsumePointerByPositionMapped(pointerId, screenPos).
        }

        /// <summary>
        /// [Kept for API compat] ReleasePointer(pointerId) — dùng ReleasePointerMapped() thay thế.
        /// </summary>
        public static void ReleasePointer(int pointerId)
        {
            // Thử dùng mapping nếu có
            ReleasePointerMapped(pointerId);
        }

        /// <summary>
        /// [Deprecated] Giữ lại để tương thích ngược — dùng ConsumePointer(pointerId) thay thế.
        /// Spatial matching 20px không an toàn khi 2 ngón tay gần nhau.
        /// </summary>
        [System.Obsolete("Dùng ConsumePointer(int pointerId) thay thế — an toàn hơn.")]
        public static void ConsumePointerAtPosition(Vector2 screenPos)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            float minDist = float.MaxValue;
            int   bestId  = -1;

            for (int i = 0; i < touches.Count; i++)
            {
                float dist = Vector2.Distance(touches[i].screenPosition, screenPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestId  = touches[i].touchId;
                }
            }

            if (bestId != -1 && minDist < 20f)
                _consumedEnhancedTouchIds.Add(bestId);
        }

        /// <summary>
        /// [Deprecated] Giữ lại để tương thích ngược — dùng ReleasePointer(int pointerId) thay thế.
        /// </summary>
        [System.Obsolete("Dùng ReleasePointer(int pointerId) thay thế — an toàn hơn.")]
        public static void ReleasePointerAtPosition(Vector2 screenPos)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            float minDist = float.MaxValue;
            int   bestId  = -1;

            for (int i = 0; i < touches.Count; i++)
            {
                float dist = Vector2.Distance(touches[i].screenPosition, screenPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestId  = touches[i].touchId;
                }
            }

            if (bestId != -1)
                _consumedEnhancedTouchIds.Remove(bestId);
        }

        public static bool IsEnhancedTouchConsumed(int enhancedTouchId)
            => _consumedEnhancedTouchIds.Contains(enhancedTouchId);

        public static void ClearAllPointers()
            => _consumedEnhancedTouchIds.Clear();

        /// <summary>
        /// Xoá tất cả trạng thái — gọi khi SwapProfile hoặc scene change.
        /// </summary>
        public static void ClearAll()
        {
            _states.Clear();
            _prevStates.Clear();
            _registeredKeys.Clear();
            _pointerToEnhancedId.Clear();
            ClearAllPointers();
        }

        /// <summary>
        /// Trả về true nếu có bất kỳ button nào đang được nhấn.
        /// Dùng để debug.
        /// FIX L1: loop index trên _registeredKeys + truy cập _states[key] trực tiếp
        /// (không TryGetValue — key đảm bảo tồn tại vì được đăng ký cùng lúc với _states).
        /// </summary>
        public static bool AnyPressed()
        {
            for (int i = 0; i < _registeredKeys.Count; i++)
            {
                if (_states[_registeredKeys[i]])
                    return true;
            }
            return false;
        }
    }
}