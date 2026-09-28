using UnityEngine;
using GaMo.TrueInputSystem;
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
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách dùng TIS API để điều khiển xoay camera\n" +
            "  theo input delta từ nhiều nguồn: Mouse, Gamepad RightStick hoặc kéo\n" +
            "  ngón tay trên mobile. Hỗ trợ Aim Assist tự động kéo ngắm về target\n" +
            "  gần nhất khi có AimAssistScanner trong scene.\n\n" +

            "PLATFORM: PC (Mouse / Gamepad) · Mobile (Touch Drag)\n\n" +

            "SETUP\n" +
            "1. Scene cần có TISInputBridge (và TISMobileInputSetup nếu dùng mobile).\n" +
            "2. Gán script này lên GameObject Camera hoặc Camera Rig.\n" +
            "3. Field '_camera' là Transform sẽ xoay theo cả yaw lẫn pitch.\n" +
            "   Để trống → script tự dùng chính transform của nó.\n" +
            "4. Để tách yaw/pitch (rig 2 tầng): bỏ comment dòng\n" +
            "   transform.localRotation trong Update và gán _camera là pivot con.\n\n" +

            "CHẾ ĐỘ HOẠT ĐỘNG\n" +
            "  [Không có AimAssistTarget]\n" +
            "    LookDelta được áp trực tiếp — xoay tự do theo input.\n\n" +
            "  [Có AimAssistTarget]\n" +
            "    Gọi AimAssistProcess() để kéo ngắm về phía target.\n" +
            "    Cần AimAssistScanner trong scene để detect target.\n\n" +

            "API\n" +
            "  bridge.LookDelta                →  Vector2 (delta per-frame)\n" +
            "    • x = yaw  (trái / phải)\n" +
            "    • y = pitch (lên / xuống, âm = nhìn xuống)\n" +
            "  bridge.AimAssistTarget          →  Transform (target gần nhất, hoặc null)\n" +
            "  bridge.AimAssistProcess(...)    →  Vector2 (look đã được điều chỉnh)\n" +
            "  bridge.GestureIsTwoFingerPanning →  bool (bỏ qua look khi đang pan 2 ngón)\n\n" +

            "GHI CHÚ\n" +
            "  – LookDelta là delta per-frame, đã nhân sensitivity trong TIS profile.\n" +
            "  – Pitch bị clamp trong [pitchMin, pitchMax] để tránh lật đầu camera.\n" +
            "  – snapRadius trong AimAssistSettings phải >= scanAngle của Scanner.\n" +
            "  – Trên PC, Cursor sẽ bị Lock tự động khi Start (trừ khi SimulateMobile).",
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