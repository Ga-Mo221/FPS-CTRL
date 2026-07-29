using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target; // Thường là Root của nhân vật hoặc phần Hông (Pelvis/Hips)

    [Header("Spine Rotation Settings")]
    [Tooltip("Thêm các xương cột sống theo thứ tự từ dưới lên trên (ví dụ: Spine, Spine1, Spine2)")]
    [SerializeField] private Transform[] spineBones;
    [Range(0f, 1f)]
    [Tooltip("Tỷ lệ phân chia góc cúi/ngửa cho mỗi xương. Tổng các phần tử nên bằng 1 (hoặc nhỏ hơn tùy ý bạn).")]
    [SerializeField] private float[] spineRotationWeights;

    [Header("Death Settings")]
    [Tooltip("Tốc độ lerp pitch về 0 khi nhân vật chết (độ/giây)")]
    [SerializeField] private float deathPitchResetSpeed = 2f;
    private bool _deathResetDone;
    
    [SerializeField] private TransformConstraint _cameraConstraint;

    private float _pitch;
    // Giá trị pitch đang thực sự được áp vào xương (lerp về 0 khi chết)
    private float _activePitch;

    private CharComponentManager _componentManager;
    private NCharMovementHandler _movementHandler;
    // Thêm reference tới component quản lý trạng thái sống/chết
    private NHPHandler _hpHandler;
    private Transform _rootRotate;

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        _movementHandler = _componentManager.NCharMovementHandler;
        _hpHandler = _componentManager.NHPHandler;

        if (_cameraConstraint == null) return;
        _rootRotate = _cameraConstraint.transform;
    }
    
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        if (_componentManager.Camera.transform.parent.gameObject.activeSelf)
            _componentManager.Camera.transform.parent.parent = null;
    }

    // Quan trọng: Phải xoay xương trong LateUpdate để ghi đè lên Animator
    private void LateUpdate()
    {
        bool isDead = _hpHandler != null && _hpHandler.IsDead;

        if (isDead)
        {
            // Đã về 0 hoàn toàn từ frame trước → skip mọi xử lý
            if (_deathResetDone) return;

            float t = Time.deltaTime * deathPitchResetSpeed;

            _activePitch = Mathf.Lerp(_activePitch, 0f, t);

            if (_cameraConstraint != null)
            {
                _cameraConstraint.rotationInfluence = Mathf.Lerp(_cameraConstraint.rotationInfluence, 0f, t);

                // Dùng Slerp thay vì Lerp EulerAngles để tránh wrap-around 360°
                if (_rootRotate != null)
                    _rootRotate.localRotation = Quaternion.Slerp(_rootRotate.localRotation, Quaternion.identity, t);
            }

            // Clamp về 0 khi đã đủ gần — tránh lerp vô tận
            if (Mathf.Abs(_activePitch) < 0.01f)
            {
                _activePitch = 0f;
                if (_cameraConstraint != null)
                {
                    _cameraConstraint.rotationInfluence = 0f;
                    if (_rootRotate != null)
                        _rootRotate.localRotation = Quaternion.identity;
                }
                _deathResetDone = true;
            }

            if (_activePitch == 0f) return;
        }
        else
        {
            _pitch = _movementHandler.NetworkedPitch;
            _activePitch = _pitch;
            _deathResetDone = false;
            _cameraConstraint.rotationInfluence = 1f;
        }

        // Nếu đã về 0 hoàn toàn thì không cần apply gì cả
        if (_activePitch == 0f && isDead) return;

        RotateSpineBones();
    }

    /// <summary>
    /// Trả về pitch hiện tại đang được áp vào xương.
    /// Lúc sống = NetworkedPitch, lúc chết = lerp về 0.
    /// </summary>
    public float GetActivePitch() => _activePitch;

    private void RotateSpineBones()
    {
        if (spineBones == null || spineBones.Length == 0) return;

        // Trục "cúi/ngửa" trong world space: sang trái/phải của target (Right vector).
        // Khi nhân vật xoay theo yaw, trục này tự động đi theo — không cần quan tâm
        // local axes của từng bone bị rigged theo hướng nào nữa.
        Vector3 worldPitchAxis = target.right;

        for (int i = 0; i < spineBones.Length; i++)
        {
            if (spineBones[i] == null) continue;

            float weight = (spineRotationWeights != null && i < spineRotationWeights.Length)
                ? spineRotationWeights[i]
                : 1f / spineBones.Length;

            float bonePitch = _activePitch * weight;

            // Tạo quaternion xoay quanh worldPitchAxis trong world space,
            // rồi áp lên rotation hiện tại của bone (đã được Animator set trong LateUpdate).
            // Thứ tự:  worldDelta * boneRotation  =  "xoay thêm trong world space"
            Quaternion worldDelta = Quaternion.AngleAxis(bonePitch, worldPitchAxis);
            spineBones[i].rotation = worldDelta * spineBones[i].rotation;
        }
    }
}