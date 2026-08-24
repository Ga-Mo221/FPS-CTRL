// ============================================================
//  TrueInputSystem — TIS_RebindingSystem.cs
//  P4 #22 (rev 3): Key Rebinding API + JSON persistence.
//
//  THAY ĐỔI SO VỚI REV 2:
//  ─ Tối ưu visibility: 4 API chuyển về private
//    · SaveOverrides()   → private  (system tự gọi, không cần gọi thủ công)
//    · GetOverridePath() → private  (GetActivePath() đã bao phủ mọi UI use-case)
//    · HasOverride()     → private  (GetActivePath() đã bao phủ mọi UI use-case)
//    · GetJsonFilePath() → private  (debug-only, không thuộc stable public API)
//
//  THAY ĐỔI SO VỚI BẢN CŨ (rev 2):
//  ─ Đổi storage từ PlayerPrefs → JSON file (Application.persistentDataPath)
//    · Dễ backup / export / import / version-control config
//    · Không bị lẫn với PlayerPrefs của hệ thống khác
//    · Có thể swap preset từ file bất kỳ
//
//  ─ Tích hợp vào hệ thống, không cần script wrapper ngoài:
//    · ForceApply()   — apply binding bằng code, không cần nhấn phím
//    · ApplyPreset()  — apply nhiều binding cùng lúc từ Dictionary
//    · FindConflict() — kiểm tra path có bị action khác dùng không
//    · GetActivePath()— lấy path đang active (override nếu có, default nếu không)
//
//  ─ BUG FIX (so với bản gốc):
//    · ResetAllBindings: sanitise profileName trước khi filter _overrides
//    · ResetBinding: kiểm tra cả file JSON, không chỉ _overrides in-memory
//    · RebuildBridge: clone từ OriginalProfile (ScriptableObject gốc),
//      không phải ActiveProfile (đã bị ApplyOverridesToProfile mutate)
//
//  JSON FILE PATH:
//      {Application.persistentDataPath}/TIS_Bindings_{profileName}.json
//
//  ══════════════════════════════════════════════════════════
//  PUBLIC API (dùng từ game code / UI):
//  ══════════════════════════════════════════════════════════
//
//  INTERACTIVE — chờ user nhấn phím:
//      TISRebindingSystem.StartRebinding(bridge, "Jump", RebindTarget.PC, result => { });
//      TISRebindingSystem.CancelRebinding();
//
//  PROGRAMMATIC — apply thẳng không cần nhấn phím:
//      TISRebindingSystem.ForceApply(bridge, "Jump", RebindTarget.PC, "<Keyboard>/g");
//
//  BATCH PRESET:
//      TISRebindingSystem.ApplyPreset(bridge, new Dictionary<string,string>{
//          {"Jump","<Keyboard>/space"}, {"Attack","<Keyboard>/f"}
//      }, RebindTarget.PC);
//
//  CONFLICT CHECK (trước khi hiển thị lên UI):
//      string who = TISRebindingSystem.FindConflict(bridge, "Jump", RebindTarget.PC, "<Keyboard>/g");
//      if (who != null) Debug.Log($"Conflict với {who}");
//
//  HIỂN THỊ LÊN UI:
//      string path = TISRebindingSystem.GetActivePath(bridge, "Jump", RebindTarget.PC);
//      label.text  = TISRebindingSystem.GetDisplayName(path);
//
//  TRẠNG THÁI:
//      if (TISRebindingSystem.State == RebindState.Listening) { /* hiện "Press any key…" */ }
//
//  EVENTS:
//      TISRebindingSystem.OnRebindStarted   += (action, target) => { /* hiện overlay */ };
//      TISRebindingSystem.OnRebindCompleted += result => { /* update label */ };
//
//  PERSISTENCE — load khi khởi động:
//      TISRebindingSystem.LoadAndApply(bridge);   // gọi trong Awake()
//
//  RESET:
//      TISRebindingSystem.ResetBinding(bridge, "Jump", RebindTarget.PC);
//      TISRebindingSystem.ResetAllBindings(bridge);
//
//  LƯU Ý:
//  ─ Chỉ hỗ trợ Button action (PC / Gamepad).
//  ─ Vector2 / Axis không rebind qua hệ thống này.
//  ─ Mobile binding không thay đổi.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrueInputSystem
{
    // ══════════════════════════════════════════════════════════
    //  ENUMS & DATA
    // ══════════════════════════════════════════════════════════

    /// <summary>Nền tảng muốn rebind.</summary>
    public enum RebindTarget { PC, Gamepad }

    /// <summary>Trạng thái hiện tại của một lần rebinding.</summary>
    public enum RebindState
    {
        Idle,
        Listening,   // đang chờ phím
        Completed,
        Cancelled,
        Failed,
    }

    /// <summary>Kết quả trả về trong callback sau khi rebinding kết thúc.</summary>
    public readonly struct RebindResult
    {
        public readonly bool         Success;
        public readonly string       ActionName;
        public readonly RebindTarget Target;
        public readonly string       NewPath;
        public readonly string       OldPath;
        public readonly string       FailReason;

        public RebindResult(bool success, string actionName, RebindTarget target,
            string newPath, string oldPath, string failReason = "")
        {
            Success    = success;
            ActionName = actionName;
            Target     = target;
            NewPath    = newPath;
            OldPath    = oldPath;
            FailReason = failReason;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  JSON DATA MODEL
    // ══════════════════════════════════════════════════════════

    [Serializable]
    internal class TISBindingEntry
    {
        public string actionName;
        public string target;    // "PC" | "Gamepad"
        public string path;      // Input System path, e.g. "<Keyboard>/g"
    }

    [Serializable]
    internal class TISBindingFile
    {
        public string                profileName;
        public string                savedAt;     // ISO 8601, dùng cho debug
        public List<TISBindingEntry> entries = new List<TISBindingEntry>();
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN CLASS
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// API tĩnh cho Key Rebinding trong TrueInputSystem.
    /// Lưu trữ override bằng JSON file thay vì PlayerPrefs.
    /// </summary>
    public static class TISRebindingSystem
    {
        // ── In-memory override store ──
        // key = "{profileName}|{actionName}|{target}"  (đã sanitise, OrdinalIgnoreCase)
        private static readonly Dictionary<string, string> _overrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ── Interactive session state ──
        private static RebindState                         _state           = RebindState.Idle;
        private static string                              _pendingAction   = "";
        private static RebindTarget                        _pendingTarget;
        private static string                              _pendingOldPath  = "";
        private static string                              _pendingProfile  = "";
        private static Action<RebindResult>                _pendingCallback;
        private static InputActionRebindingExtensions.RebindingOperation _rebindOp;

        // ── Path blacklist ──
        private static readonly HashSet<string> _pathBlacklist =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "<Keyboard>/escape",
            "<Keyboard>/printScreen",
        }; 

        // ── Events ──

        /// <summary>Fire khi bắt đầu lắng nghe phím (để UI hiện "Press any key…").</summary>
        public static event Action<string, RebindTarget> OnRebindStarted;

        /// <summary>Fire khi rebind kết thúc (success, cancel, hoặc fail).</summary>
        public static event Action<RebindResult> OnRebindCompleted;

        /// <summary>Trạng thái phiên rebinding hiện tại.</summary>
        public static RebindState State => _state;

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — INTERACTIVE
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Bắt đầu rebinding tương tác — chờ người dùng nhấn phím tiếp theo.
        /// </summary>
        /// <param name="bridge">TISInputBridge đang active.</param>
        /// <param name="actionName">Tên action (khớp với profile).</param>
        /// <param name="target">PC hoặc Gamepad.</param>
        /// <param name="onComplete">Callback khi xong.</param>
        /// <param name="cancelPath">Path để huỷ (mặc định Escape). Null = không có cancel key.</param>
        public static void StartRebinding(
            TISInputBridge       bridge,
            string               actionName,
            RebindTarget         target,
            Action<RebindResult> onComplete = null,
            string               cancelPath = "<Keyboard>/escape")
        {
            if (bridge == null)
            {
                Debug.LogError("[TISRebinding] bridge là null.");
                onComplete?.Invoke(Fail(actionName, target, "", "bridge null"));
                return;
            }

            if (_state == RebindState.Listening)
            {
                Debug.LogWarning("[TISRebinding] Đang trong quá trình rebind — huỷ cái cũ trước.");
                CancelRebinding();
            }

            var profile = bridge.ActiveProfile;
            if (profile == null)
            { onComplete?.Invoke(Fail(actionName, target, "", "no active profile")); return; }

            var asset = GetAsset(bridge);
            if (asset == null)
            { onComplete?.Invoke(Fail(actionName, target, "", "InputActionAsset not found")); return; }

            var inputAction = asset.FindAction(actionName, throwIfNotFound: false);
            if (inputAction == null)
            { onComplete?.Invoke(Fail(actionName, target, "", $"action '{actionName}' not found")); return; }

            var def = profile.FindAction(actionName);
            if (def != null && def.actionType != TISInputActionType.Button)
            { onComplete?.Invoke(Fail(actionName, target, "", "chỉ Button action mới hỗ trợ interactive rebinding")); return; }

            string oldPath = def != null
                ? (target == RebindTarget.PC ? def.bindingPC : def.bindingGamepad)
                : "";

            _state           = RebindState.Listening;
            _pendingAction   = actionName;
            _pendingTarget   = target;
            _pendingOldPath  = oldPath;
            _pendingProfile  = profile.profileName;
            _pendingCallback = onComplete;

            inputAction.Disable();

            int bindingIdx = FindFirstBindingIndex(inputAction, target);
            _rebindOp = bindingIdx >= 0
                ? inputAction.PerformInteractiveRebinding(bindingIdx)
                : inputAction.PerformInteractiveRebinding();

            _rebindOp
                .WithControlsHavingToMatchPath(target == RebindTarget.PC ? "<Keyboard>" : "<Gamepad>")
                .WithControlsHavingToMatchPath(target == RebindTarget.PC ? "<Mouse>"    : "")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => HandleRebindComplete(op, bridge, actionName, target, oldPath, onComplete))
                .OnCancel(op   => HandleRebindCancel(op,  bridge, actionName, target, oldPath, onComplete));

            if (!string.IsNullOrEmpty(cancelPath))
                _rebindOp.WithCancelingThrough(cancelPath);

            OnRebindStarted?.Invoke(actionName, target);
            _rebindOp.Start();
            Debug.Log($"[TISRebinding] ▶ Đang lắng nghe phím mới cho \"{actionName}\" [{target}]...");
        }

        /// <summary>Huỷ rebinding đang chạy. Không lưu thay đổi.</summary>
        public static void CancelRebinding()
        {
            if (_state != RebindState.Listening) return;
            _rebindOp?.Cancel();
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — PROGRAMMATIC (không cần nhấn phím)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Apply binding trực tiếp bằng code — không cần người dùng nhấn phím.
        /// Dùng cho: load preset, import config, UI dropdown chọn phím sẵn.
        /// </summary>
        /// <param name="bridge">TISInputBridge đang active.</param>
        /// <param name="actionName">Tên action.</param>
        /// <param name="target">PC hoặc Gamepad.</param>
        /// <param name="newPath">Input System path. VD: "&lt;Keyboard&gt;/g"</param>
        /// <returns>true nếu apply thành công.</returns>
        public static bool ForceApply(
            TISInputBridge bridge,
            string         actionName,
            RebindTarget   target,
            string         newPath)
        {
            if (bridge?.ActiveProfile == null)
            {
                Debug.LogError("[TISRebinding] ForceApply: bridge hoặc profile là null.");
                return false;
            }
            if (string.IsNullOrEmpty(newPath))
            {
                Debug.LogError($"[TISRebinding] ForceApply: path trống cho \"{actionName}\".");
                return false;
            }
            if (_pathBlacklist.Contains(newPath))
            {
                Debug.LogWarning($"[TISRebinding] ForceApply: \"{newPath}\" bị blacklist.");
                return false;
            }

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            _overrides[MakeKey(profileName, actionName, target)] = newPath;

            SaveToJson(bridge);
            RebuildBridge(bridge);

            Debug.Log($"[TISRebinding] ✓ ForceApply: \"{actionName}\" [{target}] = {GetDisplayName(newPath)}");
            return true;
        }

        /// <summary>
        /// Apply nhiều binding cùng lúc từ Dictionary. Chỉ rebuild một lần duy nhất sau khi xong.
        /// Dùng cho: load preset đầy đủ, import từ file JSON bên ngoài.
        /// </summary>
        /// <param name="bridge">TISInputBridge đang active.</param>
        /// <param name="preset">Dictionary: actionName → path.</param>
        /// <param name="target">PC hoặc Gamepad.</param>
        /// <param name="onConflict">
        ///     Callback tuỳ chọn cho mỗi conflict: (actionName, conflictingAction, path).
        ///     Nếu null: bỏ qua action conflict và log warning.
        /// </param>
        /// <returns>Số binding được apply thành công.</returns>
        public static int ApplyPreset(
            TISInputBridge                 bridge,
            Dictionary<string, string>     preset,
            RebindTarget                   target,
            Action<string, string, string> onConflict = null)
        {
            if (bridge?.ActiveProfile == null || preset == null) return 0;

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            int applied = 0;
            bool anyApplied = false;

            foreach (var kv in preset)
            {
                string actionName = kv.Key;
                string newPath    = kv.Value;

                if (string.IsNullOrEmpty(newPath)) continue;

                if (_pathBlacklist.Contains(newPath))
                {
                    Debug.LogWarning($"[TISRebinding] ApplyPreset: Skip \"{actionName}\" — path bị blacklist.");
                    continue;
                }

                string conflict = FindConflict(bridge, actionName, target, newPath);
                if (conflict != null)
                {
                    if (onConflict != null)
                        onConflict(actionName, conflict, newPath);
                    else
                        Debug.LogWarning($"[TISRebinding] ApplyPreset: Skip \"{actionName}\" — " +
                                         $"conflict với \"{conflict}\" ({GetDisplayName(newPath)}).");
                    continue;
                }

                _overrides[MakeKey(profileName, actionName, target)] = newPath;
                anyApplied = true;
                applied++;
            }

            if (anyApplied)
            {
                SaveToJson(bridge);
                RebuildBridge(bridge);
                Debug.Log($"[TISRebinding] ✓ ApplyPreset: {applied}/{preset.Count} binding được apply.");
            }

            return applied;
        }

        /// <summary>
        /// Kiểm tra một path có đang bị action khác dùng không.
        /// </summary>
        /// <param name="bridge">TISInputBridge đang active.</param>
        /// <param name="skipAction">Action đang được kiểm tra (không tính conflict với chính nó).</param>
        /// <param name="target">PC hoặc Gamepad.</param>
        /// <param name="path">Path cần kiểm tra. VD: "&lt;Keyboard&gt;/g"</param>
        /// <returns>Tên action đang dùng path đó, hoặc null nếu không conflict.</returns>
        public static string FindConflict(
            TISInputBridge bridge,
            string         skipAction,
            RebindTarget   target,
            string         path)
        {
            if (bridge?.ActiveProfile == null || string.IsNullOrEmpty(path)) return null;

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);

            foreach (var map in bridge.ActiveProfile.actionMaps)
            foreach (var def in map.actions)
            {
                if (string.Equals(def.actionName, skipAction, StringComparison.OrdinalIgnoreCase)) continue;
                if (def.actionType != TISInputActionType.Button) continue;

                // Lấy path đang active: override nếu có, ngược lại dùng default
                string activePath = GetOverridePath(profileName, def.actionName, target)
                    ?? (target == RebindTarget.PC ? def.bindingPC : def.bindingGamepad);

                if (string.Equals(activePath, path, StringComparison.OrdinalIgnoreCase))
                    return def.actionName;
            }

            return null;
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — RESET
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Reset một binding về default. Bridge rebuild tự động.
        /// </summary>
        public static void ResetBinding(TISInputBridge bridge, string actionName, RebindTarget target)
        {
            if (bridge?.ActiveProfile == null) return;

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            string key         = MakeKey(profileName, actionName, target);

            bool hadInDict = _overrides.ContainsKey(key);
            if (hadInDict) _overrides.Remove(key);

            // FIX: kiểm tra cả file JSON (sau domain reload dict trống nhưng file vẫn còn)
            bool hadInFile = RemoveFromJson(bridge, actionName, target);

            if (hadInDict || hadInFile)
            {
                RebuildBridge(bridge);
                Debug.Log($"[TISRebinding] ✓ Reset \"{actionName}\" [{target}] về default.");
            }
        }

        /// <summary>
        /// Reset toàn bộ override của profile hiện tại. Bridge rebuild tự động.
        /// </summary>
        public static void ResetAllBindings(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return;

            // FIX: sanitise profileName — ActiveProfile có thể chứa " (Runtime)" suffix
            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            var toRemove = new List<string>();

            foreach (var key in _overrides.Keys)
                if (key.StartsWith(profileName + "|", StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(key);

            foreach (var k in toRemove) _overrides.Remove(k);

            bool fileDeleted = DeleteJsonFile(bridge);

            if (toRemove.Count > 0 || fileDeleted)
            {
                RebuildBridge(bridge);
                Debug.Log($"[TISRebinding] ✓ Reset {toRemove.Count} override, xoá JSON cho \"{profileName}\".");
            }
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — PERSISTENCE
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Đọc override từ JSON file và apply lên bridge.
        /// Gọi trong Awake() sau khi TISInputBridge khởi tạo xong.
        /// </summary>
        public static void LoadAndApply(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return;

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            string filePath    = GetJsonPath(profileName);

            if (!File.Exists(filePath))
            {
                Debug.Log($"[TISRebinding] Không có file JSON cho \"{profileName}\" — dùng default binding.");
                return;
            }

            TISBindingFile data;
            try
            {
                data = JsonUtility.FromJson<TISBindingFile>(File.ReadAllText(filePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TISRebinding] Lỗi đọc JSON: {e.Message}");
                return;
            }

            if (data?.entries == null || data.entries.Count == 0) return;

            bool anyLoaded = false;
            foreach (var entry in data.entries)
            {
                if (string.IsNullOrEmpty(entry.actionName) || string.IsNullOrEmpty(entry.path)) continue;
                if (!Enum.TryParse<RebindTarget>(entry.target, out var tgt)) continue;

                _overrides[MakeKey(profileName, entry.actionName, tgt)] = entry.path;
                anyLoaded = true;
                Debug.Log($"[TISRebinding] ↑ Load: \"{entry.actionName}\" [{tgt}] = {GetDisplayName(entry.path)}");
            }

            if (anyLoaded) RebuildBridge(bridge);
        }

        /// <summary>
        /// Lưu toàn bộ override hiện tại vào JSON ngay lập tức.
        /// Được gọi tự động sau mỗi rebind/ForceApply/ApplyPreset/Reset thành công.
        /// → Private: không cần gọi thủ công từ bên ngoài.
        /// </summary>
        private static void SaveOverrides(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return;
            SaveToJson(bridge);
        }

        // ══════════════════════════════════════════════════════
        //  PUBLIC API — QUERY
        //  (GetOverridePath / HasOverride / GetJsonFilePath → private, xem bên dưới)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Lấy override path đang dùng. Trả về null nếu không có override.
        /// → Private: dùng nội bộ bởi GetActivePath() và FindConflict(). Bên ngoài dùng GetActivePath() thay thế.
        /// </summary>
        private static string GetOverridePath(string profileName, string actionName, RebindTarget target)
        {
            _overrides.TryGetValue(MakeKey(profileName, actionName, target), out string v);
            return v;
        }

        /// <summary>
        /// Kiểm tra một action có đang bị override không.
        /// → Private: thông tin này đã được GetActivePath() bao phủ cho mọi use-case UI thực tế.
        /// </summary>
        private static bool HasOverride(string profileName, string actionName, RebindTarget target)
            => _overrides.ContainsKey(MakeKey(profileName, actionName, target));

        /// <summary>
        /// Lấy path đang active của một action.
        /// Override nếu có, ngược lại trả về default từ profile.
        /// Dùng để hiển thị lên UI settings.
        /// </summary>
        public static string GetActivePath(TISInputBridge bridge, string actionName, RebindTarget target)
        {
            if (bridge?.ActiveProfile == null) return "";

            string profileName   = SanitiseProfileName(bridge.ActiveProfile.profileName);
            string overridePath  = GetOverridePath(profileName, actionName, target);
            if (overridePath != null) return overridePath;

            var def = bridge.ActiveProfile.FindAction(actionName);
            if (def == null) return "";
            return target == RebindTarget.PC ? def.bindingPC : def.bindingGamepad;
        }

        /// <summary>
        /// Trả về display name thân thiện cho một Input System path.
        /// VD: "&lt;Keyboard&gt;/space" → "Space"
        /// </summary>
        public static string GetDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            try
            {
                string readable = InputControlPath.ToHumanReadableString(
                    path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                return string.IsNullOrEmpty(readable) ? path : readable;
            }
            catch
            {
                int slash = path.LastIndexOf('/');
                return slash >= 0 ? path.Substring(slash + 1) : path;
            }
        }
        
        public static string GetShortDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
    
            string full = GetDisplayName(path);
    
            return full switch
            {
                "Right Trigger"       => "RT",
                "Left Trigger"        => "LT",
                "Right Shoulder"      => "RB",
                "Left Shoulder"       => "LB",
                "Left Stick Press"    => "L3",
                "Right Stick Press"   => "R3",
                "Button South"        => "A",
                "Button North"        => "Y",
                "Button East"         => "B",
                "Button West"         => "X",
                "D-Pad/Up"            => "D↑",
                "D-Pad/Down"          => "D↓",
                "D-Pad/Left"          => "D←",
                "D-Pad/Right"         => "D→",
                _                     => full,
            };
        }

        /// <summary>
        /// Trả về đường dẫn đầy đủ của file JSON override.
        /// → Private: chỉ phục vụ debug nội bộ. Không thuộc stable public API.
        ///   Nếu cần export, dùng SaveOverrides() rồi truy cập file theo convention:
        ///   {Application.persistentDataPath}/TIS_Bindings_{profileName}.json
        /// </summary>
        private static string GetJsonFilePath(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return "";
            return GetJsonPath(SanitiseProfileName(bridge.ActiveProfile.profileName));
        }

        // ══════════════════════════════════════════════════════
        //  INTERNAL — APPLY TO PROFILE (gọi bởi TISInputBridge.InitialiseWithProfile)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Apply tất cả override trong _overrides vào một profile copy.
        /// Gọi bởi TISInputBridge trước khi Build.
        /// </summary>
        internal static void ApplyOverridesToProfile(TISInputProfileConfig profile)
        {
            if (profile == null) return;

            foreach (var map in profile.actionMaps)
            foreach (var def in map.actions)
            {
                if (def.actionType != TISInputActionType.Button) continue;

                string keyPC = MakeKey(profile.profileName, def.actionName, RebindTarget.PC);
                string keyGP = MakeKey(profile.profileName, def.actionName, RebindTarget.Gamepad);

                if (_overrides.TryGetValue(keyPC, out string pcPath)) def.bindingPC      = pcPath;
                if (_overrides.TryGetValue(keyGP, out string gpPath)) def.bindingGamepad = gpPath;
            }
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — INTERACTIVE CALLBACKS
        // ══════════════════════════════════════════════════════

        private static void HandleRebindComplete(
            InputActionRebindingExtensions.RebindingOperation op,
            TISInputBridge bridge,
            string actionName, RebindTarget target,
            string oldPath, Action<RebindResult> onComplete)
        {
            string newPath = op.selectedControl?.path ?? "";

            if (_pathBlacklist.Contains(newPath))
            {
                Debug.LogWarning($"[TISRebinding] Path \"{newPath}\" bị blacklist — huỷ.");
                op.Dispose();
                ReEnableAction(bridge, actionName);
                _state = RebindState.Failed;

                var failResult = Fail(actionName, target, oldPath, "path bị blacklist");
                _pendingCallback?.Invoke(failResult);
                OnRebindCompleted?.Invoke(failResult);
                ResetSession();
                return;
            }

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            _overrides[MakeKey(profileName, actionName, target)] = newPath;

            op.Dispose();
            _state = RebindState.Completed;

            SaveToJson(bridge);
            RebuildBridge(bridge);

            Debug.Log($"[TISRebinding] ✓ \"{actionName}\" [{target}]: {GetDisplayName(oldPath)} → {GetDisplayName(newPath)}");

            var result = new RebindResult(true, actionName, target, newPath, oldPath);
            onComplete?.Invoke(result);
            OnRebindCompleted?.Invoke(result);
            ResetSession();
        }

        private static void HandleRebindCancel(
            InputActionRebindingExtensions.RebindingOperation op,
            TISInputBridge bridge,
            string actionName, RebindTarget target,
            string oldPath, Action<RebindResult> onComplete)
        {
            op.Dispose();
            ReEnableAction(bridge, actionName);
            _state = RebindState.Cancelled;

            Debug.Log($"[TISRebinding] ✗ Huỷ rebind \"{actionName}\" [{target}].");

            var result = Fail(actionName, target, oldPath, "cancelled");
            onComplete?.Invoke(result);
            OnRebindCompleted?.Invoke(result);
            ResetSession();
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — REBUILD
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Rebuild InputActionAsset từ OriginalProfile + _overrides hiện tại.
        ///
        /// PHẢI dùng bridge.OriginalProfile (ScriptableObject gốc), KHÔNG dùng ActiveProfile.
        /// ActiveProfile đã bị ApplyOverridesToProfile() mutate trực tiếp →
        /// clone từ nó sau khi Reset sẽ giữ lại binding cũ → Reset vô tác dụng.
        /// </summary>
        private static void RebuildBridge(TISInputBridge bridge)
        {
            if (bridge == null) return;

            var sourceProfile = bridge.OriginalProfile ?? bridge.ActiveProfile;
            if (sourceProfile == null) return;

            var copy = sourceProfile.CreateRuntimeCopy();
            copy.profileName = SanitiseProfileName(sourceProfile.profileName);

            ApplyOverridesToProfile(copy);
            bridge.SwapProfile(copy);
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — JSON PERSISTENCE
        // ══════════════════════════════════════════════════════

        private static string GetJsonPath(string sanitisedProfileName)
            => Path.Combine(Application.persistentDataPath,
                            $"TIS_Bindings_{sanitisedProfileName}.json");

        private static void SaveToJson(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return;

            string profileName = SanitiseProfileName(bridge.ActiveProfile.profileName);
            string prefix      = profileName + "|";

            var data = new TISBindingFile
            {
                profileName = profileName,
                savedAt     = DateTime.UtcNow.ToString("o"),
                entries     = new List<TISBindingEntry>()
            };

            foreach (var kv in _overrides)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                var parts = kv.Key.Split('|');
                if (parts.Length != 3) continue;

                data.entries.Add(new TISBindingEntry
                {
                    actionName = parts[1],
                    target     = parts[2],
                    path       = kv.Value
                });
            }

            try
            {
                File.WriteAllText(GetJsonPath(profileName), JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TISRebinding] Lỗi ghi JSON: {e.Message}");
            }
        }

        /// <summary>Xoá một entry khỏi JSON file. Trả về true nếu file tồn tại và entry được xoá.</summary>
        private static bool RemoveFromJson(TISInputBridge bridge, string actionName, RebindTarget target)
        {
            if (bridge?.ActiveProfile == null) return false;

            string filePath = GetJsonPath(SanitiseProfileName(bridge.ActiveProfile.profileName));
            if (!File.Exists(filePath)) return false;

            TISBindingFile data;
            try   { data = JsonUtility.FromJson<TISBindingFile>(File.ReadAllText(filePath)); }
            catch { return false; }

            if (data?.entries == null) return false;

            int before = data.entries.Count;
            data.entries.RemoveAll(e =>
                string.Equals(e.actionName, actionName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.target,     target.ToString(), StringComparison.OrdinalIgnoreCase));

            if (data.entries.Count == before) return false;

            data.savedAt = DateTime.UtcNow.ToString("o");
            try   { File.WriteAllText(filePath, JsonUtility.ToJson(data, prettyPrint: true)); }
            catch (Exception e) { Debug.LogError($"[TISRebinding] Lỗi ghi JSON: {e.Message}"); }

            return true;
        }

        /// <summary>Xoá toàn bộ file JSON của profile. Trả về true nếu file tồn tại.</summary>
        private static bool DeleteJsonFile(TISInputBridge bridge)
        {
            if (bridge?.ActiveProfile == null) return false;
            string filePath = GetJsonPath(SanitiseProfileName(bridge.ActiveProfile.profileName));
            if (!File.Exists(filePath)) return false;
            try   { File.Delete(filePath); return true; }
            catch (Exception e) { Debug.LogError($"[TISRebinding] Lỗi xoá JSON: {e.Message}"); return false; }
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE — HELPERS
        // ══════════════════════════════════════════════════════

        private static int FindFirstBindingIndex(InputAction action, RebindTarget target)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;

                bool isKeyboard = b.effectivePath.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0
                               || b.effectivePath.IndexOf("<Mouse>",    StringComparison.OrdinalIgnoreCase) >= 0;
                bool isGamepad  = b.effectivePath.IndexOf("<Gamepad>",  StringComparison.OrdinalIgnoreCase) >= 0;

                if (target == RebindTarget.PC     && isKeyboard) return i;
                if (target == RebindTarget.Gamepad && isGamepad)  return i;
            }
            return -1;
        }

        private static void ReEnableAction(TISInputBridge bridge, string actionName)
            => GetAsset(bridge)?.FindAction(actionName, throwIfNotFound: false)?.Enable();

        private static InputActionAsset GetAsset(TISInputBridge bridge)
        {
            var field = typeof(TISInputBridge).GetField("_asset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(bridge) as InputActionAsset;
        }

        private static void ResetSession()
        {
            _rebindOp        = null;
            _pendingCallback = null;
            _pendingAction   = "";
            _pendingOldPath  = "";
            _pendingProfile  = "";
        }

        private static RebindResult Fail(string action, RebindTarget target, string old, string reason)
            => new RebindResult(false, action, target, "", old, reason);

        private static string MakeKey(string profile, string action, RebindTarget target)
            => $"{SanitiseProfileName(profile)}|{action}|{target}";

        /// <summary>Bỏ suffix " (Runtime)" mà CreateRuntimeCopy thêm vào profileName.</summary>
        private static string SanitiseProfileName(string name)
            => name?.Replace(" (Runtime)", "").Trim() ?? "";
    }
}