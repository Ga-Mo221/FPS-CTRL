using UnityEngine;

/// <summary>
/// Death camera (CHỈ local player): khi chết, camera bay lùi ra sau + lên trên để nhìn thấy nhân vật.
///
/// Camera Root bị NPlayer tách khỏi hierarchy và được TransformConstraint (World space) ghi đè
/// pose tuyệt đối mỗi LateUpdate. Component này chạy SAU constraint (DefaultExecutionOrder cao)
/// và chỉ ghi đè pose trong lúc chết → khi hồi sinh, constraint tự kéo camera về, không bị trôi.
/// </summary>
[DefaultExecutionOrder(1000)]
public class DeathCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera Root (object bị tách ra ngoài lúc spawn). Để trống = tự lấy từ CharComponentManager.Camera.")]
    [SerializeField] private Transform _cameraRoot;
    [Tooltip("Điểm camera nhìn vào khi chết (vd. xương Spine/ngực).")]
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private CameraClipping _cameraClipping;

    [Header("Death Camera")]
    [SerializeField] private float _backDistance = 2.5f;
    [SerializeField] private float _height = 1.2f;
    [SerializeField] private float _flyDuration = 0.6f;
    [Tooltip("Layer chứa thân người của local player (bị ẩn khỏi camera lúc sống).")]
    [SerializeField] private string _localModelLayer = "LocalPlayerModel";

    [Header("Revive")]
    [Tooltip("Tên layer Dead trong Animator và state rỗng mà nó trở về khi hồi sinh.")]
    [SerializeField] private string _deadLayerName = "Dead";
    [SerializeField] private string _deadLayerIdleState = "NULL";
    [Tooltip("Thời gian tối đa (giây) giữ death camera sau khi hồi sinh để chờ Animator về NULL.")]
    [SerializeField] private float _maxReviveWait = 0.5f;

    [Header("Collision")]
    [Tooltip("Để trống (Nothing) = dùng Obstruction Mask của CameraClipping.")]
    [SerializeField] private LayerMask _obstructionMask;
    [SerializeField] private float _cameraRadius = 0.2f;

    private CharComponentManager _componentManager;
    private Camera _camera;

    private bool _active;
    private float _timer;
    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _backDir;
    private int _originalCullingMask;
    private float _reviveWait;
    private Vector3 _lastPos;
    private Quaternion _lastRot;
    private int _deadLayerIndex = -2; // -2 = chưa tra
    private int _deadIdleHash;

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        if (_componentManager == null) return;

        _camera = _componentManager.Camera;

        // Camera -> Camera Shake -> Camera Clipping -> Camera Root (cùng cách NPlayer tìm để tách ra)
        if (_cameraRoot == null && _camera != null)
            _cameraRoot = _camera.transform.parent.parent.parent;

        if (_cameraClipping == null && _cameraRoot != null)
            _cameraClipping = _cameraRoot.GetComponentInChildren<CameraClipping>(true);

        if (_lookTarget == null)
            _lookTarget = transform;
    }

    private void LateUpdate()
    {
        if (_componentManager == null || _cameraRoot == null || _camera == null) return;

        NPlayer nPlayer = _componentManager.NPlayer;
        if (nPlayer == null || !nPlayer.IsLocalPlayer)
        {
            if (_active) EndDeathCamera();
            return;
        }

        bool isDead = _componentManager.NHPHandler != null && _componentManager.NHPHandler.IsDead;

        if (isDead)
        {
            _reviveWait = 0f;
            if (!_active) BeginDeathCamera();
        }
        else if (_active)
        {
            // Đợi layer Dead của Animator về NULL rồi mới trả camera (camera anchor bám xương đầu,
            // nếu trả sớm khi xương còn ở pose nằm thì camera bị giật). Có giới hạn thời gian chờ.
            _reviveWait += Time.deltaTime;
            if (!IsDeadPoseActive() || _reviveWait >= _maxReviveWait)
                EndDeathCamera();
        }

        // Trong lúc chờ Animator về NULL (đã hồi sinh) thì giữ nguyên camera, không bám theo nhân vật
        // vừa được dịch tới điểm hồi sinh.
        if (_active && isDead) UpdateDeathCamera();
        else if (_active) _cameraRoot.SetPositionAndRotation(_lastPos, _lastRot);
    }

    private void OnDisable()
    {
        if (_active) EndDeathCamera();
    }

    // ─────────────────────────────────────────────

    private void BeginDeathCamera()
    {
        _active = true;
        _timer = 0f;
        _reviveWait = 0f;
        _startPos = _lastPos = _cameraRoot.position;
        _startRot = _lastRot = _cameraRoot.rotation;

        // Hướng lùi = ngược hướng camera đang nhìn, chiếu lên mặt phẳng ngang (cố định suốt lúc chết).
        Vector3 flatForward = Vector3.ProjectOnPlane(_cameraRoot.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        _backDir = -flatForward.normalized;

        // CameraClipping bắn ray từ trong đầu nhân vật → sẽ kéo camera về sát người. Tắt khi chết.
        if (_cameraClipping != null)
        {
            _cameraClipping.enabled = false;
            _cameraClipping.ResetPose();
        }

        // Hiện lại thân người của chính mình.
        _originalCullingMask = _camera.cullingMask;
        int layer = LayerMask.NameToLayer(_localModelLayer);
        if (layer >= 0)
            _camera.cullingMask |= 1 << layer;
    }

    private void EndDeathCamera()
    {
        _active = false;

        if (_camera != null)
            _camera.cullingMask = _originalCullingMask;

        if (_cameraClipping != null)
            _cameraClipping.enabled = true;
        // Pose của Camera Root được TransformConstraint ghi lại ngay frame sau.
    }

    private void UpdateDeathCamera()
    {
        Vector3 focus = _lookTarget.position;
        Vector3 desiredPos = GetCollisionSafePosition(focus, focus + _backDir * _backDistance + Vector3.up * _height);

        _timer += Time.deltaTime;
        float t = _flyDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_timer / _flyDuration)) : 1f;

        Vector3 pos = Vector3.Lerp(_startPos, desiredPos, t);
        Vector3 lookDir = focus - pos;
        Quaternion lookRot = lookDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(lookDir, Vector3.up) : _startRot;

        _lastPos = pos;
        _lastRot = Quaternion.Slerp(_startRot, lookRot, t);
        _cameraRoot.SetPositionAndRotation(_lastPos, _lastRot);
    }

    private bool IsDeadPoseActive()
    {
        Animator anim = _componentManager.Animator;
        if (anim == null || !anim.isActiveAndEnabled) return false;

        if (_deadLayerIndex == -2)
        {
            _deadLayerIndex = anim.GetLayerIndex(_deadLayerName);
            _deadIdleHash = Animator.StringToHash(_deadLayerIdleState);
        }
        if (_deadLayerIndex < 0) return false;

        return anim.IsInTransition(_deadLayerIndex)
            || anim.GetCurrentAnimatorStateInfo(_deadLayerIndex).shortNameHash != _deadIdleHash;
    }

    private Vector3 GetCollisionSafePosition(Vector3 origin, Vector3 desired)
    {
        LayerMask mask = _obstructionMask.value != 0 ? _obstructionMask
            : _cameraClipping != null ? _cameraClipping.ObstructionMask : (LayerMask)0;
        if (mask.value == 0) return desired;

        Vector3 dir = desired - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return desired;

        if (Physics.SphereCast(origin, _cameraRadius, dir / dist, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            return origin + dir / dist * hit.distance;

        return desired;
    }
}
