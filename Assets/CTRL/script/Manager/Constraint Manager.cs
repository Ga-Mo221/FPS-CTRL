using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ConstraintManager : MonoBehaviour
{
    [TabGroup("Constraint Manager", "List Constraints")]
    [Header("List Bone Copy Rotation (local-delta)")]
    [SerializeField] private TransformConstraint[] _list_Bone_Body;
    [TabGroup("Constraint Manager", "List Constraints")]
    [Header("List Bone Copy Transform (world)")]
    [SerializeField] private TransformConstraint[] _list_Bone_Grenade;

    [TabGroup("Constraint Manager", "Camera")]
    [HorizontalGroup("Constraint Manager/Camera/Shake", width: 0.6f)]
    [Tooltip("GameObject chứa Transform Constraint Copy Rotation Camera Của Weapon dùng để lắc camera theo các state.")]
    [SerializeField] private TransformConstraint _camera_Shake;
    [HorizontalGroup("Constraint Manager/Camera/Pivot", width: 0.6f)]
    [Tooltip("Tâm màn hình để aim weapon vào.")]
    [SerializeField] private Transform _ads_Pivot;

    [HorizontalGroup("Constraint Manager/Camera/Shake")]
    [HideLabel]
    [SerializeField] private string _camera_name = "Camera Shake";
    [HorizontalGroup("Constraint Manager/Camera/Pivot")]
    [HideLabel]
    [SerializeField] private string _ads_Pivot_name = "ADS Pivot";
    
    [TabGroup("Constraint Manager", "IK")]
    [Header("IK")]
    [SerializeField] private IKConstraint _IK_hand_left;
    [TabGroup("Constraint Manager", "IK")]
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
        ctrl.SetAdsPivotTransform(_ads_Pivot);
        
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

    public void TurnOffHand(bool Left = false, float duration = 0.25f)
    {
        if (Left)
            _IK_hand_left?.TurnOff(duration);
        else
            _IK_hand_right?.TurnOff(duration);
    }

    public void TurnOnHand(bool Left = false, float duration = 0.25f)
    {
        if (Left)
            _IK_hand_left?.TurnOn(duration);
        else
            _IK_hand_right.TurnOn(duration);
    }
    
    
    // ================================== EDITOR =======================================================
    
#if UNITY_EDITOR
    [Button("Get Constraints")]
    public void GetCopyRotateBoneInChildren()
    {
        List<TransformConstraint> result_rotate = new();
        List<TransformConstraint> result_transform = new();
        
        foreach (var trans in GetComponentsInChildren<Transform>(true))
        {
            if (trans.name.Contains(_ads_Pivot_name))
            {
                _ads_Pivot = trans;
                Debug.Log($"Da gan {trans.name} vao {_ads_Pivot.name}");
                continue;
            }
            
            TransformConstraint constraint = trans.gameObject.GetComponent<TransformConstraint>();
            if (constraint == null) continue;
            if (constraint.constraintObject == null)
                continue;

            if (constraint.transform.name.Contains(_camera_name))
            {
                _camera_Shake = constraint;
                Debug.Log($"Da gan {constraint.transform.name} vao {_camera_name}");
                continue;
            }
            
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