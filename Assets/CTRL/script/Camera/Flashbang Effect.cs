using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Đảm bảo dự án dùng URP. Nếu dùng HDRP thì đổi namespace tương ứng.

[DefaultExecutionOrder(100)]
public class FlashbangEffect : MonoBehaviour
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

    // References tới từng Override trong Profile
    private Bloom bloom;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private DepthOfField depthOfField;

    private Coroutine flashbangCoroutine;

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
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.FlashbangEffect = this;
    }

    /// <summary>
    /// Gọi hàm này khi người chơi bị trúng boom choáng
    /// </summary>
    [Button]
    public void TriggerFlashbang(float duration)
    {
        if (flashbangCoroutine != null)
        {
            StopCoroutine(flashbangCoroutine);
        }
        flashbangCoroutine = StartCoroutine(FlashbangRoutine(duration));
    }

    private IEnumerator FlashbangRoutine(float duration)
    {
        float timer = 0f;

        // Bật đè các thông số (Override) phòng trường hợp chưa bật
        SetOverridesState(true);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            // Sử dụng SmoothStep để hiệu ứng giảm dần mượt mà hơn (Lerp phi tuyến tính)
            float t = Mathf.SmoothStep(1f, 0f, progress);

            // 1. Giảm dần Bloom Intensity
            if (bloom != null)
                bloom.intensity.value = Mathf.Lerp(defaultBloomIntensity, peakBloomIntensity, t);

            // 2. Giảm dần Chromatic Aberration
            if (chromaticAberration != null)
                chromaticAberration.intensity.value = Mathf.Lerp(0f, peakChromatic, t);

            // 3. Giảm dần Lens Distortion
            if (lensDistortion != null)
                lensDistortion.intensity.value = Mathf.Lerp(0f, peakDistortion, t);

            // 4. Phục hồi Depth of Field (Focus Distance xa dần trở lại)
            if (depthOfField != null)
            {
                depthOfField.focusDistance.value = Mathf.Lerp(defaultFocusDistance, peakFocusDistance, t);
                depthOfField.focalLength.value = Mathf.Lerp(defaultFocalLength, peakFocalLength, t);
            }

            yield return null;
        }

        // Đảm bảo đưa các chỉ số về chuẩn sau khi kết thúc
        ResetToDefault();
    }

    private void SetOverridesState(bool state)
    {
        if (bloom != null) bloom.intensity.overrideState = state;
        if (chromaticAberration != null) chromaticAberration.intensity.overrideState = state;
        if (lensDistortion != null) lensDistortion.intensity.overrideState = state;
        if (depthOfField != null)
        {
            depthOfField.focusDistance.overrideState = state;
            depthOfField.focalLength.overrideState = state;
        }
    }

    private void ResetToDefault()
    {
        if (bloom != null) bloom.intensity.value = defaultBloomIntensity;
        if (chromaticAberration != null) chromaticAberration.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;
        if (depthOfField != null)
        {
            depthOfField.focusDistance.value = defaultFocusDistance;
            depthOfField.focalLength.value = defaultFocalLength;
        }
    }
}