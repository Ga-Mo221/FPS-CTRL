// =============================================================
//  LayerBuilder.cs
//  Abstract base class cho tất cả Layer Builders.
//  Mỗi layer (Full Body, Shoot, Spine) sẽ kế thừa class này.
// =============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Base class chứa các helper dùng chung cho mọi layer builder.
/// Kế thừa class này để tạo builder cho từng layer.
/// </summary>
public abstract class LayerBuilder
{
    // ── Shared clip sources (inject từ EditorWindow) ──────────
    protected List<AnimationClip> singleClips;

    public void Init(List<AnimationClip> singleClips)
    {
        this.singleClips = singleClips;
    }

    // ── Entry point ───────────────────────────────────────────
    /// <summary>Xây dựng toàn bộ layer tại index <paramref name="layerIndex"/>.</summary>
    public abstract void Build(AnimatorController ac, int layerIndex);

    // ─────────────────────────────────────────────────────────
    // SHARED HELPERS (dùng được ở mọi layer builder)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Thêm state vào StateMachine và tự động map AnimationClip từ singleClips theo tên.
    /// Bỏ qua "IsDead" và "NULL".
    /// </summary>
    protected AnimatorState AddStateAndAssignClip(AnimatorStateMachine sm, string name, Vector3 pos, string clipKey = null)
    {
        var s = sm.AddState(name, pos);
        s.writeDefaultValues = false;

        string upperName = name.ToUpper().Trim();
        if (upperName == "ISDEAD" || upperName == "NULL")
            return s;

        // clipKey cho phép chỉ định từ khóa tìm clip khác với tên state,
        // dùng khi tên state có hậu tố (VD: "SWITCH IN"/"SWITCH OUT") nhưng
        // clip lại đặt tên theo tiền tố khác (VD: "M4_SWITCH") — nếu không có
        // clipKey, searchKey mặc định sẽ lấy theo tên state như cũ.
        string searchKey = (clipKey ?? name).ToLower().Trim();
        AnimationClip matchedClip = null;

        if (singleClips != null)
        {
            foreach (var clip in singleClips)
            {
                if (clip == null) continue;
                string clipName = clip.name.ToLower();
                if (clipName == searchKey || clipName.Contains(searchKey) || searchKey.Contains(clipName))
                {
                    matchedClip = clip;
                    break;
                }
            }
        }

        if (matchedClip != null)
            s.motion = matchedClip;

        return s;
    }

    protected static AnimatorState AddState(AnimatorStateMachine sm, string name, Vector3 pos)
    {
        var s = sm.AddState(name, pos);
        s.writeDefaultValues = false;
        return s;
    }

    protected static void SetTrans(AnimatorStateTransition t, float duration, bool hasExitTime, float exitTime = 0.75f)
    {
        t.duration            = duration;
        t.hasExitTime         = hasExitTime;
        t.exitTime            = exitTime;
        t.hasFixedDuration    = true;
        t.offset              = 0;
        t.canTransitionToSelf = true;
    }

    protected static void AddBoolTrans(AnimatorState from, AnimatorState to, string param, bool value, float dur)
    {
        var t = from.AddTransition(to);
        SetTrans(t, dur, false);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    protected static void AddTriggerTrans(AnimatorState from, AnimatorState to, string param, float dur)
    {
        var t = from.AddTransition(to);
        SetTrans(t, dur, false);
        t.AddCondition(AnimatorConditionMode.If, 0, param);
    }

    protected static void AddExitTrans(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        var t = from.AddTransition(to);
        SetTrans(t, duration, true, exitTime);
    }

    protected static void SetLayerWeight(AnimatorController ac, int idx, float w)
    {
        var ls = ac.layers;
        ls[idx].defaultWeight = w;
        ac.layers = ls;
    }

    protected static BlendTree BuildBlendTree2D(
        AnimatorController ac,
        string treeName,
        BTChild[] children,
        AnimationClip[] clipArray)
    {
        BlendTree tree;
        ac.CreateBlendTreeInController(treeName, out tree);

        tree.blendType              = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter         = "MoveX";
        tree.blendParameterY        = "MoveY";
        tree.useAutomaticThresholds = false;

        // Unity tự tạo 1 child mặc định — xóa để tránh lệch slot
        tree.children = new ChildMotion[0];

        int count = children.Length;
        for (int i = 0; i < count; i++)
        {
            var c    = children[i];
            var clip = (clipArray != null && i < clipArray.Length) ? clipArray[i] : null;
            tree.AddChild(clip, new Vector2(c.px, c.py));
        }

        var cms = tree.children;
        for (int i = 0; i < count; i++)
        {
            cms[i].timeScale            = children[i].timeScale;
            cms[i].directBlendParameter = "MoveX";
        }
        tree.children = cms;

        return tree;
    }
}

/// <summary>Dữ liệu một child trong BlendTree 2D.</summary>
public struct BTChild
{
    public float px, py, timeScale;
}
#endif