// =============================================================
//  DeadLayerBuilder.cs  —  Layer 3: Dead
//  States: NULL, DEAD STAND, DEAD CROUCH, DEAD AIR, DEAD SLIDE
//
//  NULL → DEAD STAND  : IsDead==true, IsGrouded==true,  IsCrouching==false, IsSliding==false
//  NULL → DEAD CROUCH : IsDead==true, IsGrouded==true,  IsCrouching==true,  IsSliding==false
//  NULL → DEAD AIR   : IsDead==true, IsGrouded==false
//  NULL → DEAD SLIDE  : IsDead==true, IsGrouded==true,  IsCrouching==true,  IsSliding==true
//
//  DEAD * → NULL : hasExitTime=true, exitTime=1, duration=0  (phát hết clip rồi mới về)
//
//  Clip detection: giống FullBodyLayerBuilder — strip tiền tố trước '_' đầu tiên,
//  normalize '_' thành space, match theo từ khóa DEAD STAND / DEAD CROUCH /
//  DEAD AIR / DEAD SLIDE (không phân biệt hoa thường).
//
//  Layer weight: 1, default state: NULL
// =============================================================

#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

public class DeadLayerBuilder : LayerBuilder
{
    // ── Clip slots (fill bởi AutoFillDeadClips, hoặc gán tay từ UI) ──
    public AnimationClip clipDeadStand;
    public AnimationClip clipDeadCrouch;
    public AnimationClip clipDeadAir;
    public AnimationClip clipDeadSlide;

    // ── Keyword map ───────────────────────────────────────────
    // Body sau khi strip prefix và normalize '_' → space phải khớp
    // với một trong các key sau (ToUpper).
    private const string KEY_STAND  = "DEAD STAND";
    private const string KEY_CROUCH = "DEAD CROUCH";
    private const string KEY_AIR   = "DEAD AIR";
    private const string KEY_SLIDE  = "DEAD SLIDE";

    public override void Build(AnimatorController ac, int layerIndex)
    {
        // Auto-fill clips từ singleClips trước khi build
        AutoFillDeadClips();

        // ── Thêm layer ────────────────────────────────────────
        ac.AddLayer("Dead");
        SetLayerWeight(ac, layerIndex, 1f);

        var root  = ac.layers[layerIndex].stateMachine;
        root.name = "Dead";

        // ── States ────────────────────────────────────────────
        var nullS      = AddState(root, "NULL",       new Vector3(350, 120));
        nullS.writeDefaultValues = true;

        var deadStand  = AddState(root, "DEAD STAND",  new Vector3(700,   0));
        var deadCrouch = AddState(root, "DEAD CROUCH", new Vector3(700, 120));
        var deadAir   = AddState(root, "DEAD AIR",   new Vector3(700, 240));
        var deadSlide  = AddState(root, "DEAD SLIDE",  new Vector3(700, 360));

        // Gán clip
        if (clipDeadStand  != null) deadStand.motion  = clipDeadStand;
        if (clipDeadCrouch != null) deadCrouch.motion = clipDeadCrouch;
        if (clipDeadAir   != null) deadAir.motion   = clipDeadAir;
        if (clipDeadSlide  != null) deadSlide.motion  = clipDeadSlide;

        root.defaultState = nullS;

        // ── NULL → DEAD STAND ─────────────────────────────────
        {
            var t = nullS.AddTransition(deadStand);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,    0, "IsDead");
            t.AddCondition(AnimatorConditionMode.If,    0, "IsGrouded");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
        }

        // ── NULL → DEAD CROUCH ────────────────────────────────
        {
            var t = nullS.AddTransition(deadCrouch);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,    0, "IsDead");
            t.AddCondition(AnimatorConditionMode.If,    0, "IsGrouded");
            t.AddCondition(AnimatorConditionMode.If,    0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
        }

        // ── NULL → DEAD AIR ──────────────────────────────────
        {
            var t = nullS.AddTransition(deadAir);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If,    0, "IsDead");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrouded");
        }

        // ── NULL → DEAD SLIDE ─────────────────────────────────
        {
            var t = nullS.AddTransition(deadSlide);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsGrouded");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
        }

        // ── Dead states → NULL : phát hết clip VÀ IsDead==false mới về ──
        foreach (var deadState in new[] { deadStand, deadCrouch, deadAir, deadSlide })
        {
            var t = deadState.AddTransition(nullS);
            SetTrans(t, 0f, true, 1f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        }
    }

    // ─────────────────────────────────────────────────────────
    // AUTO-FILL DEAD CLIPS
    //
    // Thuật toán (nhất quán với FullBodyLayerBuilder):
    //   1. Strip tiền tố trước '_' đầu tiên  →  body
    //   2. Normalize '_' → space, ToUpper     →  key
    //   3. Match chính xác với KEY_STAND / KEY_CROUCH / KEY_AIR / KEY_SLIDE
    //   4. Chỉ fill slot nào đang null (clip gán tay được ưu tiên)
    //
    // Ví dụ tên clip hợp lệ:
    //   "M4_DEAD_STAND"  → body="DEAD_STAND" → key="DEAD STAND"  → clipDeadStand
    //   "DEAD_CROUCH"    → body="DEAD_CROUCH" → key="DEAD CROUCH" → clipDeadCrouch
    //   "Gost_Dead_Air" → body="DEAD_AIR"  → key="DEAD AIR"   → clipDeadAir
    // ─────────────────────────────────────────────────────────
    public void AutoFillDeadClips()
    {
        if (singleClips == null) return;

        foreach (var clip in singleClips)
        {
            if (clip == null) continue;

            string raw = clip.name;
            int    sep = raw.IndexOf('_');
            string body = (sep >= 0 ? raw.Substring(sep + 1) : raw)
                          .Replace('_', ' ')
                          .ToUpper()
                          .Trim();

            switch (body)
            {
                case KEY_STAND:
                    if (clipDeadStand  == null) clipDeadStand  = clip;
                    break;
                case KEY_CROUCH:
                    if (clipDeadCrouch == null) clipDeadCrouch = clip;
                    break;
                case KEY_AIR:
                    if (clipDeadAir   == null) clipDeadAir   = clip;
                    break;
                case KEY_SLIDE:
                    if (clipDeadSlide  == null) clipDeadSlide  = clip;
                    break;
            }
        }
    }
}
#endif