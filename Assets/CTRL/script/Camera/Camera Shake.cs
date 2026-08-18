using System.Collections;
using Sirenix.OdinInspector;
using TrueInputSystem;
using UnityEngine;

/// <summary>
/// CameraShake - System rung camera mượt mà dựa trên thuật toán Trauma và Perlin Noise.
/// Gắn script này trực tiếp vào Main Camera hoặc Camera Holder/Pivot.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Trauma Settings")]
    [Tooltip("Mức độ Trauma hiện tại (từ 0 đến 1). Tự động giảm dần theo thời gian.")]
    [Range(0f, 1f)]
    public float trauma = 0f;

    [Tooltip("Tốc độ suy giảm Trauma mỗi giây.")]
    [SerializeField] private float traumaDecay = 1.2f;

    [Tooltip("Hệ số mũ tính độ rung (Intensity = Trauma ^ Exponent). Giúp cú rung nặng có lực hơn và nhẹ thì dịu hơn.")]
    [SerializeField] private float traumaExponent = 2f;

    [Header("Shake Limits")]
    [Tooltip("Độ lệch vị trí tối đa theo trục X, Y, Z.")]
    [SerializeField] private Vector3 maxOffset = new Vector3(0.5f, 0.5f, 0.2f);

    [Tooltip("Độ lệch góc xoay tối đa (Pitch, Yaw, Roll).")]
    [SerializeField] private Vector3 maxAngularOffset = new Vector3(4f, 4f, 4f);

    [Header("Noise Settings")]
    [Tooltip("Tần số (tốc độ) biến đổi của chuyển động rung.")]
    [SerializeField] private float frequency = 25f;

    // Biến lưu trữ trạng thái ban đầu
    [SerializeField] private Vector3 initialLocalPosition;
    [SerializeField] private Quaternion initialLocalRotation;
    private float seedX;
    private float seedY;
    private float seedZ;
    
    private TISInputBridge _bridge;

    private void Awake()
    {
        _bridge = TISInputBridge.Instance;
        // Lưu vị trí và góc xoay ban đầu
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

        // Tạo seed ngẫu nhiên cho Perlin Noise
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(100f, 200f);
        seedZ = Random.Range(200f, 300f);
    }

    private void Update()
    {
        if (trauma > 0f)
        {
            // Tính toán cường độ rung theo hàm mũ phi tuyến tính
            float shakeIntensity = Mathf.Pow(trauma, traumaExponent);

            // Tạo chuyển động mượt bằng Perlin Noise
            float time = Time.time * frequency;

            float offsetX = maxOffset.x * shakeIntensity * (Mathf.PerlinNoise(seedX, time) * 2f - 1f);
            float offsetY = maxOffset.y * shakeIntensity * (Mathf.PerlinNoise(seedY, time) * 2f - 1f);
            float offsetZ = maxOffset.z * shakeIntensity * (Mathf.PerlinNoise(seedZ, time) * 2f - 1f);

            float angleX = maxAngularOffset.x * shakeIntensity * (Mathf.PerlinNoise(seedX + 10f, time) * 2f - 1f);
            float angleY = maxAngularOffset.y * shakeIntensity * (Mathf.PerlinNoise(seedY + 10f, time) * 2f - 1f);
            float angleZ = maxAngularOffset.z * shakeIntensity * (Mathf.PerlinNoise(seedZ + 10f, time) * 2f - 1f);

            // Cập nhật vị trí và góc xoay tương đối
            transform.localPosition = initialLocalPosition + new Vector3(offsetX, offsetY, offsetZ);
            transform.localRotation = initialLocalRotation * Quaternion.Euler(angleX, angleY, angleZ);

            // Giảm dần trauma theo thời gian
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * traumaDecay);
        }
        else
        {
            // Trả Camera về vị trí ban đầu khi hết rung
            if (transform.localPosition != initialLocalPosition || transform.localRotation != initialLocalRotation)
            {
                transform.localPosition = initialLocalPosition;
                transform.localRotation = initialLocalRotation;
            }
        }
    }

    /// <summary>
    /// Hàm chính: Thêm Trauma để kích hoạt hoặc cộng dồn độ rung.
    /// </summary>
    /// <param name="amount">Mức độ trauma từ 0.0 đến 1.0</param>
    [Button]
    public void AddTrauma(float amount)
    {
        if (_bridge != null)
            _bridge.TryRumble(0.6f, 1f, amount);
        //Debug.Log("Camera shake");
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// <summary>
    /// Rung theo thời gian cố định (Hàm phụ trợ).
    /// </summary>
    [Button]
    public void Shake(float duration, float magnitude)
    {
        StopAllCoroutines();
        StartCoroutine(TimedShakeCoroutine(duration, magnitude));
    }

    private IEnumerator TimedShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            trauma = Mathf.Clamp01(magnitude * (1f - (elapsed / duration)));
            elapsed += Time.deltaTime;
            yield return null;
        }
        trauma = 0f;
    }

    /// <summary>
    /// Dừng rung ngay lập tức.
    /// </summary>
    [Button]
    public void StopShake()
    {
        trauma = 0f;
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
    }
}