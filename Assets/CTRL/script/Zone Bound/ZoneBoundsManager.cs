using UnityEngine;

/// <summary>
/// Singleton quản lý bounds khu vực hoạt động cho map hiện tại.
/// 
/// Cách dùng:
///   1. Tạo 1 GameObject trong scene, đặt tên "ZoneBoundsManager".
///   2. Gắn component này vào, kéo MapBoundsData của map đó vào slot _boundsData.
///   3. Mỗi map có scene riêng → mỗi scene có 1 ZoneBoundsManager với data riêng.
///   4. Player prefab chỉ cần query ZoneBoundsManager.Instance, không cần biết map nào.
/// </summary>
public class ZoneBoundsManager : MonoBehaviour
{
    public static ZoneBoundsManager Instance { get; private set; }

    [Tooltip("ScriptableObject chứa bounds của map này.")]
    [SerializeField] private MapBoundsData _boundsData;
    
    // Chỉ OnDrawGizmos (#if UNITY_EDITOR) đọc → bản build báo CS0414. Giữ field để layout serialize giống Editor.
#pragma warning disable CS0414
    [SerializeField] private bool Draw = false;
#pragma warning restore CS0414

    /// <summary>
    /// Trả về bounds data của map hiện tại.
    /// Null nếu scene chưa setup (không crash — NOutOfBoundsHandler tự bỏ qua).
    /// </summary>
    public MapBoundsData BoundsData => _boundsData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ZoneBoundsManager] Duplicate detected, destroying.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_boundsData == null)
            Debug.LogWarning("[ZoneBoundsManager] BoundsData chưa được gán! Zone damage sẽ bị tắt.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Draw) return;
        if (_boundsData == null) return;

        // Fill xanh mờ
        Gizmos.color = new Color(0f, 1f, 0f, 0.08f);
        Gizmos.DrawCube(_boundsData.Center, _boundsData.Size);

        // Viền xanh rõ
        Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
        Gizmos.DrawWireCube(_boundsData.Center, _boundsData.Size);

        // Label tên map ở giữa
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.Label(
            _boundsData.Center + Vector3.up * (_boundsData.Size.y * 0.5f + 2f),
            $"[Zone] {_boundsData.name}\n" +
            $"X: {_boundsData.MinX} → {_boundsData.MaxX}\n" +
            $"Z: {_boundsData.MinZ} → {_boundsData.MaxZ}\n" +
            $"Y: {_boundsData.MinY} → {_boundsData.MaxY}"
        );
    }
#endif
}
