// =============================================================
//  WeaponClipsTab.cs  —  Tab "Weapon Clips"
//
//  Kéo file .controller vào field "Target Controller".
//  Nhấn "Add Weapon Entry" để:
//    1. Tạo state trong SSM RELOAD / SWITCH OUT / SWITCH IN
//    2. Tạo sub-SSM "ADS W{n}" bên trong SSM "ADS" với state "ADS" và
//       "CROUCH ADS" + đầy đủ transition (inject theo từng weapon)
//    3. Nối đầy đủ transition vào/ra mỗi state (giống SpineLayerBuilder)
//    4. Nối transition từ root (IsDead, NULL) THẲNG vào state W{n} vừa
//       tạo bên trong SSM (không nối vào SSM, không dùng Entry transition
//       của SSM) — làm cho MỖI entry được add
//
//  Transition đầy đủ theo SpineLayerBuilder:
//
//  ROOT (nối thẳng vào state W{n}, kèm WeaponIndex==n):
//    isDead  → SWITCH IN W{n}   : IsDead==false           (dur=0)
//    NULL    → RELOAD W{n}      : IsReloading + MoveX/Y±0.5 (dur=0)
//    NULL    → SWITCH OUT W{n}  : IsSwitching             (dur=0)
//    ADS     → RELOAD W{n}      : IsReloading             (dur=0.25)
//    ADS     → SWITCH OUT W{n}  : IsSwitching             (dur=0.25)
//    CRADS   → RELOAD W{n}      : IsReloading             (dur=0.25)
//    CRADS   → SWITCH OUT W{n}  : IsSwitching             (dur=0.25)
//
//  RELOAD W{n}:
//    (không có Entry transition riêng — vào bằng root transition ở trên)
//    Exit    → NULL            : exitTime=0.905, dur=0.2
//    Interrupt→ NULL           : IsDead(true,0.25), IsGrouded(false,0.25), IsSliding(true,0.25)
//    Interrupt→ NULL           : MoveX/Y > ±0.5 (dur=0.25) x4
//    Interrupt→ NULL           : IsShooting (Trigger, dur=0.25)
//    Interrupt→ SSM SWITCH OUT : IsSwitching (dur=0.25)
//    Interrupt→ ADS            : IfNot IsCrouching && ADS (dur=0.25)
//    Interrupt→ CROUCH ADS     : IsCrouching && ADS (dur=0.25)
//    Interrupt→ FRAG GET       : GrenadeID==1 && IsThrow (dur=0.25)
//    Interrupt→ FLASH GET      : GrenadeID==2 && IsThrow (dur=0.25)
//
//  SWITCH OUT W{n}:
//    (không có Entry transition riêng — vào bằng root transition ở trên)
//    Exit    → IsDead          : exitTime=1, dur=0
//
//  SWITCH IN W{n}:
//    speed=-1, writeDefaultValues=true
//    (không có Entry transition riêng — vào bằng root transition ở trên)
//    Exit    → NULL            : exitTime=0.75, dur=0.25
// =============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[Serializable]
public class WeaponEntry
{
    public int            weaponIndex  = 0;
    public string         weaponName   = "";   // tên tùy chỉnh — dùng đặt tên state/SSM
    public AnimationClip  clipReload;
    public AnimationClip  clipSwitch;
    public AnimationClip  clipAds;
    public AnimationClip  clipCrouchAds;

    // Tên hiển thị trong UI; fallback về "W{index}" nếu chưa đặt tên
    public string label       => string.IsNullOrWhiteSpace(weaponName) ? $"W{weaponIndex}" : weaponName;
    // Tên dùng để đặt state/SSM trong animator
    public string stateSuffix => label;
}

[Serializable]
public class WeaponClipsTab
{
    // ── State ─────────────────────────────────────────────────
    [SerializeField] public List<WeaponEntry> entries = new List<WeaponEntry>();

    // ── Target controller ──────────────────────────────────────
    private AnimatorController targetController;

    // ── Pending entry ──────────────────────────────────────────
    private int           pendingIndex     = 0;
    private string        pendingName      = "";
    private AnimationClip pendingReload;
    private AnimationClip pendingSwitch;
    private AnimationClip pendingAds;
    private AnimationClip pendingCrouchAds;

    private Vector2 scroll;

    // ─────────────────────────────────────────────────────────
    // DRAW
    // ─────────────────────────────────────────────────────────
    public void Draw()
    {
        EditorGUILayout.LabelField("Weapon Clips (Character Mode)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Kéo file .controller vào ô bên dưới.\n" +
            "Thêm từng weapon: nhập WeaponIndex và kéo thả clip Reload + Switch.\n" +
            "Nhấn 'Add Weapon Entry' để tạo state và nối đầy đủ transition vào\n" +
            "SSM RELOAD / SWITCH OUT / SWITCH IN trong Spine layer.",
            MessageType.Info);
        EditorGUILayout.Space(4);

        // ── Target controller field ────────────────────────────
        EditorGUILayout.LabelField("Target Controller", EditorStyles.boldLabel);
        targetController = (AnimatorController)EditorGUILayout.ObjectField(
            "Controller File", targetController, typeof(AnimatorController), false);

        if (targetController == null)
        {
            EditorGUILayout.HelpBox("Kéo file .controller vào đây để bật tính năng inject.", MessageType.Warning);
        }
        else
        {
            var spineLayer = FindSpineLayer(targetController);
            if (spineLayer == null)
                EditorGUILayout.HelpBox("Không tìm thấy layer 'Spine' trong controller này.", MessageType.Error);
            else
            {
                bool hasReload = FindSSM(spineLayer.stateMachine, "RELOAD")     != null;
                bool hasOut    = FindSSM(spineLayer.stateMachine, "SWITCH OUT")  != null;
                bool hasIn     = FindSSM(spineLayer.stateMachine, "SWITCH IN")   != null;
                bool hasAds    = FindSSM(spineLayer.stateMachine, "ADS")         != null;
                string status  = $"Spine layer:  RELOAD={Tick(hasReload)}  SWITCH OUT={Tick(hasOut)}  SWITCH IN={Tick(hasIn)}  ADS={Tick(hasAds)}";
                bool allOk     = hasReload && hasOut && hasIn && hasAds;
                EditorGUILayout.HelpBox(status, allOk ? MessageType.None : MessageType.Warning);
            }
        }

        EditorGUILayout.Space(8);

        // ── Add form ───────────────────────────────────────────
        EditorGUILayout.LabelField("Thêm Weapon Entry", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            pendingIndex     = EditorGUILayout.IntField("Weapon Index", pendingIndex);
            pendingName      = EditorGUILayout.TextField("Weapon Name",  pendingName);
            pendingReload    = (AnimationClip)EditorGUILayout.ObjectField(
                "Clip Reload",      pendingReload,    typeof(AnimationClip), false);
            pendingSwitch    = (AnimationClip)EditorGUILayout.ObjectField(
                "Clip Switch",      pendingSwitch,    typeof(AnimationClip), false);
            pendingAds       = (AnimationClip)EditorGUILayout.ObjectField(
                "Clip ADS",         pendingAds,       typeof(AnimationClip), false);
            pendingCrouchAds = (AnimationClip)EditorGUILayout.ObjectField(
                "Clip Crouch ADS",  pendingCrouchAds, typeof(AnimationClip), false);

            EditorGUILayout.Space(4);
            bool canAdd = pendingReload != null || pendingSwitch != null || pendingAds != null || pendingCrouchAds != null;
            GUI.enabled = canAdd;
            if (GUILayout.Button("Add Weapon Entry", GUILayout.Height(28)))
            {
                var entry = UpsertEntry(pendingIndex, pendingName, pendingReload, pendingSwitch, pendingAds, pendingCrouchAds);

                if (targetController != null)
                    InjectEntryIntoController(targetController, entry);

                pendingIndex     = 0;
                pendingName      = "";
                pendingReload    = null;
                pendingSwitch    = null;
                pendingAds       = null;
                pendingCrouchAds = null;
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Entry list ─────────────────────────────────────────
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("Chưa có weapon entry nào.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField($"Danh sách Weapon Entries ({entries.Count})", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        int removeIdx = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(e.label, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    removeIdx = i;
                EditorGUILayout.EndHorizontal();

                e.weaponIndex   = EditorGUILayout.IntField("  Weapon Index", e.weaponIndex);
                e.weaponName    = EditorGUILayout.TextField( "  Weapon Name",  e.weaponName);
                e.clipReload    = (AnimationClip)EditorGUILayout.ObjectField(
                    "  Clip Reload",     e.clipReload,    typeof(AnimationClip), false);
                e.clipSwitch    = (AnimationClip)EditorGUILayout.ObjectField(
                    "  Clip Switch",     e.clipSwitch,    typeof(AnimationClip), false);
                e.clipAds       = (AnimationClip)EditorGUILayout.ObjectField(
                    "  Clip ADS",        e.clipAds,       typeof(AnimationClip), false);
                e.clipCrouchAds = (AnimationClip)EditorGUILayout.ObjectField(
                    "  Clip Crouch ADS", e.clipCrouchAds, typeof(AnimationClip), false);

                EditorGUILayout.BeginHorizontal();
                DrawBadge("Reload",      e.clipReload    != null);
                DrawBadge("Switch",      e.clipSwitch    != null);
                DrawBadge("ADS",         e.clipAds       != null);
                DrawBadge("Crouch ADS",  e.clipCrouchAds != null);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
        if (removeIdx >= 0) entries.RemoveAt(removeIdx);
    }

    // ─────────────────────────────────────────────────────────
    // UPSERT ENTRY
    // ─────────────────────────────────────────────────────────
    private WeaponEntry UpsertEntry(int index, string name, AnimationClip reload, AnimationClip sw, AnimationClip ads, AnimationClip crouchAds)
    {
        var existing = entries.Find(e => e.weaponIndex == index);
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(name)) existing.weaponName   = name.Trim();
            if (reload    != null)                existing.clipReload    = reload;
            if (sw        != null)                existing.clipSwitch    = sw;
            if (ads       != null)                existing.clipAds       = ads;
            if (crouchAds != null)                existing.clipCrouchAds = crouchAds;
            return existing;
        }
        var entry = new WeaponEntry
        {
            weaponIndex   = index,
            weaponName    = name?.Trim() ?? "",
            clipReload    = reload,
            clipSwitch    = sw,
            clipAds       = ads,
            clipCrouchAds = crouchAds,
        };
        entries.Add(entry);
        entries.Sort((a, b) => a.weaponIndex.CompareTo(b.weaponIndex));
        return entry;
    }

    // ─────────────────────────────────────────────────────────
    // INJECT INTO CONTROLLER
    // ─────────────────────────────────────────────────────────
    private static void InjectEntryIntoController(AnimatorController ac, WeaponEntry entry)
    {
        Undo.RecordObject(ac, $"Add Weapon Entry {entry.stateSuffix}");

        var spineLayer = FindSpineLayer(ac);
        if (spineLayer == null)
        {
            Debug.LogWarning("[WeaponClipsTab] Không tìm thấy layer 'Spine' trong controller.");
            return;
        }

        var root = spineLayer.stateMachine;

        // ── Lookup các state/SSM cần thiết ────────────────────
        var nullS       = FindStateInRoot(root, "NULL");
        var isDead      = FindStateInRoot(root, "IsDead");
        var reloadSM    = FindSSM(root, "RELOAD");
        var switchOutSM = FindSSM(root, "SWITCH OUT");
        var switchInSM  = FindSSM(root, "SWITCH IN");
        var adsSM       = FindSSM(root, "ADS");

        // FRAG GET / FLASH GET nằm trong SSM "GRENADE"
        var grenadeSM = FindSSM(root, "GRENADE");
        var fragGet   = grenadeSM != null ? FindStateInSSM(grenadeSM, "FRAG GET")  : null;
        var flashGet  = grenadeSM != null ? FindStateInSSM(grenadeSM, "FLASH GET") : null;

        if (nullS == null)
        {
            Debug.LogWarning("[WeaponClipsTab] Không tìm thấy state 'NULL' trong Spine layer.");
            return;
        }

        // ── Tạo state + transition bên trong mỗi SSM ──────────
        // Mỗi hàm trả về state VỪA TẠO (null nếu state đã tồn tại từ trước,
        // tức entry này đang được edit lại, không tạo mới).

        // ADS W{n}: inject sub-SSM vào SSM "ADS", lấy ra 2 state để dùng tiếp
        AnimatorState adsState = null, crAds = null;
        if (adsSM != null)
            InjectAds(adsSM, nullS, fragGet, flashGet, entry, out adsState, out crAds);

        AnimatorState reloadState    = reloadSM    != null ? InjectReload   (reloadSM,    nullS, switchOutSM, adsState, crAds, fragGet, flashGet, entry) : null;
        AnimatorState switchOutState = switchOutSM != null ? InjectSwitchOut(switchOutSM, isDead, entry) : null;
        AnimatorState switchInState  = switchInSM  != null ? InjectSwitchIn (switchInSM,  nullS, entry) : null;

        // ── Nối transition từ ROOT thẳng vào state W{n} vừa tạo ──────
        // (không nối vào SSM — chỉ nối khi state đó thực sự mới được tạo,
        //  để tránh add transition trùng lặp lúc edit lại entry cũ)
        InjectEntryRootTransitions(isDead, nullS, adsState, crAds, reloadState, switchOutState, switchInState, entry.weaponIndex);

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WeaponClipsTab] Injected {entry.stateSuffix} vào controller '{ac.name}'.");
    }

    // =========================================================
    // INJECT ADS W{n}
    //
    // Tạo sub-SSM "ADS W{n}" bên trong SSM "ADS" với 2 state và transition
    // giống CharacterSpineLayerBuilder (trước khi refactor):
    //
    //   state "ADS"        : clipAds
    //   state "CROUCH ADS" : clipCrouchAds
    //
    //   ADS → NULL         : IsDead / ADS==false / IsSliding / MoveOut±0.3 / Grenade
    //   ADS → CROUCH ADS   : IsCrouching
    //   CROUCH ADS → NULL  : IsDead / IsCrouching==false / ADS==false / IsGrouded==false / IsSliding / MoveOut±0.3 / Grenade
    //   CROUCH ADS → ADS   : IsCrouching==false  (already covered above)
    //
    //   Entry → sub-SSM (root transitions) được inject bởi InjectEntryRootTransitions.
    //   Exit từ mỗi state → NULL được tạo ở đây.
    // =========================================================
    private static void InjectAds(
        AnimatorStateMachine adsSM,
        AnimatorState nullS,
        AnimatorState fragGet,
        AnimatorState flashGet,
        WeaponEntry entry,
        out AnimatorState adsState,
        out AnimatorState crAds)
    {
        string subName = $"ADS {entry.stateSuffix}";

        // Kiểm tra sub-SSM đã tồn tại chưa
        AnimatorStateMachine subSM = null;
        foreach (var cs in adsSM.stateMachines)
        {
            if (cs.stateMachine.name == subName)
            {
                subSM = cs.stateMachine;
                break;
            }
        }

        if (subSM != null)
        {
            // Đã tồn tại — trả null để InjectEntryRootTransitions bỏ qua,
            // tránh add transition trùng lặp (giống InjectReload/SwitchOut/SwitchIn)
            adsState = null;
            crAds    = null;
            return;
        }

        // Tính vị trí theo số sub-SSM hiện có
        int count = adsSM.stateMachines.Length;
        subSM = adsSM.AddStateMachine(subName, new Vector3(390, count * 110f));

        // ── 2 state ─────────────────────────────────────────────
        adsState = subSM.AddState("ADS",        new Vector3(390,  30));
        crAds    = subSM.AddState("CROUCH ADS", new Vector3(390, 170));
        adsState.writeDefaultValues = false;
        crAds.writeDefaultValues    = false;
        if (entry.clipAds       != null) adsState.motion = entry.clipAds;
        if (entry.clipCrouchAds != null) crAds.motion    = entry.clipCrouchAds;
        subSM.defaultState = adsState;

        // ── ADS → ───────────────────────────────────────────────
        AddBoolTrans(adsState, nullS, "IsDead",    true,  0.25f);
        AddBoolTrans(adsState, nullS, "ADS",       false, 0.25f);
        AddBoolTrans(adsState, nullS, "IsSliding", true,  0.25f);
        AddMoveOutTrans(adsState, nullS, 0.3f);
        {   // → CROUCH ADS
            var t = adsState.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }
        AddGrenadeTrans(adsState, fragGet, flashGet);

        // ── CROUCH ADS → ────────────────────────────────────────
        AddBoolTrans(crAds, nullS,    "IsDead",      true,  0.25f);
        AddBoolTrans(crAds, adsState, "IsCrouching", false, 0.25f);
        AddBoolTrans(crAds, nullS,    "ADS",         false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsGrouded",   false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsSliding",   true,  0.25f);
        AddMoveOutTrans(crAds, nullS, 0.3f);
        AddGrenadeTrans(crAds, fragGet, flashGet);
    }

    // =========================================================
    // INJECT RELOAD W{n}
    //
    // Theo BuildReloadTransitions trong SpineLayerBuilder:
    //   Exit → NULL             : exitTime=0.905, dur=0.2
    //   IsDead(true)  → NULL    : dur=0.25
    //   IsGrouded(false)→ NULL  : dur=0.25
    //   IsSliding(true)→ NULL   : dur=0.25
    //   MoveX/Y > ±0.5 → NULL  : dur=0.25  (x4)
    //   IsShooting (Trigger) → NULL : dur=0.25
    //   IsSwitching   → SWITCH OUT SSM : dur=0.25
    //   ADS && !Crouch → ADS    : dur=0.25
    //   ADS &&  Crouch → CRADS  : dur=0.25
    //   GrenadeID==1 && IsThrow → FRAG GET  : dur=0.25
    //   GrenadeID==2 && IsThrow → FLASH GET : dur=0.25
    //   (không có Entry transition riêng — vào state bằng root transition, xem InjectEntryRootTransitions)
    // =========================================================
    private static AnimatorState InjectReload(
        AnimatorStateMachine sm,
        AnimatorState nullS,
        AnimatorStateMachine switchOutSM,
        AnimatorState adsState,
        AnimatorState crAds,
        AnimatorState fragGet,
        AnimatorState flashGet,
        WeaponEntry entry)
    {
        string stateName = $"RELOAD {entry.stateSuffix}";
        if (FindStateInSSM(sm, stateName) != null) return null; // đã có state này rồi -> root transition cũng đã nối, khỏi làm lại

        int count = sm.states.Length;
        var state = sm.AddState(stateName, new Vector3(390, count * 90f));
        state.writeDefaultValues = false;
        if (entry.clipReload != null) state.motion = entry.clipReload;
        if (sm.defaultState == null) sm.defaultState = state;

        // Exit → NULL
        AddExitTrans(state, nullS, 0.905f, 0.2f);

        // Interrupt → NULL
        AddBoolTrans(state, nullS, "IsDead",    true,  0.25f);
        AddBoolTrans(state, nullS, "IsGrouded", false, 0.25f);
        AddBoolTrans(state, nullS, "IsSliding", true,  0.25f);
        AddMoveOutTrans(state, nullS, 0.5f);

        // → NULL : thoát reload ngay khi IsShooting được kích hoạt (Trigger)
        {
            var t = state.AddTransition(nullS);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsShooting");
        }

        // → SWITCH OUT SSM
        if (switchOutSM != null)
        {
            var t = state.AddTransition(switchOutSM);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsSwitching");
        }

        // → ADS / CROUCH ADS
        if (adsState != null)
        {
            var t = state.AddTransition(adsState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If,    0, "ADS");
        }
        if (crAds != null)
        {
            var t = state.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If, 0, "ADS");
        }

        // → GRENADE
        AddGrenadeTrans(state, fragGet, flashGet);

        return state;
    }

    // =========================================================
    // INJECT SWITCH OUT W{n}
    //
    //   Exit → IsDead : exitTime=1, dur=0
    //   (không có Entry transition riêng — vào state bằng root transition, xem InjectEntryRootTransitions)
    // =========================================================
    private static AnimatorState InjectSwitchOut(AnimatorStateMachine sm, AnimatorState isDead, WeaponEntry entry)
    {
        string stateName = $"SWITCH OUT {entry.stateSuffix}";
        if (FindStateInSSM(sm, stateName) != null) return null;

        int count = sm.states.Length;
        var state = sm.AddState(stateName, new Vector3(390, count * 90f));
        state.writeDefaultValues = false;
        if (entry.clipSwitch != null) state.motion = entry.clipSwitch;
        if (sm.defaultState == null) sm.defaultState = state;

        // Phát hết clip → IsDead
        AddExitTrans(state, isDead, 1f, 0f);

        return state;
    }

    // =========================================================
    // INJECT SWITCH IN W{n}
    //
    // Theo SpineLayerBuilder (dòng 68-69):
    //   speed = -1, writeDefaultValues = true
    //   AddExitTrans(switchIn, nullS, 0.75f, 0.25f)
    //   (không có Entry transition riêng — vào state bằng root transition, xem InjectEntryRootTransitions)
    // =========================================================
    private static AnimatorState InjectSwitchIn(AnimatorStateMachine sm, AnimatorState nullS, WeaponEntry entry)
    {
        string stateName = $"SWITCH IN {entry.stateSuffix}";
        if (FindStateInSSM(sm, stateName) != null) return null;

        int count = sm.states.Length;
        var state = sm.AddState(stateName, new Vector3(390, count * 90f));
        state.writeDefaultValues = true;
        state.speed = -1f;
        if (entry.clipSwitch != null) state.motion = entry.clipSwitch;
        if (sm.defaultState == null) sm.defaultState = state;

        AddExitTrans(state, nullS, 0.75f, 0.25f);

        return state;
    }

    // =========================================================
    // INJECT ENTRY ROOT TRANSITIONS  (gọi cho MỖI entry — không phải chỉ lần đầu)
    //
    // Không nối vào SSM. Nối trực tiếp vào state W{n} vừa tạo bên trong
    // SSM đó, kèm thêm điều kiện WeaponIndex==n để route đúng weapon.
    //
    //   isDead  → SWITCH IN W{n}    : IsDead==false + WeaponIndex==n                    (dur=0)
    //   NULL    → RELOAD W{n}       : IsReloading + MoveX/Y±0.5 + WI==n               (dur=0.25)
    //   NULL    → SWITCH OUT W{n}   : IsSwitching + WeaponIndex==n                    (dur=0.25)
    //   NULL    → ADS W{n}          : ADS+!Crouch+!Sliding+MoveX/Y±0.3 + WI==n       (dur=0.25)
    //   NULL    → CROUCH ADS W{n}   : ADS+Crouch+!Sliding+MoveX/Y±0.3 + WI==n        (dur=0.25)
    //   ADS     → RELOAD W{n}       : IsReloading + WeaponIndex==n                    (dur=0.25)
    //   ADS     → SWITCH OUT W{n}   : IsSwitching + WeaponIndex==n                    (dur=0.25)
    //   CRADS   → SWITCH OUT W{n}   : IsSwitching + WeaponIndex==n                    (dur=0.25)
    //   CRADS   → RELOAD W{n}       : IsReloading + WeaponIndex==n                    (dur=0.25)
    //
    // reloadState/switchOutState/switchInState == null khi state đó vừa
    // bị bỏ qua vì đã tồn tại từ trước (entry được edit lại) -> khỏi nối
    // lại root transition (đã có sẵn từ lần tạo đầu tiên).
    // =========================================================
    private static void InjectEntryRootTransitions(
        AnimatorState isDead,
        AnimatorState nullS,
        AnimatorState adsState,
        AnimatorState crAds,
        AnimatorState reloadState,
        AnimatorState switchOutState,
        AnimatorState switchInState,
        int weaponIndex)
    {
        // isDead → SWITCH IN W{n} : IsDead==false + WeaponIndex==n
        if (isDead != null && switchInState != null)
        {
            var t = isDead.AddTransition(switchInState);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsSwitching");
            t.AddCondition(AnimatorConditionMode.Equals, weaponIndex, "WeaponIndex");
        }

        // NULL → RELOAD W{n} : IsReloading + MoveX/Y idle range ±0.5 + WeaponIndex==n
        if (reloadState != null)
        {
            var t = nullS.AddTransition(reloadState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsReloading");
            t.AddCondition(AnimatorConditionMode.Greater, -0.5f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.5f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.5f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.5f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Equals, weaponIndex, "WeaponIndex");
        }

        // NULL → SWITCH OUT W{n} : IsSwitching + WeaponIndex==n
        if (switchOutState != null)
            AddBoolTransWithIndex(nullS, switchOutState, "IsSwitching", true, 0.25f, weaponIndex);

        // NULL → ADS W{n}       : ADS + !IsCrouching + !IsSliding + MoveX/Y±0.3 + WeaponIndex==n
        if (adsState != null)
        {
            var t = nullS.AddTransition(adsState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "ADS");
            t.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsSliding");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Equals, weaponIndex, "WeaponIndex");
        }

        // NULL → CROUCH ADS W{n} : ADS + IsCrouching + !IsSliding + MoveX/Y±0.3 + WeaponIndex==n
        if (crAds != null)
        {
            var t = nullS.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "ADS");
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsSliding");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Equals, weaponIndex, "WeaponIndex");
        }

        // ADS → RELOAD W{n} / SWITCH OUT W{n}
        if (adsState != null)
        {
            if (reloadState    != null) AddBoolTransWithIndex(adsState, reloadState,    "IsReloading", true, 0.25f, weaponIndex);
            if (switchOutState != null) AddBoolTransWithIndex(adsState, switchOutState, "IsSwitching", true, 0.25f, weaponIndex);
        }

        // CROUCH ADS → SWITCH OUT W{n} / RELOAD W{n}
        if (crAds != null)
        {
            if (switchOutState != null) AddBoolTransWithIndex(crAds, switchOutState, "IsSwitching", true, 0.25f, weaponIndex);
            if (reloadState    != null) AddBoolTransWithIndex(crAds, reloadState,    "IsReloading", true, 0.25f, weaponIndex);
        }
    }

    // ─────────────────────────────────────────────────────────
    // TRANSITION HELPERS
    // ─────────────────────────────────────────────────────────
    private static void SetTrans(AnimatorStateTransition t, float duration, bool hasExitTime, float exitTime = 0.75f)
    {
        t.duration            = duration;
        t.hasExitTime         = hasExitTime;
        t.exitTime            = exitTime;
        t.hasFixedDuration    = true;
        t.offset              = 0;
        t.canTransitionToSelf = true;
    }

    private static void AddExitTrans(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        var t = from.AddTransition(to);
        SetTrans(t, duration, true, exitTime);
    }

    // AnimatorState → AnimatorState
    private static void AddBoolTrans(AnimatorState from, AnimatorState to, string param, bool value, float dur)
    {
        var t = from.AddTransition(to);
        SetTrans(t, dur, false);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    // AnimatorState → AnimatorStateMachine
    private static void AddBoolTrans(AnimatorState from, AnimatorStateMachine to, string param, bool value, float dur)
    {
        var t = from.AddTransition(to);
        SetTrans(t, dur, false);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    // AnimatorState → AnimatorState, kèm điều kiện WeaponIndex==n (dùng cho root -> state cụ thể trong SSM)
    private static void AddBoolTransWithIndex(AnimatorState from, AnimatorState to, string param, bool value, float dur, int weaponIndex)
    {
        var t = from.AddTransition(to);
        SetTrans(t, dur, false);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
        t.AddCondition(AnimatorConditionMode.Equals, weaponIndex, "WeaponIndex");
    }

    // MoveX/Y > ±threshold  (4 transition)
    private static void AddMoveOutTrans(AnimatorState from, AnimatorState to, float threshold)
    {
        foreach (var (param, mode) in new[]
        {
            ("MoveX", AnimatorConditionMode.Greater),
            ("MoveX", AnimatorConditionMode.Less),
            ("MoveY", AnimatorConditionMode.Greater),
            ("MoveY", AnimatorConditionMode.Less),
        })
        {
            var t = from.AddTransition(to);
            SetTrans(t, 0.25f, false);
            float val = mode == AnimatorConditionMode.Greater ? threshold : -threshold;
            t.AddCondition(mode, val, param);
        }
    }

    private static void AddGrenadeTrans(AnimatorState from, AnimatorState fragGet, AnimatorState flashGet)
    {
        if (fragGet != null)
        {
            var t = from.AddTransition(fragGet);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.Equals, 1, "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,     0, "IsThrow");
        }
        if (flashGet != null)
        {
            var t = from.AddTransition(flashGet);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.Equals, 2, "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,     0, "IsThrow");
        }
    }

    // ─────────────────────────────────────────────────────────
    // LOOKUP HELPERS
    // ─────────────────────────────────────────────────────────
    private static AnimatorControllerLayer FindSpineLayer(AnimatorController ac)
    {
        foreach (var l in ac.layers)
            if (l.name == "Spine") return l;
        return null;
    }

    private static AnimatorStateMachine FindSSM(AnimatorStateMachine root, string name)
    {
        foreach (var cs in root.stateMachines)
            if (cs.stateMachine.name == name) return cs.stateMachine;
        return null;
    }

    private static AnimatorState FindStateInRoot(AnimatorStateMachine root, string name)
    {
        foreach (var cs in root.states)
            if (cs.state.name == name) return cs.state;
        return null;
    }

    private static AnimatorState FindStateInSSM(AnimatorStateMachine sm, string name)
    {
        foreach (var cs in sm.states)
            if (cs.state.name == name) return cs.state;
        return null;
    }

    // ─────────────────────────────────────────────────────────
    // UI HELPERS
    // ─────────────────────────────────────────────────────────
    private static void DrawBadge(string label, bool ok)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = ok ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.65f, 0.65f, 0.65f) }
        };
        EditorGUILayout.LabelField($"  [{(ok ? "✓" : "–")}] {label}", style, GUILayout.Width(100));
    }

    private static string Tick(bool ok) => ok ? "✓" : "✗";
}
#endif