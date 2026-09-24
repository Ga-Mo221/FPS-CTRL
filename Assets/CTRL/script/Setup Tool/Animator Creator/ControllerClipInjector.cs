// =============================================================
//  ControllerClipInjector.cs
//  Helper tĩnh: quét MỘT Animator Controller CÓ SẴN (mọi layer,
//  mọi Sub State Machine) và gán AnimationClip phù hợp vào từng
//  AnimatorState theo tên — KHÔNG tạo state mới, KHÔNG đụng tới
//  transition.
//
//  2 chế độ:
//    • FillEmptyOnly : chỉ gán clip cho state đang có motion == null
//    • OverwriteAll  : gán đè clip mới cho MỌI state (kể cả đã có clip)
//
//  Quy tắc match giống hệt LayerBuilder.AddStateAndAssignClip:
//    - Bỏ qua state tên "IsDead" / "NULL"
//    - So khớp không phân biệt hoa thường, so khớp theo kiểu
//      "contains" 2 chiều (tên clip chứa tên state HOẶC ngược lại)
//    - Không đụng vào state đang dùng BlendTree làm motion
//      (để không phá cấu trúc Locomotion Stand/Crouch)
// =============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

public static class ControllerClipInjector
{
    public enum FillMode
    {
        FillEmptyOnly,
        OverwriteAll,
    }

    public struct Result
    {
        public int matched;    // state đã được gán clip mới
        public int skipped;    // state bỏ qua (IsDead/NULL, BlendTree, hoặc đã có clip ở mode FillEmptyOnly)
        public int noMatch;    // state không có motion nhưng không tìm được clip phù hợp
    }

    /// <summary>
    /// Quét toàn bộ layer + sub state machine của <paramref name="ac"/> và gán clip.
    /// </summary>
    public static Result ApplyClips(AnimatorController ac, List<AnimationClip> clips, FillMode mode)
    {
        var result = new Result();
        if (ac == null) return result;

        foreach (var layer in ac.layers)
        {
            if (layer.stateMachine == null) continue;
            WalkStateMachine(layer.stateMachine, clips, mode, ref result);
        }

        return result;
    }

    // =========================================================
    // WALK (đệ quy qua mọi Sub State Machine)
    // =========================================================
    private static void WalkStateMachine(
        AnimatorStateMachine sm, List<AnimationClip> clips, FillMode mode, ref Result result)
    {
        foreach (var cs in sm.states)
        {
            var state = cs.state;
            if (state == null) continue;

            string upperName = state.name.ToUpper().Trim();
            if (upperName == "ISDEAD" || upperName == "NULL")
            {
                result.skipped++;
                continue;
            }

            // Không đụng vào BlendTree (Locomotion Stand/Crouch...) — tránh phá cấu trúc.
            if (state.motion is BlendTree)
            {
                result.skipped++;
                continue;
            }

            bool hasClip = state.motion != null;
            if (mode == FillMode.FillEmptyOnly && hasClip)
            {
                result.skipped++;
                continue;
            }

            var matchedClip = FindMatchingClip(state.name, clips);
            if (matchedClip != null)
            {
                state.motion = matchedClip;
                result.matched++;
            }
            else
            {
                result.noMatch++;
            }
        }

        // Đệ quy vào các Sub State Machine con.
        foreach (var childSm in sm.stateMachines)
        {
            if (childSm.stateMachine != null)
                WalkStateMachine(childSm.stateMachine, clips, mode, ref result);
        }
    }

    // =========================================================
    // BLEND TREE (Locomotion Stand / Crouch)
    //
    // BlendTree không có "tên state" cho từng child để match theo
    // tên như state thường, nên input ở đây là 2 mảng clip ĐÃ được
    // xác định sẵn theo đúng thứ tự slot gốc (index khớp với
    // FullBodyLayerBuilder.StandChildren / CrouchChildren) — tức là
    // kết quả của FullBodyLayerBuilder.AutoFillStandClips() /
    // AutoFillCrouchClips() (+ CopyCrouchFastGroupFromSlow()).
    //
    // Tool tự tìm state nào trong controller đang có BlendTree làm
    // motion, nhận diện là "Stand" hay "Crouch" (theo tên, fallback
    // theo số lượng children: 21 = Stand, 17 = Crouch), rồi gán
    // clip vào từng child theo đúng index đó.
    // =========================================================
    public static Result ApplyBlendTreeClips(
        AnimatorController ac, AnimationClip[] standClips, AnimationClip[] crouchClips, FillMode mode)
    {
        var result = new Result();
        if (ac == null) return result;

        foreach (var layer in ac.layers)
        {
            if (layer.stateMachine == null) continue;
            WalkForBlendTrees(layer.stateMachine, standClips, crouchClips, mode, ref result);
        }

        return result;
    }

    private static void WalkForBlendTrees(
        AnimatorStateMachine sm, AnimationClip[] standClips, AnimationClip[] crouchClips,
        FillMode mode, ref Result result)
    {
        foreach (var cs in sm.states)
        {
            var state = cs.state;
            if (state == null) continue;

            var tree = state.motion as BlendTree;
            if (tree == null) continue;

            var source = IdentifyStandOrCrouch(state, tree, standClips, crouchClips);
            if (source == null) continue; // không nhận diện được / không khớp số slot

            ApplyChildrenClips(tree, source, mode, ref result);
        }

        foreach (var childSm in sm.stateMachines)
        {
            if (childSm.stateMachine != null)
                WalkForBlendTrees(childSm.stateMachine, standClips, crouchClips, mode, ref result);
        }
    }

    private static AnimationClip[] IdentifyStandOrCrouch(
        AnimatorState state, BlendTree tree, AnimationClip[] standClips, AnimationClip[] crouchClips)
    {
        string n = ((tree.name ?? "") + " " + (state.name ?? "")).ToUpper();
        bool looksStand  = n.Contains("STAND");
        bool looksCrouch = n.Contains("CROUCH") || n.Contains("CROUNCH"); // "Crounch" = typo trong builder gốc

        if (looksStand && !looksCrouch)
            return tree.children.Length == standClips.Length ? standClips : null;

        if (looksCrouch && !looksStand)
            return tree.children.Length == crouchClips.Length ? crouchClips : null;

        // Không match được theo tên → fallback theo đúng số lượng children.
        if (tree.children.Length == standClips.Length)  return standClips;
        if (tree.children.Length == crouchClips.Length) return crouchClips;

        return null;
    }

    private static void ApplyChildrenClips(
        BlendTree tree, AnimationClip[] source, FillMode mode, ref Result result)
    {
        var children = tree.children; // ChildMotion[] — struct array, cần gán lại mới lưu được
        int count     = Mathf.Min(children.Length, source.Length);
        bool changed  = false;

        for (int i = 0; i < count; i++)
        {
            bool hasMotion = children[i].motion != null;
            if (mode == FillMode.FillEmptyOnly && hasMotion)
            {
                result.skipped++;
                continue;
            }

            if (source[i] != null)
            {
                children[i].motion = source[i];
                changed = true;
                result.matched++;
            }
            else
            {
                result.noMatch++;
            }
        }

        if (changed) tree.children = children;
    }

    // =========================================================
    // MATCH — giống hệt LayerBuilder.AddStateAndAssignClip
    // =========================================================
    private static AnimationClip FindMatchingClip(string stateName, List<AnimationClip> clips)
    {
        if (clips == null) return null;

        string searchKey = stateName.ToLower().Trim();
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            string clipName = clip.name.ToLower();
            if (clipName == searchKey || clipName.Contains(searchKey) || searchKey.Contains(clipName))
                return clip;
        }

        return null;
    }
}
#endif