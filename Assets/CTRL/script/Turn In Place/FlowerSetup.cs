using Sirenix.OdinInspector;
using UnityEngine;
using GaMo.BlenderEditorTools;

[System.Serializable]
public class FlowerSetup
{
    [TabGroup("Turn In Place", "Leg")]
    [HorizontalGroup("Turn In Place/Leg/row")]
    [HideLabel] [SerializeField] private Transform _upper_Leg;
    [HorizontalGroup("Turn In Place/Leg/row")]
    [HideLabel] [SerializeField] private Transform _Lower_Leg;
    [HorizontalGroup("Turn In Place/Leg/row")]
    [HideLabel] [SerializeField] private Transform _foot;
    
    [ReadOnly]
    [SerializeField] private Transform _target;
    [ReadOnly]
    [SerializeField] private Transform _owner;
    [ReadOnly]
    [SerializeField] private bool _isMoving;
    [ReadOnly]
    [SerializeField] private bool _canMove;
    
    [ReadOnly]
    public IKConstraint IKconstraint;

    private float _timer; // Timer là của riêng từng FlowerSetup, không dùng chung
    private Vector3 _startPos;
    private Quaternion _startRot;

    // World transform đang được khóa
    [ReadOnly]
    [SerializeField] private Vector3 _freezePos;
    [ReadOnly]
    [SerializeField] private Quaternion _freezeRot;

    // Offset cục bộ của Target so với root nhân vật, chụp 1 lần lúc Setup và
    // KHÔNG đổi theo thời gian (hierarchy IK là static sau khi tạo). Dùng để
    // tự tính lại world-pose ổn định thay vì đọc Target.transform sống —
    // Target là con (gián tiếp) của root network, nên world pose của nó bị
    // "rung" theo rotation chưa ổn định của root (double-write giữa tick raw
    // và render đã smooth) nếu đọc trực tiếp mỗi frame.
    [ReadOnly]
    [SerializeField] private Vector3 _targetLocalOffsetPos;
    [ReadOnly]
    [SerializeField] private Quaternion _targetLocalOffsetRot;

    public Transform UpperLeg => _upper_Leg;
    public Transform LowerLeg => _Lower_Leg;
    public Transform Foot => _foot;

    public Transform Target
    {
        get { return _target; }
        set { _target = value; }
    }

    public Transform Owner
    {
        get { return _owner; }
        set { _owner = value; }
    }

    public bool CanMove
    {
        get { return _canMove; }
        set { _canMove = value; }
    }

    public bool IsMoving
    {
        get { return _isMoving; }
        set { _isMoving = value; }
    }

    public float Timer
    {
        get { return _timer; }
        set { _timer = value; }
    }

    public Vector3 StartPosition
    {
        get { return _startPos; }
        set { _startPos = value; }
    }

    public Quaternion StartRotation
    {
        get { return _startRot; }
        set { _startRot = value; }
    }

    public Vector3 FreezePosition
    {
        get { return _freezePos; }
        set { _freezePos = value; }
    }

    public Quaternion FreezeRotation
    {
        get { return _freezeRot; }
        set { _freezeRot = value; }
    }

    public Vector3 TargetLocalOffsetPos
    {
        get { return _targetLocalOffsetPos; }
        set { _targetLocalOffsetPos = value; }
    }

    public Quaternion TargetLocalOffsetRot
    {
        get { return _targetLocalOffsetRot; }
        set { _targetLocalOffsetRot = value; }
    }
}