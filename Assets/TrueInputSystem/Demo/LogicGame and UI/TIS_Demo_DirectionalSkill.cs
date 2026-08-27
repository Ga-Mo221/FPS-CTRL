using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: DirectionalSkill
/// ──────────────────────────────────────────────
/// Spawn projectile bay theo hướng người chơi đã kéo chọn.
/// Đọc input qua TISInputBridge.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_DirectionalSkill))]
public class TIS_Demo_DirectionalSkill_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách dùng TIS API để thực hiện kỹ năng có hướng\n" +
            "  (directional skill): người chơi kéo joystick/swipe để chọn hướng,\n" +
            "  khi thả tay sẽ spawn projectile bay theo hướng đó trên mặt phẳng XZ.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Tạo Prefab projectile (Rigidbody + Collider), gán vào 'Projectile Prefab'.\n" +
            "2. 'Launch Point' là Transform spawn projectile. Để trống = transform của script.\n" +
            "3. Điền 'Skill Name' khớp với tên action Button trong TIS profile.\n" +
            "4. Điền 'Direction Action Name' khớp với tên action Vector2 trong TIS profile.\n" +
            "5. Đảm bảo scene có TISInputBridge.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Người chơi giữ và kéo để chọn hướng (Vector2 từ directionActionName).\n" +
            "  2. Khi thả (IsSkillReleased) → đọc hướng đã kéo → spawn projectile.\n" +
            "  3. Projectile bay thẳng theo worldDir với tốc độ projectileSpeed.\n" +
            "  4. Tự destroy sau projectileLifetime giây.\n\n" +

            "API\n" +
            "  bridge.IsSkillReleased(skillName)        →  bool (kích hoạt khi thả)\n" +
            "  bridge.GetVector2Action(directionAction)  →  Vector2 (hướng kéo)\n\n" +

            "GHI CHÚ\n" +
            "  – CastDirection = Vector2.zero khi người chơi tap nhanh → bỏ qua, không cast.\n" +
            "  – Projectile bay trên mặt phẳng XZ; Y của hướng UI được map sang Z world.\n" +
            "  – spawnOffset giúp tránh self-hit khi projectile vừa được spawn.\n" +
            "  – Bật 'Draw Debug Ray' để thấy hướng cast trong Scene view lúc play.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/DirectionalSkill")]
public class TIS_Demo_DirectionalSkill : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  INSPECTOR — REFERENCES
    // ══════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("Transform điểm spawn projectile (đầu nòng, tay nhân vật, v.v.).\n" +
             "Để trống → dùng transform của GameObject này.")]
    public Transform launchPoint;

    [Tooltip("Prefab projectile cần có Rigidbody. Để trống → chỉ log, không spawn.")]
    public GameObject projectilePrefab;

    // ══════════════════════════════════════════════════════
    //  INSPECTOR — SKILL SETTINGS
    // ══════════════════════════════════════════════════════

    [Header("Skill Settings")]
    [Tooltip("Tên action Button trong profile (GetSkillAction).")]
    public string skillName = "Skill1";

    [Tooltip("Tên action Vector2 trong profile.\nVí dụ: 'AimDirection'")]
    public string directionActionName = "AimDirection";

    [Tooltip("Tốc độ bay của projectile (unit/s).")]
    public float projectileSpeed = 15f;

    [Tooltip("Bán kính spawn offset tính từ launchPoint theo hướng cast (tránh self-hit).")]
    [Range(0f, 2f)]
    public float spawnOffset = 0.5f;

    [Tooltip("Thời gian sống của projectile trước khi tự destroy (giây).")]
    [Range(0.5f, 10f)]
    public float projectileLifetime = 3f;

    // ══════════════════════════════════════════════════════
    //  INSPECTOR — DEBUG
    // ══════════════════════════════════════════════════════

    [Header("Debug")]
    [Tooltip("Bật → vẽ ray hướng cast trong Scene view mỗi khi có cast.")]
    public bool drawDebugRay = true;

    // ══════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ══════════════════════════════════════════════════════

    private Transform       _launch;
    private ITISInputBridge _bridge;

    // ══════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ══════════════════════════════════════════════════════

    private void Start()
    {
        _launch = launchPoint != null ? launchPoint : transform;
        _bridge = TISInputBridge.Instance;

        if (_bridge == null)
            Debug.LogWarning("[DirectionalSkill] TISInputBridge.Instance là null.", this);
        if (string.IsNullOrEmpty(directionActionName))
            Debug.LogWarning("[DirectionalSkill] 'Direction Action Name' chưa được điền.", this);
    }

    private void Update()
    {
        if (_bridge == null) return;

        bool    wasCast  = _bridge.IsSkillReleased(skillName);
        Vector2 castDir2 = _bridge.GetVector2Action(directionActionName);
        //Debug.Log(castDir2);
        if (wasCast)
            HandleCast(castDir2);
    }

    // ══════════════════════════════════════════════════════
    //  CORE LOGIC
    // ══════════════════════════════════════════════════════

    private void HandleCast(Vector2 castDir2D)
    {
        Vector3 dir = castDir2D;
        // Quick tap (direction = zero) → bỏ qua
        if (castDir2D.sqrMagnitude < 0.001f)
        {
            Debug.Log("[DirectionalSkill] Quick tap — không có hướng, bỏ qua.");
            dir = launchPoint.forward;
        }

        // UI: x = phải/trái, y = lên/xuống → World: x = phải, z = lên (top-down)
        Vector3 worldDir = new Vector3(dir.x, 0f, dir.y).normalized;

        if (drawDebugRay)
            Debug.DrawRay(_launch.position, worldDir * 5f, Color.cyan, 1.5f);

        Debug.Log($"[DirectionalSkill] CAST | dir2D={dir}  worldDir={worldDir}  " +
                  $"angle={Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg:F1}°");

        SpawnProjectile(worldDir);
    }

    private void SpawnProjectile(Vector3 worldDir)
    {
        if (projectilePrefab == null)
        {
            Debug.Log("[DirectionalSkill] Projectile Prefab chưa gán — chỉ log, không spawn.");
            return;
        }

        Vector3 spawnPos = _launch.position + worldDir * spawnOffset;
        var     go       = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(worldDir));

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity     = false;
            rb.linearVelocity = worldDir * projectileSpeed;
        }

        Destroy(go, projectileLifetime);
    }
}