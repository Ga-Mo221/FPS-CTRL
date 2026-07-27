using Sirenix.OdinInspector;
using UnityEngine;

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
    
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _owner;
    [SerializeField] private bool _isMoving;
    [SerializeField] private bool _canMove;
    
    public IKConstraint IKconstraint;

    private float _timer; // Timer là của riêng từng FlowerSetup, không dùng chung
    private Vector3 _startPos;
    private Quaternion _startRot;

    // World transform đang được khóa
    private Vector3 _freezePos;
    private Quaternion _freezeRot;

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
}