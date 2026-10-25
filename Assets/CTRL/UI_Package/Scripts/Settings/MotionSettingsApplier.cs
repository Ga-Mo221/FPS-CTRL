
using GaMo.TrueInputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cau noi SettingData (gyro) -> TISInputBridge (SRP: chi lo gyro, giong DisplaySettingsApplier lo man hinh).
///
/// Cach gyro di vao game: profile "Character CTRL" bat Motion Settings › Enable Gyro + action Look tick "+ Gyro"
/// -> TIS cong toc do gyro (do/giay) vao LookDelta cung right stick. Game khong can sua CharacterInputHandler.
///
///  - Che do: Tat -> bridge.GyroEnabled = false. Khi ngam -> Activation = WhileActionHeld("ADS").
///            Luon bat -> Activation = Always.
///  - Do nhay: he so x "the gioi thuc". Goc cua profile (gyroSensitivity = 1 / PlayerData.YawModel = 0.001)
///            = xoay tay cam bao nhieu do thi camera xoay bay nhieu do (o Do nhay tay cam 1.0).
///            TIS nuong do nhay vao tham so composite luc build -> doi luc chay bang ApplyParameterOverride.
///  - Dao truc: TISMotionSettings.invertGyroX/Y (TIS doc moi frame).
///  - Profile duoc build lai (doi phim, swap) -> ban sao runtime moi -> Tick() phat hien va ap lai.
/// </summary>
public static class MotionSettingsApplier
{
    public static readonly string[] ModeOptions = { "Tắt", "Khi ngắm", "Luôn bật" };

    private const string LookAction = "Look";
    private const string AdsAction  = "ADS";
    private const string CompositeName = "TISStickMotion";

    private static TISInputBridge    _bridge;
    private static TISMotionSettings _appliedTo;   // ban sao runtime cua profile da ap
    private static bool  _calibrating;
    private static float _retryAt;            // chua tim thay action Look (map dang tat) -> thu lai sau

    // ------------------------------------------------------------------ public
    public static void Apply(SettingData d)
    {
        if (d == null) return;
        ApplyNow(d);
    }

    /// <summary>Goi moi frame (SettingManager.Update): ap lai khi bridge / profile runtime doi.</summary>
    public static void Tick(SettingData d)
    {
        if (d == null) return;
        var b = TISInputBridge.Instance;
        if (b == null) return;
        var ms = b.ActiveProfile != null ? b.ActiveProfile.motionSettings : null;
        if (b == _bridge && ReferenceEquals(ms, _appliedTo)) return;
        if (Time.unscaledTime < _retryAt && b == _bridge) return;
        ApplyNow(d);
    }

    /// <summary>Tay cam dang noi co cam bien gyro khong (DualShock 4 / DualSense / Switch Pro qua SDL3).</summary>
    public static bool HasGyroController
    {
        get
        {
            var b = TISInputBridge.Instance;
            if (b == null || b.Motion == null) return false;
            var src = b.Motion.Source;
            return src != null && src.IsConnected && src.HasMotion;
        }
    }

    public static bool IsCalibrating { get { return _calibrating; } }

    /// <summary>Bat dau hieu chinh: nguoi choi dat tay cam yen tren ban. Tra false neu khong co tay cam gyro.</summary>
    public static bool BeginCalibration()
    {
        var b = TISInputBridge.Instance;
        if (b == null || !HasGyroController) return false;
        _calibrating = true;
        b.GyroEnabled = true;               // can gyro chay de lay mau (ke ca khi nguoi choi dang de Tat)
        b.Motion.BeginGyroCalibration();
        return true;
    }

    /// <summary>Ket thuc hieu chinh, ap offset trung binh. Tra false neu khong lay duoc mau nao.</summary>
    public static bool EndCalibration(SettingData d)
    {
        var b = TISInputBridge.Instance;
        bool ok = false;
        if (b != null && b.Motion != null) ok = b.Motion.EndGyroCalibration();
        _calibrating = false;
        _appliedTo = null;                  // tra GyroEnabled ve theo cai dat
        if (d != null) ApplyNow(d);
        return ok;
    }

    // ------------------------------------------------------------------ apply
    private static void ApplyNow(SettingData d)
    {
        var b = TISInputBridge.Instance;
        _bridge = b;
        if (b == null || b.ActiveProfile == null) { _appliedTo = null; return; }
        var ms = b.ActiveProfile.motionSettings;
        if (ms == null) { _appliedTo = null; return; }

        int mode = Mathf.Clamp(d.GyroMode, 0, 2);
        b.GyroEnabled = _calibrating || mode != 0;
        ms.gyroActivation = mode == 1 ? TISGyroActivation.WhileActionHeld : TISGyroActivation.Always;
        ms.gyroActivationAction = AdsAction;
        ms.invertGyroX = d.GyroInvertX;
        ms.invertGyroY = d.GyroInvertY;

        if (ApplySensitivity(b, d)) _appliedTo = ms;
        else { _appliedTo = null; _retryAt = Time.unscaledTime + 0.5f; }
    }

    // Do nhay = goc profile (asset goc, khong phai ban runtime da bi doi) x he so nguoi choi.
    // TIS chia cho gamepadSensitivity vi LookDelta cua tay cam nhan lai he so do (TISProfileBuilder.MotionScale).
    private static bool ApplySensitivity(TISInputBridge b, SettingData d)
    {
        var look = FindLookAction();
        if (look == null) return false;

        Vector2 baseSens = new Vector2(0.001f, 0.001f);
        var orig = b.OriginalProfile;
        if (orig != null && orig.motionSettings != null) baseSens = orig.motionSettings.gyroSensitivity;
        Vector2 pad = b.ActiveProfile.cameraSettings != null ? b.ActiveProfile.cameraSettings.gamepadSensitivity : new Vector2(0.25f, 0.25f);

        float sx = Mathf.Approximately(pad.x, 0f) ? 0f : baseSens.x * d.GyroSensX / pad.x;
        float sy = Mathf.Approximately(pad.y, 0f) ? 0f : baseSens.y * d.GyroSensY / pad.y;
        look.ApplyParameterOverride("gyroScaleX", sx);
        look.ApplyParameterOverride("gyroScaleY", sy);
        return true;
    }

    // Action "Look" cua profile TIS (co binding composite "+ Gyro"). Asset cua TIS khong public -> tim trong cac action dang bat.
    private static InputAction FindLookAction()
    {
        foreach (var a in InputSystem.ListEnabledActions())
        {
            if (a == null || a.name != LookAction) continue;
            foreach (var bind in a.bindings)
                if (bind.isComposite && bind.path != null && bind.path.StartsWith(CompositeName)) return a;
        }
        return null;
    }
}
