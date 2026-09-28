using UnityEngine;
using GaMo.TrueInputSystem;

/// <summary>
/// Demo: ToucherGetLastID()
/// Platform: Mobile (iOS / Android)
/// ──────────────────────────────────────────────
/// Lấy InstanceID của GameObject bị touch trúng gần nhất.
/// Dùng để xác định "vật thể nào người chơi vừa chạm vào".
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TIS_Demo_ToucherGetLastID))]
public class TIS_Demo_ToucherGetLastID_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        UnityEditor.EditorGUILayout.HelpBox(
            "MỤC ĐÍCH\n" +
            "  Script demo minh họa cách lấy InstanceID của GameObject bị touch\n" +
            "  trúng gần nhất thông qua API ToucherGetLastID(). Dùng để xác định\n" +
            "  chính xác vật thể nào người chơi vừa chạm vào, phục vụ các logic\n" +
            "  như chọn object, highlight, hoặc trigger sự kiện theo target.\n\n" +

            "PLATFORM: Mobile (iOS / Android)\n\n" +
            "SETUP\n" +
            "1. Trong TISInputBridge Inspector → bật 'Enable Object Tracker'.\n" +
            "2. Assign Camera vào 'Raycast Camera' (hoặc để trống → Camera.main).\n" +
            "3. Các GameObject cần track phải có Collider + layer khớp LayerMask.\n" +
            "4. Gán script này lên GameManager hoặc SelectionController.\n\n" +
            "API\n" +
            "  bridge.ToucherGetLastID()    →  int  InstanceID (0 nếu chưa có)\n" +
            "  bridge.ToucherGetLastName()  →  string tên object (debug)\n\n" +
            "GHI CHÚ\n" +
            "  Dùng FindObjectsOfType + GetInstanceID() để map ID → GameObject,\n" +
            "  hoặc giữ Dictionary<int, GameObject> để lookup nhanh O(1).",
            UnityEditor.MessageType.Info);
        base.OnInspectorGUI();
    }
}
#endif

[AddComponentMenu("TIS/Demo/ToucherGetLastID")]
public class TIS_Demo_ToucherGetLastID : MonoBehaviour
{
    private TISInputBridge _input;
    private int _lastKnownID;

    private void Start() => _input = TISInputBridge.Instance;

    private void Update()
    {
        int id = _input.ToucherGetLastID();   // ← API chính

        if (id != 0 && id != _lastKnownID)
        {
            _lastKnownID = id;
            string name = _input.ToucherGetLastName();
            Debug.Log($"[ToucherGetLastID] InstanceID={id}  Name='{name}'");

            // Tìm object từ InstanceID
            GameObject found = FindByInstanceID(id);
            if (found != null)
                Debug.Log($"  → Tìm thấy: {found.name} tại {found.transform.position}");
        }
    }

    private static GameObject FindByInstanceID(int instanceID)
    {
        // Sử dụng FindObjectsByType với SortMode.None để đạt tốc độ cao nhất
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.GetInstanceID() == instanceID) return go;
        }
        return null;
    }
}