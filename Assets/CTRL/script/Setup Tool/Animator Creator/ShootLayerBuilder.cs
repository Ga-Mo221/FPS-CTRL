// =============================================================
//  ShootLayerBuilder.cs  —  Layer 1: Shoot
//  States: IsDead, NULL, SHOOT
//  Transitions: IsShooting, IsReloading, IsThrow, IsSwitching, GrenadeID
// =============================================================

#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

public class ShootLayerBuilder : LayerBuilder
{
    public override void Build(AnimatorController ac, int layerIndex)
    {
        ac.AddLayer("Shoot");
        SetLayerWeight(ac, layerIndex, 1f);

        var root  = ac.layers[layerIndex].stateMachine;
        root.name = "Shoot";

        var isDead = AddState(root, "IsDead", new Vector3(350, 120)); isDead.writeDefaultValues = true;
        var nullS  = AddState(root, "NULL",   new Vector3(640, 120)); nullS.writeDefaultValues  = true;
        var shoot  = AddStateAndAssignClip(root, "SHOOT", new Vector3(920, 120));

        root.defaultState = isDead;

        // IsDead ↔ NULL
        AddBoolTrans(isDead, nullS,  "IsDead", false, 0f);
        AddBoolTrans(nullS,  isDead, "IsDead", true,  0f);

        // NULL → SHOOT
        {
            var t = nullS.AddTransition(shoot);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.Equals, 0, "GrenadeID");
            t.AddCondition(AnimatorConditionMode.IfNot,  0, "IsReloading");
            t.AddCondition(AnimatorConditionMode.If,     0, "IsShooting");   // Trigger
            t.AddCondition(AnimatorConditionMode.IfNot,  0, "IsSwitching");
        }

        // SHOOT → NULL : phát hết clip, không cần condition (List is Empty)
        {
            var t = shoot.AddTransition(nullS);
            SetTrans(t, 0f, true, 1f);
        }
        AddBoolTrans(shoot, nullS, "IsDead",       true, 0.25f);
        AddBoolTrans(shoot, nullS, "IsReloading",  true, 0.25f);
        AddBoolTrans(shoot, nullS, "IsThrow",      true, 0.25f);
        AddBoolTrans(shoot, nullS, "IsSwitching",  true, 0.25f);
    }
}
#endif