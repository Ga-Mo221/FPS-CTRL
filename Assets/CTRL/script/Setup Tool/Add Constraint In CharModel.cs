#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEditor;


public class AddConstraintInCharModel : MonoBehaviour
{
    [TabGroup("Data")]
    [Header("CTRL BODY")]
    [SerializeField] private GameObject[] _list_Bone_Body = new GameObject[0];
    
    [TabGroup("Data")]
    [Header("CTRL GRENADE")]
    [SerializeField] private GameObject[] _list_Bone_Grenade = new GameObject[0];

    [TabGroup("Data")]
    [Header("CTRL HAND")] 
    [SerializeField] private Transform[] _list_Hand = new Transform[0];

    private IKConstraint ikleft;
    private IKConstraint ikright;
    
    [TabGroup("Constraint")]
    public TransformConstraint.SpaceMode rotationSpace = TransformConstraint.SpaceMode.Local;
    [TabGroup("Constraint")]
    public TransformConstraint.ConstraintMode _constraintmode = TransformConstraint.ConstraintMode.Delta;

    private GameObject SetupIK;
    
    [TabGroup("IK Setting")]
    [Header("Setting IK")]
    public string _suffix_Left = "_L";
    [TabGroup("IK Setting")]
    public string _suffix_Right = "_R";
    [TabGroup("IK Setting")]
    public string _upper_Arm = "Upper";
    [TabGroup("IK Setting")]
    public string _lower_Arm = "Lower";
    [TabGroup("IK Setting")]
    public string _hand = "Hand";
    
    [HorizontalGroup("Buttons")]
    [Button]
    public void SetUp()
    {
        Clear();
        if (_list_Bone_Body.Length != 0)
        {
            foreach (var bone in _list_Bone_Body)
            {
                var cons = bone.AddComponent<TransformConstraint>();
                cons.constraintObject = bone.transform;
                cons.enablePositionConstraint = false;
                cons.rotationSpace = rotationSpace;
                cons.rotationMode = _constraintmode;
            }
            
            Debug.Log($"Đã add thành công Copy Transform (local) vào {_list_Bone_Body.Length} xương");
        }

        if (_list_Bone_Grenade.Length != 0)
        {
            foreach (var bone in _list_Bone_Grenade)
            {
                var cons = bone.AddComponent<TransformConstraint>();
                cons.constraintObject = bone.transform;
            }
            
            Debug.Log($"Đã add thành công Copy Transform (world) vào {_list_Bone_Grenade.Length} xương");
        }

        if (_list_Hand.Length != 0)
        {
            IKSetup();
        }
    }
    
    [HorizontalGroup("Buttons")]
    [Button]
    public void Clear()
    {
        foreach (var cons in GetComponentsInChildren<TransformConstraint>(true))
        {
            Undo.DestroyObjectImmediate(cons);
        }
        
        if (SetupIK != null)
            Undo.DestroyObjectImmediate(SetupIK);
        
        Debug.Log("Da Xoa Toan Bo");
    }

    [HorizontalGroup("Buttons")]
    [Button]
    private void IKSetup()
    {
        if (_list_Hand.Length == 0) return;
        
        GameObject go = new GameObject("IK Setup");
        go.transform.SetParent(transform);
        SetupIK = go;
        
        GameObject handLeft = new GameObject("Hand Left");
        handLeft.transform.SetParent(go.transform);
        ikleft = handLeft.AddComponent<IKConstraint>();
        
        GameObject handRight = new GameObject("Hand Right");
        handRight.transform.SetParent(go.transform);
        ikright = handRight.AddComponent<IKConstraint>();
        
        AddboneInIK(_suffix_Left);
        AddboneInIK(_suffix_Right);
    }

    private void AddboneInIK(string suffix)
    {
        IKConstraint cons = suffix.Contains(_suffix_Left) ? ikleft : ikright;

        Transform[] bones = new Transform[3];

        foreach (var bone in _list_Hand)
        {
            if (bone == null)
                continue;
            
            // Chỉ lấy đúng bone có hậu tố _L hoặc _R
            if (!bone.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            Debug.Log($"Phát hiện xương: {bone.name}");

            if (bone.name.Contains(_upper_Arm, StringComparison.OrdinalIgnoreCase))
            {
                bones[0] = bone;
                Debug.Log($"{_upper_Arm}: {bone.name}");
            }
            else if (bone.name.Contains(_lower_Arm, StringComparison.OrdinalIgnoreCase))
            {
                bones[1] = bone;
                Debug.Log($"{_lower_Arm}: {bone.name}");
            }
            else if (bone.name.Contains(_hand, StringComparison.OrdinalIgnoreCase))
            {
                bones[2] = bone;
                Debug.Log($"{_hand}: {bone.name}");
            }
        }

        cons.bones = bones;

        if (bones[0] == null || bones[1] == null || bones[2] == null)
        {
            Debug.LogWarning(
                $"IK '{suffix}' chưa tìm đủ xương.\n" +
                $"{_upper_Arm}: {(bones[0] ? bones[0].name : "NULL")}\n" +
                $"{_lower_Arm}: {(bones[1] ? bones[1].name : "NULL")}\n" +
                $"{_hand} : {(bones[2] ? bones[2].name : "NULL")}");
        }
    }
}
#endif