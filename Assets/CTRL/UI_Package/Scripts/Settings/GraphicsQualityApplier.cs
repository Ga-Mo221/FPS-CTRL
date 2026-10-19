
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// - Bac chat luong (0 THAP .. 3 TOI DA): render scale, khoang cach + do phan giai + so tang bong, bong mem,
///   LOD bias, loc anisotropic, bat/tat SSAO va suong mu the tich (renderer feature), hieu ung hau ky nang (Volume).
/// - Ket cau (0 Thap / 1 Vua / 2 Cao): QualitySettings.globalTextureMipmapLimit = 2 / 1 / 0 (1/4, 1/2, day du do phan giai).
/// - Khu rang cua: Tat / FXAA / SMAA / MSAA 4x / TAA (camera URP + so mau MSAA tren pipeline asset).
/// Chay tren BAN SAO cua URP asset (khong ghi de file asset trong Editor); thoat game/Play mode -> tra lai mac dinh.
/// </summary>
public static class GraphicsQualityApplier
{
    public static readonly string[] TextureOptions = { "Thấp", "Vừa", "Cao" };
    public static readonly string[] AAOptions = { "Tắt", "FXAA", "SMAA", "MSAA 4x", "TAA" };

    private static readonly float[] RenderScale = { 0.75f, 0.9f, 1f, 1f };
    private static readonly float[] ShadowDistance = { 40f, 80f, 150f, 200f };
    private static readonly int[] ShadowCascades = { 1, 2, 2, 4 };
    private static readonly int[] ShadowResolution = { 1024, 2048, 2048, 4096 };
    private static readonly bool[] SoftShadows = { false, true, true, true };
    private static readonly float[] LodBias = { 0.7f, 1f, 1.5f, 2f };
    private static readonly AnisotropicFiltering[] Aniso = { AnisotropicFiltering.Disable, AnisotropicFiltering.Enable, AnisotropicFiltering.ForceEnable, AnisotropicFiltering.ForceEnable };
    private static readonly bool[] Ssao = { false, false, true, true };
    private static readonly bool[] VolumetricFog = { false, true, true, true };

    private static UniversalRenderPipelineAsset _base, _runtime;
    private static readonly Dictionary<ScriptableRendererFeature, bool> _featureDefaults = new Dictionary<ScriptableRendererFeature, bool>();
    private static int _defaultMipLimit = -1; private static float _defaultLodBias; private static AnisotropicFiltering _defaultAniso;
    private static bool _hooked;
    private static int _aa = 2;
    private static int _tier = 2;

    public static int Tier { get { return _tier; } }

    public static void Apply(SettingData d)
    {
        if (d == null) return;
        Hook();
        _tier = Mathf.Clamp(d.QualityLevel, 0, 3);
        _aa = Mathf.Clamp(d.AntiAliasing, 0, AAOptions.Length - 1);
        var a = Asset();
        if (a != null)
        {
            a.renderScale = RenderScale[_tier];
            a.shadowDistance = ShadowDistance[_tier];
            a.shadowCascadeCount = ShadowCascades[_tier];
            a.mainLightShadowmapResolution = ShadowResolution[_tier];
            SetField(a, "m_SoftShadowsSupported", SoftShadows[_tier]);
            a.msaaSampleCount = _aa == 3 ? 4 : 1;
            SetFeatures(a, _tier);
        }
        QualitySettings.lodBias = LodBias[_tier];
        QualitySettings.anisotropicFiltering = Aniso[_tier];
        QualitySettings.globalTextureMipmapLimit = 2 - Mathf.Clamp(d.TextureQuality, 0, 2);
        ApplyAllCameras();
    }

    /// <summary>Ap che do khu rang cua cho 1 camera (camera nguoi choi sinh sau khi vao tran).</summary>
    public static void ApplyCamera(Camera cam)
    {
        if (cam == null) return;
        var add = cam.GetUniversalAdditionalCameraData();
        if (add == null || add.renderType != CameraRenderType.Base) return;
        switch (_aa)
        {
            case 1: add.antialiasing = AntialiasingMode.FastApproximateAntialiasing; break;
            case 2: add.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; add.antialiasingQuality = AntialiasingQuality.High; break;
            case 4: add.antialiasing = AntialiasingMode.TemporalAntiAliasing; break;
            default: add.antialiasing = AntialiasingMode.None; break;
        }
        cam.allowMSAA = _aa == 3;
    }

    public static void ApplyAllCameras()
    {
        foreach (var c in Camera.allCameras) if (c != null && c.targetTexture == null) ApplyCamera(c);
    }

    /// <summary>Hieu ung hau ky "nang" tat o bac thap (giu nguyen lua chon Bloom/Vignette/Motion Blur cua nguoi choi).</summary>
    public static void ApplyVolumeTier(VolumeProfile profile, Dictionary<VolumeComponent, bool> defaults)
    {
        if (profile == null) return;
        foreach (var c in profile.components)
        {
            if (c == null) continue;
            if (!defaults.ContainsKey(c)) defaults[c] = c.active;
            bool heavy = c is DepthOfField || c is ChromaticAberration || c is FilmGrain || c is LensDistortion || c is ScreenSpaceLensFlare;
            bool medium = c is DepthOfField || c is FilmGrain || c is ScreenSpaceLensFlare;
            if (_tier == 0 && heavy) c.active = false;
            else if (_tier == 1 && medium) c.active = false;
            else if (heavy) c.active = defaults[c];
        }
    }

    // ---------------------------------------------------------------
    private static UniversalRenderPipelineAsset Asset()
    {
        if (_runtime != null) return _runtime;
        var cur = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (cur == null) cur = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (cur == null) return null;
        _base = cur;
        _runtime = Object.Instantiate(cur);
        _runtime.name = cur.name + " (Runtime)";
        QualitySettings.renderPipeline = _runtime;
        return _runtime;
    }

    private static void SetFeatures(UniversalRenderPipelineAsset a, int tier)
    {
        var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = f != null ? f.GetValue(a) as ScriptableRendererData[] : null;
        if (list == null) return;
        foreach (var rd in list)
        {
            if (rd == null) continue;
            foreach (var feat in rd.rendererFeatures)
            {
                if (feat == null) continue;
                string n = feat.GetType().Name;
                bool isSsao = n.Contains("AmbientOcclusion");
                bool isFog = n.Contains("VolumetricFog");
                if (!isSsao && !isFog) continue;
                if (!_featureDefaults.ContainsKey(feat)) _featureDefaults[feat] = feat.isActive;
                bool def = _featureDefaults[feat];
                feat.SetActive(def && (isSsao ? Ssao[tier] : VolumetricFog[tier]));
            }
        }
    }

    private static void SetField(object o, string name, object v)
    {
        var f = o.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(o, v);
    }

    // Thoat game / thoat Play mode: tra moi thu ve mac dinh (Editor khong bi luu thay doi vao asset / ProjectSettings).
    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        _defaultMipLimit = QualitySettings.globalTextureMipmapLimit;
        _defaultLodBias = QualitySettings.lodBias;
        _defaultAniso = QualitySettings.anisotropicFiltering;
        Application.quitting += Restore;
    }

    private static void Restore()
    {
        foreach (var kv in _featureDefaults) if (kv.Key != null) kv.Key.SetActive(kv.Value);
        _featureDefaults.Clear();
        if (_base != null) QualitySettings.renderPipeline = _base;
        if (_runtime != null) Object.Destroy(_runtime);
        _runtime = null; _base = null;
        if (_defaultMipLimit >= 0) QualitySettings.globalTextureMipmapLimit = _defaultMipLimit;
        QualitySettings.lodBias = _defaultLodBias;
        QualitySettings.anisotropicFiltering = _defaultAniso;
        Application.quitting -= Restore;
        _hooked = false;
    }
}
