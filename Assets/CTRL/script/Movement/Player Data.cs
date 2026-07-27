using UnityEngine;

[CreateAssetMenu(fileName = "Player Data", menuName = "FPS/Data/Player Data")]
public class PlayerData : ScriptableObject
{
    [Tooltip("Lực hấp dẫn áp dụng lên nhân vật (giá trị âm để kéo nhân vật xuống).")]
    [SerializeField] private float _gravity = -20.0f;

    [Tooltip("Lực đẩy khi nhảy. Giá trị càng cao nhân vật nhảy càng cao.")]
    [SerializeField] private float _jumpImpulse = 8.0f;

    [Tooltip("Tốc độ gia tốc khi nhân vật bắt đầu di chuyển.")]
    [SerializeField] private float _acceleration = 300.0f;

    [Tooltip("Tốc độ giảm tốc (hãm phanh) khi nhân vật dừng di chuyển.")]
    [SerializeField] private float _braking = 10.0f;

    
    [Header("Camera")]
    [Tooltip("Tốc độ xoay chuyển hướng của nhân vật.")]
    [SerializeField] private float _yaw_model = 1000.0f;
    [SerializeField] private float _pitch_camera = 1000.0f;
    [SerializeField] private float _pitch_Min = -80;
    [SerializeField] private float _pitch_Max = 80;
    
    [Header("Ground Check")]
    [SerializeField] private float _groundCheckRadius = 0.3f;
    [SerializeField] private float _groundCheckOffset = 0.25f; // khoảng cách từ đáy controller
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Movevement")] 
    [SerializeField] private float _aim_Speed = 1.2f;
    [SerializeField] private float _walk_Speed = 4.7f;
    [SerializeField] private float _crouch_Speed = 2.5f;
    [SerializeField] private float _run_Speed = 5.8f;
    [SerializeField] private float _sprint_Speed = 7.6f;
    
    [Header("Slide")]
    [Tooltip("Tốc độ tối đa đạt được khi trượt (slide).")]
    [SerializeField] private float _slide_Speed = 10.0f;
    [Tooltip("Gia tốc khi trượt — để thấp nhằm tạo cảm giác quán tính, khó đổi hướng đột ngột.")]
    [SerializeField] private float _slide_acceleration = 0.0f;
    [Tooltip("Thời gian giữ tốc độ slide cao nhất trước khi bắt đầu giảm dần về giá trị standing.")]
    [SerializeField] private float _slide_Hold_Duration = 0.4f;
    
    [Header("Animation")]
    [SerializeField] private float _moveSmoothSpeed = 0.08f;

    [Header("State Character Controller")] 
    [SerializeField] private float _stand_Height = 2.71f;
    [SerializeField] private float _stand_Center_Y = 0.9f;
    
    [SerializeField] private float _crouch_Height = 1.2f;
    [SerializeField] private float _crouch_Center_Y = 0.1f;
    
    [SerializeField] private float _slide_Height = 1f;
    [SerializeField] private float _slide_Center_Y = 0.045f;
    

    public float Gravity => _gravity;
    public float JumpImpulse => _jumpImpulse;
    public float Braking => _braking;
    public float MoveSmooth => _moveSmoothSpeed;
    
    // Camera
    public float YawModel => _yaw_model;
    public float PitchCamera => _pitch_camera;
    public float PitchMin => _pitch_Min;
    public float PitchMax => _pitch_Max;
    
    
    // Ground Check
    public float GroundCheckRadius => _groundCheckRadius;
    public float GroundCheckOffset => _groundCheckOffset;
    public LayerMask GroundMask => _groundMask;
    
    public float SlideHoldDuration => _slide_Hold_Duration;
    
    // Set Height Character Controller
    public float StandHeight => _stand_Height;
    public float Stand_Center_Y => _stand_Center_Y;
    public float CrouchHeight => _crouch_Height;
    public float Crouch_Center_Y => _crouch_Center_Y;
    public float SlideHeight => _slide_Height;
    public float Slide_Center_Y => _slide_Center_Y;

    public (float currentSpeed, float Acce) MaxSpeed(PlayerStateFlags state)
    {
        float maxspeed = _walk_Speed;
        float acce = _acceleration;
        
        if (state.IsADS || state.IsThrow) maxspeed = _aim_Speed;
        else if (state.IsSlide)
        {
            acce = _slide_acceleration;
            maxspeed = _slide_Speed;
        }
        else if (state.IsRun) maxspeed =  _run_Speed;
        else if (state.IsSprint) maxspeed =  _sprint_Speed;
        else if (state.IsCrouch) maxspeed =  _crouch_Speed;
        
        return (maxspeed, acce);
    }
}
