using UnityEngine;
using TrueInputSystem;

/// <summary>
/// Demo: ToucherGetSingleObject()
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Lấy GameObject bị tap trúng trong frame này (single tap, không phải double-tap).
/// null nếu không tap hoặc không trúng object nào.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_GetSingleObject))]
public class TIS_Demo_GetSingleObject_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách lấy GameObject bị single-tap trúng trong frame\n" +
            "  hiện tại, kèm hệ thống highlight đơn giản: tap trúng object → đổi material,\n" +
            "  tap vào vùng trống → bỏ chọn và khôi phục material gốc.\n" +
            "  Dùng cho: chọn unit, di chuyển đến điểm, inspect object.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +

            "SETUP\n" +
            "1. Trong TISInputBridge Inspector:\n" +
            "   ✓ Enable Object Tracker = true\n" +
            "   ✓ Assign Camera (hoặc để trống → Camera.main)\n" +
            "   ✓ Layer Mask = layer chứa các object cần tap\n" +
            "2. Gán script này lên SelectionController / GameManager.\n" +
            "3. Assign 'Selected Material' để highlight object khi được chọn.\n" +
            "   Để trống → vẫn hoạt động nhưng không có hiệu ứng highlight.\n\n" +

            "LUỒNG HOẠT ĐỘNG\n" +
            "  1. Mỗi frame gọi ToucherGetSingleObject() → nhận GameObject hoặc null.\n" +
            "  2. Nếu có object → SelectObject(): lưu material gốc, đổi sang selectedMaterial.\n" +
            "  3. Nếu null + đang có object được chọn + có touch → DeselectObject(): khôi phục material.\n\n" +

            "API\n" +
            "  bridge.ToucherGetSingleObject()  →  GameObject (null nếu không trúng)\n" +
            "  bridge.ToucherIsSingleTouch()    →  bool (có tap vào màn hình không)\n\n" +

            "GHI CHÚ\n" +
            "  – Kết quả chỉ hợp lệ đúng 1 frame ngay sau khi tap — kiểm tra trong Update.\n" +
            "  – Khác ToucherGetLastID(): hàm này reset mỗi frame, không tích lũy.\n" +
            "  – Object phải có Collider để Raycast trúng.\n" +
            "  – Object phải có Renderer nếu muốn dùng tính năng highlight material.",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/GetSingleObject")]
public class TIS_Demo_GetSingleObject : MonoBehaviour
{
    [Header("Highlight khi chọn")]
    public Material selectedMaterial;
    private Material defaultMaterial;

    private TISInputBridge _input;
    private GameObject _selected;
    private Renderer _selectedRenderer;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        // ← API chính: object bị tap trúng frame này
        GameObject tapped = _input.ToucherGetSingleObject();
        defaultMaterial = tapped?.GetComponent<Renderer>().material;

        if (tapped != null)
        {
            Debug.Log($"[GetSingleObject] Tap trúng: '{tapped.name}'");
            SelectObject(tapped);
        }

        // Tap vào vùng trống → bỏ chọn
        if (_input.ToucherIsSingleTouch() && tapped == null && _selected != null)
            DeselectObject();
    }

    private void SelectObject(GameObject go)
    {
        DeselectObject();
        _selected = go;
        _selectedRenderer = go.GetComponent<Renderer>();
        if (_selectedRenderer != null && selectedMaterial != null)
            _selectedRenderer.material = selectedMaterial;
    }

    private void DeselectObject()
    {
        if (_selectedRenderer != null && defaultMaterial != null)
            _selectedRenderer.material = defaultMaterial;
        _selected = null;
        _selectedRenderer = null;
    }
}
