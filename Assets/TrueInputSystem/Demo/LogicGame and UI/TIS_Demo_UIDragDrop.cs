using UnityEngine;
using UnityEngine.UI;
using TrueInputSystem;

/// <summary>
/// Demo: GetPointerScreenPosition() — Drag & Drop item trong Canvas UI
/// Platform: PC / Mobile
/// ──────────────────────────────────────────────
/// Kéo một Image/RectTransform theo ngón tay hoặc chuột trên Canvas.
/// Thả ra thì trả về vị trí gốc.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_UIDragDrop))]
public class TIS_Demo_UIDragDrop_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "PLATFORM: PC · Mobile\n\n" +
            "SETUP\n" +
            "1. Tạo Canvas (Screen Space — Overlay).\n" +
            "2. Tạo Image bên trong Canvas, gắn script này vào Image đó.\n" +
            "3. Bật 'Enable Object Tracker' trên TISInputBridge (Inspector).\n" +
            "4. Gắn Camera vào field 'UI Camera' (để trống = Camera.main).\n\n" +
            "API\n" +
            "  bridge.IsPointerHeld               → bool  (đang giữ)\n" +
            "  bridge.GetPointerScreenPosition()  → Vector2 (pixel màn hình)\n" +
            "  bridge.GetPointerScreenDelta()     → Vector2 (delta frame)\n\n" +
            "GHI CHÚ\n" +
            "  Canvas phải dùng Screen Space Overlay.\n" +
            "  Với Screen Space Camera thì cần RectTransformUtility.ScreenPointToLocalPointInRectangle.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/UI Drag Drop")]
[RequireComponent(typeof(RectTransform))]
public class TIS_Demo_UIDragDrop : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tốc độ item theo pointer (1 = chính xác tuyệt đối, <1 = mượt hơn)")]
    public float followSpeed = 1f;

    [Tooltip("Trả về vị trí gốc khi thả ra")]
    public bool returnOnRelease = true;

    [Header("Debug (ReadOnly)")]
    [ReadOnly] public bool  isDragging;
    [ReadOnly] public Vector2 pointerPos;

    private RectTransform _rect;
    private Vector2       _originPos;
    private TISInputBridge _input;

    // Offset giữa tâm item và điểm bắt đầu chạm — để item không nhảy vị trí
    private Vector2 _grabOffset;

    private void Awake()
    {
        _rect      = GetComponent<RectTransform>();
        _originPos = _rect.anchoredPosition;
    }

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        pointerPos = _input.GetPointerScreenPosition();
        isDragging = _input.IsPointerHeld;

        if (isDragging)
        {
            // Lần đầu bắt đầu giữ → tính offset để item không nhảy
            if (_input.GetPointerScreenDelta() == Vector2.zero)
                _grabOffset = (Vector2)_rect.position - pointerPos;

            // Di chuyển item theo pointer (Screen Space Overlay: position = screen pixel)
            Vector2 target = pointerPos + _grabOffset;
            _rect.position = followSpeed >= 1f
                ? target
                : Vector2.Lerp(_rect.position, target, followSpeed * 30f * Time.deltaTime);
        }
        else if (returnOnRelease)
        {
            // Trả về vị trí gốc khi thả
            _rect.anchoredPosition = Vector2.Lerp(
                _rect.anchoredPosition, _originPos, 15f * Time.deltaTime);
        }
    }
}
