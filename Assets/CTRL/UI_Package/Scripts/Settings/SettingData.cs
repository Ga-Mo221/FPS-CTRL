using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// TOAN BO thong so tuy chinh cua player (1 asset duy nhat).
/// Giu nguyen pattern cua lead (platform + sensitivity), mo rong them Audio/Graphics/Switch/Crosshair/Privacy.
/// Keybind KHONG luu o day - TrueInputSystem tu luu JSON rieng (TIS_Bindings_*.json).
/// SettingManager serialize ca asset nay ra JSON (local file + PlayFab).
/// </summary>
[CreateAssetMenu(fileName = "SettingData", menuName = "CTRL/Setting Data", order = 1)]
public class SettingData : ScriptableObject
{
    // ================= NEN TANG + DO NHAY =================
    [SerializeField] private AimAssistPlatform _platform = AimAssistPlatform.PC;

    [Header("PC Setting mouse Speed")]
    [SerializeField] private float _PCmouseSensitivity = 1.0f;
    [SerializeField] private float _PCrifleADSSensitivity = 0.3f;
    [SerializeField] private float _PCsniperADSSensitivity = 0.1f;

    [Header("GamePad Setting mouse Speed")]
    [SerializeField] private float _GamePadmouseSensitivity = 1.0f;
    [SerializeField] private float _GamePadrifleADSSensitivity = 0.3f;
    [SerializeField] private float _GamePadsniperADSSensitivity = 0.1f;

    // ================= AM THANH (0..1) =================
    [Header("Audio")]
    [SerializeField, Range(0f,1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f,1f)] private float _musicVolume = 1f;
    [SerializeField, Range(0f,1f)] private float _sfxVolume = 1f;
    [SerializeField, Range(0f,1f)] private float _uiVolume = 1f; //UI+Announcer gop
    [SerializeField] private bool _surround3D = true;   // Am thanh vom (3D / HRTF)

    // ================= DO HOA (Volume / hau ky) =================
    [Header("Graphics - Post Processing (Volume)")]
    [SerializeField, Range(-1f,1f)] private float _brightness = 0f;   // Color Adjustments.postExposure
    [SerializeField, Range(-50f,50f)] private float _contrast = 0f;   // Color Adjustments.contrast
    [SerializeField, Range(-50f,50f)] private float _saturation = 0f; // Color Adjustments.saturation
    [SerializeField] private bool _bloomOn = true;
    [SerializeField, Range(0f,2f)] private float _bloomIntensity = 0.7f;
    [SerializeField] private bool _vignetteOn = true;
    [SerializeField] private bool _motionBlurOn = false;
    [SerializeField] private bool _stableCamera = false; // Chong rung camera (CameraStabilizer giu RAW_Camera co dinh)

    [Header("Graphics - Engine")]
    [SerializeField] private bool _vSync = false;
    [SerializeField] private int _qualityLevel = 2;   // index QualitySettings (0=Thap..3=Toi da)
    [SerializeField, Range(60f,120f)] private float _fov = 90f;
    [SerializeField] private int _textureQuality = 2;  //  0 Thap / 1 Vua / 2 Cao (globalTextureMipmapLimit)
    [SerializeField] private int _antiAliasing = 2;    // 0 Tat / 1 FXAA / 2 SMAA / 3 MSAA 4x / 4 TAA

    [Header("Display")] //  DisplaySettingsApplier
    [SerializeField] private int _displayMode = 1;      // 0 Toan man hinh (Exclusive) / 1 Khong vien / 2 Cua so
    [SerializeField] private int _resolutionWidth = 0;  // 0 = do phan giai goc man hinh
    [SerializeField] private int _resolutionHeight = 0;
    [SerializeField] private int _refreshRate = 0;      // Hz, 0 = cao nhat
    [SerializeField] private int _fpsLimit = 0;         // 0 = khong gioi han (chi tac dung khi V-Sync tat)

    [Header("Ngon ngu")]
    [SerializeField] private int _language = 0;        //  0 Tieng Viet / 1 English

    // ================= CHUOT (them) =================
    [Header("Mouse options")]
    [SerializeField] private bool _invertY = false;
    [SerializeField] private bool _mouseAccel = false;

    // ================= GYRO (tay cam co cam bien: DualShock 4 / DualSense / Switch Pro) =================
    [Header("Gyro")]
    [SerializeField] private int _gyroMode = 0;            //  0 Tat / 1 Khi ngam (giu ADS) / 2 Luon bat
    [SerializeField] private float _gyroSensX = 1.5f;      // he so "the gioi thuc": 1 = xoay tay cam 10 do -> camera 10 do (o do nhay tay cam 1.0)
    [SerializeField] private float _gyroSensY = 1.5f;
    [SerializeField] private bool _gyroInvertX = false;
    [SerializeField] private bool _gyroInvertY = false;

    // ================= TAM NGAM =================
    [Header("Crosshair")]
    [SerializeField] private int _crosshairColorIndex = 0;      // index bang mau (6 o)
    [SerializeField] private float _crosshairSize = 6f;         // Chieu dai tay (0-30)
    [SerializeField] private float _crosshairThickness = 2f;    // Do day (0-10)
    [SerializeField] private float _crosshairGap = 3f;          // Khoang cach tam (0-30)
    [SerializeField] private float _crosshairOpacity = 100f;    // Do mo (0-100 %)
    [SerializeField] private bool _crosshairCenterDot = true;   // Cham giua
    [SerializeField] private bool _crosshairOutline = true;     // Vien den

    // ================= QUYEN RIENG TU =================
    [Header("Privacy")]
    [SerializeField] private bool _allowFriendInvites = true;
    [SerializeField] private bool _showOnlineStatus = true;

    // ============================ PROPERTIES ============================

    public AimAssistPlatform Platform { get { return _platform; } set { _platform = value; } }

    // -- Sensitivity (tu dong theo platform, giu logic lead) --
    public float MouseSensitivity {
        get { return _platform == AimAssistPlatform.PC ? _PCmouseSensitivity : _GamePadmouseSensitivity; }
        set { if (_platform == AimAssistPlatform.PC) _PCmouseSensitivity = value; else _GamePadmouseSensitivity = value; } }
    public float RifleADSSensitivity {
        get { return _platform == AimAssistPlatform.PC ? _PCrifleADSSensitivity : _GamePadrifleADSSensitivity; }
        set { if (_platform == AimAssistPlatform.PC) _PCrifleADSSensitivity = value; else _GamePadrifleADSSensitivity = value; } }
    public float SniperADSSensitivity {
        get { return _platform == AimAssistPlatform.PC ? _PCsniperADSSensitivity : _GamePadsniperADSSensitivity; }
        set { if (_platform == AimAssistPlatform.PC) _PCsniperADSSensitivity = value; else _GamePadsniperADSSensitivity = value; } }

    // -- Audio --
    public float MasterVolume { get { return _masterVolume; } set { _masterVolume = Mathf.Clamp01(value); } }
    public float MusicVolume  { get { return _musicVolume; }  set { _musicVolume  = Mathf.Clamp01(value); } }
    public float SfxVolume    { get { return _sfxVolume; }    set { _sfxVolume    = Mathf.Clamp01(value); } }
    public float UIVolume    { get { return _uiVolume; }    set { _uiVolume    = Mathf.Clamp01(value); } }
    public bool  Surround3D   { get { return _surround3D; }   set { _surround3D   = value; } }

    // -- Graphics post --
    public float Brightness     { get { return _brightness; }     set { _brightness = value; } }
    public float Contrast       { get { return _contrast; }       set { _contrast = value; } }
    public float Saturation     { get { return _saturation; }     set { _saturation = value; } }
    public bool  BloomOn        { get { return _bloomOn; }        set { _bloomOn = value; } }
    public float BloomIntensity { get { return _bloomIntensity; } set { _bloomIntensity = value; } }
    public bool  VignetteOn     { get { return _vignetteOn; }     set { _vignetteOn = value; } }
    public bool  MotionBlurOn   { get { return _motionBlurOn; }   set { _motionBlurOn = value; } }
    public bool  StableCamera   { get { return _stableCamera; }   set { _stableCamera = value; } }

    // -- Graphics engine --
    public bool  VSync        { get { return _vSync; }        set { _vSync = value; } }
    public int   QualityLevel { get { return _qualityLevel; } set { _qualityLevel = value; } }
    public float Fov          { get { return _fov; }          set { _fov = value; } }
    public int   TextureQuality { get { return _textureQuality; } set { _textureQuality = Mathf.Clamp(value, 0, 2); } }
    public int   AntiAliasing   { get { return _antiAliasing; }   set { _antiAliasing = Mathf.Clamp(value, 0, 4); } }
    public int   DisplayMode    { get { return _displayMode; }    set { _displayMode = Mathf.Clamp(value, 0, 2); } }
    public int   ResolutionWidth  { get { return _resolutionWidth; }  set { _resolutionWidth = Mathf.Max(0, value); } }
    public int   ResolutionHeight { get { return _resolutionHeight; } set { _resolutionHeight = Mathf.Max(0, value); } }
    public int   RefreshRate    { get { return _refreshRate; }    set { _refreshRate = Mathf.Max(0, value); } }
    public int   FpsLimit       { get { return _fpsLimit; }       set { _fpsLimit = Mathf.Max(0, value); } }
    public int   Language       { get { return _language; }       set { _language = Mathf.Clamp(value, 0, 1); } }

    // -- Mouse --
    public bool InvertY    { get { return _invertY; }    set { _invertY = value; } }
    public bool MouseAccel { get { return _mouseAccel; } set { _mouseAccel = value; } }

    // -- Gyro --
    public int   GyroMode    { get { return _gyroMode; }    set { _gyroMode = Mathf.Clamp(value, 0, 2); } }
    public float GyroSensX   { get { return _gyroSensX; }   set { _gyroSensX = Mathf.Clamp(value, 0.1f, 5f); } }
    public float GyroSensY   { get { return _gyroSensY; }   set { _gyroSensY = Mathf.Clamp(value, 0.1f, 5f); } }
    public bool  GyroInvertX { get { return _gyroInvertX; } set { _gyroInvertX = value; } }
    public bool  GyroInvertY { get { return _gyroInvertY; } set { _gyroInvertY = value; } }

    // -- Crosshair --
    public int   CrosshairColorIndex { get { return _crosshairColorIndex; } set { _crosshairColorIndex = value; } }
    public float CrosshairSize       { get { return _crosshairSize; }       set { _crosshairSize = value; } }
    public float CrosshairThickness  { get { return _crosshairThickness; }  set { _crosshairThickness = value; } }
    public float CrosshairGap        { get { return _crosshairGap; }        set { _crosshairGap = value; } }
    public float CrosshairOpacity    { get { return _crosshairOpacity; }    set { _crosshairOpacity = value; } }
    public bool  CrosshairCenterDot  { get { return _crosshairCenterDot; }  set { _crosshairCenterDot = value; } }
    public bool  CrosshairOutline    { get { return _crosshairOutline; }    set { _crosshairOutline = value; } }

    // -- Privacy --
    public bool AllowFriendInvites { get { return _allowFriendInvites; } set { _allowFriendInvites = value; } }
    public bool ShowOnlineStatus   { get { return _showOnlineStatus; }   set { _showOnlineStatus = value; } }


    //  Constructor rong: dam bao gia tri mac dinh (khoi tao truong) duoc dung khi Unity tao asset/
    // CreateInstance (truoc day chi co constructor co tham so -> truong moi them vao bi 0: Texture "Thap", AA "Tat").
    public SettingData() { }

    // Constructor tuong thich ban goc cua lead (it dung: SO thuong tao qua CreateInstance).
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
}
