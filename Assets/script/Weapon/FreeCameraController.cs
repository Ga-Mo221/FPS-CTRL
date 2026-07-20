using TrueInputSystem;
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Look Constraints")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("References")]
    [SerializeField] private Transform target; // Thường là Root của nhân vật hoặc phần Hông (Pelvis/Hips)

    [Header("Spine Rotation Settings")]
    [Tooltip("Thêm các xương cột sống theo thứ tự từ dưới lên trên (ví dụ: Spine, Spine1, Spine2)")]
    [SerializeField] private Transform[] spineBones;
    [Range(0f, 1f)]
    [Tooltip("Tỷ lệ phân chia góc cúi/ngửa cho mỗi xương. Tổng các phần tử nên bằng 1 (hoặc nhỏ hơn tùy ý bạn).")]
    [SerializeField] private float[] spineRotationWeights;

    private float _pitch = 0f;
    private float _yaw = 0f;

    public void SetPitch(float pitch)
    {
        _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    [Header("Test Settings")]
    [SerializeField] private float testRotationSpeed = 10000f;
    private PlayerMotionController _anim;

    private TISInputBridge _input;

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _anim = GetComponent<PlayerMotionController>();
    }

    private void Start()
    {
        _yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        Vector2 mouseDelta = _input.LookDelta;

        _anim.ActiveWeponCTRL().SetLookDelta(mouseDelta);

        _yaw += mouseDelta.x * testRotationSpeed  * Time.deltaTime;
        target.rotation = Quaternion.Euler(0f, _yaw, 0f);

        _pitch -= mouseDelta.y * testRotationSpeed * Time.deltaTime;
        SetPitch(_pitch);
    }

    // Quan trọng: Phải xoay xương trong LateUpdate để ghi đè lên Animator
    private void LateUpdate()
    {
        RotateSpineBones();
    }

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

            float bonePitch = _pitch * weight;

            // Tạo quaternion xoay quanh worldPitchAxis trong world space,
            // rồi áp lên rotation hiện tại của bone (đã được Animator set trong LateUpdate).
            // Thứ tự:  worldDelta * boneRotation  =  "xoay thêm trong world space"
            Quaternion worldDelta = Quaternion.AngleAxis(bonePitch, worldPitchAxis);
            spineBones[i].rotation = worldDelta * spineBones[i].rotation;
        }
    }
}