using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Weapon Collision Resolver - Animation-Compatible + Smooth Slide Version
///
/// KIẾN TRÚC: Additive Offset Pattern, tách biệt 3 lớp offset độc lập:
///   • Push Offset   — đẩy súng ra khỏi tường (theo normal của tường)
///   • Slide Offset  — trượt súng dọc theo tường khi bị đâm (theo trục local)
///   • Rotation      — nghiêng súng theo độ sâu đâm vào tường
///
/// Mỗi lớp có Lerp tốc độ riêng → smooth hoàn toàn độc lập với nhau.
/// slideDepth được tính bằng Raycast dọc trục forward của súng → chính xác hơn
/// penetrationDepth của sphere (sphere chỉ đo overlap tại mũi, không đo "sâu bao nhiêu").
/// </summary>
[DefaultExecutionOrder(100)]
public class WeaponCollisionResolver : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // COLLISION
    // ─────────────────────────────────────────────────────────────────────────
    [TabGroup("WeaponCollision", "Collision")]
    [Title("Chế Độ Hoạt Động")]
    [InfoBox(
        "PIVOT MODE (khuyến nghị): Offset áp lên GameObject cha không bị Animator kiểm soát.\n" +
        "BONE OVERRIDE MODE: Ghi đè trực tiếp lên bone sau khi Animator chạy (Execution Order +100).",
        InfoMessageType.Info)]
    [TabGroup("WeaponCollision", "Collision")]
    [EnumToggleButtons]
    public CollisionMode collisionMode = CollisionMode.PivotMode;

    [TabGroup("WeaponCollision", "Collision")]
    [ShowIf("collisionMode", CollisionMode.PivotMode)]
    [Required("Gán GameObject cha (không bị Animator kiểm soát) vào đây!")]
    public Transform collisionPivot;

    [TabGroup("WeaponCollision", "Collision")]
    [ShowIf("collisionMode", CollisionMode.BoneOverrideMode)]
    [Required("Gán RAW_Root hoặc bone cần xử lý vào đây!")]
    public Transform animatedBone;

    [TabGroup("WeaponCollision", "Collision")]
    [Title("Mũi Súng (Tip Sphere)")]
    public Transform weaponTipTransform;

    [TabGroup("WeaponCollision", "Collision")]
    public float tipSphereRadius = 0.05f;

    [TabGroup("WeaponCollision", "Collision")]
    [ShowIf("@weaponTipTransform == null")]
    public Vector3 tipLocalOffset = new Vector3(0f, 0f, 0.5f);

    // ─────────────────────────────────────────────────────────────────────────
    // WALL
    // ─────────────────────────────────────────────────────────────────────────
    [TabGroup("WeaponCollision", "Wall")]
    [Title("Va Chạm & Tường")]
    public LayerMask obstacleLayer;

    [TabGroup("WeaponCollision", "Wall")]
    [Tooltip("Khoảng đệm chống giật ở mép tường.")]
    public float antiJitterBuffer = 0.03f;

    [TabGroup("WeaponCollision", "Wall")]
    [Title("Tốc Độ Push (đẩy ra / kéo về)")]
    public float pushSpeed = 35f;

    [TabGroup("WeaponCollision", "Wall")]
    public float returnSpeed = 8f;

    // ─────────────────────────────────────────────────────────────────────────
    // SLIDE
    // ─────────────────────────────────────────────────────────────────────────
    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    public bool enableSliding = true;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    [ShowIf("enableSliding")]
    [Tooltip("Hướng trượt trong không gian local của súng.\n" +
             "X = phải/trái, Y = lên/xuống, Z = tiến/lùi.\n" +
             "Giá trị âm Y (-1) = trượt xuống khi đâm vào tường — cảm giác tự nhiên nhất.")]
    public Vector3 slideMultiplier = new Vector3(0f, -1f, 0f);

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    [ShowIf("enableSliding")]
    [Tooltip("Khoảng trượt tối đa (meters).")]
    public float maxSlideDistance = 0.25f;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    [ShowIf("enableSliding")]
    [Tooltip("Tốc độ Lerp của slide — tách biệt với pushSpeed để có thể tune riêng.\n" +
             "Thấp hơn pushSpeed → slide có độ trễ nhẹ, trông mượt hơn.")]
    public float slideSpeed = 12f;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    [ShowIf("enableSliding")]
    [Tooltip("Tốc độ Lerp kéo slide về 0 khi không còn va chạm.")]
    public float slideReturnSpeed = 6f;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Slide")]
    [ShowIf("enableSliding")]
    [Tooltip("Độ sâu Raycast (meters) từ gốc súng theo trục forward để đo slideDepth.\n" +
             "Đặt bằng hoặc lớn hơn chiều dài súng.")]
    public float slideRaycastLength = 1.0f;

    // ─────────────────────────────────────────────────────────────────────────
    // ROTATE
    // ─────────────────────────────────────────────────────────────────────────
    [TabGroup("WeaponCollision", "Slide & Rotate")]
    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Rotate")]
    public bool enableRotation = true;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Rotate")]
    [ShowIf("enableRotation")]
    public Vector3 maxRotationAngle = new Vector3(15f, 30f, 10f);

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Rotate")]
    [ShowIf("enableRotation")]
    [Tooltip("Nhân với slideDepth để ra góc xoay. Tăng lên nếu muốn súng nghiêng nhiều hơn.")]
    public float rotationSensitivity = 2.5f;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Rotate")]
    [ShowIf("enableRotation")]
    [Tooltip("Tốc độ Lerp xoay súng — tách biệt để có độ trễ riêng.")]
    public float rotateSpeed = 10f;

    [TabGroup("WeaponCollision/Slide & Rotate/tabs", "Rotate")]
    [ShowIf("enableRotation")]
    public float rotateReturnSpeed = 6f;

    // ─────────────────────────────────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────────────────────────────────
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public bool isColliding;
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public float currentPenetrationDepth;
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public float currentSlideDepth;       // độ sâu đo bằng raycast forward
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public Vector3 currentPushOffset;     // offset đẩy ra (world)
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public Vector3 currentSlideOffset;    // offset trượt đang thực tế áp (world, smoothed)
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public Vector3 currentRotationOffset; // euler đang thực tế áp (smoothed)

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────
    private Vector3 pivotOriginalLocalPos;
    private Quaternion pivotOriginalLocalRot;

    private Vector3 boneAdditiveWorldOffset;
    private Quaternion boneAdditiveWorldRot;

    // 3 lớp offset smoothed — mỗi lớp Lerp độc lập
    private Vector3  _smoothedPushOffset;    // world space
    private Vector3  _smoothedSlideOffset;   // world space
    private Quaternion _smoothedRotOffset;   // local rotation additive

    // Proxy sphere cho Physics.ComputePenetration
    private GameObject   _proxyGO;
    private SphereCollider _proxySphere;

    // ─────────────────────────────────────────────────────────────────────────

    public enum CollisionMode
    {
        [LabelText("Pivot Mode (Khuyến nghị)")]  PivotMode,
        [LabelText("Bone Override Mode")]          BoneOverrideMode
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INIT
    // ─────────────────────────────────────────────────────────────────────────
    void Start() => InitializeMode();

    void InitializeMode()
    {
        if (collisionMode == CollisionMode.PivotMode && collisionPivot != null)
        {
            pivotOriginalLocalPos = collisionPivot.localPosition;
            pivotOriginalLocalRot = collisionPivot.localRotation;
        }

        _smoothedPushOffset  = Vector3.zero;
        _smoothedSlideOffset = Vector3.zero;
        _smoothedRotOffset   = Quaternion.identity;
        boneAdditiveWorldOffset = Vector3.zero;
        boneAdditiveWorldRot    = Quaternion.identity;

        // Proxy sphere cho ComputePenetration — ẩn, không tham gia simulation
        _proxyGO = new GameObject("__WeaponTipProxy__");
        _proxyGO.hideFlags = HideFlags.HideAndDontSave;
        _proxySphere = _proxyGO.AddComponent<SphereCollider>();
        _proxySphere.isTrigger = true;
        _proxyGO.layer = 2; // Ignore Raycast
    }

    void OnDestroy()
    {
        if (_proxyGO != null) Destroy(_proxyGO);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        switch (collisionMode)
        {
            case CollisionMode.PivotMode:      ResolvePivotMode();      break;
            case CollisionMode.BoneOverrideMode: ResolveBoneOverrideMode(); break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CORE — tính toán 3 offset target, Lerp mỗi lớp độc lập
    // ─────────────────────────────────────────────────────────────────────────
    void ComputeOffsets(
        Vector3 worldTipPos,
        Transform referenceTransform,   // dùng để tính trục local cho slide
        out Vector3 targetPush,
        out Vector3 targetSlide,
        out Quaternion targetRot)
    {
        float checkRadius = tipSphereRadius + (isColliding ? antiJitterBuffer : 0f);
        Collider[] hits = Physics.OverlapSphere(worldTipPos, checkRadius, obstacleLayer);

        if (hits.Length > 0)
        {
            isColliding = true;

            // ── 1. PUSH: hướng + độ sâu từ ComputePenetration ────────────────
            Vector3 pushDir;
            float   penetration;
            ComputePushVector(hits[0], worldTipPos, checkRadius, out pushDir, out penetration);
            currentPenetrationDepth = penetration;

            targetPush = pushDir * (penetration + antiJitterBuffer);
            currentPushOffset = targetPush;

            // ── 2. SLIDE DEPTH: Raycast từ gốc súng theo -forward ─────────────
            // Dùng raycast thay vì penetrationDepth vì penetration chỉ đo overlap
            // tại mũi sphere — không phản ánh "súng đâm sâu bao nhiêu vào tường".
            // Raycast dọc forward từ gốc súng đến tường cho khoảng cách thực.
            float slideDepth = 0f;
            if (enableSliding || enableRotation)
            {
                Vector3 gunOrigin  = referenceTransform.position;
                Vector3 gunForward = referenceTransform.forward;

                // Raycast từ gốc về phía tường (forward)
                RaycastHit rHit;
                if (Physics.Raycast(gunOrigin, gunForward, out rHit, slideRaycastLength, obstacleLayer))
                {
                    // slideDepth = bao nhiêu mét súng "muốn đâm qua" nhưng bị chặn
                    // = (khoảng cách từ gốc đến mũi) - (khoảng cách từ gốc đến tường)
                    float gunLength   = Vector3.Distance(gunOrigin, worldTipPos);
                    float wallDist    = rHit.distance;
                    slideDepth        = Mathf.Max(0f, gunLength - wallDist);
                }
                else
                {
                    // Raycast không trúng (góc xiên), dùng penetration làm fallback
                    slideDepth = penetration;
                }
                currentSlideDepth = slideDepth;
            }

            // ── 3. SLIDE OFFSET: trượt theo local axes của súng ───────────────
            if (enableSliding && slideDepth > 0f)
            {
                // slideMultiplier nhân với slideDepth (meters) → offset thuần túy
                Vector3 rawSlide = slideMultiplier * slideDepth;
                rawSlide = Vector3.ClampMagnitude(rawSlide, maxSlideDistance);

                // Chuyển từ local → world dùng trục của referenceTransform
                targetSlide = (referenceTransform.right   * rawSlide.x)
                            + (referenceTransform.up      * rawSlide.y)
                            + (referenceTransform.forward * rawSlide.z);
            }
            else
            {
                targetSlide = Vector3.zero;
            }

            // ── 4. ROTATION: theo slideDepth (không phải penetrationDepth) ───
            if (enableRotation && slideDepth > 0f)
            {
                float rotFactor  = Mathf.Clamp01(slideDepth * rotationSensitivity);
                Vector3 euler    = maxRotationAngle * rotFactor;
                currentRotationOffset = euler;
                targetRot        = Quaternion.Euler(euler);
            }
            else
            {
                targetRot = Quaternion.identity;
            }
        }
        else
        {
            // Không va chạm — tất cả target về 0
            isColliding             = false;
            currentPenetrationDepth = 0f;
            currentSlideDepth       = 0f;
            currentPushOffset       = Vector3.zero;
            currentRotationOffset   = Vector3.zero;

            targetPush  = Vector3.zero;
            targetSlide = Vector3.zero;
            targetRot   = Quaternion.identity;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PIVOT MODE
    // ─────────────────────────────────────────────────────────────────────────
    void ResolvePivotMode()
    {
        if (collisionPivot == null) return;

        Vector3 worldTipPos = GetWorldTipPosition();

        Vector3    targetPush, targetSlide;
        Quaternion targetRot;
        ComputeOffsets(worldTipPos, collisionPivot, out targetPush, out targetSlide, out targetRot);

        // Lerp 3 lớp với tốc độ RIÊNG BIỆT
        float pSpeed = isColliding ? pushSpeed        : returnSpeed;
        float sSpeed = isColliding ? slideSpeed       : slideReturnSpeed;
        float rSpeed = isColliding ? rotateSpeed      : rotateReturnSpeed;

        _smoothedPushOffset  = Vector3.Lerp(_smoothedPushOffset,  targetPush,  Time.deltaTime * pSpeed);
        _smoothedSlideOffset = Vector3.Lerp(_smoothedSlideOffset, targetSlide, Time.deltaTime * sSpeed);
        _smoothedRotOffset   = Quaternion.Slerp(_smoothedRotOffset, targetRot, Time.deltaTime * rSpeed);

        currentSlideOffset = _smoothedSlideOffset; // debug readout

        // Tổng offset = push + slide (world space)
        Vector3 totalWorldOffset = _smoothedPushOffset + _smoothedSlideOffset;

        // Áp vào pivot
        Vector3 worldOrigPivotPos = collisionPivot.parent != null
            ? collisionPivot.parent.TransformPoint(pivotOriginalLocalPos)
            : pivotOriginalLocalPos;

        Vector3 targetWorldPivotPos = worldOrigPivotPos + totalWorldOffset;

        collisionPivot.localPosition = collisionPivot.parent != null
            ? collisionPivot.parent.InverseTransformPoint(targetWorldPivotPos)
            : targetWorldPivotPos;

        collisionPivot.localRotation = pivotOriginalLocalRot * _smoothedRotOffset;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BONE OVERRIDE MODE
    // ─────────────────────────────────────────────────────────────────────────
    void ResolveBoneOverrideMode()
    {
        if (animatedBone == null) return;

        // Đọc vị trí Animator đã set (trước khi ta ghi đè)
        Vector3    animWorldPos = animatedBone.position;
        Quaternion animWorldRot = animatedBone.rotation;

        Vector3 worldTipPos = GetWorldTipPosition();

        Vector3    targetPush, targetSlide;
        Quaternion targetRot;
        ComputeOffsets(worldTipPos, animatedBone, out targetPush, out targetSlide, out targetRot);

        float pSpeed = isColliding ? pushSpeed        : returnSpeed;
        float sSpeed = isColliding ? slideSpeed       : slideReturnSpeed;
        float rSpeed = isColliding ? rotateSpeed      : rotateReturnSpeed;

        _smoothedPushOffset  = Vector3.Lerp(_smoothedPushOffset,  targetPush,  Time.deltaTime * pSpeed);
        _smoothedSlideOffset = Vector3.Lerp(_smoothedSlideOffset, targetSlide, Time.deltaTime * sSpeed);
        _smoothedRotOffset   = Quaternion.Slerp(_smoothedRotOffset, targetRot, Time.deltaTime * rSpeed);

        currentSlideOffset = _smoothedSlideOffset;

        // Cộng additive lên trên trạng thái Animator (không thay thế)
        animatedBone.position = animWorldPos + _smoothedPushOffset + _smoothedSlideOffset;
        animatedBone.rotation = animWorldRot * _smoothedRotOffset;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    Vector3 GetWorldTipPosition()
    {
        if (weaponTipTransform != null) return weaponTipTransform.position;

        Transform root = collisionMode == CollisionMode.PivotMode
            ? (collisionPivot != null ? collisionPivot : transform)
            : (animatedBone   != null ? animatedBone   : transform);

        return root.TransformPoint(tipLocalOffset);
    }

    void ComputePushVector(Collider col, Vector3 worldTipPos, float checkRadius,
                           out Vector3 pushDir, out float penetration)
    {
        if (_proxySphere != null && col != null)
        {
            _proxyGO.transform.position = worldTipPos;
            _proxyGO.transform.rotation = Quaternion.identity;
            _proxySphere.radius = checkRadius;

            Vector3 dir; float dist;
            bool overlap = Physics.ComputePenetration(
                _proxySphere, worldTipPos, Quaternion.identity,
                col, col.transform.position, col.transform.rotation,
                out dir, out dist);

            if (overlap && dist > 0.0001f)
            {
                pushDir     = dir;
                penetration = dist;
                return;
            }

            // Fallback: ClosestPoint (trường hợp OverlapSphere bắt được nhưng chưa overlap thật sự)
            Vector3 closest = col.ClosestPoint(worldTipPos);
            Vector3 delta   = worldTipPos - closest;
            float   d       = delta.magnitude;

            if (d < 0.001f)
            {
                Transform root = collisionMode == CollisionMode.PivotMode ? collisionPivot : animatedBone;
                pushDir     = root != null ? -root.forward : Vector3.back;
                penetration = checkRadius;
            }
            else
            {
                pushDir     = delta.normalized;
                penetration = Mathf.Max(0f, checkRadius - d);
            }
        }
        else
        {
            Transform root = collisionMode == CollisionMode.PivotMode ? collisionPivot : animatedBone;
            pushDir     = root != null ? -root.forward : Vector3.back;
            penetration = tipSphereRadius;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GIZMOS
    // ─────────────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Vector3 tipPos = GetWorldTipPosition();

        // Tip sphere
        Gizmos.color = isColliding ? Color.red : new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(tipPos, tipSphereRadius);

        // Buffer zone
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(tipPos, tipSphereRadius + antiJitterBuffer);

        // Raycast forward (slide depth)
        Transform refT = collisionMode == CollisionMode.PivotMode ? collisionPivot : animatedBone;
        if (refT != null && enableSliding)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(refT.position, refT.position + refT.forward * slideRaycastLength);
        }

        // Push vector
        if (isColliding && currentPushOffset.magnitude > 0.001f)
        {
            Gizmos.color = Color.red;
            if (refT != null) Gizmos.DrawLine(refT.position, refT.position + currentPushOffset);
        }

        // Slide vector (smoothed — giá trị thực tế đang áp)
        if (_smoothedSlideOffset.magnitude > 0.001f)
        {
            Gizmos.color = Color.cyan;
            if (refT != null) Gizmos.DrawLine(refT.position, refT.position + _smoothedSlideOffset);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RUNTIME API
    // ─────────────────────────────────────────────────────────────────────────
    [Button("Reset Về Trạng Thái Gốc"), TabGroup("WeaponCollision", "Debug")]
    public void ResetToOriginal()
    {
        if (collisionMode == CollisionMode.PivotMode && collisionPivot != null)
        {
            collisionPivot.localPosition = pivotOriginalLocalPos;
            collisionPivot.localRotation = pivotOriginalLocalRot;
        }
        _smoothedPushOffset     = Vector3.zero;
        _smoothedSlideOffset    = Vector3.zero;
        _smoothedRotOffset      = Quaternion.identity;
        boneAdditiveWorldOffset = Vector3.zero;
        boneAdditiveWorldRot    = Quaternion.identity;
        isColliding             = false;
    }

    public void RefreshOriginalState()
    {
        if (collisionMode == CollisionMode.PivotMode && collisionPivot != null)
        {
            pivotOriginalLocalPos = collisionPivot.localPosition;
            pivotOriginalLocalRot = collisionPivot.localRotation;
        }
    }
}