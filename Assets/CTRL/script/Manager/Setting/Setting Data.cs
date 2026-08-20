using UnityEngine;
using TrueInputSystem;

[CreateAssetMenu(fileName = "SettingData", menuName = "CTRL/Setting Data", order = 1)]
public class SettingData : ScriptableObject
{
    [SerializeField] private AimAssistPlatform _platform = AimAssistPlatform.PC;

    [Header("PC Setting mouse Speed")]
    [SerializeField] private float _PCmouseSensitivity = 1.0f; // khi muốn cập nhật thì hãy gọi rpc_setCameraSpeed() trong NCharMovementHandler thông qua Nplayer check IsLocalPlayer
    [SerializeField] private float _PCrifleADSSensitivity = 0.3f;
    [SerializeField] private float _PCsniperADSSensitivity = 0.1f;

    [Header("GamePad Setting mouse Speed")]
    [SerializeField] private float _GamePadmouseSensitivity = 1.0f;
    [SerializeField] private float _GamePadrifleADSSensitivity = 0.3f;
    [SerializeField] private float _GamePadsniperADSSensitivity = 0.1f;

    public SettingData(AimAssistPlatform platform, float pcMouseSensitivity, float pcRifleADSSensitivity, float pcSniperADSSensitivity, float gamePadMouseSensitivity, float gamePadRifleADSSensitivity, float gamePadSniperADSSensitivity)
    {
        _platform = platform;
        _PCmouseSensitivity = pcMouseSensitivity;
        _PCrifleADSSensitivity = pcRifleADSSensitivity;
        _PCsniperADSSensitivity = pcSniperADSSensitivity;
        _GamePadmouseSensitivity = gamePadMouseSensitivity;
        _GamePadrifleADSSensitivity = gamePadRifleADSSensitivity;
        _GamePadsniperADSSensitivity = gamePadSniperADSSensitivity;
    }

    public AimAssistPlatform Platform
    {
        get => _platform;
        set => _platform = value;
    }

    public float MouseSensitivity
    {
        get => _platform == AimAssistPlatform.PC ? _PCmouseSensitivity : _GamePadmouseSensitivity;
        set
        {
            if (_platform == AimAssistPlatform.PC)
                _PCmouseSensitivity = value;
            else
                _GamePadmouseSensitivity = value;
        }
    }

    public float RifleADSSensitivity
    {
        get => _platform == AimAssistPlatform.PC ? _PCrifleADSSensitivity : _GamePadrifleADSSensitivity;
        set
        {
            if (_platform == AimAssistPlatform.PC)
                _PCrifleADSSensitivity = value;
            else
                _GamePadrifleADSSensitivity = value;
        }
    }

    public float SniperADSSensitivity
    {
        get => _platform == AimAssistPlatform.PC ? _PCsniperADSSensitivity : _GamePadsniperADSSensitivity;
        set
        {
            if (_platform == AimAssistPlatform.PC)
                _PCsniperADSSensitivity = value;
            else
                _GamePadsniperADSSensitivity = value;
        }
    }
}


// [System.Flags]
// public enum AimAssistPlatform
// {
//     None      = 0,
//     PC        = 1 << 0,   // Keyboard + Mouse
//     Gamepad   = 1 << 1,   // Controller (Xbox, PS, v.v.)
//     Mobile    = 1 << 2,   // Touchscreen
//     All       = PC | Gamepad | Mobile
// }