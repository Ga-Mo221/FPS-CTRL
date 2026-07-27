// ============================================================
//  TrueInputSystem — TISCode_PointerPosition.cs
//  Code Sub-Tab Module: Pointer Pos
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public class TISCode_PointerPosition : ICodeSubTabModule
    {
        public string Label => "Pointer Pos";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Pointer Position (Drag)",
                "GetPointerScreenPosition, GetPointerScreenDelta, IsPointerHeld, GetPointerWorldPosition — drag & drop UI và World Space.");

            win.Body("4 API này hoạt động khi <b>Enable Object Tracker</b> được bật trong Inspector của TISInputBridge. " +
                     "Dùng <b>IsPointerHeld</b> làm guard trước khi đọc vị trí để tránh overhead khi không drag.");

            GUILayout.Space(8);
            win.H2("IsPointerHeld — Guard Trước Khi Drag");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!TISInputBridge.Instance.IsPointerHeld) return;\n\n" +
                "    Vector2 screenPos = TISInputBridge.Instance.GetPointerScreenPosition();\n" +
                "    Debug.Log($\"Pointer tại: {screenPos}\");\n" +
                "}");

            win.Sep();
            win.H2("API 1 — GetPointerScreenPosition (UI Drag)");
            win.Body("Vị trí screen space (pixel) của pointer — dùng để kéo RectTransform trong Canvas UI:");
            win.CodeBlock(
                "public class DragItem : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n" +
                "    private RectTransform  _rt;\n" +
                "    private Canvas         _canvas;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _input  = TISInputBridge.Instance;\n" +
                "        _rt     = GetComponent<RectTransform>();\n" +
                "        _canvas = GetComponentInParent<Canvas>();\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        if (!_input.IsPointerHeld) return;\n\n" +
                "        RectTransformUtility.ScreenPointToLocalPointInRectangle(\n" +
                "            _canvas.transform as RectTransform,\n" +
                "            _input.GetPointerScreenPosition(),\n" +
                "            _canvas.worldCamera,\n" +
                "            out Vector2 localPos\n" +
                "        );\n" +
                "        _rt.localPosition = localPos;\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("GetPointerScreenDelta — Kéo Bằng Delta");
            win.Body("Delta pixel so với frame trước — lý tưởng cho drag offset mà không cần cache vị trí:");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.IsPointerHeld) return;\n\n" +
                "    Vector2 delta = _input.GetPointerScreenDelta();\n" +
                "    _rt.anchoredPosition += delta;\n\n" +
                "    // Hoặc scroll một panel\n" +
                "    _scrollRect.content.anchoredPosition += delta;\n" +
                "}");

            win.Note("GetPointerScreenDelta tự reset về Vector2.zero khi không có pointer active — không cần if check thêm sau IsPointerHeld.");

            win.Sep();
            win.H2("API 2 — GetPointerWorldPosition (World Space Drag)");
            win.Body("Kéo thả GameObject trong không gian 3D. <b>depth</b> = khoảng cách từ Camera đến mặt phẳng đặt object:");
            win.CodeBlock(
                "public class DragObject3D : MonoBehaviour\n" +
                "{\n" +
                "    private TISInputBridge _input;\n" +
                "    private float          _depth;\n\n" +
                "    void Start() => _input = TISInputBridge.Instance;\n\n" +
                "    void OnMouseDown()\n" +
                "    {\n" +
                "        _depth = Vector3.Distance(Camera.main.transform.position, transform.position);\n" +
                "    }\n\n" +
                "    void Update()\n" +
                "    {\n" +
                "        if (!_input.IsPointerHeld) return;\n" +
                "        transform.position = _input.GetPointerWorldPosition(_depth);\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Game 2D — Orthographic Camera");
            win.CodeBlock(
                "void Update()\n" +
                "{\n" +
                "    if (!_input.IsPointerHeld) return;\n\n" +
                "    float depth = Mathf.Abs(Camera.main.transform.position.z);\n" +
                "    Vector3 worldPos = _input.GetPointerWorldPosition(depth);\n" +
                "    worldPos.z = transform.position.z;\n" +
                "    transform.position = worldPos;\n" +
                "}");

            win.Note("Toàn bộ Pointer API trả về zero (Vector2.zero / Vector3.zero) khi không có pointer active. " +
                     "Luôn kiểm tra IsPointerHeld trước để tránh snap đột ngột về origin.");
        }
    }
}
#endif
