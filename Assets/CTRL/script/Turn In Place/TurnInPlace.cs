using Sirenix.OdinInspector;
using UnityEngine;

public class TurnInPlace : MonoBehaviour
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
    
    [SerializeField] private bool _canTurn;
    private bool _lastIsTurn;

    private CharComponentManager _componentManager;
    private NCharMovementHandler _movementHandler;
    private NHPHandler _hpHandler;

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        _hpHandler = _componentManager.NHPHandler;
        _late_root_Rot_Y = _root.rotation.eulerAngles.y;
    }
    
    private void Update()
    {
        if (_hpHandler.IsDead) { TurnOff(); return; }

        bool IsTurn = _movementHandler.IsTurnInPlaceActive;
        if (IsTurn != _lastIsTurn)
        {
            if (IsTurn) TurnOn();
            else TurnOff();
            _lastIsTurn = IsTurn;
        }

        if (!_canTurn) return;

        ToggleTurn();

        bool movedL = false, movedR = false;
        if (!_left)
        {
            movedL = Move(_flowerSetup_L);
            if (!movedL) movedR = Move(_flowerSetup_R);
        }
        else
        {
            movedR = Move(_flowerSetup_R);
            if (!movedR) movedL = Move(_flowerSetup_L);
        }

        if (!movedL) MoveAndRotate.Lock(_flowerSetup_L);
        if (!movedR) MoveAndRotate.Lock(_flowerSetup_R);
    }

    [Button]
    
    public void Setup()
    {
        TurnInPlaceSetup.Setup(transform, ref _flowerSetup_L, ref _flowerSetup_R);
    }

    private void ToggleTurn()
    {
        MoveAndRotate.ToggleTurn(_flowerSetup_L, _flowerSetup_R, _root, ref _late_root_Rot_Y, _angle, ref _left, offset);
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

    private void TurnOff(float time = 0.1f)
    {
        _flowerSetup_L.IKconstraint.TurnOff(time);
        _flowerSetup_R.IKconstraint.TurnOff(time);

        // Snapshot vị trí hiện tại để FreezePosition không bị stale
        if (_flowerSetup_L.Owner != null)
        {
            _flowerSetup_L.FreezePosition = _flowerSetup_L.Owner.position;
            _flowerSetup_L.FreezeRotation = _flowerSetup_L.Owner.rotation;
        }
        if (_flowerSetup_R.Owner != null)
        {
            _flowerSetup_R.FreezePosition = _flowerSetup_R.Owner.position;
            _flowerSetup_R.FreezeRotation = _flowerSetup_R.Owner.rotation;
        }

        _canTurn = false;
    }

    private void TurnOn(float time = 0.3f)
    {
        MoveAndRotate.resetFree(_flowerSetup_L);
        MoveAndRotate.resetFree(_flowerSetup_R);
        
        MoveAndRotate.ForceStartMoving(_flowerSetup_L);
        MoveAndRotate.ForceStartMoving(_flowerSetup_R);
        
        _flowerSetup_L.IKconstraint.TurnOn(time);
        _flowerSetup_R.IKconstraint.TurnOn(time);
        _canTurn = true;
    }
}