// =============================================================
//  FullBodyLayerBuilder.cs  —  Layer 0: Full Body
//  Toàn bộ logic cho layer "Full Body":
//    • Data: StandLabels, StandChildren, CrouchLabels, CrouchChildren
//    • Build: Sub-machine Locomoion (Stand/Crouch BlendTree 2D)
//    • Build: States IsDead, NULL, JUMP UP/AIR/PLAN, SLIDE IN/SLIDING/OUT
//    • Auto-fill: AutoFillStandClips, AutoFillCrouchClips
// =============================================================

#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEngine;

public class FullBodyLayerBuilder : LayerBuilder
{
    // ── Clip arrays (gán tay từ UI hoặc auto-fill) ────────────
    public AnimationClip[] standClips  = new AnimationClip[21];
    public AnimationClip[] crouchClips = new AnimationClip[17];

    // ─────────────────────────────────────────────────────────
    // STAND BLEND TREE DATA
    // ─────────────────────────────────────────────────────────
    public static readonly string[] StandLabels = new[]
    {
        /* [0]  */ "Idle Gun",
        /* [1]  */ "ADS Walk Forward",
        /* [2]  */ "ADS Walk Forward Right",
        /* [3]  */ "ADS Walk Right",
        /* [4]  */ "ADS Walk Back Right",
        /* [5]  */ "ADS Walk Back",
        /* [6]  */ "ADS Walk Back Left",
        /* [7]  */ "ADS Walk Left",
        /* [8]  */ "ADS Walk Forward Left",
        /* [9]  */ "Walk Forward",
        /* [10] */ "Walk Forward Right",
        /* [11] */ "Walk Right",
        /* [12] */ "Walk Back Right",
        /* [13] */ "Walk Back",
        /* [14] */ "Walk Back Left",
        /* [15] */ "Walk Left",
        /* [16] */ "Walk Forward Left",
        /* [17] */ "Run Forward",
        /* [18] */ "Run Forward Right",
        /* [19] */ "Run Forward Left",
        /* [20] */ "Sprint Forward",
    };

    private static readonly BTChild[] StandChildren = new[]
    {
        new BTChild { px= 0f,    py= 0f,   timeScale= 1f  }, // [0]  Idle Gun
        // ADS Walk (Pos 0.2) — 8 hướng
        new BTChild { px= 0f,    py= 0.2f, timeScale= 1f  }, // [1]  ADS Walk Forward
        new BTChild { px= 0.2f,  py= 0.2f, timeScale= 1f  }, // [2]  ADS Walk Forward Right
        new BTChild { px= 0.2f,  py= 0f,   timeScale= 1f  }, // [3]  ADS Walk Right
        new BTChild { px= 0.2f,  py=-0.2f, timeScale=-1f  }, // [4]  ADS Walk Back Right
        new BTChild { px= 0f,    py=-0.2f, timeScale= 1f  }, // [5]  ADS Walk Back
        new BTChild { px=-0.2f,  py=-0.2f, timeScale=-1f  }, // [6]  ADS Walk Back Left
        new BTChild { px=-0.2f,  py= 0f,   timeScale=-1f  }, // [7]  ADS Walk Left
        new BTChild { px=-0.2f,  py= 0.2f, timeScale= 1f  }, // [8]  ADS Walk Forward Left
        // Walk (Pos 0.4) — 8 hướng
        new BTChild { px= 0f,    py= 0.4f, timeScale= 1f  }, // [9]  Walk Forward
        new BTChild { px= 0.4f,  py= 0.4f, timeScale= 1f  }, // [10] Walk Forward Right
        new BTChild { px= 0.4f,  py= 0f,   timeScale= 1f  }, // [11] Walk Right
        new BTChild { px= 0.4f,  py=-0.4f, timeScale=-1f  }, // [12] Walk Back Right
        new BTChild { px= 0f,    py=-0.4f, timeScale=-1f  }, // [13] Walk Back
        new BTChild { px=-0.4f,  py=-0.4f, timeScale=-1f  }, // [14] Walk Back Left
        new BTChild { px=-0.4f,  py= 0f,   timeScale=-1f  }, // [15] Walk Left
        new BTChild { px=-0.4f,  py= 0.4f, timeScale= 1f  }, // [16] Walk Forward Left
        // Run (Pos 0.6) — 3 hướng
        new BTChild { px= 0f,    py= 0.6f, timeScale= 1f  }, // [17] Run Forward
        new BTChild { px= 0.6f,  py= 0.6f, timeScale= 1f  }, // [18] Run Forward Right
        new BTChild { px=-0.6f,  py= 0.6f, timeScale= 1f  }, // [19] Run Forward Left
        // Sprint (Pos 1.0) — 1 hướng
        new BTChild { px= 0f,    py= 1f,   timeScale= 1f  }, // [20] Sprint Forward
    };

    // ─────────────────────────────────────────────────────────
    // CROUCH BLEND TREE DATA
    // ─────────────────────────────────────────────────────────
    // Cấu trúc: IDLE(1) → CROUCH WALK slow 8 hướng (pos 0.2) → CROUCH WALK fast 8 hướng (pos 0.4)
    public static readonly string[] CrouchLabels = new[]
    {
        /* [0]  */ "Crouch Idle",
        /* [1]  */ "Crouch Walk Forward (slow)",
        /* [2]  */ "Crouch Walk Forward Right (slow)",
        /* [3]  */ "Crouch Walk Right (slow)",
        /* [4]  */ "Crouch Walk Back Right (slow)",
        /* [5]  */ "Crouch Walk Back (slow)",
        /* [6]  */ "Crouch Walk Back Left (slow)",
        /* [7]  */ "Crouch Walk Left (slow)",
        /* [8]  */ "Crouch Walk Forward Left (slow)",
        /* [9]  */ "Crouch Walk Forward (fast)",
        /* [10] */ "Crouch Walk Forward Right (fast)",
        /* [11] */ "Crouch Walk Right (fast)",
        /* [12] */ "Crouch Walk Back Right (fast)",
        /* [13] */ "Crouch Walk Back (fast)",
        /* [14] */ "Crouch Walk Back Left (fast)",
        /* [15] */ "Crouch Walk Left (fast)",
        /* [16] */ "Crouch Walk Forward Left (fast)",
    };

    private static readonly BTChild[] CrouchChildren = new[]
    {
        new BTChild { px= 0f,   py= 0f,   timeScale= 1f   }, // [0]  Crouch Idle
        // Crouch Walk slow (pos 0.2) — 8 hướng, timeScale ±0.6
        new BTChild { px= 0f,   py= 0.2f, timeScale= 0.6f }, // [1]  Crouch Walk Forward slow
        new BTChild { px= 0.2f, py= 0.2f, timeScale= 0.6f }, // [2]  Crouch Walk Forward Right slow
        new BTChild { px= 0.2f, py= 0f,   timeScale= 0.6f }, // [3]  Crouch Walk Right slow
        new BTChild { px= 0.2f, py=-0.2f, timeScale=-0.6f }, // [4]  Crouch Walk Back Right slow
        new BTChild { px= 0f,   py=-0.2f, timeScale=-0.6f }, // [5]  Crouch Walk Back slow
        new BTChild { px=-0.2f, py=-0.2f, timeScale=-0.6f }, // [6]  Crouch Walk Back Left slow
        new BTChild { px=-0.2f, py= 0f,   timeScale=-0.6f }, // [7]  Crouch Walk Left slow
        new BTChild { px=-0.2f, py= 0.2f, timeScale= 0.6f }, // [8]  Crouch Walk Forward Left slow
        // Crouch Walk fast (pos 0.4) — 8 hướng, timeScale ±1
        new BTChild { px= 0f,   py= 0.4f, timeScale= 1f   }, // [9]  Crouch Walk Forward fast
        new BTChild { px= 0.4f, py= 0.4f, timeScale= 1f   }, // [10] Crouch Walk Forward Right fast
        new BTChild { px= 0.4f, py= 0f,   timeScale= 1f   }, // [11] Crouch Walk Right fast
        new BTChild { px= 0.4f, py=-0.4f, timeScale=-1f   }, // [12] Crouch Walk Back Right fast
        new BTChild { px= 0f,   py=-0.4f, timeScale=-1f   }, // [13] Crouch Walk Back fast
        new BTChild { px=-0.4f, py=-0.4f, timeScale=-1f   }, // [14] Crouch Walk Back Left fast
        new BTChild { px=-0.4f, py= 0f,   timeScale=-1f   }, // [15] Crouch Walk Left fast
        new BTChild { px=-0.4f, py= 0.4f, timeScale= 1f   }, // [16] Crouch Walk Forward Left fast
    };

    // ─────────────────────────────────────────────────────────
    // BUILD
    // ─────────────────────────────────────────────────────────
    public override void Build(AnimatorController ac, int layerIndex)
    {
        var layers = ac.layers;
        layers[layerIndex].name = "Full Body";
        ac.layers = layers;

        var root  = ac.layers[layerIndex].stateMachine;
        root.name = "Full Body";

        // Sub-machine: Locomoion
        var locoSM = root.AddStateMachine("Locomoion", new Vector3(1000, 120));

        AutoFillStandClips();
        var standState  = locoSM.AddState("Stand",   new Vector3(400,   0));
        standState.motion = BuildBlendTree2D(ac, "Stand BlendTree",  StandChildren,  standClips);

        AutoFillCrouchClips();
        // Slot [9–16] (fast) dùng cùng clip với slot [1–8] (slow), chỉ khác pos/timeScale
        for (int i = 0; i < 8; i++)
        {
            if (crouchClips[9 + i] == null)
                crouchClips[9 + i] = crouchClips[1 + i];
        }
        var crouchState = locoSM.AddState("Crounch", new Vector3(400, 150));
        crouchState.motion = BuildBlendTree2D(ac, "Crouch BlendTree", CrouchChildren, crouchClips);

        locoSM.defaultState = standState;

        // Sub-machine: Dead (placeholder, mở rộng sau)
        root.AddStateMachine("Dead", new Vector3(1300, 120));

        // Bare states
        var isDead    = AddState(root, "IsDead",    new Vector3(360,  120)); isDead.writeDefaultValues    = true;
        var nullState = AddState(root, "NULL",      new Vector3(700,  120)); nullState.writeDefaultValues = true;

        var jumpUp    = AddStateAndAssignClip(root, "JUMP UP",   new Vector3(700,  -10));
        var jumpAir   = AddStateAndAssignClip(root, "JUMP AIR",  new Vector3(1000, -50));
        var jumpPlan  = AddStateAndAssignClip(root, "JUMP PLAN", new Vector3(1300, -10));
        var slideIn   = AddStateAndAssignClip(root, "SLIDE IN",  new Vector3(700,  250));
        var sliding   = AddStateAndAssignClip(root, "SLIDING",   new Vector3(1000, 290));
        var slideOut  = AddStateAndAssignClip(root, "SLIDE OUT", new Vector3(1300, 250));

        root.defaultState = isDead;

        BuildTransitions(
            nullState, standState, crouchState,
            isDead, jumpUp, jumpAir, jumpPlan,
            slideIn, sliding, slideOut);
    }

    // ─────────────────────────────────────────────────────────
    // TRANSITIONS
    // ─────────────────────────────────────────────────────────
    private void BuildTransitions(
        AnimatorState nullState, AnimatorState standState, AnimatorState crouchState,
        AnimatorState isDead,
        AnimatorState jumpUp,   AnimatorState jumpAir, AnimatorState jumpPlan,
        AnimatorState slideIn,  AnimatorState sliding,  AnimatorState slideOut)
    {
        AddBoolTrans(isDead, nullState, "IsDead", false, 0f);

        {
            var t = nullState.AddTransition(standState);
            SetTrans(t, 0f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsGrouded");
        }
        AddBoolTrans(nullState, jumpAir, "IsGrouded", false, 0f);

        {
            var t = standState.AddTransition(crouchState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If,   0,    "IsCrouching");
            t.AddCondition(AnimatorConditionMode.Less, 0.9f, "MoveY");
        }
        AddBoolTrans(standState, jumpAir, "IsGrouded", false, 0.25f);
        {
            var t = sliding.AddTransition(jumpUp);
            SetTrans(t, 0.1f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        }
        {
            var t = sliding.AddTransition(slideOut);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }
        {
            var t = sliding.AddTransition(slideOut);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.Greater, 0.5f, "MoveY");
        }
        {
            var t = standState.AddTransition(slideIn);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
        }

        AddBoolTrans(crouchState, standState, "IsCrouching", false, 0.25f);
        AddBoolTrans(crouchState, jumpAir,    "IsGrouded",   false, 0.25f);

        AddExitTrans(jumpUp, jumpAir, 0.397f, 0.139f);
        {
            var t = jumpAir.AddTransition(jumpPlan);
            SetTrans(t, 0.019f, false);
            t.AddCondition(AnimatorConditionMode.If,    0, "IsGrouded");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        }
        {
            var t = jumpAir.AddTransition(crouchState);
            SetTrans(t, 0.25f, false);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsGrouded");
            t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        }

        AddExitTrans(jumpPlan,  standState,  0.75f, 0.25f);
        AddExitTrans(slideIn,   sliding,     1f,    0.25f);
        //AddBoolTrans(sliding,   slideOut,    "IsSliding",  false, 0.25f);
        //AddTriggerTrans(sliding, jumpUp,     "IsJumping",  0.25f);
        AddBoolTrans(sliding,   jumpAir,     "IsGrouded",  false, 0.25f);
        AddExitTrans(slideOut,  crouchState, 0f, 0.25f);
    }

    // ─────────────────────────────────────────────────────────
    // AUTO-FILL STAND BLEND TREE CLIPS
    //
    // Thuật toán:
    //   1. Strip tiền tố trước '_' đầu tiên → "body"
    //      VD: "Gost_ADS WALK FORWARD" → body = "ADS WALK FORWARD"
    //   2. Từ đầu tiên của body xác định NHÓM:
    //      "IDLE"   → slot [0]
    //      "ADS"    → slots [1–8]   (ADS WALK 8 hướng)
    //      "WALK"   → slots [9–16]  (WALK 8 hướng)
    //      "RUN"    → slots [17–19] (RUN 3 hướng)
    //      "SPRINT" → slot [20]
    //   3. Chỉ điền slot nào standClips[slot] == null (chưa gán tay).
    // ─────────────────────────────────────────────────────────
    public void AutoFillStandClips()
    {
        if (singleClips == null) return;

        foreach (var clip in singleClips)
        {
            if (clip == null) continue;

            string raw   = clip.name;
            int    sep   = raw.IndexOf('_');
            string body  = (sep >= 0 ? raw.Substring(sep + 1) : raw).ToUpper().Trim();
            string[] words = body.Split(new char[]{' '}, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            string first = words[0];

            if (first == "IDLE")
            {
                if (standClips[0] == null) standClips[0] = clip;
                continue;
            }

            if (first == "ADS")
            {
                int offset = GetDirectionSlot8(words, 2); // bỏ "ADS" và "WALK"
                if (offset >= 0 && standClips[1 + offset] == null)
                    standClips[1 + offset] = clip;
                continue;
            }

            if (first == "WALK")
            {
                int offset = GetDirectionSlot8(words, 1); // bỏ "WALK"
                if (offset >= 0 && standClips[9 + offset] == null)
                    standClips[9 + offset] = clip;
                continue;
            }

            if (first == "RUN")
            {
                int offset = GetDirectionSlot3Run(words, 1); // bỏ "RUN"
                if (offset >= 0 && standClips[17 + offset] == null)
                    standClips[17 + offset] = clip;
                continue;
            }

            if (first == "SPRINT")
            {
                if (standClips[20] == null) standClips[20] = clip;
                continue;
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // AUTO-FILL CROUCH BLEND TREE CLIPS
    //
    // Chỉ fill slow group [1–8] từ tên clip.
    // Fast group [9–16] được copy tự động từ slow group trong Build() —
    // cùng clip nhưng pos 0.4 và timeScale ±1 (thay vì 0.2/±0.6).
    //
    // Thuật toán:
    //   1. Strip tiền tố trước '_' đầu tiên → "body"
    //   2. Phải bắt đầu bằng "CROUCH"
    //   3. CROUCH IDLE → slot [0]
    //   4. CROUCH WALK <DIR> → slow group, slots [1–8]
    //   5. Bỏ qua clip có suffix "FAST" (không cần clip riêng)
    //   6. Chỉ điền slot nào crouchClips[slot] == null (chưa gán tay).
    // ─────────────────────────────────────────────────────────
    public void AutoFillCrouchClips()
    {
        if (singleClips == null) return;

        foreach (var clip in singleClips)
        {
            if (clip == null) continue;

            string raw  = clip.name;
            int    sep  = raw.IndexOf('_');
            string body = (sep >= 0 ? raw.Substring(sep + 1) : raw).ToUpper().Trim();
            string[] words = body.Split(new char[]{' '}, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words[0] != "CROUCH") continue;

            // CROUCH IDLE → slot [0]
            if (words[1] == "IDLE")
            {
                if (crouchClips[0] == null) crouchClips[0] = clip;
                continue;
            }

            // Phải có "WALK" ở từ thứ hai
            if (words[1] != "WALK") continue;

            // Chỉ fill slow group [1–8]; fast group [9–16] sẽ được copy từ slow sau khi fill xong
            // Bỏ qua từ cuối nếu là "FAST" (clip fast riêng nếu có, không ghi đè slow)
            string lastWord = words[words.Length - 1];
            if (lastWord == "FAST") continue;

            int offset = GetDirectionSlot8(words, 2); // bỏ "CROUCH WALK"
            if (offset < 0) continue;

            int slot = 1 + offset; // slots [1–8]
            if (crouchClips[slot] == null) crouchClips[slot] = clip;
        }
    }

    // ─────────────────────────────────────────────────────────
    // DIRECTION HELPERS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Map chuỗi hướng thành index 0–7 (theo chiều kim đồng hồ từ FORWARD).
    /// Đọc từ <paramref name="startIdx"/> đến <paramref name="endIdx"/> (exclusive).
    /// </summary>
    private static int GetDirectionSlot8(string[] words, int startIdx, int endIdx = -1)
    {
        if (startIdx >= words.Length) return -1;
        if (endIdx < 0) endIdx = words.Length;

        var sb = new System.Text.StringBuilder();
        for (int i = startIdx; i < endIdx; i++)
        {
            if (i > startIdx) sb.Append(' ');
            sb.Append(words[i]);
        }
        string dir = sb.ToString();

        switch (dir)
        {
            case "FORWARD":
            case "FORWARED":          return 0; // typo trong tên asset Gost
            case "FORWARD RIGHT":
            case "FORWARED RIGHT":    return 1;
            case "RIGHT":             return 2;
            case "BACK RIGHT":        return 3;
            case "BACK":              return 4;
            case "BACK LEFT":         return 5;
            case "LEFT":              return 6;
            case "FORWARD LEFT":
            case "FORWARED LEFT":     return 7;
            default:                  return -1;
        }
    }

    /// <summary>Map chuỗi hướng RUN thành index 0–2 (FORWARD / FORWARD RIGHT / FORWARD LEFT).</summary>
    private static int GetDirectionSlot3Run(string[] words, int startIdx)
    {
        if (startIdx >= words.Length) return -1;

        var sb = new System.Text.StringBuilder();
        for (int i = startIdx; i < words.Length; i++)
        {
            if (i > startIdx) sb.Append(' ');
            sb.Append(words[i]);
        }
        string dir = sb.ToString();

        switch (dir)
        {
            case "FORWARD":
            case "FORWARED":         return 0;
            case "FORWARD RIGHT":
            case "FORWARED RIGHT":   return 1;
            case "FORWARD LEFT":
            case "FORWARED LEFT":    return 2;
            default:                 return -1;
        }
    }
}
#endif