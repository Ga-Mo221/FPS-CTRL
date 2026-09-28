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

        private struct CachedAction
        {
            public InputAction       action;
            public TISButtonReadMode readMode;
            public bool              isAnalogTrigger;
            public string            mobileBinding;
            public int               triggerIndex;
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

        // Trigger per-frame state (P1 FIX #3)
        private readonly bool[] _triggerHeldLastFrame = new bool[TISConstants.MaxAnalogTriggers];
        private readonly bool[] _triggerHeldThisFrame = new bool[TISConstants.MaxAnalogTriggers];
        private readonly bool[] _triggerHeldLastFrameSnapshot = new bool[TISConstants.MaxAnalogTriggers];
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
            Array.Clear(_triggerHeldLastFrameSnapshot, 0, _triggerHeldLastFrameSnapshot.Length);
        }

        /// <summary>Cập nhật _isMobileMode khi SimulateMobile thay đổi lúc runtime (Editor only).</summary>
        public void SetMobileMode(bool isMobileMode) => _isMobileMode = isMobileMode;

        /// <summary>
        /// Gọi từ TISInputBridge.LateUpdate() — snapshot trigger buffer cuối frame (P1 FIX #3).
        /// </summary>
        public void SnapshotTriggerState()
        {
            Array.Copy(_triggerHeldThisFrame, _triggerHeldLastFrame, TISConstants.MaxAnalogTriggers);
            Array.Copy(_triggerHeldThisFrame, _triggerHeldLastFrameSnapshot, TISConstants.MaxAnalogTriggers);
        }

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
        {
            if (_skillCache.TryGetValue(actionName, out var ca))
                return EvalCachedAction(actionName, ca.readMode, in ca);

            var def  = _activeProfile?.FindAction(actionName);
            var mode = def?.readMode ?? TISButtonReadMode.PressDown;
            return GetSkillAction(actionName, mode);
        }

        public bool GetSkillAction(string actionName, TISButtonReadMode mode)
        {
            if (_skillCache.TryGetValue(actionName, out var ca))
                return EvalCachedAction(actionName, mode, in ca);

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
                    triggerIndex    = isAnalogTrigger && _triggerCount < TISConstants.MaxAnalogTriggers
                                        ? _triggerCount++ : -1,
                };
                _skillCache[actionName] = newCa;
                return EvalCachedAction(actionName, mode, in newCa);
            }

            Debug.LogWarning(
                $"[TISActionReader] GetSkillAction: không tìm thấy \"{actionName}\".\n" +
                $"→ Kiểm tra tên trong TISInputProfileConfig.asset.");
            return false;
        }

        // ══════════════════════════════════════════════════════
        //  VECTOR2 / AXIS ACTIONS
        // ══════════════════════════════════════════════════════

        public Vector2 GetVector2Action(string actionName)
        {
            // P3: TISDirectionalSkillButton có thể override action này real-time
            if (TISDirectionalRegistry.TryGetVector2Override(actionName, out var overrideVal))
                return overrideVal;

            if (!_v2Cache.TryGetValue(actionName, out var action))
            {
                action = _asset?.FindAction(actionName, throwIfNotFound: false);
                _v2Cache[actionName] = action;
            }
            return action?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        public float GetAxisAction(string actionName)
        {
            if (!_axisCache.TryGetValue(actionName, out var action))
            {
                action = _asset?.FindAction(actionName, throwIfNotFound: false);
                _axisCache[actionName] = action;
            }
            if (action == null) return 0f;

            float rawVal = action.ReadValue<float>();

            if (_skillCache.TryGetValue(actionName, out var ca) && ca.isAnalogTrigger)
                return ApplyTriggerCurve(rawVal);

            return rawVal;
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
                float val  = action.ReadValue<float>();
                bool  held = val >= TISConstants.TriggerPressPoint;

                // Cập nhật _triggerHeldThisFrame (state hiện tại)
                if (triggerIndex >= 0 && triggerIndex < TISConstants.MaxAnalogTriggers)
                    _triggerHeldThisFrame[triggerIndex] = held;

                // Đọc từ snapshot (không mutate):
                bool wasHeldLastFrame = triggerIndex >= 0 && triggerIndex < TISConstants.MaxAnalogTriggers
                    ? _triggerHeldLastFrameSnapshot[triggerIndex]
                    : false;

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
            _triggerCount = 0;
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
                    triggerIndex    = isAnalogTrigger && _triggerCount < TISConstants.MaxAnalogTriggers
                                        ? _triggerCount++ : -1,
                };
            }
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

        private static bool IsAnalogTriggerAction(InputAction action)
        {
            foreach (var binding in action.bindings)
                if (TISProfileBuilder.IsTriggerAnalogPath(binding.effectivePath)) return true;
            return false;
        }
    }
}