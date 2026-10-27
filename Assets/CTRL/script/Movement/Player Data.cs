using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "Player Data", menuName = "FPS/Data/Player Data")]
public class PlayerData : ScriptableObject
{
    // ─────────────────────────────────────────────
    //  HEALTH
    // ─────────────────────────────────────────────

    [Header("Health")]
    [Tooltip("Lượng máu tối đa của nhân vật.")]
    [SerializeField] private byte _hp = 100;


    // ─────────────────────────────────────────────
    //  PHYSICS
    // ─────────────────────────────────────────────

    [TabGroup("PlayerData", "Physic & Locomotion")]
    [FoldoutGroup("PlayerData/Physic & Locomotion/Physic")]
    [Tooltip("Lực hấp dẫn áp dụng lên nhân vật. Dùng giá trị âm để kéo nhân vật xuống đất.")]
    [SerializeField] private float _gravity = -20.0f;
    
    [FoldoutGroup("PlayerData/Physic & Locomotion/Physic")]
    [Tooltip("Lực đẩy thẳng đứng khi nhân vật nhảy. Giá trị càng cao thì nhảy càng cao.")]
    [SerializeField] private float _jumpImpulse = 8.0f;
    
    [FoldoutGroup("PlayerData/Physic & Locomotion/Physic")]
    [Tooltip("Tốc độ gia tốc khi nhân vật bắt đầu di chuyển. Giá trị cao cho phép đạt tốc độ tối đa nhanh hơn.")]
    [SerializeField] private float _acceleration = 300.0f;
    
    [FoldoutGroup("PlayerData/Physic & Locomotion/Physic")]
    [Tooltip("Tốc độ giảm tốc khi nhân vật ngừng di chuyển. Giá trị cao khiến nhân vật dừng lại gấp hơn.")]
    [SerializeField] private float _braking = 10.0f;


    // ─────────────────────────────────────────────
    //  MOVEMENT SPEED
    // ─────────────────────────────────────────────
    [Header("Locomotion")]
    [FoldoutGroup("PlayerData/Physic & Locomotion/Movement")]
    [Tooltip("Tốc độ di chuyển khi đang ADS (ngắm bằng kính).")]
    [SerializeField] private float _aimSpeed = 1.2f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Movement")]
    [Tooltip("Tốc độ di chuyển khi đi bộ thông thường.")]
    [SerializeField] private float _walkSpeed = 4.7f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Movement")]
    [Tooltip("Tốc độ di chuyển khi đang khom người (crouch).")]
    [SerializeField] private float _crouchSpeed = 2.5f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Movement")]
    [Tooltip("Tốc độ di chuyển khi chạy (run).")]
    [SerializeField] private float _runSpeed = 5.8f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Movement")]
    [Tooltip("Tốc độ di chuyển khi chạy nước rút (sprint).")]
    [SerializeField] private float _sprintSpeed = 7.6f;


    // ─────────────────────────────────────────────
    //  SLIDE
    // ─────────────────────────────────────────────

    [FoldoutGroup("PlayerData/Physic & Locomotion/Slide")]
    [Tooltip("Tốc độ tối đa đạt được ngay khi bắt đầu trượt (slide).")]
    [SerializeField] private float _slideSpeed = 10.0f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Slide")]
    [Tooltip("Gia tốc trong khi đang trượt. Để thấp nhằm tạo cảm giác quán tính, khó đổi hướng đột ngột.")]
    [SerializeField] private float _slideAcceleration = 0.0f;

    [FoldoutGroup("PlayerData/Physic & Locomotion/Slide")]
    [Tooltip("Thời gian (giây) duy trì tốc độ slide tối đa trước khi bắt đầu giảm dần về tốc độ đứng.")]
    [SerializeField] private float _slideHoldDuration = 0.4f;


    // ─────────────────────────────────────────────
    //  CAMERA
    // ─────────────────────────────────────────────

    [TabGroup("PlayerData", "Camera")]
    [Tooltip("Tốc độ xoay ngang (Yaw) của model nhân vật theo input chuột.")]
    [SerializeField] private float _yawModel = 1000.0f;

    [TabGroup("PlayerData", "Camera")]
    [Tooltip("Tốc độ xoay dọc (Pitch) của camera theo input chuột.")]
    [SerializeField] private float _pitchCamera = 1000.0f;

    [TabGroup("PlayerData", "Camera")]
    [Tooltip("Góc nhìn xuống tối đa (độ). Nên là giá trị âm, ví dụ -80.")]
    [SerializeField] private float _pitchMin = -80f;

    [TabGroup("PlayerData", "Camera")]
    [Tooltip("Góc nhìn lên tối đa (độ). Ví dụ 80.")]
    [SerializeField] private float _pitchMax = 80f;

    // [TabGroup("PlayerData", "Camera")]
    // [Tooltip("Hệ số nhân tốc độ xoay camera khi đang ADS. Giá trị thấp hơn giúp ngắm chính xác hơn (0.1 = chậm nhất, 0.9 = gần bình thường).")]
    // [Range(0.1f, 0.9f)]
    // [SerializeField] private float _adsLookSpeedMultiplier = 0.3f;


    // ─────────────────────────────────────────────
    //  GROUND CHECK
    // ─────────────────────────────────────────────
    
    [TabGroup("PlayerData", "NCC")]
    [FoldoutGroup("PlayerData/NCC/Ground Check")]
    [Tooltip("Bán kính hình cầu dùng để kiểm tra va chạm với mặt đất. Nên khớp với bán kính đáy CharacterController.")]
    [SerializeField] private float _groundCheckRadius = 0.3f;

    [FoldoutGroup("PlayerData/NCC/Ground Check")]
    [Tooltip("Khoảng cách offset từ đáy CharacterController đến tâm hình cầu kiểm tra đất.")]
    [SerializeField] private float _groundCheckOffset = 0.25f;

    [FoldoutGroup("PlayerData/NCC/Ground Check")]
    [Tooltip("LayerMask xác định những layer nào được coi là mặt đất hợp lệ.")]
    [SerializeField] private LayerMask _groundMask = ~0;
    
    // ─────────────────────────────────────────────
    //  CHARACTER CONTROLLER STATE
    // ─────────────────────────────────────────────
    
    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Header("Character Controller — Stand")]
    [Tooltip("Chiều cao CharacterController khi nhân vật đứng thẳng.")]
    [SerializeField] private float _standHeight = 2.71f;

    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Tooltip("Tọa độ Y của tâm CharacterController khi nhân vật đứng thẳng.")]
    [SerializeField] private float _standCenterY = 0.9f;

    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Header("Character Controller — Crouch")]
    [Tooltip("Chiều cao CharacterController khi nhân vật khom người.")]
    [SerializeField] private float _crouchHeight = 1.2f;

    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Tooltip("Tọa độ Y của tâm CharacterController khi nhân vật khom người.")]
    [SerializeField] private float _crouchCenterY = 0.1f;

    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Header("Character Controller — Slide")]
    [Tooltip("Chiều cao CharacterController khi nhân vật đang trượt.")]
    [SerializeField] private float _slideHeight = 1f;

    [FoldoutGroup("PlayerData/NCC/Controller Collider Height")]
    [Tooltip("Tọa độ Y của tâm CharacterController khi nhân vật đang trượt.")]
    [SerializeField] private float _slideCenterY = 0.045f;
    
    // ─────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────

    [Header("Animation")]
    [Tooltip("Tốc độ làm mượt (smooth) giá trị di chuyển truyền vào Animator. Giá trị lớn cho chuyển động mượt hơn nhưng phản hồi chậm hơn.")]
    [SerializeField] private float _moveSmoothSpeed = 0.08f;

    // ─────────────────────────────────────────────
    //  SOURCE
    // ─────────────────────────────────────────────

    [TabGroup("sound", "Source")] 
    [FoldoutGroup("sound/Source/Foot Step")] 
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_Foot = 1f;
    [FoldoutGroup("sound/Source/Foot Step")]
    [SerializeField] private AudioClip[] _footStepClip;
    
    [FoldoutGroup("sound/Source/Jump")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_Jump = 1f;
    [FoldoutGroup("sound/Source/Jump")]
    [SerializeField] private AudioClip[] _jumpClips;
    
    [FoldoutGroup("sound/Source/Slide")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_Slide = 1f;
    [FoldoutGroup("sound/Source/Slide")]
    [SerializeField] private AudioClip[] _slideClips;
    
    [FoldoutGroup("sound/Source/Take Damage")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_TakeDamage = 1f;
    [FoldoutGroup("sound/Source/Take Damage")]
    [SerializeField] private AudioClip[] _takeDamageClips;
    
    [FoldoutGroup("sound/Source/Hit Damage")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_HitDamage = 1f;
    [FoldoutGroup("sound/Source/Hit Damage")]
    [SerializeField] private AudioClip[] _hitDamageClips;
    
    [FoldoutGroup("sound/Source/Flashbang")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_Flashbang = 1f;
    [FoldoutGroup("sound/Source/Flashbang")]
    [SerializeField] private AudioClip[] _flashbangClips;

    [FoldoutGroup("sound/Source/Drop Weapon")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_DropWeapon = 1f;
    [FoldoutGroup("sound/Source/Drop Weapon")]
    [SerializeField] private AudioClip[] _dropWeaponClips;

    [FoldoutGroup("sound/Source/Pickup Weapon")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_PickupWeapon = 1f;
    [FoldoutGroup("sound/Source/Pickup Weapon")]
    [SerializeField] private AudioClip[] _pickupWeaponClips;

    // ═════════════════════════════════════════════
    //  PUBLIC PROPERTIES
    // ═════════════════════════════════════════════

    // Health
    public byte HP => _hp;

    // Physics
    public float Gravity           => _gravity;
    public float JumpImpulse       => _jumpImpulse;
    public float Braking           => _braking;

    // Camera
    public float YawModel              => _yawModel;
    public float PitchCamera           => _pitchCamera;
    public float PitchMin              => _pitchMin;
    public float PitchMax              => _pitchMax;
    //public float AdsLookSpeedMultiplier => _adsLookSpeedMultiplier;

    // Ground Check
    public float     GroundCheckRadius => _groundCheckRadius;
    public float     GroundCheckOffset => _groundCheckOffset;
    public LayerMask GroundMask        => _groundMask;

    // Slide
    public float SlideHoldDuration => _slideHoldDuration;

    // Animation
    public float MoveSmooth => _moveSmoothSpeed;

    // Character Controller State
    public float StandHeight   => _standHeight;
    public float StandCenterY  => _standCenterY;
    public float CrouchHeight  => _crouchHeight;
    public float CrouchCenterY => _crouchCenterY;
    public float SlideHeight   => _slideHeight;
    public float SlideCenterY  => _slideCenterY;
    
    // Source
    public float Volume_FootStep => _volume_Foot;
    public AudioClip[] FootStepClip => _footStepClip;
    public float Volume_Jump => _volume_Jump;
    public AudioClip[] JumpClips => _jumpClips;
    public float Volume_Slide => _volume_Slide;
    public AudioClip[] SlideClips => _slideClips;
    public  float Volume_TakeDamage => _volume_TakeDamage;
    public AudioClip[] TakeDamageClips => _takeDamageClips;
    public  float Volume_HitDamage => _volume_HitDamage;
    public AudioClip[] HitDamageClips => _hitDamageClips;
    public  float Volume_Flashbang => _volume_Flashbang;
    public AudioClip[] FlashbangClips => _flashbangClips;
    public  float Volume_DropWeapon => _volume_DropWeapon;
    public AudioClip[] DropWeaponClips => _dropWeaponClips;
    public  float Volume_PickupWeapon => _volume_PickupWeapon;
    public AudioClip[] PickupWeaponClips => _pickupWeaponClips;


    // ═════════════════════════════════════════════
    //  METHODS
    // ═════════════════════════════════════════════

    /// <summary>
    /// Trả về tốc độ tối đa và gia tốc tương ứng với trạng thái hiện tại của nhân vật.
    /// Thứ tự ưu tiên: ADS / Throw > Slide > Run > Sprint > Crouch > Walk.
    /// </summary>
    public (float maxSpeed, float acceleration) MaxSpeed(PlayerStateFlags state)
    {
        float speed = _walkSpeed;
        float accel = _acceleration;

        if (state.IsADS || state.IsThrow)
        {
            speed = _aimSpeed;
        }
        else if (state.IsSlide)
        {
            speed = _slideSpeed;
            accel = _slideAcceleration;
        }
        else if (state.IsRun)
        {
            speed = _runSpeed;
        }
        else if (state.IsSprint)
        {
            speed = _sprintSpeed;
        }
        else if (state.IsCrouch)
        {
            speed = _crouchSpeed;
        }

        return (speed, accel);
    }
}