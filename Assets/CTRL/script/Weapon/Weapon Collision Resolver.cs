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
    [Tooltip("Bone/điểm ngực trên của nhân vật (upper chest). Dùng để bắn 1 raycast\n" +
             "từ chest đến đầu súng — bắt trường hợp thân người sát tường trong khi\n" +
             "gunOrigin (vai/hông) và tip vẫn chưa chạm gì. Để trống nếu không dùng.")]
    public Transform upperChest;

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
    [Tooltip("Khoảng cách tối đa cho phép giữa animated bone (sau khi áp offset) và upperChest.\n" +
             "Ngăn súng trượt ra quá xa khỏi ngực khi va chạm kéo dài. 0 = không giới hạn.\n" +
             "Chỉ có tác dụng khi upperChest được gán.")]
    [ShowIf("@upperChest != null")]
    [MinValue(0f)]
    public float maxBoneDistanceFromChest = 0f;

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
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public bool debugChestRayEvaluated; // true nếu chest raycast có chạy frame này (không bị lớp trước chặn)
    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public bool debugChestRayHit;        // true nếu chest raycast thực sự bắn trúng collider

    [TabGroup("WeaponCollision", "Debug")]
    [Tooltip("Bật để in Debug.Log mỗi khi có va chạm, ghi rõ LỚP nào phát hiện\n" +
             "(Overlap/Sweep/Body/Chest) và TÊN collider bị trúng.")]
    public bool enableDebugLogs = false;

    [TabGroup("WeaponCollision", "Debug")]
    [ReadOnly] public bool isEnabled = true; // bật/tắt toàn bộ tính năng resolver

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

    // Swept-test (CCD) chống tunneling qua collider mỏng
    private Vector3 _prevWorldTipPos;
    private bool    _hasPrevTipPos;

    // Chống spam Debug.Log — chỉ log khi lớp phát hiện hoặc collider bị trúng thay đổi
    private string _lastDebugLogKey = "";

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
        _hasPrevTipPos          = false; // reset swept-test, tránh dùng vị trí cũ không hợp lệ

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
        if (!isEnabled) return;

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

        // ── SWEPT TEST (CCD) — chống tunneling qua collider MỎNG ──────────────
        // OverlapSphere chỉ kiểm tra tại vị trí hiện tại của tip. Nếu súng di chuyển
        // đủ nhanh trong 1 frame, tip có thể "nhảy" từ trước ra sau một tường mỏng
        // mà không frame nào overlap được → mất va chạm hoàn toàn (xuyên tường).
        // Giải pháp: SphereCast quét từ vị trí tip frame trước đến frame này, nếu
        // đường quét cắt ngang collider thì coi như vẫn đang va chạm, dùng điểm
        // cắt đó để tính push (kéo tip lùi về đúng bề mặt tường).
        bool       sweepHitFound = false;
        RaycastHit sweepHit      = default;

        if (hits.Length == 0 && _hasPrevTipPos)
        {
            Vector3 sweepDelta = worldTipPos - _prevWorldTipPos;
            float   sweepDist  = sweepDelta.magnitude;

            if (sweepDist > tipSphereRadius * 0.01f) // bỏ qua nếu gần như đứng yên
            {
                sweepHitFound = Physics.SphereCast(
                    _prevWorldTipPos, tipSphereRadius, sweepDelta.normalized,
                    out sweepHit, sweepDist, obstacleLayer);
            }
        }

        // ── BODY RAYCAST — chắn ngang thân súng dù đầu mũi không chạm gì ──────
        // OverlapSphere + swept-test ở trên chỉ theo dõi ĐẦU MŨI súng. Nếu súng
        // đi sượt qua góc tường/khung cửa khiến THÂN súng (đoạn giữa gunOrigin
        // và tip) bị chắn trong khi đầu mũi vẫn lơ lửng ngoài không khí, cả 2 lớp
        // trên đều không phát hiện được. Bắn 1 raycast dọc suốt chiều dài súng
        // (gunOrigin → tip) để bắt trường hợp này. Đây thực chất là phép quét
        // "capsule bán kính 0" — rẻ hơn CapsuleCast nhưng có thể bỏ sót nếu vật
        // cản chỉ sượt rất sát mép tia mà không cắt đúng đường thẳng.
        bool       bodyHitFound = false;
        RaycastHit bodyHit      = default;

        if (hits.Length == 0 && !sweepHitFound)
        {
            Vector3 bodyDelta = worldTipPos - referenceTransform.position;
            float   bodyDist  = bodyDelta.magnitude;

            if (bodyDist > 0.0001f)
            {
                bodyHitFound = Physics.Raycast(
                    referenceTransform.position, bodyDelta.normalized,
                    out bodyHit, bodyDist, obstacleLayer);
            }
        }

        // ── CHEST RAYCAST — thân người sát tường dù gunOrigin/tip chưa chạm ───
        // gunOrigin (pivot/bone) thường nằm ở vai/hông, có thể vẫn còn cách
        // tường 1 khoảng dù thân người (chest) đã áp sát. Bắn raycast từ
        // upperChest → tip để bắt sớm trường hợp này. Gốc = upperChest, hướng
        // = chest→tip, độ dài = đúng khoảng cách chest–tip.
        //
        // QUAN TRỌNG: Chest raycast chạy SONG SONG với OverlapSphere (không phải
        // chỉ fallback). Trường hợp tường mỏng bị xuyên qua: OverlapSphere vẫn
        // detect được collider nhưng tip đã nằm phía bên kia → ComputePenetration
        // trả về normal sai chiều (đẩy súng ra xa thay vì về phía người). Chest
        // raycast bắn từ người → tip, luôn cắt tường đúng chiều, nên được ưu tiên
        // khi phát hiện súng đã xuyên qua (chestHitFound=true ngay cả khi hits>0).
        bool       chestHitFound = false;
        RaycastHit chestHit      = default;
        debugChestRayEvaluated   = false;
        debugChestRayHit         = false;

        if (upperChest != null)
        {
            Vector3 chestDelta = worldTipPos - upperChest.position;
            float   chestDist  = chestDelta.magnitude;

            if (chestDist > 0.0001f)
            {
                debugChestRayEvaluated = true; // tia có thực sự được bắn frame này

                chestHitFound = Physics.Raycast(
                    upperChest.position, chestDelta.normalized,
                    out chestHit, chestDist, obstacleLayer);

                debugChestRayHit = chestHitFound; // có trúng collider hay không
            }
        }

        // Chest raycast được ưu tiên TRÊN OverlapSphere khi cả hai đều phát hiện:
        // đây là dấu hiệu tip đã xuyên qua tường (OverlapSphere từ phía sai,
        // chest raycast luôn từ phía người → normal đúng chiều).
        bool chestOverridesOverlap = chestHitFound && hits.Length > 0;

        if (hits.Length > 0 || sweepHitFound || bodyHitFound || chestHitFound)
        {
            isColliding = true;

            // ── DEBUG LOG — ghi rõ lớp nào phát hiện + tên collider bị trúng ──
            // Chỉ log khi lớp/collider thay đổi (tránh spam mỗi frame khi đang collide liên tục).
            if (enableDebugLogs)
            {
                string layerName;
                Collider hitCollider;

                // Chest được ưu tiên khi cả hai detect (tunneling case)
                if (chestHitFound)         { layerName = chestOverridesOverlap
                                                ? "Raycast (chest: upperChest→tip) [OVERRIDE tip-sphere]"
                                                : "Raycast (chest: upperChest→tip)";
                                             hitCollider = chestHit.collider; }
                else if (hits.Length > 0)  { layerName = "OverlapSphere (tip)"; hitCollider = hits[0]; }
                else if (sweepHitFound)    { layerName = "SphereCast (swept/thin-wall)"; hitCollider = sweepHit.collider; }
                else                       { layerName = "Raycast (body: gunOrigin→tip)"; hitCollider = bodyHit.collider; }

                string key = layerName + "|" + hitCollider.GetInstanceID();
                if (key != _lastDebugLogKey)
                {
                    _lastDebugLogKey = key;
                    Debug.Log(
                        $"[WeaponCollisionResolver] Phát hiện va chạm bằng [{layerName}] " +
                        $"— trúng collider: '{hitCollider.name}' (layer: {LayerMask.LayerToName(hitCollider.gameObject.layer)})",
                        hitCollider);
                }
            }

            // ── 1. PUSH: hướng + độ sâu ────────────────────────────────────
            Vector3 pushDir;
            float   penetration;

            // Chest raycast được ưu tiên CAO NHẤT — kể cả khi OverlapSphere cũng hit.
            // Lý do: nếu chestHitFound=true mà hits>0, tip đã xuyên qua tường (tunneling).
            // OverlapSphere lúc này detect từ phía sai → normal sai chiều → đẩy ra xa thay vì về người.
            // Chest ray bắn từ người→tip, luôn cắt tường đúng chiều → normal luôn đúng.
            if (chestHitFound)
            {
                // Trường hợp chest sát tường hoặc tip đã xuyên qua tường mỏng:
                // dùng normal tại điểm chắn trên đường chest→tip.
                // penetration = phần còn lại từ điểm chắn đến tip (càng gần tip → đẩy mạnh hơn).
                pushDir = chestHit.normal;
                float chestToTip = Vector3.Distance(upperChest.position, worldTipPos);
                float pastPoint  = Mathf.Max(0f, chestToTip - chestHit.distance);
                penetration = pastPoint + tipSphereRadius;
            }
            else if (hits.Length > 0)
            {
                // Trường hợp bình thường: đang overlap thật sự (tường dày hoặc
                // tường mỏng nhưng tip chưa kịp xuyên qua hẳn) → dùng ComputePenetration.
                ComputePushVector(hits[0], worldTipPos, checkRadius, out pushDir, out penetration);
            }
            else if (sweepHitFound)
            {
                // Trường hợp tunneling: tip đã xuyên qua hẳn tường mỏng chỉ trong
                // 1 frame. Dùng normal + điểm va chạm từ SphereCast để tính hướng
                // đẩy ra, và penetration = quãng đường tip đã "lọt" qua khỏi bề mặt
                // (đo dọc theo hướng di chuyển), để súng bị kéo lùi đúng về bề mặt tường.
                pushDir = sweepHit.normal;
                float distancePastSurface = Vector3.Distance(worldTipPos, sweepHit.point);
                penetration = distancePastSurface + tipSphereRadius;
            }
            else
            {
                // Trường hợp thân súng bị chắn ngang (bodyHit): dùng normal tại
                // điểm chắn để đẩy ra, penetration ước lượng theo cùng công thức
                // với slideDepth bên dưới (gunLength - wallDist) — càng đâm sâu
                // qua điểm chắn thì đẩy càng mạnh.
                pushDir = bodyHit.normal;
                float gunLength = Vector3.Distance(referenceTransform.position, worldTipPos);
                float pastPoint = Mathf.Max(0f, gunLength - bodyHit.distance);
                penetration = pastPoint + tipSphereRadius;
            }

            currentPenetrationDepth = penetration;

            targetPush = pushDir * (penetration + antiJitterBuffer);
            currentPushOffset = targetPush;


            // ── 2. SLIDE DEPTH: Raycast từ gốc súng theo forward của tip ──────
            // Dùng raycast thay vì penetrationDepth vì penetration chỉ đo overlap
            // tại mũi sphere — không phản ánh "súng đâm sâu bao nhiêu vào tường".
            // Raycast dọc forward từ gốc súng đến tường cho khoảng cách thực.
            float slideDepth = 0f;
            if (enableSliding || enableRotation)
            {
                Vector3 gunOrigin  = referenceTransform.position;

                // Dùng forward của weaponTipTransform (nếu có gán) thay vì
                // referenceTransform, vì tip mới phản ánh đúng hướng nòng súng
                // thực tế — referenceTransform (pivot/bone) có thể bị lệch trục
                // do setup rig/animation. Fallback về referenceTransform nếu
                // weaponTipTransform chưa được gán.
                Vector3 gunForward = weaponTipTransform != null
                    ? weaponTipTransform.forward
                    : referenceTransform.forward;

                // Độ dài raycast = khoảng cách thực tế từ gốc súng đến tip,
                // KHÔNG dùng slideRaycastLength cố định nữa — vì slideDepth
                // được tính dựa trên gunLength này, raycast phải bắn đúng bằng
                // đúng chiều dài đó thì mới đo được điểm chặn nằm ở đâu dọc
                // theo thân súng một cách chính xác.
                float gunLength = Vector3.Distance(gunOrigin, worldTipPos);

                // Raycast từ gốc về phía tường (forward)
                RaycastHit rHit;
                if (Physics.Raycast(gunOrigin, gunForward, out rHit, gunLength, obstacleLayer))
                {
                    // slideDepth = bao nhiêu mét súng "muốn đâm qua" nhưng bị chặn
                    // = (khoảng cách từ gốc đến mũi) - (khoảng cách từ gốc đến tường)
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

            if (enableDebugLogs && _lastDebugLogKey != "")
            {
                Debug.Log("[WeaponCollisionResolver] Hết va chạm.");
            }
            _lastDebugLogKey = ""; // reset để lần va chạm tiếp theo log lại từ đầu

            targetPush  = Vector3.zero;
            targetSlide = Vector3.zero;
            targetRot   = Quaternion.identity;
        }

        // Cập nhật mốc vị trí tip cho swept-test (CCD) ở frame kế tiếp
        _prevWorldTipPos = worldTipPos;
        _hasPrevTipPos   = true;
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

        // ClampPositionToChest chỉ chạy khi đang va chạm — khi súng tự nhiên trở về
        // vị trí Animator thì không được can thiệp.
        Vector3 finalWorldPivotPos = isColliding
            ? ClampPositionToChest(targetWorldPivotPos, worldOrigPivotPos)
            : targetWorldPivotPos;

        collisionPivot.localPosition = collisionPivot.parent != null
            ? collisionPivot.parent.InverseTransformPoint(finalWorldPivotPos)
            : finalWorldPivotPos;

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
        Vector3 newBonePos = animWorldPos + _smoothedPushOffset + _smoothedSlideOffset;
        // ClampPositionToChest chỉ chạy khi đang va chạm — khi súng tự nhiên trở về
        // vị trí Animator thì không được can thiệp.
        animatedBone.position = isColliding
            ? ClampPositionToChest(newBonePos, animWorldPos)
            : newBonePos;
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

    /// <summary>
    /// Clamp vị trí world của bone sao cho khoảng cách đến upperChest không vượt quá
    /// maxBoneDistanceFromChest. Nếu maxBoneDistanceFromChest = 0 hoặc upperChest chưa gán,
    /// trả về nguyên vị trí gốc (không làm gì).
    ///
    /// baseWorldPos: vị trí gốc trước khi áp offset (tức là vị trí Animator đã set).
    /// Clamp CHỈ hoạt động khi worldPos xa chest HƠN baseWorldPos — tức là offset đang
    /// kéo súng ra xa. Khi offset đang trượt súng về phía chest (gần hơn) thì không clamp.
    /// </summary>
    Vector3 ClampPositionToChest(Vector3 worldPos, Vector3 baseWorldPos)
    {
        if (upperChest == null || maxBoneDistanceFromChest <= 0f) return worldPos;

        Vector3 chestPos  = upperChest.position;
        Vector3 delta     = worldPos - chestPos;
        float   dist      = delta.magnitude;

        // Chỉ clamp khi đang bị đẩy xa hơn khỏi chest so với vị trí gốc Animator
        float baseDist = (baseWorldPos - chestPos).magnitude;
        if (dist > maxBoneDistanceFromChest && dist > baseDist)
            return chestPos + delta.normalized * maxBoneDistanceFromChest;

        return worldPos;
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

        // refT dùng chung cho các gizmo push/slide vector bên dưới
        Transform refT = collisionMode == CollisionMode.PivotMode ? collisionPivot : animatedBone;

        // Chest raycast (upperChest → tip) — lớp phát hiện thân người sát tường
        // Màu debug: XANH LÁ = raycast thực sự hit collider (đang hoạt động đúng).
        //            TÍM     = tia có bắn nhưng KHÔNG hit gì (kiểm tra obstacleLayer/trigger).
        //            XÁM     = tia không được bắn frame này (lớp trước đã bắt collision rồi).
        if (upperChest != null)
        {
            if (!debugChestRayEvaluated)      Gizmos.color = Color.gray;
            else if (debugChestRayHit)        Gizmos.color = Color.green;
            else                              Gizmos.color = Color.magenta;

            Gizmos.DrawLine(upperChest.position, tipPos);
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
    /// <summary>
    /// Bật/tắt toàn bộ tính năng resolver.
    /// Khi tắt: dừng xử lý va chạm ở LateUpdate và trả súng về trạng thái gốc ngay lập tức
    /// (tránh bị kẹt lại ở vị trí offset khi tắt giữa chừng lúc đang va chạm).
    /// Khi bật lại: resolver hoạt động bình thường từ frame kế tiếp.
    /// </summary>
    public void SetState(bool on)
    {
        isEnabled = on;

        if (!on)
        {
            ResetToOriginal();
        }
    }

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
        _hasPrevTipPos          = false; // reset swept-test sau khi teleport về gốc
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