using UnityEngine;
using TrueInputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Demo: LookDelta
/// ──────────────────────────────────────────────
/// Đọc delta xoay camera từ Mouse / Gamepad RightStick / Kéo ngón tay.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_LookDelta))]
public class TIS_Demo_LookDelta_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "SETUP\n" +
            "1. Gán script này lên GameObject Camera (hoặc Camera Rig).\n" +
            "2. Đảm bảo scene có TISInputBridge.\n" +
            "3. Field 'CameraPivot' là Transform sẽ xoay theo pitch (up/down).\n" +
            "   Để trống → script tự xoay chính transform của nó.\n\n" +
            "API\n" +
            "  TISInputBridge.Instance.LookDelta  →  Vector2\n" +
            "  • x = yaw  (trái/phải)\n" +
            "  • y = pitch (lên/xuống, âm = nhìn xuống)\n\n" +
            "GHI CHÚ\n" +
            "  – LookDelta là delta per-frame, đã nhân sensitivity trong profile.\n" +
            "  – Clamp pitch trong [-80, 80] để tránh lật đầu.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/LookDelta")]
public class TIS_Demo_LookDelta : MonoBehaviour
{
    [Header("Look Settings")]
    public float sensitivity = 1f;
    [Range(-90f, 0f)]  public float pitchMin = -80f;
    [Range(0f,  90f)]  public float pitchMax =  80f;

    [FormerlySerializedAs("camera")]
    [Header("References")]
    [Tooltip("Transform xoay pitch (up/down). Để trống = dùng chính transform này.")]
    public Transform _camera;

    private TISInputBridge _input;
    private float _yaw;
    private float _pitch;
    
    private AimAssistSettings aimsetting;
    [SerializeField] AimAssistPlatform enabledPlatforms = AimAssistPlatform.All;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        if (_camera == null) _camera = transform;
        aimsetting = new AimAssistSettings(
            snapRadius:          15f,   // ← tăng lên, phải >= scanAngle của Scanner (10°)
            assistSpeed:         10f,   // tốc độ kéo
            assistWithoutInput:  true,  // tự kéo khi đứng yên
            enabledPlatforms:    enabledPlatforms
        );
        if (_input.SimulateMobileInEditor()) return;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (_input.GestureIsTwoFingerPanning) return;

        Vector2 inputlook = _input.LookDelta;
        Vector2 rawlook = new Vector2(inputlook.x, -inputlook.y);

        // Fix 1: chỉ gọi AimAssist khi thực sự có target
        Transform targettransform = _input.AimAssistTarget;
        Vector2 look;
        if (targettransform != null)
        {
            look = _input.AimAssistProcess(aimsetting, _camera, targettransform.position, rawlook);
        }
        else
        {
            look = rawlook;
        }

        _yaw   += look.x * sensitivity;
        _pitch += look.y * sensitivity;
        _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        if (targettransform != null)
            Debug.Log($"Look: {look} Yaw: {_yaw}, Pitch: {_pitch}, target: {targettransform.name}");
        // Fix 2: tách yaw/pitch nếu có pivot riêng
        //transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        _camera.localRotation   = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}