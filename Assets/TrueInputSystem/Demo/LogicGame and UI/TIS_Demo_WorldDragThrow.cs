using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: GetPointerWorldPosition() — Kéo &amp; Vứt GameObject trong World 3D
/// Platform: PC / Mobile
/// ──────────────────────────────────────────────
/// Tap vào object để bắt đầu kéo. Giữ ngón tay / chuột để di chuyển.
/// Thả ra → object rơi theo vật lý (nếu có Rigidbody) hoặc đứng yên tại chỗ.
/// Vứt mạnh = nhả khi đang kéo nhanh → áp dụng velocity vào Rigidbody.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_WorldDragThrow))]
public class TIS_Demo_WorldDragThrow_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: PC · Mobile\n\n" +
            "SETUP\n" +
            "1. Gắn script này vào GameObject có Collider (Box, Sphere, Mesh...).\n" +
            "2. Thêm Rigidbody nếu muốn object rơi / bị vứt sau khi thả.\n" +
            "3. Bật 'Enable Object Tracker' trên TISInputBridge (Inspector).\n" +
            "4. Gắn Main Camera vào TISInputBridge > Raycast Camera.\n\n" +
            "API\n" +
            "  bridge.IsPointerHeld                    → bool\n" +
            "  bridge.GetPointerWorldPosition(depth)   → Vector3\n" +
            "  bridge.ToucherIsSingleTouch()           → bool (phát hiện tap)\n\n" +
            "GHI CHÚ\n" +
            "  depth = khoảng cách từ Camera đến object lúc bắt đầu kéo.\n" +
            "  Giữ nguyên depth trong suốt drag để object không bay xa hay gần.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/World Drag Throw")]
public class TIS_Demo_WorldDragThrow : MonoBehaviour
{
    [Header("Drag Settings")]
    [Tooltip("Tốc độ object theo pointer (1 = tức thì, <1 = lag mượt)")]
    public float followSpeed = 12f;

    [Tooltip("Nhân thêm khi vứt — velocity = delta * throwMultiplier")]
    public float throwMultiplier = 8f;

    [Header("Debug (ReadOnly)")]
    [ReadOnly] public bool    isDragging;
    [ReadOnly] public float   dragDepth;
    [ReadOnly] public Vector3 velocity;

    private TISInputBridge _input;
    private Rigidbody      _rb;

    // Offset tâm object so với điểm chạm ban đầu (world space)
    private Vector3 _grabOffset;

    // Vị trí world frame trước để tính velocity khi vứt
    private Vector3 _prevWorldPos;

    private void Start()
    {
        _input = TISInputBridge.Instance;
        _rb    = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // ── Bắt đầu kéo: phát hiện tap trúng object này ─────
        if (!isDragging && _input.ToucherIsSingleTouch())
        {
            var tapped = _input.ToucherGetSingleObject();
            if (tapped == gameObject)
                BeginDrag();
        }

        // ── Đang kéo ─────────────────────────────────────────
        if (isDragging)
        {
            if (_input.IsPointerHeld)
            {
                DragUpdate();
            }
            else
            {
                // Pointer được nhả → vứt
                EndDrag();
            }
        }
    }

    // ── Bắt đầu kéo ──────────────────────────────────────────

    private void BeginDrag()
    {
        isDragging = true;

        // Tính depth = khoảng cách từ camera đến object lúc bắt đầu
        var cam = Camera.main;
        dragDepth  = cam != null
            ? Vector3.Distance(cam.transform.position, transform.position)
            : 10f;

        // Tính grab offset để object không nhảy về tâm pointer
        Vector3 pointerWorld = _input.GetPointerWorldPosition(dragDepth);
        _grabOffset  = transform.position - pointerWorld;
        _prevWorldPos = transform.position;

        // Tắt vật lý trong khi kéo
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity    = Vector3.zero;
        }
    }

    // ── Cập nhật vị trí khi đang kéo ─────────────────────────

    private void DragUpdate()
    {
        Vector3 target = _input.GetPointerWorldPosition(dragDepth) + _grabOffset;

        // Tính velocity để dùng lúc vứt
        velocity = (target - _prevWorldPos) / Time.deltaTime;
        _prevWorldPos = transform.position;

        // Di chuyển object theo pointer
        transform.position = followSpeed >= 20f
            ? target
            : Vector3.Lerp(transform.position, target, followSpeed * Time.deltaTime);
    }

    // ── Kết thúc kéo / vứt ───────────────────────────────────

    private void EndDrag()
    {
        isDragging = false;

        // Bật lại vật lý và áp velocity (ném object)
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity    = velocity * throwMultiplier;
        }

        velocity  = Vector3.zero;
        dragDepth = 0f;
    }
}
