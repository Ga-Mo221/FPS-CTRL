using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GaMo.TrueInputSystem;

using Sirenix.OdinInspector;
using Backend.Bootstrap;

/// <summary>
/// MANAGER DUY NHAT quan ly TOAN BO setting cua player (mo rong tu mau lead).
/// - Giu 1 SettingData (asset). Save/Load qua JSON local + PlayFab. Apply xuong engine/Volume/audio/keybind.
/// - Keybind: dung TrueInputSystem (TIS tu luu JSON rieng); manager chi goi LoadAndApply / ResetAllBindings.
/// - Phan can NPlayer ben game (mouse sensitivity RPC) -> thay bang event OnSensitivityChanged de game hook,
///   nho vay manager compile duoc o CA project UI lan game.
///
/// LEAD - cach dung:
/// * Setup 1 lan: dat 1 SettingManager (persist tu Menu qua DontDestroyOnLoad, hoac dat trong scene game),
///   gan _settingData = PlayerSettingData.asset, _postVolume = Global Volume scene game, (tuy chon) _mixer.
/// * Vao scene game: goi SetPostVolume(sceneVolume) de hook post-processing cua scene do.
/// * Doc du lieu: SettingManager.Instance.SettingData (mouse sens, FOV, crosshair...).
/// * Game hook: dang ky OnSensitivityChanged(float) -> ap sens vao camera; OnSettingsApplied(SettingData) -> vd set camera FOV.
/// * UI da tu goi Apply()+Save() qua SettingsBinder khi nguoi dung chinh; code khac khong can goi tay.
/// </summary>
public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    [SerializeField] private SettingData _settingData;
    public SettingData SettingData { get { return _settingData; } }
    public Volume PostVolume { get { return _postVolume; } }

    [Header("Audio (tuy chon)")]
    [SerializeField] private UnityEngine.Audio.AudioMixer _mixer;
    public UnityEngine.Audio.AudioMixer Mixer => _mixer;
    [SerializeField] private string _musicParam = "MusicVolume";
    [SerializeField] private string _sfxParam = "SfxVolume";
    [SerializeField] private string _masterParam = "MasterVolume"; //  Master qua mixer cho dong bo
    [SerializeField] private string _uiParam = "UIVolume";           // UI+Announcer gop 1 slider
    [SerializeField] private string _announcerParam = "AnnouncerVolume";

    [Header("Graphics - Global Volume (gan o scene game; de trong o menu)")]
    [SerializeField] private Volume _postVolume;

    [Header("Persistence")]
    [Tooltip("Dong bo len PlayFab (ngoai file JSON local).")]
    [SerializeField] private bool _syncPlayFab = true;

    private string FilePath { get { return Path.Combine(Application.persistentDataPath, "PlayerSettings.json"); } }

    // --- Events cho gameplay hook (thay RPC truc tiep NPlayer) ---
    /// <summary>Ban khi setting duoc apply (sau Load hoac thay doi).</summary>
    public event Action<SettingData> OnSettingsApplied;
    /// <summary>Ban gia tri MouseSensitivity moi (game hook de goi RPC_SetCameraSpeed).</summary>
    public event Action<float> OnSensitivityChanged;
    /// <summary> Ban khi Platform (thiet bi dieu khien) doi -> UI keybind hook doi layout.</summary>
    public event Action<AimAssistPlatform> OnPlatformChanged;

    // cached Volume overrides
    private ColorAdjustments _color; private Bloom _bloom; private Vignette _vignette; private MotionBlur _motionBlur;
    private Camera _aaCamera; private float _aaCheckAt;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!enabled) enabled = true;   // Tu bat lai neu checkbox component bi tat -> dam bao Update device-follow + runtime luon chay.
        DontDestroyOnLoad(gameObject);
        CacheVolume();
        Load();
        SceneManager.sceneLoaded += OnSceneLoaded;   // tu hook Global Volume moi khi vao scene
    }

    // AudioMixer.SetFloat goi trong Awake bi Unity bo qua (mixer chua khoi tao xong) -> am luong luu trong Cai dat
    // khong co hieu luc khi mo game. Ap lai o Start (va moi lan vao scene moi cho chac).
    private void Start()
    {
        if (_settingData != null) ApplyAudio();
    }

    //  Khi load scene moi, tu tim Global Volume trong scene do va hook post-processing.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var v = FindFirstObjectByType<Volume>();
        if (v != null && v != _postVolume) SetPostVolume(v);
        if (_settingData != null) ApplyAudio();
    }

    private void OnDestroy()   //  go event + clear Instance khi bi destroy (vd duplicate)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    //  Device-follow: theo doi thiet bi TIS dang dung -> cap nhat Platform + ban events (sens/view follow thiet bi).
    private AimAssistPlatform _lastPlatform = (AimAssistPlatform)(-1);
    private void Update()
    {
        if (_settingData == null) return;
        // Camera nguoi choi sinh sau khi vao tran -> ap khu rang cua cho camera moi.
        if (Time.unscaledTime >= _aaCheckAt)
        {
            _aaCheckAt = Time.unscaledTime + 0.5f;
            var cam = Camera.main;
            if (cam != null && cam != _aaCamera) { _aaCamera = cam; GraphicsQualityApplier.ApplyAllCameras(); }
        }
        var bridge = TISInputBridge.Instance;
        if (bridge == null) return;
        if (bridge != _keybindsLoadedFor) ApplyKeybinds();   // bridge sinh sau SettingManager -> van nap phim da luu
        MotionSettingsApplier.Tick(_settingData);            //gyro: ap lai khi bridge / profile duoc dung lai (rebind, swap)
        AimAssistPlatform p;
        switch (bridge.ActiveDevice)
        {
            case TISActiveDevice.Gamepad: p = AimAssistPlatform.Gamepad; break;
            case TISActiveDevice.Mobile:  p = AimAssistPlatform.Mobile;  break;
            default:                      p = AimAssistPlatform.PC;      break;
        }
        if (p == _lastPlatform) return;
        _lastPlatform = p;
        if (_settingData.Platform != p)
        {
            _settingData.Platform = p;
            if (OnSensitivityChanged != null) OnSensitivityChanged(_settingData.MouseSensitivity);
            if (OnSettingsApplied != null) OnSettingsApplied(_settingData);
            if (OnPlatformChanged != null) OnPlatformChanged(p);
        }
    }

    private void CacheVolume()
    {
        if (_postVolume == null || _postVolume.profile == null) return;
        _postVolume.profile.TryGet(out _color);
        _postVolume.profile.TryGet(out _bloom);
        _postVolume.profile.TryGet(out _vignette);
        _postVolume.profile.TryGet(out _motionBlur);
    }

    /// <summary>Gan Volume luc runtime (vd khi vao scene game) roi apply lai.</summary>
    public void SetPostVolume(Volume v) { _postVolume = v; CacheVolume(); ApplyGraphics(); }

    // ============================ SAVE ============================
    /// <summary>Luu setting ra JSON local + (tuy chon) PlayFab. Goi sau moi thay doi.</summary>
    public void Save()
    {
        if (_settingData == null) return;
        string json = JsonUtility.ToJson(_settingData);
        try { File.WriteAllText(FilePath, json); } catch (Exception e) { Debug.LogWarning("[SettingManager] Luu local loi: " + e.Message); }
        if (_syncPlayFab) PushToPlayFab(json);
    }

    private void PushToPlayFab(string json)
    {
        var svc = BackendBootstrap.SettingsService;
        if (svc == null) return; // chua login / backend chua san -> chi luu local
        // Boc try/catch: PlayFab nem exception DONG BO khi chua dang nhap -> nuot loi, chi luu local (tranh chet Awake).
        try { svc.SaveSettings(json, null, err => Debug.LogWarning("[SettingManager] PlayFab save loi: " + (err != null ? err.ErrorMessage : "?"))); }
        catch (Exception e) { Debug.LogWarning("[SettingManager] Chua the sync PlayFab (save): " + e.Message); }
    }

    // ============================ LOAD ============================
    /// <summary>Doc JSON local -> apply; roi keo PlayFab phu len neu co.</summary>
    public void Load()
    {
        if (_settingData == null) { Debug.LogWarning("[SettingManager] Chua gan SettingData."); return; }
        if (File.Exists(FilePath))
        {
            try { JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), _settingData); } catch (Exception e) { Debug.LogWarning("[SettingManager] Doc local loi: " + e.Message); }
        }
        if (_settingData.Platform == AimAssistPlatform.None) _settingData.Platform = AimAssistPlatform.PC;   //  tranh Platform None lay nham slot sens
        Apply();
        if (_syncPlayFab) PullFromPlayFab();
    }

    private void PullFromPlayFab()
    {
        var svc = BackendBootstrap.SettingsService;
        if (svc == null) return;
        // Boc try/catch: PlayFab GetUserData nem exception DONG BO khi chua dang nhap -> nuot loi (tranh chet Awake -> Unity disable component).
        try { svc.LoadSettings(json =>
        {
            if (string.IsNullOrEmpty(json) || _settingData == null) return;
            try { JsonUtility.FromJsonOverwrite(json, _settingData); } catch { return; }
            if (_settingData.Platform == AimAssistPlatform.None) _settingData.Platform = AimAssistPlatform.PC; 
            Apply();
            try { File.WriteAllText(FilePath, json); } catch { }
        }, err => { /* offline -> giu ban local */ }); }
        catch (Exception e) { Debug.LogWarning("[SettingManager] Chua the sync PlayFab (load): " + e.Message); }
    }

    // ============================ APPLY ============================
    /// <summary>Ap TOAN BO setting hien tai xuong engine/Volume/audio/keybind + ban events.</summary>
    public void Apply()
    {
        if (_settingData == null) return;
        Game.UI.Loc.SetLanguage(_settingData.Language); // ngon ngu giao dien
        ApplyAudio();
        ApplyGraphics();
        ApplyKeybinds();
        MotionSettingsApplier.Apply(_settingData); //  gyro tay cam
        if (OnSensitivityChanged != null) OnSensitivityChanged(_settingData.MouseSensitivity);
        if (OnSettingsApplied != null) OnSettingsApplied(_settingData);
    }

    private void ApplyAudio()
    {
        ApplyMixer(_masterParam, _settingData.MasterVolume); // Master -> mixer group Master (dong bo)
        AudioListener.volume = 1f;
        ApplyMixer(_musicParam, _settingData.MusicVolume);
        ApplyMixer(_sfxParam, _settingData.SfxVolume);
        ApplyMixer(_uiParam, _settingData.UIVolume);        // 1 slider -> ca UI
        ApplyMixer(_announcerParam, _settingData.UIVolume); // ... va Announcer
    }
    private void ApplyMixer(string param, float v01)
    {
        if (_mixer == null || string.IsNullOrEmpty(param)) return;
        float db = v01 <= 0.0001f ? -80f : Mathf.Log10(v01) * 20f;
        _mixer.SetFloat(param, db);
    }

    private void ApplyGraphics()
    {
        if (_settingData == null) return; //  guard tranh NRE khi chua gan SettingData
        QualitySettings.vSyncCount = _settingData.VSync ? 1 : 0;
        // Project chi co 2 muc QualitySettings (Mobile/PC) -> tu ap bac THAP..TOI DA + texture + AA.
        GraphicsQualityApplier.Apply(_settingData);
        DisplaySettingsApplier.Apply(_settingData); // che do man hinh / do phan giai / Hz / gioi han FPS
        ApplyPostVolume();
    }

    /// <summary>Ap lai trang thai Volume theo setting (bac chat luong + mau + Bloom/Vignette/Motion Blur).
    /// VolumeController goi sau khi hieu ung flashbang / ADS ket thuc de tra Volume ve dung setting.</summary>
    public void ApplyPostVolume()
    {
        if (_settingData == null) return;
        if (_postVolume != null && _postVolume.profile != null) GraphicsQualityApplier.ApplyVolumeTier(_postVolume);
        if (_color != null)
        {
            _color.postExposure.overrideState = true; _color.postExposure.value = _settingData.Brightness;
            _color.contrast.overrideState = true;     _color.contrast.value = _settingData.Contrast;
            _color.saturation.overrideState = true;   _color.saturation.value = _settingData.Saturation;
        }
        if (_bloom != null) { _bloom.active = _settingData.BloomOn; _bloom.intensity.overrideState = true; _bloom.intensity.value = _settingData.BloomIntensity; }
        if (_vignette != null) _vignette.active = _settingData.VignetteOn;
        if (_motionBlur != null) _motionBlur.active = _settingData.MotionBlurOn;
    }

    // Chi LoadAndApply 1 lan cho moi bridge. Truoc day moi lan keo slider (Apply) deu doc JSON +
    // rebuild toan bo InputActionAsset -> giat input tay cam khi giu D-pad chinh slider. TIS tu rebuild sau moi rebind/reset.
    private TISInputBridge _keybindsLoadedFor;
    private void ApplyKeybinds()
    {
        var bridge = TISInputBridge.Instance;
        if (bridge == null || bridge == _keybindsLoadedFor) return;
        _keybindsLoadedFor = bridge;
        TISRebindingSystem.LoadAndApply(bridge);
    }

    // ============================ RESET ============================
    /// <summary>Khoi phuc mac dinh: reset SettingData + keybind (TIS) roi apply + luu.</summary>
    public void ResetToDefaults()
    {
        if (_settingData == null) return;
        var def = ScriptableObject.CreateInstance<SettingData>();
        string json = JsonUtility.ToJson(def);
        JsonUtility.FromJsonOverwrite(json, _settingData);
        ScriptableObject.Destroy(def);
        var bridge = TISInputBridge.Instance;
        if (bridge != null) TISRebindingSystem.ResetAllBindings(bridge);
        Apply();
        Save();
    }


    // Reset chi 1 nhom (tab) ve mac dinh.
    public void ResetCategory(string cat)
    {
        if (_settingData == null) return;
        if (string.IsNullOrEmpty(cat) || cat == "all") { ResetToDefaults(); return; }
        var d = ScriptableObject.CreateInstance<SettingData>();
        switch (cat)
        {
            case "audio":
                _settingData.MasterVolume = d.MasterVolume; _settingData.SfxVolume = d.SfxVolume; _settingData.MusicVolume = d.MusicVolume; _settingData.Surround3D = d.Surround3D; _settingData.VoiceMic = d.VoiceMic; _settingData.VoiceHear = d.VoiceHear; _settingData.VoicePushToTalk = d.VoicePushToTalk; _settingData.VoiceVolume = d.VoiceVolume; _settingData.VoiceMicDevice = d.VoiceMicDevice; break;
            case "mouse":
                _settingData.MouseSensitivity = d.MouseSensitivity; _settingData.RifleADSSensitivity = d.RifleADSSensitivity; _settingData.SniperADSSensitivity = d.SniperADSSensitivity; _settingData.InvertY = d.InvertY; _settingData.MouseAccel = d.MouseAccel;
                _settingData.GyroMode = d.GyroMode; _settingData.GyroSensX = d.GyroSensX; _settingData.GyroSensY = d.GyroSensY; _settingData.GyroInvertX = d.GyroInvertX; _settingData.GyroInvertY = d.GyroInvertY; break;
            case "graphics":
                _settingData.Fov = d.Fov; _settingData.VSync = d.VSync; _settingData.Brightness = d.Brightness; _settingData.Contrast = d.Contrast; _settingData.Saturation = d.Saturation; _settingData.BloomOn = d.BloomOn; _settingData.BloomIntensity = d.BloomIntensity; _settingData.VignetteOn = d.VignetteOn; _settingData.MotionBlurOn = d.MotionBlurOn; _settingData.StableCamera = d.StableCamera; _settingData.StableCameraStrength = d.StableCameraStrength; _settingData.QualityLevel = d.QualityLevel; _settingData.TextureQuality = d.TextureQuality; _settingData.AntiAliasing = d.AntiAliasing; _settingData.DisplayMode = d.DisplayMode; _settingData.ResolutionWidth = d.ResolutionWidth; _settingData.ResolutionHeight = d.ResolutionHeight; _settingData.RefreshRate = d.RefreshRate; _settingData.FpsLimit = d.FpsLimit; break;
            case "crosshair":
                _settingData.CrosshairColorIndex = d.CrosshairColorIndex; _settingData.CrosshairThickness = d.CrosshairThickness; _settingData.CrosshairSize = d.CrosshairSize; _settingData.CrosshairGap = d.CrosshairGap; _settingData.CrosshairOpacity = d.CrosshairOpacity; _settingData.CrosshairCenterDot = d.CrosshairCenterDot; _settingData.CrosshairOutline = d.CrosshairOutline; break;
            case "privacy":
                _settingData.AllowFriendInvites = d.AllowFriendInvites; _settingData.ShowOnlineStatus = d.ShowOnlineStatus; break;
            case "keybind":
                { var bridge = TISInputBridge.Instance; if (bridge != null) TISRebindingSystem.ResetAllBindings(bridge); } break;
        }
        ScriptableObject.Destroy(d);
        Apply(); Save();
    }

    // Cong cu Inspector (giu tu ban lead): day MouseSensitivity xuong local NPlayer qua RPC.
    // Runtime nen dung event OnSensitivityChanged; nut nay chi de test tay trong Editor.
    [Button]
    private void UpdateMouseSensitivity()
    {
        if (_settingData == null) return;
        NPlayer[] players = FindObjectsByType<NPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsLocalPlayer)
            {
                player._movementHandler.RPC_SetCameraSpeed(_settingData.MouseSensitivity);
                return;
            }
        }
    }
}
