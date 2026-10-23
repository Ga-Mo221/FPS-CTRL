using Fusion;
using GaMo.BlenderEditorTools;
using UnityEngine;

/// <summary>
/// Chống rung camera kiểu GIMBAL (tuỳ chọn Cài đặt → Đồ họa → "Chống rung camera").
///
/// Bình thường: Camera-Root bám <c>Camera Anchor Point</c>, mà Anchor nằm dưới <c>Root</c> — Root copy pose
/// THẾ GIỚI của xương <c>RAW_Camera</c> trên thân → camera lắc/nghiêng/nhấp nhô theo animation đi lại.
/// (Không thể chỉ khoá localRotation của RAW_Camera: animation cố ý xoay xương này để bù độ cong lưng khi
/// ngước/cúi — khoá nó làm camera nghiêng theo pitch.)
///
/// Khi bật: Root bám "Root Gimbal" (thay RAW_Camera thân) → cả camera chain lẫn Weapon Parrent (súng cầm hông)
/// cùng ổn định; Camera-Root bám "Camera Gimbal" (= Root Gimbal × giật súng, bỏ Camera Shake theo animation súng).
/// Pose 2 gimbal do <see cref="CameraGimbalSolver"/> tính TRONG ConstraintScheduler (sau Animator, trước Root/Camera-Root/súng):
/// • Xoay = yaw nhân vật (chuột ngang) × pitch chuột (NetworkedPitch × <see cref="_pitchScale"/>, giống hệt
///   tỉ lệ thân tạo ra) × giật súng (Camera Recoil). Không roll, không lắc theo dáng đi.
/// • Vị trí = điểm xoay cổ (làm mượt → hết nhấp nhô khi chạy, vẫn hạ thấp khi ngồi) + cánh tay cổ→camera
///   xoay theo pitch gimbal (ngước/cúi vẫn tức thì, không trễ).
/// Chỉ áp cho nhân vật của máy mình.
/// </summary>
[DefaultExecutionOrder(140)]
public class CameraStabilizer : MonoBehaviour
{
    [Tooltip("Pitch camera = NetworkedPitch × hệ số này (đo được từ animation cúi/ngước của thân: ~0.845).")]
    [SerializeField] private float _pitchScale = 0.845f;

    [Tooltip("Vị trí camera so với điểm xoay cổ, trong hệ trục camera (đo từ animation ngước/cúi).")]
    [SerializeField] private Vector3 _neckToCamera = new Vector3(0f, 0.313f, 0.051f);

    [Tooltip("Thời gian làm mượt vị trí điểm xoay cổ (giây). Lớn = ít nhấp nhô hơn nhưng ngồi xuống/đứng lên chậm hơn.")]
    [SerializeField] private float _positionSmoothTime = 0.3f;

    [Tooltip("Độ lệch tối đa (m) cho phép giữa camera đã làm mượt và điểm xoay cổ thật.\n" +
             "Đi/chạy nhấp nhô ~3–4.5cm nên vẫn được làm mượt; nhảy/đáp/ngồi nhanh (thân tụt tới ~50cm) " +
             "thì camera không bị bỏ lại phía trên-sau đầu → không thấy thân mình. 0 = không giới hạn.")]
    [SerializeField] private float _maxPositionLag = 0.05f;

    [Tooltip("Hiện trạng thái tuỳ chọn \"Chống rung camera\" trong Cài đặt.\n" +
             "Trong Play mode tick/bỏ tick ở đây = đổi cài đặt đó (và lưu lại).")]
    [SerializeField] private bool _stabilize = false;

    /// <summary>Bật = camera kiểu gimbal. Đọc/ghi SettingData.StableCamera (lưu JSON local + PlayFab).</summary>
    public static bool Enabled
    {
        get
        {
            var sm = SettingManager.Instance;
            return sm != null && sm.SettingData != null && sm.SettingData.StableCamera;
        }
        set
        {
            var sm = SettingManager.Instance;
            if (sm == null || sm.SettingData == null || sm.SettingData.StableCamera == value) return;
            sm.SettingData.StableCamera = value;
            sm.Save();
        }
    }

    /// <summary>[Aemy 2026-10-22] Muc chong rung 0..1 (SettingData.StableCameraStrength, slider "Mức chống rung").
    /// 1 = gimbal khoa hoan toan nhu truoc; nho hon = tron lai mot phan lac / nghieng / nhap nho cua than
    /// (bat chong rung ma van con cam giac co the); 0 = giong nhu tat.</summary>
    public static float Strength
    {
        get
        {
            var sm = SettingManager.Instance;
            return sm != null && sm.SettingData != null ? Mathf.Clamp01(sm.SettingData.StableCameraStrength) : 1f;
        }
    }

    private const float SNAP_DISTANCE = 1f; // điểm xoay nhảy xa hơn mức này (hồi sinh/dịch chuyển) → đặt thẳng, không trượt

    private NetworkObject _networkObject;
    private NCharMovementHandler _movement;
    private NHPHandler _hp;
    private CharComponentManager _components;
    private Transform _anchor;          // Camera Anchor Point (target gốc của Camera-Root)
    private Transform _recoil;          // Camera Recoil (giữ hiệu ứng giật khi bắn)
    private Transform _rootBone;        // "Root" (cha của camera chain + Weapon Parrent)
    private Transform _bodyCamera;      // RAW_Camera của thân (target gốc của Root)
    private TransformConstraint _rootConstraint;
    private TransformConstraint _cameraRootConstraint;
    private Transform _rootGimbal;      // Root bám cái này → súng cầm hông cũng ổn định theo camera
    private Transform _cameraGimbal;    // Camera-Root bám cái này (= Root Gimbal × giật súng)
    private bool _active;               // đang thay target sang gimbal
    private Vector3 _pivotLocal;        // điểm xoay cổ trong hệ yaw của nhân vật (đã làm mượt)
    private Vector3 _pivotVelocity;
    private bool _pivotReady;

    private void Awake()
    {
        _networkObject = GetComponentInParent<NetworkObject>();
        _movement = GetComponentInChildren<NCharMovementHandler>(true);
        _hp = GetComponentInChildren<NHPHandler>(true);
        _components = GetComponentInChildren<CharComponentManager>(true);
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Camera Anchor Point") _anchor = t;
            else if (t.name == "Camera Recoil") _recoil = t;
            else if (t.name == "Root" && t.parent == transform) _rootBone = t;
        }
        _stabilize = Enabled;
    }

    private void OnValidate()
    {
        // Tick trong Inspector lúc đang chơi → áp dụng ngay.
        if (Application.isPlaying && _stabilize != Enabled) Enabled = _stabilize;
    }

    private void Update() => _stabilize = Enabled; // Inspector luôn hiện đúng trạng thái (kể cả khi đổi từ menu)

    // Chỉ bật/tắt (đổi target) ở đây; pose gimbal tính trong ConstraintScheduler (CameraGimbalSolver),
    // sau khi thân đã có pose của frame này và trước Root / Camera-Root / súng.
    private void LateUpdate()
    {
        bool want = Enabled && IsLocalAlive() && ResolveConstraints();
        if (want != _active) SetActive(want);
    }

    private void OnDisable() { if (_active) SetActive(false); }
    private void OnDestroy()
    {
        if (_rootGimbal != null) Destroy(_rootGimbal.gameObject);
        if (_cameraGimbal != null) Destroy(_cameraGimbal.gameObject);
    }

    private bool IsLocalAlive() =>
        _networkObject != null && _networkObject.IsValid && _networkObject.HasInputAuthority &&
        _movement != null && _anchor != null && _rootBone != null && (_hp == null || !_hp.IsDead);

    private bool ResolveConstraints()
    {
        if (_rootConstraint == null)
        {
            // Root: TransformConstraint ghi chính Root, target = RAW_Camera của thân.
            foreach (var tc in _rootBone.GetComponents<TransformConstraint>())
                if (tc.constraintObject == _rootBone && tc.targets.Count > 0 && tc.targets[0] != null && tc.targets[0].target != null)
                { _rootConstraint = tc; _bodyCamera = tc.targets[0].target; break; }
        }
        if (_cameraRootConstraint == null && _components != null && _components.Camera != null)
        {
            foreach (var tc in _components.Camera.transform.root.GetComponents<TransformConstraint>())
                for (int i = 0; i < tc.targets.Count; i++)
                    if (tc.targets[i] != null && tc.targets[i].target == _anchor) { _cameraRootConstraint = tc; break; }
        }
        return _rootConstraint != null && _cameraRootConstraint != null;
    }

    // Đổi target (World space copy tuyệt đối nên không cần chỉnh baseline):
    //   Root:        RAW_Camera thân ↔ Root Gimbal   (→ Weapon Parrent/súng cầm hông ổn định theo)
    //   Camera-Root: Anchor          ↔ Camera Gimbal
    private void SetActive(bool on)
    {
        _active = on;
        if (_rootConstraint == null || _cameraRootConstraint == null) return;
        if (on && _rootGimbal == null)
        {
            _rootGimbal = new GameObject("Root Gimbal").transform;
            _rootGimbal.SetParent(transform, false);
            _cameraGimbal = new GameObject("Camera Gimbal").transform;
            _cameraGimbal.SetParent(transform, false);
            var solver = _rootGimbal.gameObject.AddComponent<CameraGimbalSolver>();
            solver.Owner = this;
            solver.Input = _bodyCamera;
            solver.SecondOutput = _cameraGimbal;
        }
        if (_rootGimbal != null) _rootGimbal.gameObject.SetActive(on); // tắt → solver rời scheduler
        _pivotReady = false;
        if (on) SolveGimbal();                                         // có pose ngay frame đầu

        Swap(_rootConstraint, on ? _bodyCamera : _rootGimbal, on ? _rootGimbal : _bodyCamera);
        Swap(_cameraRootConstraint, on ? _anchor : _cameraGimbal, on ? _cameraGimbal : _anchor);
        ConstraintScheduler.MarkDirty();
    }

    private static void Swap(TransformConstraint tc, Transform from, Transform to)
    {
        var targets = tc.targets;
        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null && targets[i].target == from) targets[i].target = to;
    }

    /// <summary>[2026-10-23] Giới hạn MỀM độ trễ của điểm xoay đã làm mượt so với điểm xoay thật.
    /// Lỗi cũ: clip JUMP UP hạ thân ~50cm trong ~0.1s, SmoothDamp 0.3s để camera tụt lại phía trên-sau đầu
    /// tới ~46cm (× Mức chống rung) → thấy đầu/vai của chính mình vài frame khi nhảy.
    /// Dưới "knee" (60% giới hạn) giữ nguyên; trên đó nén dần, tiệm cận _maxPositionLag (không cắt cứng → không giật).</summary>
    private void LimitPivotLag(Vector3 target)
    {
        if (_maxPositionLag <= 0f) return;
        Vector3 lag = _pivotLocal - target;
        float d = lag.magnitude;
        float knee = _maxPositionLag * 0.6f;
        if (d <= knee) return;

        float range = _maxPositionLag - knee;
        float limited = knee + range * (1f - Mathf.Exp(-(d - knee) / range));
        Vector3 dir = lag / d;
        _pivotLocal = target + dir * limited;

        // Bỏ phần vận tốc đang kéo ra xa điểm thật, tránh tích vận tốc rồi vọt quá khi thân dừng lại.
        float outward = Vector3.Dot(_pivotVelocity, dir);
        if (outward > 0f) _pivotVelocity -= dir * outward;
    }

    /// <summary>Gọi bởi <see cref="CameraGimbalSolver"/> trong ConstraintScheduler (pose của frame hiện tại).</summary>
    public void SolveGimbal()
    {
        if (!_active || _movement == null || _bodyCamera == null) return;
        Transform player = transform;
        Quaternion yaw = player.rotation;

        // Xoay lý tưởng: yaw + pitch chuột, không roll, không lắc theo dáng đi.
        Quaternion rot = yaw * Quaternion.Euler(_movement.NetworkedPitch * _pitchScale, 0f, 0f);

        // Điểm xoay cổ từ pose thật của thân (Root ≡ RAW_Camera thân; Anchor ≡ Root khi không giật)
        // → làm mượt trong hệ yaw của nhân vật.
        Vector3 pivotWorld = _bodyCamera.position - _bodyCamera.rotation * _neckToCamera;
        Vector3 target = Quaternion.Inverse(yaw) * (pivotWorld - player.position);
        if (!_pivotReady || (target - _pivotLocal).sqrMagnitude > SNAP_DISTANCE * SNAP_DISTANCE)
        {
            _pivotLocal = target;
            _pivotVelocity = Vector3.zero;
            _pivotReady = true;
        }
        else
        {
            _pivotLocal = Vector3.SmoothDamp(_pivotLocal, target, ref _pivotVelocity, _positionSmoothTime);
            LimitPivotLag(target);
        }

        Vector3 pos = player.position + yaw * _pivotLocal + rot * _neckToCamera;

        // Camera = Root × giật súng (Camera Recoil); bỏ Camera Shake theo animation súng.
        Quaternion camRot = rot;
        Vector3 camPos = pos;
        if (_recoil != null) { camRot = rot * _recoil.localRotation; camPos = pos + rot * _recoil.localPosition; }

        // [Aemy 2026-10-22] Tron voi pose "theo than" (y het luc tat) theo muc chong rung.
        float k = Strength;
        if (k < 0.999f)
        {
            // Root khi tat = RAW_Camera thân; Camera-Root khi tat = Anchor = Root(thân) × (Anchor so voi Root, gom Camera Shake).
            Vector3 bodyPos = _bodyCamera.position; Quaternion bodyRot = _bodyCamera.rotation;
            Quaternion invRoot = Quaternion.Inverse(_rootBone.rotation);
            Quaternion anchorRelRot = invRoot * _anchor.rotation;
            Vector3 anchorRelPos = invRoot * (_anchor.position - _rootBone.position);
            Vector3 bodyCamPos = bodyPos + bodyRot * anchorRelPos; Quaternion bodyCamRot = bodyRot * anchorRelRot;

            pos = Vector3.LerpUnclamped(bodyPos, pos, k);           rot = Quaternion.SlerpUnclamped(bodyRot, rot, k);
            camPos = Vector3.LerpUnclamped(bodyCamPos, camPos, k);  camRot = Quaternion.SlerpUnclamped(bodyCamRot, camRot, k);
        }
        _rootGimbal.SetPositionAndRotation(pos, rot);
        _cameraGimbal.SetPositionAndRotation(camPos, camRot);
    }
}
