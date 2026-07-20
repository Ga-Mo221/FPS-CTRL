using System.Collections.Generic;
using UnityEngine;

public class ConstraintManager : MonoBehaviour
{
    [Header("List Bone Copy Rotation (local-delta)")]
    [SerializeField] private TransformConstraint[] _list_Bone_Body;
    [Header("List Bone Copy Transform (world)")]
    [SerializeField] private TransformConstraint[] _list_Bone_Grenade;

    [Header("Camera")] 
    [SerializeField] private TransformConstraint _camera_Shake;
    [SerializeField] private Transform _ads_Pivot;
    
    [Header("IK")]
    [SerializeField] private IKConstraint _IK_hand_left;
    [SerializeField] private IKConstraint _IK_hand_right;

    public void AddNewWeapon(WeaponController ctrl, float weight = 0)
    {
        foreach (var body in _list_Bone_Body)
        {
            foreach (var trans in ctrl.List_Bone_Body)
            {
                if (body.transform.name.Contains(trans.name))
                {
                    body.AddTarget(ctrl.Data.ID, trans, weight);
                    break;
                }
            }
        }

        foreach (var grenade in _list_Bone_Grenade)
        {
            foreach (var trans in ctrl.List_Bone_Grenade)
                if (grenade.transform.name.Contains(trans.name))
                {
                    grenade.AddTarget(ctrl.Data.ID, trans, weight);
                    break;
                }
        }
        
        _camera_Shake.AddTarget(ctrl.Data.ID, ctrl.Camera_Shake, weight);
        ctrl.ADS_Pivot.AddTarget(ctrl.Data.ID, _ads_Pivot);
        
        _IK_hand_left?.AddPair(ctrl.Data.ID, ctrl.Hand_Left, ctrl.Pole_Left, weight);
        _IK_hand_right?.AddPair(ctrl.Data.ID, ctrl.Hand_Right, ctrl.Pole_Right, weight);
    }

    public void RemoveWeapon(string id)
    {
        foreach (var cons in _list_Bone_Body)
            cons?.RemoveTarget(id);
        foreach (var cons in _list_Bone_Grenade)
            cons?.RemoveTarget(id);
        _camera_Shake.RemoveTarget(id);
        _IK_hand_left?.RemovePair(id);
        _IK_hand_right?.RemovePair(id);
    }

    public void SwitchWeapon(string id, float duration = 0.25f)
    {
        foreach (var cons in _list_Bone_Body)
            cons?.SwitchTarget(id, duration);
        foreach (var cons in _list_Bone_Grenade)
            cons?.SwitchTarget(id, duration);
        _camera_Shake.SwitchTarget(id, duration);
        _IK_hand_left?.SwitchPair(id, duration);
        _IK_hand_right?.SwitchPair(id, duration);
    }
    
    
    // ================================== EDITOR =======================================================
    
#if UNITY_EDITOR
    [ContextMenu("Get Copy Rotate Bone In Children")]
    public void GetCopyRotateBoneInChildren()
    {
        List<TransformConstraint> result_rotate = new();
        List<TransformConstraint> result_transform = new();
        
        foreach (var constraint in GetComponentsInChildren<TransformConstraint>(true))
        {
            if (constraint.constraintObject == null)
                continue;
            
            if (constraint.enablePositionConstraint)
                result_transform.Add(constraint);
            else
                result_rotate.Add(constraint);
        }

        _list_Bone_Body = result_rotate.ToArray();
        _list_Bone_Grenade = result_transform.ToArray();
        
        foreach (var ik in GetComponentsInChildren<IKConstraint>(true))
        {
            if (ik.bones == null)
                continue;
            
            if (ik.transform.name.Contains("Hand Left"))
                _IK_hand_left = ik;
            else
                _IK_hand_right = ik;
        }
    }
#endif
}