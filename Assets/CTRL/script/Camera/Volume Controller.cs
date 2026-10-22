using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Đảm bảo dự án dùng URP. Nếu dùng HDRP thì đổi namespace tương ứng.

[DefaultExecutionOrder(100)]
public class VolumeController : MonoBehaviour
{
    [Header("Volume Reference")]
    [SerializeField] private Volume volume;

    [Header("Bloom Peak Values")]
    [SerializeField] private float peakBloomIntensity = 15.0f; // Mờ mắt / Chói trắng ban đầu
    [SerializeField] private float defaultBloomIntensity = 0.5f;

    [Header("Chromatic Aberration Peak Values")]
    [SerializeField] private float peakChromatic = 1.0f;

    [Header("Lens Distortion Peak Values")]
    [SerializeField] private float peakDistortion = -0.5f;

    [Header("Depth of Field Peak Values")]
    [SerializeField] private float peakFocusDistance = 0.1f; // Nhìn mờ tịt ở cự ly gần
    [SerializeField] private float defaultFocusDistance = 10f;
    [SerializeField] private int peakFocalLength = 100;
    [SerializeField] private int defaultFocalLength = 59;

    [Header("ADS (Aim Down Sight) Values")]
    [SerializeField] private float adsFocusDistance = 0.1f;   // Focus Distance khi ADS
    [SerializeField] private int adsFocalLength = 27;          // Focal Length khi ADS

    // References tới từng Override trong Profile
    private Bloom bloom;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private DepthOfField depthOfField;

    private Coroutine flashbangCoroutine;
    private Coroutine adsCoroutine;

    // Trạng thái hiện tại
    private bool isAiming = false;
    private bool isFlashbanging = false;

    // Trạng thái bật/tắt ban đầu của các Override — chỉ dùng khi không có SettingManager để trả lại.
    private bool bloomWasActive, chromaticWasActive, distortionWasActive, dofWasActive;

    private void Awake()
    {
        if (volume == null)
            volume = GetComponent<Volume>();

        // Lấy các Override từ Profile
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out bloom);
            volume.profile.TryGet(out chromaticAberration);
            volume.profile.TryGet(out lensDistortion);
            volume.profile.TryGet(out depthOfField);
        }

        bloomWasActive      = bloom != null && bloom.active;
        chromaticWasActive  = chromaticAberration != null && chromaticAberration.active;
        distortionWasActive = lensDistortion != null && lensDistortion.active;
        dofWasActive        = depthOfField != null && depthOfField.active;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.VolumeController = this;

        // Đưa DoF / Chromatic / Distortion về giá trị nghỉ ngay từ đầu (trước đây chỉ reset sau lần ADS/flashbang đầu tiên).
        SetOverridesState(true);
        ResetToDefault();
        RestoreSettingState();
    }

    // Setting đồ họa (Thấp/Vừa) tắt DoF, Chromatic, Lens Distortion; người chơi có thể tắt Bloom.
    // Trong lúc hiệu ứng đang chạy, giữ bật các Override cần thiết (kể cả khi SettingManager vừa áp lại setting).
    // Chạy ở LateUpdate -> sau mọi Update/UI apply trong frame, nên không bị nháy.
    private void LateUpdate()
    {
        if (isFlashbanging)
        {
            ForceActive(bloom);
            ForceActive(chromaticAberration);
            ForceActive(lensDistortion);
            ForceActive(depthOfField);
        }
        else if (isAiming || adsCoroutine != null)
        {
            ForceActive(depthOfField);
        }
    }

    // ─────────────────────────────────────────────
    //  FLASHBANG
    // ─────────────────────────────────────────────

    /// <summary>
    /// Gọi hàm này khi người chơi bị trúng boom choáng.
    /// Flashbang luôn được ưu tiên. Sau khi hết, nếu vẫn đang ADS thì tự chuyển sang ADS.
    /// </summary>
    public void TriggerFlashbang(float duration)
    {
        // Dừng ADS đang chạy (nếu có) để nhường cho Flashbang
        if (adsCoroutine != null)
        {
            StopCoroutine(adsCoroutine);
            adsCoroutine = null;
        }

        if (flashbangCoroutine != null)
            StopCoroutine(flashbangCoroutine);

        flashbangCoroutine = StartCoroutine(FlashbangRoutine(duration));
    }

    private IEnumerator FlashbangRoutine(float duration)
    {
        isFlashbanging = true;
        float timer = 0f;

        SetOverridesState(true);
        float baseBloom = RestBloomIntensity();

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            // SmoothStep: hiệu ứng giảm dần mượt mà
            float t = Mathf.SmoothStep(1f, 0f, progress);

            if (bloom != null)
                bloom.intensity.value = Mathf.Lerp(baseBloom, peakBloomIntensity, t);

            if (chromaticAberration != null)
                chromaticAberration.intensity.value = Mathf.Lerp(0f, peakChromatic, t);

            if (lensDistortion != null)
                lensDistortion.intensity.value = Mathf.Lerp(0f, peakDistortion, t);

            if (depthOfField != null)
            {
                depthOfField.focusDistance.value = Mathf.Lerp(defaultFocusDistance, peakFocusDistance, t);
                depthOfField.focalLength.value   = Mathf.Lerp(defaultFocalLength, peakFocalLength, t);
            }

            yield return null;
        }

        isFlashbanging = false;
        flashbangCoroutine = null;

        // Sau flashbang: nếu người chơi vẫn đang ADS thì blend sang ADS,
        // ngược lại reset về mặc định.
        if (isAiming)
        {
            // Khởi lại ADS blend từ giá trị hiện tại (không snap cứng)
            if (bloom != null) bloom.intensity.value = baseBloom;
            if (chromaticAberration != null) chromaticAberration.intensity.value = 0f;
            if (lensDistortion != null) lensDistortion.intensity.value = 0f;
            RestoreSettingState(); // DoF vẫn được LateUpdate giữ bật vì đang ADS
            adsCoroutine = StartCoroutine(ADSRoutine(true, 0.15f));
        }
        else
        {
            ResetToDefault();
            RestoreSettingState();
        }
    }

    // ─────────────────────────────────────────────
    //  ADS  (Aim Down Sight)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Gọi khi người chơi mở / đóng ống ngắm.
    /// <param name="ads">true = đang ngắm, false = thôi ngắm</param>
    /// <param name="duration">Thời gian blend (giây)</param>
    /// </summary>
    public void TriggerADS(bool ads, float duration)
    {
        isAiming = ads;

        // Nếu Flashbang đang chạy, chỉ lưu trạng thái ADS lại,
        // không làm gì cả — Flashbang sẽ tự chuyển sang ADS khi kết thúc.
        if (isFlashbanging) return;

        if (adsCoroutine != null)
            StopCoroutine(adsCoroutine);

        adsCoroutine = StartCoroutine(ADSRoutine(ads, duration));
    }

    private IEnumerator ADSRoutine(bool ads, float duration)
    {
        SetOverridesState(true);

        // Lấy giá trị hiện tại làm điểm xuất phát để blend mượt
        float startFocusDistance = depthOfField != null ? depthOfField.focusDistance.value : defaultFocusDistance;
        float startFocalLength   = depthOfField != null ? depthOfField.focalLength.value   : defaultFocalLength;

        float targetFocusDistance = ads ? adsFocusDistance   : defaultFocusDistance;
        float targetFocalLength   = ads ? adsFocalLength      : defaultFocalLength;

        float timer = 0f;

        while (timer < duration)
        {
            // Nếu Flashbang bị kích hoạt giữa chừng, dừng ngay
            if (isFlashbanging) yield break;

            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));

            if (depthOfField != null)
            {
                depthOfField.focusDistance.value = Mathf.Lerp(startFocusDistance, targetFocusDistance, t);
                depthOfField.focalLength.value   = Mathf.Lerp(startFocalLength,   targetFocalLength,   t);
            }

            yield return null;
        }

        // Snap về đúng giá trị đích khi xong
        if (depthOfField != null)
        {
            depthOfField.focusDistance.value = targetFocusDistance;
            depthOfField.focalLength.value   = targetFocalLength;
        }

        adsCoroutine = null;

        // Thôi ngắm -> trả DoF về đúng setting (tắt nếu bậc đồ họa đang tắt DoF, giữ nguyên nếu đang bật).
        if (!ads) RestoreSettingState();
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private void SetOverridesState(bool state)
    {
        if (bloom != null) bloom.intensity.overrideState = state;
        if (chromaticAberration != null) chromaticAberration.intensity.overrideState = state;
        if (lensDistortion != null) lensDistortion.intensity.overrideState = state;
        if (depthOfField != null)
        {
            depthOfField.focusDistance.overrideState = state;
            depthOfField.focalLength.overrideState   = state;
        }
    }

    private void ResetToDefault()
    {
        if (bloom != null) bloom.intensity.value = RestBloomIntensity();
        if (chromaticAberration != null) chromaticAberration.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;
        if (depthOfField != null)
        {
            depthOfField.focusDistance.value = defaultFocusDistance;
            depthOfField.focalLength.value   = defaultFocalLength;
        }
    }

    private static void ForceActive(VolumeComponent c)
    {
        if (c != null && !c.active) c.active = true;
    }

    /// <summary>Trả bật/tắt các Override về đúng setting đồ họa (tắt nếu setting tắt, giữ nguyên nếu setting bật).</summary>
    private void RestoreSettingState()
    {
        var sm = SettingManager.Instance;
        if (sm != null && sm.SettingData != null && volume != null)
        {
            if (sm.PostVolume != volume) sm.SetPostVolume(volume); // dam bao SettingManager dang quan ly dung Volume nay
            else sm.ApplyPostVolume();
            return;
        }
        if (bloom != null) bloom.active = bloomWasActive;
        if (chromaticAberration != null) chromaticAberration.active = chromaticWasActive;
        if (lensDistortion != null) lensDistortion.active = distortionWasActive;
        if (depthOfField != null) depthOfField.active = dofWasActive;
    }

    /// <summary>Cường độ Bloom lúc nghỉ = giá trị người chơi chọn trong setting (0 nếu tắt Bloom).</summary>
    private float RestBloomIntensity()
    {
        var data = SettingManager.Instance != null ? SettingManager.Instance.SettingData : null;
        if (data == null) return defaultBloomIntensity;
        return data.BloomOn ? data.BloomIntensity : 0f;
    }
}