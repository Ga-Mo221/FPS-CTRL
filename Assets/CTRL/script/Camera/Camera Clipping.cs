using UnityEngine;

public class CameraClipping : MonoBehaviour
{
    [SerializeField] private Transform _cameraRoot;
    [SerializeField] private float _cameraRadius = 0.2f;
    [SerializeField] private LayerMask _obstructionMask;

    private Vector3 _originalLocalPosition;

    void Awake()
    {
        // Lưu local position gốc ngay khi start
        _originalLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (_cameraRoot == null) return;

        // ✅ Restore về local position gốc trước, rồi mới tính lại
        transform.localPosition = _originalLocalPosition;

        Vector3 origin = _cameraRoot.position;
        Vector3 desiredPos = transform.position;        // world position sau khi restore
        Vector3 direction = desiredPos - origin;
        float distance = direction.magnitude;

        if (distance < 0.01f) return;

        if (Physics.SphereCast(
                origin,
                _cameraRadius,
                direction.normalized,
                out RaycastHit hit,
                distance,
                _obstructionMask))
        {
            transform.position = origin + direction.normalized * hit.distance;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_cameraRoot == null) return;

        // Tính desiredPos từ original local position để gizmo chính xác
        Vector3 worldDesired = transform.parent != null
            ? transform.parent.TransformPoint(_originalLocalPosition)
            : transform.position;

        Vector3 origin = _cameraRoot.position;
        Vector3 direction = worldDesired - origin;
        float distance = direction.magnitude;

        if (distance < 0.01f) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, worldDesired);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, _cameraRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(worldDesired, _cameraRadius);

        if (Physics.SphereCast(
                origin,
                _cameraRadius,
                direction.normalized,
                out RaycastHit hit,
                distance,
                _obstructionMask))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin + direction.normalized * hit.distance, _cameraRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(hit.point, hit.normal * 0.3f);
        }
    }
#endif
}