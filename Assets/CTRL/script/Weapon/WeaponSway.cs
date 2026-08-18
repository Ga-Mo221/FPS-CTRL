using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// WeaponSway - Mô phỏng chuyển động súng theo chuột và di chuyển ngang
/// </summary>
public class WeaponSway : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  Inspector Settings
    // ─────────────────────────────────────────

    [TabGroup("Weapon Sway", "Mouse")]
    [BoxGroup("Weapon Sway/Mouse/Mouse Sway")]
    [Tooltip("Tốc độ làm mượt khi súng theo chuột. Giá trị cao = phản ứng nhanh hơn.")]
    [SerializeField] private float mouseSwaySmoothness = 8f;
    
    [BoxGroup("Weapon Sway/Mouse/Mouse Sway")]
    [Tooltip("Cường độ lắc súng theo chuyển động chuột. Giá trị cao = súng lắc mạnh hơn.")]
    [SerializeField] private float mouseSwayMultiplier = 0.06f;

    [BoxGroup("Weapon Sway/Mouse/Mouse Sway")]
    [Tooltip("Giới hạn góc xoay tối đa (độ) do chuột tạo ra. Ngăn súng xoay quá nhiều.")]
    [SerializeField] private float mouseSwayClamp = 8f;

    [BoxGroup("Weapon Sway/Mouse/Aimed Sway (Weight = 1)")]
    [Tooltip("Cường độ lắc theo chuột khi Weight = 1 (aim). Thường nhỏ hơn hip-fire để súng ổn định hơn.")]
    [SerializeField] private float aimedSwayMultiplier = 0.015f;

    [BoxGroup("Weapon Sway/Mouse/Aimed Sway (Weight = 1)")]
    [Tooltip("Giới hạn góc xoay tối đa khi Weight = 1 (aim). Thường nhỏ hơn hip-fire.")]
    [SerializeField] private float aimedSwayClamp = 2.5f;

    [BoxGroup("Weapon Sway/Mouse/Aimed Sway (Weight = 1)")]
    [Tooltip("Hệ số nhân lên toàn bộ bob/strafe/turn khi Weight = 1. 0 = tắt hoàn toàn, 1 = không thay đổi.")]
    [SerializeField] private float aimedBobReduction = 0.3f;

    [TabGroup("Weapon Sway", "Strafe")]
    [BoxGroup("Weapon Sway/Strafe/Hip-Fire Strafe")]
    [Tooltip("Góc nghiêng súng (độ) khi di chuyển ngang trái/phải. Giá trị cao = súng nghiêng nhiều hơn.")]
    [SerializeField] private float strafeTiltAngle = 5f;

    [BoxGroup("Weapon Sway/Strafe/Hip-Fire Strafe")]
    [Tooltip("Độ dịch chuyển vị trí súng theo trục X khi di chuyển ngang. Tạo cảm giác trọng lượng.")]
    [SerializeField] private float strafeOffsetAmount = 0.015f;

    [BoxGroup("Weapon Sway/Strafe/Hip-Fire Strafe")]
    [Tooltip("Tốc độ làm mượt hiệu ứng strafe. Giá trị cao = phản ứng nhanh, thấp = mượt và trễ hơn.")]
    [SerializeField] private float strafeSmoothness = 6f;

    [BoxGroup("Weapon Sway/Strafe/Aimed Strafe (Weight = 1)")]
    [Tooltip("Góc nghiêng súng (độ) khi di chuyển ngang, lúc Weight = 1 (aim). Thường nhỏ hơn hip-fire để súng ổn định hơn.")]
    [SerializeField] private float aimedStrafeTiltAngle = 1.5f;

    [BoxGroup("Weapon Sway/Strafe/Aimed Strafe (Weight = 1)")]
    [Tooltip("Độ dịch chuyển vị trí súng theo trục X khi di chuyển ngang, lúc Weight = 1 (aim).")]
    [SerializeField] private float aimedStrafeOffsetAmount = 0.005f;

    [BoxGroup("Weapon Sway/Strafe/Aimed Strafe (Weight = 1)")]
    [Tooltip("Tốc độ làm mượt hiệu ứng strafe khi Weight = 1 (aim).")]
    [SerializeField] private float aimedStrafeSmoothness = 6f;
    
    [TabGroup("Weapon Sway","Turn")]
    [Tooltip("Góc nghiêng súng (độ) khi xoay camera nhanh sang trái/phải. Tạo cảm giác quán tính.")]
    [SerializeField] private float turnTiltAmount = 3f;

    [TabGroup("Weapon Sway","Turn")]
    [Tooltip("Tốc độ làm mượt hiệu ứng nghiêng khi xoay. Giá trị thấp = súng trễ hơn khi theo camera.")]
    [SerializeField] private float turnTiltSmoothness = 5f;
    
    [Tooltip("Blend giữa hip-fire và aimed.\n0 = hip-fire: sway/bob đầy đủ.\n1 = aimed: sway/bob giảm theo aimedBobReduction.\nKéo slide hoặc gọi SetWeight() từ code.")]
    [Range(0f, 1f)]
    [SerializeField] private float Aim_Weight = 0f;

    // ─────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────

    private Transform weaponTransform;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    private Vector3 mouseTiltOffset;
    private Vector3 strafeOffset;
    private Vector3 turnOffset;

    private float strafeTiltCurrent;
    private float turnTiltCurrent;

    private Vector2 LookDelta = Vector2.zero;
    private Vector2 MoveDirection = Vector2.zero;
    

    // ─────────────────────────────────────────
    //  Unity Messages
    // ─────────────────────────────────────────

    private void Awake()
    {
        weaponTransform  = transform;
        originalLocalPos = weaponTransform.localPosition;
        originalLocalRot = weaponTransform.localRotation;
    }

    private void Update()
    {
        ComputeMouseSway();
        ComputeStrafeLean();
        ComputeTurnLean();
        ApplyFinalTransform();
    }

    // ─────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────

    /// <summary>Set weight từ code. 0 = hip-fire, 1 = aimed.</summary>
    public void SetWeight(float value) => Aim_Weight = Mathf.Clamp01(value);
    public float GetWeight() => Aim_Weight;
    
    public void SetLookDelta(Vector2 value) => LookDelta = value;
    public void SetMoveDirection(Vector2 value) => MoveDirection = value;

    // ─────────────────────────────────────────
    //  Mouse Sway
    // ─────────────────────────────────────────

    private void ComputeMouseSway()
    {
        float rawX = LookDelta.x;
        float rawY = LookDelta.y;

        float mult  = Mathf.Lerp(mouseSwayMultiplier, aimedSwayMultiplier, Aim_Weight);
        float clamp = Mathf.Lerp(mouseSwayClamp,      aimedSwayClamp,      Aim_Weight);

        float swayX = Mathf.Clamp(-rawX * mult, -clamp, clamp);
        float swayY = Mathf.Clamp(-rawY * mult, -clamp, clamp);

        mouseTiltOffset = Vector3.Lerp(
            mouseTiltOffset,
            new Vector3(swayY, swayX, 0f),
            Time.deltaTime * mouseSwaySmoothness
        );
    }

    // ─────────────────────────────────────────
    //  Strafe Lean
    // ─────────────────────────────────────────

    private void ComputeStrafeLean()
    {
        float h = MoveDirection.x;

        float tiltAngle   = Mathf.Lerp(strafeTiltAngle,    aimedStrafeTiltAngle,    Aim_Weight);
        float offsetAmount = Mathf.Lerp(strafeOffsetAmount, aimedStrafeOffsetAmount, Aim_Weight);
        float smoothness   = Mathf.Lerp(strafeSmoothness,   aimedStrafeSmoothness,   Aim_Weight);

        float targetTilt = -h * tiltAngle;
        float targetPosX = -h * offsetAmount;

        strafeTiltCurrent = Mathf.Lerp(strafeTiltCurrent, targetTilt, Time.deltaTime * smoothness);
        strafeOffset      = new Vector3(targetPosX, 0f, 0f);
    }

    // ─────────────────────────────────────────
    //  Turn Lean
    // ─────────────────────────────────────────

    private void ComputeTurnLean()
    {
        float mouseX     = MoveDirection.y;
        float reducer    = Mathf.Lerp(1f, aimedBobReduction, Aim_Weight);
        float targetTurn = -mouseX * turnTiltAmount * reducer;

        turnTiltCurrent = Mathf.Lerp(turnTiltCurrent, targetTurn, Time.deltaTime * turnTiltSmoothness);
        turnOffset      = new Vector3(0f, 0f, turnTiltCurrent);
    }

    // ─────────────────────────────────────────
    //  Final Transform Apply
    // ─────────────────────────────────────────

    private void ApplyFinalTransform()
    {
        Vector3 finalPos = originalLocalPos + strafeOffset;

        Vector3 finalRot =
            mouseTiltOffset
            + new Vector3(0f, 0f, strafeTiltCurrent)
            + turnOffset;

        weaponTransform.localPosition = Vector3.Lerp(
            weaponTransform.localPosition,
            finalPos,
            Time.deltaTime * mouseSwaySmoothness
        );

        weaponTransform.localRotation = Quaternion.Slerp(
            weaponTransform.localRotation,
            originalLocalRot * Quaternion.Euler(finalRot),
            Time.deltaTime * mouseSwaySmoothness
        );
    }

    // ─────────────────────────────────────────
    //  Gizmo for debug
    // ─────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.2f,
            $"Weight: {Aim_Weight:F2}"
        );
    }
#endif
}