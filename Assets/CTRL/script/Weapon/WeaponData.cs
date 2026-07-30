using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapon Data", menuName = "Weapons/Weapon Data", order = 1)]
public class WeaponData : ScriptableObject
{
    [TabGroup("Weapon Data", "Info")]
    [SerializeField] private string _iD;
    [TabGroup("Weapon Data", "Info")]
    [Tooltip("Index là thứ tự của súng trong Animator controller, dùng để phân biệt giữa các loại súng sử dụng khác một số animation như Switch và Reload")]
    [SerializeField] private int _index;
    [TabGroup("Weapon Data", "Info")]
    [SerializeField] private string _name;
    [TabGroup("Weapon Data", "Info")]
    [Tooltip("Lượng đạn tối đa của súng sau khi thay đạn.")]
    [SerializeField] private int _defaultBullet = 9;
    [TabGroup("Weapon Data", "Info")]
    [Tooltip("lượng đạn tối đa có thể mang theo")]
    [SerializeField] private int _maxBullet = 27;
    [TabGroup("Weapon Data", "Info")]
    [Tooltip("Thời gian chờ (giây) giữa hai phát bắn.\n" +
             "Ví dụ: 0.5 → click nhanh cỡ nào cũng phải chờ 0.5s mới bắn được phát tiếp.")]
    [Min(0.01f)]
    [SerializeField] private float _fireDelay = 0.133f;
    
    [TabGroup("Weapon Data", "Bullet")]
    [Tooltip("Tầm bắn của đạn có thể bắn trúng kẻ định.(chiều dài của raycat)")]
    [SerializeField] private float _range = 100f;

    [TabGroup("Weapon Data", "Bullet")] 
    [SerializeField] private int _damage = 10;
    
    [TabGroup("Weapon Data", "ADS")]
    [Tooltip("mức độ zoom của camera khi không có ống ngắp. giá trị càng nhỏ zoom càng xa.")]
    [SerializeField] private float _aDS_Zoom = 35f;
    [TabGroup("Weapon Data", "ADS")]
    [Tooltip("góc nhìn mặc định của súng khi không ngắm bắn.")]
    [SerializeField] private float _stand_Zoom = 60f;

    [TabGroup("Weapon Data", "ADS")]
    [Tooltip("camera sẽ di chuyển trục y lên một khoản khi ngắm để tạo cảm giác ngắm tốt hơn.(điều chỉnh sao cho súng nằm gọn trong màn hình)")]
    [SerializeField] private float _aDS_Zoom_Z = 0.177f;
    [TabGroup("Weapon Data", "ADS")]
    [Tooltip("vị trí mặc định")]
    [SerializeField] private float _stamd_Zoom_Z = 0f;

    [TabGroup("Weapon Data", "ADS")]
    [Range(0,1f)]
    [SerializeField] private float _aDS_duration = 0.25f;

    // ========================================= AUDIO ========================================================
    [TabGroup("Sound", "sound")]
    [FoldoutGroup("Sound/sound/Fire")]
    [SerializeField] private AudioClip _fire_clip;
    [FoldoutGroup("Sound/sound/Fire")] 
    [Range(0.5f, 2f)]
    [SerializeField] private float _volume_fire = 1f;
    
    [FoldoutGroup("Sound/sound/Magazine")]
    [SerializeField] private AudioClip _magazine_in_clip;
    [FoldoutGroup("Sound/sound/Magazine")]
    [Range(0.5f, 2f)]
    [SerializeField] private float _volume_magazine = 1f;
    [FoldoutGroup("Sound/sound/Slide")]
    [SerializeField] private AudioClip _slide_clip;
    [FoldoutGroup("Sound/sound/Slide")]
    [Range(0.5f, 2f)]
    [SerializeField] private float _volume_slide = 1f;
    
    [FoldoutGroup("Sound/sound/Switch")]
    [SerializeField] private AudioClip _switch_clip;
    [FoldoutGroup("Sound/sound/Switch")]
    [Range(0.5f, 2f)]
    [SerializeField] private float _volume_switch = 1f;
    
    [FoldoutGroup("Sound/sound/Switch Mode")]
    [SerializeField] private AudioClip _switch_model_clip;
    [FoldoutGroup("Sound/sound/Switch Mode")]
    [Range(0.5f, 2f)]
    [SerializeField] private float _volume_switch_model = 1f;
    
    
    [Tooltip("Bộ tham số recoil (GunRecoilData) gắn với khẩu súng này.\n" +
             "GunRecoilController sẽ đọc trực tiếp từ đây thay vì được assign riêng lẻ,\n" +
             "đảm bảo mỗi WeaponData luôn đi kèm đúng 1 bộ recoil của chính nó.")]
    [SerializeField] private GunRecoilData _recoilData;

    [TabGroup("Weapon Data", "ADS")]
    [SerializeField] private float _ads_Pivot_Height = 0f;
    [TabGroup("Weapon Data", "ADS")]
    [SerializeField] private float _stand_Pivot_Height = 0f;

    /// <summary>True nếu WeaponData có đủ dữ liệu cần thiết để spawn/sử dụng.</summary>
    public bool HasRecoilData => _recoilData != null;
    
    public string ID => _iD;
    public int Index => _index;
    public string Name => _name;
    public int DefaultBullet => _defaultBullet;
    public int MaxBullet => _maxBullet;
    public float FireDelay => _fireDelay;
    public float ADS_Zoom => _aDS_Zoom;
    public float Stand_Zoom => _stand_Zoom;
    public float ADS_Zoom_Z => _aDS_Zoom_Z;
    public float Stamd_Zoom_Z => _stamd_Zoom_Z;
    public float ADS_duration => _aDS_duration;
    public float Range => _range;
    public int Damage => _damage;
    public GunRecoilData RecoilData => _recoilData;
    public float Ads_Pivot_Height => _ads_Pivot_Height;
    public float Stand_Pivot_Height => _stand_Pivot_Height;
    public AudioClip FireClip => _fire_clip;
    public float Volume_Fire => _volume_fire;
    
    public AudioClip MagazineInClip => _magazine_in_clip;
    public float Volume_Magazine => _volume_magazine;
    
    public AudioClip SlideClip => _slide_clip;
    public float Volume_Slide => _volume_slide;
    
    public AudioClip SwitchClip => _switch_clip;
    public float Volume_Switch => _volume_switch;
    
    public AudioClip Switch_ModelClip => _switch_model_clip;
    public float Volume_Switch_Model => _volume_switch_model;
}