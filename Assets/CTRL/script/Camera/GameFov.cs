// [Aemy 2026-10-19] FILE MOI. Truong nhin (FOV) trong Cai dat ap len camera nguoi choi.
using UnityEngine;

/// <summary>
/// Quy uoc: so tren slider Cai dat (60..120, mac dinh 90) la FOV NGANG nhu cac game FPS pho bien
/// (CS/Valorant...). Unity Camera.fieldOfView la FOV DOC -> doi theo ti le khung hinh.
/// 90 ngang o 16:9 ~ 58.7 doc ~ dung bang camera mac dinh cu (60) -> mac dinh gan nhu khong doi cam giac.
/// Ngam ADS: giu dung TI LE phong to cua tung sung (WeaponData.ADS_Zoom / Stand_Zoom) tren nen FOV moi.
/// </summary>
public static class GameFov
{
    public const float DefaultHorizontal = 90f;

    /// <summary>FOV ngang dang chon trong Cai dat (khong co SettingManager -> 90).</summary>
    public static float Horizontal
    {
        get
        {
            var m = SettingManager.Instance;
            float f = (m != null && m.SettingData != null) ? m.SettingData.Fov : DefaultHorizontal;
            return Mathf.Clamp(f, 50f, 130f);
        }
    }

    /// <summary>FOV doc khi KHONG ngam (dung cho Camera.fieldOfView).</summary>
    public static float StandVertical(float aspect)
    {
        if (aspect <= 0.01f) aspect = 16f / 9f;
        return Camera.HorizontalToVerticalFieldOfView(Horizontal, aspect);
    }

    public static float StandVertical(Camera cam) { return StandVertical(cam != null ? cam.aspect : 16f / 9f); }

    /// <summary>FOV doc mong muon cho 1 sung: dung yen = FOV Cai dat; ngam = giu ti le zoom cua sung.</summary>
    public static float ForWeapon(Camera cam, float weaponStandZoom, float weaponAdsZoom, bool ads)
    {
        float stand = StandVertical(cam);
        if (!ads) return stand;
        if (weaponStandZoom <= 0.01f) return weaponAdsZoom;
        return Mathf.Clamp(weaponAdsZoom * (stand / weaponStandZoom), 1f, 179f);
    }
}
