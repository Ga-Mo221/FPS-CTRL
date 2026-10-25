
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ap cac thiet lap hien thi xuong engine (SRP: chi lo man hinh / FPS, giong GraphicsQualityApplier lo chat luong).
///  - Che do man hinh: Toan man hinh (Exclusive) / Khong vien (FullScreenWindow) / Cua so (Windowed).
///  - Do phan giai + tan so quet: lay tu Screen.resolutions cua man hinh dang dung; 0 = mac dinh (do phan giai goc, Hz cao nhat).
///  - Gioi han FPS: Application.targetFrameRate (CHI co tac dung khi V-Sync tat - V-Sync bat thi khoa theo Hz man hinh).
/// Chi goi Screen.SetResolution khi gia tri THAY DOI (Apply chay moi lan keo slider -> tranh nhay man hinh).
/// Trong Editor, Screen.SetResolution / fullScreenMode khong co tac dung (chi co o ban build).
/// </summary>
public static class DisplaySettingsApplier
{
    public static readonly string[] ModeOptions = { "Toàn màn hình", "Không viền", "Cửa sổ" };
    private static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    /// <summary>Muc gioi han FPS (0 = khong gioi han).</summary>
    public static readonly int[] FpsValues = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };

    private static List<Vector2Int> _resolutions;
    private static List<int> _rates;
    private static string _applied; // chu ky lan ap cuoi (mode|w|h|hz)

    // ------------------------------------------------------------------ danh sach lua chon
    /// <summary>Cac do phan giai man hinh ho tro (khong trung, lon -> nho). Phan tu 0 = lon nhat.</summary>
    public static List<Vector2Int> Resolutions()
    {
        if (_resolutions != null) return _resolutions;
        _resolutions = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (v.x >= 800 && v.y >= 600 && !_resolutions.Contains(v)) _resolutions.Add(v);
        }
        if (_resolutions.Count == 0) _resolutions.Add(Native());
        _resolutions.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
        return _resolutions;
    }

    /// <summary>Cac tan so quet (Hz, lam tron, cao -> thap). Phan tu 0 = cao nhat.</summary>
    public static List<int> RefreshRates()
    {
        if (_rates != null) return _rates;
        _rates = new List<int>();
        foreach (var r in Screen.resolutions)
        {
            int hz = Mathf.RoundToInt((float)r.refreshRateRatio.value);
            if (hz > 0 && !_rates.Contains(hz)) _rates.Add(hz);
        }
        if (_rates.Count == 0) _rates.Add(Mathf.Max(60, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)));
        _rates.Sort((a, b) => b.CompareTo(a));
        return _rates;
    }

    public static string[] ResolutionOptions()
    {
        var list = Resolutions(); var o = new string[list.Count];
        for (int i = 0; i < list.Count; i++) o[i] = ResolutionLabel(list[i].x, list[i].y);
        return o;
    }

    public static string[] RefreshOptions()
    {
        var list = RefreshRates(); var o = new string[list.Count];
        for (int i = 0; i < list.Count; i++) o[i] = list[i] + " Hz";
        return o;
    }

    public static string[] FpsOptions()
    {
        var o = new string[FpsValues.Length];
        for (int i = 0; i < FpsValues.Length; i++) o[i] = FpsValues[i] <= 0 ? "Không giới hạn" : FpsValues[i] + " FPS";
        return o;
    }

    /// <summary>"2560 × 1440 · 16:9".</summary>
    public static string ResolutionLabel(int w, int h)
    {
        return w + " × " + h + " · " + Aspect(w, h);
    }

    private static string Aspect(int w, int h)
    {
        float r = (float)w / Mathf.Max(1, h);
        if (Mathf.Abs(r - 16f / 9f) < 0.02f) return "16:9";
        if (Mathf.Abs(r - 16f / 10f) < 0.02f) return "16:10";
        if (Mathf.Abs(r - 4f / 3f) < 0.02f) return "4:3";
        if (Mathf.Abs(r - 5f / 4f) < 0.02f) return "5:4";
        if (r > 2.3f && r < 2.45f) return "21:9";
        if (r > 3.4f && r < 3.6f) return "32:9";
        int g = Gcd(w, h);
        return (w / g) + ":" + (h / g);
    }
    private static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Max(1, a); }

    private static Vector2Int Native()
    {
        var c = Screen.currentResolution;
        return new Vector2Int(c.width, c.height);
    }

    // ------------------------------------------------------------------ index <-> SettingData
    public static int ResolutionIndex(SettingData d)
    {
        var list = Resolutions();
        if (d.ResolutionWidth > 0) { int i = list.IndexOf(new Vector2Int(d.ResolutionWidth, d.ResolutionHeight)); if (i >= 0) return i; }
        int n = list.IndexOf(Native()); return n >= 0 ? n : 0;
    }
    public static void SetResolutionIndex(SettingData d, int i)
    {
        var list = Resolutions(); i = Mathf.Clamp(i, 0, list.Count - 1);
        d.ResolutionWidth = list[i].x; d.ResolutionHeight = list[i].y;
    }

    public static int RefreshIndex(SettingData d)
    {
        var list = RefreshRates();
        if (d.RefreshRate > 0) { int i = list.IndexOf(d.RefreshRate); if (i >= 0) return i; }
        return 0;
    }
    public static void SetRefreshIndex(SettingData d, int i)
    {
        var list = RefreshRates(); d.RefreshRate = list[Mathf.Clamp(i, 0, list.Count - 1)];
    }

    public static int FpsIndex(SettingData d)
    {
        int i = System.Array.IndexOf(FpsValues, d.FpsLimit); return i >= 0 ? i : 0;
    }
    public static void SetFpsIndex(SettingData d, int i) { d.FpsLimit = FpsValues[Mathf.Clamp(i, 0, FpsValues.Length - 1)]; }

    // ------------------------------------------------------------------ ap xuong engine
    public static void Apply(SettingData d)
    {
        if (d == null) return;

        // Gioi han FPS (V-Sync bat thi Unity bo qua targetFrameRate).
        Application.targetFrameRate = (!d.VSync && d.FpsLimit > 0) ? d.FpsLimit : -1;

        var mode = Modes[Mathf.Clamp(d.DisplayMode, 0, Modes.Length - 1)];
        var res = d.ResolutionWidth > 0 ? new Vector2Int(d.ResolutionWidth, d.ResolutionHeight) : Native();
        if (!Resolutions().Contains(res)) res = Native(); // doi man hinh -> do phan giai cu khong con -> ve goc
        int hz = d.RefreshRate > 0 ? d.RefreshRate : RefreshRates()[0];
        var rate = BestRate(res, hz);

        string sig = mode + "|" + res.x + "|" + res.y + "|" + rate.value.ToString("F2");
        if (sig == _applied) return;
        _applied = sig;
        if (Application.isEditor) return; // Editor: Game view tu quan ly kich thuoc
        Screen.SetResolution(res.x, res.y, mode, rate);
    }

    // Tan so ho tro gan nhat (<= hz neu co) cho do phan giai da chon.
    private static RefreshRate BestRate(Vector2Int res, int hz)
    {
        RefreshRate best = Screen.currentResolution.refreshRateRatio; bool found = false; double bestDiff = double.MaxValue;
        foreach (var r in Screen.resolutions)
        {
            if (r.width != res.x || r.height != res.y) continue;
            double diff = System.Math.Abs(r.refreshRateRatio.value - hz);
            if (diff < bestDiff) { bestDiff = diff; best = r.refreshRateRatio; found = true; }
        }
        return found ? best : Screen.currentResolution.refreshRateRatio;
    }
}
