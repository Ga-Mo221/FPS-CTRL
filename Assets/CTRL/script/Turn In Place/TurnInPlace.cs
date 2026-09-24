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
    [Tooltip("Khoảng cách Raycast kiểm tra mặt đất từ transform.position.")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float _groundCheckDistance = 0.15f;

    [TabGroup("Turn In Place", "Detection")]
    [Tooltip("Layer mask cho ground raycast fallback.")]
    [SerializeField] private LayerMask _groundLayer = ~0;

    // Internal state
    [ReadOnly]
    [SerializeField] private bool _canTurn;
    private bool _lastIsTurn;

    // Position-delta fallback
    private Vector3 _lastPosition;

    // Debounce timer – chỉ dùng khi chuyển từ moving → idle
    private float _idleTimer;

    private CharComponentManager _componentManager;
    private NHPHandler _hpHandler;
    private NetworkCharacterControllerCustom _networkController;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _hpHandler = _componentManager.NHPHandler;
        _networkController = GetComponent<NetworkCharacterControllerCustom>();

        _late_root_Rot_Y = GetSmoothedYaw();
        _lastPosition = transform.position;
    }

    /// <summary>
    /// Yaw ổn định để dùng cho mọi tính toán turn-in-place. Ưu tiên
    /// NetworkCharacterControllerCustom.SmoothedYaw (giá trị đã qua SmoothDamp
    /// trong Render(), không bao giờ bị AccumulateYaw() snap thô kể cả khi có
    /// resimulation). KHÔNG đọc _root.rotation.eulerAngles.y trực tiếp trong lúc
    /// đang chạy vì nó bị ghi đè 2 lần/frame với timing khác nhau (raw tick +
    /// smoothed render) → đó chính là nguồn gây giật/step sai nhịp trước đây.
    /// Fallback về _root.rotation chỉ dùng khi không có network controller
    /// (ví dụ test offline / nhân vật không networked).
    /// </summary>
    private float GetSmoothedYaw()
    {
        if (_networkController != null)
            return _networkController.SmoothedYaw;

        return _root.rotation.eulerAngles.y;
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
    }

    /// <summary>
    /// Toàn bộ phần GHI vị trí/rotation của Owner (ToggleTurn quyết định có bắt
    /// đầu bước mới không, Move animate, Lock ghim khi đứng yên) được đặt ở
    /// LateUpdate, KHÔNG phải Update.
    ///
    /// Lý do: Owner là con (qua LegTurn/IK) của chính root network. Dù ta gán
    /// world-position cho Owner bằng giá trị đã tính ổn định, Unity chỉ quy đổi
    /// giá trị đó sang local offset MỘT LẦN tại thời điểm gán. Nếu SAU đó, trong
    /// cùng frame, root còn bị ghi rotation thêm lần nữa (CopyToEngine() snap
    /// thô trong BeforeAllTicks, hoặc Render() smooth lại — cả hai đều có thể
    /// chạy sau Update() của các MonoBehaviour thường), Owner sẽ "trôi" theo vì
    /// local offset không đổi trong khi world-matrix của parent vừa đổi.
    ///
    /// LateUpdate() của MỌI script luôn chạy sau Update() của MỌI script (và
    /// sau khi NetworkRunner của Fusion đã gọi xong Render() cho frame đó, vì
    /// Render() được Fusion dispatch trong pha Update, trước LateUpdate). Nên
    /// tới lúc LateUpdate này chạy, root coi như đã chốt rotation cuối cùng của
    /// frame — Owner gán ở đây sẽ không còn bị ghi đè thêm trước khi render.
    /// </summary>
    private void LateUpdate()
    {
        if (!_canTurn) return;

        float smoothedYaw = GetSmoothedYaw();

        ToggleTurn(smoothedYaw);

        bool movedL = false, movedR = false;
        if (!_left)
        {
            movedL = Move(_flowerSetup_L, smoothedYaw);
            if (!movedL) movedR = Move(_flowerSetup_R, smoothedYaw);
        }
        else
        {
            movedR = Move(_flowerSetup_R, smoothedYaw);
            if (!movedR) movedL = Move(_flowerSetup_L, smoothedYaw);
        }

        if (!movedL) MoveAndRotate.Lock(_flowerSetup_L);
        if (!movedR) MoveAndRotate.Lock(_flowerSetup_R);
    }

    // ── Local detection logic ────────────────────────────────────────────────

    /// <summary>
    /// Trả về true khi character đứng yên VÀ đang chạm đất → cần TurnInPlace.
    /// Tốc độ tính từ position delta; ground check bằng Raycast.
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
    /// Kiểm tra chạm đất bằng Raycast từ transform.position hướng xuống.
    /// </summary>
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance, _groundLayer, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Lấy tốc độ ngang (XZ) từ position delta trong frame.
    /// </summary>
    private float GetHorizontalSpeed()
    {
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - _lastPosition;
        _lastPosition = currentPos;

        return new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
    }

    // ── Existing logic (không đổi) ───────────────────────────────────────────

    [Button]
    public void Setup()
    {
        TurnInPlaceSetup.Setup(transform, _root, ref _flowerSetup_L, ref _flowerSetup_R);
    }

    private void ToggleTurn(float smoothedYaw)
    {
        MoveAndRotate.ToggleTurn(_flowerSetup_L, _flowerSetup_R, _root, ref _late_root_Rot_Y, _angle, ref _left, smoothedYaw, offset);
    }

    private bool Move(FlowerSetup flowerSetup, float smoothedYaw)
    {
        return MoveAndRotate.Move(flowerSetup, _root, smoothedYaw, jumpHeight, duration, offset);
    }

    private void OnEnable()
    {
        float smoothedYaw = GetSmoothedYaw();
        MoveAndRotate.StartMoving(_flowerSetup_L, _root, smoothedYaw);
        MoveAndRotate.StartMoving(_flowerSetup_R, _root, smoothedYaw);
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
        float smoothedYaw = GetSmoothedYaw();

        MoveAndRotate.resetFree(_flowerSetup_L);
        MoveAndRotate.resetFree(_flowerSetup_R);

        MoveAndRotate.ForceStartMoving(_flowerSetup_L, _root, smoothedYaw);
        MoveAndRotate.ForceStartMoving(_flowerSetup_R, _root, smoothedYaw);

        _flowerSetup_L.IKconstraint.TurnOn(time);
        _flowerSetup_R.IKconstraint.TurnOn(time);
        _canTurn = true;
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 rayOrigin = transform.position;
        Vector3 rayEnd    = rayOrigin + Vector3.down * _groundCheckDistance;

        bool rayHit = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                                      _groundCheckDistance, _groundLayer,
                                      QueryTriggerInteraction.Ignore);

        // Màu ray: xanh lá = grounded, đỏ = không chạm đất
        Gizmos.color = rayHit ? Color.green : Color.red;
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
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rayEnd, 0.04f);
        }

        // Label trạng thái (hiện trong Scene view)
        UnityEditor.Handles.color = rayHit ? Color.green : Color.red;
        UnityEditor.Handles.Label(
            rayOrigin + Vector3.up * 0.1f,
            rayHit ? "Grounded ✓" : "NOT Grounded"
        );
    }
#endif
}