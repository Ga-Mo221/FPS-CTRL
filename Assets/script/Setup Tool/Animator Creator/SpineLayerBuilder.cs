// =============================================================
//  SpineLayerBuilder.cs  —  Layer 2: Spine
//  States: IsDead, NULL, RELOAD, SWITCH OUT, SWITCH IN (speed -1, dùng chung
//          clip với SWITCH OUT — nối IsDead → SWITCH IN → NULL khi hồi sinh)
//  Sub-machines: ADS (ADS + CROUCH ADS), GRENADE (Frag / Flash)
//  Transitions: ADS, IsCrouching, IsReloading, IsSwitching, IsThrow,
//               GrenadeID, MoveX/Y thresholds
// =============================================================

#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

public class SpineLayerBuilder : LayerBuilder
{
    public override void Build(AnimatorController ac, int layerIndex)
    {
        ac.AddLayer("Spine");
        SetLayerWeight(ac, layerIndex, 1f);

        var root  = ac.layers[layerIndex].stateMachine;
        root.name = "Spine";

        // ── Bare states ───────────────────────────────────────
        var isDead = AddState(root, "IsDead", new Vector3(380, 120)); isDead.writeDefaultValues = true;
        var nullS  = AddState(root, "NULL",   new Vector3(680, 120)); nullS.writeDefaultValues  = true;
        var reload = AddStateAndAssignClip(root, "RELOAD", new Vector3(1140, 220));

        // Truyền rõ clipKey = "SWITCH" cho cả 2 state, vì tên clip thường có
        // dạng "..._SWITCH" (tiền tố), trong khi tên state "SWITCH OUT"/
        // "SWITCH IN" có hậu tố IN/OUT nên logic match tự động (Contains 2
        // chiều) sẽ không khớp được nếu chỉ dựa vào tên state.
        var sw = AddStateAndAssignClip(root, "SWITCH OUT", new Vector3(990, 30), "SWITCH");

        // SWITCH IN dùng chung clip với SWITCH OUT, nhưng chạy ngược
        // (speed = -1) để mô phỏng animation rút súng lùi lại.
        var switchIn = AddStateAndAssignClip(root, "SWITCH IN", new Vector3(540, 30), "SWITCH");
        switchIn.speed              = -1f;
        switchIn.writeDefaultValues = true;

        root.defaultState = isDead;

        // ── Sub-machine: ADS ──────────────────────────────────
        var adsSM    = root.AddStateMachine("ADS",     new Vector3(940, 110));
        var adsState = AddStateAndAssignClip(adsSM, "ADS",        new Vector3(390,  30));
        var crAds    = AddStateAndAssignClip(adsSM, "CROUCH ADS", new Vector3(390, 170));
        adsSM.defaultState = adsState;

        // ── Sub-machine: GRENADE ──────────────────────────────
        var gSM       = root.AddStateMachine("GRENADE", new Vector3(990, 310));
        var fragGet   = AddStateAndAssignClip(gSM, "FRAG GET",    new Vector3(490, 110));
        var fragIdle  = AddStateAndAssignClip(gSM, "FRAG IDLE",   new Vector3(490, 200));
        var fragThrow = AddStateAndAssignClip(gSM, "FRAG THROW",  new Vector3(490, 280));
        var flashGet  = AddStateAndAssignClip(gSM, "FLASH GET",   new Vector3(490, 370));
        var flashIdle = AddStateAndAssignClip(gSM, "FLASH IDLE",  new Vector3(490, 450));
        var flashThrow= AddStateAndAssignClip(gSM, "FLASH THROW", new Vector3(490, 530));
        gSM.defaultState = fragGet;

        // ── Grenade internal transitions ──────────────────────
        AddExitTrans(fragGet,  fragIdle,  0.9f, 0.114f);
        AddExitTrans(flashGet, flashIdle, 0.9f, 0.056f);
        BuildFragIdleTransitions(fragIdle,   nullS, fragThrow);
        BuildFlashIdleTransitions(flashIdle, nullS, flashThrow);
        AddExitTrans(fragThrow,  nullS, 0.9f, 0.068f);
        AddExitTrans(flashThrow, nullS, 0.9f, 0.075f);

        // ── IsDead → SWITCH IN → NULL (hồi sinh) ──────────────
        AddBoolTrans(isDead, switchIn, "IsDead", false, 0f);
        AddExitTrans(switchIn, nullS,  0.75f, 0.25f);

        // ── NULL → IsDead (chết) ──────────────────────────────
        AddBoolTrans(nullS,  isDead, "IsDead", true,  0f);

        // ── NULL → destinations ───────────────────────────────
        BuildNullTransitions(nullS, adsState, crAds, reload, sw, fragGet, flashGet);

        // ── ADS ↔ CROUCH ADS ─────────────────────────────────
        BuildAdsTransitions(adsState, nullS, crAds, reload, sw, fragGet, flashGet);
        BuildCrouchAdsTransitions(crAds, nullS, adsState, reload, sw, fragGet, flashGet);

        // ── RELOAD ────────────────────────────────────────────
        BuildReloadTransitions(reload, nullS, adsState, crAds, sw, fragGet, flashGet);

        // ── SWITCH OUT ─────────────────────────────────────────
        AddExitTrans(sw, nullS, 1f, 0f);
    }

    // ─────────────────────────────────────────────────────────
    // NULL TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildNullTransitions(
        AnimatorState nullS,
        AnimatorState adsState, AnimatorState crAds,
        AnimatorState reload,   AnimatorState sw,
        AnimatorState fragGet,  AnimatorState flashGet)
    {
        // → ADS (standing, idle)
        {
            var t = nullS.AddTransition(adsState);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "ADS");
            t.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
        }
        // → CROUCH ADS
        {
            var t = nullS.AddTransition(crAds);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "ADS");
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
        }
        // → RELOAD (idle range)
        {
            var t = nullS.AddTransition(reload);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsReloading");
            t.AddCondition(AnimatorConditionMode.Greater, -0.5f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.5f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.5f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.5f, "MoveY");
        }
        // → SWITCH
        AddBoolTrans(nullS, sw, "IsSwitching", true, 0f);

        // → FRAG GET
        {
            var t = nullS.AddTransition(fragGet);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.Equals,  1,     "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsThrow");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
        }
        // → FLASH GET
        {
            var t = nullS.AddTransition(flashGet);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.Equals,  2,     "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,      0,     "IsThrow");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveX");
            t.AddCondition(AnimatorConditionMode.Greater, -0.3f, "MoveY");
            t.AddCondition(AnimatorConditionMode.Less,     0.3f, "MoveY");
        }
    }

    // ─────────────────────────────────────────────────────────
    // ADS TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildAdsTransitions(
        AnimatorState adsState, AnimatorState nullS, AnimatorState crAds,
        AnimatorState reload,   AnimatorState sw,
        AnimatorState fragGet,  AnimatorState flashGet)
    {
        AddBoolTrans(adsState, nullS,   "IsDead",     true,  0.25f);
        AddBoolTrans(adsState, nullS,   "ADS",        false, 0.25f);
        AddBoolTrans(adsState, nullS,   "IsGrouded",  false, 0.25f);
        AddBoolTrans(adsState, nullS,   "IsSliding",  true,  0.25f);
        AddMoveOutTrans(adsState, nullS, 0.3f);

        {
            var t = adsState.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }

        AddBoolTrans(adsState, reload, "IsReloading", true, 0.25f);
        AddBoolTrans(adsState, sw,     "IsSwitching", true, 0.25f);
        AddGrenadeTrans(adsState, fragGet, flashGet);
    }

    // ─────────────────────────────────────────────────────────
    // CROUCH ADS TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildCrouchAdsTransitions(
        AnimatorState crAds,  AnimatorState nullS, AnimatorState adsState,
        AnimatorState reload, AnimatorState sw,
        AnimatorState fragGet, AnimatorState flashGet)
    {
        AddBoolTrans(crAds, nullS,    "IsDead",      true,  0.25f);
        AddBoolTrans(crAds, adsState, "IsCrouching", false, 0.25f);
        AddBoolTrans(crAds, nullS,    "ADS",         false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsGrouded",   false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsSliding",   true,  0.25f);
        AddMoveOutTrans(crAds, nullS, 0.3f);
        AddBoolTrans(crAds, sw,     "IsSwitching", true, 0.25f);
        AddBoolTrans(crAds, reload, "IsReloading", true, 0.25f);
        AddGrenadeTrans(crAds, fragGet, flashGet);
    }

    // ─────────────────────────────────────────────────────────
    // RELOAD TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildReloadTransitions(
        AnimatorState reload,  AnimatorState nullS,
        AnimatorState adsState, AnimatorState crAds,
        AnimatorState sw,
        AnimatorState fragGet, AnimatorState flashGet)
    {
        AddExitTrans(reload, nullS, 0.905f, 0.2f);
        AddBoolTrans(reload, nullS, "IsDead",    true,  0.25f);
        AddBoolTrans(reload, nullS, "IsGrouded", false, 0.25f);
        AddBoolTrans(reload, nullS, "IsSliding", true,  0.25f);
        AddMoveOutTrans(reload, nullS, 0.5f);

        // → NULL : thoát reload ngay khi IsShooting được kích hoạt (Trigger)
        {
            var t = reload.AddTransition(nullS);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsShooting");
        }
        AddGrenadeTrans(reload, fragGet, flashGet);
        AddBoolTrans(reload, sw, "IsSwitching", true, 0.25f);

        {
            var t = reload.AddTransition(adsState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If,    0, "ADS");
        }
        {
            var t = reload.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If, 0, "ADS");
        }
    }

    // ─────────────────────────────────────────────────────────
    // GRENADE IDLE TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildFragIdleTransitions(AnimatorState fragIdle, AnimatorState nullS, AnimatorState fragThrow)
    {
        var t = fragIdle.AddTransition(fragThrow);
        SetTrans(t, 0.068f, false);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrow");

        AddBoolTrans(fragIdle, nullS, "IsDead", true, 0.25f);
        AddMoveOutTrans(fragIdle, nullS, 0.3f);
    }

    private void BuildFlashIdleTransitions(AnimatorState flashIdle, AnimatorState nullS, AnimatorState flashThrow)
    {
        var t = flashIdle.AddTransition(flashThrow);
        SetTrans(t, 0.075f, false);
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrow");

        AddBoolTrans(flashIdle, nullS, "IsDead", true, 0.25f);
        AddMoveOutTrans(flashIdle, nullS, 0.3f);
    }

    // ─────────────────────────────────────────────────────────
    // SHARED SPINE HELPERS
    // ─────────────────────────────────────────────────────────

    /// <summary>4 transition thoát khi MoveX hoặc MoveY vượt ngưỡng ±<paramref name="threshold"/>.</summary>
    private void AddMoveOutTrans(AnimatorState from, AnimatorState to, float threshold)
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
            float val = (mode == AnimatorConditionMode.Greater) ? threshold : -threshold;
            t.AddCondition(mode, val, param);
        }
    }

    /// <summary>Transition sang fragGet (GrenadeID==1) và flashGet (GrenadeID==2) khi IsThrow.</summary>
    private void AddGrenadeTrans(AnimatorState from, AnimatorState fragGet, AnimatorState flashGet)
    {
        {
            var t = from.AddTransition(fragGet);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.Equals, 1, "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,     0, "IsThrow");
        }
        {
            var t = from.AddTransition(flashGet);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.Equals, 2, "GrenadeID");
            t.AddCondition(AnimatorConditionMode.If,     0, "IsThrow");
        }
    }
}
#endif