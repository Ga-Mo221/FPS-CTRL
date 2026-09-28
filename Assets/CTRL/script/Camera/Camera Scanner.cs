using GaMo.TrueInputSystem;
using UnityEngine;

public class CameraScanner : MonoBehaviour
{
    private TISInputBridge _input;
    private AimAssistSettings _aimsetting;
    private TISTargetScanner _targetScanner;
    
    [SerializeField] AimAssistPlatform _enabledPlatforms = AimAssistPlatform.All;
    [SerializeField] private float _speedSnap = 8f;
    [SerializeField] private bool _assistWithoutInput = true;
    [SerializeField] private float _returnSpeed = 3f;

    private float _yaw;
    private float _pitch;
    private Vector2 _originRot; // local yaw, pitch gốc

    private void Awake()
    {
        _input = TISInputBridge.Instance;
        _targetScanner = GetComponent<TISTargetScanner>();
    }

    private void Start()
    {
        // Lưu localRotation gốc
        Vector3 euler = transform.localEulerAngles;
        _yaw   = euler.y > 180f ? euler.y - 360f : euler.y;
        _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        _originRot = new Vector2(_yaw, _pitch);
    }

    private void Update()
    {
        _input.UpdateActivePlatform();
        //if (!SettingManager.Instance.SettingData.Platform.HasFlag(_enabledPlatforms)) return;
        
        if (_input.GestureIsTwoFingerPanning) return;
        _aimsetting = new AimAssistSettings(
            _targetScanner.GetScanAngle(),
            _speedSnap,
            _assistWithoutInput,
            _enabledPlatforms);
        
        Transform targettransform = _input.AimAssistTarget;

        if (targettransform != null && _targetScanner.CurrentAngle <= _targetScanner.GetScanAngle())
        {
            Vector2 assistDelta = _input.AimAssistProcess(
                _aimsetting,
                transform,
                targettransform.position,
                Vector2.zero);

            _yaw   += assistDelta.x;
            _pitch += assistDelta.y;
        }
        else
        {
            // Không có target → lerp về local rotation gốc
            _yaw   = Mathf.LerpAngle(_yaw,   _originRot.x, _returnSpeed * Time.deltaTime);
            _pitch = Mathf.LerpAngle(_pitch, _originRot.y, _returnSpeed * Time.deltaTime);
        }

        // ✅ localRotation thay vì rotation
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}