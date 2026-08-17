using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using Fusion;

/// <summary>
/// Thành phần điều khiển nhân vật tùy chỉnh tích hợp hệ thống mạng Photon Fusion (Network Character Controller).
/// Quản lý di chuyển, trọng lực, nhảy và đồng bộ hóa vị trí mượt mà giữa các máy client.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[NetworkBehaviourWeaved(NetworkCCData.WORDS)]
[AddComponentMenu("Network Character Controller")]
[DefaultExecutionOrder(20)]
// ReSharper disable once CheckNamespace
public sealed class NetworkCharacterControllerCustom : NetworkTRSP, INetworkTRSPTeleport, IBeforeAllTicks, IAfterAllTicks, IBeforeCopyPreviousState 
{
    /// <summary>
    /// Lấy tham chiếu đến cấu trúc dữ liệu mạng NetworkCCData của thực thể này.
    /// </summary>
    new ref NetworkCCData Data => ref ReinterpretState<NetworkCCData>();

    private NCharMovementHandler _movementHandler;
    
    private PlayerData _data;

    Tick                _initial;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private Transform _groundCheckPoint;
    
    private float _currentAngularVelocity;

    /// <summary>
    /// Truy cập đến thành phần CharacterController gốc của Unity gắn trên Object này.
    /// </summary>
    public CharacterController Controller => _controller;

    /// <summary>
    /// Vận tốc hiện tại của nhân vật (được đồng bộ qua mạng).
    /// </summary>
    public Vector3 Velocity {
        get => Data.Velocity;
        set => Data.Velocity = value;
    }

    /// <summary>
    /// Trạng thái chạm đất hiện tại của nhân vật (được đồng bộ qua mạng).
    /// </summary>
    public bool Grounded {
        get => CheckGrounded();
    }
    
    private bool CheckGrounded()
    {
        if (_groundCheckPoint == null)
        {
            Debug.LogWarning("Ground Check point is null!");
            return false;
        }
        
        // Tâm sphere đặt tại đáy CharacterController
        Vector3 point = _groundCheckPoint.position;
        point.y += _data.GroundCheckOffset;
        Vector3 origin = point;

        return Physics.CheckSphere(origin, _data.GroundCheckRadius, _data.GroundMask, QueryTriggerInteraction.Ignore);
    }
    
    /// <summary>
    /// Lấy vị trí hiện tại của nhân vật từ dữ liệu mạng TRSP.
    /// </summary>
    /// <returns>Tọa độ Vector3 trong không gian mạng.</returns>
    public Vector3 GetNetworkPosition() => Data.TRSPData.Position;

    /// <summary>
    /// Lấy vòng quay hiện tại của nhân vật từ dữ liệu mạng TRSP.
    /// </summary>
    /// <returns>Góc quay Quaternion trong không gian mạng.</returns>
    public Quaternion GetNetworkRotation() => Data.TRSPData.Rotation;
    
    /// <summary>
    /// Hàm callback của Photon Fusion chạy khi thực thể mạng được khởi tạo thành công (Spawned).
    /// Thiết lập cấu hình ban đầu và đồng bộ hóa trạng thái Transform.
    /// </summary>
    public override void Spawned()
    {
        _movementHandler = GetComponent<NCharMovementHandler>();
        _initial = default;
        _currentAngularVelocity = 0f;
        TryGetComponent(out _controller);
        _controller.enabled = false;
        _controller.enabled = true;
        CopyToBuffer();
    }

    /// <summary>
    /// Hàm callback chạy mỗi khung hình Render (Update) của Unity để thực hiện nội suy (Interpolation), 
    /// giúp chuyển động của nhân vật mượt mà giữa các Tick mạng.
    /// </summary>
    public override void Render() {
        NetworkTRSP.Render(this, transform, false, false, false, ref _initial);
    }

    /// <summary>
    /// Thực thi trước khi tất cả các Ticks mô phỏng bắt đầu. Đồng bộ dữ liệu mạng vào Unity Engine.
    /// </summary>
    void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount) {
        CopyToEngine();
    }

    /// <summary>
    /// Thực thi sau khi tất cả các Ticks mô phỏng hoàn tất. Ghi nhận dữ liệu vị trí vật lý ngược lại vào bộ đệm mạng.
    /// </summary>
    void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount) {
        CopyToBuffer();
    }

    /// <summary>
    /// Thực thi trước khi sao chép trạng thái khung hình trước đó của Fusion. Bảo toàn dữ liệu vị trí hiện tại.
    /// </summary>
    void IBeforeCopyPreviousState.BeforeCopyPreviousState() {
        CopyToBuffer();
    }

    /// <summary>
    /// Hàm vòng đời mặc định của Unity, dùng để cache trước thành phần CharacterController.
    /// </summary>
    void Awake() {
        TryGetComponent(out _controller);
        var componentManager = GetComponent<CharComponentManager>();
        _data = componentManager.NCharMovementHandler.Data;
    }

    /// <summary>
    /// Sao chép vị trí và vòng quay Transform hiện tại của Unity vào cấu trúc dữ liệu mạng TRSP để chuẩn bị đồng bộ.
    /// </summary>
    void CopyToBuffer() {
        Data.TRSPData.Position = transform.position;
        Data.TRSPData.Rotation = transform.rotation;
    }

    /// <summary>
    /// Áp dụng vị trí và vòng quay từ dữ liệu mạng TRSP vào thành phần Transform của Unity, 
    /// có vô hiệu hóa tạm thời CharacterController để tránh lỗi tính toán sai lệch vị trí của Unity Engine.
    /// </summary>
    void CopyToEngine() {
        _controller.enabled = false;
        transform.SetPositionAndRotation(Data.TRSPData.Position, Data.TRSPData.Rotation);
        _controller.enabled = true;
    }
    
    /// <summary>
    /// Dịch chuyển tức thời nhân vật đến một vị trí và/hoặc vòng quay mới qua mạng, 
    /// tự động xử lý bật/tắt CharacterController để tránh xung đột vật lý.
    /// </summary>
    /// <param name="position">Vị trí mới cần dịch chuyển đến (để null nếu giữ nguyên).</param>
    /// <param name="rotation">Vòng quay mới cần thay đổi (để null nếu giữ nguyên).</param>
    public void Teleport(Vector3? position = null, Quaternion? rotation = null) {
        _controller.enabled = false;
        NetworkTRSP.Teleport(this, transform, position, rotation);
        _controller.enabled = true;
    }
    
    public void SetControllerHeight(PlayerState state)
    {
        float newHeight;
        float newCenterY;

        switch (state)
        {
            case PlayerState.Standing:
                newHeight  = _data.StandHeight;
                newCenterY = _data.StandCenterY;
                break;
            case PlayerState.Crouching:
                newHeight  = _data.CrouchHeight;
                newCenterY = _data.CrouchCenterY;
                break;
            case PlayerState.Slide:
                newHeight  = _data.SlideHeight;
                newCenterY = _data.SlideCenterY;
                break;
            default:
                return;
        }

        // Vị trí đáy chân (world space) TRƯỚC khi thay đổi
        float oldBottomY = transform.position.y + _controller.center.y - (_controller.height * 0.5f);

        _controller.height = newHeight;
        Vector3 center = _controller.center;
        center.y = newCenterY;
        _controller.center = center;

        // Vị trí đáy chân SAU khi thay đổi height/center (nếu không bù trừ)
        float newBottomY = transform.position.y + newCenterY - (newHeight * 0.5f);

        // Bù trừ transform để đáy chân giữ nguyên vị trí world -> tránh đâm xuyên đất
        float deltaY = oldBottomY - newBottomY;

        _controller.enabled = false;
        transform.position += new Vector3(0f, deltaY, 0f);
        _controller.enabled = true;
    }

    /// <summary>
    /// Thực hiện hành động nhảy bằng cách áp dụng lực đẩy lên trục Y của vận tốc.
    /// </summary>
    /// <param name="ignoreGrounded">Nếu true, nhân vật có thể nhảy ngay cả khi đang ở trên không.</param>
    /// <param name="overrideImpulse">Ghi đè lực nhảy mặc định bằng một giá trị lực mới.</param>
    public bool Jump(bool ignoreGrounded = false, float? overrideImpulse = null) 
    {
        if (Grounded || ignoreGrounded) {
            var newVel = Data.Velocity;
            newVel.y      += overrideImpulse ?? _data.JumpImpulse;
            Data.Velocity =  newVel;
            return true;
        }
        return false;
    }

    public void Rotate(float rotationY, float smoothing)
    {
        var yawModel = _data.YawModel;
        if (_movementHandler != null)
            if (_movementHandler.StateFl.IsADS || _movementHandler.StateFl.IsThrow)
                yawModel *= _data.AdsLookSpeedMultiplier;

        float targetAngularVel = rotationY * yawModel;

        _currentAngularVelocity = Mathf.Lerp(
            _currentAngularVelocity,
            targetAngularVel,
            smoothing * Runner.DeltaTime
        );

        if (Mathf.Abs(_currentAngularVelocity) < 0.01f && Mathf.Approximately(rotationY, 0f))
            _currentAngularVelocity = 0f;

        transform.Rotate(0f, _currentAngularVelocity * Runner.DeltaTime, 0f);
    }

    /// <summary>
    /// Xử lý logic di chuyển của nhân vật dựa trên hướng truyền vào, áp dụng gia tốc, hãm phanh, trọng lực và cập nhật trạng thái vật lý.
    /// </summary>
    /// <param name="direction">Vector hướng di chuyển mong muốn (thường lấy từ Input của người chơi).</param>
    /// <para name = "maxSpeed">Tốc độ tối đa</para>
    /// <para name = "acceleration">là gia tốc (mức tăng tốc), tức là vận tốc sẽ tăng thêm bao nhiêu trong mỗi giây khi người chơi giữ phím di chuyển.</para>
    public void Move(Vector3 direction, float maxSpeed, float acceleration) 
    {
        var deltaTime    = Runner.DeltaTime;
        var previousPos  = transform.position;
        var moveVelocity = Data.Velocity;

        direction = direction.normalized;

        if (Grounded && moveVelocity.y < 0) {
            moveVelocity.y = 0f;
        }

        moveVelocity.y += _data.Gravity * Runner.DeltaTime;

        var horizontalVel = default(Vector3);
        horizontalVel.x = moveVelocity.x;
        horizontalVel.z = moveVelocity.z;

        if (direction == default) {
            horizontalVel = Vector3.Lerp(horizontalVel, default, _data.Braking * deltaTime);
            if (horizontalVel.sqrMagnitude < 0.0001f)
                horizontalVel = Vector3.zero;
        } else {
            horizontalVel = Vector3.ClampMagnitude(horizontalVel + direction * acceleration * deltaTime, maxSpeed);
        }

        moveVelocity.x = horizontalVel.x;
        moveVelocity.z = horizontalVel.z;

        _controller.Move(moveVelocity * deltaTime);

        // --- SỬA Ở ĐÂY ---
        // Lấy displacement thực tế sau khi CharacterController xử lý va chạm/slide
        Vector3 actualDisplacement = transform.position - previousPos;

        // XZ: dùng displacement thực để phản ánh việc bị chặn/trượt tường
        // Y:  dùng moveVelocity.y vì displacement Y bị ảnh hưởng bởi step/slope,
        //     không phản ánh đúng vận tốc dọc cần giữ (trọng lực tích lũy)
        Data.Velocity = new Vector3(
            actualDisplacement.x / deltaTime,
            moveVelocity.y,
            actualDisplacement.z / deltaTime
        );
    }
    
    // Gizmo
    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;
        if (_groundCheckPoint == null)
        {
            Debug.LogWarning("Ground Check point is null!");
            return;
        }
        Vector3 point = _groundCheckPoint.position;
        point.y += _data.GroundCheckOffset;
        Vector3 origin = point;

        Gizmos.color = Application.isPlaying && Grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, _data.GroundCheckRadius);
    }
}

/// <summary>
/// Cấu trúc dữ liệu mạng lưu trữ và đồng bộ trạng thái của Character Controller tùy chỉnh qua Photon Fusion.
/// Sử dụng Layout Explicit để tối ưu hóa bộ nhớ và băng thông truyền tải.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
[NetworkStructWeaved(WORDS + 4)]
public struct NetworkCCData : INetworkStruct 
{
    public const int WORDS = NetworkTRSPData.WORDS + 4;
    public const int SIZE  = WORDS * 4;

    [FieldOffset(0)]
    public NetworkTRSPData TRSPData;

    [FieldOffset((NetworkTRSPData.WORDS + 0) * Allocator.REPLICATE_WORD_SIZE)]
    int _grounded;

    [FieldOffset((NetworkTRSPData.WORDS + 1) * Allocator.REPLICATE_WORD_SIZE)]
    Vector3Compressed _velocityData;

    /// <summary>
    /// Vận tốc hiện tại của nhân vật được nén để truyền tải qua mạng.
    /// </summary>
    public Vector3 Velocity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _velocityData;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _velocityData = value;
    }
}

public enum PlayerState
{
    Standing,
    Crouching,
    Slide
}