using UnityEngine;

public class PlayerMotionController : MonoBehaviour
{
    // [NET-OPT 2026-10-18 Aemy] (bao cao 9.8) Weapon_Active() bi goi ~10 lan/frame cho MOI player (ca proxy)
    // -> cache theo frame (tu het han moi frame nen doi sung van dung ngay frame sau).
    private WeaponController _activeCache;
    private int _activeCacheFrame = -1;
    private WeaponController ActiveCached()
    {
        // het han khi sang frame moi, hoac sung dang cache vua bi tat/despawn (doi sung giua frame)
        if (_activeCacheFrame != Time.frameCount || _activeCache == null || !_activeCache.gameObject.activeSelf)
        {
            _activeCache = _w_manager != null ? _w_manager.Weapon_Active() : null;
            _activeCacheFrame = Time.frameCount;
        }
        return _activeCache;
    }

    private CharComponentManager _componentManager;
    private Animator _anim;
    private WeaponManager _w_manager;

    private void Awake()
    {
        _componentManager = GetComponent<CharComponentManager>();
        if (_componentManager == null)  return;
        _anim = _componentManager.Animator;
        _w_manager = _componentManager.WeaponManager;
    }

    public void IsDead(bool value)
    {
        _anim.SetBool("IsDead", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsDead(value);
    }
    
    public void IsCrouching(bool value)
    {
        _anim.SetBool("IsCrouching", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsCrouching(value);
    }
    
    public void IsSliding(bool value)
    {
        _anim.SetBool("IsSliding", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsSliding(value);
    }
    
    public void ADS(bool value)
    {
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
         if (controller.ADS(value))
             _anim.SetBool("ADS", value);
    }
    
    public bool IsReloading(bool value)
    {
        if (_w_manager == null) return false;
        WeaponController controller = ActiveCached();
        if (controller == null) return false;
        
        if (controller.IsReloading(value))
        {
            _anim.SetBool("IsReloading", value);
            return true;
        }
        return false;
    }
    
    public void IsThrow(bool value)
    {
        _anim.SetBool("IsThrow", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsThrow(value);
    }
    
    public void IsSwitching(bool value)
    {
        _anim.SetBool("IsSwitching", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsSwitching(value);
    }
    
    public void IsGrouded(bool value)
    {
        _anim.SetBool("IsGrouded", value);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsGrouded(value);
    }
    
    public void IsJumping()
    {
        _anim.SetTrigger("IsJumping");
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.IsJumping();
    }

    public void ResetJumping()
    {
        _anim.ResetTrigger("IsJumping");
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.ResetJumping();
    }
    
    public void IsShooting()
    {        
        _anim.SetTrigger("IsShooting");
    }

    public void SetShootIndex(int index)
    {
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        if (controller.WeaponType != WeaponType.Melee) return;
        
        _anim.SetInteger("ShootIndex", index);
        controller.SetShotIndex(index);
    }

    public void GrenadeID(int grenadeID)
    {
        _anim.SetInteger("GrenadeID", grenadeID);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.GrenadeID(grenadeID);
    }
    
    public void Move(Vector2 move)
    {
        _anim.SetFloat("MoveX", move.x);
        _anim.SetFloat("MoveY", move.y);
        if (_w_manager == null) return;
        WeaponController controller = ActiveCached();
        if (controller == null) return;
        controller.Move(move);
    }
    
    public void WeaponIndex(int index)
        => _anim?.SetInteger("WeaponIndex", index);

    public GunRecoilController.FiringMode ActiveWeaponMode()
    {
        if (_w_manager == null) return GunRecoilController.FiringMode.Semi;
        WeaponController controller = ActiveCached();
        if (controller != null) return controller.GunRecoilCTRL.GetFiringMode();
        return GunRecoilController.FiringMode.Semi;
    }

    public WeaponController ActiveWeponCTRL()
    {
        if (_w_manager == null)
        {
            Debug.Log("<color=red> WeaponController not found!</color>", this);
            return null;
        }
        return ActiveCached();
    }

    public void ModeSwitch()
    {
        ActiveCached().SwitchMode();
    }
}
