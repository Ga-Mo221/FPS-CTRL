using TrueInputSystem;
using UnityEngine;

/// <summary>
/// WeaponController — chỉ đọc input và gọi GunRecoilController.
/// Mọi logic timing, cooldown, allowed mode đều nằm trong GunRecoilController + GunRecoilData.
/// </summary>
public class GunRecoilDemo : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Kéo GunRecoilController vào đây.")]
    [SerializeField] private GunRecoilController recoilController;

    [SerializeField] private WeaponController weaponController;
    
    private Animator _animator;

    [Header("Burst Settings")]
    [Tooltip("Số phát mặc định trong một loạt burst.")]
    [SerializeField] private int defaultBurstCount = 3;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private GunRecoilController.FiringMode _currentMode = GunRecoilController.FiringMode.Semi;
    private int _burstCount;
    private TISInputBridge _input;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (recoilController == null)
        {
            Debug.LogError("[GunRecoilDemo] Chưa assign GunRecoilController!", this);
            enabled = false;
            return;
        }
        
        _currentMode = recoilController.GetFiringMode();

        _burstCount = defaultBurstCount;
        recoilController.SetBurstCount(_burstCount);

        _input = TISInputBridge.Instance;
        Debug.Log("[GunRecoilDemo] Ready. " + GetStatusString());
    }

    private void Update()
    {
        HandleModeSwitch();
        HandleADS();
        //HandleFire();
        HandleSwitchWeapons();
    }

    // ─────────────────────────────────────────────
    //  INPUT HANDLERS
    // ─────────────────────────────────────────────

    private void HandleModeSwitch()
    {
        if (_input.GetSkillAction("Mode"))
        {
            weaponController.SwitchMode();
        }
    }

    private void HandleADS()
    {
        weaponController.ADS(_input.GetSkillAction("ADS"));
    }

    /// <summary>
    /// Chỉ đọc input và gọi OnFire().
    /// Semi/Burst dùng PressDown, Auto dùng Hold.
    /// GunRecoilController tự lo cooldown — gọi bao nhiêu lần cũng không spam được.
    /// </summary>
    // private void HandleFire()
    // {
    //     switch (_currentMode)
    //     {
    //         case GunRecoilController.FiringMode.Semi:
    //         case GunRecoilController.FiringMode.Burst:
    //             if (_input.GetSkillAction("Attack", TISButtonReadMode.PressDown))
    //                 weaponController.OnFire();
    //             break;
    //         case GunRecoilController.FiringMode.Auto:
    //             if (_input.GetSkillAction("Attack", TISButtonReadMode.Hold))
    //                 weaponController.OnFire();
    //             break;
    //     }
    // }

    private void HandleSwitchWeapons()
    {
        if (_input.ZoomDelta != 0)
            weaponController.IsSwitching(true);
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    private string GetStatusString() =>
        $"Mode: {_currentMode} | BurstCount: {_burstCount}\n" +
        "Phím: [LMB/Space]=Bắn  [R]=ADS  [1]=Semi  [2]=Auto  [3]=Burst  [4/5]=BurstCount±  [Tab]=Status";
}