using System;
using UnityEngine;
using UnityEngine.Events;

public class CustomLocalRigidbody : MonoBehaviour
{
    [Header("--- CẤU HÌNH VẬT LÝ ---")]
    [Tooltip("Vận tốc hiện tại của vật thể")]
    public Vector3 velocity = Vector3.zero;

    [Tooltip("Gia tốc trọng lực")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    [Tooltip("Độ cản không khí (0 = không cản, 1 = cản hoàn toàn)")]
    [Range(0f, 1f)] public float drag = 0.05f;

    [Tooltip("Độ nảy khi va chạm (0 = dính chặt, 1 = nảy 100%)")]
    [Range(0f, 1f)] public float bounciness = 0.4f;

    [Tooltip("Bán kính hình cầu dùng để kiểm tra va chạm")]
    public float radius = 0.1f;

    [Tooltip("Layer mà vật thể này có thể va chạm (Ví dụ: Ground, Default...)")]
    public LayerMask collisionMask = ~0; // Mặc định va chạm với tất cả Layer

    [Tooltip("Vận tốc tối thiểu để tiếp tục nảy. Dưới ngưỡng này vật thể sẽ ngủ (dừng lại)")]
    public float stopThreshold = 0.5f;

    //[Header("--- CALLBACK EVENTS ---")]
    // Delegate C# dành cho việc Subscribe bằng CODE
    public event Action<CustomCollisionData> OnCollisionEnterLocal;

    // UnityEvent dành cho việc kéo thả trực tiếp trên INSPECTOR
    public UnityEvent<CustomCollisionData> onCollisionEnterInspector;

    // Trạng thái dừng vật lý khi đã nằm yên
    public bool IsSleeping { get; private set; } = false;

    // Structure chứa thông tin va chạm chi tiết
    [Serializable]
    public struct CustomCollisionData
    {
        public RaycastHit hitInfo;      // Thông tin tia va chạm (điểm va chạm, pháp tuyến, Collider bị đâm...)
        public Vector3 impactVelocity;  // Vận tốc ngay trước thời điểm va chạm
        public float impactForce;       // Độ lớn lực va chạm (độ dài vector vận tốc)
        public Collider hitCollider => hitInfo.collider; // Trả về Collider đã chạm
    }

    private void Update()
    {
        if (IsSleeping) return;

        // 1. TÍNH TOÁN VẬN TỐC & TRỌNG LỰC
        velocity += gravity * Time.deltaTime;
        velocity *= Mathf.Clamp01(1f - drag * Time.deltaTime);

        // 2. TÍNH QUÃNG ĐƯỜNG DI CHUYỂN TRONG FRAME NÀY
        Vector3 moveDelta = velocity * Time.deltaTime;
        float distance = moveDelta.magnitude;

        if (distance < 0.0001f) return;

        // 3. QUÉT VA CHẠM BẰNG SPHERE CAST (Tránh xuyên tường/sàn khi di chuyển nhanh)
        if (Physics.SphereCast(transform.position, radius, moveDelta.normalized, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            // Dịch chuyển vật thể đến đúng vị trí tiếp xúc
            transform.position = hit.point + hit.normal * radius;

            Vector3 preImpactVelocity = velocity;

            // Đóng gói dữ liệu va chạm
            CustomCollisionData collisionData = new CustomCollisionData
            {
                hitInfo = hit,
                impactVelocity = preImpactVelocity,
                impactForce = preImpactVelocity.magnitude
            };

            // Tính vận tốc nảy lại
            Vector3 reflectedVelocity = Vector3.Reflect(preImpactVelocity, hit.normal);
            velocity = reflectedVelocity * bounciness;

            // KÍCH HOẠT CALLBACKS RA BÊN NGOÀI
            OnCollisionEnterLocal?.Invoke(collisionData);
            onCollisionEnterInspector?.Invoke(collisionData);

            // Kiểm tra xem lực nảy còn đủ lớn không, nếu quá yếu thì cho vật thể "Ngủ" (Stop)
            if (velocity.magnitude < stopThreshold)
            {
                velocity = Vector3.zero;
                IsSleeping = true;
            }
        }
        else
        {
            // Không vướng va chạm -> Di chuyển tự do
            transform.position += moveDelta;
        }
    }

    /// <summary>
    /// Hàm dùng để tác động lực/đẩy vật thể đi (Ví dụ: khi nổ, văng ra...)
    /// </summary>
    public void AddForce(Vector3 force)
    {
        IsSleeping = false;
        velocity += force;
    }

    // Hiển thị khung bán kính va chạm trong Scene view để dễ tinh chỉnh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}