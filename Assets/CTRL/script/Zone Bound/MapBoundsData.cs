using UnityEngine;

/// <summary>
/// ScriptableObject lưu giới hạn khu vực hoạt động cho từng map.
/// Tạo asset: chuột phải trong Project → Create → Game/Map Bounds Data
/// Mỗi map tạo 1 file riêng và kéo vào ZoneBoundsManager trong scene.
/// </summary>
[CreateAssetMenu(fileName = "MapBoundsData", menuName = "FPS/MAP/Map Bounds Data")]
public class MapBoundsData : ScriptableObject
{
    [Header("Giới hạn 4 hướng ngang (World Space)")]
    [Tooltip("Giới hạn X tối thiểu (Tây)")]
    public float MinX = -100f;
    [Tooltip("Giới hạn X tối đa (Đông)")]
    public float MaxX =  100f;
    [Tooltip("Giới hạn Z tối thiểu (Nam)")]
    public float MinZ = -100f;
    [Tooltip("Giới hạn Z tối đa (Bắc)")]
    public float MaxZ =  100f;

    [Header("Giới hạn độ cao")]
    [Tooltip("Độ cao tối thiểu — rơi xuống vực")]
    public float MinY = -20f;
    [Tooltip("Độ cao tối đa — trần bầu trời")]
    public float MaxY =  80f;

    [Header("Damage Settings")]
    [Tooltip("Tổng thời gian sống sau khi ra ngoài vùng (giây)")]
    public float TimeToKill = 5f;
    [Tooltip("Khoảng cách giữa mỗi lần tick damage (giây)")]
    public float DamageTickInterval = 0.5f;

    /// <summary>
    /// Kiểm tra nhanh xem position có nằm trong bounds không.
    /// </summary>
    public bool IsInsideBounds(Vector3 pos)
    {
        return pos.x >= MinX && pos.x <= MaxX
            && pos.z >= MinZ && pos.z <= MaxZ
            && pos.y >= MinY && pos.y <= MaxY;
    }

    /// <summary>
    /// Tâm của box bounds, dùng để vẽ Gizmo.
    /// </summary>
    public Vector3 Center => new Vector3(
        (MinX + MaxX) * 0.5f,
        (MinY + MaxY) * 0.5f,
        (MinZ + MaxZ) * 0.5f
    );

    /// <summary>
    /// Kích thước của box bounds, dùng để vẽ Gizmo.
    /// </summary>
    public Vector3 Size => new Vector3(
        MaxX - MinX,
        MaxY - MinY,
        MaxZ - MinZ
    );
}
