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

    // ── Local detection ──────────────────────────────────────────────────────
    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("Tốc độ di chuyển (XZ) tối thiểu để coi là 'đang di chuyển'. Dưới ngưỡng này → TurnInPlace ON.")]
    [Range(0.01f, 2f)]
    [SerializeField] private float _moveSpeedThreshold = 0.1f;

    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("Số giây character phải đứng yên liên tiếp trước khi bật TurnInPlace (chống giật khi lag ngắn).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _idleConfirmDelay = 0.08f;

    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("(Tùy chọn) Gán CharacterController để đọc velocity + isGrounded chính xác hơn. Nếu để trống sẽ tính từ position delta + Raycast.")]
    [SerializeField] private CharacterController _characterController;

    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("Raycast fallback: khoảng cách kiểm tra mặt đất khi không có CharacterController.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _groundCheckDistance = 0.15f;

    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("Layer mask cho ground raycast fallback.")]
    [SerializeField] private LayerMask _groundLayer = ~0;

    // Internal state
    [SerializeField] private bool _canTurn;
    private bool _lastIsTurn;

    // Position-delta fallback
    private Vector3 _lastPosition;

    // Debounce timer – chỉ dùng khi chuyển từ moving → idle
    private float _idleTimer;

    private CharComponentManager _componentManager;
    private NHPHandler _hpHandler;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _hpHandler = _componentManager.NHPHandler;

        // CharacterController auto-grab nếu không gán tay
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        _late_root_Rot_Y = _root.rotation.eulerAngles.y;
        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (_hpHandler.IsDead) { TurnOff(); return; }

        bool isTurnInPlace = DetectTurnInPlace();

        if (isTurnInPlace != _lastIsTurn)
        {
            if (isTurnInPlace) TurnOn();
            else TurnOff();
            _lastIsTurn = isTurnInPlace;
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

    // ── Local detection logic ────────────────────────────────────────────────

    /// <summary>
    /// Trả về true khi character đứng yên VÀ đang chạm đất → cần TurnInPlace.
    /// Ưu tiên đọc từ CharacterController; fallback về position delta + Raycast.
    /// Có debounce nhỏ để tránh giật khi bước vào trạng thái idle.
    /// </summary>
    private bool DetectTurnInPlace()
    {
        bool isGrounded = IsGrounded();
        float horizontalSpeed = GetHorizontalSpeed();
        bool isMoving = horizontalSpeed > _moveSpeedThreshold;

        // Đang bay / nhảy hoặc đang di chuyển → tắt TurnInPlace ngay, reset timer
        if (!isGrounded || isMoving)
        {
            _idleTimer = 0f;
            return false;
        }

        // Đứng yên + chạm đất → chờ đủ delay rồi mới bật
        _idleTimer += Time.deltaTime;
        return _idleTimer >= _idleConfirmDelay;
    }

    /// <summary>
    /// Kiểm tra chạm đất.
    /// Raycast luôn là nguồn chính (đáng tin hơn CC.isGrounded vì CC chỉ true
    /// khi frame trước có gọi Move/SimpleMove và có va chạm phía dưới).
    /// CC.isGrounded dùng thêm như OR để bắt những trường hợp Raycast bỏ sót.
    /// </summary>
    private bool IsGrounded()
    {
        // Bắn ray từ feet (đáy CC nếu có, không thì transform.position)
        Vector3 rayOrigin = _characterController != null
            ? transform.position + Vector3.up * _characterController.skinWidth
            : transform.position;

        bool rayHit = Physics.Raycast(rayOrigin, Vector3.down, _groundCheckDistance, _groundLayer, QueryTriggerInteraction.Ignore);

        // CC.isGrounded bổ sung — true khi CC đang tiếp xúc ground trong frame hiện tại
        bool ccGrounded = _characterController != null && _characterController.isGrounded;

        return rayHit || ccGrounded;
    }

    /// <summary>
    /// Lấy tốc độ ngang (XZ). Dùng CharacterController.velocity nếu có,
    /// ngược lại tính từ position delta trong frame.
    /// </summary>
    private float GetHorizontalSpeed()
    {
        if (_characterController != null)
        {
            Vector3 ccVel = _characterController.velocity;
            return new Vector2(ccVel.x, ccVel.z).magnitude;
        }

        // Fallback: position delta
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - _lastPosition;
        _lastPosition = currentPos;

        float speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        return speed;
    }

    // ── Existing logic (không đổi) ───────────────────────────────────────────

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

    // ── Gizmos ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Tính origin giống hệt IsGrounded()
        float skinWidth = _characterController != null ? _characterController.skinWidth : 0f;
        Vector3 rayOrigin = transform.position + Vector3.up * skinWidth;
        Vector3 rayEnd    = rayOrigin + Vector3.down * _groundCheckDistance;

        // Raycast để biết có hit không (dùng đúng layer như runtime)
        bool rayHit = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                                      _groundCheckDistance, _groundLayer,
                                      QueryTriggerInteraction.Ignore);
        bool ccGrounded  = _characterController != null && _characterController.isGrounded;
        bool isGrounded  = rayHit || ccGrounded;

        // Màu ray: xanh lá = grounded, đỏ = không chạm đất
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(rayOrigin, rayEnd);

        // Điểm bắt đầu ray (sphere nhỏ)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rayOrigin, 0.02f);

        // Điểm hit nếu có
        if (rayHit)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hit.point, 0.04f);
        }
        else
        {
            // Điểm cuối ray khi không hit
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rayEnd, 0.04f);
        }

        // Label trạng thái (hiện trong Scene view)
        UnityEditor.Handles.color = isGrounded ? Color.green : Color.red;
        UnityEditor.Handles.Label(
            rayOrigin + Vector3.up * 0.1f,
            isGrounded
                ? $"Grounded ✓  (ray:{rayHit} | cc:{ccGrounded})"
                : $"NOT Grounded  (ray:{rayHit} | cc:{ccGrounded})"
        );
    }
#endif
}