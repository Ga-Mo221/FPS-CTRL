// ============================================================
//  TrueInputSystem — TISActionReader.cs
//
//  P3 FIX #15: Tách ra từ TISInputBridge.
//  Trách nhiệm DUY NHẤT:
//    - Cache và đọc skill actions (Button / Vector2 / Axis)
//    - Xử lý analog trigger state per-frame (P1 FIX #3)
//    - Đọc qua TISMobileButtonRegistry trên mobile
//    - Flush trigger buffer cuối frame (gọi từ LateUpdate Bridge)
//
//  TISInputBridge giữ một instance TISActionReader và delegate
//  mọi lời gọi GetSkillAction / GetVector2Action / GetAxisAction sang đây.
//  TISActionReader không biết đến TISInputBridge.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Đọc và cache tất cả Input Actions. Plain class — không phải MonoBehaviour.
    /// </summary>
    public sealed class TISActionReader
    {
        // ══════════════════════════════════════════════════════
        //  INNER TYPES
        // ══════════════════════════════════════════════════════

        internal struct CachedAction
        {
            public InputAction       action;
            public TISButtonReadMode readMode;
            public bool              isAnalogTrigger;
            public string            mobileBinding;
            public int               triggerIndex;
        }

        /// <summary>
        /// Kết quả tra cứu đã resolve cho 1 tên action (1 slot / tên, không phân biệt hoa thường).
        /// Hợp lệ khi *Version == _cacheVersion; mọi thay đổi 3 cache bên dưới đều tăng
        /// _cacheVersion → slot tự resolve lại → kết quả luôn giống hệt đường tra bằng string.
        /// </summary>
        internal sealed class ActionSlot
        {
            public readonly string name;
            public int          skillVersion, v2Version, axisVersion;
            public CachedAction skill;
            public InputAction  v2;
            public InputAction  axis;
            public bool         axisIsTrigger;
            public ActionSlot(string name) => this.name = name;
        }

        /// <summary>So sánh tham chiếu — string literal được intern nên cùng 1 instance mỗi lần gọi.</summary>
        private sealed class RefComparer : IEqualityComparer<string>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public bool Equals(string a, string b) => ReferenceEquals(a, b);
            public int  GetHashCode(string s)      => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
        }

        // ══════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════

        private InputActionAsset      _asset;
        private TISInputProfileConfig _activeProfile;

        // Common actions — cached once per profile load
        private InputAction _moveAction;
        private InputAction _lookAction;
        private bool        _lookActionIsMouseDelta;

        // Skill / button action cache
        private readonly Dictionary<string, CachedAction> _skillCache =
            new Dictionary<string, CachedAction>(StringComparer.OrdinalIgnoreCase);

        // Vector2 / Axis action caches (P2 FIX #6)
        private readonly Dictionary<string, InputAction> _v2Cache =
            new Dictionary<string, InputAction>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InputAction> _axisCache =
            new Dictionary<string, InputAction>(StringComparer.OrdinalIgnoreCase);

        // Slot cache cho TISActionHandle và đường string nhanh. Slot sống qua Load() để handle
        // không bao giờ hỏng; dữ liệu trong slot được làm mới theo _cacheVersion.
        private readonly Dictionary<string, ActionSlot> _slotByName =
            new Dictionary<string, ActionSlot>(StringComparer.OrdinalIgnoreCase);
        // Tra theo tham chiếu (rẻ hơn băm OrdinalIgnoreCase); giới hạn kích thước để
        // string tạo động mỗi frame (vd. $"Skill{i}") không làm phình bộ nhớ.
        private readonly Dictionary<string, ActionSlot> _slotByRef =
            new Dictionary<string, ActionSlot>(RefComparer.Instance);
        private const int MaxRefSlots = 256;
        private int _cacheVersion = 1;

        // Trigger per-frame state (P1 FIX #3)
        private readonly bool[] _triggerHeldLastFrame = new bool[TISConstants.MaxAnalogTriggers];
        private readonly bool[] _triggerHeldThisFrame = new bool[TISConstants.MaxAnalogTriggers];
        private readonly InputAction[] _triggerActions = new InputAction[TISConstants.MaxAnalogTriggers];
        private int _triggerCount;

        // P2 FIX #8: cached once, refreshed on Load() and when simulateMobile changes
        private bool _isMobileMode;

        // Debug flag — set from TISInputBridge inspector field
        private bool _debugSkillAction;

        // ══════════════════════════════════════════════════════
        //  PUBLIC SETUP
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// (Re)initialise reader với asset và profile mới.
        /// Gọi sau mỗi lần build profile hoặc swap profile.
        /// </summary>
        public void Load(
            InputActionAsset asset,
            TISInputProfileConfig activeProfile,
            bool isMobileMode,
            bool debugSkillAction)
        {
            _asset           = asset;
            _activeProfile   = activeProfile;
            _isMobileMode    = isMobileMode;
            _debugSkillAction = debugSkillAction;

            ClearCaches();
            BuildCommonActionCache();
            BuildSkillCache();
        }

        /// <summary>Cập nhật _isMobileMode khi SimulateMobile thay đổi lúc runtime (Editor only).</summary>
        public void SetMobileMode(bool isMobileMode) => _isMobileMode = isMobileMode;

        /// <summary>
        /// Gọi ĐẦU mỗi frame từ TISInputBridge.Update() (ExecutionOrder -100, trước script game):
        /// lấy mẫu trạng thái giữ của MỌI analog trigger đã cache.
        /// Trước đây trạng thái chỉ cập nhật khi có code gọi GetSkillAction() → game đọc
        /// không đều (chỉ theo tick Fusion, chỉ khi GameStarted...) làm PressDown bắn đôi / bị mất.
        /// </summary>
        public void SampleTriggers()
        {
            for (int i = 0; i < _triggerCount; i++)
            {
                _triggerHeldLastFrame[i] = _triggerHeldThisFrame[i];
                var a = _triggerActions[i];
                _triggerHeldThisFrame[i] = a != null && a.ReadValue<float>() >= TISConstants.TriggerPressPoint;
            }
        }

        /// <summary>Giữ để tương thích — trigger giờ được lấy mẫu trong <see cref="SampleTriggers"/>.</summary>
        [Obsolete("Trigger được lấy mẫu đầu frame trong SampleTriggers(); không cần gọi nữa.")]
        public void SnapshotTriggerState() { }

        // ══════════════════════════════════════════════════════
        //  COMMON ACTION PROPERTIES
        // ══════════════════════════════════════════════════════

        public InputAction MoveAction              => _moveAction;
        public InputAction LookAction              => _lookAction;
        public bool        LookActionIsMouseDelta  => _lookActionIsMouseDelta;

        // ══════════════════════════════════════════════════════
        //  SKILL ACTIONS (Button)
        // ══════════════════════════════════════════════════════

        public bool GetSkillAction(string actionName)
            => SkillDefaultMode(actionName, GetSlot(actionName));

        public bool GetSkillAction(string actionName, TISButtonReadMode mode)
            => SkillWithMode(actionName, mode, GetSlot(actionName));

        private bool SkillDefaultMode(string actionName, ActionSlot slot)
        {
            if (slot != null && slot.skillVersion == _cacheVersion)
                return EvalCachedAction(actionName, slot.skill.readMode, in slot.skill);

            if (_skillCache.TryGetValue(actionName, out var ca))
            {
                RememberSkill(slot, in ca);
                return EvalCachedAction(actionName, ca.readMode, in ca);
            }

            var def  = _activeProfile?.FindAction(actionName);
            var mode = def?.readMode ?? TISButtonReadMode.PressDown;
            return SkillWithMode(actionName, mode, slot);
        }

        private bool SkillWithMode(string actionName, TISButtonReadMode mode, ActionSlot slot)
        {
            if (slot != null && slot.skillVersion == _cacheVersion)
                return EvalCachedAction(actionName, mode, in slot.skill);

            if (_skillCache.TryGetValue(actionName, out var ca))
            {
                RememberSkill(slot, in ca);
                return EvalCachedAction(actionName, mode, in ca);
            }

            // Lazy-build cache entry for unknown action name
            var found = _asset?.FindAction(actionName, throwIfNotFound: false);
            var def   = _activeProfile?.FindAction(actionName);

            if (found != null || def != null)
            {
                bool isAnalogTrigger = found != null && IsAnalogTriggerAction(found);
                var newCa = new CachedAction
                {
                    action          = found,
                    readMode        = mode,
                    isAnalogTrigger = isAnalogTrigger,
                    mobileBinding   = def?.bindingMobile ?? "",
                    triggerIndex    = isAnalogTrigger ? RegisterTrigger(found) : -1,
                };
                _skillCache[actionName] = newCa;
                _cacheVersion++;
                RememberSkill(slot, in newCa);
                return EvalCachedAction(actionName, mode, in newCa);
            }

            // Cache "không tồn tại" → cảnh báo 1 lần; các lần gọi sau (mỗi frame) trả false ngay,
            // không tạo string + Debug.LogWarning mỗi frame (rất tốn trên mobile).
            // Cache được xoá khi Load() (đổi / swap profile) → tên hợp lệ sau đó vẫn hoạt động.
            var missing = new CachedAction
            {
                action        = null,
                readMode      = mode,
                mobileBinding = "",
                triggerIndex  = -1,
            };
            _skillCache[actionName] = missing;
            _cacheVersion++;
            RememberSkill(slot, in missing);
            Debug.LogWarning(
                $"[TISActionReader] GetSkillAction: không tìm thấy \"{actionName}\".\n" +
                $"→ Kiểm tra tên trong TISInputProfileConfig.asset.");
            return false;
        }

        private void RememberSkill(ActionSlot slot, in CachedAction ca)
        {
            if (slot == null) return;
            slot.skill        = ca;
            slot.skillVersion = _cacheVersion;
        }

        // ══════════════════════════════════════════════════════
        //  VECTOR2 / AXIS ACTIONS
        // ══════════════════════════════════════════════════════

        public Vector2 GetVector2Action(string actionName)
            => Vector2Core(actionName, GetSlot(actionName));

        public float GetAxisAction(string actionName)
            => AxisCore(actionName, GetSlot(actionName));

        private Vector2 Vector2Core(string actionName, ActionSlot slot)
        {
            // P3: TISDirectionalSkillButton có thể override action này real-time
            if (TISDirectionalRegistry.TryGetVector2Override(actionName, out var overrideVal))
                return overrideVal;

            InputAction action;
            if (slot != null && slot.v2Version == _cacheVersion)
                action = slot.v2;
            else
            {
                if (!_v2Cache.TryGetValue(actionName, out action))
                {
                    action = _asset?.FindAction(actionName, throwIfNotFound: false);
                    _v2Cache[actionName] = action;
                    _cacheVersion++;
                }
                if (slot != null) { slot.v2 = action; slot.v2Version = _cacheVersion; }
            }
            return action?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        private float AxisCore(string actionName, ActionSlot slot)
        {
            InputAction action;
            bool isTrigger;
            if (slot != null && slot.axisVersion == _cacheVersion)
            {
                action    = slot.axis;
                isTrigger = slot.axisIsTrigger;
            }
            else
            {
                if (!_axisCache.TryGetValue(actionName, out action))
                {
                    action = _asset?.FindAction(actionName, throwIfNotFound: false);
                    _axisCache[actionName] = action;
                    _cacheVersion++;
                }
                // Tra _skillCache cả khi action null (không side effect) để slot lưu đủ cả 2 giá trị
                isTrigger = _skillCache.TryGetValue(actionName, out var ca) && ca.isAnalogTrigger;
                if (slot != null) { slot.axis = action; slot.axisIsTrigger = isTrigger; slot.axisVersion = _cacheVersion; }
            }
            if (action == null) return 0f;

            float rawVal = action.ReadValue<float>();

            if (isTrigger)
                return ApplyTriggerCurve(rawVal);

            return rawVal;
        }

        // ══════════════════════════════════════════════════════
        //  ACTION HANDLE — tra tên 1 lần, đọc mỗi frame không băm string
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Lấy handle cho <paramref name="actionName"/> (gọi 1 lần, vd. trong Awake/Start, rồi giữ lại).
        /// Handle vẫn đúng sau khi đổi / swap profile. Tên chưa có trong profile vẫn lấy được handle —
        /// kết quả đọc giống hệt API string (kể cả cảnh báo 1 lần khi không tìm thấy).
        /// </summary>
        public TISActionHandle GetHandle(string actionName)
            => actionName == null ? default : new TISActionHandle(this, GetSlot(actionName), actionName);

        public bool GetSkillAction(in TISActionHandle handle)
        {
            if (handle.Reader == this) return SkillDefaultMode(handle.Name, handle.Slot);
            return handle.Name != null && GetSkillAction(handle.Name);
        }

        public bool GetSkillAction(in TISActionHandle handle, TISButtonReadMode mode)
        {
            if (handle.Reader == this) return SkillWithMode(handle.Name, mode, handle.Slot);
            return handle.Name != null && GetSkillAction(handle.Name, mode);
        }

        public Vector2 GetVector2Action(in TISActionHandle handle)
        {
            if (handle.Reader == this) return Vector2Core(handle.Name, handle.Slot);
            return handle.Name != null ? GetVector2Action(handle.Name) : Vector2.zero;
        }

        public float GetAxisAction(in TISActionHandle handle)
        {
            if (handle.Reader == this) return AxisCore(handle.Name, handle.Slot);
            return handle.Name != null ? GetAxisAction(handle.Name) : 0f;
        }

        /// <summary>Slot của tên action; <c>null</c> khi tên null (đường string giữ hành vi cũ: ném ArgumentNullException).</summary>
        private ActionSlot GetSlot(string actionName)
        {
            if (actionName == null) return null;
            if (_slotByRef.TryGetValue(actionName, out var slot)) return slot;

            if (!_slotByName.TryGetValue(actionName, out slot))
            {
                slot = new ActionSlot(actionName);
                _slotByName[actionName] = slot;
            }
            if (_slotByRef.Count < MaxRefSlots) _slotByRef[actionName] = slot;
            return slot;
        }

        // ══════════════════════════════════════════════════════
        //  EDITOR HOOKS  (static delegates — null at runtime build)
        //
        //  Editor assembly đăng ký qua [InitializeOnLoad].
        //  Khi build DLL Runtime tất cả = null → no-op.
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Hook: Editor debug panel gọi để lấy snapshot của skill cache.
        /// Signature: (TISActionReader reader) → IReadOnlyDictionary&lt;string, object&gt;
        /// </summary>
        public static Func<TISActionReader, IReadOnlyDictionary<string, object>>
            EditorHook_GetSkillCacheSnapshot;

        /// <summary>
        /// Hook: Log debug khi skill action kích hoạt từ mobile button.
        /// Signature: (actionName, mode, sourceName, debugEnabled) → void
        /// </summary>
        public static Action<string, TISButtonReadMode, string, bool> EditorHook_LogSkillAction;

        /// <summary>
        /// Hook: Log debug khi skill action kích hoạt từ keyboard/gamepad.
        /// Signature: (actionName, mode, inputAction, debugEnabled) → void
        /// </summary>
        public static Action<string, TISButtonReadMode, InputAction, bool> EditorHook_LogSkillActionWithSource;

        /// <summary>
        /// Expose skill cache cho editor debug panel thông qua hook.
        /// Trả về null nếu không có Editor assembly (runtime build).
        /// </summary>
        public IReadOnlyDictionary<string, object> GetSkillCacheSnapshot()
            => EditorHook_GetSkillCacheSnapshot?.Invoke(this);

        /// <summary>
        /// Internal: xây snapshot box-ed từ _skillCache. Chỉ gọi từ Editor hook.
        /// </summary>
        public Dictionary<string, object> BuildSkillCacheSnapshot()
        {
            // Boxing chấp nhận được — chỉ được gọi từ Editor
            var snap = new Dictionary<string, object>(_skillCache.Count);
            foreach (var kv in _skillCache)
                snap[kv.Key] = kv.Value;
            return snap;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — EVAL
        // ══════════════════════════════════════════════════════

        private bool EvalCachedAction(string actionName, TISButtonReadMode mode, in CachedAction ca)
        {
            // P2 FIX #8: _isMobileMode already cached — no Application.isMobilePlatform call per frame
            if (_isMobileMode && !string.IsNullOrEmpty(ca.mobileBinding))
            {
                bool result = mode switch
                {
                    TISButtonReadMode.PressDown => TISMobileButtonRegistry.WasPressedThisFrame(actionName),
                    TISButtonReadMode.Hold      => TISMobileButtonRegistry.GetState(actionName),
                    TISButtonReadMode.PressUp   => TISMobileButtonRegistry.WasReleasedThisFrame(actionName),
                    _                           => false,
                };

                if (result) EditorHook_LogSkillAction?.Invoke(actionName, mode, "TISMobileSkillButton", _debugSkillAction);

                // Nút ảo chưa kích hoạt → vẫn đọc InputAction: tay cầm / bàn phím Bluetooth
                // cắm vào điện thoại / tablet phải dùng được cùng lúc với nút ảo.
                // Editor giả lập mobile: chuột đang đóng vai ngón tay → bỏ qua action do chuột kích hoạt
                // (không thì kéo chuột xoay camera sẽ bắn luôn Attack = <Mouse>/leftButton).
                if (!result && ca.action != null
                    && !(Application.isEditor && ca.action.activeControl?.device is Pointer))
                    result = ReadButton(ca.action, mode, ca.isAnalogTrigger, ca.triggerIndex);
#if DEVELOPMENT_BUILD
                if (result && _debugSkillAction)
                    Debug.Log(
                        $"[TISActionReader] ✓ Action \"{actionName}\" [{mode}] — nguồn: TISMobileSkillButton");
#endif
                return result;
            }

            if (ca.action == null) return false;

            bool btnResult = ReadButton(ca.action, mode, ca.isAnalogTrigger, ca.triggerIndex);

            if (btnResult) EditorHook_LogSkillActionWithSource?.Invoke(actionName, mode, ca.action, _debugSkillAction);
#if DEVELOPMENT_BUILD
            if (btnResult && _debugSkillAction)
            {
                var    ctrl = ca.action.activeControl;
                string src  = ctrl?.device is Gamepad gp
                    ? $"Gamepad [{gp.displayName}]"
                    : $"Keyboard [{ctrl?.displayName ?? "?"}]";
                Debug.Log($"[TISActionReader] ✓ Action \"{actionName}\" [{mode}] — nguồn: {src}");
            }
#endif
            return btnResult;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — READ BUTTON
        // ══════════════════════════════════════════════════════

        private bool ReadButton(
            InputAction action, TISButtonReadMode mode,
            bool isAnalogTrigger, int triggerIndex = -1)
        {
            if (action == null) return false;

            if (isAnalogTrigger)
            {
                bool held, wasHeldLastFrame;
                if (triggerIndex >= 0 && triggerIndex < _triggerCount)
                {
                    // Lấy từ mẫu đầu frame — không mutate, đọc bao nhiêu lần cũng nhất quán
                    held             = _triggerHeldThisFrame[triggerIndex];
                    wasHeldLastFrame = _triggerHeldLastFrame[triggerIndex];
                }
                else
                {
                    // Vượt MaxAnalogTriggers → không theo dõi được cạnh, chỉ trả Hold đúng
                    held             = action.ReadValue<float>() >= TISConstants.TriggerPressPoint;
                    wasHeldLastFrame = held;
                }

                return mode switch
                {
                    TISButtonReadMode.Hold      => held,
                    TISButtonReadMode.PressDown => held && !wasHeldLastFrame,    // ✓ 1 frame
                    TISButtonReadMode.PressUp   => !held && wasHeldLastFrame,    // ✓ 1 frame
                    _                           => false,
                };
            }

            return mode switch
            {
                TISButtonReadMode.Hold      => action.IsPressed(),
                TISButtonReadMode.PressDown => action.WasPressedThisFrame(),
                TISButtonReadMode.PressUp   => action.WasReleasedThisFrame(),
                _                           => false,
            };
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — CACHE BUILD
        // ══════════════════════════════════════════════════════

        private void ClearCaches()
        {
            _skillCache.Clear();
            _v2Cache.Clear();
            _axisCache.Clear();
            System.Array.Clear(_triggerHeldLastFrame, 0, _triggerHeldLastFrame.Length);
            System.Array.Clear(_triggerHeldThisFrame, 0, _triggerHeldThisFrame.Length);
            System.Array.Clear(_triggerActions, 0, _triggerActions.Length);
            _triggerCount = 0;
            _cacheVersion++;   // slot của TISActionHandle resolve lại
        }

        private void BuildCommonActionCache()
        {
            _moveAction = _asset?.FindAction("Move", throwIfNotFound: false);
            _lookAction = _asset?.FindAction("Look", throwIfNotFound: false);

            _lookActionIsMouseDelta = false;
            if (_lookAction != null)
            {
                foreach (var binding in _lookAction.bindings)
                {
                    if (binding.effectivePath.IndexOf(
                            "<Mouse>/delta", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _lookActionIsMouseDelta = true;
                        break;
                    }
                }
            }
        }

        private void BuildSkillCache()
        {
            if (_asset == null || _activeProfile == null) return;

            foreach (var mapData in _activeProfile.actionMaps)
            foreach (var def in mapData.actions)
            {
                if (string.IsNullOrEmpty(def.actionName)) continue;
                if (_skillCache.ContainsKey(def.actionName)) continue;

                var act = _asset.FindAction(def.actionName, throwIfNotFound: false);
                bool isAnalogTrigger = act != null && IsAnalogTriggerAction(act);
                _skillCache[def.actionName] = new CachedAction
                {
                    action          = act,
                    readMode        = def.readMode,
                    isAnalogTrigger = isAnalogTrigger,
                    mobileBinding   = def.bindingMobile,
                    triggerIndex    = isAnalogTrigger ? RegisterTrigger(act) : -1,
                };
            }
            _cacheVersion++;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — HELPERS
        // ══════════════════════════════════════════════════════

        private float ApplyTriggerCurve(float rawVal)
        {
            float curve = _activeProfile?.gamepadSettings.stickCurve ?? 1f;
            if (Mathf.Approximately(curve, 1f)) return rawVal;
            return Mathf.Pow(Mathf.Clamp01(rawVal), curve);
        }

        /// <summary>
        /// Đăng ký trigger để SampleTriggers() theo dõi. Lấy mẫu ngay để frame đầu không
        /// sinh PressDown giả (last = this = trạng thái hiện tại).
        /// </summary>
        private int RegisterTrigger(InputAction action)
        {
            if (_triggerCount >= TISConstants.MaxAnalogTriggers) return -1;
            int i = _triggerCount++;
            _triggerActions[i] = action;
            bool held = action != null && action.ReadValue<float>() >= TISConstants.TriggerPressPoint;
            _triggerHeldThisFrame[i] = held;
            _triggerHeldLastFrame[i] = held;
            return i;
        }

        private static bool IsAnalogTriggerAction(InputAction action)
        {
            foreach (var binding in action.bindings)
                if (TISProfileBuilder.IsTriggerAnalogPath(binding.effectivePath)) return true;
            return false;
        }
    }

    /// <summary>
    /// Handle tra cứu action đã resolve sẵn — lấy 1 lần qua <c>TISInputBridge.GetActionHandle(name)</c>,
    /// đọc mỗi frame qua các overload nhận handle (không băm / so sánh string).
    /// Kết quả giống hệt API string cùng tên; vẫn đúng sau khi đổi / swap profile.
    /// <c>default</c> (handle rỗng) luôn đọc ra false / 0.
    /// </summary>
    public readonly struct TISActionHandle
    {
        internal readonly TISActionReader              Reader;
        internal readonly TISActionReader.ActionSlot   Slot;

        /// <summary>Tên action đã dùng để lấy handle.</summary>
        public readonly string Name;

        /// <summary><c>true</c> nếu handle được tạo từ một tên (không phải <c>default</c>).</summary>
        public bool IsValid => Name != null;

        internal TISActionHandle(TISActionReader reader, TISActionReader.ActionSlot slot, string name)
        {
            Reader = reader;
            Slot   = slot;
            Name   = name;
        }

        public override string ToString() => Name ?? "(none)";
    }
}
