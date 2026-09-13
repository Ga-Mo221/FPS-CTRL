using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapon Data", menuName = "Weapons/Weapon Data", order = 1)]
public class WeaponData : ScriptableObject
{
    //========================== INFO ====================================
    [TabGroup("Weapon Data", "Info")]
    [HorizontalGroup("Weapon Data/Info/Info", width: 0.2f)]
    [PreviewField(50, ObjectFieldAlignment.Left)]
    [HideLabel]
    [SerializeField] private Sprite _icon;
    
    [HorizontalGroup("Weapon Data/Info/Info")]
    [VerticalGroup("Weapon Data/Info/Info/Right")]
    [Tooltip("dùng để switch weapon giữa có Constraint.")]
    [SerializeField] private string _iD;
    [VerticalGroup("Weapon Data/Info/Info/Right")]
    [Tooltip("Index là thứ tự của súng trong Animator controller, dùng để phân biệt giữa các loại súng sử dụng khác một số animation như Switch và Reload")]
    [SerializeField] private int _index;
    [VerticalGroup("Weapon Data/Info/Info/Right")]
    [SerializeField] private string _name;
    
    [TabGroup("Weapon Data", "Info")]
    [Tooltip("Bộ tham số recoil (GunRecoilData) gắn với khẩu súng này.\n" +
             "GunRecoilController sẽ đọc trực tiếp từ đây thay vì được assign riêng lẻ,\n" +
             "đảm bảo mỗi WeaponData luôn đi kèm đúng 1 bộ recoil của chính nó.")]
    [SerializeField] private GunRecoilData _recoilData;

    [TabGroup("Weapon Data", "Info")] 
    [SerializeField] private GameObject _dropItemPrefab;
    
    //========================== ATTACK/ALL ====================================
    
    [TabGroup("Weapon Data", "Attack")] 
    [TabGroup("Weapon Data/Attack/SubGroup", "All")]
    [SerializeField] private int _damage = 10;
    
    [TabGroup("Weapon Data/Attack/SubGroup", "All")]
    [Tooltip("Tầm bắn của đạn có thể bắn trúng kẻ định.(chiều dài của raycat)")]
    [SerializeField] private float _range = 100f;
    
    [TabGroup("Weapon Data/Attack/SubGroup", "All")]
    [Tooltip("Thời gian chờ (giây) giữa hai phát bắn.\n" +
             "Ví dụ: 0.5 → click nhanh cỡ nào cũng phải chờ 0.5s mới bắn được phát tiếp.")]
    [Min(0.01f)]
    [SerializeField] private float _fireDelay = 0.133f;
    
    //========================== ATTACK/WEAPON ====================================
    //========================== ATTACK/WEAPON/GUN ====================================
    
    [TabGroup("Weapon Data/Attack/SubGroup", "Weapon")]
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Header("Bullet")]
    [Tooltip("Lượng đạn tối đa của súng sau khi thay đạn.")]
    [SerializeField] private byte _defaultBullet = 9;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")] 
    [Tooltip("số đạn được nạp mỗi lần")]
    [SerializeField] private int _bulletPlusCount = 9;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Tooltip("lượng đạn tối đa có thể mang theo")]
    [SerializeField] private byte _maxBullet = 27;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Space]
    [Header("ADS")]
    [Tooltip("mức độ zoom của camera khi không có ống ngắp. giá trị càng nhỏ zoom càng xa.")]
    [SerializeField] private float _aDS_Zoom = 35f;
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Tooltip("góc nhìn mặc định của súng khi không ngắm bắn.")]
    [SerializeField] private float _stand_Zoom = 60f;

    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Tooltip("camera sẽ di chuyển trục y lên một khoản khi ngắm để tạo cảm giác ngắm tốt hơn.(điều chỉnh sao cho súng nằm gọn trong màn hình)")]
    [SerializeField] private float _aDS_Zoom_Z = 0.177f;
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Tooltip("vị trí mặc định")]
    [SerializeField] private float _stamd_Zoom_Z = 0f;

    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [Range(0,1f)]
    [SerializeField] private float _aDS_duration = 0.25f;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [SerializeField] private float _ads_Pivot_Height = 0f;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Gun")]
    [SerializeField] private float _stand_Pivot_Height = 0f;
    
    //========================== ATTACK/WEAPON/MELEE ====================================
    
    [Header("ALL")]
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [SerializeField] private int _rightDamage = 20;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [SerializeField] private float _meeleRadius = 0.6f;
    
    [Header("Default Punch Delay")]
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [SerializeField] private float _meleeHitDelay = 0.2f;
    
    [Header("Right Stander Punch Delay")]
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [Tooltip("Thời gian kết thúc animation khi đứng của skill chuột phải.")]
    [Min(0.01f)]
    [SerializeField] private float _fireStandRightDelay = 3f;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [Tooltip("Thời gian kích hoạt trigger khi ngồi của skill chuột phải.")]
    [Min(0.01f)]
    [SerializeField] private float _hitRightStandDelay = 3f;
    
    [Header("Right Crouch Punch Delay")]
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [Tooltip("Thời gian kết thúc animation khi ngồi của skill chuột phải.")]
    [Min(0.01f)]
    [SerializeField] private float _fireCroundRightDelay = 3f;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "Melee")]
    [Tooltip("Thời gian kích hoạt trigger khi đúng của skill chuột phải.")]
    [Min(0.01f)]
    [SerializeField] private float _hitRightCrouchDelay = 3f;
    
    //========================== ATTACK/WEAPON/SHOTGUN ====================================
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "ShotGun")]
    [SerializeField] private int _pelletCount = 7;
    
    [TabGroup("Weapon Data/Attack/SubGroup/Weapon/SubGroup", "ShotGun")]
    [SerializeField] private int _spreadAngle = 5;

    // ========================================= AUDIO ========================================================
    [TabGroup("Sound", "sound")]
    [FoldoutGroup("Sound/sound/Fire")]
    [SerializeField] private AudioClip[] _fire_clip;
    [FoldoutGroup("Sound/sound/Fire")] 
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_fire = 1f;
    
    [FoldoutGroup("Sound/sound/Fire Failed")]
    [SerializeField] private AudioClip _fire_failed_clip;
    [FoldoutGroup("Sound/sound/Fire Failed")] 
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_fire_failed = 1f;
    
    [FoldoutGroup("Sound/sound/Magazine")]
    [SerializeField] private AudioClip[] _magazine_in_clip;
    [FoldoutGroup("Sound/sound/Magazine")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_magazine = 1f;
    
    [FoldoutGroup("Sound/sound/Slide")]
    [SerializeField] private AudioClip[] _slide_clip;
    [FoldoutGroup("Sound/sound/Slide")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_slide = 1f;
    
    [FoldoutGroup("Sound/sound/Switch")]
    [SerializeField] private AudioClip _switch_clip;
    [FoldoutGroup("Sound/sound/Switch")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_switch = 1f;
    
    [FoldoutGroup("Sound/sound/Switch Mode")]
    [SerializeField] private AudioClip _switch_model_clip;
    [FoldoutGroup("Sound/sound/Switch Mode")]
    [Range(0.05f, 2f)]
    [SerializeField] private float _volume_switch_model = 1f;

    /// <summary>True nếu WeaponData có đủ dữ liệu cần thiết để spawn/sử dụng.</summary>
    public bool HasRecoilData => _recoilData != null;
    
    public GameObject DropItemPrefab => _dropItemPrefab;
    
    public Sprite Icon => _icon;
    public string ID => _iD;
    public int Index => _index;
    public string WeaponName => _name;
    public byte DefaultBullet => _defaultBullet;
    public byte MaxBullet => _maxBullet;
    public float FireDelay => _fireDelay;
    public float FireStandRightDelay => _fireStandRightDelay;
    public float FireCrouchRightDelay => _fireCroundRightDelay;
    public float ADS_Zoom => _aDS_Zoom;
    public float Stand_Zoom => _stand_Zoom;
    public float ADS_Zoom_Z => _aDS_Zoom_Z;
    public float Stamd_Zoom_Z => _stamd_Zoom_Z;
    public float ADS_duration => _aDS_duration;
    public float Range => _range;
    public float MeleeRadius => _meeleRadius;
    public float MeleeHitDelay => _meleeHitDelay;
    public float HitStandRightDelay => _hitRightStandDelay;
    public float HitCrouchRightDelay => _hitRightCrouchDelay;
    public int Damage => _damage;
    public int RightDamage => _rightDamage;
    public int PelletCount => _pelletCount;
    public int SpreadAngle => _spreadAngle;
    public int BulletPlusCount => _bulletPlusCount;
    public GunRecoilData RecoilData => _recoilData;
    public float Ads_Pivot_Height => _ads_Pivot_Height;
    public float Stand_Pivot_Height => _stand_Pivot_Height;
    
    public AudioClip[] FireClip => _fire_clip;
    public float Volume_Fire => _volume_fire;
    
    public AudioClip FireFailedClip => _fire_failed_clip;
    public float Volume_Fire_Failed => _volume_fire_failed;
    
    public AudioClip[] MagazineInClip => _magazine_in_clip;
    public float Volume_Magazine => _volume_magazine;
    
    public AudioClip[] SlideClip => _slide_clip;
    public float Volume_Slide => _volume_slide;
    
    public AudioClip SwitchClip => _switch_clip;
    public float Volume_Switch => _volume_switch;
    
    public AudioClip Switch_ModelClip => _switch_model_clip;
    public float Volume_Switch_Model => _volume_switch_model;
}