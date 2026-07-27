using Fusion;
using Sirenix.OdinInspector;
using UnityEngine;

public class TurnInPlace : NetworkBehaviour
{
    [TabGroup("Turn In Place", "Preference")]
    [SerializeField] private Transform _root;
    
    private float _late_root_Rot_Y;
    private bool _left;
    
    [TabGroup("Turn In Place", "Preference")]
    [SerializeField] private FlowerSetup _flowerSetup_L;
    [TabGroup("Turn In Place", "Preference")]
    [SerializeField] private FlowerSetup _flowerSetup_R;

    [TabGroup("Turn In Place", "Setting")]
    [Range(25f, 90f)]
    [SerializeField] private float _angle = 30f;
    [TabGroup("Turn In Place", "Setting")]
    [Range(0.1f, 2f)]
    [SerializeField] private float duration = 1f;
    [TabGroup("Turn In Place", "Setting")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float jumpHeight = 0.5f;
    [TabGroup("Turn In Place", "Setting")]
    [Range(0f, 1f)]
    [SerializeField] private float offset;
    
    private bool _canTurn;
    private bool _lateCanTurn;

    private NCharMovementHandler _movementHandler;

    public override void Spawned()
    {
        _movementHandler = GetComponent<NCharMovementHandler>();
        _late_root_Rot_Y = _root.rotation.eulerAngles.y;
    }

    public override void Render()
    {
        bool IsTurn = _movementHandler.IsTurnInPlaceActive;
        if (IsTurn) TurnOn(); else TurnOff();

        if (!_canTurn) return;
        if (_canTurn != _lateCanTurn)
        {
            MoveAndRotate.resetFree(_flowerSetup_L);
            MoveAndRotate.resetFree(_flowerSetup_R);
            _lateCanTurn = _canTurn;
        }
        MoveAndRotate.Lock(_flowerSetup_L);
        MoveAndRotate.Lock(_flowerSetup_R);

        ToggleTurn();
        if (!_left)
        {
            if (!Move(_flowerSetup_L)) Move(_flowerSetup_R);
        }
        else
        {
            if (!Move(_flowerSetup_R)) Move(_flowerSetup_L);
        }
    }

    [Button]
    public void Setup()
    {
        TurnInPlaceSetup.Setup(transform, ref _flowerSetup_L, ref _flowerSetup_R);
    }

    private void ToggleTurn()
    {
        MoveAndRotate.ToggleTurn(_flowerSetup_L, _flowerSetup_R, _root, ref _late_root_Rot_Y, _angle, ref _left);
    }

    private bool Move(FlowerSetup flowerSetup)
    {
        return MoveAndRotate.Move(flowerSetup, jumpHeight, duration, offset);
    }

    private void OnEnable()
    {
        MoveAndRotate.StartMoving(_flowerSetup_L);
        MoveAndRotate.StartMoving(_flowerSetup_R);
    }

    public void TurnOff(float time = 0.1f)
    {
        _flowerSetup_L.IKconstraint.TurnOff(time);
        _flowerSetup_R.IKconstraint.TurnOff(time);
        _canTurn = false;
        _lateCanTurn = false;
    }

    public void TurnOn(float time = 0.1f)
    {
        _flowerSetup_L.IKconstraint.TurnOn(time);
        _flowerSetup_R.IKconstraint.TurnOn(time);
        _canTurn = true;
    }
}