using UnityEngine;

public class MotionCall : StateMachineBehaviour
{
    // Char
    public void Switch(Animator animator)
    {
        var Manager = animator.GetComponentInParent<WeaponManager>();

        if (Manager  == null)
        {
            Debug.LogWarning("WeaponManager not found in parent of animator.");
            return;
        }

        Manager.SwitchWeapon();
    }

    // Char
    public void ResetReload(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        var weaponManager = animator.GetComponentInParent<WeaponManager>();
        
        controller?.StopReload();
        weaponManager?.Weapon_Active()?.Reload();
    }

    // ===================================== WEAPON =========================================================
    
    // Gun
    public void WeaponMagazine(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.MagazineInClip,
            controller.Data.Volume_Magazine);
    }
    
    // Gun
    public void WeaponSlide(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.SlideClip,
            controller.Data.Volume_Slide);
    }
}