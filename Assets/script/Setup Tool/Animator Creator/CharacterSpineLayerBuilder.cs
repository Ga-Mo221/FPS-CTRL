// =============================================================
//  CharacterSpineLayerBuilder.cs  —  Spine layer: Character mode
//
//  Khác với SpineLayerBuilder (weapon mode) ở chỗ:
//    • RELOAD    → SSM "RELOAD"    — tạo RỖNG (không state, không transition)
//    • SWITCH OUT → SSM "SWITCH OUT" — tạo RỖNG
//    • SWITCH IN  → SSM "SWITCH IN"  — tạo RỖNG
//
//  State và transition bên trong 3 SSM trên được inject sau bởi
//  WeaponClipsTab khi người dùng nhấn "Add Weapon Entry":
//    RELOAD   W{n} : Entry(WeaponIndex==n) ; Exit(0.905,0.2)→NULL ;
//                    IsDead/IsGrouded-false/IsSliding→NULL
//    SWITCH OUT W{n} : Entry(WeaponIndex==n) ; Exit(1,0)→NULL ; IsDead→NULL
//    SWITCH IN  W{n} : speed=-1 ; Entry(WeaponIndex==n) ; Exit(0.75,0.25)→NULL
//
//  Transition logic ở root:
//    IsDead → SSM SWITCH IN  : IsDead==false  (hồi sinh)
//    NULL   → SSM SWITCH OUT : IsSwitching==true
//    NULL   → SSM RELOAD     : IsReloading==true (+ MoveX/Y range)
//    SWITCH OUT / SWITCH IN → NULL : qua Exit của mỗi SSM (inject bởi WeaponClipsTab)
//
//  Parameter "WeaponIndex" (Int) được tự thêm nếu chưa có.
// =============================================================

#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

public class CharacterSpineLayerBuilder : LayerBuilder
{
    public override void Build(AnimatorController ac, int layerIndex)
    {
        EnsureWeaponIndexParam(ac);

        ac.AddLayer("Spine");
        SetLayerWeight(ac, layerIndex, 1f);

        var root  = ac.layers[layerIndex].stateMachine;
        root.name = "Spine";

        // ── Bare states ───────────────────────────────────────
        var isDead = AddState(root, "IsDead", new Vector3(380, 120));
        isDead.writeDefaultValues = true;

        var nullS = AddState(root, "NULL", new Vector3(680, 120));
        nullS.writeDefaultValues = true;

        // ── SSM: RELOAD / SWITCH OUT / SWITCH IN — tạo RỖNG ──
        // State và transition được inject sau bởi WeaponClipsTab.
        root.AddStateMachine("RELOAD",     new Vector3(1140, 220));
        root.AddStateMachine("SWITCH OUT", new Vector3(990,   30));
        root.AddStateMachine("SWITCH IN",  new Vector3(990,  -80));

        // ── SSM: ADS ──────────────────────────────────────────
        var adsSM    = root.AddStateMachine("ADS",     new Vector3(940, 220));
        var adsState = AddStateAndAssignClip(adsSM, "ADS",        new Vector3(390,  30));
        var crAds    = AddStateAndAssignClip(adsSM, "CROUCH ADS", new Vector3(390, 170));
        adsSM.defaultState = adsState;

        // ── SSM: GRENADE ──────────────────────────────────────
        var gSM        = root.AddStateMachine("GRENADE", new Vector3(990, 310));
        var fragGet    = AddStateAndAssignClip(gSM, "FRAG GET",    new Vector3(490, 110));
        var fragIdle   = AddStateAndAssignClip(gSM, "FRAG IDLE",   new Vector3(490, 200));
        var fragThrow  = AddStateAndAssignClip(gSM, "FRAG THROW",  new Vector3(490, 280));
        var flashGet   = AddStateAndAssignClip(gSM, "FLASH GET",   new Vector3(490, 370));
        var flashIdle  = AddStateAndAssignClip(gSM, "FLASH IDLE",  new Vector3(490, 450));
        var flashThrow = AddStateAndAssignClip(gSM, "FLASH THROW", new Vector3(490, 530));
        gSM.defaultState = fragGet;

        // ── Grenade internal transitions ──────────────────────
        AddExitTrans(fragGet,  fragIdle,  0.9f, 0.114f);
        AddExitTrans(flashGet, flashIdle, 0.9f, 0.056f);
        BuildFragIdleTransitions(fragIdle,  nullS, fragThrow);
        BuildFlashIdleTransitions(flashIdle, nullS, flashThrow);
        AddExitTrans(fragThrow,  nullS, 0.9f, 0.068f);
        AddExitTrans(flashThrow, nullS, 0.9f, 0.075f);

        root.defaultState = isDead;

        // ── NULL → IsDead ─────────────────────────────────────
        AddBoolTrans(nullS, isDead, "IsDead", true, 0f);

        // ── NULL → ADS / GRENADE ──────────────────────────────
        // Transition vào SSM RELOAD / SWITCH OUT / SWITCH IN
        // được inject sau bởi WeaponClipsTab, không tạo ở đây.
        BuildNullTransitions(nullS, adsState, crAds, fragGet, flashGet);

        // ── ADS ↔ CROUCH ADS ──────────────────────────────────
        BuildAdsTransitions(adsState, nullS, crAds, fragGet, flashGet);
        BuildCrouchAdsTransitions(crAds, nullS, adsState, fragGet, flashGet);
    }

    // =========================================================
    // NULL TRANSITIONS
    // =========================================================
    private void BuildNullTransitions(
        AnimatorState nullS,
        AnimatorState adsState, AnimatorState crAds,
        AnimatorState fragGet, AnimatorState flashGet)
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

    // =========================================================
    // ADS TRANSITIONS
    // =========================================================
    private void BuildAdsTransitions(
        AnimatorState adsState, AnimatorState nullS, AnimatorState crAds,
        AnimatorState fragGet, AnimatorState flashGet)
    {
        AddBoolTrans(adsState, nullS,   "IsDead",    true,  0.25f);
        AddBoolTrans(adsState, nullS,   "ADS",       false, 0.25f);
        AddBoolTrans(adsState, nullS,   "IsGrouded", false, 0.25f);
        AddBoolTrans(adsState, nullS,   "IsSliding", true,  0.25f);
        AddMoveOutTrans(adsState, nullS, 0.3f);
        {
            var t = adsState.AddTransition(crAds);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }
        AddGrenadeTrans(adsState, fragGet, flashGet);
    }

    // =========================================================
    // CROUCH ADS TRANSITIONS
    // =========================================================
    private void BuildCrouchAdsTransitions(
        AnimatorState crAds, AnimatorState nullS, AnimatorState adsState,
        AnimatorState fragGet, AnimatorState flashGet)
    {
        AddBoolTrans(crAds, nullS,    "IsDead",      true,  0.25f);
        AddBoolTrans(crAds, adsState, "IsCrouching", false, 0.25f);
        AddBoolTrans(crAds, nullS,    "ADS",         false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsGrouded",   false, 0.25f);
        AddBoolTrans(crAds, nullS,    "IsSliding",   true,  0.25f);
        AddMoveOutTrans(crAds, nullS, 0.3f);
        AddGrenadeTrans(crAds, fragGet, flashGet);
    }

    // =========================================================
    // GRENADE IDLE TRANSITIONS
    // =========================================================
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

    // =========================================================
    // HELPERS
    // =========================================================
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
            float val = mode == AnimatorConditionMode.Greater ? threshold : -threshold;
            t.AddCondition(mode, val, param);
        }
    }

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

    private static void EnsureWeaponIndexParam(AnimatorController ac)
    {
        foreach (var p in ac.parameters)
            if (p.name == "WeaponIndex") return;
        ac.AddParameter(new AnimatorControllerParameter
        {
            name       = "WeaponIndex",
            type       = AnimatorControllerParameterType.Int,
            defaultInt = 0,
        });
    }


}
#endif