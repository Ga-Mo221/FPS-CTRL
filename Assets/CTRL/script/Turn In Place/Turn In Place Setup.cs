using UnityEngine;
using GaMo.BlenderEditorTools;

public static class TurnInPlaceSetup
{
    public static void Setup
    (
        Transform parrent,
        Transform root,
        ref FlowerSetup flowerSetup_L,
        ref FlowerSetup flowerSetup_R
        )
    {
        GameObject LegTurn = new GameObject("Turn In Place Setup");
        LegTurn.transform.SetParent(parrent);
        
        createIK(LegTurn.transform, root, "IK Leg Left", flowerSetup_L);
        createIK(LegTurn.transform, root, "IK Leg Right", flowerSetup_R);
    }

    public static void createIK
    (
        Transform parrent,
        Transform root,
        string name,
        FlowerSetup flowerSetup
        )
    {
        GameObject IK = new GameObject(name);
        IK.transform.SetParent(parrent.transform);
        IKConstraint IKconstraint = IK.AddComponent<IKConstraint>();
        flowerSetup.IKconstraint = IKconstraint;
        Transform[] list = new Transform[3];
        list[0] = flowerSetup.UpperLeg;
        list[1] = flowerSetup.LowerLeg;
        list[2] = flowerSetup.Foot;
        IKconstraint.bones =  list;
        
        GameObject Owner = new GameObject("Owner");
        Owner.transform.SetParent(IK.transform);
        Owner.transform.position = flowerSetup.Foot.position;
        Owner.transform.rotation = flowerSetup.Foot.rotation;
        
        GameObject Target = new GameObject("Target");
        Target.transform.SetParent(IK.transform);
        Target.transform.position = flowerSetup.Foot.position;
        Target.transform.rotation = flowerSetup.Foot.rotation;
        
        
        GameObject Pole = new GameObject("Pole");
        Pole.transform.SetParent(IK.transform);
        Pole.transform.rotation = flowerSetup.LowerLeg.rotation;
        Pole.transform.position = flowerSetup.LowerLeg.position;

        IKconstraint.AddPair(name, Owner.transform, Pole.transform, 1f);
        
        flowerSetup.Owner = Owner.transform;
        flowerSetup.Target = Target.transform;

        // Chụp offset của Target so với root NGAY sau khi tạo (hierarchy này
        // static sau setup, offset không đổi theo thời gian). Dùng để tính lại
        // world-pose ổn định ở runtime (xem MoveAndRotate.GetStableTargetPose)
        // thay vì đọc Target.transform sống — vì Target nằm dưới root network,
        // world rotation của nó bị "rung" theo root mỗi khi AccumulateYaw()
        // snap thô rồi Render() lại smooth về trong cùng 1 frame.
        if (root != null)
        {
            flowerSetup.TargetLocalOffsetPos = root.InverseTransformPoint(Target.transform.position);
            flowerSetup.TargetLocalOffsetRot = Quaternion.Inverse(root.rotation) * Target.transform.rotation;
        }
    }
}