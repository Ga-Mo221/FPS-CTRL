using UnityEngine;

public class PlayerMotionController : MonoBehaviour
{
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
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsDead(value);
    }
    
    public void IsCrouching(bool value)
    {
        _anim.SetBool("IsCrouching", value);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsCrouching(value);
    }
    
    public void IsSliding(bool value)
    {
        _anim.SetBool("IsSliding", value);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsSliding(value);
    }
    
    public void ADS(bool value)
    {
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
         if (controller.ADS(value))
            _anim.SetBool("ADS", value);
    }
    
    public bool IsReloading(bool value)
    {
        if (_w_manager == null) return false;
        WeaponController controller = _w_manager.Weapon_Active();
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
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsThrow(value);
    }
    
    public void IsSwitching(bool value)
    {
        _anim.SetBool("IsSwitching", value);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsSwitching(value);
    }
    
    public void IsGrouded(bool value)
    {
        _anim.SetBool("IsGrouded", value);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsGrouded(value);
    }
    
    public void IsJumping()
    {
        _anim.SetTrigger("IsJumping");
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.IsJumping();
    }
    
    public void IsShooting()
    {
        _anim.SetTrigger("IsShooting");
    }

    public void GrenadeID(int grenadeID)
    {
        _anim.SetInteger("GrenadeID", grenadeID);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.GrenadeID(grenadeID);
    }
    
    public void Move(Vector2 move)
    {
        _anim.SetFloat("MoveX", move.x);
        _anim.SetFloat("MoveY", move.y);
        if (_w_manager == null) return;
        WeaponController controller = _w_manager.Weapon_Active();
        if (controller == null) return;
        controller.Move(move);
    }
    
    public void WeaponIndex(int index)
        => _anim?.SetInteger("WeaponIndex", index);

    public GunRecoilController.FiringMode ActiveWeaponMode()
    {
        if (_w_manager == null) return GunRecoilController.FiringMode.Semi;
        WeaponController controller = _w_manager.Weapon_Active();
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
        return _w_manager.Weapon_Active();
    }

    public void ModeSwitch()
    {
        _w_manager.Weapon_Active().SwitchMode();
    }
}
