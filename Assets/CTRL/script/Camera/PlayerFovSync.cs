// [Aemy 2026-10-19] FILE MOI. Gan len Main Camera cua prefab nhan vat.
using UnityEngine;

/// <summary>
/// Dat FOV camera nguoi choi theo Cai dat luc vao tran va moi khi doi Cai dat (slider FOV, doi do phan giai...).
/// Chi doi khi camera DANG o FOV "dung yen" (khong dang ngam) - luc ngam WeaponController tu blend FOV ADS
/// (tinh qua GameFov.ForWeapon) nen khong bi giat.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerFovSync : MonoBehaviour
{
    private Camera _cam;
    private float _lastStand = -1f;
    private float _lastAspect = -1f;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _lastStand = _cam.fieldOfView; // FOV dung yen hien tai (prefab = 60)
    }

    private void OnEnable()
    {
        if (SettingManager.Instance != null) SettingManager.Instance.OnSettingsApplied += OnSettings;
        Sync(true);
    }

    private void OnDisable()
    {
        if (SettingManager.Instance != null) SettingManager.Instance.OnSettingsApplied -= OnSettings;
    }

    private void OnSettings(SettingData d) { Sync(false); }

    // Ti le khung hinh doi (doi do phan giai / cua so) -> tinh lai FOV doc.
    private void LateUpdate()
    {
        if (_cam != null && Mathf.Abs(_cam.aspect - _lastAspect) > 0.001f) Sync(false);
    }

    private void Sync(bool force)
    {
        if (_cam == null) return;
        _lastAspect = _cam.aspect;
        float stand = GameFov.StandVertical(_cam);
        bool atStand = Mathf.Abs(_cam.fieldOfView - _lastStand) < 0.5f;
        if (force || atStand) _cam.fieldOfView = stand;
        _lastStand = stand;
    }
}
