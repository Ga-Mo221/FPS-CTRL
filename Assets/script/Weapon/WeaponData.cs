using UnityEngine;

[CreateAssetMenu(fileName = "Weapon Data", menuName = "Weapons/Weapon Data", order = 1)]
public class WeaponData : ScriptableObject
{
    [Header("Weapon Info")]
    [SerializeField] private string _iD;
    [SerializeField] private int _index;
    [SerializeField] private string _name;

    [Header("ADS")]
    [SerializeField] private float _aDS_Zoom = 90f;
    [SerializeField] private float _stand_Zoom = 60f;

    [SerializeField] private float _aDS_Zoom_Y = 0.177f;
    [SerializeField] private float _stamd_Zoom_Y = 0f;

    [Range(0,1f)]
    [SerializeField] private float _aDS_duration = 0.25f;

    [Header("Recoil")]
    [Tooltip("Bộ tham số recoil (GunRecoilData) gắn với khẩu súng này.\n" +
             "GunRecoilController sẽ đọc trực tiếp từ đây thay vì được assign riêng lẻ,\n" +
             "đảm bảo mỗi WeaponData luôn đi kèm đúng 1 bộ recoil của chính nó.")]
    [SerializeField] private GunRecoilData _recoilData;

    /// <summary>True nếu WeaponData có đủ dữ liệu cần thiết để spawn/sử dụng.</summary>
    public bool HasRecoilData => _recoilData != null;
    
    public string ID => _iD;
    public int Index => _index;
    public string Name => _name;    
    public float ADS_Zoom => _aDS_Zoom;
    public float Stand_Zoom => _stand_Zoom;
    public float ADS_Zoom_Y => _aDS_Zoom_Y;
    public float Stamd_Zoom_Y => _stamd_Zoom_Y;
    public float ADS_duration => _aDS_duration;
    public GunRecoilData RecoilData => _recoilData;
}